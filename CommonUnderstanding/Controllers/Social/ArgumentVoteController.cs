using CommonUnderstanding.Data;
using CommonUnderstanding.Hubs;
using CommonUnderstanding.Models.Social;
using CommonUnderstanding.Services.Social;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CommonUnderstanding.Controllers.Social;

/// <summary>
/// Vote CRUD for SocialArguments.
/// Rate limiting: 30 votes/hour/user (enforced in VotingService).
/// All mutations require authentication.
/// </summary>
[ApiController]
[Route("api/arguments/{argumentId:guid}/votes")]
[Produces("application/json")]
public class ArgumentVoteController : ControllerBase
{
    private readonly VotingService _votingService;
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly IHubContext<VotingHub> _hubContext;

    public ArgumentVoteController(
        VotingService votingService,
        IDbContextFactory<ApplicationDbContext> dbFactory,
        IHubContext<VotingHub> hubContext)
    {
        _votingService = votingService;
        _dbFactory = dbFactory;
        _hubContext = hubContext;
    }

    /// <summary>GET /api/arguments/{id}/votes/tally — public tally snapshot.</summary>
    [HttpGet("tally")]
    public async Task<IActionResult> GetTally(Guid argumentId, CancellationToken ct)
    {
        var tally = await _votingService.GetTallyAsync(argumentId, ct);
        if (tally is null) return NotFound();
        return Ok(tally);
    }

    /// <summary>GET /api/arguments/{id}/votes/mine — caller's current vote.</summary>
    [HttpGet("mine")]
    [Authorize]
    public async Task<IActionResult> GetMyVote(Guid argumentId, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var vote = await _votingService.GetUserVoteAsync(userId, argumentId, ct);
        if (vote is null) return NotFound();

        return Ok(new
        {
            argumentId,
            vote = vote.Vote.ToString(),
            rationale = vote.Rationale.ToString(),
            comment = vote.Comment,
            epistemicWeight = vote.EpistemicWeight
        });
    }

    /// <summary>POST /api/arguments/{id}/votes — cast or update vote.</summary>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CastVote(Guid argumentId, [FromBody] CastVoteRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        if (!Enum.TryParse<VoteValue>(request.Vote, ignoreCase: true, out var voteValue))
            return BadRequest(new { title = "Invalid vote value.", detail = $"'{request.Vote}' is not a valid vote." });

        if (!Enum.TryParse<VoteRationale>(request.Rationale, ignoreCase: true, out var rationale))
            return BadRequest(new { title = "Invalid rationale.", detail = $"'{request.Rationale}' is not a valid rationale." });

        var result = await _votingService.CastVoteAsync(userId, argumentId, voteValue, rationale, request.Comment, ct);

        if (!result.IsSuccess)
        {
            // Distinguish rate limit (429) from other errors (422)
            if (result.ErrorMessage?.Contains("Rate limit") == true)
                return StatusCode(StatusCodes.Status429TooManyRequests,
                    new { title = "Rate limit exceeded.", detail = result.ErrorMessage, retryAfter = 3600 });

            return UnprocessableEntity(new { title = "Vote rejected.", detail = result.ErrorMessage });
        }

        // Broadcast updated tally to all SignalR subscribers
        await _hubContext.Clients.Group(VotingHub.GroupKey(argumentId))
            .SendAsync("VoteScoreUpdated", result.Tally, ct);

        return Ok(result.Tally);
    }

    /// <summary>DELETE /api/arguments/{id}/votes — retract vote (sets to Abstain).</summary>
    [HttpDelete]
    [Authorize]
    public async Task<IActionResult> RevokeVote(Guid argumentId, CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var tally = await _votingService.RevokeVoteAsync(userId, argumentId, ct);

        // Broadcast updated tally to all SignalR subscribers
        await _hubContext.Clients.Group(VotingHub.GroupKey(argumentId))
            .SendAsync("VoteScoreUpdated", tally, ct);

        return Ok(tally);
    }
}

public record CastVoteRequest(string Vote, string Rationale, string? Comment);
