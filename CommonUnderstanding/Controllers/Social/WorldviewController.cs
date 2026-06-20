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
/// Worldview CRUD, chain management, convergence scoring, and bridge argument generation.
/// </summary>
[ApiController]
[Route("api/worldviews")]
[Produces("application/json")]
public class WorldviewController : ControllerBase
{
    private readonly WorldviewService _worldviewService;
    private readonly BridgeArgumentPlugin _bridgePlugin;
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public WorldviewController(
        WorldviewService worldviewService,
        BridgeArgumentPlugin bridgePlugin,
        IDbContextFactory<ApplicationDbContext> dbFactory)
    {
        _worldviewService = worldviewService;
        _bridgePlugin = bridgePlugin;
        _dbFactory = dbFactory;
    }

    /// <summary>GET /api/worldviews — list public worldviews with cursor pagination.</summary>
    [HttpGet]
    public async Task<IActionResult> ListWorldviews(
        [FromQuery] string? tags,
        [FromQuery] string? userId,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var query = db.Worldviews.AsNoTracking()
            .Where(w => w.IsPublic);

        if (!string.IsNullOrEmpty(userId))
            query = query.Where(w => w.UserId == userId);

        if (!string.IsNullOrEmpty(tags))
        {
            var tagList = tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            query = query.Where(w => w.Tags.Any(t => tagList.Contains(t)));
        }

        var worldviews = await query
            .OrderByDescending(w => w.UpdatedAt)
            .Take(Math.Min(limit, 100))
            .Select(w => new
            {
                id = w.Id,
                title = w.Title,
                description = w.Description,
                userId = w.UserId,
                tags = w.Tags,
                schwartzValues = w.SchwartzValues,
                chainCount = w.WorldviewChains.Count,
                updatedAt = w.UpdatedAt
            })
            .ToListAsync(ct);

        return Ok(new { items = worldviews });
    }

    /// <summary>GET /api/worldviews/{id} — worldview detail.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetWorldview(Guid id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var worldview = await _worldviewService.GetWorldviewAsync(id, userId, ct);

        if (worldview is null) return NotFound();

