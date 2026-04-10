using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using CommonUnderstanding.Data;
using CommonUnderstanding.Models;

namespace CommonUnderstanding.Services;

/// <summary>
/// Core engine for computing the convergence map between two users.
/// Operates across three analytical layers:
///   1. Profile layer  — BeliefSnapshot dimension comparison
///   2. Argument layer — cross-user ArgumentComparison premise overlap
///   3. Graph layer    — shared/disputed CommonUnderstandingNodes
/// Synthesizes findings into DivergencePoints and AI-generated ExpansionPathways.
/// </summary>
public class ConvergenceMapService
{
    private readonly ApplicationDbContext _db;
    private readonly UserProfileStore _profileStore;
    private readonly SemanticKernelService _kernelService;
    private readonly ILogger<ConvergenceMapService> _logger;

    private static readonly JsonSerializerOptions _json = new() { WriteIndented = false };

    // Threshold above which a dimension gap is considered a divergence point
    private const double DivergenceGapThreshold = 0.3;

    public ConvergenceMapService(
        ApplicationDbContext db,
        UserProfileStore profileStore,
        SemanticKernelService kernelService,
        ILogger<ConvergenceMapService> logger)
    {
        _db = db;
        _profileStore = profileStore;
        _kernelService = kernelService;
        _logger = logger;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a fresh ConvergenceMap for the two users and persists it.
    /// If a map already exists between these users it is refreshed in-place.
    /// </summary>
    public async Task<ConvergenceMap> GenerateAsync(
        string user1Id,
        string user2Id,
        CancellationToken cancellationToken = default)
    {
        var user1 = _profileStore.GetProfile(user1Id)
            ?? throw new InvalidOperationException($"User {user1Id} not found.");
        var user2 = _profileStore.GetProfile(user2Id)
            ?? throw new InvalidOperationException($"User {user2Id} not found.");

        _logger.LogInformation("Generating convergence map: {U1} ↔ {U2}", user1Id, user2Id);

        // Retrieve or create the map record
        var map = await _db.ConvergenceMaps
            .FirstOrDefaultAsync(m =>
                (m.User1Id == user1Id && m.User2Id == user2Id) ||
                (m.User1Id == user2Id && m.User2Id == user1Id),
                cancellationToken)
            ?? new ConvergenceMap { User1Id = user1Id, User2Id = user2Id };

        var isNew = map.Id == 0;

        // ── Layer 1: Profile ──────────────────────────────────────────────────
        var profileOverlap = BuildProfileOverlap(user1, user2);
        map.ProfileOverlapJson = JsonSerializer.Serialize(profileOverlap, _json);

        // ── Layer 2: Dimension divergence ─────────────────────────────────────
        var divergencePoints = BuildDivergencePoints(user1, user2);
        map.DivergencePointsJson = JsonSerializer.Serialize(divergencePoints, _json);

        // ── Layer 3: Proposition graph ────────────────────────────────────────
        var (sharedIds, disputedIds) = await FindGraphOverlapAsync(user1Id, user2Id, cancellationToken);
        map.SharedPropositionIdsJson = JsonSerializer.Serialize(sharedIds, _json);
        map.DisputedPropositionIdsJson = JsonSerializer.Serialize(disputedIds, _json);

        // ── Scoring ───────────────────────────────────────────────────────────
        map.OverallConvergenceScore = ComputeScore(profileOverlap, sharedIds.Count, disputedIds.Count, divergencePoints);

        // ── Expansion pathways (AI) ───────────────────────────────────────────
        var pathways = await GenerateExpansionPathwaysAsync(user1, user2, divergencePoints, profileOverlap, cancellationToken);
        map.ExpansionPathwaysJson = JsonSerializer.Serialize(pathways, _json);

        // ── Narrative summary (AI) ────────────────────────────────────────────
        map.NarrativeSummary = await GenerateNarrativeSummaryAsync(user1, user2, map, cancellationToken);

        // ── Snapshot history ──────────────────────────────────────────────────
        var history = DeserializeHistory(map.EvolutionHistoryJson);
        history.Add(new ConvergenceSnapshot
        {
            RecordedAt = DateTime.UtcNow,
            ConvergenceScore = map.OverallConvergenceScore,
            SharedPropositionCount = sharedIds.Count,
            DisputedPropositionCount = disputedIds.Count,
            TriggerEvent = isNew ? "Initial" : "Refresh"
        });
        map.EvolutionHistoryJson = JsonSerializer.Serialize(history.TakeLast(50), _json);

        map.LastRefreshedAt = DateTime.UtcNow;

        if (isNew)
            _db.ConvergenceMaps.Add(map);

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Convergence map {Id} saved. Score: {Score:F1}", map.Id, map.OverallConvergenceScore);
        return map;
    }

    /// <summary>
    /// Fetches the most recent map for two users without regenerating it.
    /// Returns null if no map exists.
    /// </summary>
    public async Task<ConvergenceMap?> GetMapAsync(
        string user1Id,
        string user2Id,
        CancellationToken cancellationToken = default)
    {
        return await _db.ConvergenceMaps
            .FirstOrDefaultAsync(m =>
                (m.User1Id == user1Id && m.User2Id == user2Id) ||
                (m.User1Id == user2Id && m.User2Id == user1Id),
                cancellationToken);
    }

    /// <summary>
    /// Returns all maps that involve a given user.
    /// </summary>
    public async Task<List<ConvergenceMap>> GetMapsForUserAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await _db.ConvergenceMaps
            .Where(m => m.User1Id == userId || m.User2Id == userId)
            .OrderByDescending(m => m.LastRefreshedAt)
            .ToListAsync(cancellationToken);
    }

