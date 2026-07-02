using CommonUnderstanding.Data;
using CommonUnderstanding.Models.Social;
using CommonUnderstanding.Services.Social;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CommonUnderstanding.Hubs;

/// <summary>
/// SignalR hub for real-time reputation updates.
/// Clients receive badge awards, rank changes, and leaderboard updates.
/// </summary>
[Authorize]
public class ReputationHub : Hub
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly ILogger<ReputationHub> _logger;

    public ReputationHub(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        ILogger<ReputationHub> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    /// <summary>
    /// Called when a client connects. Adds them to a personal group for targeted updates.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
            _logger.LogDebug("ReputationHub: User {UserId} connected (connection {ConnectionId})",
                userId, Context.ConnectionId);
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is not null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
        }
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Client requests their current reputation snapshot.
    /// </summary>
    public async Task RequestReputation()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return;

        await using var db = await _dbFactory.CreateDbContextAsync();
        var rep = await db.UserReputations
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == userId);

        if (rep is null) return;

        await Clients.Caller.SendAsync("ReputationUpdated", new
        {
            userId = rep.UserId,
            xp = rep.XP,
            rank = rep.Rank,
            currentStreak = rep.CurrentStreak,
            longestStreak = rep.LongestStreak,
            dmiScore = rep.DmiScore,
            badgeCount = rep.Badges.Length,
            badges = rep.Badges
        });
    }

    /// <summary>
    /// Broadcasts a badge award to a specific user.
    /// Called from BadgeAwardService after a badge is earned.
    /// </summary>
    public async Task NotifyBadgeAwarded(string userId, string badgeId, string badgeName, string tier)
    {
        await Clients.Group($"user_{userId}").SendAsync("BadgeAwarded", new
        {
            badgeId,
            badgeName,
            tier,
            awardedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Broadcasts a rank change to a specific user.
    /// </summary>
    public async Task NotifyRankChanged(string userId, string newRank, long xp)
    {
        await Clients.Group($"user_{userId}").SendAsync("RankChanged", new
        {
            newRank,
            xp,
            changedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Broadcasts a leaderboard update to all connected clients.
    /// </summary>
    public async Task NotifyLeaderboardUpdate(string leaderboardType)
    {
        await Clients.All.SendAsync("LeaderboardUpdated", new
        {
            type = leaderboardType,
            updatedAt = DateTime.UtcNow
        });
    }
}