using CommonUnderstanding.Data;
using CommonUnderstanding.Models.Social;
using CommonUnderstanding.Services.Social;
using CommonUnderstanding.Services.Social.Plugins;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CommonUnderstanding.Controllers.Social;

/// <summary>
/// CRUD for SocialArgument (social posts).
/// All creates are published immediately but subject to AI validation (shadow-ban if low validity).
/// Requires authentication for mutations.
/// </summary>
[ApiController]
[Route("api/arguments")]
[Produces("application/json")]
public class SocialArgumentController : ControllerBase
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly ArgumentLinkSuggestionPlugin _linkSuggestionPlugin;
    private readonly XPAwardService _xpAwards;
    private readonly ILogger<SocialArgumentController> _logger;

    public SocialArgumentController(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        ArgumentLinkSuggestionPlugin linkSuggestionPlugin,
        XPAwardService xpAwards,
        ILogger<SocialArgumentController> logger)
    {
        _dbFactory = dbFactory;
        _linkSuggestionPlugin = linkSuggestionPlugin;
        _xpAwards = xpAwards;
        _logger = logger;
    }

    /// <summary>GET /api/arguments — list public arguments with filtering and sorting.</summary>
    [HttpGet]
    public async Task<IActionResult> ListArguments(
        [FromQuery] string? sort,
        [FromQuery] string[]? tags,
        [FromQuery] string? userId,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var query = db.SocialArguments
            .AsNoTracking()
            .Include(a => a.ClaimProposition)
            .Where(a => a.IsPublic && !a.IsShadowBanned);

        if (!string.IsNullOrEmpty(userId))
            query = query.Where(a => a.UserId == userId);

        if (tags is { Length: > 0 })
            query = query.Where(a => a.Tags.Any(t => tags.Contains(t)));

        // Sort options: hot (default), wilson, recent, controversial
        query = sort switch
        {
            "wilson" => query.OrderByDescending(a => a.WilsonScore),
            "recent" => query.OrderByDescending(a => a.CreatedAt),
            "controversial" => query.OrderByDescending(a => a.ControversyScore),
            _ => query.OrderByDescending(a => a.HotScore) // hot (default)
        };

        var arguments = await query
            .Take(Math.Min(limit, 100))
            .Select(a => new
            {
                id = a.Id,
                title = a.Title,
                claimText = a.ClaimProposition!.Text,
                warrantText = a.WarrantText,
                userId = a.UserId,
                tags = a.Tags,
                schwartzValues = a.SchwartzValues,
                upvotes = a.UpvoteCount,
                downvotes = a.DownvoteCount,
                wilsonScore = a.WilsonScore,
                hotScore = a.HotScore,
                isAIValidated = a.IsAIValidated,
                aiValidityScore = a.AIValidityScore,
                createdAt = a.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(new { items = arguments, sort = sort ?? "hot" });
    }

    /// <summary>GET /api/arguments/{id} — full argument detail with votes.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetArgument(Guid id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var arg = await db.SocialArguments
            .AsNoTracking()
            .Include(a => a.ClaimProposition)
            .Include(a => a.Votes)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (arg is null || (!arg.IsPublic && arg.UserId != userId))
            return NotFound();

        var userVote = arg.Votes.FirstOrDefault(v => v.UserId == userId);

        return Ok(new
        {
            id = arg.Id,
            title = arg.Title,
            claimText = arg.ClaimProposition?.Text,
            warrantText = arg.WarrantText,
            userId = arg.UserId,
            tags = arg.Tags,
            schwartzValues = arg.SchwartzValues,
            upvotes = arg.UpvoteCount,
            downvotes = arg.DownvoteCount,
            wilsonScore = arg.WilsonScore,
            hotScore = arg.HotScore,
            isAIValidated = arg.IsAIValidated,
            aiValidityScore = arg.AIValidityScore,
            aiFallacyFlags = arg.AIFallacyFlags,
            myVote = userVote is not null ? new
            {
                vote = userVote.Vote.ToString(),
                rationale = userVote.Rationale.ToString(),
                comment = userVote.Comment,
                epistemicWeight = userVote.EpistemicWeight
            } : null,
            createdAt = arg.CreatedAt,
            updatedAt = arg.UpdatedAt
        });
    }

    /// <summary>POST /api/arguments — create new argument.</summary>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateArgument([FromBody] CreateArgumentRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // Ensure claim proposition exists
        SocialProposition? claimProp = null;
        if (request.ClaimPropositionId.HasValue)
        {
            claimProp = await db.SocialPropositions
                .FirstOrDefaultAsync(p => p.Id == request.ClaimPropositionId.Value, ct);

            if (claimProp is null)
                return NotFound(new { title = "Claim proposition not found." });
        }
        else if (!string.IsNullOrEmpty(request.ClaimText))
        {
            // Auto-create claim proposition
            claimProp = new SocialProposition
            {
                Text = request.ClaimText,
                Type = SocialPropositionType.Claim,
                UserId = userId,
                IsAIGenerated = false,
                IsConfirmed = true
            };
            db.SocialPropositions.Add(claimProp);
            await db.SaveChangesAsync(ct);
        }

        var argument = new SocialArgument
        {
            Title = request.Title,
            ClaimPropositionId = claimProp?.Id ?? Guid.Empty,
            WarrantText = request.WarrantText,
            UserId = userId,
            IsPublic = request.IsPublic,
            Tags = request.Tags ?? Array.Empty<string>(),
            SchwartzValues = request.SchwartzValues ?? Array.Empty<string>()
        };

        db.SocialArguments.Add(argument);
        await db.SaveChangesAsync(ct);

        // Award XP for first argument
        var argCount = await db.SocialArguments.CountAsync(a => a.UserId == userId && a.IsPublic, ct);
        if (argCount == 1)
            await _xpAwards.AwardAsync(userId, 10, "First argument published", argument.Id, ct);

        _logger.LogInformation("Created argument {ArgumentId} by user {UserId}", argument.Id, userId);

        return CreatedAtAction(nameof(GetArgument), new { id = argument.Id }, new
        {
            id = argument.Id,
            title = argument.Title,
            userId = argument.UserId,
            isPublic = argument.IsPublic
        });
    }

    /// <summary>PUT /api/arguments/{id} — update own argument (before publication).</summary>
    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> UpdateArgument(Guid id, [FromBody] UpdateArgumentRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var arg = await db.SocialArguments.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (arg is null) return NotFound();
        if (arg.UserId != userId && !User.IsInRole("Admin")) return Forbid();

        // Prevent edits if already has votes (published and engaged)
        var voteCount = await db.ArgumentVotes.CountAsync(v => v.ArgumentId == id, ct);
        if (voteCount > 0)
            return Conflict(new { detail = "Cannot edit argument after receiving votes." });

        if (request.Title is not null) arg.Title = request.Title;
        if (request.WarrantText is not null) arg.WarrantText = request.WarrantText;
        if (request.Tags is not null) arg.Tags = request.Tags;
        if (request.SchwartzValues is not null) arg.SchwartzValues = request.SchwartzValues;
        if (request.IsPublic.HasValue) arg.IsPublic = request.IsPublic.Value;

        arg.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>DELETE /api/arguments/{id} — delete own unpublished argument.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteArgument(Guid id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var arg = await db.SocialArguments.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (arg is null) return NotFound();
        if (arg.UserId != userId && !User.IsInRole("Admin")) return Forbid();

        var voteCount = await db.ArgumentVotes.CountAsync(v => v.ArgumentId == id, ct);
        if (voteCount > 0)
            return Conflict(new { detail = "Cannot delete argument after receiving votes." });

        // Cascade: remove any links
        var links = await db.ArgumentLinks
            .Where(l => l.SourceArgumentId == id || l.TargetArgumentId == id)
            .ToListAsync(ct);
        db.ArgumentLinks.RemoveRange(links);

        db.SocialArguments.Remove(arg);
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>GET /api/arguments/{id}/relatedarguments — AI-suggested related arguments.</summary>
    [HttpGet("{id:guid}/related")]
    public async Task<IActionResult> GetRelatedArguments(Guid id, [FromQuery] int limit = 5, CancellationToken ct = default)
    {
        var suggestions = await _linkSuggestionPlugin.SuggestLinksAsync(id, limit, ct);

        return Ok(new
        {
            suggestions = suggestions.Select(s => new
            {
                targetArgumentId = s.TargetArgumentId,
                targetTitle = s.TargetTitle,
                suggestedLinkType = s.SuggestedLinkType.ToString(),
                explanation = s.Explanation,
                similarityScore = s.SimilarityScore
            })
        });
    }
}

// ── Request DTOs ──────────────────────────────────────────────────────────────

public record CreateArgumentRequest(
    string Title,
    string? WarrantText,
    Guid? ClaimPropositionId,
    string? ClaimText,
    string[]? Tags,
    string[]? SchwartzValues,
    bool IsPublic = false);

public record UpdateArgumentRequest(
    string? Title,
    string? WarrantText,
    string[]? Tags,
    string[]? SchwartzValues,
    bool? IsPublic);
