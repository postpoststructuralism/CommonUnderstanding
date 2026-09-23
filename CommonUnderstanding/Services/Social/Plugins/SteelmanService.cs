using System.Text.Json;
using CommonUnderstanding.Data;
using CommonUnderstanding.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel.ChatCompletion;

namespace CommonUnderstanding.Services.Social.Plugins;

public sealed record SteelmanSuggestion(
    Guid SourceArgumentId,
    string CounterPosition,
    string Claim,
    string Warrant,
    IReadOnlyList<string> StrongestEvidenceNeeded,
    IReadOnlyList<string> SharedValues,
    IReadOnlyList<string> Concessions,
    IReadOnlyList<string> Uncertainties,
    string FairnessRationale)
{
    public bool IsAIGenerated => true;
}

public interface ISteelmanService
{
    Task<SteelmanSuggestion?> GenerateAsync(
        Guid argumentId,
        string? requestingUserId = null,
        CancellationToken cancellationToken = default);
}

public sealed class SteelmanService : ISteelmanService
{
    private static readonly TimeSpan GenerationTimeout = TimeSpan.FromSeconds(30);
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly SemanticKernelService _kernelService;
    private readonly ILogger<SteelmanService> _logger;

    public SteelmanService(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        SemanticKernelService kernelService,
        ILogger<SteelmanService> logger)
    {
        _dbFactory = dbFactory;
        _kernelService = kernelService;
        _logger = logger;
    }

    public async Task<SteelmanSuggestion?> GenerateAsync(
        Guid argumentId,
        string? requestingUserId = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var argument = await db.SocialArguments
            .AsNoTracking()
            .Include(item => item.ClaimProposition)
            .FirstOrDefaultAsync(
                item => item.Id == argumentId
                    && !item.IsShadowBanned
                    && (item.IsPublic || item.UserId == requestingUserId),
                cancellationToken);
        if (argument is null) return null;

        var source = new
        {
            argument.Title,
            Claim = argument.ClaimProposition?.Text ?? argument.Title,
            Warrant = argument.WarrantText,
            Resolution = argument.ResolutionText,
            SchwartzValues = argument.SchwartzValues.Take(10).ToArray(),
            Tags = argument.Tags.Take(10).ToArray()
        };

        var history = new ChatHistory();
        history.AddSystemMessage(SystemPrompt);
        history.AddUserMessage(JsonSerializer.Serialize(source));

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(GenerationTimeout);
            var chatService = _kernelService.GetKernel().GetRequiredService<IChatCompletionService>();
            var response = await chatService.GetChatMessageContentAsync(history, cancellationToken: timeout.Token);
            return ParseSuggestion(argumentId, response.Content ?? string.Empty);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Steelman generation failed for argument {ArgumentId}.", argumentId);
            return null;
        }
    }

    internal static SteelmanSuggestion? ParseSuggestion(Guid sourceArgumentId, string content)
    {
        content = StripMarkdown(content);
        if (content.Length == 0 || content.Length > 16_000) return null;

        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            var counterPosition = ReadRequiredString(root, "counterPosition", 500);
            var claim = ReadRequiredString(root, "claim", 1_000);
            var warrant = ReadRequiredString(root, "warrant", 2_000);
            var fairnessRationale = ReadRequiredString(root, "fairnessRationale", 1_000);
            if (counterPosition is null || claim is null || warrant is null || fairnessRationale is null)
                return null;

            return new SteelmanSuggestion(
                sourceArgumentId,
                counterPosition,
                claim,
                warrant,
                ReadStringArray(root, "strongestEvidenceNeeded", 5, 500),
                ReadStringArray(root, "sharedValues", 5, 100),
                ReadStringArray(root, "concessions", 5, 500),
                ReadStringArray(root, "uncertainties", 5, 500),
                fairnessRationale);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadRequiredString(JsonElement root, string name, int maxLength)
    {
        if (!root.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
            return null;

        var value = property.GetString()?.Trim();
        return string.IsNullOrWhiteSpace(value) || value.Length > maxLength ? null : value;
    }

    private static IReadOnlyList<string> ReadStringArray(
        JsonElement root,
        string name,
        int maxItems,
        int maxItemLength)
    {
        if (!root.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.Array)
            return [];

        return property.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()?.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item) && item.Length <= maxItemLength)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(maxItems)
            .Select(item => item!)
            .ToArray();
    }

    private static string StripMarkdown(string content)
    {
        content = content.Trim();
        if (!content.StartsWith("```", StringComparison.Ordinal)) return content;

        var start = content.IndexOf('\n');
        var end = content.LastIndexOf("```", StringComparison.Ordinal);
        return start >= 0 && end > start ? content[(start + 1)..end].Trim() : string.Empty;
    }

    private const string SystemPrompt = """
        You are a neutral expert in argumentation, epistemology, and moral psychology.
        Construct the strongest good-faith version of the position opposing the supplied argument.
        Strengthen the opposing case; do not merely negate the source claim and do not try to reconcile the two sides.

        Requirements:
        - Preserve uncertainty and concede valid points from the source argument.
        - Use the supplied Schwartz values when they can honestly support the opposing position.
        - Describe evidence that would be needed; never invent citations, studies, statistics, quotations, or factual findings.
        - Avoid attributing motives or beliefs to the source author.
        - Produce a position a reasonable advocate of the opposing view could endorse.

        Respond only with JSON in this shape:
        {
          "counterPosition": "short name for the opposing position",
          "claim": "the strongest opposing claim",
          "warrant": "the reasoning connecting its premises to its claim",
          "strongestEvidenceNeeded": ["up to five concrete evidence requirements"],
          "sharedValues": ["up to five supplied or clearly relevant Schwartz values"],
          "concessions": ["valid points accepted from the source"],
          "uncertainties": ["material limits or unresolved questions"],
          "fairnessRationale": "why this is a fair and strong representation of the opposition"
        }
        """;
}