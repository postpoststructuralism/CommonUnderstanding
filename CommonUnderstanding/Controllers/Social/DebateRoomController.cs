using CommonUnderstanding.Data;
using CommonUnderstanding.Models.Social;
using CommonUnderstanding.Services.Social;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CommonUnderstanding.Controllers.Social;

/// <summary>
/// DebateRoom CRUD and management.
/// Contributions must reference existing SocialArguments.
/// AI referee runs asynchronously via Phase2DebateHub.
/// </summary>
[ApiController]
[Route("api/debaterooms")]
[Produces("application/json")]
public class DebateRoomController : ControllerBase
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly XPAwardService _xpAwards;

    public DebateRoomController(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        XPAwardService xpAwards)
    {
        _dbFactory = dbFactory;
        _xpAwards = xpAwards;
    }

    /// <summary>GET /api/debaterooms — list rooms by status, topic.</summary>
    [HttpGet]
    public async Task<IActionResult> ListRooms(
        [FromQuery] string? status,
        [FromQuery] string? topic,
        [FromQuery] int limit = 20,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var query = db.DebateRooms.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<DebateStatus>(status, true, out var ds))
            query = query.Where(r => r.Status == ds);

        if (!string.IsNullOrEmpty(topic))
            query = query.Where(r => r.Topic.Contains(topic));

        var rooms = await query
            .OrderByDescending(r => r.CreatedAt)
            .Take(Math.Min(limit, 100))
            .Select(r => new
            {
                id = r.Id,
                title = r.Title,
                topic = r.Topic,
                motionText = r.MotionText,
                status = r.Status.ToString(),
                format = r.Format.ToString(),
                proponentUserId = r.ProponentUserId,
                opponentUserId = r.OpponentUserId,
                contributionCount = r.Contributions.Count,
                createdAt = r.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(new { items = rooms });
    }

    /// <summary>GET /api/debaterooms/{id} — room detail with contributions.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetRoom(Guid id, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var room = await db.DebateRooms
            .AsNoTracking()
            .Include(r => r.Contributions)
                .ThenInclude(c => c.Argument)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (room is null) return NotFound();

        return Ok(new
        {
            id = room.Id,
            title = room.Title,
            topic = room.Topic,
            motionText = room.MotionText,
            status = room.Status.ToString(),
            format = room.Format.ToString(),
            proponentUserId = room.ProponentUserId,
            opponentUserId = room.OpponentUserId,
            judgeUserIds = room.JudgeUserIds,
            timeLimitSeconds = room.TimeLimitSeconds,
            maxContributionsPerSide = room.MaxContributionsPerSide,
            proponentScore = room.ProponentScore,
            opponentScore = room.OpponentScore,
            aiRefereeEnabled = room.AIRefereeEnabled,
            concludedAt = room.ConcludedAt,
            contributions = room.Contributions
                .OrderBy(c => c.OrderIndex)
                .Select(c => new
                {
                    id = c.Id,
                    userId = c.UserId,
                    argumentId = c.ArgumentId,
                    argumentTitle = c.Argument.Title,
                    role = c.Role.ToString(),
                    orderIndex = c.OrderIndex,
                    validityScore = c.ValidityScore,
                    fallacyFlags = c.FallacyFlags,
                    aiRefereeComment = c.AIRefereeComment,
                    createdAt = c.CreatedAt
                })
        });
    }

    /// <summary>POST /api/debaterooms — create room (caller becomes Proponent).</summary>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateRoom([FromBody] CreateDebateRoomRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        if (!Enum.TryParse<DebateFormat>(request.Format, true, out var format))
            return BadRequest(new { title = "Invalid format.", detail = $"'{request.Format}' is not valid." });

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var room = new DebateRoom
        {
            Title = request.Title,
            Topic = request.Topic,
            MotionText = request.MotionText,
            MotionPropositionId = request.MotionPropositionId,
            ProponentUserId = userId,
            Format = format,
            TimeLimitSeconds = request.TimeLimitSeconds,
            MaxContributionsPerSide = request.MaxContributionsPerSide,
            AIRefereeEnabled = request.AIRefereeEnabled
        };

        db.DebateRooms.Add(room);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetRoom), new { id = room.Id }, new
        {
            id = room.Id,
            title = room.Title,
            status = room.Status.ToString()
        });
    }

    /// <summary>POST /api/debaterooms/{id}/join — join as Opponent.</summary>
    [HttpPost("{id:guid}/join")]
    [Authorize]
    public async Task<IActionResult> JoinRoom(Guid id, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var room = await db.DebateRooms.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (room is null) return NotFound();
        if (room.Status != DebateStatus.Open) return Conflict(new { detail = "Room is not open for joining." });
        if (room.ProponentUserId == userId) return Conflict(new { detail = "You are already the Proponent." });
        if (room.OpponentUserId is not null) return Conflict(new { detail = "Opponent slot is already filled." });

        room.OpponentUserId = userId;
        await db.SaveChangesAsync(ct);

        return Ok(new { message = "Joined as Opponent." });
    }

    /// <summary>GET /api/debaterooms/{id}/aiflags — AI referee flags for this room.</summary>
    [HttpGet("{id:guid}/aiflags")]
    public async Task<IActionResult> GetAIFlags(Guid id, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var flags = await db.DebateContributions
            .AsNoTracking()
            .Where(c => c.DebateRoomId == id && c.FallacyFlags != null)
            .Select(c => new
            {
                contributionId = c.Id,
                userId = c.UserId,
                validityScore = c.ValidityScore,
                fallacyFlags = c.FallacyFlags,
                aiRefereeComment = c.AIRefereeComment,
                orderIndex = c.OrderIndex
            })
            .OrderBy(c => c.orderIndex)
            .ToListAsync(ct);

        return Ok(new { items = flags });
    }
}

// ── Request DTOs ──────────────────────────────────────────────────────────────

public record CreateDebateRoomRequest(
    string Title,
    string Topic,
    string MotionText,
    Guid? MotionPropositionId,
    string Format,
    int TimeLimitSeconds = 300,
    int MaxContributionsPerSide = 5,
    bool AIRefereeEnabled = true);
