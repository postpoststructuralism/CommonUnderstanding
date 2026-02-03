using Microsoft.AspNetCore.SignalR;
using CommonUnderstanding.Models;
using CommonUnderstanding.Services;

namespace CommonUnderstanding.Hubs;

public class DebateHub : Hub
{
    private readonly DebateMonitorService _monitorService;
    private readonly ILogger<DebateHub> _logger;
    private static readonly Dictionary<string, string> _userSessions = new();

    public DebateHub(DebateMonitorService monitorService, ILogger<DebateHub> logger)
    {
        _monitorService = monitorService;
        _logger = logger;
    }

    public async Task JoinSession(string sessionId, string userName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);
        _userSessions[Context.ConnectionId] = sessionId;
        
        _logger.LogInformation("User {UserName} joined debate session {SessionId}", userName, sessionId);
        
        await Clients.Group(sessionId).SendAsync("UserJoined", new
        {
            userName,
            timestamp = DateTime.UtcNow
        });
    }

    public async Task LeaveSession(string sessionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, sessionId);
        _userSessions.Remove(Context.ConnectionId);
        
        await Clients.Group(sessionId).SendAsync("UserLeft", new
        {
            timestamp = DateTime.UtcNow
        });
    }

    public async Task SendMessage(string sessionId, string userName, string content)
    {
        try
        {
            _logger.LogInformation("Processing message from {UserName} in session {SessionId}", userName, sessionId);
            
            // Immediately echo the message to all participants
            await Clients.Group(sessionId).SendAsync("ReceiveMessage", new
            {
                userName,
                content,
                timestamp = DateTime.UtcNow,
                isAnalyzing = true
            });

            // Analyze the message with AI (this happens in the background)
            var analyzedMessage = await _monitorService.AnalyzeMessageAsync(
                sessionId,
                Context.ConnectionId,
                userName,
                content
            );

            // Broadcast the analysis results
            await Clients.Group(sessionId).SendAsync("ReceiveAnalysis", analyzedMessage);

            // If there are fact checks, send them separately for highlighting
            if (analyzedMessage.FactChecks.Any())
            {
                await Clients.Group(sessionId).SendAsync("ReceiveFactChecks", new
                {
                    messageId = analyzedMessage.Id,
                    factChecks = analyzedMessage.FactChecks
                });
            }

            // If there are misunderstanding alerts, send them with higher priority
            if (analyzedMessage.MisunderstandingAlerts.Any())
            {
                var highSeverityAlerts = analyzedMessage.MisunderstandingAlerts
                    .Where(a => a.Severity > 0.6)
                    .ToList();
                    
                if (highSeverityAlerts.Any())
                {
                    await Clients.Group(sessionId).SendAsync("ReceiveMisunderstandingAlert", new
                    {
                        messageId = analyzedMessage.Id,
                        alerts = highSeverityAlerts,
                        severity = "HIGH"
                    });
                }
            }

            // Send intent insights
            if (analyzedMessage.IntentAnalysis != null)
            {
                await Clients.Group(sessionId).SendAsync("ReceiveIntentAnalysis", new
                {
                    messageId = analyzedMessage.Id,
                    intent = analyzedMessage.IntentAnalysis
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing debate message");
            
            await Clients.Caller.SendAsync("Error", new
            {
                message = "Failed to analyze message. Please try again.",
                timestamp = DateTime.UtcNow
            });
        }
    }

    public async Task RequestSessionSummary(string sessionId)
    {
        var session = _monitorService.GetSession(sessionId);
        
        if (session != null)
        {
            await Clients.Caller.SendAsync("ReceiveSessionSummary", new
            {
                session.Id,
                session.Title,
                session.Analytics,
                messageCount = session.Messages.Count,
                duration = session.EndedAt.HasValue 
                    ? session.EndedAt.Value - session.StartedAt 
                    : DateTime.UtcNow - session.StartedAt
            });
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_userSessions.TryGetValue(Context.ConnectionId, out var sessionId))
        {
            await Clients.Group(sessionId).SendAsync("UserLeft", new
            {
                timestamp = DateTime.UtcNow
            });
            
            _userSessions.Remove(Context.ConnectionId);
        }
        
        await base.OnDisconnectedAsync(exception);
    }
}
