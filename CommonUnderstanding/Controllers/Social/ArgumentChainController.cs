using CommonUnderstanding.Data;
using CommonUnderstanding.Models.Social;
using CommonUnderstanding.Services.Social;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CommonUnderstanding.Controllers.Social;

/// <summary>
/// CRUD wrapper around ArgumentChainService.
/// Chains are owned by creator and require authentication for mutations.
/// </summary>
[ApiController]
[Route("api/argumentchains")]
[Produces("application/json")]
public class ArgumentChainController : ControllerBase
{
    private readonly ArgumentChainService _chainService;
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public ArgumentChainController(
        ArgumentChainService chainService,
        IDbContextFactory<ApplicationDbContext> dbFactory)
    {
        _chainService = chainService;
        _dbFactory = dbFactory;
    }

    /// <summary>GET /api/argumentchains — list public chains.</summary>
    [HttpGet]
    public async Task<IActionResult> ListChains(
        [FromQuery] string? tags,
        [FromQuery] string? userId,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var query = db.ArgumentChains.AsNoTracking().Where(c => c.IsPublic);

        if (!string.IsNullOrEmpty(userId))
            query = query.Where(c => c.UserId == userId);

        if (!string.IsNullOrEmpty(tags))
        {
            var tagList = tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            query = query.Where(c => c.Tags.Any(t => tagList.Contains(t)));
        }

        var chains = await query
            .OrderByDescending(c => c.UpdatedAt)
            .Take(Math.Min(limit, 100))
            .Select(c => new
            {
                id = c.Id,
                title = c.Title,
                description = c.Description,
                userId = c.UserId,
                tags = c.Tags,
                argumentCount = c.ArgumentIds.Length,
                updatedAt = c.UpdatedAt
            })
            .ToListAsync(ct);

        return Ok(new { items = chains });
    }

    /// <summary>GET /api/argumentchains/{id} — chain detail with full argument list.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetChain(Guid id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var chain = await _chainService.GetChainAsync(id, userId, ct);

        if (chain is null) return NotFound();

        // Load full argument details
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var arguments = await db.SocialArguments
            .AsNoTracking()
            .Include(a => a.ClaimProposition)
            .Where(a => chain.ArgumentIds.Contains(a.Id))
            .ToListAsync(ct);

        var argMap = arguments.ToDictionary(a => a.Id);

        return Ok(new
        {
            id = chain.Id,
            title = chain.Title,
            description = chain.Description,
            userId = chain.UserId,
            isPublic = chain.IsPublic,
            tags = chain.Tags,
            rootArgumentId = chain.RootArgumentId,
            argumentCount = chain.ArgumentIds.Length,
            arguments = chain.ArgumentIds.Select(id =>
                argMap.TryGetValue(id, out var a)
                    ? new
                    {
                        id = a.Id,
                        title = a.Title,
                        claimText = a.ClaimProposition?.Text,
                        wilsonScore = a.WilsonScore,
                        orderIndex = Array.IndexOf(chain.ArgumentIds, id)
                    }
                    : null).Where(x => x is not null),
            updatedAt = chain.UpdatedAt
        });
    }

    /// <summary>POST /api/argumentchains — create new chain.</summary>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateChain([FromBody] CreateChainRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        if (request.ArgumentIds is not { Length: > 0 })
            return BadRequest(new { detail = "At least one argument is required." });

        try
        {
            var chain = await _chainService.CreateChainAsync(
                userId,
                request.Title,
                request.Description,
                request.RootArgumentId,
                request.ArgumentIds,
                request.Tags ?? Array.Empty<string>(),
                request.IsPublic,
                ct);

            return CreatedAtAction(nameof(GetChain), new { id = chain.Id }, new
            {
                id = chain.Id,
                title = chain.Title,
                userId = chain.UserId
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    /// <summary>PUT /api/argumentchains/{id} — update chain metadata.</summary>
    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> UpdateChain(Guid id, [FromBody] UpdateChainRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var chain = await db.ArgumentChains.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (chain is null) return NotFound();
        if (chain.UserId != userId && !User.IsInRole("Admin")) return Forbid();

        if (request.Title is not null) chain.Title = request.Title;
        if (request.Description is not null) chain.Description = request.Description;
        if (request.Tags is not null) chain.Tags = request.Tags;
        if (request.IsPublic.HasValue) chain.IsPublic = request.IsPublic.Value;

        chain.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>DELETE /api/argumentchains/{id} — delete chain.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteChain(Guid id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var chain = await db.ArgumentChains.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (chain is null) return NotFound();
        if (chain.UserId != userId && !User.IsInRole("Admin")) return Forbid();

        db.ArgumentChains.Remove(chain);
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>POST /api/argumentchains/{id}/arguments — add argument to chain.</summary>
    [HttpPost("{id:guid}/arguments")]
    [Authorize]
    public async Task<IActionResult> AddArgument(Guid id, [FromBody] AddArgumentRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var added = await _chainService.AddArgumentToChainAsync(id, request.ArgumentId, userId, ct);
        return added ? Ok() : NotFound();
    }

    /// <summary>DELETE /api/argumentchains/{id}/arguments/{argumentId} — remove argument from chain.</summary>
    [HttpDelete("{id:guid}/arguments/{argumentId:guid}")]
    [Authorize]
    public async Task<IActionResult> RemoveArgument(Guid id, Guid argumentId, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var removed = await _chainService.RemoveArgumentFromChainAsync(id, argumentId, userId, ct);
        return removed ? NoContent() : NotFound();
    }

    /// <summary>GET /api/argumentchains/{id}/graph — traverse argument graph.</summary>
    [HttpGet("{id:guid}/graph")]
    public async Task<IActionResult> GetChainGraph(Guid id, [FromQuery] int depth = 2, CancellationToken ct = default)
    {
        depth = Math.Clamp(depth, 1, 5);
        var graph = await _chainService.GetArgumentGraphAsync(id, depth, ct);
        return Ok(graph);
    }
}

// ── Request DTOs ──────────────────────────────────────────────────────────────

public record CreateChainRequest(
    string Title,
    string? Description,
    Guid RootArgumentId,
    Guid[] ArgumentIds,
    string[]? Tags,
    bool IsPublic = false);

public record UpdateChainRequest(
    string? Title,
    string? Description,
    string[]? Tags,
    bool? IsPublic);

public record AddArgumentRequest(Guid ArgumentId);
