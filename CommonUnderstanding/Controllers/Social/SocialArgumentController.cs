using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommonUnderstanding.Data;
using CommonUnderstanding.Models.Social;
using CommonUnderstanding.Services.Social;
using CommonUnderstanding.Services.Social.Plugins;
using CommonUnderstanding.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;

namespace CommonUnderstanding.Controllers.Social;

[ApiController]
[Route("api/arguments")]
[Produces("application/json")]
public class SocialArgumentController : ControllerBase
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly FollowUpArgumentService _followUpService;
    private readonly ArgumentLinkSuggestionPlugin _linkSuggestionPlugin;
    private readonly XPAwardService _xpAwards;
    private readonly ILogger<SocialArgumentController> _logger;

    public SocialArgumentController(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        FollowUpArgumentService followUpService,
        ArgumentLinkSuggestionPlugin linkSuggestionPlugin,
        XPAwardService xpAwards,
        ILogger<SocialArgumentController> logger)
    {
        _dbFactory = dbFactory;
        _followUpService = followUpService;
        _linkSuggestionPlugin = linkSuggestionPlugin;
        _xpAwards = xpAwards;
        _logger = logger;
    }

    /// <summary>
    /// Create a follow-up argument (reply) to an existing argument
    /// </summary>
    [HttpPost("{id:guid}/follow-ups")]
    [Authorize]
    public async Task<IActionResult> CreateFollowUpArgument(
        Guid id,
        [FromBody] CreateFollowUpRequest request,
        CancellationToken ct = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var parentArg = await db.SocialArguments
            .FirstOrDefaultAsync(a => a.Id == id, ct);
        
        if (parentArg == null || !parentArg.IsPublic)
            return NotFound(new { title = "Parent argument not found or not public" });

        // Prevent self-replies
        if (parentArg.UserId == userId)
            return BadRequest(new { error = "Cannot reply to your own argument" });

        var followUp = new SocialArgument
        {
            Title = request.Title,
            WarrantText = request.WarrantText,
            ResolutionText = request.ResolutionText,
            UserId = userId,
            IsPublic = true,
            Tags = request.Tags?.Length > 0 ? request.Tags : parentArg.Tags,
            SchwartzValues = parentArg.SchwartzValues
        };

        // Use the service for validation, rate limiting, depth checking, and creation
        try
        {
            await _followUpService.CreateFollowUpArgumentAsync(id, followUp, userId, ct);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(429, new { error = ex.Message });
        }

        await _xpAwards.AwardAsync(userId, 5, "Posted follow-up argument", followUp.Id, ct);
        
        var hubContext = HttpContext.RequestServices.GetRequiredService<IHubContext<VotingHub>>();
        await hubContext.Clients.Group(VotingHub.GroupKey(id))
            .SendAsync("ReplyAdded", new { parentId = id, replyId = followUp.Id, replyTitle = followUp.Title }, ct);
            
        await hubContext.Clients.Group(VotingHub.GroupKey(id))
            .SendAsync("ReplyCountUpdated", new { argumentId = id, newCount = parentArg.ReplyCount + 1 }, ct);

        return Ok(new
        {
            id = followUp.Id,
            title = followUp.Title,
            parentId = id,
            replyCount = parentArg.ReplyCount + 1
        });
    }

    /// <summary>
    /// Get paginated follow-up arguments for a parent argument
    /// </summary>
    [HttpGet("{id:guid}/follow-ups")]
    [AllowAnonymous]
    public async Task<IActionResult> GetFollowUpArguments(
        Guid id,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var parentExists = await db.SocialArguments.AnyAsync(a => a.Id == id, ct);
        if (!parentExists)
            return NotFound(new { error = "Parent argument not found" });

        var (arguments, totalCount) = await _followUpService.GetFollowUpArgumentsAsync(
            id, skip, take, ct);

        var parentReplyCount = await db.SocialArguments
            .Where(a => a.Id == id)
            .Select(a => a.ReplyCount)
            .FirstOrDefaultAsync(ct);

        return Ok(new
        {
            arguments = arguments.Select(a => new
            {
                id = a.Id,
                title = a.Title,
                warrantText = a.WarrantText,
                resolutionText = a.ResolutionText,
                claimProposition = a.ClaimProposition == null ? null : new
                {
                    id = a.ClaimProposition.Id,
                    text = a.ClaimProposition.Text
                },
                userId = a.UserId,
                upvoteCount = a.UpvoteCount,
                downvoteCount = a.DownvoteCount,
                wilsonScore = a.WilsonScore,
                replyCount = a.ReplyCount,
                tags = a.Tags,
                createdAt = a.CreatedAt
            }),
            totalCount,
            parentReplyCount,
            skip,
            take
        });
    }

    public record CreateFollowUpRequest(
        [property: Required, MaxLength(300)] string Title,
        [property: Required] string WarrantText,
        string? ResolutionText = null,
        string? ClaimText = null,
        string[]? Tags = null,
        string? Annotation = null
    );
}