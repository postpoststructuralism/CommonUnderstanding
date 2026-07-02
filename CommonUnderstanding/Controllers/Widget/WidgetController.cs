using CommonUnderstanding.Authentication;
using CommonUnderstanding.Data;
using CommonUnderstanding.Models.Widget;
using CommonUnderstanding.Models.Widget.DTOs;
using CommonUnderstanding.Services.Widget;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Controllers.Widget;

/// <summary>
/// REST API for the embeddable comments widget.
/// Publisher-facing endpoints use cookie auth; widget endpoints use API key auth.
/// </summary>
[ApiController]
public class WidgetController : ControllerBase
{
    private readonly ThreadService _threadService;
    private readonly WidgetModerationService _moderationService;
    private readonly WidgetAnalyticsService _analyticsService;
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ILogger<WidgetController> _logger;

    public WidgetController(
        ThreadService threadService,
        WidgetModerationService moderationService,
        WidgetAnalyticsService analyticsService,
        IDbContextFactory<ApplicationDbContext> contextFactory,
        ILogger<WidgetController> logger)
    {
        _threadService = threadService;
        _moderationService = moderationService;
        _analyticsService = analyticsService;
        _contextFactory = contextFactory;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Publisher Site Management (cookie auth)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Register a new publisher site.</summary>
    [HttpPost("api/widget/sites")]
    [Authorize]
    public async Task<ActionResult<RegisterSiteResponse>> RegisterSite(
        [FromBody] RegisterSiteRequest request)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        await using var db = await _contextFactory.CreateDbContextAsync();

        var apiKey = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

        var site = new CommentSite
        {
            OwnerUserId = userId,
            Domain = request.Domain,
            SiteName = request.SiteName,
            PlanTier = request.PlanTier ?? "free",
            ApiKey = apiKey,
            AllowedOrigins = request.AllowedOrigins ?? new[] { request.Domain },
            CustomCssUrl = request.CustomCssUrl,
            LogoUrl = request.LogoUrl,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.CommentSites.Add(site);
        await db.SaveChangesAsync();

        _logger.LogInformation("New publisher site registered: {SiteName} ({Domain})", site.SiteName, site.Domain);

        return Ok(new RegisterSiteResponse(
            SiteId: site.Id,
            ApiKey: apiKey,
            EmbedScriptUrl: $"/widget/v1/embed.js?site={site.Id}",
            DashboardUrl: $"/Widget/Dashboard/{site.Id}"
        ));
    }

    /// <summary>Get the current user's sites.</summary>
    [HttpGet("api/widget/sites")]
    [Authorize]
    public async Task<ActionResult<List<object>>> GetMySites()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        await using var db = await _contextFactory.CreateDbContextAsync();

        var sites = await db.CommentSites
            .Where(s => s.OwnerUserId == userId)
            .Select(s => new
            {
                s.Id,
                s.Domain,
                s.SiteName,
                s.PlanTier,
                s.IsActive,
                s.CreatedAt,
                ThreadCount = s.Threads.Count,
                PendingModeration = s.ModerationQueue.Count(m => m.Status == "pending")
            })
            .ToListAsync();

        return Ok(sites);
    }

    /// <summary>Update a site's configuration.</summary>
    [HttpPut("api/widget/sites/{siteId}")]
    [Authorize]
    public async Task<ActionResult> UpdateSite(Guid siteId, [FromBody] RegisterSiteRequest request)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        await using var db = await _contextFactory.CreateDbContextAsync();

        var site = await db.CommentSites.FirstOrDefaultAsync(s => s.Id == siteId && s.OwnerUserId == userId);
        if (site == null)
            return NotFound();

