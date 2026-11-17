using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement;
using CommonUnderstanding.Services;

namespace CommonUnderstanding.Controllers;

/// <summary>
/// API endpoints for the conversation shell.
/// The agent processes messages and returns responses.
/// </summary>
[ApiController]
[Route("api/conversation")]
public class ConversationApiController : ControllerBase
{
    private readonly ConversationAgentService _agent;
    private readonly IFeatureManager _featureManager;
    private readonly ILogger<ConversationApiController> _logger;

    // In-memory conversation history. In production this will be DB-backed.
    private static readonly List<ConversationTurn> _demoHistory = new();

    public ConversationApiController(
        ConversationAgentService agent,
        IFeatureManager featureManager,
        ILogger<ConversationApiController> logger)
    {
        _agent = agent;
        _featureManager = featureManager;
        _logger = logger;
    }

    /// <summary>
    /// Send a message to the conversation agent and receive a response.
    /// </summary>
    [HttpPost("send")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Send([FromBody] ConversationMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { error = "Message is required." });
        }

        var response = await _agent.RespondAsync(request.Message, _demoHistory);

        _demoHistory.Add(new ConversationTurn
        {
            UserMessage = request.Message,
            AgentResponse = response
        });

        return Ok(new { response });
    }
}

public class ConversationMessageRequest
{
    public string Message { get; set; } = string.Empty;
}