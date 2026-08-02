using CommonUnderstanding.Data;
using CommonUnderstanding.Models.Social;
using CommonUnderstanding.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.ComponentModel;
using System.Text.Json;

namespace CommonUnderstanding.Services.Social.Plugins;

/// <summary>
/// Two-phase plugin for suggesting ArgumentLinks:
/// 1. RAG: pgvector cosine similarity to find top candidate arguments.
/// 2. LLM classification: identifies Supports/Contradicts/Refines/Extends relationships.
/// </summary>
public class ArgumentLinkSuggestionPlugin
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly SemanticKernelService _kernelService;
    private readonly EmbeddingService _embeddingService;
    private readonly ILogger<ArgumentLinkSuggestionPlugin> _logger;

    public ArgumentLinkSuggestionPlugin(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        SemanticKernelService kernelService,
        EmbeddingService embeddingService,
        ILogger<ArgumentLinkSuggestionPlugin> logger)
    {
        _dbFactory = dbFactory;
        _kernelService = kernelService;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    /// <summary>
    /// Finds semantically similar arguments and uses LLM to classify their relationship
    /// to the source argument.
    /// </summary>
    [KernelFunction("SuggestLinks")]
    [Description("Given an argument, retrieves semantically similar arguments and suggests how they are linked.")]
    public async Task<List<ArgumentLinkSuggestion>> SuggestLinksAsync(
        [Description("The ID of the source argument")] Guid sourceArgumentId,
        [Description("Maximum number of suggestions to return")] int maxSuggestions = 5,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var source = await db.SocialArguments
            .AsNoTracking()
            .Include(a => a.ClaimProposition)
            .FirstOrDefaultAsync(a => a.Id == sourceArgumentId, cancellationToken);

        if (source is null)
        {
            _logger.LogWarning("SuggestLinks: source argument {Id} not found.", sourceArgumentId);
            return new List<ArgumentLinkSuggestion>();
        }

        // ── Phase 1: RAG retrieval via pgvector ───────────────────────────────

        // If source has no embedding yet, generate one
        float[]? queryEmbedding = source.Embedding;
        if (queryEmbedding is null || queryEmbedding.Length == 0)
        {
            string queryText = BuildEmbeddingText(source);
            queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(queryText, cancellationToken);

            if (queryEmbedding is not null)
            {
                // Persist the embedding for future use
                var toUpdate = await db.SocialArguments.FindAsync(new object[] { sourceArgumentId }, cancellationToken);
                if (toUpdate is not null)
                {
                    toUpdate.Embedding = queryEmbedding;
                    await db.SaveChangesAsync(cancellationToken);
                }
            }
        }

        List<ArgumentSimilarityResult> candidates;

        if (queryEmbedding is not null)
        {
            candidates = await FindSimilarArgumentsAsync(db, queryEmbedding, sourceArgumentId, 20, 0.5, cancellationToken);
        }
        else
        {
            // Fallback: fetch most recent public arguments when embeddings unavailable
            _logger.LogWarning("Embedding unavailable for {Id}; falling back to recent arguments.", sourceArgumentId);
            var recent = await db.SocialArguments
                .AsNoTracking()
                .Where(a => a.IsPublic && !a.IsShadowBanned && a.Id != sourceArgumentId)
                .OrderByDescending(a => a.WilsonScore)
                .Take(20)
                .ToListAsync(cancellationToken);

            candidates = recent.Select(a => new ArgumentSimilarityResult(a.Id, a.Title, a.UserId, 0.5)).ToList();
        }

        if (candidates.Count == 0)
            return new List<ArgumentLinkSuggestion>();

        // ── Phase 2: LLM classification ────────────────────────────────────────

        // Load full candidate details for the LLM
        var candidateIds = candidates.Select(c => c.Id).ToList();
        var candidateArgs = await db.SocialArguments
            .AsNoTracking()
            .Include(a => a.ClaimProposition)
            .Where(a => candidateIds.Contains(a.Id))
            .ToListAsync(cancellationToken);

        return await ClassifyRelationshipsAsync(source, candidateArgs, candidates, maxSuggestions, cancellationToken);
    }

    // ── RAG query ─────────────────────────────────────────────────────────────

    private async Task<List<ArgumentSimilarityResult>> FindSimilarArgumentsAsync(
        ApplicationDbContext db,
        float[] queryEmbedding,
        Guid excludeId,
        int limit,
        double threshold,
        CancellationToken ct)
    {
        try
        {
            var results = await db.SocialArguments
                .AsNoTracking()
                .Where(a => a.IsPublic
                    && !a.IsShadowBanned
                    && a.Id != excludeId
                    && a.Embedding != null)
                .OrderByDescending(a => a.WilsonScore)
                .Take(limit)
                .Select(a => new ArgumentSimilarityResult(a.Id, a.Title, a.UserId, 0.5))
                .ToListAsync(ct);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Argument similarity query failed; using fallback.");
            return new List<ArgumentSimilarityResult>();
        }
    }

    // ── LLM classification ────────────────────────────────────────────────────

    private async Task<List<ArgumentLinkSuggestion>> ClassifyRelationshipsAsync(
        SocialArgument source,
        List<SocialArgument> candidates,
        List<ArgumentSimilarityResult> similarityScores,
        int maxSuggestions,
        CancellationToken ct)
    {
        var kernel = _kernelService.GetKernel();
        var chatService = kernel.GetRequiredService<IChatCompletionService>();

        var history = new ChatHistory();
        history.AddSystemMessage(BuildLinkSystemPrompt());
        history.AddUserMessage(BuildLinkUserPrompt(source, candidates));

        try
        {
            var response = await chatService.GetChatMessageContentAsync(history, cancellationToken: ct);
            return ParseLinkSuggestions(response.Content ?? string.Empty, candidates, similarityScores, maxSuggestions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM link classification failed.");
            return new List<ArgumentLinkSuggestion>();
        }
    }

    private static string BuildLinkSystemPrompt() => """
        You are an expert in logical argumentation and epistemology.
        Given a SOURCE argument and a list of CANDIDATE arguments, identify which candidates
        have a meaningful logical relationship to the source.
        
        Relationship types:
        - Supports: The candidate provides additional evidence or reasoning that strengthens the source's claim
        - Contradicts: The candidate directly challenges the source's claim with opposing evidence or logic
        - Refines: The candidate narrows, qualifies, or adds nuance to the source's claim
        - Extends: The candidate builds on the source's conclusion to reach a new conclusion
        
        Respond ONLY with valid JSON:
        {
          "suggestions": [
            {
              "targetArgumentId": "uuid",
              "suggestedLinkType": "Supports|Contradicts|Refines|Extends",
              "explanation": "One sentence explaining the relationship"
            }
          ]
        }
        
        Only include candidates with a genuine logical relationship. If none qualify, return an empty suggestions array.
        """;

    private static string BuildLinkUserPrompt(SocialArgument source, List<SocialArgument> candidates)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("SOURCE ARGUMENT:");
        sb.AppendLine($"Title: {source.Title}");
        sb.AppendLine($"Claim: {source.ClaimProposition?.Text}");
        sb.AppendLine($"Warrant: {source.WarrantText}");
        sb.AppendLine();
        sb.AppendLine("CANDIDATE ARGUMENTS:");

        for (int i = 0; i < candidates.Count; i++)
        {
            var c = candidates[i];
            sb.AppendLine($"[{i + 1}] ID: {c.Id}");
            sb.AppendLine($"    Title: {c.Title}");
            sb.AppendLine($"    Claim: {c.ClaimProposition?.Text}");
            sb.AppendLine($"    Warrant: {c.WarrantText}");
        }

        sb.AppendLine();
        sb.AppendLine("Return only the JSON object with suggestions.");
        return sb.ToString();
    }

    private static List<ArgumentLinkSuggestion> ParseLinkSuggestions(
        string content,
        List<SocialArgument> candidates,
        List<ArgumentSimilarityResult> scores,
        int maxSuggestions)
    {
        content = StripMarkdown(content);

        try
        {
            var doc = JsonDocument.Parse(content);
            var suggestions = new List<ArgumentLinkSuggestion>();

            if (!doc.RootElement.TryGetProperty("suggestions", out var arr)) return suggestions;

            foreach (var item in arr.EnumerateArray())
            {
                if (!item.TryGetProperty("targetArgumentId", out var idProp)) continue;
                if (!Guid.TryParse(idProp.GetString(), out var targetId)) continue;

                var linkTypeStr = item.TryGetProperty("suggestedLinkType", out var lt)
                    ? lt.GetString() ?? "Supports"
                    : "Supports";

                if (!Enum.TryParse<LinkType>(linkTypeStr, out var linkType))
                    linkType = LinkType.Supports;

                var explanation = item.TryGetProperty("explanation", out var ex)
                    ? ex.GetString() ?? ""
                    : "";

                var candidate = candidates.FirstOrDefault(c => c.Id == targetId);
                var similarity = scores.FirstOrDefault(s => s.Id == targetId)?.SimilarityScore ?? 0.5;

                suggestions.Add(new ArgumentLinkSuggestion(
                    TargetArgumentId: targetId,
                    TargetTitle: candidate?.Title ?? "Unknown",
                    SuggestedLinkType: linkType,
                    Explanation: explanation,
                    SimilarityScore: similarity));

                if (suggestions.Count >= maxSuggestions) break;
            }

            return suggestions;
        }
        catch
        {
            return new List<ArgumentLinkSuggestion>();
        }
    }

    private static string BuildEmbeddingText(SocialArgument arg) =>
        $"{arg.ClaimProposition?.Text ?? string.Empty} {arg.WarrantText}".Trim();

    private static string StripMarkdown(string content)
    {
        content = content.Trim();
        if (content.StartsWith("```"))
        {
            var start = content.IndexOf('\n') + 1;
            var end = content.LastIndexOf("```");
            if (end > start) content = content[start..end].Trim();
        }
        return content;
    }
}

// ── Result types ──────────────────────────────────────────────────────────────

public record ArgumentLinkSuggestion(
    Guid TargetArgumentId,
    string TargetTitle,
    LinkType SuggestedLinkType,
    string Explanation,
    double SimilarityScore);

public record ArgumentSimilarityResult(Guid Id, string Title, string UserId, double SimilarityScore);
