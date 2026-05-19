using Microsoft.SemanticKernel;
using CommonUnderstanding.Models;

namespace CommonUnderstanding.Services;

/// <summary>
/// Uses the LLM to automatically classify evidence tier and direction from citation text.
/// The user's explicit selection always takes precedence; this fills defaults or validates.
/// </summary>
public class EvidenceClassificationService
{
    private static readonly TimeSpan ClassificationTimeout = TimeSpan.FromSeconds(12);

    private readonly SemanticKernelService _kernelService;
    private readonly ILogger<EvidenceClassificationService> _logger;

    public EvidenceClassificationService(
        SemanticKernelService kernelService,
        ILogger<EvidenceClassificationService> logger)
    {
        _kernelService = kernelService;
        _logger = logger;
    }

    /// <summary>
    /// Given a citation text and the proposition it relates to, infer evidence tier,
    /// direction, and quality concerns.
    /// </summary>
    public async Task<EvidenceClassification> ClassifyAsync(
        string citation,
        string propositionText,
        string? abstractOrSummary = null)
    {
        _logger.LogInformation("Classifying evidence: {Citation}", citation[..Math.Min(80, citation.Length)]);

        var kernel = _kernelService.GetKernel();

        var context = string.IsNullOrWhiteSpace(abstractOrSummary)
            ? string.Empty
            : $"\n\nAbstract/summary:\n{abstractOrSummary}";

        var prompt = $$$"""
        Always respond in English.
        You are an expert in research methodology and evidence evaluation.

        Proposition being evaluated:
        "{{{propositionText}}}"

        Evidence citation:
        "{{{citation}}}"
        {{{context}}}

        Classify this evidence on two dimensions:

        1. STUDY_TYPE: Identify the study design. Choose ONE:
           - systematic_review (meta-analysis, systematic literature review)
           - rct (randomized controlled trial, controlled experiment)
           - observational (cohort study, case-control, cross-sectional, survey)
           - expert_consensus (guideline, position statement, expert panel)
           - case_study (single case, qualitative study, interview-based)
           - anecdote (personal account, blog, opinion piece, unverified claim)
           - unknown

        2. DIRECTION: Does this evidence support or oppose the proposition?
           - supports
           - opposes
           - neutral

        3. QUALITY_CONCERNS: Name any quality concerns in one brief sentence, or "None".

        Respond in exactly this format:
        STUDY_TYPE: [value]
        DIRECTION: [value]
        QUALITY_CONCERNS: [text]
        """;

        try
        {
            using var timeoutCts = new CancellationTokenSource(ClassificationTimeout);
            var result = await kernel.InvokePromptAsync(prompt, cancellationToken: timeoutCts.Token);
            return ParseClassification(result.ToString());
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Evidence classification timed out after {Seconds}s; returning defaults", ClassificationTimeout.TotalSeconds);
            return new EvidenceClassification
            {
                Tier = EvidenceTier.T5_CaseStudy,
                Direction = EvidenceDirection.Neutral,
                QualityConcerns = "Auto-classification timed out."
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Evidence classification failed; returning defaults");
            return new EvidenceClassification
            {
                Tier = EvidenceTier.T5_CaseStudy,
                Direction = EvidenceDirection.Neutral,
                QualityConcerns = "Auto-classification unavailable."
            };
        }
    }

    private EvidenceClassification ParseClassification(string text)
    {
        var result = new EvidenceClassification();

        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("STUDY_TYPE:", StringComparison.OrdinalIgnoreCase))
            {
                var val = trimmed[11..].Trim().ToLowerInvariant();
                result.Tier = val switch
                {
                    "systematic_review" => EvidenceTier.T1_SystematicReview,
                    "rct" => EvidenceTier.T2_RCT,
                    "observational" => EvidenceTier.T3_Observational,
                    "expert_consensus" => EvidenceTier.T4_ExpertConsensus,
                    "case_study" => EvidenceTier.T5_CaseStudy,
                    _ => EvidenceTier.T6_AnecdoteOpinion
                };
            }
            else if (trimmed.StartsWith("DIRECTION:", StringComparison.OrdinalIgnoreCase))
            {
                var val = trimmed[10..].Trim().ToLowerInvariant();
                result.Direction = val switch
                {
                    "supports" => EvidenceDirection.Supports,
                    "opposes" => EvidenceDirection.Opposes,
                    _ => EvidenceDirection.Neutral
                };
            }
            else if (trimmed.StartsWith("QUALITY_CONCERNS:", StringComparison.OrdinalIgnoreCase))
            {
                result.QualityConcerns = trimmed[17..].Trim();
            }
        }

        return result;
    }
}

public class EvidenceClassification
{
    public EvidenceTier Tier { get; set; } = EvidenceTier.T5_CaseStudy;
    public EvidenceDirection Direction { get; set; } = EvidenceDirection.Neutral;
    public string? QualityConcerns { get; set; }
}
