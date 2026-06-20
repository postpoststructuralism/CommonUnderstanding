using CommonUnderstanding.Data;
using CommonUnderstanding.Models.Social;
using CommonUnderstanding.Services.Social;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CommonUnderstanding.Controllers.Social;

/// <summary>
/// CRUD for SocialProposition (atomic claims: Claim/Premise/Rebuttal).
/// Used as building blocks for arguments.
/// </summary>
[ApiController]
[Route("api/propositions")]
[Produces("application/json")]
public class SocialPropositionController : ControllerBase
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly EmbeddingService _embeddingService;

    public SocialPropositionController(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        EmbeddingService embeddingService)
    {
        _dbFactory = dbFactory;
        _embeddingService = embeddingService;
    }

    /// <summary>GET /api/propositions — list public propositions.</summary>
    [HttpGet]
    public async Task<IActionResult> ListPropositions(
        [FromQuery] string? type,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var query = db.SocialPropositions
            .AsNoTracking()
            .Where(p => p.IsConfirmed);

        if (!string.IsNullOrEmpty(type) && Enum.TryParse<SocialPropositionType>(type, out var propType))
            query = query.Where(p => p.Type == propType);

        var propositions = await query
            .OrderByDescending(p => p.CreatedAt)
            .Take(Math.Min(limit, 100))
            .Select(p => new
            {
                id = p.Id,
                text = p.Text,
                type = p.Type.ToString(),
                userId = p.UserId,
                isAIGenerated = p.IsAIGenerated,
                createdAt = p.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(new { items = propositions });
    }

    /// <summary>GET /api/propositions/{id} — proposition detail.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetProposition(Guid id, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var prop = await db.SocialPropositions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (prop is null) return NotFound();

        return Ok(new
        {
            id = prop.Id,
            text = prop.Text,
            type = prop.Type.ToString(),
            userId = prop.UserId,
            isAIGenerated = prop.IsAIGenerated,
            isConfirmed = prop.IsConfirmed,
            createdAt = prop.CreatedAt,
            embedding = prop.Embedding is not null ? "present" : null
        });
    }

    /// <summary>POST /api/propositions — create new proposition.</summary>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateProposition([FromBody] CreatePropositionRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var proposition = new SocialProposition
        {
            Text = request.Text,
            Type = request.Type,
            UserId = userId,
            IsAIGenerated = false,
            IsConfirmed = true
        };

        // Async embedding generation (best-effort)
        _ = _embeddingService.GenerateEmbeddingAsync(request.Text).ContinueWith(async t =>
        {
            if (t.IsCompletedSuccessfully && t.Result is not null)
            {
                await using var db2 = await _dbFactory.CreateDbContextAsync(ct);
                var prop = await db2.SocialPropositions.FindAsync(new object[] { proposition.Id }, cancellationToken: ct);
                if (prop is not null)
                {
                    prop.Embedding = t.Result;
                    await db2.SaveChangesAsync(ct);
                }
            }
        });

        db.SocialPropositions.Add(proposition);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetProposition), new { id = proposition.Id }, new
        {
            id = proposition.Id,
            text = proposition.Text,
            type = proposition.Type.ToString(),
            userId = proposition.UserId
        });
    }

    /// <summary>PUT /api/propositions/{id} — update own proposition before confirmation.</summary>
    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> UpdateProposition(Guid id, [FromBody] UpdatePropositionRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var prop = await db.SocialPropositions.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (prop is null) return NotFound();
        if (prop.UserId != userId && !User.IsInRole("Admin")) return Forbid();

        if (request.Text is not null) prop.Text = request.Text;
        if (request.Type.HasValue) prop.Type = request.Type.Value;

        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>DELETE /api/propositions/{id} — delete own proposition.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteProposition(Guid id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var prop = await db.SocialPropositions.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (prop is null) return NotFound();
        if (prop.UserId != userId && !User.IsInRole("Admin")) return Forbid();

        // Check if referenced by any arguments (prevent deletion if used)
        var argCount = await db.SocialArguments
            .CountAsync(a => a.ClaimPropositionId == id, ct);

        if (argCount > 0)
            return Conflict(new { detail = "Proposition is referenced by arguments and cannot be deleted." });

        db.SocialPropositions.Remove(prop);
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>POST /api/propositions/{id}/confirm — confirm AI-generated proposition.</summary>
    [HttpPost("{id:guid}/confirm")]
    [Authorize]
    public async Task<IActionResult> ConfirmProposition(Guid id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var prop = await db.SocialPropositions.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (prop is null) return NotFound();
        if (prop.UserId != userId) return Forbid();

        prop.IsConfirmed = true;
        await db.SaveChangesAsync(ct);

        return Ok(new { id = prop.Id, isConfirmed = true });
    }
}

// ── Request DTOs ──────────────────────────────────────────────────────────────

public record CreatePropositionRequest(
    string Text,
    SocialPropositionType Type);

public record UpdatePropositionRequest(
    string? Text,
    SocialPropositionType? Type);