    // ── Layer 1: Profile comparison ───────────────────────────────────────────

    private static BeliefComparison BuildProfileOverlap(UserProfile user1, UserProfile user2)
    {
        var snap1 = user1.CurrentBeliefSnapshot;
        var snap2 = user2.CurrentBeliefSnapshot;

        var comparison = new BeliefComparison
        {
            BeliefSystem1Id = user1.Id,
            BeliefSystem2Id = user2.Id,
            BeliefSystem1Name = user1.Name,
            BeliefSystem2Name = user2.Name
        };

        if (snap1 is null || snap2 is null) return comparison;

        // Shared values — values present in both snapshots ranked by combined importance
        var values1 = snap1.Values.Select(v => v.Name.ToLowerInvariant()).ToHashSet();
        var values2 = snap2.Values.Select(v => v.Name.ToLowerInvariant()).ToHashSet();
        var sharedValueNames = values1.Intersect(values2).ToList();

        if (sharedValueNames.Count > 0)
        {
            comparison.AreasOfOverlap.Add(new CommonGround
            {
                Theme = "Shared Values",
                Description = $"Both users express similar underlying values.",
                SharedValues = sharedValueNames,
                StrengthScore = Math.Min(10, sharedValueNames.Count * 2)
            });
        }

        // Shared moral foundations — foundations within 0.2 of each other
        var mf1 = snap1.MoralFoundations;
        var mf2 = snap2.MoralFoundations;
        var sharedFoundations = new List<string>();
        if (Math.Abs(mf1.Care.Score - mf2.Care.Score) < 0.2) sharedFoundations.Add("Care");
        if (Math.Abs(mf1.Fairness.Score - mf2.Fairness.Score) < 0.2) sharedFoundations.Add("Fairness");
        if (Math.Abs(mf1.Loyalty.Score - mf2.Loyalty.Score) < 0.2) sharedFoundations.Add("Loyalty");
        if (Math.Abs(mf1.Authority.Score - mf2.Authority.Score) < 0.2) sharedFoundations.Add("Authority");
        if (Math.Abs(mf1.Sanctity.Score - mf2.Sanctity.Score) < 0.2) sharedFoundations.Add("Sanctity");
        if (Math.Abs(mf1.Liberty.Score - mf2.Liberty.Score) < 0.2) sharedFoundations.Add("Liberty");

        if (sharedFoundations.Count > 0)
        {
            comparison.AreasOfOverlap.Add(new CommonGround
            {
                Theme = "Moral Foundations",
                Description = "Aligned moral foundations suggest similar ethical intuitions.",
                SharedPrinciples = sharedFoundations,
                StrengthScore = Math.Min(10, sharedFoundations.Count * 2)
            });
        }

        // Simple overlap score: proportion of shared values + aligned foundations
        double maxPossible = Math.Max(1, values1.Count + values2.Count) / 2.0 + 6.0;
        double actual = sharedValueNames.Count + sharedFoundations.Count;
        comparison.OverlapScore = Math.Round((actual / maxPossible) * 100, 1);

        return comparison;
    }

