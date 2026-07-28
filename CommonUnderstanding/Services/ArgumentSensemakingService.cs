using Microsoft.SemanticKernel.ChatCompletion;
using System.Text.Json;

namespace CommonUnderstanding.Services;

public sealed class ArgumentSensemakingService
{
    private const int MaxMessages = 20;
    private const int MaxMessageLength = 4000;
    private readonly IChatCompletionService _chatService;
    private readonly ILogger<ArgumentSensemakingService> _logger;

    public ArgumentSensemakingService(
        IChatCompletionService chatService,
        ILogger<ArgumentSensemakingService> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    public async Task<ArgumentSensemakingResult> ContinueAsync(
        IReadOnlyList<ArgumentSensemakingMessage> messages,
        string? currentDraft,
        CancellationToken cancellationToken)
    {
        var boundedMessages = messages
            .Where(message => !string.IsNullOrWhiteSpace(message.Content))
            .TakeLast(MaxMessages)
            .Select(message => new ArgumentSensemakingMessage(
                message.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase) ? "assistant" : "user",
                message.Content.Trim()[..Math.Min(message.Content.Trim().Length, MaxMessageLength)]))
            .ToList();

        if (boundedMessages.Count == 0 || boundedMessages[^1].Role != "user")
            throw new ArgumentException("The conversation must end with a user message.", nameof(messages));

        var history = new ChatHistory();
        history.AddSystemMessage("""
            You are a thoughtful argument sense-making partner. Help the user discover and articulate what
            they believe; do not debate them, diagnose people, take sides, or manufacture certainty. Reflect
            tensions and emotions without presenting psychological or legal conclusions. Ask exactly one
            focused, open question at a time. When another person's perspective is involved, distinguish what
            the user observed from what they infer, and invite the most charitable plausible interpretation.

            Maintain an evolving first-person argument draft that faithfully captures the user's own thinking.
            A useful draft identifies the situation, core concern or claim, observations, reasoning, uncertainty,
            values or needs, and what understanding or change the user seeks. Do not invent facts. Set ready true
            only when the draft is coherent and specific enough to submit for structured analysis. Even when ready,
            the reply may invite one final refinement.

            Return only valid JSON with this shape:
            {"reply":"brief reflection followed by exactly one question","draft":"first-person standalone draft","ready":false}
            Do not use markdown fences. Keep reply under 140 words and draft under 900 words.
            """);

        if (!string.IsNullOrWhiteSpace(currentDraft))
        {
            var boundedDraft = currentDraft.Trim()[..Math.Min(currentDraft.Trim().Length, 8000)];
            history.AddSystemMessage($"The user's current editable draft is below. Revise it only to reflect new understanding; preserve useful details and the user's voice.\n\n{boundedDraft}");
        }

        foreach (var message in boundedMessages)
        {
            if (message.Role == "assistant")
                history.AddAssistantMessage(message.Content);
            else
                history.AddUserMessage(message.Content);
        }

        var results = await _chatService.GetChatMessageContentsAsync(
            history,
            cancellationToken: cancellationToken);
        var content = results.FirstOrDefault()?.Content?.Trim();

        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("The sense-making agent returned an empty response.");

        try
        {
            var json = ExtractJson(content);
            var result = JsonSerializer.Deserialize<ArgumentSensemakingResult>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result is null || string.IsNullOrWhiteSpace(result.Reply))
                throw new JsonException("The agent response did not include a reply.");

            return result with
            {
                Reply = result.Reply.Trim(),
                Draft = result.Draft?.Trim() ?? string.Empty
            };
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Could not parse argument sense-making response: {Response}", content);
            throw new InvalidOperationException("The sense-making agent returned an invalid response.", exception);
        }
    }

    private static string ExtractJson(string content)
    {
        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');
        return start >= 0 && end > start ? content[start..(end + 1)] : content;
    }
}

public sealed record ArgumentSensemakingMessage(string Role, string Content);

public sealed record ArgumentSensemakingResult(string Reply, string Draft, bool Ready);