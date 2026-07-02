using CommonUnderstanding.Data;
using CommonUnderstanding.Models.Widget;
using CommonUnderstanding.Services.Widget;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Hubs;

/// <summary>
/// SignalR hub for real-time widget updates.
/// Publishers subscribe to their site channel; widget clients subscribe to thread channels.
/// </summary>
[Authorize]
public class WidgetHub : Hub
{
    private readonly ThreadService _threadService;
    private readonly WidgetModerationService _moderationService;
    private readonly WidgetAnalyticsService _analyticsService;
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ILogger<WidgetHub> _logger;

    public WidgetHub(
        ThreadService threadService,
        WidgetModerationService moderationService,
        WidgetAnalyticsService analyticsService,
        IDbContextFactory<ApplicationDbContext> contextFactory,
        ILogger<WidgetHub> logger)
    {
        _threadService = threadService;
        _moderationService = moderationService;
        _analyticsService = analyticsService;
        _contextFactory = contextFactory;
        _logger = logger;
    }

    /// <summary>Subscribe to real-time updates for a specific thread.</summary>
    public async Task SubscribeToThread(string threadId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"thread_{threadId}");
        _logger.LogDebug("Client {Client} subscribed to thread {Thread}", Context.ConnectionId, threadId);
    }

    /// <summary>Unsubscribe from a thread.</summary>
    public async Task UnsubscribeFromThread(string threadId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"thread_{threadId}");
    }

    /// <summary>Subscribe to publisher dashboard updates for a site.</summary>
    public async Task SubscribeToSite(string siteId)
    {
        // Verify the user owns this site
        var userId = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return;

        await using var db = await _contextFactory.CreateDbContextAsync();
        var site = await db.CommentSites
            .FirstOrDefaultAsync(s => s.Id == Guid.Parse(siteId) && s.OwnerUserId == userId);

        if (site != null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"site_{siteId}");
            _logger.LogDebug("Publisher {User} subscribed to site {Site}", userId, siteId);
        }
    }

    /// <summary>Post a comment via SignalR (real-time).</summary>
    public async Task PostComment(string siteId, string threadId, string content, string? parentId)
    {
        var userId = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? "anonymous";

        var argument = await _threadService.PostCommentAsync(
            Guid.Parse(threadId), userId, content, parentId);

        if (argument == null)
        {
            await Clients.Caller.SendAsync("error", "Thread is locked or not found");
            return;
        }

        await _analyticsService.RecordCommentAsync(Guid.Parse(siteId));

        var commentDto = new
        {
            Id = argument.Id.ToString(),
            AuthorName = userId,
            Content = argument.WarrantText,
            Upvotes = 0,
            Downvotes = 0,
            ReplyCount = 0,
            CreatedAt = argument.CreatedAt,
            ParentId = parentId,
            IsDeleted = false
        };

        // Broadcast to all subscribers of this thread
        await Clients.Group($"thread_{threadId}").SendAsync("newComment", commentDto);

        // Notify publisher dashboard
        await Clients.Group($"site_{siteId}").SendAsync("moderationAlert", new
        {
            CommentId = argument.Id.ToString(),
            Snippet = content.Length > 100 ? content[..97] + "..." : content,
            RequiresReview = false
        });
    }

    /// <summary>Vote on a comment via SignalR.</summary>
    public async Task Vote(string siteId, string argumentId, string direction)
    {
        await _analyticsService.RecordVoteAsync(Guid.Parse(siteId));

        // Broadcast vote update to thread subscribers
        await Clients.Group($"thread_{argumentId}").SendAsync("voteUpdate", new
        {
            ArgumentId = argumentId,
            Direction = direction
        });
    }

    /// <summary>Flag a comment for moderation.</summary>
    public async Task FlagComment(string siteId, string argumentId, string reason)
    {
        await _moderationService.FlagCommentAsync(
            Guid.Parse(siteId), Guid.Parse(argumentId), reason);

        await Clients.Group($"site_{siteId}").SendAsync("moderationAlert", new
        {
            CommentId = argumentId,
            Reason = reason,
            RequiresReview = true
        });
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogDebug("Widget client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogDebug("Widget client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}