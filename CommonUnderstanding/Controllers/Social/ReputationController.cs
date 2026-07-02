using CommonUnderstanding.Data;
using CommonUnderstanding.Models.Social;
using CommonUnderstanding.Services.Social;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CommonUnderstanding.Controllers.Social;

/// <summary>
/// User reputation and achievement endpoints.
/// Leaderboards, badges, XP, streak data, and resolution endorsements.
/// </summary>
[ApiController]
[Route("api/reputation")]
[Produces("application/json")]
public class ReputationController : ControllerBase
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly XPAwardService _xpAwards;
    private readonly BadgeAwardService _badgeAwards;
    private readonly ResolutionEndorsementService _endorsements;
    private readonly DmiScoreService _dmiService;

    public ReputationController(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        XPAwardService xpAwards,
        BadgeAwardService badgeAwards,
        ResolutionEndorsementService endorsements,
        DmiScoreService dmiService)
    {
        _dbFactory = dbFactory;
        _xpAwards = xpAwards;
        _badgeAwards = badgeAwards;
        _endorsements = endorsements;
        _dmiService = dmiService;
    }

    /// <summary>GET /api/reputation/me — caller's full reputation profile.</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetMyReputation(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var rep = await db.UserReputations
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == userId, ct);

        if (rep is null)
            return Ok(new
            {
                userId,
                xp = 0L,
                rank = "Strategist-in-Training",
                currentStreak = 0,
                longestStreak = 0,
                streakFreezes = 0,
                dmiScore = 0.0,
                badges = Array.Empty<string>()
            });

        return Ok(new
        {
            userId,
            xp = rep.XP,
            rank = rep.Rank,
            currentStreak = rep.CurrentStreak,
            longestStreak = rep.LongestStreak,
            streakFreezes = rep.StreakFreezes,
            dmiScore = rep.DmiScore,
            lastStreakDate = rep.LastStreakDate,
            badges = rep.Badges,
            lastActiveAt = rep.LastActiveAt
        });
    }

    /// <summary>GET /api/reputation/users/{userId} — public reputation profile (sans private fields).</summary>
    [HttpGet("users/{userId}")]
    public async Task<IActionResult> GetUserReputation(string userId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var rep = await db.UserReputations
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == userId, ct);

        if (rep is null)
            return Ok(new
            {
                userId,
                xp = 0L,
                rank = "Strategist-in-Training",
                longestStreak = 0,
                dmiScore = 0.0,
                badges = Array.Empty<string>()
            });

        return Ok(new
        {
            userId,
            xp = rep.XP,
            rank = rep.Rank,
            longestStreak = rep.LongestStreak,
            dmiScore = rep.DmiScore,
            badges = rep.Badges
        });
    }

    /// <summary>GET /api/reputation/xpleaderboard — XP leaderboard.</summary>
    [HttpGet("xpleaderboard")]
    public async Task<IActionResult> GetXPLeaderboard(
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var leaderboard = await db.UserReputations
            .AsNoTracking()
            .OrderByDescending(r => r.XP)
            .Take(Math.Min(limit, 100))
            .Select(r => new
            {
                userId = r.UserId,
                xp = r.XP,
                rank = r.Rank,
                dmiScore = r.DmiScore,
                badges = r.Badges.Length
            })
            .ToListAsync(ct);

        return Ok(new { items = leaderboard });
    }

    /// <summary>GET /api/reputation/streakleaderboard — longest streak leaderboard.</summary>
    [HttpGet("streakleaderboard")]
    public async Task<IActionResult> GetStreakLeaderboard(
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var leaderboard = await db.UserReputations
            .AsNoTracking()
            .Where(r => r.LongestStreak > 0)
            .OrderByDescending(r => r.LongestStreak)
            .Take(Math.Min(limit, 100))
            .Select(r => new
            {
                userId = r.UserId,
                longestStreak = r.LongestStreak,
                currentStreak = r.CurrentStreak,
                xp = r.XP
            })
            .ToListAsync(ct);

        return Ok(new { items = leaderboard });
    }

    /// <summary>GET /api/reputation/badges — list all badge definitions.</summary>
    [HttpGet("badges")]
    public IActionResult GetAllBadges()
    {
        var badges = BadgeRegistry.All.Values
            .Select(b => new
            {
                id = b.Id,
                name = b.Name,
                description = b.Description,
                tier = b.Tier
            })
            .OrderBy(b => b.tier)
            .ThenBy(b => b.name);

        return Ok(new { items = badges });
    }

    /// <summary>GET /api/reputation/badges/{userId} — user's badge collection with tier info.</summary>
    [HttpGet("badges/{userId}")]
    public async Task<IActionResult> GetBadges(string userId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var rep = await db.UserReputations
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == userId, ct);

        var earnedBadgeIds = rep?.Badges ?? Array.Empty<string>();

        var allBadges = BadgeRegistry.All.Values.Select(b => new
        {
            id = b.Id,
            name = b.Name,
            description = b.Description,
            tier = b.Tier,
            earned = earnedBadgeIds.Contains(b.Id)
        }).OrderBy(b => b.tier).ThenBy(b => b.name);

        return Ok(new { userId, badges = allBadges });
    }

    /// <summary>GET /api/reputation/badges/{badgeId}/holders — users who hold a specific badge.</summary>
    [HttpGet("badges/{badgeId}/holders")]
    public async Task<IActionResult> GetBadgeHolders(string badgeId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var holders = await db.UserReputations
            .AsNoTracking()
            .Where(r => r.Badges.Contains(badgeId))
            .Select(r => new
            {
                userId = r.UserId,
                xp = r.XP,
                rank = r.Rank
            })
            .ToListAsync(ct);

        return Ok(new { badgeId, holders });
    }

    /// <summary>GET /api/reputation/weekly-leaderboard — top 100 by XP earned in last 7 days.</summary>
    [HttpGet("weekly-leaderboard")]
    public async Task<IActionResult> GetWeeklyLeaderboard(
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);

        var leaderboard = await db.XPTransactions
            .AsNoTracking()
            .Where(t => t.CreatedAt >= sevenDaysAgo)
            .GroupBy(t => t.UserId)
            .Select(g => new
            {
                userId = g.Key,
                weeklyXp = g.Sum(t => t.Amount)
            })
            .OrderByDescending(x => x.weeklyXp)
            .Take(Math.Min(limit, 100))
            .ToListAsync(ct);

        return Ok(new { items = leaderboard });
    }

    /// <summary>GET /api/reputation/nexus-leaderboard — top 100 by resolution count.</summary>
    [HttpGet("nexus-leaderboard")]
    public async Task<IActionResult> GetNexusLeaderboard(
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var leaderboard = await db.StructuralResolutions
            .AsNoTracking()
            .Where(r => r.AuthorId != null)
            .GroupBy(r => r.AuthorId!)
            .Select(g => new
            {
                userId = g.Key,
                resolutionCount = g.Count()
            })
            .OrderByDescending(x => x.resolutionCount)
            .Take(Math.Min(limit, 100))
            .ToListAsync(ct);

        return Ok(new { items = leaderboard });
    }

    /// <summary>GET /api/reputation/mastery-leaderboard — top 100 by DmiScore.</summary>
    [HttpGet("mastery-leaderboard")]
    public async Task<IActionResult> GetMasteryLeaderboard(
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var leaderboard = await db.UserReputations
            .AsNoTracking()
            .Where(r => r.DmiScore > 0)
            .OrderByDescending(r => r.DmiScore)
            .Take(Math.Min(limit, 100))
            .Select(r => new
            {
                userId = r.UserId,
                dmiScore = r.DmiScore,
                xp = r.XP,
                rank = r.Rank
            })
            .ToListAsync(ct);

        return Ok(new { items = leaderboard });
    }

    /// <summary>POST /api/reputation/resolutions/{resolutionId}/endorse — endorse a resolution.</summary>
    [HttpPost("resolutions/{resolutionId:guid}/endorse")]
    [Authorize]
    public async Task<IActionResult> EndorseResolution(Guid resolutionId, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var added = await _endorsements.AddEndorsementAsync(resolutionId, userId, ct);

        if (!added)
            return Conflict(new { message = "You have already endorsed this resolution." });

        return Ok(new { message = "Resolution endorsed." });
    }

    /// <summary>DELETE /api/reputation/resolutions/{resolutionId}/endorse — remove endorsement.</summary>
    [HttpDelete("resolutions/{resolutionId:guid}/endorse")]
    [Authorize]
    public async Task<IActionResult> RemoveEndorsement(Guid resolutionId, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var removed = await _endorsements.RemoveEndorsementAsync(resolutionId, userId, ct);

        if (!removed)
            return NotFound(new { message = "Endorsement not found." });

        return Ok(new { message = "Endorsement removed." });
    }

    /// <summary>GET /api/reputation/resolutions/{resolutionId}/endorsements — list endorsers.</summary>
    [HttpGet("resolutions/{resolutionId:guid}/endorsements")]
    public async Task<IActionResult> GetEndorsements(Guid resolutionId, CancellationToken ct)
    {
        var endorsers = await _endorsements.GetEndorsersAsync(resolutionId, ct);
        return Ok(new { resolutionId, endorsers });
    }

    /// <summary>POST /api/reputation/admin/award-badge — manually award a badge (admin only).</summary>
    [HttpPost("admin/award-badge")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AwardBadgeManually(
        [FromBody] AwardBadgeRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var awarded = await _badgeAwards.AwardBadgeAsync(
            request.UserId, request.BadgeId, request.TriggerSummary, db, ct);

        if (!awarded)
            return Conflict(new { message = "User already holds this badge." });

        return Ok(new { message = $"Badge '{request.BadgeId}' awarded to {request.UserId}." });
    }

    /// <summary>POST /api/reputation/xptransaction — record manual XP award (admin only).</summary>
    [HttpPost("xptransaction")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AwardXP([FromBody] AwardXPRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var tx = new XPTransaction
        {
            UserId = request.UserId,
            Amount = request.Amount,
            Reason = request.Reason,
            ReferenceEntityId = request.ReferenceEntityId
        };

        db.XPTransactions.Add(tx);

        var rep = await db.UserReputations.FirstOrDefaultAsync(r => r.UserId == request.UserId, ct);
        if (rep is null)
        {
            rep = new UserReputation { UserId = request.UserId };
            db.UserReputations.Add(rep);
        }

        rep.XP = Math.Max(0, rep.XP + request.Amount);
        rep.Rank = XPAwardService.ComputeRank(rep.XP);
        rep.LastActiveAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Ok(new { userId = request.UserId, newXP = rep.XP, newRank = rep.Rank });
    }

    /// <summary>GET /api/reputation/xphistory — recent XP transactions for user.</summary>
    [HttpGet("xphistory")]
    [Authorize]
    public async Task<IActionResult> GetXPHistory([FromQuery] int limit = 20, CancellationToken ct = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var history = await db.XPTransactions
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .Take(Math.Min(limit, 100))
            .Select(t => new
            {
                id = t.Id,
                amount = t.Amount,
                reason = t.Reason,
                referenceEntityId = t.ReferenceEntityId,
                createdAt = t.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(new { items = history });
    }
}

// ── Request DTOs ──────────────────────────────────────────────────────────────

public record AwardXPRequest(string UserId, long Amount, string Reason, Guid? ReferenceEntityId);

public record AwardBadgeRequest(string UserId, string BadgeId, string? TriggerSummary);