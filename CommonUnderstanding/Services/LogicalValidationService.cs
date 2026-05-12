using Microsoft.SemanticKernel;
using CommonUnderstanding.Models;

namespace CommonUnderstanding.Services;

/// <summary>
/// Validates the logical form of syllogisms and detects informal fallacies.
/// </summary>
public class LogicalValidationService
{
    private readonly SemanticKernelService _kernelService;
    private readonly ILogger<LogicalValidationService> _logger;

    // Well-known valid syllogistic forms (mood + figure combinations)
    private static readonly HashSet<string> ValidSyllogisticForms = new(StringComparer.OrdinalIgnoreCase)
    {
        "AAA-1", "EAE-1", "AII-1", "EIO-1",  // Figure 1 (Barbara, Celarent, Darii, Ferio)
        "EAE-2", "AEE-2", "EIO-2", "AOO-2",  // Figure 2 (Cesare, Camestres, Festino, Baroco)
        "AII-3", "IAI-3", "EIO-3", "OAO-3",  // Figure 3 (Darapti*, Datisi, Felapton*, Bocardo)
        "AEE-4", "IAI-4", "EIO-4"             // Figure 4 (Bramantip*, Dimaris, Fesapo*)
    };

    private static readonly string[] CommonFallacies =
    [
        "ad hominem",
        "straw man",
        "false dichotomy",
        "appeal to authority",
        "circular reasoning",
        "slippery slope",
        "equivocation",
        "hasty generalization",
        "post hoc ergo propter hoc",
        "appeal to emotion",
        "bandwagon",
        "red herring",
        "tu quoque",
        "false analogy",
        "composition/division",
        "begging the question",
        "loaded question",
        "appeal to ignorance"
    ];

    public LogicalValidationService(
        SemanticKernelService kernelService,
        ILogger<LogicalValidationService> logger)
    {
        _kernelService = kernelService;
        _logger = logger;
    }

    /// <summary>
    /// Validates an entire decomposition result: checks syllogism forms and
    /// scans the full argument text for informal fallacies.
    /// </summary>
    public async Task<ValidationReport> ValidateAsync(
        DecompositionResult decomposition,
        string originalArgumentText,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Validating argument with {Count} syllogisms", decomposition.Syllogisms.Count);

        var syllogismResults = new List<SyllogismValidation>();
        foreach (var s in decomposition.Syllogisms)
            syllogismResults.Add(ValidateSyllogismForm(s));

        var fallacyReport = await DetectFallaciesAsync(originalArgumentText, decomposition.ClaimText, cancellationToken);

        return new ValidationReport
        {
            SyllogismValidations = syllogismResults,
            FallaciesDetected = fallacyReport,
            OverallFormValid = syllogismResults.All(s => s.IsValid) || !syllogismResults.Any(),
            ValidationNotes = BuildValidationNotes(syllogismResults, fallacyReport)
        };
    }

    /// <summary>
    /// Validates the logical form of a single syllogism using rule-based checks.
    /// Does NOT evaluate truth — only structural validity.
    /// </summary>
    public SyllogismValidation ValidateSyllogismForm(SyllogismDto syllogism)
    {
        var issues = new List<string>();

        // Basic completeness check
        if (string.IsNullOrWhiteSpace(syllogism.MajorPremise))
            issues.Add("Missing major premise.");
        if (string.IsNullOrWhiteSpace(syllogism.MinorPremise))
            issues.Add("Missing minor premise.");
        if (string.IsNullOrWhiteSpace(syllogism.Conclusion))
            issues.Add("Missing conclusion.");

        // Inductive / abductive / analogical inferences are not formally invalid —
        // they are evaluated differently (strength of evidence, not deductive validity).
        bool isValid;
        if (syllogism.InferenceType != InferenceType.Deductive)
        {
            isValid = !issues.Any();
        }
        else
        {
            // For deductive: run structural checks
            isValid = !issues.Any() && CheckDeductiveStructure(syllogism, issues);
        }

        return new SyllogismValidation
        {
            Syllogism = syllogism,
            IsValid = isValid,
            Issues = issues
        };
    }