    // ── Layer 2: Dimension divergence ─────────────────────────────────────────

    private static List<DivergenceDimension> BuildDivergencePoints(UserProfile user1, UserProfile user2)
    {
        var snap1 = user1.CurrentBeliefSnapshot;
        var snap2 = user2.CurrentBeliefSnapshot;

        if (snap1 is null || snap2 is null) return new();

        var dims1 = snap1.Dimensions.Where(d => d.Position.HasValue)
            .ToDictionary(d => d.Name.ToLowerInvariant(), d => d);

        var divergences = new List<DivergenceDimension>();

        foreach (var dim2 in snap2.Dimensions.Where(d => d.Position.HasValue))
        {
            var key = dim2.Name.ToLowerInvariant();
            if (!dims1.TryGetValue(key, out var dim1)) continue;

            var gap = Math.Abs(dim1.Position!.Value - dim2.Position!.Value);
            if (gap < DivergenceGapThreshold) continue;

            divergences.Add(new DivergenceDimension
            {
                DimensionName = dim2.Name,
                Category = dim2.Category,
                User1Position = dim1.Position.Value,
                User2Position = dim2.Position.Value,
                Gap = gap,
                IsValueLevel = dim2.Category is "Ethical" or "Metaphysical" or "Moral",
                User1Confidence = dim1.Confidence,
                User2Confidence = dim2.Confidence
            });
        }

        // Rank: value-level gaps first, then by gap magnitude descending
        return divergences
            .OrderByDescending(d => d.IsValueLevel)
            .ThenByDescending(d => d.Gap)
            .ToList();
    }

    // ── Layer 3: Proposition graph ────────────────────────────────────────────

    private async Task<(List<int> Shared, List<int> Disputed)> FindGraphOverlapAsync(
        string user1Id,
        string user2Id,
        CancellationToken cancellationToken)
    {
        // Find all argument IDs contributed by each user
        var args1 = await _db.Arguments
            .Where(a => a.SubmittedBy == user1Id)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        var args2 = await _db.Arguments
            .Where(a => a.SubmittedBy == user2Id)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        if (!args1.Any() || !args2.Any()) return (new(), new());

        // Load all nodes and filter by citation in each user's arguments
        var allNodes = await _db.CommonUnderstandingNodes.ToListAsync(cancellationToken);

        var nodesReferencedByUser1 = new HashSet<int>();
        var nodesReferencedByUser2 = new HashSet<int>();

        foreach (var node in allNodes)
        {
            var referencedArgIds = JsonSerializer.Deserialize<List<int>>(node.ArgumentIdsJson) ?? new();
            if (referencedArgIds.Intersect(args1).Any()) nodesReferencedByUser1.Add(node.Id);
            if (referencedArgIds.Intersect(args2).Any()) nodesReferencedByUser2.Add(node.Id);
        }

        // Shared: nodes referenced by both users
        var shared = nodesReferencedByUser1.Intersect(nodesReferencedByUser2).ToList();

        // Disputed: nodes where the two users' StakeholderPositions are on opposite sides
        var positions1 = await _db.StakeholderPositions
            .Where(sp => args1.Contains(sp.ArgumentId))
            .ToListAsync(cancellationToken);
        var positions2 = await _db.StakeholderPositions
            .Where(sp => args2.Contains(sp.ArgumentId))
            .ToListAsync(cancellationToken);

        var accepted1 = positions1.SelectMany(p =>
            JsonSerializer.Deserialize<List<int>>(p.AcceptedPremiseIdsJson) ?? new()).ToHashSet();
        var accepted2 = positions2.SelectMany(p =>
            JsonSerializer.Deserialize<List<int>>(p.AcceptedPremiseIdsJson) ?? new()).ToHashSet();
        var rejected1 = positions1.SelectMany(p =>
            JsonSerializer.Deserialize<List<int>>(p.RejectedPremiseIdsJson) ?? new()).ToHashSet();
        var rejected2 = positions2.SelectMany(p =>
            JsonSerializer.Deserialize<List<int>>(p.RejectedPremiseIdsJson) ?? new()).ToHashSet();

        // A node is disputed if one user accepts it (in a proposition) and the other rejects it
        var disputed = accepted1.Intersect(rejected2)
            .Union(accepted2.Intersect(rejected1))
            .ToList();

        return (shared, disputed);
    }

