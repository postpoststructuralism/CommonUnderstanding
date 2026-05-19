using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using CommonUnderstanding.Data;
using CommonUnderstanding.Models;

namespace CommonUnderstanding.Services;

/// <summary>
/// Orchestrates multi-user collaborative analysis sessions.
/// Participants contribute arguments, which are merged into a joint proposition graph.
/// The session then runs the full Emergent Conclusions + Convergence Map pipeline over the combined corpus.
/// </summary>
public class CollaborativeSessionService
{
    private readonly ApplicationDbContext _db;
    private readonly UserProfileStore _profileStore;
    private readonly ArgumentDecompositionService _decompositionService;
    private readonly ComparativeAnalysisService _comparativeAnalysisService;
    private readonly EmergentConclusionsEngine _emergentConclusionsEngine;
    private readonly ConvergenceMapService _convergenceMapService;
    private readonly HarmonyDetector _harmonyDetector;
    private readonly ILogger<CollaborativeSessionService> _logger;

    private static readonly JsonSerializerOptions _json = new() { WriteIndented = false };

    public CollaborativeSessionService(
        ApplicationDbContext db,
        UserProfileStore profileStore,
        ArgumentDecompositionService decompositionService,
        ComparativeAnalysisService comparativeAnalysisService,
        EmergentConclusionsEngine emergentConclusionsEngine,
        ConvergenceMapService convergenceMapService,
        HarmonyDetector harmonyDetector,
        ILogger<CollaborativeSessionService> logger)
    {
        _db = db;
        _profileStore = profileStore;
        _decompositionService = decompositionService;
        _comparativeAnalysisService = comparativeAnalysisService;
        _emergentConclusionsEngine = emergentConclusionsEngine;
        _convergenceMapService = convergenceMapService;
        _harmonyDetector = harmonyDetector;
        _logger = logger;
    }

    // ── CRUD ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new collaborative session and invites the specified participants.
    /// </summary>
    public async Task<CollaborativeSession> CreateSessionAsync(
        string initiatorUserId,
        IEnumerable<string> invitedUserIds,
        string title,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        var allParticipants = new List<string>(invitedUserIds) { initiatorUserId };
        // Validate all participants exist
        foreach (var uid in allParticipants)
        {
            if (!_profileStore.ProfileExists(uid))
                throw new InvalidOperationException($"User {uid} not found.");
        }

        var session = new CollaborativeSession
        {
            Title = title,
            Description = description,
            ParticipantIdsJson = JsonSerializer.Serialize(allParticipants, _json),
            Status = SessionStatus.Active
        };

        _db.CollaborativeSessions.Add(session);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Collaborative session {Id} created by {U}", session.Id, initiatorUserId);
        return session;
    }

    public async Task<CollaborativeSession?> GetSessionAsync(int sessionId, CancellationToken cancellationToken = default)
    {
        return await _db.CollaborativeSessions.FindAsync([sessionId], cancellationToken);
    }

