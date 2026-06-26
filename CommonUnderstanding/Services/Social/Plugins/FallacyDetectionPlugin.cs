using CommonUnderstanding.Services;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.ComponentModel;
using System.Text.Json;

namespace CommonUnderstanding.Services.Social.Plugins;

/// <summary>
/// Detects logical fallacies in debate contributions in real-time.
/// Uses zero-shot classification against a predefined 20-fallacy taxonomy.
/// Primary model: gpt-4o-mini or fastest available provider (latency target < 3s).
/// Does NOT use RAG or embeddings.
/// </summary>
public class FallacyDetectionPlugin
{
    private readonly SemanticKernelService _kernelService;
    private readonly ILogger<FallacyDetectionPlugin> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public FallacyDetectionPlugin(
        SemanticKernelService kernelService,
        ILogger<FallacyDetectionPlugin> logger)
    {
        _kernelService = kernelService;
        _logger = logger;
    }

    /// <summary>
    /// Detects logical fallacies in argument text.
    /// Returns a structured result with validity score and flagged fallacies.
    /// </summary>
    [KernelFunction("DetectFallacies")]
    [Description("Detects logical fallacies in a debate contribution in real-time.")]
    public async Task<FallacyDetectionResult> DetectFallaciesAsync(
        [Description("The argument text (claim + evidence + warrant)")] string argumentText,
        [Description("The prior contributions in the debate for context")] string priorContext,
        [Description("The motion being debated")] string motionText,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(argumentText))
            return new FallacyDetectionResult(true, 1.0, new List<FallacyFlag>(), null);

        var kernel = _kernelService.GetKernel();
        var chatService = kernel.GetRequiredService<IChatCompletionService>();

        var history = new ChatHistory();
        history.AddSystemMessage(BuildSystemPrompt());
        history.AddUserMessage(BuildUserPrompt(argumentText, priorContext, motionText));

        try
        {
            var response = await chatService.GetChatMessageContentAsync(
                history,
                cancellationToken: cancellationToken);

            return ParseResponse(response.Content ?? string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fallacy detection failed; returning optimistic result.");
            return new FallacyDetectionResult(true, 0.5, new List<FallacyFlag>(),
                "Fallacy detection unavailable.");
        }
    }

    // ── Prompt construction ───────────────────────────────────────────────────

    private static string BuildSystemPrompt() => """
        You are a formal logic expert analyzing debate contributions for logical fallacies.
        
        Evaluate the argument against these 20 common informal fallacies:
        1. Ad Hominem – attacking the person instead of their argument
        2. Straw Man – misrepresenting an opponent's argument
        3. False Dichotomy – presenting only two options when more exist
        4. Appeal to Authority – using authority as evidence without other support
        5. Slippery Slope – assuming one event inevitably leads to extreme consequences
        6. Hasty Generalization – drawing broad conclusions from insufficient samples
        7. Circular Reasoning – the conclusion is used as a premise
        8. Appeal to Emotion – manipulating emotions instead of using valid reasoning
        9. Red Herring – introducing irrelevant information to distract
        10. Appeal to Popularity – something is true because many people believe it
        11. False Cause – assuming correlation implies causation
        12. Equivocation – using a word with multiple meanings in a misleading way
        13. Appeal to Ignorance – claiming something is true because it hasn't been proven false
        14. Bandwagon – appeal to popularity of an action
        15. Genetic Fallacy – dismissing based on origin rather than merit
        16. Tu Quoque – deflecting criticism by pointing out opponent's similar flaws
        17. Burden of Proof – shifting burden of proof inappropriately
        18. No True Scotsman – modifying a claim to avoid counterexamples
        19. Black or White – similar to false dichotomy
        20. Loaded Question – asking a question with a questionable presupposition
        
        Respond ONLY with valid JSON in this exact format:
        {
          "isValid": true,
          "validityScore": 0.85,
          "fallacies": [
            {
              "name": "FallacyName",
              "description": "One sentence explanation",
              "quotedText": "The exact text from the argument that contains the fallacy"
            }
          ],
          "suggestedImprovement": "One sentence on how to strengthen the argument, or null if no improvement needed"
        }
        
        validityScore ranges from 0.0 (completely invalid) to 1.0 (logically sound).
        An argument is isValid = true if validityScore >= 0.5 and no major fallacies are present.
        """;

    private static string BuildUserPrompt(string argumentText, string priorContext, string motionText) =>
        $"""
        MOTION BEING DEBATED:
        {motionText}

        PRIOR DEBATE CONTEXT:
        {priorContext}

        ARGUMENT TO ANALYZE:
        {argumentText}

        Analyze this argument for logical fallacies. Return only the JSON object, no markdown.
        """;

    // ── Response parsing ──────────────────────────────────────────────────────

