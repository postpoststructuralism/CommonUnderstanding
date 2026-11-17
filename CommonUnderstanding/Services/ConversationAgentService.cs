using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.FeatureManagement;

namespace CommonUnderstanding.Services;

/// <summary>
/// The conversation agent — the heart of the new UX.
///
/// This agent drives the conversation with the user, helping them explore
/// what they believe, why they believe it, and how their understanding
/// relates to others'. It uses Semantic Kernel for LLM access and can
/// invoke domain tools (discovery, graph, arguments) when the AgentTools
/// feature flag is enabled.
///
/// The agent is a persistent mediator — it maintains conversation context
/// and guides the user toward deeper understanding.
/// </summary>
public class ConversationAgentService
{
    private readonly SemanticKernelService _kernelService;
    private readonly IFeatureManager _featureManager;
    private readonly ILogger<ConversationAgentService> _logger;

    // Domain services for agent tools
    private readonly UnderstandingQueryService _queryService;
    private readonly ArgumentDecompositionService _decompositionService;
    private readonly BeliefAnalysisService _beliefService;

    private const string SystemPrompt = """
        You are the guide for "A Common Understanding" — a space where people explore
        what they believe, why they believe it, and how their understanding relates to others'.

        Your role is to be a thoughtful, curious, and compassionate conversation partner.
        You are not a debater or a persuader. You are a mediator of understanding.

        Core principles:
        - Listen deeply before responding. Ask clarifying questions.
        - Help the user articulate their beliefs more precisely.
        - Gently surface assumptions they may not have examined.
        - Connect their ideas to broader frameworks and perspectives.
        - Never judge, dismiss, or argue against their beliefs.
        - When appropriate, note how their view relates to others' views.
        - Keep responses concise but substantive — 2-4 paragraphs unless asked for more.
        - Use a warm, natural tone. Avoid academic jargon unless the user introduces it.

        You are speaking with one person at a time. This is a private, reflective conversation.
        The goal is not to reach agreement but to deepen understanding — theirs and yours.
        """;

    public ConversationAgentService(
        SemanticKernelService kernelService,
        IFeatureManager featureManager,
            ILogger<ConversationAgentService> logger,
            UnderstandingQueryService queryService,
            ArgumentDecompositionService decompositionService,
            BeliefAnalysisService beliefService)
    {
        _kernelService = kernelService;
        _featureManager = featureManager;
        _logger = logger;
            _queryService = queryService;
            _decompositionService = decompositionService;
            _beliefService = beliefService;
    }

    /// <summary>
    /// Process a user message and return the agent's response.
    /// </summary>
    /// <param name="userMessage">The user's message.</param>
    /// <param name="conversationHistory">Previous messages in this conversation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The agent's text response.</returns>
    public async Task<string> RespondAsync(
        string userMessage,
        IReadOnlyList<ConversationTurn> conversationHistory,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var kernel = _kernelService.GetKernel();
            var chat = kernel.GetRequiredService<IChatCompletionService>();

            // Build the chat history for this request
            var history = new ChatHistory(SystemPrompt);

            // Add prior conversation turns (last N to stay within context window)
            const int maxHistoryTurns = 20;
            var recentTurns = conversationHistory
                .TakeLast(maxHistoryTurns)
                .ToList();

            foreach (var turn in recentTurns)
            {
                history.AddUserMessage(turn.UserMessage);
                if (!string.IsNullOrWhiteSpace(turn.AgentResponse))
                {
                    history.AddAssistantMessage(turn.AgentResponse);
                }
            }

            // Add the current user message
            history.AddUserMessage(userMessage);

            // If agent tools are enabled, register them with the kernel
            if (await _featureManager.IsEnabledAsync("AgentTools"))
            {
                EnableAgentTools(kernel);
            }

            // Get the response
            var settings = new PromptExecutionSettings
            {
                ExtensionData = new Dictionary<string, object>
                {
                    ["max_tokens"] = 1024,
                    ["temperature"] = 0.7
                }
            };

            var response = await chat.GetChatMessageContentAsync(history, settings, kernel, cancellationToken);
            return response.Content ?? "I'm not sure how to respond to that. Could you rephrase?";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent response failed for message: {Message}", userMessage);
            return "I'm having trouble thinking clearly right now. Could we pause and try again in a moment?";
        }
    }

    /// <summary>
    /// Register domain tools as kernel plugins when the AgentTools flag is enabled.
    /// These let the agent invoke existing features (discovery, graph, arguments)
    /// as tools during conversation.
    /// </summary>
    private void EnableAgentTools(Kernel kernel)
    {
            // Only register if not already registered (idempotent across turns)
            if (kernel.Plugins.Any(p => p.Name == "DiscoveryPlugin"))
                return;

            try
            {
                kernel.Plugins.AddFromObject(new DiscoveryPlugin(_queryService), "DiscoveryPlugin");
                kernel.Plugins.AddFromObject(new ArgumentPlugin(_decompositionService), "ArgumentPlugin");
                kernel.Plugins.AddFromObject(new BeliefPlugin(_beliefService), "BeliefPlugin");
                _logger.LogInformation("Agent tools registered: Discovery, Argument, Belief");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to register agent tools — continuing in conversation-only mode");
            }
    }
}

/// <summary>
/// A single turn in a conversation.
/// </summary>
public class ConversationTurn
{
    public string UserMessage { get; set; } = string.Empty;
    public string AgentResponse { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}