    // ── Scoring ───────────────────────────────────────────────────────────────

    private static double ComputeScore(
        BeliefComparison profileOverlap,
        int sharedNodeCount,
        int disputedNodeCount,
        List<DivergenceDimension> divergencePoints)
    {
        // Profile layer: 50% weight
        double profileScore = profileOverlap.OverlapScore * 0.5;

        // Graph layer: 30% weight — shared proportion relative to disputed
        double graphDenominator = Math.Max(1, sharedNodeCount + disputedNodeCount);
        double graphScore = (sharedNodeCount / graphDenominator) * 100 * 0.3;

        // Dimension layer: 20% weight — inverse of average normalized gap
        double dimensionScore = 20.0;
        if (divergencePoints.Count > 0)
        {
            double avgGap = divergencePoints.Average(d => d.Gap) / 2.0; // normalize to 0-1
            dimensionScore = (1.0 - avgGap) * 100 * 0.2;
        }

        return Math.Round(Math.Clamp(profileScore + graphScore + dimensionScore, 0, 100), 1);
    }

    // ── AI: Expansion pathways ────────────────────────────────────────────────

    private async Task<List<ExpansionPathway>> GenerateExpansionPathwaysAsync(
        UserProfile user1,
        UserProfile user2,
        List<DivergenceDimension> divergencePoints,
        BeliefComparison profileOverlap,
        CancellationToken cancellationToken)
    {
        if (!divergencePoints.Any()) return new();

        var sharedValues = profileOverlap.AreasOfOverlap
            .SelectMany(a => a.SharedValues.Concat(a.SharedPrinciples))
            .Distinct()
            .Take(5)
            .ToList();

        var topDivergences = divergencePoints.Take(3).ToList();

        var prompt = $"""
        You are a dialogue facilitator helping two people build shared understanding.

        USER 1: {user1.Name}
        {(user1.CurrentBeliefSnapshot?.NarrativeSummary is { Length: > 0 } ns1 ? $"Worldview: {ns1}" : "No narrative yet.")}

        USER 2: {user2.Name}
        {(user2.CurrentBeliefSnapshot?.NarrativeSummary is { Length: > 0 } ns2 ? $"Worldview: {ns2}" : "No narrative yet.")}

        SHARED VALUES/FOUNDATIONS: {string.Join(", ", sharedValues.DefaultIfEmpty("(none identified yet)"))}

        TOP DIVERGENCE POINTS:
        {string.Join("\n", topDivergences.Select(d =>
            $"  - {d.DimensionName} ({d.Category}): {user1.Name} position {d.User1Position:+0.00;-0.00}, {user2.Name} position {d.User2Position:+0.00;-0.00}, gap {d.Gap:0.00}{(d.IsValueLevel ? " [values-level]" : "")}"))}

        Generate exactly {Math.Min(3, topDivergences.Count)} expansion pathways — one per divergence point.
        Each pathway must:
        1. Anchor on a SHARED value listed above (or identify a latent shared value if none listed).
        2. Offer a COMMON FRAMING neither person has yet articulated.
        3. Provide 2 targeted Socratic questions for {user1.Name} and 2 for {user2.Name}, calibrated to their respective positions.
        4. Assign priority: High if values-level divergence, Medium otherwise.
        5. Estimate convergence gain 0.0–1.0 if both users engage this pathway.

        Respond ONLY in this exact format, one block per pathway — no preamble or postamble:

        PATHWAY_START
        TITLE: [concise title]
        DIVERGENCE: [one-sentence description of the divergence]
        ANCHOR: [the shared value this pathway leverages]
        FRAMING: [common framing sentence]
        Q_USER1_1: [question for {user1.Name}]
        Q_USER1_2: [question for {user1.Name}]
        Q_USER2_1: [question for {user2.Name}]
        Q_USER2_2: [question for {user2.Name}]
        PRIORITY: [High|Medium|Low]
        GAIN: [0.0–1.0]
        PATHWAY_END
        """;

        try
        {
            var kernel = _kernelService.GetKernel();
            var result = await kernel.InvokePromptAsync(prompt, cancellationToken: cancellationToken);
            return ParsePathways(result.ToString(), user1.Name, user2.Name, topDivergences);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pathway generation failed; returning empty list");
            return new();
        }
    }

