using CommonUnderstanding.Data;
using CommonUnderstanding.Models.Social;
using CommonUnderstanding.Services.Social;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Hubs;

/// <summary>
/// Real-time hub for collaborative argument chain editing.
/// Broadcasts graph mutations (node/edge add/remove) to all co-editors.
///
/// Client → Server:
///   JoinChainSession(Guid chainId)
///   LeaveChainSession(Guid chainId)
///   NotifyArgumentAdded(Guid chainId, Guid argumentId)
///   NotifyArgumentRemoved(Guid chainId, Guid argumentId)
///   NotifyLinkCreated(Guid chainId, object link)
///
/// Server → Client:
///   "ChainArgumentAdded"   — { chainId, argument }
///   "ChainArgumentRemoved" — { chainId, argumentId }
///   "ChainLinkCreated"     — { chainId, link }
///   "ChainUpdated"         — full refresh signal
/// </summary>
[Authorize]
public class ChainUpdateHub : Hub
{
    private readonly SingletonDbContextFactory _dbFactory;
    private readonly ILogger<ChainUpdateHub> _logger;

    public ChainUpdateHub(
        SingletonDbContextFactory dbFactory,
        ILogger<ChainUpdateHub> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task JoinChainSession(Guid chainId)
    {
        try
        {
            var userId = Context.UserIdentifier!;

            await using var db = await _dbFactory.CreateDbContextAsync(Context.ConnectionAborted);

            var chain = await db.ArgumentChains
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == chainId, Context.ConnectionAborted);

            // Access check: owner or chain is public
            if (chain is null || (!chain.IsPublic && chain.UserId != userId))
            {
                Context.Abort();
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, ChainGroup(chainId));
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("JoinChainSession cancelled (client disconnected) for chain {ChainId}", chainId);
        }
    }

    public async Task LeaveChainSession(Guid chainId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, ChainGroup(chainId));
    }

    public async Task NotifyArgumentAdded(Guid chainId, Guid argumentId)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(Context.ConnectionAborted);

            var argument = await db.SocialArguments
                .AsNoTracking()
                .Include(a => a.ClaimProposition)
                .FirstOrDefaultAsync(a => a.Id == argumentId, Context.ConnectionAborted);

            if (argument is null) return;

            var dto = MapArgumentToDto(argument);

            await Clients.OthersInGroup(ChainGroup(chainId))
                .SendAsync("ChainArgumentAdded", new { chainId, argument = dto }, Context.ConnectionAborted);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("NotifyArgumentAdded cancelled (client disconnected) for chain {ChainId}", chainId);
        }
    }

    public async Task NotifyArgumentRemoved(Guid chainId, Guid argumentId)
    {
        try
        {
            await Clients.OthersInGroup(ChainGroup(chainId))
                .SendAsync("ChainArgumentRemoved", new { chainId, argumentId }, Context.ConnectionAborted);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("NotifyArgumentRemoved cancelled (client disconnected) for chain {ChainId}", chainId);
        }
    }

    public async Task NotifyLinkCreated(Guid chainId, object link)
    {
        try
        {
            await Clients.OthersInGroup(ChainGroup(chainId))
                .SendAsync("ChainLinkCreated", new { chainId, link }, Context.ConnectionAborted);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("NotifyLinkCreated cancelled (client disconnected) for chain {ChainId}", chainId);
        }
    }

    private static object MapArgumentToDto(SocialArgument a) => new
    {
        id = a.Id,
        title = a.Title,
        claimText = a.ClaimProposition?.Text,
        wilsonScore = a.WilsonScore,
        userId = a.UserId,
        tags = a.Tags,
        schwartzValues = a.SchwartzValues
    };

    private static string ChainGroup(Guid chainId) => $"chain-{chainId}";
}
