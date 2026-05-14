using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using CommonUnderstanding.Data;
using CommonUnderstanding.Models;

namespace CommonUnderstanding.Services;

/// <summary>
/// Computes confidence propagation from individual propositions through syllogisms
/// to an overall claim confidence, then generates a decision recommendation and
/// persists an AdjudicationSummary.
/// </summary>
public class AdjudicationEngine
{
    private readonly ApplicationDbContext _db;
    private readonly SemanticKernelService _kernelService;
    private readonly CommonUnderstandingService _cuService;
    private readonly ILogger<AdjudicationEngine> _logger;

    // Recommendation thresholds
    private const double ProceedThreshold = 0.70;
    private const double InvestigateThreshold = 0.45;

    public AdjudicationEngine(
        ApplicationDbContext db,
        SemanticKernelService kernelService,
        CommonUnderstandingService cuService,
        ILogger<AdjudicationEngine> logger)
    {
        _db = db;
        _kernelService = kernelService;
        _cuService = cuService;
        _logger = logger;
    }

    /// <summary>
    /// Runs a full adjudication pass for the given argument:
    /// 1. Recalculates per-proposition confidence from evidence
    /// 2. Propagates confidence through syllogisms
    /// 3. Computes overall claim confidence
    /// 4. Generates a decision recommendation
    /// 5. Creates or updates the AdjudicationSummary
    /// </summary>
    public async Task<AdjudicationSummary> AdjudicateAsync(int argumentId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Running adjudication for argument {ArgumentId}", argumentId);

        var argument = await _db.Arguments
            .Include(a => a.Claims)
                .ThenInclude(c => c.Premises)
                    .ThenInclude(p => p.EvidenceItems)
            .Include(a => a.Claims)
                .ThenInclude(c => c.Syllogisms)
            .Include(a => a.Claims)
                .ThenInclude(c => c.Assumptions)
            .Include(a => a.Claims)
                .ThenInclude(c => c.Rebuttals)
            .Include(a => a.AdjudicationSummary)
            .FirstOrDefaultAsync(a => a.Id == argumentId)
            ?? throw new InvalidOperationException($"Argument {argumentId} not found.");

        // ── Step 1: Recalculate all proposition confidences ───────────────────
        var allPropositions = argument.Claims
            .SelectMany(c => c.Premises)
            .ToList();

        foreach (var p in allPropositions)
        {
            var confidence = CalculatePropositionConfidence(p.EvidenceItems.ToList(), p.ProvisionalConfidence);
            p.ConfidenceScore = confidence;
            p.Status = DeterminePropositionStatus(p.EvidenceItems.ToList(), confidence);
            p.EvidenceCount = p.EvidenceItems.Count;
        }

        await _db.SaveChangesAsync();

        // ── Step 2: Evidence gaps and conflicts ───────────────────────────────
        var evidenceGaps = allPropositions
            .Where(p => p.EvidenceCount == 0)
            .Select(p => p.Id)
            .ToList();

        var conflictingPropositions = allPropositions
            .Where(p => p.EvidenceItems.Any(e => e.Direction == EvidenceDirection.Supports) &&
                        p.EvidenceItems.Any(e => e.Direction == EvidenceDirection.Opposes))
            .Select(p => p.Id)
            .ToList();

        // ── Step 3: Overall claim confidence ─────────────────────────────────
        double overallConfidence;
        if (!allPropositions.Any())
        {
            overallConfidence = 0.5; // No structure yet
        }
        else
        {
            // Weight each proposition by its evidence count (min weight 0.1)
            double totalWeight = 0;
            double weightedSum = 0;
            foreach (var p in allPropositions)
            {
                double weight = Math.Max(0.1, p.EvidenceCount);
                totalWeight += weight;
                weightedSum += weight * p.ConfidenceScore;
            }
            overallConfidence = totalWeight > 0 ? weightedSum / totalWeight : 0.5;
        }

        // ── Step 4: Rebuttal and assumption risk ─────────────────────────────
        var highStrengthRebuttals = argument.Claims
            .SelectMany(c => c.Rebuttals)
            .Any(r => r.Strength?.Equals("high", StringComparison.OrdinalIgnoreCase) == true);

        var criticalUnsupportedAssumptions = argument.Claims
            .SelectMany(c => c.Assumptions)
            .Any(a => a.IsCritical && !a.IsSupported);

        // ── Step 5: Generate recommendation ──────────────────────────────────
        var recommendation = DetermineRecommendation(
            overallConfidence,
            evidenceGaps.Count,
            allPropositions.Count,
            highStrengthRebuttals,
            criticalUnsupportedAssumptions);

        // ── Step 6: Reasoning trace + narrative + next steps (1 LLM call) ────
        var reasoningTrace = BuildReasoningTrace(
            overallConfidence, allPropositions.Count,
            evidenceGaps.Count, conflictingPropositions.Count,
            highStrengthRebuttals, criticalUnsupportedAssumptions,
            recommendation);

        var (detailedNarrative, nextSteps) = await GenerateNarrativeAndNextStepsAsync(
            argument, allPropositions, overallConfidence, recommendation,
            evidenceGaps, conflictingPropositions,
            highStrengthRebuttals, criticalUnsupportedAssumptions,
            cancellationToken);

        // ── Step 7: Persist ───────────────────────────────────────────────────
        var summary = argument.AdjudicationSummary;
        if (summary == null)
        {
            summary = new AdjudicationSummary { ArgumentId = argumentId };
            _db.AdjudicationSummaries.Add(summary);
        }

        summary.OverallConfidence = Math.Round(overallConfidence, 3);
        summary.Recommendation = recommendation;
        summary.ReasoningTrace = reasoningTrace;
        summary.EvidenceGapsJson = System.Text.Json.JsonSerializer.Serialize(evidenceGaps);
        summary.ConflictingEvidenceJson = System.Text.Json.JsonSerializer.Serialize(conflictingPropositions);
        summary.NextSteps = nextSteps;
        summary.DetailedNarrative = detailedNarrative;
        summary.ComputedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        // Sync into Common Understanding Graph
        await _cuService.SyncFromArgumentAsync(argumentId);

        _logger.LogInformation(
            "Adjudication complete for argument {ArgumentId}: {Recommendation} ({Confidence:P0})",
            argumentId, recommendation, overallConfidence);

        return summary;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Core calculations
    // ─────────────────────────────────────────────────────────────────────────

    internal static double CalculatePropositionConfidence(List<EvidenceItem> evidence, double? provisionalConfidence = null)
    {
        if (!evidence.Any()) return provisionalConfidence ?? 0.5;

        static double TierWeight(EvidenceTier t) => t switch
        {
            EvidenceTier.T1_SystematicReview => 0.95,
            EvidenceTier.T2_RCT => 0.80,
            EvidenceTier.T3_Observational => 0.60,
            EvidenceTier.T4_ExpertConsensus => 0.40,
            EvidenceTier.T5_CaseStudy => 0.25,
            EvidenceTier.T6_AnecdoteOpinion => 0.05,
            _ => 0.05
        };

        double totalWeight = 0;
        double weightedSignal = 0;

        foreach (var item in evidence)
        {
            double weight = TierWeight(item.Tier);

            // Replication multiplier
            weight *= item.ReplicationStatus?.ToLower() switch
            {
                "replicated" => 1.3,
                "partial" => 1.0,
                "unreplicated" => 0.8,
                "contradicted" => 0.5,
                _ => 1.0
            };

            // Sample size bonus for empirical studies
            if (item.SampleSize.HasValue && item.SampleSize > 0)
            {
                var sizeBonus = Math.Log10(Math.Max(10, item.SampleSize.Value)) / 5.0;
                weight *= (1 + Math.Min(0.5, sizeBonus));
            }

            double signal = item.Direction switch
            {
                EvidenceDirection.Supports => 1.0,
                EvidenceDirection.Opposes => -1.0,
                EvidenceDirection.Neutral => 0.0,
                _ => 0.0
            };

            totalWeight += weight;
            weightedSignal += weight * signal;
        }

        // Map weighted signal [-totalWeight, totalWeight] → confidence [0,1]
        var normalisedSignal = totalWeight > 0 ? weightedSignal / totalWeight : 0;
        var confidence = 0.5 + normalisedSignal * 0.45;
        return Math.Round(Math.Clamp(confidence, 0.05, 0.95), 3);
    }

    private static PropositionStatus DeterminePropositionStatus(List<EvidenceItem> evidence, double confidence)
    {
        if (!evidence.Any()) return PropositionStatus.Unevaluated;

        bool hasSupporting = evidence.Any(e => e.Direction == EvidenceDirection.Supports);
        bool hasOpposing = evidence.Any(e => e.Direction == EvidenceDirection.Opposes);

        if (hasSupporting && hasOpposing) return PropositionStatus.Contested;
        if (confidence >= 0.70 || confidence <= 0.30) return PropositionStatus.Settled;
        return PropositionStatus.Unknown;
    }

    private static DecisionRecommendation DetermineRecommendation(
        double confidence,
        int evidenceGapCount,
        int totalPropositions,
        bool highStrengthRebuttals,
        bool criticalUnsupportedAssumptions)
    {
        // Critical assumption failure → reject regardless of confidence
        if (criticalUnsupportedAssumptions && confidence < ProceedThreshold)
            return DecisionRecommendation.Reject;

        // High confidence but structural risks → defer
        if (confidence >= ProceedThreshold && (highStrengthRebuttals || criticalUnsupportedAssumptions))
            return DecisionRecommendation.Defer;

        // High confidence, clean structure → proceed
        if (confidence >= ProceedThreshold)
            return DecisionRecommendation.Proceed;

        // Moderate confidence, or significant evidence gaps → investigate
        if (confidence >= InvestigateThreshold)
            return DecisionRecommendation.Investigate;

        // Majority of propositions have no evidence → investigate rather than reject
        double gapRatio = totalPropositions > 0 ? (double)evidenceGapCount / totalPropositions : 1;
        if (gapRatio >= 0.5)
            return DecisionRecommendation.Investigate;

        // Low confidence, evidence exists but mostly opposing
        return DecisionRecommendation.Reject;
    }

    private static string BuildReasoningTrace(
        double confidence,
        int totalPropositions, int gapCount, int conflictCount,
        bool highRebuttals, bool criticalAssumptions,
        DecisionRecommendation recommendation)
    {
        var parts = new List<string>
        {
            $"Overall claim confidence: {confidence:P0} across {totalPropositions} proposition(s)."
        };

        if (gapCount > 0)
            parts.Add($"{gapCount} proposition(s) have no evidence.");

        if (conflictCount > 0)
            parts.Add($"{conflictCount} proposition(s) have conflicting evidence.");

        if (highRebuttals)
            parts.Add("At least one high-strength rebuttal was identified.");

        if (criticalAssumptions)
            parts.Add("One or more critical underlying assumptions are unsupported.");

        parts.Add($"Recommendation: {recommendation}.");

        return string.Join(" ", parts);
    }

    private async Task<(string? Narrative, string? NextSteps)> GenerateNarrativeAndNextStepsAsync(
        Argument argument,
        List<Proposition> allPropositions,
        double overallConfidence,
        DecisionRecommendation recommendation,
        List<int> evidenceGaps,
        List<int> conflictingIds,
        bool highStrengthRebuttals,
        bool criticalUnsupportedAssumptions,
        CancellationToken cancellationToken)
    {
        try
        {
            var kernel = _kernelService.GetKernel();

            var premiseSummaries = allPropositions.Select(p =>
            {
                var status = p.Status.ToString();
                var evidenceNote = p.EvidenceCount > 0
                    ? $"{p.EvidenceCount} evidence item(s), confidence {p.ConfidenceScore:P0}"
                    : "no evidence submitted";
                var provisionalNote = !string.IsNullOrEmpty(p.ProvisionalAssessment)
                    ? $" Provisional AI assessment: {p.ProvisionalAssessment} ({p.ProvisionalConfidence:P0})"
                    : "";
                return $"- \"{p.Text}\" [{status}, {evidenceNote}]{provisionalNote}";
            });

            var rebuttals = argument.Claims
                .SelectMany(c => c.Rebuttals)
                .Select(r => $"- {r.Text} (strength: {r.Strength})");

            var assumptions = argument.Claims
                .SelectMany(c => c.Assumptions)
                .Select(a => $"- {a.Text} (critical: {(a.IsCritical ? "yes" : "no")}, supported: {(a.IsSupported ? "yes" : "no")})");

            var gapTexts = allPropositions
                .Where(p => evidenceGaps.Contains(p.Id))
                .Select(p => $"- {p.Text}");

            var conflictTexts = allPropositions
                .Where(p => conflictingIds.Contains(p.Id))
                .Select(p => $"- {p.Text}");

            var prompt = $$$"""
            Always respond in English.
            You are a senior analyst writing a detailed adjudication report.

            ARGUMENT: "{{{argument.Title}}}"
            OVERALL CONFIDENCE: {{{overallConfidence:P0}}}
            RECOMMENDATION: {{{recommendation}}}

            PREMISES AND THEIR STATUS:
            {{{string.Join("\n", premiseSummaries)}}}

            KEY ASSUMPTIONS:
            {{{string.Join("\n", assumptions)}}}

            REBUTTALS IDENTIFIED:
            {{{string.Join("\n", rebuttals)}}}

            EVIDENCE GAPS ({{{evidenceGaps.Count}}} propositions with no evidence):
            {{{string.Join("\n", gapTexts)}}}

            CONFLICTING EVIDENCE ({{{conflictingIds.Count}}} propositions):
            {{{string.Join("\n", conflictTexts)}}}

            HIGH-STRENGTH REBUTTALS: {{{(highStrengthRebuttals ? "Yes" : "No")}}}
            UNSUPPORTED CRITICAL ASSUMPTIONS: {{{(criticalUnsupportedAssumptions ? "Yes" : "No")}}}

            Produce your response in TWO clearly separated sections:

            ═══ NARRATIVE ═══
            Write a detailed, structured narrative (3-5 paragraphs) that:
            1. Summarises what the argument claims and why it matters
            2. Evaluates the strength of each key premise, noting which are well-supported and which are weak
            3. Discusses the assumptions, rebuttals, and evidence gaps that affect the conclusion
            4. Explains precisely why the recommendation of "{{{recommendation}}}" was reached
            5. Identifies what would need to change for the recommendation to be upgraded or downgraded
            Use formal, analytical language. Write flowing prose, not bullet points.

            ═══ NEXT STEPS ═══
            In 2-3 sentences, recommend what specific evidence or data collection would most
            strengthen the analysis and move the decision forward.
            """;

            var result = await kernel.InvokePromptAsync(prompt, cancellationToken: cancellationToken);
            var raw = result.ToString().Trim();

            // Split on the NEXT STEPS section marker
            string? narrative = null;
            string? nextSteps = null;

            var nextStepsIdx = raw.IndexOf("NEXT STEPS", StringComparison.OrdinalIgnoreCase);
            if (nextStepsIdx > 0)
            {
                narrative = CleanSectionText(raw[..nextStepsIdx]);
                nextSteps = CleanSectionText(raw[nextStepsIdx..]);
            }
            else
            {
                narrative = CleanSectionText(raw);
            }

            return (narrative, nextSteps);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Narrative+next-steps generation failed for argument {Id}", argument.Id);
            return (null, null);
        }
    }

    private static string? CleanSectionText(string text)
    {
        // Strip the section header lines (═══ ... ═══) and trim
        var cleaned = System.Text.RegularExpressions.Regex.Replace(
            text, @"[═]+\s*(NARRATIVE|NEXT\s*STEPS)\s*[═]+", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? null : cleaned;
    }
}
