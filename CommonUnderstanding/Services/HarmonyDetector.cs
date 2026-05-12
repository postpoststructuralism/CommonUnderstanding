using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using CommonUnderstanding.Data;
using CommonUnderstanding.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CommonUnderstanding.Services;

/// <summary>
/// Detects harmonies — convergences, complementary patterns, and opportunities
/// hidden across the community's argument inventory.
/// Phase 1: graph/database analysis only — no LLM calls.
/// </summary>
public class HarmonyDetector
{
    private readonly ApplicationDbContext _db;
    private readonly SemanticKernelService _kernelService;
    private readonly ILogger<HarmonyDetector> _logger;

    public HarmonyDetector(
        ApplicationDbContext db,
        SemanticKernelService kernelService,
        ILogger<HarmonyDetector> logger)
    {
        _db = db;
        _kernelService = kernelService;
        _logger = logger;
    }

    /// <summary>
    /// Runs all Phase 1 harmony detectors and returns the combined findings.
    /// </summary>
    public async Task<List<EmergentConclusion>> DetectAllAsync(CancellationToken ct = default)
    {
        var results = new List<EmergentConclusion>();

        var tasks = new[]
        {
            DetectConvergentGroundAsync(ct),
            DetectEmergentConsensusAsync(ct),
            DetectComplementaryChainsAsync(ct)
        };

        var all = await Task.WhenAll(tasks);
        foreach (var batch in all)
            results.AddRange(batch);

        return results.OrderByDescending(r => r.Significance).ToList();
    }

    /// <summary>
    /// Runs Phase 1 + LLM-powered harmony detectors (shared values, cross-domain reinforcement).
    /// </summary>
    public async Task<List<EmergentConclusion>> DetectAllDeepAsync(CancellationToken ct = default)
    {
        var phase1 = await DetectAllAsync(ct);
        var sharedValueTask = ExtractSharedValueCoreAsync(ct);
        var crossDomainTask = DetectCrossDomainReinforcementAsync(ct);
        await Task.WhenAll(sharedValueTask, crossDomainTask);
        return phase1
            .Concat(await sharedValueTask)
            .Concat(await crossDomainTask)
            .OrderByDescending(r => r.Significance)
            .ToList();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  1. Convergent Ground
    //  Opposing stakeholders who accept the same specific premises
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<List<EmergentConclusion>> DetectConvergentGroundAsync(CancellationToken ct)
    {
        var findings = new List<EmergentConclusion>();

        // Load all arguments that have at least two stakeholder positions (opposing)
        var positions = await _db.StakeholderPositions
            .Include(sp => sp.StakeholderRef)
            .Include(sp => sp.Argument)
            .ToListAsync(ct);

        if (!positions.Any()) return findings;

        // Group by argument
        var byArgument = positions.GroupBy(p => p.ArgumentId);

        foreach (var group in byArgument)
        {
            var supporters = group
                .Where(p => p.Position == StakeholderPositionType.Support)
                .ToList();
            var opposers = group
                .Where(p => p.Position == StakeholderPositionType.Oppose)
                .ToList();

            if (!supporters.Any() || !opposers.Any()) continue;

            // Parse accepted premise IDs for each side
            var supporterAccepted = supporters
                .SelectMany(s => ParseIds(s.AcceptedPremiseIdsJson))
                .ToHashSet();

            var opposerAccepted = opposers
                .SelectMany(o => ParseIds(o.AcceptedPremiseIdsJson))
                .ToHashSet();

            var sharedPremiseIds = supporterAccepted.Intersect(opposerAccepted).ToList();

            if (!sharedPremiseIds.Any()) continue;

            // Fetch the shared proposition texts
            var sharedProps = await _db.Propositions
                .Where(p => sharedPremiseIds.Contains(p.Id))
                .ToListAsync(ct);

            if (!sharedProps.Any()) continue;

            var argument = group.First().Argument;
            var argumentTitle = argument?.Title ?? $"Argument #{group.Key}";

            var supporterNames = supporters
                .Where(s => !s.IsAnonymous)
                .Select(s => s.StakeholderRef?.Name ?? "Unknown")
                .Distinct()
                .ToList();

            var opposerNames = opposers
                .Where(o => !o.IsAnonymous)
                .Select(o => o.StakeholderRef?.Name ?? "Unknown")
                .Distinct()
                .ToList();

            var allStakeholderIds = group
                .Select(p => p.StakeholderId)
                .Distinct()
                .ToList();

            double significance = Math.Min(1.0,
                (double)sharedPremiseIds.Count / Math.Max(sharedProps.Count + 1, 1) * 0.8
                + allStakeholderIds.Count * 0.05);

            findings.Add(new EmergentConclusion
            {
                Type = EmergentType.Harmony,
                Category = EmergentCategory.ConvergentGround,
                Title = $"Opposing Stakeholders Share {sharedPremiseIds.Count} Common Premise(s)",
                Description =
                    $"On the argument \"{argumentTitle}\", stakeholders who support and " +
                    $"oppose the overall conclusion both accept these {sharedPremiseIds.Count} " +
                    $"premise(s): \"{string.Join("\"; \"", sharedProps.Select(p => TruncateText(p.Text, 80)))}\". " +
                    $"This is unstated common ground that could anchor productive dialogue.",
                Significance = Math.Round(significance, 3),
                Confidence = 0.95,
                InvolvedArgumentIds = new List<int> { group.Key },
                InvolvedArgumentTitles = new List<string> { argumentTitle },
                InvolvedPropositionIds = sharedPremiseIds,
                InvolvedPropositionTexts = sharedProps.Select(p => p.Text).ToList(),
                InvolvedStakeholderIds = allStakeholderIds,
                OpportunityDescription =
                    $"Begin any dialogue between {string.Join(", ", supporterNames)} and " +
                    $"{string.Join(", ", opposerNames)} by explicitly acknowledging these " +
                    $"shared premises. Agreement here creates a foundation for resolving " +
                    $"the contested aspects of the argument."
            });
        }

