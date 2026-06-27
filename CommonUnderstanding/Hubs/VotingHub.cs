using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using CommonUnderstanding.Models.Social;
using CommonUnderstanding.Services.Social;

namespace CommonUnderstanding.Hubs;

/// <summary>
/// Real-time voting hub for the social feed.
/// Clients subscribe to individual arguments and receive live tally updates.
/// 
/// Client → Server:
///   SubscribeToArgument(Guid argumentId)
///   UnsubscribeFromArgument(Guid argumentId)
///   CastVote(Guid argumentId, string vote, string rationale)
///   RevokeVote(Guid argumentId)
///
/// Server → Client:
///   "VoteScoreUpdated"  — VoteTallyDto (broadcast to argument group)
///   "VoteCastConfirmed" — { argumentId, newTally } (caller only)
///   "VoteRejected"      — { reason } (caller only)
///   "ReplyAdded"        — { parentId, replyId, replyTitle } (broadcast to argument group)
///   "ReplyCountUpdated" — { argumentId, newCount } (broadcast to argument group)
/// </summary>
public class VotingHub : Hub
{
    private readonly VotingService _votingService;
    private readonly ILogger<VotingHub> _logger;

    public VotingHub(VotingService votingService, ILogger<VotingHub> logger)
    {
        _votingService = votingService;
        _logger = logger;
    }

    // ── Group subscriptions ───────────────────────────────────────────────────

    [AllowAnonymous]
    public async Task SubscribeToArgument(Guid argumentId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupKey(argumentId));

        try
        {
            // Send current tally immediately so client gets initial state
            var tally = await _votingService.GetTallyAsync(argumentId, Context.ConnectionAborted);
            if (tally is not null)
                await Clients.Caller.SendAsync("VoteScoreUpdated", tally, Context.ConnectionAborted);
        }
        catch (OperationCanceledException)
        {
            // Client disconnected before the query completed — this is expected.
            _logger.LogDebug("SubscribeToArgument cancelled (client disconnected) for argument {ArgumentId}", argumentId);
        }
    }

    [AllowAnonymous]
    public async Task UnsubscribeFromArgument(Guid argumentId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupKey(argumentId));
    }

    // ── Vote actions ──────────────────────────────────────────────────────────

    [Authorize]
    public async Task CastVote(Guid argumentId, string vote, string rationale, string? comment = null)
    {
        var userId = Context.UserIdentifier;
        if (userId is null)
        {
            await Clients.Caller.SendAsync("VoteRejected", new { reason = "Not authenticated." });
            return;
        }

        if (!Enum.TryParse<VoteValue>(vote, ignoreCase: true, out var voteValue))
        {
            await Clients.Caller.SendAsync("VoteRejected", new { reason = $"Invalid vote value: {vote}" });
            return;
        }

        if (!Enum.TryParse<VoteRationale>(rationale, ignoreCase: true, out var rationaleValue))
        {
            await Clients.Caller.SendAsync("VoteRejected", new { reason = $"Invalid rationale: {rationale}" });
            return;
        }

        var result = await _votingService.CastVoteAsync(
            userId, argumentId, voteValue, rationaleValue, comment,
            Context.ConnectionAborted);

        if (!result.IsSuccess)
        {
            await Clients.Caller.SendAsync("VoteRejected", new { reason = result.ErrorMessage });
            return;
        }

        // Confirm to caller
        await Clients.Caller.SendAsync("VoteCastConfirmed",
            new { argumentId, vote = voteValue.ToString(), newTally = result.Tally },
            Context.ConnectionAborted);

        // Broadcast updated tally to all subscribers of this argument
        await Clients.Group(GroupKey(argumentId))
            .SendAsync("VoteScoreUpdated", result.Tally, Context.ConnectionAborted);
    }

    [Authorize]
    public async Task RevokeVote(Guid argumentId)
    {
        var userId = Context.UserIdentifier;
        if (userId is null) return;

        var tally = await _votingService.RevokeVoteAsync(userId, argumentId, Context.ConnectionAborted);

        await Clients.Group(GroupKey(argumentId))
            .SendAsync("VoteScoreUpdated", tally, Context.ConnectionAborted);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    public static string GroupKey(Guid argumentId) => $"arg-votes-{argumentId}";
    
    public async Task NotifyReplyAdded(Guid parentId, Guid replyId, string replyTitle)
    {
        await Clients.Group(GroupKey(parentId))
            .SendAsync("ReplyAdded", new { parentId, replyId, replyTitle });
    }
    
    public async Task NotifyReplyCountUpdated(Guid argumentId, int newCount)
    {
        await Clients.Group(GroupKey(argumentId))
            .SendAsync("ReplyCountUpdated", new { argumentId, newCount });
    }
}
