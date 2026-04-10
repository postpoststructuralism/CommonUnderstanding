using System.Text.Json;
using CommonUnderstanding.Data;
using CommonUnderstanding.Models;

namespace CommonUnderstanding.Services;

/// <summary>
/// Iterative session engine that drives convergence expansion between two users.
/// Selects the highest-priority ExpansionPathway, issues targeted questions to each user,
/// then re-runs ConvergenceMapService after each response pair to track growth.
/// </summary>
public class ConvergenceExpansionService
{
    private readonly ApplicationDbContext _db;
    private readonly UserProfileStore _profileStore;
    private readonly ConvergenceMapService _convergenceMapService;
    private readonly BeliefDiscoveryOrchestrator _orchestrator;
    private readonly ILogger<ConvergenceExpansionService> _logger;

    private static readonly JsonSerializerOptions _json = new() { WriteIndented = false };

    public ConvergenceExpansionService(
        ApplicationDbContext db,
        UserProfileStore profileStore,
        ConvergenceMapService convergenceMapService,
        BeliefDiscoveryOrchestrator orchestrator,
        ILogger<ConvergenceExpansionService> logger)
    {
        _db = db;
        _profileStore = profileStore;
        _convergenceMapService = convergenceMapService;
        _orchestrator = orchestrator;
        _logger = logger;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the first pending question pair for the highest-priority pathway on this map.
    /// The returned pair contains one interaction targeting User 1 and one targeting User 2.
    /// </summary>
    public (UserInteraction? ForUser1, UserInteraction? ForUser2) GetNextQuestionPair(ConvergenceMap map)
    {
        var pathway = SelectActivePathway(map);
        if (pathway is null) return (null, null);

        var q1 = pathway.QuestionsForUser1.FirstOrDefault();
        var q2 = pathway.QuestionsForUser2.FirstOrDefault();

        UserInteraction? interaction1 = q1 is not null
            ? BuildInteraction(map.User1Id, q1, pathway.DivergenceDescription)
            : null;

        UserInteraction? interaction2 = q2 is not null
            ? BuildInteraction(map.User2Id, q2, pathway.DivergenceDescription)
            : null;

        return (interaction1, interaction2);
    }

    /// <summary>
    /// Processes a completed response from one user in a convergence expansion session.
    /// Updates that user's belief snapshot via the full discovery pipeline, then
    /// refreshes the convergence map to reflect the new state.
    /// Returns the updated map.
    /// </summary>
    public async Task<ConvergenceMap> ProcessResponseAsync(
        int mapId,
        string userId,
        UserInteraction completedInteraction,
        CancellationToken cancellationToken = default)
    {
        var map = await _db.ConvergenceMaps.FindAsync([mapId], cancellationToken)
            ?? throw new InvalidOperationException($"ConvergenceMap {mapId} not found.");

        var profile = _profileStore.GetProfile(userId)
            ?? throw new InvalidOperationException($"User {userId} not found.");

        _logger.LogInformation("Processing expansion response from {UserId} on map {MapId}", userId, mapId);

        // Run through the full belief discovery pipeline
        await _orchestrator.ProcessResponseAndContinueAsync(profile, completedInteraction);
        // (Discard the "next question" returned — questions in expansion sessions come from pathways)

        // Re-compute the convergence map now that one user's snapshot has changed
        var otherUserId = map.User1Id == userId ? map.User2Id : map.User1Id;
        var refreshed = await _convergenceMapService.GenerateAsync(userId, otherUserId, cancellationToken);

        _logger.LogInformation("Map {MapId} refreshed. New score: {Score:F1}", mapId, refreshed.OverallConvergenceScore);
        return refreshed;
    }

    /// <summary>
    /// Returns a delta summary comparing the current convergence score with the baseline
    /// recorded at the start of expansion sessions.
    /// </summary>
    public ConvergenceExpansionSummary GetSessionSummary(ConvergenceMap map)
    {
        var history = DeserializeHistory(map.EvolutionHistoryJson);
        if (history.Count < 2)
        {
            return new ConvergenceExpansionSummary
            {
                CurrentScore = map.OverallConvergenceScore,
                BaselineScore = map.OverallConvergenceScore,
                Delta = 0
            };
        }

        // Baseline: first snapshot that doesn't have TriggerEvent = "Initial"
        var baseline = history.FirstOrDefault(s => s.TriggerEvent == "Initial")
            ?? history.First();

        return new ConvergenceExpansionSummary
        {
            CurrentScore = map.OverallConvergenceScore,
            BaselineScore = baseline.ConvergenceScore,
            Delta = map.OverallConvergenceScore - baseline.ConvergenceScore,
            SharedPropositionGain = (DeserializeIds(map.SharedPropositionIdsJson).Count) - baseline.SharedPropositionCount,
            DisputedPropositionReduction = baseline.DisputedPropositionCount - (DeserializeIds(map.DisputedPropositionIdsJson).Count),
            SnapshotCount = history.Count
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ExpansionPathway? SelectActivePathway(ConvergenceMap map)
    {
        var pathways = DeserializePathways(map.ExpansionPathwaysJson);
        return pathways
            .OrderByDescending(p => p.Priority)
            .ThenByDescending(p => p.EstimatedGain)
            .FirstOrDefault(p => p.QuestionsForUser1.Count > 0 || p.QuestionsForUser2.Count > 0);
    }

    private static UserInteraction BuildInteraction(string userId, string questionText, string context)
    {
        return new UserInteraction
        {
            UserId = userId,
            Content = new InteractionContent
            {
                Question = questionText,
                Context = context,
                Format = InteractionFormat.OpenText
            },
            Response = new UserResponse(),
            Type = InteractionType.OpenEndedQuestion,
            TargetedDimensions = new() { "convergence_expansion" }
        };
    }

    private static List<ExpansionPathway> DeserializePathways(string json)
    {
        try { return JsonSerializer.Deserialize<List<ExpansionPathway>>(json) ?? new(); }
        catch { return new(); }
    }

    private static List<ConvergenceSnapshot> DeserializeHistory(string json)
    {
        try { return JsonSerializer.Deserialize<List<ConvergenceSnapshot>>(json) ?? new(); }
        catch { return new(); }
    }

    private static List<int> DeserializeIds(string json)
    {
        try { return JsonSerializer.Deserialize<List<int>>(json) ?? new(); }
        catch { return new(); }
    }
}

// ─────────────────────────────────────────────
//  Summary DTO
// ─────────────────────────────────────────────

public class ConvergenceExpansionSummary
{
    public double CurrentScore { get; set; }
    public double BaselineScore { get; set; }
    public double Delta { get; set; }
    public int SharedPropositionGain { get; set; }
    public int DisputedPropositionReduction { get; set; }
    public int SnapshotCount { get; set; }
}
