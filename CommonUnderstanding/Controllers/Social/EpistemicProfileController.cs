using CommonUnderstanding.Data;
using CommonUnderstanding.Models.Social;
using CommonUnderstanding.Services.Social;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CommonUnderstanding.Controllers.Social;

/// <summary>
/// Epistemic profile endpoints — per-user, per-domain reputation scores.
/// Scores are computed by EpistemicScoringWorker every 15 minutes.
/// </summary>
[ApiController]
[Route("api/epistemic")]
[Produces("application/json")]
public class EpistemicProfileController : ControllerBase
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public EpistemicProfileController(IDbContextFactory<ApplicationDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    /// <summary>GET /api/epistemic/me — caller's full epistemic profile across all domains.</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetMyProfile(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var profiles = await db.EpistemicProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.EpistemicScore)
            .ToListAsync(ct);

        return Ok(new
        {
            userId,
            profiles = profiles.Select(p => new
            {
                topicDomain = p.TopicDomain,
                epistemicScore = p.EpistemicScore,
                voteAccuracy = p.VoteAccuracy,
                contributionCount = p.ContributionCount,
                voteCount = p.VoteCount,
                updatedAt = p.UpdatedAt
            })
        });
    }

    /// <summary>GET /api/epistemic/users/{userId} — public epistemic profile.</summary>
    [HttpGet("users/{userId}")]
    public async Task<IActionResult> GetUserProfile(string userId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var profiles = await db.EpistemicProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.EpistemicScore)
            .ToListAsync(ct);

        return Ok(new
        {
            userId,
            profiles = profiles.Select(p => new
            {
                topicDomain = p.TopicDomain,
                epistemicScore = p.EpistemicScore,
                contributionCount = p.ContributionCount
            })
        });
    }

    /// <summary>GET /api/epistemic/leaderboard — leaderboard by domain.</summary>
    [HttpGet("leaderboard")]
    public async Task<IActionResult> GetLeaderboard(
        [FromQuery] string? domain,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var query = db.EpistemicProfiles.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(domain))
            query = query.Where(p => p.TopicDomain == domain);

        var leaderboard = await query
            .OrderByDescending(p => p.EpistemicScore)
            .Take(Math.Min(limit, 100))
            .Select(p => new
            {
                userId = p.UserId,
                topicDomain = p.TopicDomain,
                epistemicScore = p.EpistemicScore,
                contributionCount = p.ContributionCount
            })
            .ToListAsync(ct);

        return Ok(new { items = leaderboard });
    }

    /// <summary>GET /api/epistemic/domains — list all active topic domains.</summary>
    [HttpGet("domains")]
    public async Task<IActionResult> GetDomains(CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var domains = await db.EpistemicProfiles
            .AsNoTracking()
            .GroupBy(p => p.TopicDomain)
            .Select(g => new
            {
                domain = g.Key,
                participantCount = g.Count(),
                averageScore = g.Average(p => p.EpistemicScore)
            })
            .OrderByDescending(d => d.participantCount)
            .ToListAsync(ct);

        return Ok(new { items = domains });
    }
}