        _logger.LogInformation("ConvergentGround: found {Count} patterns", findings.Count);
        return findings;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  2. Emergent Consensus
    //  Graph nodes that have been updated multiple times and are gaining confidence
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<List<EmergentConclusion>> DetectEmergentConsensusAsync(CancellationToken ct)
    {
        var findings = new List<EmergentConclusion>();

        // Nodes that have been updated more than once (Version > 1) and have reasonable confidence
        var evolvingNodes = await _db.CommonUnderstandingNodes
            .Where(n => n.Version > 1 && n.Confidence >= 0.55 && n.EvidenceCount > 0)
            .OrderByDescending(n => n.Confidence)
            .Take(20)
            .ToListAsync(ct);

        foreach (var node in evolvingNodes)
        {
            List<int> argIds;
            try { argIds = JsonSerializer.Deserialize<List<int>>(node.ArgumentIdsJson) ?? new(); }
            catch { argIds = new(); }

            if (argIds.Count < 2) continue; // Only interesting if multiple arguments contributed

            var argumentTitles = await _db.Arguments
                .Where(a => argIds.Contains(a.Id))
                .Select(a => a.Title)
                .ToListAsync(ct);

            var ageInDays = (DateTime.UtcNow - node.FirstSeenAt).TotalDays;
            double convergenceRate = ageInDays > 0 ? (node.Confidence - 0.5) / ageInDays : 0;

            double significance = Math.Min(1.0,
                (node.Confidence - 0.5) * 1.5 + argIds.Count * 0.05);

            string statusLabel = node.Status == PropositionStatus.Settled ? "settled" :
                                 node.Status == PropositionStatus.Contested ? "formerly contested" : "developing";

            findings.Add(new EmergentConclusion
            {
                Type = EmergentType.Harmony,
                Category = EmergentCategory.EmergentConsensus,
                Title = $"Community Converging on \"{TruncateText(node.Text, 70)}\"",
                Description =
                    $"This proposition has been independently referenced by {argIds.Count} argument(s) " +
                    $"and updated {node.Version} times. Its combined confidence is now {node.Confidence:P0} " +
                    $"(status: {statusLabel}), supported by {node.EvidenceCount} evidence item(s). " +
                    $"The community is naturally converging on this as shared knowledge.",
                Significance = Math.Round(significance, 3),
                Confidence = 0.80,
                InvolvedArgumentIds = argIds,
                InvolvedArgumentTitles = argumentTitles,
                InvolvedNodeIds = new List<int> { node.Id },
                InvolvedPropositionTexts = new List<string> { node.Text },
                OpportunityDescription =
                    $"Formally mark this proposition as a shared anchor. " +
                    $"Future arguments can cite it as settled ground rather than re-litigating it. " +
                    $"This reduces duplication and anchors the community's shared understanding."
            });
        }

        _logger.LogInformation("EmergentConsensus: found {Count} patterns", findings.Count);
        return findings.Take(6).ToList();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  3. Complementary Chains
    //  Arguments that share high-confidence graph nodes, implying mutual support
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<List<EmergentConclusion>> DetectComplementaryChainsAsync(CancellationToken ct)
    {
        var findings = new List<EmergentConclusion>();

        // Find graph nodes referenced by multiple arguments with high confidence
        var sharedNodes = await _db.CommonUnderstandingNodes
            .Where(n => n.Confidence >= 0.65 && n.EvidenceCount >= 1)
            .ToListAsync(ct);

        var multiArgNodes = new List<(CommonUnderstandingNode Node, List<int> ArgIds)>();

        foreach (var node in sharedNodes)
        {
            List<int> argIds;
            try { argIds = JsonSerializer.Deserialize<List<int>>(node.ArgumentIdsJson) ?? new(); }
            catch { argIds = new(); }

            if (argIds.Count >= 2)
                multiArgNodes.Add((node, argIds));
        }

        if (!multiArgNodes.Any()) return findings;

        // Also check existing comparisons for complementary premises
        var comparisons = await _db.ArgumentComparisons
            .Where(c => c.ComplementaryPremisesJson != null)
            .Include(c => c.ArgumentA)
            .Include(c => c.ArgumentB)
            .ToListAsync(ct);

        foreach (var comp in comparisons)
        {
            List<string> complementary;
            try { complementary = JsonSerializer.Deserialize<List<string>>(comp.ComplementaryPremisesJson!) ?? new(); }
            catch { complementary = new(); }

            if (complementary.Count < 2) continue;

            double significance = Math.Min(1.0, complementary.Count * 0.15);

            findings.Add(new EmergentConclusion
            {
                Type = EmergentType.Harmony,
                Category = EmergentCategory.ComplementaryChains,
                Title = $"Arguments \"{TruncateText(comp.ArgumentA?.Title ?? "", 50)}\" and \"{TruncateText(comp.ArgumentB?.Title ?? "", 50)}\" Share {complementary.Count} Reinforcing Premises",
                Description =
                    $"A comparative analysis found {complementary.Count} premises that both arguments " +
                    $"share or agree on. These arguments are not competing — they are building " +
                    $"complementary cases. Key shared ground: \"{TruncateText(complementary.First(), 100)}\"" +
                    (complementary.Count > 1 ? $" and {complementary.Count - 1} more." : "."),
                Significance = Math.Round(significance, 3),
                Confidence = 0.90,
                InvolvedArgumentIds = new List<int> { comp.ArgumentAId, comp.ArgumentBId },
                InvolvedArgumentTitles = new List<string>
                {
                    comp.ArgumentA?.Title ?? $"Argument #{comp.ArgumentAId}",
                    comp.ArgumentB?.Title ?? $"Argument #{comp.ArgumentBId}"
                },
                InvolvedPropositionTexts = complementary.Take(3).ToList(),
                OpportunityDescription =
                    "Consider synthesising these two arguments into a combined position. " +
                    "The shared premises could form the basis of a joint proposal or a " +
                    "stronger combined case that draws on both lines of reasoning."
            });
        }

        // Add findings for high-confidence shared nodes not already captured by comparisons
        var alreadyCapturedArgPairs = comparisons
            .Select(c => (Math.Min(c.ArgumentAId, c.ArgumentBId), Math.Max(c.ArgumentAId, c.ArgumentBId)))
            .ToHashSet();

        foreach (var (node, argIds) in multiArgNodes.Take(5))
        {
            // Only flag argument pairs that haven't been formally compared yet
            var uncheckedPairs = from a in argIds
                                 from b in argIds
                                 where a < b && !alreadyCapturedArgPairs.Contains((a, b))
                                 select (a, b);

            if (!uncheckedPairs.Any()) continue;

            var firstPair = uncheckedPairs.First();
            var titleA = (await _db.Arguments.FindAsync(new object[] { firstPair.a }, ct))?.Title
                         ?? $"Argument #{firstPair.a}";
            var titleB = (await _db.Arguments.FindAsync(new object[] { firstPair.b }, ct))?.Title
                         ?? $"Argument #{firstPair.b}";

            findings.Add(new EmergentConclusion
            {
                Type = EmergentType.Harmony,
                Category = EmergentCategory.ComplementaryChains,
                Title = $"Uncompared Arguments Share High-Confidence Proposition",
                Description =
                    $"{argIds.Count} arguments all reference the proposition " +
                    $"\"{TruncateText(node.Text, 130)}\" (confidence: {node.Confidence:P0}). " +
                    $"These arguments may be mutually reinforcing but no formal comparison has been run.",
                Significance = Math.Round(Math.Min(1.0, argIds.Count * 0.12), 3),
                Confidence = 0.75,
                InvolvedArgumentIds = argIds,
                InvolvedNodeIds = new List<int> { node.Id },
                InvolvedPropositionTexts = new List<string> { node.Text },
                OpportunityDescription =
                    $"Run a comparative analysis between \"{titleA}\" and \"{titleB}\" " +
                    $"to surface the full extent of their complementarity. " +
                    $"The shared proposition \"{TruncateText(node.Text, 60)}\" may be a powerful " +
                    $"common-ground anchor for synthesis."
            });
        }

        _logger.LogInformation("ComplementaryChains: found {Count} patterns", findings.Count);
        return findings.OrderByDescending(f => f.Significance).Take(8).ToList();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  4. Shared Value Core (Phase 2 — LLM)
    //  Values consistently appealed to across arguments and stakeholder reasoning
    // ─────────────────────────────────────────────────────────────────────────

    internal async Task<List<EmergentConclusion>> ExtractSharedValueCoreAsync(CancellationToken ct)
    {
        var findings = new List<EmergentConclusion>();

        // Collect stakeholder reasoning texts + adjudication narratives
        var stakeholderReasonings = await _db.StakeholderPositions
            .Where(sp => sp.Reasoning != null && sp.Reasoning.Length > 20)
            .Select(sp => sp.Reasoning!)
            .ToListAsync(ct);

        var adjudicationNarratives = await _db.AdjudicationSummaries
            .Where(a => a.DetailedNarrative != null && a.DetailedNarrative.Length > 50)
            .Select(a => a.DetailedNarrative!)
            .Take(10)
            .ToListAsync(ct);

        var allTexts = stakeholderReasonings.Concat(adjudicationNarratives).ToList();
        if (allTexts.Count < 2) return findings;

        var textSample = string.Join("\n---\n",
            allTexts.Take(15).Select((t, i) => $"[{i + 1}] {t[..Math.Min(300, t.Length)]}"));

        try
        {
            var kernel = _kernelService.GetKernel();
            var prompt = $"""
            You are an analyst examining what core values underpin a community's reasoning.

            Below are excerpts from stakeholder reasoning and decision analyses.
            Identify the underlying VALUES (not positions or facts) that appear consistently
            across multiple excerpts — things like fairness, efficiency, safety, accountability,
            transparency, autonomy, equity, sustainability, evidence-based decision-making, etc.

            LIST ONLY values that appear in THREE or more excerpts.
            Format each value on its own line:
            VALUE: [value name] | [one sentence explaining how it manifests in the excerpts]

            EXCERPTS:
            {textSample}
            """;

            var result = await kernel.InvokePromptAsync(prompt, cancellationToken: ct);
            var raw = result.ToString();

            var valueMatches = Regex.Matches(raw,
                @"VALUE:\s*([^\|]+)\s*\|\s*(.+)",
                RegexOptions.IgnoreCase);

            if (!valueMatches.Any()) return findings;

            var valueNames = valueMatches
                .Cast<Match>()
                .Select(m => m.Groups[1].Value.Trim())
                .ToList();
            var valueDescriptions = valueMatches
                .Cast<Match>()
                .Select(m => m.Groups[2].Value.Trim())
                .ToList();

            var allArgumentIds = await _db.Arguments.Select(a => a.Id).ToListAsync(ct);
            var allArgumentTitles = await _db.Arguments.Select(a => a.Title).ToListAsync(ct);

            findings.Add(new EmergentConclusion
            {
                Type = EmergentType.Harmony,
                Category = EmergentCategory.SharedValueCore,
                Title = $"Shared Value Core: {string.Join(", ", valueNames.Take(4))}",
                Description =
                    $"Despite surface disagreements, the community consistently appeals to " +
                    $"{valueNames.Count} core value(s) across its arguments and stakeholder positions: " +
                    string.Join("; ", valueNames.Zip(valueDescriptions, (n, d) => $"**{n}** — {d}")) + ".",
                Significance = Math.Min(1.0, 0.50 + valueNames.Count * 0.08),
                Confidence = 0.70,
                InvolvedArgumentIds = allArgumentIds.Take(10).ToList(),
                InvolvedArgumentTitles = allArgumentTitles.Take(10).ToList(),
                OpportunityDescription =
                    "Make these shared values explicit at the start of any dialogue or negotiation. " +
                    "Framing contested arguments in terms of values the community already agrees on " +
                    "significantly improves the odds of reaching common ground."
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Shared value core LLM extraction failed");
        }

        _logger.LogInformation("SharedValueCore: found {Count} patterns", findings.Count);
        return findings;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  5. Cross-Domain Reinforcement (Phase 2 — LLM)
    //  Arguments from different domains whose conclusions reinforce each other
    // ─────────────────────────────────────────────────────────────────────────

    internal async Task<List<EmergentConclusion>> DetectCrossDomainReinforcementAsync(CancellationToken ct)
    {
        var findings = new List<EmergentConclusion>();

        // Load arguments with adjudication summaries (need titles + conclusions)
        var adjudicated = await _db.Arguments
            .Where(a => a.AdjudicationSummary != null)
            .Include(a => a.Claims)
                .ThenInclude(c => c.Syllogisms)
            .Include(a => a.AdjudicationSummary)
            .Take(12)
            .ToListAsync(ct);

        if (adjudicated.Count < 3) return findings;

        // Gather conclusions from syllogisms per argument
        var argumentSummaries = adjudicated.Select(a =>
        {
            var conclusions = a.Claims
                .SelectMany(c => c.Syllogisms)
                .Select(s => s.Conclusion)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Take(2)
                .ToList();
            var confText = a.AdjudicationSummary?.OverallConfidence is double conf
                ? $" (confidence: {conf:P0})"
                : "";
            var conclusionText = conclusions.Any()
                ? string.Join("; ", conclusions.Select(c => c[..Math.Min(120, c.Length)]))
                : "(no formal conclusion)";
            return $"[{a.Id}] \"{a.Title}\"{confText}: {conclusionText}";
        }).ToList();

        try
        {
            var kernel = _kernelService.GetKernel();
            var prompt = $"""
            You are an analyst looking for reinforcing relationships between arguments from different domains.

            Below is a list of arguments with their conclusions.
            Identify pairs of arguments where the CONCLUSION of one argument SUPPORTS or REINFORCES
            the conclusion of another — especially across different subject areas.

            Only flag genuinely reinforcing pairs (not just similar topics).
            Format each reinforcing pair on its own line:
            REINFORCE: [id1] | [id2] | [one sentence explaining how one conclusion supports the other]

            ARGUMENTS:
            {string.Join("\n", argumentSummaries)}
            """;

            var result = await kernel.InvokePromptAsync(prompt, cancellationToken: ct);
            var raw = result.ToString();

            var matches = Regex.Matches(raw,
                @"REINFORCE:\s*\[?(\d+)\]?\s*\|\s*\[?(\d+)\]?\s*\|\s*(.+)",
                RegexOptions.IgnoreCase);

            foreach (Match m in matches)
            {
                if (!int.TryParse(m.Groups[1].Value.Trim(), out int idA)) continue;
                if (!int.TryParse(m.Groups[2].Value.Trim(), out int idB)) continue;
                string explanation = m.Groups[3].Value.Trim();

                var argA = adjudicated.FirstOrDefault(a => a.Id == idA);
                var argB = adjudicated.FirstOrDefault(a => a.Id == idB);
                if (argA == null || argB == null) continue;

                findings.Add(new EmergentConclusion
                {
                    Type = EmergentType.Harmony,
                    Category = EmergentCategory.CrossDomainReinforcement,
                    Title = $"Cross-Domain Reinforcement: \"{TruncateText(argA.Title, 45)}\" supports \"{TruncateText(argB.Title, 45)}\"",
                    Description =
                        $"The conclusions of \"{argA.Title}\" and \"{argB.Title}\" " +
                        $"are mutually reinforcing across different domains. {explanation}",
                    Significance = 0.55,
                    Confidence = 0.65,
                    InvolvedArgumentIds = new List<int> { idA, idB },
                    InvolvedArgumentTitles = new List<string> { argA.Title, argB.Title },
                    OpportunityDescription =
                        "Cite both arguments together in decision briefs to demonstrate that " +
                        "the same conclusion is supported from independent lines of reasoning. " +
                        "This substantially strengthens the combined case."
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cross-domain reinforcement LLM detection failed");
        }

        _logger.LogInformation("CrossDomainReinforcement: found {Count} patterns", findings.Count);
        return findings.Take(5).ToList();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static List<int> ParseIds(string json)
    {
        try { return JsonSerializer.Deserialize<List<int>>(json) ?? new(); }
        catch { return new(); }
    }

    private static string TruncateText(string text, int max) =>
        text.Length <= max ? text : text[..max] + "…";
}
