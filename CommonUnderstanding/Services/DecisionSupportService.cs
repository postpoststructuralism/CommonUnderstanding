using Microsoft.EntityFrameworkCore;
using CommonUnderstanding.Data;
using CommonUnderstanding.Models;

namespace CommonUnderstanding.Services;

/// <summary>
/// Synthesizes evidence adjudication + stakeholder consensus into an
/// actionable decision recommendation with a full reasoning trace.
/// </summary>
public class DecisionSupportService
{
    private readonly ApplicationDbContext _db;
    private readonly StakeholderService _stakeholderService;
    private readonly ILogger<DecisionSupportService> _logger;

    public DecisionSupportService(
        ApplicationDbContext db,
        StakeholderService stakeholderService,
        ILogger<DecisionSupportService> logger)
    {
        _db = db;
        _stakeholderService = stakeholderService;
        _logger = logger;
    }

    /// <summary>
    /// Generates a full decision support result for the given argument,
    /// integrating Bayesian evidence confidence with stakeholder consensus.
    /// </summary>
    public async Task<DecisionSupportResult> GenerateAsync(int argumentId)
    {
        // Load argument with all linked data
        var argument = await _db.Arguments
            .Include(a => a.Claims)
                .ThenInclude(c => c.Premises)
                    .ThenInclude(p => p.EvidenceItems)
            .Include(a => a.AdjudicationSummary)
            .FirstOrDefaultAsync(a => a.Id == argumentId);

        if (argument == null)
            throw new ArgumentException($"Argument {argumentId} not found.");

        var adjudication = argument.AdjudicationSummary;
        var baseConfidence = adjudication?.OverallConfidence ?? 0.5;
        var baseRecommendation = adjudication?.Recommendation ?? DecisionRecommendation.Investigate;

        // Stakeholder consensus
        var consensus = await _stakeholderService.GetConsensusAsync(argumentId);
        var positions = await _stakeholderService.GetPositionsForArgumentAsync(argumentId);

        // Contested premises: premises where ≥1 stakeholder explicitly rejected them
        var allPremises = argument.Claims.SelectMany(c => c.Premises).ToList();
        var contestedPremises = BuildContestedPremises(positions, allPremises);

        // Adjust recommendation based on stakeholder alignment
        var finalRecommendation = AdjustRecommendation(baseRecommendation, baseConfidence, consensus);

        // Confidence level label
        string confidenceLevel = baseConfidence >= 0.80 ? "High"
                                : baseConfidence >= 0.60 ? "Moderate"
                                : baseConfidence >= 0.40 ? "Low"
                                : "Very Low";

        // Reasoning trace
        var trace = BuildReasoningTrace(baseConfidence, consensus, finalRecommendation, baseRecommendation, contestedPremises);

        // Suggested discussion topics
        var discussionTopics = BuildDiscussionTopics(contestedPremises, adjudication, consensus);

        return new DecisionSupportResult
        {
            ArgumentId           = argumentId,
            FinalRecommendation  = finalRecommendation,
            ConfidenceLevel      = confidenceLevel,
            BaseConfidence       = baseConfidence,
            StakeholderConsensus = consensus,
            ContestedPremises    = contestedPremises,
            SuggestedDiscussionTopics = discussionTopics,
            ReasoningTrace       = trace,
            GeneratedAt          = DateTime.UtcNow
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Private helpers
    // ─────────────────────────────────────────────────────────────────────────

    private DecisionRecommendation AdjustRecommendation(
        DecisionRecommendation baseRec,
        double baseConfidence,
        StakeholderConsensus consensus)
    {
        if (consensus.TotalResponses == 0)
            return baseRec; // no stakeholder input → keep evidence-based recommendation

        // Strong stakeholder opposition overrides a Proceed recommendation
        if (baseRec == DecisionRecommendation.Proceed)
        {
            double oppositionRate = consensus.TotalResponses > 0
                ? (double)consensus.OpposeCount / consensus.TotalResponses : 0;

            if (oppositionRate >= 0.50)
                return DecisionRecommendation.Defer; // Majority oppose → defer
            if (oppositionRate >= 0.33)
                return DecisionRecommendation.Investigate; // Minority opposition → investigate
        }

        // Strong stakeholder support can upgrade Investigate → Proceed (when confidence is near threshold)
        if (baseRec == DecisionRecommendation.Investigate && consensus.MajorityPosition == "Support")
        {
            double supportRate = (double)consensus.SupportCount / consensus.TotalResponses;
            if (supportRate >= 0.75 && baseConfidence >= 0.60)
                return DecisionRecommendation.Proceed;
        }

        return baseRec;
    }

    private List<ContestedPremise> BuildContestedPremises(
        List<StakeholderPosition> positions,
        List<Proposition> premises)
    {
        var result = new List<ContestedPremise>();

        foreach (var premise in premises)
        {
            int rejectCount   = 0;
            int acceptCount   = 0;
            var rejectors     = new List<string>();
            var acceptors     = new List<string>();

            foreach (var pos in positions)
            {
                var rejected = System.Text.Json.JsonSerializer.Deserialize<List<int>>(pos.RejectedPremiseIdsJson)
                               ?? new List<int>();
                var accepted = System.Text.Json.JsonSerializer.Deserialize<List<int>>(pos.AcceptedPremiseIdsJson)
                               ?? new List<int>();

                if (rejected.Contains(premise.Id))
                {
                    rejectCount++;
                    if (!pos.IsAnonymous && pos.StakeholderRef != null)
                        rejectors.Add(pos.StakeholderRef.Name);
                }
                if (accepted.Contains(premise.Id))
                {
                    acceptCount++;
                    if (!pos.IsAnonymous && pos.StakeholderRef != null)
                        acceptors.Add(pos.StakeholderRef.Name);
                }
            }

            if (rejectCount > 0)
            {
                result.Add(new ContestedPremise
                {
                    PropositionId  = premise.Id,
                    Text           = premise.Text,
                    RejectCount    = rejectCount,
                    AcceptCount    = acceptCount,
                    RejectedBy     = rejectors,
                    AcceptedBy     = acceptors
                });
            }
        }

        return result.OrderByDescending(p => p.RejectCount).ToList();
    }

    private static string BuildReasoningTrace(
        double baseConfidence,
        StakeholderConsensus consensus,
        DecisionRecommendation final,
        DecisionRecommendation baseRec,
        List<ContestedPremise> contested)
    {
        var parts = new List<string>();

        parts.Add($"Evidence confidence: {baseConfidence:P0} → evidence-based recommendation: {baseRec}.");

        if (consensus.TotalResponses > 0)
        {
            parts.Add($"Stakeholder input: {consensus.SupportCount} support, " +
                      $"{consensus.OpposeCount} oppose, {consensus.UndecidedCount} undecided " +
                      $"(consensus rate {consensus.ConsensusRate:P0}, majority: {consensus.MajorityPosition}).");
        }
        else
        {
            parts.Add("No stakeholder positions recorded.");
        }

        if (contested.Any())
            parts.Add($"{contested.Count} premise(s) are contested by at least one stakeholder.");

        if (final != baseRec)
            parts.Add($"Stakeholder alignment adjusted recommendation from {baseRec} → {final}.");

        return string.Join(" ", parts);
    }

    private static List<string> BuildDiscussionTopics(
        List<ContestedPremise> contested,
        AdjudicationSummary? adjudication,
        StakeholderConsensus consensus)
    {
        var topics = new List<string>();

        foreach (var p in contested.Take(3))
            topics.Add($"Resolve disagreement on: \"{TruncateText(p.Text, 80)}\"");

        if (!string.IsNullOrWhiteSpace(adjudication?.EvidenceGapsJson))
            topics.Add($"Fill evidence gaps identified during adjudication.");

        if (!string.IsNullOrWhiteSpace(adjudication?.ConflictingEvidenceJson))
            topics.Add($"Reconcile conflicting evidence identified during adjudication.");

        if (consensus.TotalResponses > 0 && consensus.OpposeCount > 0 && !consensus.HasConsensus)
            topics.Add("Facilitate a structured dialogue session to address stakeholder opposition.");

        return topics;
    }

    private static string TruncateText(string text, int maxLength)
        => text.Length <= maxLength ? text : text[..maxLength] + "…";
}

// ─────────────────────────────────────────────────────────────────────────────
//  Result DTO
// ─────────────────────────────────────────────────────────────────────────────

public class DecisionSupportResult
{
    public int ArgumentId { get; set; }
    public DecisionRecommendation FinalRecommendation { get; set; }
    public string ConfidenceLevel { get; set; } = "Unknown";
    public double BaseConfidence { get; set; }
    public StakeholderConsensus StakeholderConsensus { get; set; } = new();
    public List<ContestedPremise> ContestedPremises { get; set; } = new();
    public List<string> SuggestedDiscussionTopics { get; set; } = new();
    public string ReasoningTrace { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
}

public class ContestedPremise
{
    public int PropositionId   { get; set; }
    public string Text         { get; set; } = string.Empty;
    public int AcceptCount     { get; set; }
    public int RejectCount     { get; set; }
    public List<string> AcceptedBy { get; set; } = new();
    public List<string> RejectedBy { get; set; } = new();
}
