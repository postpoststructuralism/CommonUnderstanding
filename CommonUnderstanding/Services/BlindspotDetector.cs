using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using CommonUnderstanding.Data;
using CommonUnderstanding.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CommonUnderstanding.Services;

/// <summary>
/// Detects blindspots in the community's argument inventory by traversing
/// the Common Understanding Graph, evidence data, and assumption records.
/// Phase 1: graph/database analysis only — no LLM calls.
/// </summary>
public class BlindspotDetector
{
    private readonly ApplicationDbContext _db;
    private readonly SemanticKernelService _kernelService;
    private readonly ILogger<BlindspotDetector> _logger;

    public BlindspotDetector(
        ApplicationDbContext db,
        SemanticKernelService kernelService,
        ILogger<BlindspotDetector> logger)
    {
        _db = db;
        _kernelService = kernelService;
        _logger = logger;
    }

    /// <summary>
    /// Runs all Phase 1 blindspot detectors and returns the combined findings.
    /// </summary>
    public async Task<List<EmergentConclusion>> DetectAllAsync(CancellationToken ct = default)
    {
        var results = new List<EmergentConclusion>();

        var totalArguments = await _db.Arguments.CountAsync(ct);

        var tasks = new[]
        {
            DetectAssumptionCascadesAsync(totalArguments, ct),
            DetectEvidenceDesertsAsync(ct),
            DetectConfidenceIllusionsAsync(ct),
            DetectUnaddressedRebuttalsAsync(ct)
        };

        var all = await Task.WhenAll(tasks);
        foreach (var batch in all)
            results.AddRange(batch);

        return results.OrderByDescending(r => r.Significance).ToList();
    }