        site.SiteName = request.SiteName;
        site.Domain = request.Domain;
        site.PlanTier = request.PlanTier ?? site.PlanTier;
        site.AllowedOrigins = request.AllowedOrigins ?? new[] { request.Domain };
        site.CustomCssUrl = request.CustomCssUrl;
        site.LogoUrl = request.LogoUrl;
        site.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Ok();
    }

    // ═══════════════════════════════════════════════════════════════
    //  Widget Embed Endpoints (API key auth)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Get widget configuration for embedding.</summary>
    [HttpGet("api/widget/{siteId}/config")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationOptions.DefaultScheme)]
    public async Task<ActionResult<WidgetConfigDto>> GetWidgetConfig(Guid siteId, [FromQuery] string? pageUrl)
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var site = await db.CommentSites.FindAsync(siteId);
        if (site == null || !site.IsActive)
            return NotFound("Site not found or inactive");

        if (string.IsNullOrEmpty(pageUrl))
            return BadRequest("pageUrl is required");

        var thread = await _threadService.GetOrCreateThreadAsync(siteId, pageUrl, null);

        // Record page view
        await _analyticsService.RecordPageViewAsync(siteId);

        return Ok(new WidgetConfigDto(
            SiteId: siteId.ToString(),
            ThreadId: thread.Id.ToString(),
            SortOrder: thread.SortOrder,
            CustomCssUrl: site.CustomCssUrl,
            LogoUrl: site.LogoUrl,
            IsModerated: thread.IsModerated,
            IsLocked: thread.IsLocked
        ));
    }

    /// <summary>Get comments for a thread.</summary>
    [HttpGet("api/widget/{siteId}/threads/{threadId}/comments")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationOptions.DefaultScheme)]
    public async Task<ActionResult<List<CommentDto>>> GetComments(
        Guid siteId, Guid threadId,
        [FromQuery] string sort = "hot",
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50)
    {
        var comments = await _threadService.GetCommentsAsync(threadId, sort, skip, take);
        return Ok(comments);
    }

    /// <summary>Post a comment to a thread.</summary>
    [HttpPost("api/widget/{siteId}/threads/{threadId}/comments")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationOptions.DefaultScheme)]
    public async Task<ActionResult<CommentDto>> PostComment(
        Guid siteId, Guid threadId,
        [FromBody] PostCommentRequest request)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? "anonymous";

        var argument = await _threadService.PostCommentAsync(
            threadId, userId, request.Content, request.ParentArgumentId);

        if (argument == null)
            return BadRequest("Thread is locked or not found");

        await _analyticsService.RecordCommentAsync(siteId);

        return Ok(new CommentDto(
            Id: argument.Id.ToString(),
            AuthorName: userId,
            Content: argument.WarrantText,
            Upvotes: 0,
            Downvotes: 0,
            ReplyCount: 0,
            WilsonScore: null,
            CreatedAt: argument.CreatedAt,
            ParentId: request.ParentArgumentId,
            IsDeleted: false
        ));
    }

    /// <summary>Vote on a comment.</summary>
    [HttpPost("api/widget/{siteId}/comments/{argumentId}/vote")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationOptions.DefaultScheme)]
    public async Task<ActionResult> Vote(
        Guid siteId, Guid argumentId,
        [FromQuery] string direction = "up")
    {
        // Record the vote in analytics
        await _analyticsService.RecordVoteAsync(siteId);
        return Ok();
    }

    // ═══════════════════════════════════════════════════════════════
    //  Moderation Endpoints (cookie auth — publisher dashboard)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Get moderation queue for a site.</summary>
    [HttpGet("api/widget/sites/{siteId}/moderation")]
    [Authorize]
    public async Task<ActionResult<List<ModerationQueueItemDto>>> GetModerationQueue(
        Guid siteId, [FromQuery] string? status)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        await using var db = await _contextFactory.CreateDbContextAsync();
        var site = await db.CommentSites.FirstOrDefaultAsync(s => s.Id == siteId && s.OwnerUserId == userId);
        if (site == null) return NotFound();

        var queue = await _moderationService.GetQueueAsync(siteId, status);
        return Ok(queue);
    }

    /// <summary>Review a moderation item.</summary>
    [HttpPost("api/widget/sites/{siteId}/moderation/{itemId}/review")]
    [Authorize]
    public async Task<ActionResult> ReviewModerationItem(
        Guid siteId, Guid itemId, [FromQuery] bool approved = true)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _moderationService.ReviewItemAsync(itemId, userId, approved);
        if (!result) return NotFound();
        return Ok();
    }

    // ═══════════════════════════════════════════════════════════════
    //  Analytics Endpoints (cookie auth — publisher dashboard)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Get usage stats for a site.</summary>
    [HttpGet("api/widget/sites/{siteId}/analytics")]
    [Authorize]
    public async Task<ActionResult<List<UsageStatsDto>>> GetAnalytics(
        Guid siteId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        await using var db = await _contextFactory.CreateDbContextAsync();
        var site = await db.CommentSites.FirstOrDefaultAsync(s => s.Id == siteId && s.OwnerUserId == userId);
        if (site == null) return NotFound();

        var fromDate = from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var toDate = to ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var stats = await _analyticsService.GetUsageAsync(siteId, fromDate, toDate);
        return Ok(stats);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Contradiction Endpoints (cookie auth — publisher dashboard)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Get cross-thread contradictions for a site.</summary>
    [HttpGet("api/widget/sites/{siteId}/contradictions")]
    [Authorize]
    public async Task<ActionResult<List<ContradictionDto>>> GetContradictions(Guid siteId)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        await using var db = await _contextFactory.CreateDbContextAsync();
        var site = await db.CommentSites.FirstOrDefaultAsync(s => s.Id == siteId && s.OwnerUserId == userId);
        if (site == null) return NotFound();

        var contradictions = await db.ThreadContradictions
            .Where(c => c.SiteId == siteId && !c.IsResolved)
            .OrderByDescending(c => c.Confidence)
            .Take(50)
            .ToListAsync();

        // Get thread URLs and comment snippets
        var threadIds = contradictions.SelectMany(c => new[] { c.ThreadIdA, c.ThreadIdB }).Distinct().ToList();
        var threads = await db.CommentThreads
            .Where(t => threadIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.PageUrl);

        var argIds = contradictions.SelectMany(c => new[] { c.ArgumentIdA, c.ArgumentIdB }).Distinct().ToList();
        var args = await db.SocialArguments
            .Where(a => argIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.WarrantText);

        var result = contradictions.Select(c => new ContradictionDto(
            Id: c.Id.ToString(),
            ThreadUrlA: threads.TryGetValue(c.ThreadIdA, out var urlA) ? urlA : "unknown",
            ThreadUrlB: threads.TryGetValue(c.ThreadIdB, out var urlB) ? urlB : "unknown",
            CommentSnippetA: args.TryGetValue(c.ArgumentIdA, out var snippetA)
                ? (snippetA.Length > 200 ? snippetA[..197] + "..." : snippetA)
                : "[deleted]",
            CommentSnippetB: args.TryGetValue(c.ArgumentIdB, out var snippetB)
                ? (snippetB.Length > 200 ? snippetB[..197] + "..." : snippetB)
                : "[deleted]",
            ContradictionType: c.ContradictionType,
            Confidence: c.Confidence,
            Explanation: c.Explanation,
            DetectedAt: c.DetectedAt
        )).ToList();

        return Ok(result);
    }
}