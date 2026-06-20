using CommonUnderstanding.Data;
using CommonUnderstanding.Models.Social;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Hubs;

/// <summary>
/// Structured Phase 2 debate hub.
/// All contributions reference existing SocialArguments — no free-form text posts.
///
/// Client → Server:
///   JoinDebate(Guid debateRoomId)
///   LeaveDebate(Guid debateRoomId)
///   SubmitArgument(Guid debateRoomId, Guid argumentId, string role)
///   JudgeScore(Guid debateRoomId, string scoredUserId, double score, string? comment)
///   RequestAIReferee(Guid debateRoomId, Guid contributionId)
///
/// Server → Client:
///   "ContributionAdded"  — DebateContributionDto
///   "ScoreUpdated"       — { userId, score }
///   "AIRefereeFlag"      — { contributionId, fallacies[], validityScore, comment }
///   "DebateConcluded"    — { proponentScore, opponentScore, winner }
///   "SpectatorCount"     — { count }
/// </summary>
[Authorize]
public class Phase2DebateHub : Hub
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly Services.Social.Plugins.FallacyDetectionPlugin _fallacyPlugin;
    private readonly Services.Social.XPAwardService _xpAwards;
    private readonly ILogger<Phase2DebateHub> _logger;

    // Track spectator counts per room (in-memory; use Redis in multi-server deployments)
    private static readonly Dictionary<Guid, HashSet<string>> _roomConnections = new();
    private static readonly object _roomLock = new();

    public Phase2DebateHub(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        Services.Social.Plugins.FallacyDetectionPlugin fallacyPlugin,
        Services.Social.XPAwardService xpAwards,
        ILogger<Phase2DebateHub> logger)
    {
        _dbFactory = dbFactory;
        _fallacyPlugin = fallacyPlugin;
        _xpAwards = xpAwards;
        _logger = logger;
    }

    // ── Room presence ─────────────────────────────────────────────────────────

    public async Task JoinDebate(Guid debateRoomId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroup(debateRoomId));

        lock (_roomLock)
        {
            if (!_roomConnections.TryGetValue(debateRoomId, out var connections))
            {
                connections = new HashSet<string>();
                _roomConnections[debateRoomId] = connections;
            }
            connections.Add(Context.ConnectionId);
        }

        int count = GetSpectatorCount(debateRoomId);
        await Clients.Group(RoomGroup(debateRoomId))
            .SendAsync("SpectatorCount", new { count }, Context.ConnectionAborted);
    }

    public async Task LeaveDebate(Guid debateRoomId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, RoomGroup(debateRoomId));

        lock (_roomLock)
        {
            if (_roomConnections.TryGetValue(debateRoomId, out var connections))
                connections.Remove(Context.ConnectionId);
        }

        int count = GetSpectatorCount(debateRoomId);
        await Clients.Group(RoomGroup(debateRoomId))
            .SendAsync("SpectatorCount", new { count }, Context.ConnectionAborted);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Clean up from all rooms this connection was in
        lock (_roomLock)
        {
            foreach (var room in _roomConnections.Values)
                room.Remove(Context.ConnectionId);
        }
        await base.OnDisconnectedAsync(exception);
    }

    // ── Contributions ─────────────────────────────────────────────────────────

    public async Task SubmitArgument(Guid debateRoomId, Guid argumentId, string role)
    {
        var userId = Context.UserIdentifier!;

        if (!Enum.TryParse<DebateRole>(role, ignoreCase: true, out var debateRole))
        {
            await Clients.Caller.SendAsync("Error", new { message = $"Invalid role: {role}" });
            return;
        }

        await using var db = await _dbFactory.CreateDbContextAsync(Context.ConnectionAborted);

        var room = await db.DebateRooms
            .Include(r => r.Contributions)
            .FirstOrDefaultAsync(r => r.Id == debateRoomId, Context.ConnectionAborted);

        if (room is null || room.Status == DebateStatus.Concluded || room.Status == DebateStatus.Cancelled)
        {
            await Clients.Caller.SendAsync("Error", new { message = "Debate room is not active." });
            return;
        }

        // Validate the user is allowed to contribute in this role
        if (!IsAuthorizedToContribute(room, userId, debateRole))
        {
            await Clients.Caller.SendAsync("Error", new { message = "You are not authorized to submit in this role." });
            return;
        }

        // Enforce per-side contribution cap
        if (debateRole != DebateRole.JudgeComment)
        {
            int sideContributions = room.Contributions.Count(c =>
                (debateRole == DebateRole.Proponent && c.UserId == room.ProponentUserId) ||
                (debateRole == DebateRole.Opponent  && c.UserId == room.OpponentUserId));

            if (sideContributions >= room.MaxContributionsPerSide)
            {
                await Clients.Caller.SendAsync("Error", new { message = "Maximum contributions per side reached." });
                return;
            }
        }

        // Validate the argument exists and is accessible
        var argument = await db.SocialArguments
            .Include(a => a.ClaimProposition)
            .FirstOrDefaultAsync(a => a.Id == argumentId, Context.ConnectionAborted);

        if (argument is null || (!argument.IsPublic && argument.UserId != userId))
        {
            await Clients.Caller.SendAsync("Error", new { message = "Argument not found or not accessible." });
            return;
        }

        // Activate room on first contribution
        if (room.Status == DebateStatus.Open)
            room.Status = DebateStatus.Active;

        var contribution = new DebateContribution
        {
            DebateRoomId = debateRoomId,
            UserId = userId,
            ArgumentId = argumentId,
            Role = debateRole,
            OrderIndex = room.Contributions.Count
        };
        db.DebateContributions.Add(contribution);
        await db.SaveChangesAsync(Context.ConnectionAborted);

        // Broadcast to room immediately
        var dto = MapContributionToDto(contribution, argument);
        await Clients.Group(RoomGroup(debateRoomId))
            .SendAsync("ContributionAdded", dto, Context.ConnectionAborted);

        // AI referee — fire and forget, results arrive asynchronously
        if (room.AIRefereeEnabled)
            _ = RunAIRefereeAsync(contribution.Id, argument, room, debateRoomId);
    }

    public async Task JudgeScore(Guid debateRoomId, string scoredUserId, double score, string? comment)
    {
        var judgeId = Context.UserIdentifier!;

        await using var db = await _dbFactory.CreateDbContextAsync(Context.ConnectionAborted);

        var room = await db.DebateRooms
            .FirstOrDefaultAsync(r => r.Id == debateRoomId, Context.ConnectionAborted);

        if (room is null || !room.JudgeUserIds.Contains(judgeId))
        {
            await Clients.Caller.SendAsync("Error", new { message = "Not a judge in this room." });
            return;
        }

        // Update the relevant score
        if (scoredUserId == room.ProponentUserId)
            room.ProponentScore = (room.ProponentScore ?? 0) + score;
        else if (scoredUserId == room.OpponentUserId)
            room.OpponentScore = (room.OpponentScore ?? 0) + score;

        await db.SaveChangesAsync(Context.ConnectionAborted);

        await Clients.Group(RoomGroup(debateRoomId))
            .SendAsync("ScoreUpdated", new { userId = scoredUserId, score }, Context.ConnectionAborted);
    }

    public async Task ConcludeDebate(Guid debateRoomId)
    {
        var userId = Context.UserIdentifier!;

        await using var db = await _dbFactory.CreateDbContextAsync(Context.ConnectionAborted);

        var room = await db.DebateRooms
            .FirstOrDefaultAsync(r => r.Id == debateRoomId, Context.ConnectionAborted);

        if (room is null) return;

        // Only judges or the proponent can conclude
        bool isJudge = room.JudgeUserIds.Contains(userId);
        bool isProponent = room.ProponentUserId == userId;
        if (!isJudge && !isProponent) return;

        room.Status = DebateStatus.Concluded;
        room.ConcludedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(Context.ConnectionAborted);

        // Determine winner
        string? winner = null;
        if (room.ProponentScore.HasValue && room.OpponentScore.HasValue)
        {
            winner = room.ProponentScore > room.OpponentScore
                ? room.ProponentUserId
                : room.OpponentUserId;
        }

        await Clients.Group(RoomGroup(debateRoomId))
            .SendAsync("DebateConcluded", new
            {
                proponentScore = room.ProponentScore,
                opponentScore = room.OpponentScore,
                winner
            }, Context.ConnectionAborted);

        // Award XP
        if (winner is not null)
        {
            await _xpAwards.AwardAsync(winner, 50, "Won a Debate Room", debateRoomId, Context.ConnectionAborted);
            var loser = winner == room.ProponentUserId ? room.OpponentUserId : room.ProponentUserId;
            if (loser is not null)
                await _xpAwards.AwardAsync(loser, 10, "Participated in Debate Room", debateRoomId, Context.ConnectionAborted);
        }
    }

    // ── AI referee (async, results pushed via SignalR) ────────────────────────

    private async Task RunAIRefereeAsync(
        Guid contributionId,
        SocialArgument argument,
        DebateRoom room,
        Guid debateRoomId)
    {
        try
        {
            string argumentText = BuildArgumentText(argument);
            string priorContext = BuildPriorContext(room);

            var result = await _fallacyPlugin.DetectFallaciesAsync(
                argumentText, priorContext, room.MotionText,
                CancellationToken.None);

            // Persist referee output
            await using var db = await _dbFactory.CreateDbContextAsync(CancellationToken.None);
            var contribution = await db.DebateContributions
                .FirstOrDefaultAsync(c => c.Id == contributionId, CancellationToken.None);

            if (contribution is not null)
            {
                contribution.FallacyFlags = System.Text.Json.JsonSerializer.Serialize(result.Fallacies);
                contribution.ValidityScore = result.ValidityScore;
                contribution.AIRefereeComment = result.SuggestedImprovement;
                await db.SaveChangesAsync(CancellationToken.None);
            }

            // Push results to room
            await Clients.Group(RoomGroup(debateRoomId))
                .SendAsync("AIRefereeFlag", new
                {
                    contributionId,
                    fallacies = result.Fallacies,
                    validityScore = result.ValidityScore,
                    comment = result.SuggestedImprovement
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI referee failed for contribution {ContributionId}", contributionId);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsAuthorizedToContribute(DebateRoom room, string userId, DebateRole role)
    {
        return role switch
        {
            DebateRole.Proponent   => room.ProponentUserId == userId,
            DebateRole.Opponent    => room.OpponentUserId == userId,
            DebateRole.Rebuttal    => room.ProponentUserId == userId || room.OpponentUserId == userId,
            DebateRole.JudgeComment => room.JudgeUserIds.Contains(userId),
            _ => false
        };
    }

    private static object MapContributionToDto(DebateContribution c, SocialArgument arg) => new
    {
        id = c.Id,
        debateRoomId = c.DebateRoomId,
        userId = c.UserId,
        argumentId = c.ArgumentId,
        argumentTitle = arg.Title,
        claimText = arg.ClaimProposition?.Text,
        role = c.Role.ToString(),
        orderIndex = c.OrderIndex,
        createdAt = c.CreatedAt
    };

    private static string BuildArgumentText(SocialArgument arg) =>
        $"Claim: {arg.ClaimProposition?.Text ?? string.Empty}\nWarrant: {arg.WarrantText}";

    private static string BuildPriorContext(DebateRoom room)
    {
        // Load prior contributions summaries for context (loaded already via Include)
        return $"Motion: {room.MotionText}. Prior contributions: {room.Contributions.Count}.";
    }

    private static string RoomGroup(Guid debateRoomId) => $"debate-{debateRoomId}";

    private static int GetSpectatorCount(Guid debateRoomId)
    {
        lock (_roomLock)
        {
            return _roomConnections.TryGetValue(debateRoomId, out var s) ? s.Count : 0;
        }
    }
}