    /// <summary>
    /// Detects informal fallacies in argument text using the LLM.
    /// </summary>
    public async Task<List<FallacyFinding>> DetectFallaciesAsync(string argumentText, string claimText, CancellationToken cancellationToken = default)
    {
        var kernel = _kernelService.GetKernel();

        var fallacyList = string.Join(", ", CommonFallacies);

        var prompt = $$$"""
        You are an expert in informal logic and critical thinking.

        Central claim: {{{claimText}}}

        Argument text:
        ---
        {{{argumentText}}}
        ---

        Identify any of the following logical fallacies that appear in this argument:
        {{{fallacyList}}}

        For each fallacy found:
        - Name the fallacy
        - Quote the specific passage that contains it (max 30 words)
        - Briefly explain why it is that fallacy (one sentence)

        Format each finding as:
        FALLACY: [name]
        PASSAGE: [quoted text]
        REASON: [explanation]

        If no fallacies are detected, respond with: NONE

        Do not invent fallacies. Only report clear instances.
        """;

        var result = await kernel.InvokePromptAsync(prompt, cancellationToken: cancellationToken);
        return ParseFallacyFindings(result.ToString());
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Private helpers
    // ─────────────────────────────────────────────────────────────────────────

    private bool CheckDeductiveStructure(SyllogismDto syllogism, List<string> issues)
    {
        // Check that the conclusion doesn't introduce entirely new concepts
        // not present in any premise (undistributed middle / illicit process heuristic).
        // This is a simplified heuristic — full syllogistic analysis needs term extraction.

        var major = syllogism.MajorPremise.ToLowerInvariant();
        var minor = syllogism.MinorPremise.ToLowerInvariant();
        var conclusion = syllogism.Conclusion.ToLowerInvariant();

        // Extract key nouns/phrases as simple word tokens (≥5 chars, skip stop words)
        var stopWords = new HashSet<string> { "the", "this", "that", "with", "from", "will", "have", "when", "than", "does", "does", "some", "every", "all", "are", "is", "an", "a" };
        var premiseWords = (major + " " + minor)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim('.', ',', ';', ':'))
            .Where(w => w.Length >= 5 && !stopWords.Contains(w))
            .ToHashSet();

        var conclusionWords = conclusion
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim('.', ',', ';', ':'))
            .Where(w => w.Length >= 5 && !stopWords.Contains(w))
            .ToList();

        // If the conclusion introduces entirely foreign content, flag it
        var novelWords = conclusionWords.Where(w => !premiseWords.Contains(w)).ToList();
        if (novelWords.Count > conclusionWords.Count * 0.6 && conclusionWords.Count > 3)
        {
            issues.Add("Conclusion may introduce concepts not grounded in the premises.");
            return false;
        }

        return true;
    }

    private List<FallacyFinding> ParseFallacyFindings(string text)
    {
        var findings = new List<FallacyFinding>();

        if (text.Contains("NONE", StringComparison.OrdinalIgnoreCase) &&
            !text.Contains("FALLACY:", StringComparison.OrdinalIgnoreCase))
            return findings;

        var blocks = text.Split("FALLACY:", StringSplitOptions.RemoveEmptyEntries);
        foreach (var block in blocks)
        {
            if (string.IsNullOrWhiteSpace(block)) continue;

            var finding = new FallacyFinding();
            foreach (var line in block.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (!trimmed.Contains(':')) { finding.FallacyName = trimmed.Trim(); continue; }
                var sep = trimmed.IndexOf(':');
                var key = trimmed[..sep].ToUpperInvariant().Trim();
                var value = trimmed[(sep + 1)..].Trim();
                switch (key)
                {
                    case "FALLACY": finding.FallacyName = value; break;
                    case "PASSAGE": finding.Passage = value; break;
                    case "REASON": finding.Explanation = value; break;
                }
            }

            if (!string.IsNullOrWhiteSpace(finding.FallacyName))
                findings.Add(finding);
        }

        return findings;
    }

    private string BuildValidationNotes(
        List<SyllogismValidation> syllogismResults,
        List<FallacyFinding> fallacies)
    {
        var notes = new System.Text.StringBuilder();

        var invalidSyllogisms = syllogismResults.Where(s => !s.IsValid).ToList();
        if (invalidSyllogisms.Any())
        {
            notes.AppendLine($"{invalidSyllogisms.Count} syllogism(s) have structural issues:");
            foreach (var s in invalidSyllogisms)
                notes.AppendLine("  • " + string.Join("; ", s.Issues));
        }

        if (fallacies.Any())
        {
            notes.AppendLine($"{fallacies.Count} informal fallacy/fallacies detected:");
            foreach (var f in fallacies)
                notes.AppendLine($"  • {f.FallacyName}: {f.Explanation}");
        }

        if (notes.Length == 0)
            notes.AppendLine("No structural issues or fallacies detected.");

        return notes.ToString().Trim();
    }
}

// ─────────────────────────────────────────────
//  Result types
// ─────────────────────────────────────────────

public class ValidationReport
{
    public List<SyllogismValidation> SyllogismValidations { get; set; } = new();
    public List<FallacyFinding> FallaciesDetected { get; set; } = new();
    public bool OverallFormValid { get; set; }
    public string ValidationNotes { get; set; } = string.Empty;
}

public class SyllogismValidation
{
    public SyllogismDto Syllogism { get; set; } = new();
    public bool IsValid { get; set; }
    public List<string> Issues { get; set; } = new();
}

public class FallacyFinding
{
    public string FallacyName { get; set; } = string.Empty;
    public string Passage { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
}
