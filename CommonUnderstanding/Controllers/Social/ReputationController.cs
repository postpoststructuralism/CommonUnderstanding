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
/// Leaderboards, badges, XP, and streak data.
/// </summary>
[ApiController]
[Route("api/reputation")]
[Produces("application/json")]
public class ReputationController : ControllerBase
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly XPAwardService _xpAwards;

    public ReputationController(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        XPAwardService xpAwards)
    {
        _dbFactory = dbFactory;
        _xpAwards = xpAwards;
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
                rank = "Novice",
                currentStreak = 0,
                longestStreak = 0,
                streakFreezes = 0,
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
                rank = "Novice",
                longestStreak = 0,
                badges = Array.Empty<string>()
            });

        return Ok(new
        {
            userId,
            xp = rep.XP,
            rank = rep.Rank,
            longestStreak = rep.LongestStreak,
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

    /// <summary>GET /api/reputation/badges/{userId} — user's badge collection.</summary>
    [HttpGet("badges/{userId}")]
    public async Task<IActionResult> GetBadges(string userId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var rep = await db.UserReputations
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == userId, ct);

        var badges = rep?.Badges ?? Array.Empty<string>();

        var badgeDetails = badges.Select(b => new
        {
            id = b,
            name = FormatBadgeName(b),
            description = GetBadgeDescription(b)
        });

        return Ok(new { userId, badges = badgeDetails });
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

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string FormatBadgeName(string id) => id switch
    {
        "first_argument" => "First Post",
        "first_upvote" => "First Support",
        "chain_builder" => "Chain Builder",
        "worldview_author" => "Worldview Architect",
        "debate_winner" => "Debate Champion",
        "bridge_builder" => "Bridge Builder",
        "changed_mind" => "Humble Scholar",
        "epistemic_expert" => "Epistemic Expert",
        "streak_7" => "Weekly Warrior",
        "streak_30" => "Monthly Stalwart",
        "top_argument" => "Highly Upvoted",
        "convergence_catalyst" => "Convergence Catalyst",
        "fallacy_free" => "Logically Sound",
        "judge" => "Debate Judge",
        _ => id
    };

    private static string GetBadgeDescription(string id) => id switch
    {
        "first_argument" => "Published your first argument.",
        "first_upvote" => "Received your first upvote.",
        "chain_builder" => "Created an argument chain with 5+ arguments.",
        "worldview_author" => "Published a worldview.",
        "debate_winner" => "Won a structured debate.",
        "bridge_builder" => "Built a bridge argument between divergent worldviews.",
        "changed_mind" => "Received 5+ 'ChangedMyView' rationales.",
        "epistemic_expert" => "Achieved epistemic score of 4.0+ in a domain.",
        "streak_7" => "Maintained a 7-day daily activity streak.",
        "streak_30" => "Maintained a 30-day daily activity streak.",
        "top_argument" => "Argument received 100+ upvotes.",
        "convergence_catalyst" => "Facilitated convergence between 2+ worldviews.",
        "fallacy_free" => "All arguments passed AI fallacy validation (no flags).",
        "judge" => "Judged a structured debate.",
        _ => "Achievement unlocked."
    };
}

// ── Request DTOs ──────────────────────────────────────────────────────────────

public record AwardXPRequest(string UserId, long Amount, string Reason, Guid? ReferenceEntityId);
