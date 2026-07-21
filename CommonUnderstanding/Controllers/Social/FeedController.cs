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

    public FeedController(FeedService feedService)
    {
        _feedService = feedService;
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
}
