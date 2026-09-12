using CommonUnderstanding.Services.Social;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CommonUnderstanding.Controllers.Social;

/// <summary>
/// Social feed endpoints for authenticated and anonymous users.
/// </summary>
[ApiController]
[Route("api/feed")]
[Produces("application/json")]
public class FeedController : ControllerBase
{
    private readonly FeedService _feedService;
    private readonly IFeedRankingService _rankingService;

    public FeedController(FeedService feedService, IFeedRankingService rankingService)
    {
        _feedService = feedService;
        _rankingService = rankingService;
    }

    /// <summary>GET /api/feed — public feed with various sort/filter options.</summary>
    [HttpGet]
    public async Task<IActionResult> GetPublicFeed(
        [FromQuery] int limit = 20,
        [FromQuery] string sort = "recent",
        [FromQuery] string? domain = null,
        [FromQuery] string? tags = null,
        CancellationToken ct = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var tagArray = string.IsNullOrEmpty(tags)
            ? null
            : tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var result = await _feedService.GetFeedAsync(userId, sort, domain, tagArray, limit, ct);
        return Ok(result);
    }

    /// <summary>GET /api/feed/user — personalized feed for caller (requires auth).</summary>
    [HttpGet("user")]
    [Authorize]
    public async Task<IActionResult> GetUserFeed([FromQuery] int limit = 20, CancellationToken ct = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var items = await _feedService.GetUserFeedAsync(userId, limit, ct);
        return Ok(new { items });
    }

    /// <summary>GET /api/feed/recommended — multi-lane personalized ranking.</summary>
    [HttpGet("recommended")]
    public async Task<IActionResult> GetRecommendedFeed(
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 20,
        [FromQuery] string? tags = null,
        CancellationToken ct = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var tagArray = string.IsNullOrWhiteSpace(tags)
            ? null
            : tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return Ok(await _rankingService.GetFeedAsync(userId, offset, limit, tagArray, ct));
    }

    [HttpGet("preferences")]
    [Authorize]
    public async Task<IActionResult> GetPreferences(CancellationToken ct = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return userId is null ? Unauthorized() : Ok(await _rankingService.GetPreferencesAsync(userId, ct));
    }

    [HttpPut("preferences")]
    [Authorize]
    public async Task<IActionResult> SavePreferences([FromBody] UpdateFeedPreferencesDto update, CancellationToken ct = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return userId is null ? Unauthorized() : Ok(await _rankingService.SavePreferencesAsync(userId, update, ct));
    }

    [HttpPost("engagement")]
    [Authorize]
    public async Task<IActionResult> RecordEngagement([FromBody] FeedEngagementDto engagement, CancellationToken ct = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();
        await _rankingService.RecordEngagementAsync(userId, engagement, ct);
        return NoContent();
    }
}
