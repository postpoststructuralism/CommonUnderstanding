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
/// Three-phase plugin that generates bridge arguments reconciling divergent worldviews:
/// 1. Identify the most divergent arguments between the two worldviews.
/// 2. RAG: search for existing arguments that span both sides.
/// 3. LLM generation: if no bridge found, generate a new bridging argument (IsAIGenerated = true).
/// </summary>
public class BridgeArgumentPlugin
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly SemanticKernelService _kernelService;
    private readonly EmbeddingService _embeddingService;
    private readonly ILogger<BridgeArgumentPlugin> _logger;

    public BridgeArgumentPlugin(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        SemanticKernelService kernelService,
        EmbeddingService embeddingService,
        ILogger<BridgeArgumentPlugin> logger)
    {
        _dbFactory = dbFactory;
        _kernelService = kernelService;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    /// <summary>
    /// Generates bridge argument suggestions between two divergent worldviews.
    /// Prefers existing arguments from the DB over AI-generated ones.
    /// </summary>
    [KernelFunction("GenerateBridgeArguments")]
    [Description("Generates bridge arguments that could reconcile two diverging worldviews.")]
    public async Task<List<BridgeArgumentSuggestion>> GenerateBridgeArgumentsAsync(
        [Description("ID of the first Worldview")] Guid worldviewAId,
        [Description("ID of the second Worldview")] Guid worldviewBId,
        [Description("Maximum number of bridge arguments to generate")] int count = 3,
        string? requestingUserId = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var wvA = await LoadWorldviewWithArgumentsAsync(db, worldviewAId, cancellationToken);
        var wvB = await LoadWorldviewWithArgumentsAsync(db, worldviewBId, cancellationToken);

        if (wvA is null || wvB is null)
            return new List<BridgeArgumentSuggestion>();

        // ── Phase 1: Identify divergent arguments ─────────────────────────────

        var argsAIds = new HashSet<Guid>(GetAllArgumentIds(wvA));
        var argsBIds = new HashSet<Guid>(GetAllArgumentIds(wvB));

        // Arguments unique to each worldview (not shared)
        var uniqueToA = argsAIds.Except(argsBIds).ToHashSet();
        var uniqueToB = argsBIds.Except(argsAIds).ToHashSet();

        // Load top upvoted divergent arguments from each side
        var topA = await db.SocialArguments
            .AsNoTracking()
            .Include(a => a.ClaimProposition)
            .Where(a => uniqueToA.Contains(a.Id) && a.IsPublic)
            .OrderByDescending(a => a.WilsonScore)
            .Take(5)
            .ToListAsync(cancellationToken);

        var topB = await db.SocialArguments
            .AsNoTracking()
            .Include(a => a.ClaimProposition)
            .Where(a => uniqueToB.Contains(a.Id) && a.IsPublic)
            .OrderByDescending(a => a.WilsonScore)
            .Take(5)
            .ToListAsync(cancellationToken);

        if (topA.Count == 0 && topB.Count == 0)
            return new List<BridgeArgumentSuggestion>();

        var suggestions = new List<BridgeArgumentSuggestion>();

        // ── Phase 2: RAG search for existing bridge arguments ─────────────────

        // Look for existing arguments that reference propositions from both sides
        foreach (var argA in topA.Take(3))
        {
            foreach (var argB in topB.Take(3))
            {
                if (suggestions.Count >= count) break;

                // Simple textual overlap heuristic for bridge search
                // In production, use pgvector midpoint search between the two embeddings
                var existing = await FindBridgeArgumentAsync(db, argA, argB, argsAIds, argsBIds, cancellationToken);
                if (existing is not null)
                {
                    suggestions.Add(new BridgeArgumentSuggestion(
                        IsExisting: true,
                        ExistingArgumentId: existing.Id,
                        GeneratedClaim: null,
                        GeneratedWarrant: null,
                        SharedSchwartzValue: FindSharedSchwartzValue(wvA.SchwartzValues, wvB.SchwartzValues),
                        BridgeRationale: $"This existing argument '{existing.Title}' addresses themes present in both worldviews."
                    ));
                }
            }
            if (suggestions.Count >= count) break;
        }

        // ── Phase 3: LLM generation for remaining slots ───────────────────────

        int remaining = count - suggestions.Count;
        if (remaining > 0 && (topA.Count > 0 || topB.Count > 0))
        {
            var generated = await GenerateBridgeArgumentsLLMAsync(
                topA, topB, wvA, wvB, remaining, requestingUserId, db, cancellationToken);
            suggestions.AddRange(generated);
        }

        return suggestions.Take(count).ToList();
    }

    // ── RAG bridge search ─────────────────────────────────────────────────────

    private static async Task<SocialArgument?> FindBridgeArgumentAsync(
        ApplicationDbContext db,
        SocialArgument argA,
        SocialArgument argB,
        HashSet<Guid> argsAIds,
        HashSet<Guid> argsBIds,
        CancellationToken ct)
    {
        // Find public arguments that are not in either worldview's exclusive sets
        // and whose tags overlap with both sides (simple heuristic)
        var tagsA = argA.Tags.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var tagsB = argB.Tags.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var allExclusive = argsAIds.Union(argsBIds).ToHashSet();

        return await db.SocialArguments
            .AsNoTracking()
            .Include(a => a.ClaimProposition)
            .Where(a => a.IsPublic
                     && !a.IsShadowBanned
                     && !allExclusive.Contains(a.Id)
                     && (a.Tags.Any(t => tagsA.Contains(t)) || a.Tags.Any(t => tagsB.Contains(t))))
            .OrderByDescending(a => a.WilsonScore)
            .FirstOrDefaultAsync(ct);
    }

    // ── LLM bridge generation ─────────────────────────────────────────────────

    private async Task<List<BridgeArgumentSuggestion>> GenerateBridgeArgumentsLLMAsync(
        List<SocialArgument> topA,
        List<SocialArgument> topB,
        Worldview wvA,
        Worldview wvB,
        int count,
        string? requestingUserId,
        ApplicationDbContext db,
        CancellationToken ct)
    {
        var kernel = _kernelService.GetKernel();
        var chatService = kernel.GetRequiredService<IChatCompletionService>();

        var prompt = BuildBridgePrompt(topA, topB, wvA, wvB, count);
        var history = new ChatHistory();
        history.AddSystemMessage("""
            You are an expert in conflict resolution, epistemology, and the Schwartz Theory of Basic Human Values.
            Your task is to identify shared underlying values and generate bridging arguments that
            acknowledge claims from both sides while proposing a reconciling perspective.
            
            Each bridge argument must:
            1. Acknowledge a valid point from EACH side
            2. Identify a shared underlying Schwartz value (from: SelfDirection, Stimulation, Hedonism,
               Achievement, Power, Security, Conformity, Tradition, Benevolence, Universalism)
            3. Propose a resolution that a reasonable person from either worldview could accept
            
            Respond ONLY with valid JSON:
            {
              "bridgeArguments": [
                {
                  "claim": "The central bridging claim",
                  "warrant": "The logical principle connecting both sides",
                  "sharedSchwartzValue": "Universalism",
                  "rationale": "One paragraph explaining how this bridges the two worldviews"
                }
              ]
            }
            """);

        history.AddUserMessage(prompt);

        try
        {
            var response = await chatService.GetChatMessageContentAsync(history, cancellationToken: ct);
            return await ParseAndPersistBridgeArgumentsAsync(
                response.Content ?? string.Empty, requestingUserId, db, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Bridge argument LLM generation failed.");
            return new List<BridgeArgumentSuggestion>();
        }
    }

    private static string BuildBridgePrompt(
        List<SocialArgument> topA,
        List<SocialArgument> topB,
        Worldview wvA,
        Worldview wvB,
        int count)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Generate {count} bridge argument(s) between these two worldviews:");
        sb.AppendLine();
        sb.AppendLine($"WORLDVIEW A: \"{wvA.Title}\"");
        sb.AppendLine($"Core values: {string.Join(", ", wvA.SchwartzValues.Take(5))}");
        sb.AppendLine("Top arguments unique to this worldview:");
        foreach (var a in topA.Take(3))
            sb.AppendLine($"  - {a.Title}: {a.ClaimProposition?.Text}");

        sb.AppendLine();
        sb.AppendLine($"WORLDVIEW B: \"{wvB.Title}\"");
        sb.AppendLine($"Core values: {string.Join(", ", wvB.SchwartzValues.Take(5))}");
        sb.AppendLine("Top arguments unique to this worldview:");
        foreach (var a in topB.Take(3))
            sb.AppendLine($"  - {a.Title}: {a.ClaimProposition?.Text}");

        sb.AppendLine();
        sb.AppendLine("Return only the JSON object.");
        return sb.ToString();
    }

    private async Task<List<BridgeArgumentSuggestion>> ParseAndPersistBridgeArgumentsAsync(
        string content,
        string? requestingUserId,
        ApplicationDbContext db,
        CancellationToken ct)
    {
        content = StripMarkdown(content);
        var suggestions = new List<BridgeArgumentSuggestion>();

        try
        {
            var doc = JsonDocument.Parse(content);
            if (!doc.RootElement.TryGetProperty("bridgeArguments", out var arr)) return suggestions;

            foreach (var item in arr.EnumerateArray())
            {
                string claim = item.TryGetProperty("claim", out var c) ? c.GetString() ?? "" : "";
                string warrant = item.TryGetProperty("warrant", out var w) ? w.GetString() ?? "" : "";
                string schwartzValue = item.TryGetProperty("sharedSchwartzValue", out var sv) ? sv.GetString() ?? "" : "";
                string rationale = item.TryGetProperty("rationale", out var r) ? r.GetString() ?? "" : "";

                if (string.IsNullOrWhiteSpace(claim)) continue;

                // Persist as AI-generated argument requiring user confirmation
                Guid? persistedId = null;
                if (requestingUserId is not null)
                {
                    var claimProp = new SocialProposition
                    {
                        Text = claim,
                        Type = SocialPropositionType.Claim,
                        UserId = requestingUserId,
                        IsAIGenerated = true,
                        IsConfirmed = false
                    };
                    db.SocialPropositions.Add(claimProp);
                    await db.SaveChangesAsync(ct);

                    var argument = new SocialArgument
                    {
                        Title = $"Bridge: {claim[..Math.Min(100, claim.Length)]}",
                        ClaimPropositionId = claimProp.Id,
                        WarrantText = warrant,
                        UserId = requestingUserId,
                        IsPublic = false,
                        IsAIValidated = false,
                        SchwartzValues = string.IsNullOrEmpty(schwartzValue)
                            ? Array.Empty<string>()
                            : new[] { schwartzValue }
                    };
                    db.SocialArguments.Add(argument);
                    await db.SaveChangesAsync(ct);
                    persistedId = argument.Id;
                }

                suggestions.Add(new BridgeArgumentSuggestion(
                    IsExisting: false,
                    ExistingArgumentId: persistedId,
                    GeneratedClaim: claim,
                    GeneratedWarrant: warrant,
                    SharedSchwartzValue: schwartzValue,
                    BridgeRationale: rationale));
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse bridge argument JSON.");
        }

        return suggestions;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<Worldview?> LoadWorldviewWithArgumentsAsync(
        ApplicationDbContext db,
        Guid worldviewId,
        CancellationToken ct)
    {
        return await db.Worldviews
            .AsNoTracking()
            .Include(w => w.WorldviewChains)
                .ThenInclude(wc => wc.ArgumentChain)
            .FirstOrDefaultAsync(w => w.Id == worldviewId, ct);
    }

    private static IEnumerable<Guid> GetAllArgumentIds(Worldview wv) =>
        wv.WorldviewChains
            .SelectMany(wc => wc.ArgumentChain?.ArgumentIds ?? Array.Empty<Guid>())
            .Distinct();

    private static string FindSharedSchwartzValue(string[] valuesA, string[] valuesB)
    {
        var setA = new HashSet<string>(valuesA, StringComparer.OrdinalIgnoreCase);
        return valuesB.FirstOrDefault(v => setA.Contains(v)) ?? "Universalism";
    }

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

public record BridgeArgumentSuggestion(
    bool IsExisting,
    Guid? ExistingArgumentId,
    string? GeneratedClaim,
    string? GeneratedWarrant,
    string SharedSchwartzValue,
    string BridgeRationale);