        return Ok(new
        {
            id = worldview.Id,
            title = worldview.Title,
            description = worldview.Description,
            userId = worldview.UserId,
            isPublic = worldview.IsPublic,
            tags = worldview.Tags,
            schwartzValues = worldview.SchwartzValues,
            schwartzVector = worldview.SchwartzVector,
            chains = worldview.WorldviewChains
                .OrderBy(wc => wc.OrderIndex)
                .Select(wc => new
                {
                    chainId = wc.ArgumentChainId,
                    orderIndex = wc.OrderIndex,
                    title = wc.ArgumentChain?.Title,
                    argumentCount = wc.ArgumentChain?.ArgumentIds.Length ?? 0
                }),
            updatedAt = worldview.UpdatedAt
        });
    }

    /// <summary>POST /api/worldviews — create worldview.</summary>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateWorldview([FromBody] CreateWorldviewRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var worldview = await _worldviewService.CreateWorldviewAsync(
            userId, request.Title, request.Description,
            request.Tags ?? Array.Empty<string>(),
            request.IsPublic, ct);

        return CreatedAtAction(nameof(GetWorldview), new { id = worldview.Id }, new
        {
            id = worldview.Id,
            title = worldview.Title,
            userId = worldview.UserId
        });
    }

    /// <summary>PUT /api/worldviews/{id} — update worldview metadata.</summary>
    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> UpdateWorldview(Guid id, [FromBody] UpdateWorldviewRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var worldview = await db.Worldviews.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (worldview is null) return NotFound();
        if (worldview.UserId != userId && !User.IsInRole("Admin")) return Forbid();

        if (request.Title is not null) worldview.Title = request.Title;
        if (request.Description is not null) worldview.Description = request.Description;
        if (request.Tags is not null) worldview.Tags = request.Tags;
        if (request.IsPublic.HasValue) worldview.IsPublic = request.IsPublic.Value;
        worldview.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>DELETE /api/worldviews/{id} — delete worldview.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteWorldview(Guid id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var worldview = await db.Worldviews.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (worldview is null) return NotFound();
        if (worldview.UserId != userId && !User.IsInRole("Admin")) return Forbid();

        db.Worldviews.Remove(worldview);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ── Chain management ──────────────────────────────────────────────────────

    /// <summary>POST /api/worldviews/{id}/chains — add chain to worldview.</summary>
    [HttpPost("{id:guid}/chains")]
    [Authorize]
    public async Task<IActionResult> AddChain(Guid id, [FromBody] AddChainRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var added = await _worldviewService.AddChainAsync(id, request.ChainId, userId, ct);
        return added ? Ok() : NotFound();
    }

    /// <summary>DELETE /api/worldviews/{id}/chains/{chainId} — remove chain.</summary>
    [HttpDelete("{id:guid}/chains/{chainId:guid}")]
    [Authorize]
    public async Task<IActionResult> RemoveChain(Guid id, Guid chainId, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var removed = await _worldviewService.RemoveChainAsync(id, chainId, userId, ct);
        return removed ? NoContent() : NotFound();
    }

    /// <summary>PUT /api/worldviews/{id}/chains/order — reorder chains.</summary>
    [HttpPut("{id:guid}/chains/order")]
    [Authorize]
    public async Task<IActionResult> ReorderChains(Guid id, [FromBody] ReorderChainsRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var reordered = await _worldviewService.ReorderChainsAsync(id, request.OrderedChainIds, userId, ct);
        return reordered ? NoContent() : NotFound();
    }

    // ── Convergence ───────────────────────────────────────────────────────────

    /// <summary>GET /api/worldviews/{id}/convergence/{otherId} — compute convergence score.</summary>
    [HttpGet("{id:guid}/convergence/{otherId:guid}")]
    public async Task<IActionResult> GetConvergence(
        Guid id,
        Guid otherId,
        [FromQuery] bool includeNarrative = false,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _worldviewService.ComputeConvergenceAsync(id, otherId, includeNarrative, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { title = "Worldview not found.", detail = ex.Message });
        }
    }

    /// <summary>GET /api/worldviews/{id}/bridges/{otherId} — AI bridge arguments.</summary>
    [HttpGet("{id:guid}/bridges/{otherId:guid}")]
    [Authorize]
    public async Task<IActionResult> GetBridgeArguments(
        Guid id,
        Guid otherId,
        [FromQuery] int count = 3,
        CancellationToken ct = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var bridges = await _bridgePlugin.GenerateBridgeArgumentsAsync(
            id, otherId, count, userId, ct);

        return Ok(new { bridges });
    }

    /// <summary>POST /api/worldviews/{id}/votes — vote on a worldview.</summary>
    [HttpPost("{id:guid}/votes")]
    [Authorize]
    public async Task<IActionResult> VoteWorldview(Guid id, [FromBody] WorldviewVoteRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        if (!Enum.TryParse<VoteValue>(request.Vote, ignoreCase: true, out var voteValue))
            return BadRequest(new { title = "Invalid vote value." });

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var worldview = await db.Worldviews.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (worldview is null) return NotFound();

        var existing = await db.WorldviewVotes
            .FirstOrDefaultAsync(v => v.WorldviewId == id && v.UserId == userId, ct);

        if (existing is null)
            db.WorldviewVotes.Add(new WorldviewVote { WorldviewId = id, UserId = userId, Vote = voteValue });
        else
            existing.Vote = voteValue;

        await db.SaveChangesAsync(ct);
        return Ok();
    }
}

// ── Request DTOs ──────────────────────────────────────────────────────────────

public record CreateWorldviewRequest(
    string Title,
    string? Description,
    string[]? Tags,
    bool IsPublic);

public record UpdateWorldviewRequest(
    string? Title,
    string? Description,
    string[]? Tags,
    bool? IsPublic);

public record AddChainRequest(Guid ChainId);
public record ReorderChainsRequest(Guid[] OrderedChainIds);
public record WorldviewVoteRequest(string Vote);