    /// <summary>
    /// Runs Phase 1 detectors PLUS LLM-powered detectors (silent contradictions).
    /// </summary>
    public async Task<List<EmergentConclusion>> DetectAllDeepAsync(CancellationToken ct = default)
    {
        var phase1 = await DetectAllAsync(ct);
        var phase2 = await DetectSilentContradictionsAsync(ct);
        return phase1.Concat(phase2).OrderByDescending(r => r.Significance).ToList();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  1. Assumption Cascade
    //  Critical unsupported assumptions that appear across multiple arguments
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<List<EmergentConclusion>> DetectAssumptionCascadesAsync(
        int totalArguments, CancellationToken ct)
    {
        var findings = new List<EmergentConclusion>();

        // Load all critical, unsupported assumptions with their parent argument IDs
        var assumptions = await _db.Assumptions
            .Where(a => a.IsCritical && !a.IsSupported)
            .Include(a => a.Claim)
            .ToListAsync(ct);

        if (assumptions.Count == 0) return findings;

        // Group by normalized text
        var groups = assumptions
            .GroupBy(a => Normalize(a.Text))
            .Where(g => g.Count() >= 2)
            .ToList();

        foreach (var group in groups)
        {
            var involvedArgumentIds = group
                .Where(a => a.Claim?.ArgumentId != null)
                .Select(a => a.Claim!.ArgumentId)
                .Distinct()
                .ToList();

            if (involvedArgumentIds.Count < 2) continue;

            var argumentTitles = await _db.Arguments
                .Where(a => involvedArgumentIds.Contains(a.Id))
                .Select(a => new { a.Id, a.Title })
                .ToListAsync(ct);

            double significance = Math.Min(1.0,
                (double)involvedArgumentIds.Count / Math.Max(totalArguments, 1) * 3.0);

            var representativeText = group.First().Text;

            findings.Add(new EmergentConclusion
            {
                Type = EmergentType.Blindspot,
                Category = EmergentCategory.AssumptionCascade,
                Title = $"Shared Untested Assumption Across {involvedArgumentIds.Count} Arguments",
                Description =
                    $"The critical assumption \"{TruncateText(representativeText, 120)}\" " +
                    $"appears in {group.Count()} places across {involvedArgumentIds.Count} arguments, " +
                    $"but has never been evidenced or tested. If this assumption is wrong, the " +
                    $"conclusions of all affected arguments may be invalid.",
                Significance = Math.Round(significance, 3),
                Confidence = 0.90,
                InvolvedArgumentIds = involvedArgumentIds,
                InvolvedArgumentTitles = argumentTitles.Select(a => a.Title).ToList(),
                InvolvedPropositionTexts = new List<string> { representativeText },
                SuggestedAction =
                    $"Submit an argument or evidence item that directly evaluates: " +
                    $"\"{TruncateText(representativeText, 100)}\". " +
                    $"Consider whether this assumption holds across all the contexts in which it is being applied."
            });
        }

        _logger.LogInformation("AssumptionCascade: found {Count} patterns", findings.Count);
        return findings;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  2. Evidence Desert
    //  Propositions referenced across arguments with zero or only anecdotal evidence
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<List<EmergentConclusion>> DetectEvidenceDesertsAsync(CancellationToken ct)
    {
        var findings = new List<EmergentConclusion>();

        // Nodes referenced by multiple arguments but lacking real evidence
        var nodes = await _db.CommonUnderstandingNodes
            .Where(n => n.EvidenceCount == 0 || n.Confidence >= 0.60)
            .ToListAsync(ct);

        var desertNodes = new List<(CommonUnderstandingNode Node, double ConfidenceGap, List<int> ArgIds)>();

        foreach (var node in nodes)
        {
            List<int> argIds;
            try { argIds = JsonSerializer.Deserialize<List<int>>(node.ArgumentIdsJson) ?? new(); }
            catch { argIds = new(); }

            if (argIds.Count == 0) continue;

            // Check whether the underlying propositions have substantive evidence
            var propositionTexts = await _db.Propositions
                .Where(p => p.Claim != null && argIds.Contains(p.Claim.ArgumentId)
                            && EF.Functions.Like(p.Text, $"%{node.NormalizedKey.Substring(0, Math.Min(30, node.NormalizedKey.Length))}%"))
                .Include(p => p.EvidenceItems)
                .ToListAsync(ct);

            bool hasSubstantiveEvidence = propositionTexts
                .SelectMany(p => p.EvidenceItems)
                .Any(e => e.Tier <= EvidenceTier.T4_ExpertConsensus);

            if (node.EvidenceCount == 0 || !hasSubstantiveEvidence)
            {
                double provisionalConfidence = propositionTexts
                    .Where(p => p.ProvisionalConfidence.HasValue)
                    .Select(p => p.ProvisionalConfidence!.Value)
                    .DefaultIfEmpty(0.5)
                    .Average();

                double evidenceBasedConfidence = node.EvidenceCount == 0 ? 0.5 : node.Confidence;
                double confidenceGap = Math.Max(0, provisionalConfidence - evidenceBasedConfidence);

                if (argIds.Count >= 2 || node.Confidence >= 0.70)
                {
                    desertNodes.Add((node, confidenceGap, argIds));
                }
            }
        }

        // Sort by (argument count × confidence gap) for significance
        foreach (var (node, gap, argIds) in desertNodes
            .OrderByDescending(d => d.ArgIds.Count * (1 + d.ConfidenceGap))
            .Take(10))
        {
            var argumentTitles = await _db.Arguments
                .Where(a => argIds.Contains(a.Id))
                .Select(a => a.Title)
                .ToListAsync(ct);

            double significance = Math.Min(1.0,
                (argIds.Count / 10.0) + (node.Confidence - 0.5));

            findings.Add(new EmergentConclusion
            {
                Type = EmergentType.Blindspot,
                Category = EmergentCategory.EvidenceDesert,
                Title = $"High-Stakes Proposition With No Substantive Evidence",
                Description =
                    $"The proposition \"{TruncateText(node.Text, 140)}\" is referenced across " +
                    $"{argIds.Count} argument(s) with a confidence of {node.Confidence:P0}, " +
                    $"but has {(node.EvidenceCount == 0 ? "no evidence at all" : "only anecdotal or expert-opinion evidence")}. " +
                    $"The community is treating this as settled without empirical grounding.",
                Significance = Math.Round(Math.Max(significance, 0.05), 3),
                Confidence = 0.85,
                InvolvedArgumentIds = argIds,
                InvolvedArgumentTitles = argumentTitles,
                InvolvedNodeIds = new List<int> { node.Id },
                InvolvedPropositionTexts = new List<string> { node.Text },
                SuggestedAction =
                    "Find and submit peer-reviewed evidence (T1–T3) for this proposition. " +
                    "If no empirical evidence exists, mark the proposition as Unknown and " +
                    "lower the confidence of all arguments that depend on it."
            });
        }

        _logger.LogInformation("EvidenceDesert: found {Count} patterns", findings.Count);
        return findings;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  3. Confidence Illusion
    //  Propositions with high confidence built entirely on low-tier evidence
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<List<EmergentConclusion>> DetectConfidenceIllusionsAsync(CancellationToken ct)
    {
        var findings = new List<EmergentConclusion>();

        // Propositions with high confidence but low-tier evidence
        var highConfidenceProps = await _db.Propositions
            .Where(p => p.ConfidenceScore >= 0.70 && p.EvidenceCount > 0)
            .Include(p => p.EvidenceItems)
            .Include(p => p.Claim)
            .ToListAsync(ct);

        foreach (var prop in highConfidenceProps)
        {
            if (!prop.EvidenceItems.Any()) continue;

            var maxTier = prop.EvidenceItems.Min(e => (int)e.Tier); // lower int = better tier
            if (maxTier <= 3) continue; // T1/T2/T3 = empirical — not an illusion

            // All evidence is T4 or worse
            var tierNames = prop.EvidenceItems
                .Select(e => e.Tier.ToString())
                .Distinct()
                .ToList();

            double gap = prop.ConfidenceScore - 0.5;
            double significance = Math.Min(1.0, gap * 2.0);

            int argumentId = prop.Claim?.ArgumentId ?? 0;
            string argumentTitle = argumentId > 0
                ? (await _db.Arguments.FindAsync(new object[] { argumentId }, ct))?.Title ?? ""
                : "";

            findings.Add(new EmergentConclusion
            {
                Type = EmergentType.Blindspot,
                Category = EmergentCategory.ConfidenceIllusion,
                Title = $"High Confidence ({prop.ConfidenceScore:P0}) Built on Non-Empirical Evidence",
                Description =
                    $"The proposition \"{TruncateText(prop.Text, 140)}\" has a confidence score of " +
                    $"{prop.ConfidenceScore:P0}, but all {prop.EvidenceItems.Count} evidence " +
                    $"item(s) are {string.Join(", ", tierNames)} — with no peer-reviewed empirical studies. " +
                    $"This confidence level is not warranted by the evidence quality.",
                Significance = Math.Round(significance, 3),
                Confidence = 0.80,
                InvolvedArgumentIds = argumentId > 0 ? new List<int> { argumentId } : new(),
                InvolvedArgumentTitles = !string.IsNullOrEmpty(argumentTitle)
                    ? new List<string> { argumentTitle } : new(),
                InvolvedPropositionIds = new List<int> { prop.Id },
                InvolvedPropositionTexts = new List<string> { prop.Text },
                SuggestedAction =
                    $"The confidence score for this proposition should be re-evaluated. " +
                    $"Submit peer-reviewed studies (T1–T3) to substantiate or revise it. " +
                    $"Until then, treat this conclusion with appropriate scepticism."
            });
        }

        _logger.LogInformation("ConfidenceIllusion: found {Count} patterns", findings.Count);
        return findings.OrderByDescending(f => f.Significance).Take(8).ToList();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  4. Unaddressed Rebuttal
    //  High-strength counter-arguments that no one has engaged with
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<List<EmergentConclusion>> DetectUnaddressedRebuttalsAsync(CancellationToken ct)
    {
        var findings = new List<EmergentConclusion>();

        var highRebuttals = await _db.Rebuttals
            .Where(r => r.Strength == "high")
            .Include(r => r.Claim)
            .ToListAsync(ct);

        if (!highRebuttals.Any()) return findings;

        // Load all proposition texts for a naive string-match cross-reference
        var allPropositionTexts = await _db.Propositions
            .Select(p => p.Text.ToLower())
            .ToListAsync(ct);

        foreach (var rebuttal in highRebuttals)
        {
            if (rebuttal.Claim == null) continue;

            // Check whether any proposition in the graph addresses this rebuttal topic
            var keyWords = ExtractKeywords(rebuttal.Text);
            bool addressed = allPropositionTexts.Any(pt =>
                keyWords.Any(kw => pt.Contains(kw, StringComparison.OrdinalIgnoreCase)));

            if (!addressed)
            {
                int argumentId = rebuttal.Claim.ArgumentId;
                string argumentTitle = (await _db.Arguments.FindAsync(new object[] { argumentId }, ct))?.Title ?? "";

                findings.Add(new EmergentConclusion
                {
                    Type = EmergentType.Blindspot,
                    Category = EmergentCategory.UnaddressedRebuttal,
                    Title = "Strong Counter-Argument Has Never Been Addressed",
                    Description =
                        $"The argument \"{argumentTitle}\" contains a high-strength rebuttal: " +
                        $"\"{TruncateText(rebuttal.Text, 140)}\". " +
                        $"No proposition in the community's argument inventory addresses this challenge. " +
                        $"Any conclusion that depends on this argument may be vulnerable.",
                    Significance = 0.65,
                    Confidence = 0.70,
                    InvolvedArgumentIds = new List<int> { argumentId },
                    InvolvedArgumentTitles = new List<string> { argumentTitle },
                    InvolvedPropositionTexts = new List<string> { rebuttal.Text },
                    SuggestedAction =
                        "Submit an argument or evidence item that directly responds to this rebuttal. " +
                        "If the rebuttal cannot be answered, the parent argument's conclusion " +
                        "should be qualified or its confidence reduced."
                });
            }
        }

        _logger.LogInformation("UnaddressedRebuttal: found {Count} patterns", findings.Count);
        return findings.Take(6).ToList();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  5. Silent Contradiction (Phase 2 — LLM)
    //  High-confidence proposition pairs that logically contradict each other
    //  but have no existing "contradicts" edge or filed comparison
    // ─────────────────────────────────────────────────────────────────────────

    internal async Task<List<EmergentConclusion>> DetectSilentContradictionsAsync(CancellationToken ct)
    {
        var findings = new List<EmergentConclusion>();

        // Collect settled / high-confidence graph nodes
        var candidates = await _db.CommonUnderstandingNodes
            .Where(n => n.Confidence >= 0.65 &&
                        (n.Status == PropositionStatus.Settled || n.Status == PropositionStatus.Unknown))
            .OrderByDescending(n => n.Confidence)
            .Take(30)   // cap for cost — compare top 30 pairs
            .ToListAsync(ct);

        if (candidates.Count < 2) return findings;

        // Load IDs already connected by a contradiction edge
        var existingContradictionPairs = await _db.CommonUnderstandingEdges
            .Where(e => e.Relationship == "contradicts")
            .Select(e => new { e.SourceNodeId, e.TargetNodeId })
            .ToListAsync(ct);

        var contradictedPairs = existingContradictionPairs
            .Select(p => (Math.Min(p.SourceNodeId, p.TargetNodeId), Math.Max(p.SourceNodeId, p.TargetNodeId)))
            .ToHashSet();

        // Build pairs not already marked as contradicting
        var pairs = new List<(CommonUnderstandingNode A, CommonUnderstandingNode B)>();
        for (int i = 0; i < candidates.Count; i++)
            for (int j = i + 1; j < candidates.Count; j++)
            {
                var a = candidates[i];
                var b = candidates[j];
                if (!contradictedPairs.Contains((Math.Min(a.Id, b.Id), Math.Max(a.Id, b.Id))))
                    pairs.Add((a, b));
            }

        if (!pairs.Any()) return findings;

        // Batch up to 15 pairs into a single LLM call
        var batchPairs = pairs.Take(15).ToList();
        var propositionLines = batchPairs
            .Select((p, idx) => $"PAIR {idx + 1}: [{p.A.Text}] vs [{p.B.Text}]");

        try
        {
            var kernel = _kernelService.GetKernel();
            var prompt = $"""
            You are an expert logician examining proposition pairs for logical contradiction.
            For each pair, determine whether the two propositions logically CONTRADICT each other
            (i.e., both cannot be true at the same time in the same context).

            Respond with ONLY lines in this format — one per pair that contradicts:
            CONTRADICTION: [pair number] | [one sentence explaining the contradiction]

            If a pair does NOT contradict, output nothing for that pair.
            Do not add any other commentary.

            PROPOSITION PAIRS:
            {string.Join("\n", propositionLines)}
            """;

            var result = await kernel.InvokePromptAsync(prompt, cancellationToken: ct);
            var raw = result.ToString();

            var matches = Regex.Matches(raw,
                @"CONTRADICTION:\s*(\d+)\s*\|\s*(.+)",
                RegexOptions.IgnoreCase);

            foreach (Match m in matches)
            {
                if (!int.TryParse(m.Groups[1].Value.Trim(), out int pairIdx)) continue;
                pairIdx--; // Convert to 0-based
                if (pairIdx < 0 || pairIdx >= batchPairs.Count) continue;

                var (nodeA, nodeB) = batchPairs[pairIdx];
                string explanation = m.Groups[2].Value.Trim();

                double avgConf = (nodeA.Confidence + nodeB.Confidence) / 2.0;
                double significance = Math.Min(1.0, avgConf * 1.3);

                List<int> argIds;
                try
                {
                    argIds = JsonSerializer.Deserialize<List<int>>(nodeA.ArgumentIdsJson) ?? new();
                    argIds.AddRange(JsonSerializer.Deserialize<List<int>>(nodeB.ArgumentIdsJson) ?? new());
                    argIds = argIds.Distinct().ToList();
                }
                catch { argIds = new(); }

                var argTitles = await _db.Arguments
                    .Where(a => argIds.Contains(a.Id))
                    .Select(a => a.Title)
                    .ToListAsync(ct);

                findings.Add(new EmergentConclusion
                {
                    Type = EmergentType.Blindspot,
                    Category = EmergentCategory.SilentContradiction,
                    Title = "Two High-Confidence Propositions Contradict Each Other",
                    Description =
                        $"The proposition \"{TruncateText(nodeA.Text, 100)}\" " +
                        $"(confidence: {nodeA.Confidence:P0}) contradicts " +
                        $"\"{TruncateText(nodeB.Text, 100)}\" " +
                        $"(confidence: {nodeB.Confidence:P0}). {explanation} " +
                        $"Neither argument has flagged this conflict.",
                    Significance = Math.Round(significance, 3),
                    Confidence = 0.75,
                    InvolvedArgumentIds = argIds,
                    InvolvedArgumentTitles = argTitles,
                    InvolvedNodeIds = new List<int> { nodeA.Id, nodeB.Id },
                    InvolvedPropositionTexts = new List<string> { nodeA.Text, nodeB.Text },
                    SuggestedAction =
                        "Run a head-to-head argument comparison to formally document this contradiction. " +
                        "One of these propositions must be revisited — the community cannot proceed " +
                        "as though both are true simultaneously."
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Silent contradiction LLM detection failed");
        }

        _logger.LogInformation("SilentContradiction: found {Count} patterns", findings.Count);
        return findings;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static string Normalize(string text) =>
        Regex.Replace(text.Trim().ToLowerInvariant(), @"\s+", " ")
             .TrimEnd('.', ',', ';', ':');

    private static string TruncateText(string text, int max) =>
        text.Length <= max ? text : text[..max] + "…";

    private static List<string> ExtractKeywords(string text)
    {
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "a", "an", "is", "are", "was", "were", "be", "been", "being",
            "have", "has", "had", "do", "does", "did", "will", "would", "could",
            "should", "may", "might", "can", "to", "of", "in", "on", "at", "by",
            "for", "with", "about", "that", "this", "it", "its", "not", "but",
            "and", "or", "if", "then", "when", "which", "who", "what", "how"
        };

        return Regex.Split(text.ToLowerInvariant(), @"\W+")
            .Where(w => w.Length > 4 && !stopWords.Contains(w))
            .Distinct()
            .Take(6)
            .ToList();
    }
}
