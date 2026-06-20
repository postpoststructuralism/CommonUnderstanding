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
/// CRUD and graph traversal for ArgumentLinks.
/// Cycle detection is enforced server-side on every POST.
/// </summary>
[ApiController]
[Produces("application/json")]
public class ArgumentLinkController : ControllerBase
{
    private readonly ArgumentChainService _chainService;
    private readonly ArgumentLinkSuggestionPlugin _linkSuggestionPlugin;
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public ArgumentLinkController(
        ArgumentChainService chainService,
        ArgumentLinkSuggestionPlugin linkSuggestionPlugin,
        IDbContextFactory<ApplicationDbContext> dbFactory)
    {
        _chainService = chainService;
        _linkSuggestionPlugin = linkSuggestionPlugin;
        _dbFactory = dbFactory;
    }

    /// <summary>GET /api/argumentlinks — query links by source, target, or type.</summary>
    [HttpGet("api/argumentlinks")]
    public async Task<IActionResult> GetLinks(
        [FromQuery] Guid? sourceId,
        [FromQuery] Guid? targetId,
        [FromQuery] string? linkType,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var query = db.ArgumentLinks.AsNoTracking().AsQueryable();

        if (sourceId.HasValue) query = query.Where(l => l.SourceArgumentId == sourceId.Value);
        if (targetId.HasValue) query = query.Where(l => l.TargetArgumentId == targetId.Value);
        if (!string.IsNullOrEmpty(linkType) && Enum.TryParse<LinkType>(linkType, true, out var lt))
            query = query.Where(l => l.LinkType == lt);

        var links = await query
            .OrderByDescending(l => l.CreatedAt)
            .Take(Math.Min(limit, 100))
            .Select(l => new
            {
                id = l.Id,
                sourceArgumentId = l.SourceArgumentId,
                targetArgumentId = l.TargetArgumentId,
                linkType = l.LinkType.ToString(),
                annotation = l.Annotation,
                userId = l.UserId,
                createdAt = l.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(new { items = links });
    }

    /// <summary>POST /api/argumentlinks — create a new link with cycle detection.</summary>
    [HttpPost("api/argumentlinks")]
    [Authorize]
    public async Task<IActionResult> CreateLink([FromBody] CreateLinkRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        if (!Enum.TryParse<LinkType>(request.LinkType, ignoreCase: true, out var linkType))
            return BadRequest(new { title = "Invalid link type.", detail = $"'{request.LinkType}' is not valid." });

        var (link, error) = await _chainService.CreateLinkAsync(
            request.SourceArgumentId,
            request.TargetArgumentId,
            linkType,
            request.Annotation,
            userId,
            ct);

        if (link is null)
        {
            if (error?.Contains("cycle") == true)
                return Conflict(new { title = "Cycle detected.", detail = error });

            return UnprocessableEntity(new { title = "Cannot create link.", detail = error });
        }

        return CreatedAtAction(nameof(GetLinks), new { sourceId = link.SourceArgumentId }, new
        {
            id = link.Id,
            sourceArgumentId = link.SourceArgumentId,
            targetArgumentId = link.TargetArgumentId,
            linkType = link.LinkType.ToString(),
            annotation = link.Annotation,
            createdAt = link.CreatedAt
        });
    }

    /// <summary>DELETE /api/argumentlinks/{id} — delete own link.</summary>
    [HttpDelete("api/argumentlinks/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteLink(Guid id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var link = await db.ArgumentLinks.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (link is null) return NotFound();
        if (link.UserId != userId && !User.IsInRole("Admin")) return Forbid();

        db.ArgumentLinks.Remove(link);
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>GET /api/arguments/{id}/graph — argument graph up to N hops.</summary>
    [HttpGet("api/arguments/{id:guid}/graph")]
    public async Task<IActionResult> GetArgumentGraph(Guid id, [FromQuery] int depth = 2, CancellationToken ct = default)
    {
        depth = Math.Clamp(depth, 1, 5); // max 5 hops per spec
        var graph = await _chainService.GetArgumentGraphAsync(id, depth, ct);
        return Ok(graph);
    }

    /// <summary>POST /api/argumentlinks/suggest — AI link suggestions for a given argument.</summary>
    [HttpPost("api/argumentlinks/suggest")]
    [Authorize]
    public async Task<IActionResult> SuggestLinks([FromBody] SuggestLinksRequest request, CancellationToken ct)
    {
        var suggestions = await _linkSuggestionPlugin.SuggestLinksAsync(
            request.SourceArgumentId, request.MaxSuggestions, ct);

        return Ok(new { suggestions });
    }
}

public record CreateLinkRequest(
    Guid SourceArgumentId,
    Guid TargetArgumentId,
    string LinkType,
    string? Annotation);

public record SuggestLinksRequest(Guid SourceArgumentId, int MaxSuggestions = 5);