    /// <summary>
    /// Adds argument IDs contributed by a user to the session.
    /// </summary>
    public async Task ContributeArgumentsAsync(
        int sessionId,
        string userId,
        IEnumerable<int> argumentIds,
        CancellationToken cancellationToken = default)
    {
        var session = await _db.CollaborativeSessions.FindAsync([sessionId], cancellationToken)
            ?? throw new InvalidOperationException($"Session {sessionId} not found.");

        EnsureParticipant(session, userId);

        var contributions = DeserializeContributions(session.ContributedArgumentIdsJson);
        if (!contributions.TryGetValue(userId, out var existing))
            existing = contributions[userId] = new();

        foreach (var id in argumentIds)
            if (!existing.Contains(id)) existing.Add(id);

        session.ContributedArgumentIdsJson = JsonSerializer.Serialize(contributions, _json);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {U} contributed {N} arguments to session {S}",
            userId, argumentIds.Count(), sessionId);
    }

    // ── Analysis pipeline ─────────────────────────────────────────────────────

    /// <summary>
    /// Runs the full joint analysis pipeline for a session:
    ///   1. Decompose any undecomposed contributed arguments
    ///   2. Run comparative analysis on all cross-user argument pairs
    ///   3. Run Emergent Conclusions over the combined corpus
    ///   4. Generate a joint ConvergenceMap (2-participant sessions only)
    /// Updates the session record and transitions to Concluded.
    /// </summary>
    public async Task<CollaborativeSession> RunJointAnalysisAsync(
        int sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await _db.CollaborativeSessions.FindAsync([sessionId], cancellationToken)
            ?? throw new InvalidOperationException($"Session {sessionId} not found.");

        session.Status = SessionStatus.Analyzing;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Running joint analysis for session {Id}", sessionId);

        try
        {
            var contributions = DeserializeContributions(session.ContributedArgumentIdsJson);

            // ── Step 1: Decompose undecomposed arguments ──────────────────────
            var allArgIds = contributions.Values.SelectMany(ids => ids).Distinct().ToList();
            foreach (var argId in allArgIds)
            {
                var arg = await _db.Arguments.FindAsync(new object[] { argId }, cancellationToken);
                if (arg is not null && arg.Status == ArgumentStatus.Draft && !string.IsNullOrWhiteSpace(arg.RawText))
                {
                    _logger.LogInformation("Decomposing argument {Id} for joint session", argId);
                    await _decompositionService.DecomposeAsync(arg.RawText, onProgress: null, cancellationToken: cancellationToken);
                }
            }

            // ── Step 2: Cross-user comparative analysis ───────────────────────
            var userIds = contributions.Keys.ToList();
            for (int i = 0; i < userIds.Count; i++)
            {
                for (int j = i + 1; j < userIds.Count; j++)
                {
                    var argsA = contributions[userIds[i]];
                    var argsB = contributions[userIds[j]];

                    foreach (var argA in argsA)
                    {
                        foreach (var argB in argsB)
                        {
                            // Skip if already compared
                            var alreadyCompared = await _db.ArgumentComparisons
                                .AnyAsync(c =>
                                    (c.ArgumentAId == argA && c.ArgumentBId == argB) ||
                                    (c.ArgumentAId == argB && c.ArgumentBId == argA),
                                    cancellationToken);

                            if (!alreadyCompared)
                            {
                                _logger.LogInformation("Comparing arguments {A} ↔ {B}", argA, argB);
                                await _comparativeAnalysisService.CompareAsync(argA, argB, cancellationToken);
                            }
                        }
                    }
                }
            }

            // ── Step 3: Emergent conclusions over combined corpus ─────────────
            var report = await _emergentConclusionsEngine.GenerateReportAsync(
                deep: false, ct: cancellationToken);
            session.ConsolidatedReportJson = JsonSerializer.Serialize(report, _json);
            session.ExecutiveSummary = report.ExecutiveSummary;

            // Merge node IDs referenced by contributed arguments
            var mergedNodes = await GetMergedNodeIdsAsync(allArgIds, cancellationToken);
            session.MergedNodeIdsJson = JsonSerializer.Serialize(mergedNodes, _json);

            // ── Step 4: Joint ConvergenceMap (2-participant sessions) ─────────
            if (userIds.Count == 2)
            {
                var jointMap = await _convergenceMapService.GenerateAsync(userIds[0], userIds[1], cancellationToken);
                session.JointConvergenceMapId = jointMap.Id;
            }

            session.Status = SessionStatus.Concluded;
            session.ConcludedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Joint analysis complete for session {Id}", sessionId);
            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Joint analysis failed for session {Id}", sessionId);
            session.Status = SessionStatus.Active;  // roll back to editable state
            await _db.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void EnsureParticipant(CollaborativeSession session, string userId)
    {
        var participants = JsonSerializer.Deserialize<List<string>>(session.ParticipantIdsJson) ?? new();
        if (!participants.Contains(userId))
            throw new UnauthorizedAccessException($"User {userId} is not a participant in session {session.Id}.");
    }

    private static Dictionary<string, List<int>> DeserializeContributions(string json)
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, List<int>>>(json) ?? new(); }
        catch { return new(); }
    }

    private async Task<List<int>> GetMergedNodeIdsAsync(List<int> argumentIds, CancellationToken cancellationToken)
    {
        var allNodes = await _db.CommonUnderstandingNodes.ToListAsync(cancellationToken);
        return allNodes
            .Where(n =>
            {
                var ids = JsonSerializer.Deserialize<List<int>>(n.ArgumentIdsJson) ?? new();
                return ids.Intersect(argumentIds).Any();
            })
            .Select(n => n.Id)
            .ToList();
    }
}