    private static List<ExpansionPathway> ParsePathways(
        string raw,
        string user1Name,
        string user2Name,
        List<DivergenceDimension> divergencePoints)
    {
        var pathways = new List<ExpansionPathway>();
        var blocks = raw.Split("PATHWAY_START", StringSplitOptions.RemoveEmptyEntries);

        foreach (var block in blocks)
        {
            var endIdx = block.IndexOf("PATHWAY_END", StringComparison.Ordinal);
            var content = endIdx >= 0 ? block[..endIdx] : block;

            string GetField(string key)
            {
                var line = content.Split('\n')
                    .FirstOrDefault(l => l.TrimStart().StartsWith(key + ":", StringComparison.OrdinalIgnoreCase));
                return line is null ? string.Empty : line[(line.IndexOf(':') + 1)..].Trim();
            }

            var title = GetField("TITLE");
            if (string.IsNullOrWhiteSpace(title)) continue;

            var priority = GetField("PRIORITY");
            double.TryParse(GetField("GAIN"), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var gain);

            pathways.Add(new ExpansionPathway
            {
                Title = title,
                DivergenceDescription = GetField("DIVERGENCE"),
                SharedValueAnchor = GetField("ANCHOR"),
                PotentialCommonFraming = GetField("FRAMING"),
                QuestionsForUser1 = new List<string> { GetField("Q_USER1_1"), GetField("Q_USER1_2") }
                    .Where(q => !string.IsNullOrWhiteSpace(q)).ToList(),
                QuestionsForUser2 = new List<string> { GetField("Q_USER2_1"), GetField("Q_USER2_2") }
                    .Where(q => !string.IsNullOrWhiteSpace(q)).ToList(),
                Priority = Enum.TryParse<PathwayPriority>(priority, out var p) ? p : PathwayPriority.Medium,
                EstimatedGain = gain
            });
        }

        return pathways;
    }

    // ── AI: Narrative summary ─────────────────────────────────────────────────

    private async Task<string> GenerateNarrativeSummaryAsync(
        UserProfile user1,
        UserProfile user2,
        ConvergenceMap map,
        CancellationToken cancellationToken)
    {
        var prompt = $"""
        You are a neutral analyst describing the convergence landscape between two people.

        {user1.Name} and {user2.Name} have an overall convergence score of {map.OverallConvergenceScore:F1}/100.

        They share {JsonSerializer.Deserialize<List<int>>(map.SharedPropositionIdsJson)?.Count ?? 0} propositions in common
        and have {JsonSerializer.Deserialize<List<int>>(map.DisputedPropositionIdsJson)?.Count ?? 0} disputed propositions.

        Write a 2–3 sentence narrative summary of their convergence landscape suitable for display in a UI.
        Do NOT use names — say "User 1" and "User 2". Focus on:
        - What genuinely unites them
        - The nature of the real disagreement
        - The best opportunity for expanding shared understanding
        """;

        try
        {
            var kernel = _kernelService.GetKernel();
            var result = await kernel.InvokePromptAsync(prompt, cancellationToken: cancellationToken);
            return result.ToString().Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Narrative summary generation failed");
            return string.Empty;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static List<ConvergenceSnapshot> DeserializeHistory(string json)
    {
        try { return JsonSerializer.Deserialize<List<ConvergenceSnapshot>>(json) ?? new(); }
        catch { return new(); }
    }
}
