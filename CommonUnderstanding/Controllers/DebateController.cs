using Microsoft.AspNetCore.Mvc;
using CommonUnderstanding.Models;
using CommonUnderstanding.Services;

namespace CommonUnderstanding.Controllers;

public class DebateController : Controller
{
    private readonly DebateMonitorService _monitorService;
    private readonly ILogger<DebateController> _logger;

    public DebateController(DebateMonitorService monitorService, ILogger<DebateController> logger)
    {
        _monitorService = monitorService;
        _logger = logger;
    }

    // MVC action to serve the Monitor view
    public IActionResult Monitor()
    {
        return View();
    }

    // API Endpoints
    [HttpPost("api/debate/sessions")]
    public IActionResult CreateSession([FromBody] CreateSessionRequest request)
    {
        try
        {
            var session = _monitorService.CreateSession(request.Title);
            return Ok(session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating debate session");
            return StatusCode(500, new { error = "Failed to create session" });
        }
    }

    [HttpGet("api/debate/sessions")]
    public IActionResult GetActiveSessions()
    {
        try
        {
            var sessions = _monitorService.GetActiveSessions();
            return Ok(sessions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active sessions");
            return StatusCode(500, new { error = "Failed to retrieve sessions" });
        }
    }

    [HttpGet("api/debate/sessions/{sessionId}")]
    public IActionResult GetSession(string sessionId)
    {
        try
        {
            var session = _monitorService.GetSession(sessionId);
            
            if (session == null)
                return NotFound(new { error = "Session not found" });
                
            return Ok(session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Failed to retrieve session" });
        }
    }

    [HttpPost("api/debate/sessions/{sessionId}/end")]
    public IActionResult EndSession(string sessionId)
    {
        try
        {
            var session = _monitorService.GetSession(sessionId);
            
            if (session == null)
                return NotFound(new { error = "Session not found" });
                
            _monitorService.EndSession(sessionId);
            
            return Ok(new { message = "Session ended successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ending session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Failed to end session" });
        }
    }

    [HttpGet("api/debate/sessions/{sessionId}/messages")]
    public IActionResult GetSessionMessages(string sessionId)
    {
        try
        {
            var session = _monitorService.GetSession(sessionId);
            
            if (session == null)
                return NotFound(new { error = "Session not found" });
                
            return Ok(session.Messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving messages for session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Failed to retrieve messages" });
        }
    }

    [HttpGet("api/debate/sessions/{sessionId}/analytics")]
    public IActionResult GetSessionAnalytics(string sessionId)
    {
        try
        {
            var session = _monitorService.GetSession(sessionId);
            
            if (session == null)
                return NotFound(new { error = "Session not found" });
                
            return Ok(session.Analytics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving analytics for session {SessionId}", sessionId);
            return StatusCode(500, new { error = "Failed to retrieve analytics" });
        }
    }
}

public class CreateSessionRequest
{
    public string Title { get; set; } = string.Empty;
}
