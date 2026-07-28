using Microsoft.SemanticKernel.ChatCompletion;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CommonUnderstanding.Services;

public sealed class ArgumentSensemakingService
{
    private const int MaxMessages = 20;
    private const int MaxMessageLength = 4000;
    private static readonly Regex TolerantResultPattern = new(
        "\\\"reply\\\"\\s*:\\s*\\\"(?<reply>.*?)\\\"\\s*,\\s*\\\"draft\\\"\\s*:\\s*\\\"(?<draft>.*?)\\\"\\s*,\\s*\\\"ready\\\"\\s*:\\s*(?<ready>true|false)",
        RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
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

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var results = await _chatService.GetChatMessageContentsAsync(
                history,
                cancellationToken: cancellationToken);
            var content = results.FirstOrDefault()?.Content?.Trim();

            if (!string.IsNullOrWhiteSpace(content))
            {
                if (TryParseResult(content, out var result))
                    return result;

                if (TryUsePlainTextResult(content, currentDraft, out result))
                {
                    _logger.LogInformation(
                        "Argument sense-making provider returned plain text; preserving the current draft.");
                    return result;
                }
            }

            _logger.LogWarning(
                "Argument sense-making response was empty or invalid on attempt {Attempt}: {Response}",
                attempt,
                content);

            if (attempt == 1)
            {
                history.AddSystemMessage(
                    "Your previous response could not be read. Respond again with only the required valid JSON object, with no markdown or commentary.");
            }
        }

        throw new InvalidOperationException("The sense-making agent returned an invalid response twice.");
    }

    private static bool TryParseResult(string content, out ArgumentSensemakingResult result)
    {
        foreach (var candidate in ExtractJsonCandidates(content))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<ArgumentSensemakingResult>(candidate, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                });

                if (parsed is null || string.IsNullOrWhiteSpace(parsed.Reply))
                    continue;

                result = parsed with
                {
                    Reply = parsed.Reply.Trim(),
                    Draft = parsed.Draft?.Trim() ?? string.Empty
                };
                return true;
            }
            catch (JsonException)
            {
                // Try an earlier object or the provider's Python-style object below.
            }
        }

        return TryParseTolerantResult(content, out result)
            || TryParseSingleQuotedResult(content, out result);
    }

    private static bool TryParseTolerantResult(string content, out ArgumentSensemakingResult result)
    {
        var match = TolerantResultPattern.Match(content);
        if (!match.Success)
        {
            result = default!;
            return false;
        }

        var reply = DecodeJsonString(match.Groups["reply"].Value).Trim();
        if (string.IsNullOrWhiteSpace(reply))
        {
            result = default!;
            return false;
        }

        result = new ArgumentSensemakingResult(
            reply,
            DecodeJsonString(match.Groups["draft"].Value).Trim(),
            bool.Parse(match.Groups["ready"].Value));
        return true;
    }

    private static bool TryParseSingleQuotedResult(string content, out ArgumentSensemakingResult result)
    {
        const string replyMarker = "{'reply': ";
        const string draftMarker = ", 'draft': ";
        const string readyMarker = ", 'ready': ";
        var replyStart = content.IndexOf(replyMarker, StringComparison.OrdinalIgnoreCase);
        var draftStart = content.IndexOf(draftMarker, StringComparison.OrdinalIgnoreCase);
        var readyStart = content.LastIndexOf(readyMarker, StringComparison.OrdinalIgnoreCase);

        if (replyStart < 0 || draftStart <= replyStart || readyStart <= draftStart)
        {
            result = default!;
            return false;
        }

        var reply = TrimQuotedValue(content[(replyStart + replyMarker.Length)..draftStart]);
        var draft = TrimQuotedValue(content[(draftStart + draftMarker.Length)..readyStart]);
        var readyText = content[(readyStart + readyMarker.Length)..].Trim().TrimEnd('}').Trim();

        if (string.IsNullOrWhiteSpace(reply) || !bool.TryParse(readyText, out var ready))
        {
            result = default!;
            return false;
        }

        result = new ArgumentSensemakingResult(reply, draft, ready);
        return true;
    }

    private static bool TryUsePlainTextResult(
        string content,
        string? currentDraft,
        out ArgumentSensemakingResult result)
    {
        var reply = content.Trim();
        var thinkEnd = reply.LastIndexOf("</think>", StringComparison.OrdinalIgnoreCase);
        if (thinkEnd >= 0)
            reply = reply[(thinkEnd + "</think>".Length)..].Trim();

        var looksLikePromptEcho = reply.Contains(
            "You are a thoughtful argument sense-making partner",
            StringComparison.OrdinalIgnoreCase);
        if (looksLikePromptEcho || reply.Length < 10)
        {
            result = default!;
            return false;
        }

        result = new ArgumentSensemakingResult(reply, currentDraft?.Trim() ?? string.Empty, false);
        return true;
    }

    private static string TrimQuotedValue(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length >= 2 &&
            ((trimmed[0] == '\'' && trimmed[^1] == '\'') ||
             (trimmed[0] == '"' && trimmed[^1] == '"')))
        {
            trimmed = trimmed[1..^1];
        }

        return DecodeJsonString(trimmed).Trim();
    }

    private static string DecodeJsonString(string value)
    {
        return Regex.Replace(value, @"\\(?:[\""\\/bfnrt]|u[0-9a-fA-F]{4})", match =>
        {
            var escape = match.Value;
            return escape[1] switch
            {
                '"' => "\"",
                '\\' => "\\",
                '/' => "/",
                'b' => "\b",
                'f' => "\f",
                'n' => "\n",
                'r' => "\r",
                't' => "\t",
                'u' => char.ConvertFromUtf32(int.Parse(escape.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)),
                _ => escape
            };
        });
    }

    private static IEnumerable<string> ExtractJsonCandidates(string content)
    {
        var end = content.LastIndexOf('}');
        if (end < 0)
            yield break;

        for (var start = content.LastIndexOf('{', end); start >= 0; start = content.LastIndexOf('{', start - 1))
            yield return content[start..(end + 1)];
    }
}

public sealed record ArgumentSensemakingMessage(string Role, string Content);

public sealed record ArgumentSensemakingResult(string Reply, string Draft, bool Ready);