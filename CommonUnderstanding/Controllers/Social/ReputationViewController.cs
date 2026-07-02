using CommonUnderstanding.Data;
using CommonUnderstanding.Services.Social;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Controllers.Social;

/// <summary>
/// MVC controller for badge system frontend pages.
/// Serves Leaderboard, Badge Gallery, and How It Works pages.
/// </summary>
[Route("reputation")]
public class ReputationViewController : Controller
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public ReputationViewController(IDbContextFactory<ApplicationDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    /// <summary>GET /reputation/leaderboard — tabbed leaderboard page.</summary>
    [HttpGet("leaderboard")]
    public async Task<IActionResult> Leaderboard(CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // XP Overall (top 100)
        var xpOverall = await db.UserReputations
            .AsNoTracking()
            .OrderByDescending(r => r.XP)
            .Take(100)
            .Select(r => new
            {
                userId = r.UserId,
                xp = r.XP,
                rank = r.Rank,
                dmiScore = r.DmiScore,
                badgeCount = r.Badges.Length
            })
            .ToListAsync(ct);

        // Weekly XP (top 100)
        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
        var weekly = await db.XPTransactions
            .AsNoTracking()
            .Where(t => t.CreatedAt >= sevenDaysAgo)
            .GroupBy(t => t.UserId)
            .Select(g => new
            {
                userId = g.Key,
                weeklyXp = g.Sum(t => t.Amount)
            })
            .OrderByDescending(x => x.weeklyXp)
            .Take(100)
            .ToListAsync(ct);

        // Nexus Builders (top 100)
        var nexus = await db.StructuralResolutions
            .AsNoTracking()
            .Where(r => r.AuthorId != null)
            .GroupBy(r => r.AuthorId!)
            .Select(g => new
            {
                userId = g.Key,
                resolutionCount = g.Count()
            })
            .OrderByDescending(x => x.resolutionCount)
            .Take(100)
            .ToListAsync(ct);

        // Mastery (top 100)
        var mastery = await db.UserReputations
            .AsNoTracking()
            .Where(r => r.DmiScore > 0)
            .OrderByDescending(r => r.DmiScore)
            .Take(100)
            .Select(r => new
            {
                userId = r.UserId,
                dmiScore = r.DmiScore,
                xp = r.XP,
                rank = r.Rank
            })
            .ToListAsync(ct);

        ViewBag.XPOverall = xpOverall;
        ViewBag.Weekly = weekly;
        ViewBag.Nexus = nexus;
        ViewBag.Mastery = mastery;

        return View();
    }

    /// <summary>GET /reputation/badges — badge gallery page.</summary>
    [HttpGet("badges")]
    public async Task<IActionResult> BadgeGallery([FromQuery] string? userId, CancellationToken ct)
    {
        var allBadges = BadgeRegistry.All.Values
            .Select(b => new BadgeViewModel
            {
                Id = b.Id,
                Name = b.Name,
                Description = b.Description,
                Tier = b.Tier,
                Earned = false
            })
            .OrderBy(b => b.TierOrder)
            .ThenBy(b => b.Name)
            .ToList();

        HashSet<string> earnedBadgeIds = new();

        if (!string.IsNullOrEmpty(userId))
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            var rep = await db.UserReputations
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.UserId == userId, ct);

            if (rep?.Badges != null)
                earnedBadgeIds = new HashSet<string>(rep.Badges);
        }

        foreach (var badge in allBadges)
        {
            badge.Earned = earnedBadgeIds.Contains(badge.Id);
        }

        ViewBag.UserId = userId;
        return View(allBadges);
    }

    /// <summary>GET /reputation/how-it-works — static info page.</summary>
    [HttpGet("how-it-works")]
    public IActionResult HowItWorks()
    {
        return View();
    }
}

public class BadgeViewModel
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Tier { get; set; } = null!;
    public bool Earned { get; set; }

    public int TierOrder => Tier switch
    {
        "Bronze" => 0,
        "Silver" => 1,
        "Gold" => 2,
        "Platinum" => 3,
        _ => 99
    };

    public string TierIcon => Tier switch
    {
        "Bronze" => "🥉",
        "Silver" => "🥈",
        "Gold" => "🥇",
        "Platinum" => "💎",
        _ => "🏅"
    };

    public string TierColor => Tier switch
    {
        "Bronze" => "#cd7f32",
        "Silver" => "#a8a8a8",
        "Gold" => "#ffd700",
        "Platinum" => "#00bfff",
        _ => "#666"
    };
}