    private FallacyDetectionResult ParseResponse(string content)
    {
        // Strip markdown code fences if present
        content = content.Trim();
        if (content.StartsWith("```"))
        {
            var start = content.IndexOf('\n') + 1;
            var end = content.LastIndexOf("```");
            if (end > start)
                content = content[start..end].Trim();
        }

        try
        {
            var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            bool isValid = root.TryGetProperty("isValid", out var iv) && iv.GetBoolean();
            double score = root.TryGetProperty("validityScore", out var vs) ? vs.GetDouble() : 0.5;

            var fallacies = new List<FallacyFlag>();
            if (root.TryGetProperty("fallacies", out var fa) && fa.ValueKind == JsonValueKind.Array)
            {
                foreach (var f in fa.EnumerateArray())
                {
                    fallacies.Add(new FallacyFlag(
                        Name: f.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                        Description: f.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
                        QuotedText: f.TryGetProperty("quotedText", out var q) ? q.GetString() ?? "" : ""
                    ));
                }
            }

            string? improvement = root.TryGetProperty("suggestedImprovement", out var si)
                && si.ValueKind != JsonValueKind.Null
                ? si.GetString()
                : null;

            return new FallacyDetectionResult(isValid, score, fallacies, improvement);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse fallacy detection JSON: {Content}", content);
            return new FallacyDetectionResult(true, 0.5, new List<FallacyFlag>(),
                "Could not parse AI response.");
        }
    }

    /// <summary>
    /// Assesses how relevant and effective a follow-up argument (reply) is at
    /// addressing its parent argument. Returns a score from 0.0 to 1.0 with
    /// explanatory notes.
    /// </summary>
    [KernelFunction("AssessFollowUpRelevance")]
    [Description("Assesses how relevant and effective a follow-up argument is at addressing its parent argument.")]
    public async Task<FollowUpRelevanceResult> AssessFollowUpRelevanceAsync(
        [Description("The follow-up (reply) argument text")] string replyText,
        [Description("The parent argument text that the reply is responding to")] string parentText,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(replyText) || string.IsNullOrWhiteSpace(parentText))
            return new FollowUpRelevanceResult(0.5, "Insufficient text to assess.");

        var kernel = _kernelService.GetKernel();
        var chatService = kernel.GetRequiredService<IChatCompletionService>();

        var history = new ChatHistory();
        history.AddSystemMessage(BuildRelevanceSystemPrompt());
        history.AddUserMessage(BuildRelevanceUserPrompt(replyText, parentText));

        try
        {
            var response = await chatService.GetChatMessageContentAsync(
                history,
                cancellationToken: cancellationToken);

            return ParseRelevanceResponse(response.Content ?? string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Follow-up relevance assessment failed; returning neutral score.");
            return new FollowUpRelevanceResult(0.5, "Relevance assessment unavailable.");
        }
    }

    private static string BuildRelevanceSystemPrompt() => """
        You are a debate analyst evaluating how well a follow-up argument (reply) addresses
        the argument it is responding to.

        Evaluate the reply on these criteria:
        1. Relevance (0.0–1.0): Does the reply directly engage with the parent's claim and warrant,
           or does it change the subject / introduce unrelated points?
        2. Effectiveness (0.0–1.0): How well does the reply challenge, support, or refine the
           parent argument? Does it provide new reasoning, evidence, or perspective?
        3. Clarity (0.0–1.0): Is the reply clearly stated and easy to understand?

        The overall relevanceScore is the weighted average of these three (relevance 50%,
        effectiveness 35%, clarity 15%).

        Respond ONLY with valid JSON in this exact format:
        {
          "relevanceScore": 0.75,
          "effectivenessNotes": "2-3 sentences explaining the assessment, noting specific strengths or weaknesses"
        }

        relevanceScore ranges from 0.0 (completely irrelevant / ineffective) to 1.0
        (highly relevant and effective).
        """;

    private static string BuildRelevanceUserPrompt(string replyText, string parentText) =>
        $"""
        PARENT ARGUMENT:
        {parentText}

        FOLLOW-UP REPLY:
        {replyText}

        Evaluate how relevant and effective this reply is at addressing the parent argument.
        Return only the JSON object, no markdown.
        """;

    private FollowUpRelevanceResult ParseRelevanceResponse(string content)
    {
        // Strip markdown code fences if present
        content = content.Trim();
        if (content.StartsWith("```"))
        {
            var start = content.IndexOf('\n') + 1;
            var end = content.LastIndexOf("```");
            if (end > start)
                content = content[start..end].Trim();
        }

        try
        {
            var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            double score = root.TryGetProperty("relevanceScore", out var rs) ? rs.GetDouble() : 0.5;
            score = Math.Clamp(score, 0.0, 1.0);

            string notes = root.TryGetProperty("effectivenessNotes", out var en)
                && en.ValueKind != JsonValueKind.Null
                ? en.GetString() ?? ""
                : "";

            return new FollowUpRelevanceResult(score, notes);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse follow-up relevance JSON: {Content}", content);
            return new FollowUpRelevanceResult(0.5, "Could not parse AI response.");
        }
    }

    // ── Result types ───────────────────────────────────────────────────────────────
}

    public record FallacyDetectionResult(
    bool IsValid,
    double ValidityScore,
    List<FallacyFlag> Fallacies,
    string? SuggestedImprovement);

public record FallacyFlag(string Name, string Description, string QuotedText);

public record FollowUpRelevanceResult(
    double RelevanceScore,
    string EffectivenessNotes);
