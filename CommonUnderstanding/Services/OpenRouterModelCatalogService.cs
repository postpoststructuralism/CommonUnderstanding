using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CommonUnderstanding.Services;

public sealed class OpenRouterModelCatalogService
{
    private const string ModelsEndpoint = "https://openrouter.ai/api/v1/models";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan FailureBackoffMin = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan FailureBackoffMax = TimeSpan.FromMinutes(20);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OpenRouterModelCatalogService> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private CatalogSnapshot _snapshot = CatalogSnapshot.Empty;
    private int _consecutiveFailures;
    private DateTimeOffset _nextRefreshAllowedAt = DateTimeOffset.MinValue;

    public OpenRouterModelCatalogService(
        IHttpClientFactory httpClientFactory,
        ILogger<OpenRouterModelCatalogService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public IReadOnlyList<string> GetCachedModels() => _snapshot.Models;

    public async Task<IReadOnlyList<string>> GetAvailableModelsAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        if (!forceRefresh && _snapshot.IsFresh(CacheTtl))
            return _snapshot.Models;

        if (!forceRefresh && _snapshot.Models.Count > 0 && now < _nextRefreshAllowedAt)
            return _snapshot.Models;

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            now = DateTimeOffset.UtcNow;
            if (!forceRefresh && _snapshot.IsFresh(CacheTtl))
                return _snapshot.Models;

            if (!forceRefresh && _snapshot.Models.Count > 0 && now < _nextRefreshAllowedAt)
                return _snapshot.Models;

            var models = await FetchModelsAsync(cancellationToken);
            _snapshot = new CatalogSnapshot(models, DateTimeOffset.UtcNow);
            _consecutiveFailures = 0;
            _nextRefreshAllowedAt = DateTimeOffset.MinValue;

            _logger.LogInformation("Loaded {Count} live free OpenRouter models.", models.Count);
            return models;
        }
        catch (Exception ex)
        {
            _consecutiveFailures++;
            var backoff = ComputeFailureBackoff(_consecutiveFailures);
            _nextRefreshAllowedAt = DateTimeOffset.UtcNow + backoff;

            if (_snapshot.Models.Count > 0)
            {
                _logger.LogWarning(ex,
                    "Failed to refresh OpenRouter model catalog. Using cached list of {Count} models and backing off refreshes for {Minutes} minute(s).",
                    _snapshot.Models.Count,
                    Math.Round(backoff.TotalMinutes, 1));
                return _snapshot.Models;
            }

            _logger.LogWarning(ex,
                "Failed to load OpenRouter model catalog and no cached catalog is available. Falling back to bootstrap list for {Minutes} minute(s).",
                Math.Round(backoff.TotalMinutes, 1));
            return OpenRouterModelCatalog.AvailableModels;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public async Task<bool> IsValidAsync(string? modelId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelId))
            return false;

        var models = await GetAvailableModelsAsync(cancellationToken: cancellationToken);
        return models.Contains(modelId, StringComparer.Ordinal);
    }

    public async Task<string?> GetNextModelAsync(
        IReadOnlySet<string> triedModels,
        CancellationToken cancellationToken = default)
    {
        var models = await GetAvailableModelsAsync(cancellationToken: cancellationToken);
        return models.FirstOrDefault(model => !triedModels.Contains(model));
    }

    private async Task<IReadOnlyList<string>> FetchModelsAsync(CancellationToken cancellationToken)
    {
        using var client = _httpClientFactory.CreateClient();
        using var response = await client.GetAsync(ModelsEndpoint, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var modelElements = EnumerateModelElements(doc.RootElement);

        // Shuffle within each priority tier so concurrent requests from this server
        // spread across different models rather than all hitting the same one first.
        var rng = Random.Shared;
        var models = modelElements
            .Select(ToCandidate)
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!)
            .Where(candidate => candidate.IsFree)
            .GroupBy(candidate => candidate.Priority)
            .OrderBy(g => g.Key)
            .SelectMany(g => g.OrderBy(_ => rng.Next()))  // shuffle within tier
            .Select(candidate => candidate.Id)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return models;
    }

    private static IEnumerable<JsonElement> EnumerateModelElements(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
            return root.EnumerateArray();

        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("data", out var data) &&
            data.ValueKind == JsonValueKind.Array)
            return data.EnumerateArray();

        return [];
    }

    private static OpenRouterModelCandidate? ToCandidate(JsonElement model)
    {
        if (!model.TryGetProperty("id", out var idElement))
            return null;

        var id = idElement.GetString();
        if (string.IsNullOrWhiteSpace(id))
            return null;

        var name = model.TryGetProperty("name", out var nameElement)
            ? nameElement.GetString() ?? id
            : id;

        var isFree = false;
        if (model.TryGetProperty("pricing", out var pricing) && pricing.ValueKind == JsonValueKind.Object)
        {
            isFree = IsZeroPrice(pricing, "prompt") && IsZeroPrice(pricing, "completion");
        }

        var sizeBillions = ParseModelSizeBillions(id, name);
        var priority = CalculatePriority(id, name, sizeBillions);

        return new OpenRouterModelCandidate(id, isFree, priority, sizeBillions);
    }

    private static bool IsZeroPrice(JsonElement pricing, string propertyName)
    {
        if (!pricing.TryGetProperty(propertyName, out var value))
            return false;

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetDecimal() == 0m,
            JsonValueKind.String => decimal.TryParse(
                value.GetString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed) && parsed == 0m,
            _ => false
        };
    }

    private static double? ParseModelSizeBillions(string id, string name)
    {
        var match = Regex.Match($"{id} {name}", @"(?<size>\d+(?:\.\d+)?)\s*b", RegexOptions.IgnoreCase);
        if (!match.Success)
            return null;

        return double.TryParse(match.Groups["size"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var size)
            ? size
            : null;
    }

    private static int CalculatePriority(string id, string name, double? sizeBillions)
    {
        var combined = $"{id} {name}".ToLowerInvariant();

        if (combined.Contains("flash-lite")) return 0;
        if (combined.Contains("flash")) return 1;
        if (combined.Contains("mini")) return 2;
        if (combined.Contains("nano") || combined.Contains("small")) return 3;
        if (sizeBillions is <= 4.5) return 4;
        if (sizeBillions is <= 8.5) return 5;
        if (sizeBillions is <= 14) return 6;
        return 10;
    }

    private static TimeSpan ComputeFailureBackoff(int consecutiveFailures)
    {
        var multiplier = Math.Min(6, Math.Max(0, consecutiveFailures - 1));
        var minutes = FailureBackoffMin.TotalMinutes * Math.Pow(2, multiplier);
        return TimeSpan.FromMinutes(Math.Min(FailureBackoffMax.TotalMinutes, minutes));
    }

    private sealed record OpenRouterModelCandidate(string Id, bool IsFree, int Priority, double? SizeBillions);

    private sealed record CatalogSnapshot(IReadOnlyList<string> Models, DateTimeOffset FetchedAt)
    {
        public static readonly CatalogSnapshot Empty = new([], DateTimeOffset.MinValue);

        public bool IsFresh(TimeSpan ttl)
            => Models.Count > 0 && DateTimeOffset.UtcNow - FetchedAt < ttl;
    }
}