using Microsoft.SemanticKernel;
using CommonUnderstanding.Models;
using System.Text.Json;

namespace CommonUnderstanding.Services;

/// <summary>
/// Decomposes a natural-language argument into formal logical structure using
/// a multi-step Semantic Kernel prompt chain (Toulmin model + syllogistic form).
/// </summary>
public class ArgumentDecompositionService
{
    private readonly SemanticKernelService _kernelService;
    private readonly ILogger<ArgumentDecompositionService> _logger;

    public ArgumentDecompositionService(
        SemanticKernelService kernelService,
        ILogger<ArgumentDecompositionService> logger)
    {
        _kernelService = kernelService;
        _logger = logger;
    }

    /// <summary>
    /// Decomposes an argument into claims, premises, syllogisms, assumptions,
    /// qualifiers and rebuttals.
    /// </summary>
    public async Task<DecompositionResult> DecomposeAsync(
        string argumentText,
        Func<string, int, int, Task>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Beginning argument decomposition ({Length} chars)", argumentText.Length);

        var kernel = _kernelService.GetKernel();

        // ── Step 1: Extract the central claim (fast, needed for title update) ──
        if (onProgress != null) await onProgress("Extracting central claim…", 1, 2);
        var claimText = await ExtractClaimAsync(kernel, argumentText, cancellationToken);

        // ── Step 2: Full structural decomposition (single consolidated prompt) ──
        if (onProgress != null) await onProgress("Decomposing argument structure & assessing premises…", 2, 2);
        var rawAnalysis = await FullDecompositionAsync(kernel, argumentText, claimText, cancellationToken);

        // ── Parse ────────────────────────────────────────────────────────────
        var result = ParseFullDecomposition(claimText, rawAnalysis);

        _logger.LogInformation(
            "Decomposition complete: {Premises} premises, {Syllogisms} syllogisms, {Assumptions} assumptions, {Assessments} assessments",
            result.Premises.Count, result.Syllogisms.Count, result.Assumptions.Count, result.ProvisionalAssessments.Count);

        return result;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  LLM calls (only 2 total)
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<string> ExtractClaimAsync(Kernel kernel, string argumentText, CancellationToken ct)
    {
        var prompt = $$$"""
        You are an expert in argument analysis and formal logic.

        Read the following argument and identify the SINGLE central claim it is making.
        The claim is the top-level conclusion the author wants the reader to accept.

        Argument:
        ---
        {{{argumentText}}}
        ---

        Respond with ONLY the claim as one concise sentence. No preamble.
        """;

        var result = await kernel.InvokePromptAsync(prompt, cancellationToken: ct);
        return result.ToString().Trim();
    }

    /// <summary>
    /// Single consolidated prompt that extracts premises, syllogisms, assumptions,
    /// qualifiers, rebuttals, AND provisional truth-value assessments in one pass.
    /// Replaces five+ separate LLM calls.
    /// </summary>
    private async Task<string> FullDecompositionAsync(Kernel kernel, string argumentText, string claimText, CancellationToken ct)
    {
        var prompt = $$$"""
        You are an expert in argument analysis, formal logic, and epistemology.

        CENTRAL CLAIM: {{{claimText}}}

        ARGUMENT TEXT:
        ---
        {{{argumentText}}}
        ---

        Perform a COMPLETE structured analysis. Output ALL sections below using the EXACT formats shown.
        Do NOT add any commentary, preamble, or explanation outside the tagged lines.

        ═══ SECTION 1: PREMISES ═══
        List every distinct supporting proposition (explicit or strongly implied).
        For each premise also provide a provisional truth-value assessment from your knowledge base.
        Format — one per line:
        PREMISE: [premise text] | CONFIDENCE: [0.0-1.0] | ASSESSMENT: [brief explanation citing evidence/knowledge]

        Confidence guide: 0.9-1.0 = established fact; 0.7-0.89 = strong evidence; 0.5-0.69 = plausible but debated; 0.3-0.49 = weak evidence; 0.0-0.29 = dubious/contradicted.

        ═══ SECTION 2: SYLLOGISMS ═══
        Arrange the premises into formal syllogistic chains. Format each as a block:
        SYLLOGISM:
        MAJOR: [general rule or principle]
        MINOR: [specific case]
        CONCLUSION: [what follows]
        TYPE: [deductive | inductive | abductive | analogical]

        ═══ SECTION 3: ASSUMPTIONS ═══
        Identify unstated premises the argument depends on. Up to 5 most important.
        Format — one per line:
        ASSUMPTION: [text] | CRITICAL: yes/no

        ═══ SECTION 4: QUALIFIERS AND REBUTTALS ═══
        Qualifiers — scope limits on the claim:
        QUALIFIER: [text]

        Rebuttals — conditions or counter-arguments that could defeat the conclusion:
        REBUTTAL: [text] | STRENGTH: [low/medium/high]
        """;

        var result = await kernel.InvokePromptAsync(prompt, cancellationToken: ct);
        return result.ToString().Trim();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Parsing (unified for the single mega-prompt response)
    // ─────────────────────────────────────────────────────────────────────────

    private DecompositionResult ParseFullDecomposition(string claimText, string rawAnalysis)
    {
        var result = new DecompositionResult { ClaimText = claimText };

        var lines = rawAnalysis.Split('\n');

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            // ── PREMISE lines (include provisional assessment) ──
            if (line.StartsWith("PREMISE:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split('|');
                var premiseText = parts[0].Replace("PREMISE:", "", StringComparison.OrdinalIgnoreCase).Trim();

                if (string.IsNullOrWhiteSpace(premiseText)) continue;
                result.Premises.Add(premiseText);

                double confidence = 0.5;
                string assessment = string.Empty;

                if (parts.Length >= 2)
                {
                    var confStr = parts[1].Replace("CONFIDENCE:", "", StringComparison.OrdinalIgnoreCase).Trim();
                    if (double.TryParse(confStr, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var c))
                        confidence = Math.Clamp(c, 0.0, 1.0);
                }
                if (parts.Length >= 3)
                    assessment = parts[2].Replace("ASSESSMENT:", "", StringComparison.OrdinalIgnoreCase).Trim();

                result.ProvisionalAssessments.Add(new ProvisionalAssessmentDto
                {
                    PremiseText = premiseText,
                    Confidence = confidence,
                    Assessment = assessment
                });
                continue;
            }

            // ── ASSUMPTION lines ──
            if (line.StartsWith("ASSUMPTION:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split('|');
                var text = parts[0].Replace("ASSUMPTION:", "", StringComparison.OrdinalIgnoreCase).Trim();
                var isCritical = parts.Length > 1 &&
                                 parts[1].Contains("yes", StringComparison.OrdinalIgnoreCase);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    result.Assumptions.Add(text);
                    result.CriticalAssumptions.Add(new CriticalAssumptionDto { Text = text, IsCritical = isCritical });
                }
                continue;
            }

            // ── QUALIFIER lines ──
            if (line.StartsWith("QUALIFIER:", StringComparison.OrdinalIgnoreCase))
            {
                var text = line.Split('|')[0].Replace("QUALIFIER:", "", StringComparison.OrdinalIgnoreCase).Trim();
                if (!string.IsNullOrWhiteSpace(text))
                    result.Qualifiers.Add(text);
                continue;
            }

            // ── REBUTTAL lines ──
            if (line.StartsWith("REBUTTAL:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split('|');
                var text = parts[0].Replace("REBUTTAL:", "", StringComparison.OrdinalIgnoreCase).Trim();
                var strength = "medium";
                if (parts.Length > 1)
                {
                    var sp = parts[1];
                    if (sp.Contains("high", StringComparison.OrdinalIgnoreCase)) strength = "high";
                    else if (sp.Contains("low", StringComparison.OrdinalIgnoreCase)) strength = "low";
                }
                if (!string.IsNullOrWhiteSpace(text))
                    result.Rebuttals.Add(new RebuttalDto { Text = text, Strength = strength });
                continue;
            }
        }

        // ── Parse syllogism blocks (multi-line) ──
        result.Syllogisms = ParseSyllogismBlocks(rawAnalysis);

        return result;
    }

    private static List<SyllogismDto> ParseSyllogismBlocks(string text)
    {
        var syllogisms = new List<SyllogismDto>();
        var blocks = text.Split("SYLLOGISM:", StringSplitOptions.RemoveEmptyEntries);

        foreach (var block in blocks)
        {
            if (string.IsNullOrWhiteSpace(block)) continue;

            var dto = new SyllogismDto();
            foreach (var line in block.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("MAJOR:", StringComparison.OrdinalIgnoreCase))
                    dto.MajorPremise = trimmed[6..].Trim();
                else if (trimmed.StartsWith("MINOR:", StringComparison.OrdinalIgnoreCase))
                    dto.MinorPremise = trimmed[6..].Trim();
                else if (trimmed.StartsWith("CONCLUSION:", StringComparison.OrdinalIgnoreCase))
                    dto.Conclusion = trimmed[11..].Trim();
                else if (trimmed.StartsWith("TYPE:", StringComparison.OrdinalIgnoreCase))
                {
                    var typeStr = trimmed[5..].Trim().ToLowerInvariant();
                    dto.InferenceType = typeStr switch
                    {
                        "inductive" => InferenceType.Inductive,
                        "abductive" => InferenceType.Abductive,
                        "analogical" => InferenceType.Analogical,
                        _ => InferenceType.Deductive
                    };
                }
            }

            if (!string.IsNullOrWhiteSpace(dto.Conclusion))
                syllogisms.Add(dto);
        }

        return syllogisms;
    }
}
