using System.Text.Json;
using System.Globalization;
using CommonUnderstanding.Data;
using CommonUnderstanding.Models;
using CommonUnderstanding.Services.Social;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services.Provenance;

public sealed record LiteratureRecord(
    string Provider,
    string ExternalId,
    string Title,
    string Abstract,
    string? DOI,
    string? Uri,
    int? PublicationYear,
    DateTime PublishedAt,
    string SourceName,
    string? PublisherIdentifier,
    SourceVerificationStatus VerificationStatus)
{
    public string? Authors { get; init; }
    public string? CoverUrl { get; init; }
}

public interface ILiteratureProvider
{
    Task<IReadOnlyList<LiteratureRecord>> SearchAsync(
        string topic,
        DateTime? updatedSince,
        CancellationToken cancellationToken = default);
}

public sealed class OpenLibraryBookSearch(IHttpClientFactory httpClientFactory)
{
    public async Task<IReadOnlyList<LiteratureRecord>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var uri = $"https://openlibrary.org/search.json?q={Uri.EscapeDataString(query)}&fields=key,title,author_name,first_publish_year,cover_i,publisher&limit=8";
        using var response = await httpClientFactory.CreateClient().GetAsync(uri, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("docs", out var docs) || docs.ValueKind != JsonValueKind.Array)
            return [];

        var books = new List<LiteratureRecord>();
        foreach (var book in docs.EnumerateArray())
        {
            var key = book.TryGetProperty("key", out var keyValue) ? keyValue.GetString() : null;
            var title = book.TryGetProperty("title", out var titleValue) ? titleValue.GetString() : null;
            if (string.IsNullOrWhiteSpace(key) || !key.StartsWith("/works/", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(title))
                continue;

            var authors = book.TryGetProperty("author_name", out var authorNames) && authorNames.ValueKind == JsonValueKind.Array
                ? string.Join(", ", authorNames.EnumerateArray().Take(3).Select(author => author.GetString()))
                : null;
            var publisher = book.TryGetProperty("publisher", out var publishers) && publishers.ValueKind == JsonValueKind.Array
                && publishers.GetArrayLength() > 0 ? publishers[0].GetString() : null;
            var year = book.TryGetProperty("first_publish_year", out var yearValue) && yearValue.TryGetInt32(out var parsedYear)
                ? parsedYear : (int?)null;
            var coverId = book.TryGetProperty("cover_i", out var coverValue) && coverValue.TryGetInt32(out var parsedCover)
                && parsedCover > 0 ? parsedCover : (int?)null;

            books.Add(new LiteratureRecord("Open Library", key, title, string.Empty, null,
                $"https://openlibrary.org{key}", year, DateTime.UtcNow,
                publisher ?? "Open Library", null, SourceVerificationStatus.Verified)
            {
                Authors = authors,
                CoverUrl = coverId is null ? null : $"https://covers.openlibrary.org/b/id/{coverId}-M.jpg?default=false"
            });
        }
        return books;
    }
}

public sealed class CrossrefLiteratureProvider : ILiteratureProvider
{
    private const int ResultLimit = 20;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public CrossrefLiteratureProvider(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<IReadOnlyList<LiteratureRecord>> SearchAsync(
        string topic,
        DateTime? updatedSince,
        CancellationToken cancellationToken = default)
    {
        var query = Uri.EscapeDataString(topic);
        var filter = updatedSince.HasValue
            ? $"&filter=from-update-date:{updatedSince.Value:yyyy-MM-dd}"
            : string.Empty;
        var mailto = _configuration["Provenance:CrossrefMailto"];
        var politePool = string.IsNullOrWhiteSpace(mailto)
            ? string.Empty
            : $"&mailto={Uri.EscapeDataString(mailto)}";
        var uri = $"https://api.crossref.org/works?query={query}&rows={ResultLimit}{filter}{politePool}";

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd("CommonUnderstanding/1.0 (literature provenance)");
        using var response = await _httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("message", out var message)
            || !message.TryGetProperty("items", out var items))
            return [];

        var records = new List<LiteratureRecord>();
        foreach (var item in items.EnumerateArray())
        {
            var doi = GetString(item, "DOI");
            var title = GetFirstString(item, "title");
            if (string.IsNullOrWhiteSpace(doi) || string.IsNullOrWhiteSpace(title))
                continue;

            var publishedAt = GetDate(item, "published-print")
                ?? GetDate(item, "published-online")
                ?? GetDate(item, "created")
                ?? DateTime.UtcNow;
            var type = GetString(item, "type");
            var memberId = item.TryGetProperty("member", out var member) && TryGetInt32(member, out var parsedMember)
                ? $"crossref:{parsedMember}"
                : null;

            var authors = item.TryGetProperty("author", out var authorList) && authorList.ValueKind == JsonValueKind.Array
                ? string.Join(", ", authorList.EnumerateArray().Take(3)
                    .Select(author => string.Join(" ", new[] { GetString(author, "given"), GetString(author, "family") }
                        .Where(part => !string.IsNullOrWhiteSpace(part)))))
                : null;
            var isbn = type is "book" or "monograph" or "edited-book" or "reference-book"
                && item.TryGetProperty("ISBN", out var isbnList) && isbnList.ValueKind == JsonValueKind.Array
                ? isbnList.EnumerateArray()
                    .Where(value => value.ValueKind == JsonValueKind.String)
                    .Select(value => value.GetString())
                    .FirstOrDefault(value => value is not null &&
                        ((value.Length == 13 && value.All(char.IsAsciiDigit)) ||
                         (value.Length == 10 && value[..9].All(char.IsAsciiDigit) &&
                          (char.IsAsciiDigit(value[9]) || value[9] == 'X'))))
                : null;

            records.Add(new LiteratureRecord(
                "Crossref",
                doi,
                title,
                GetString(item, "abstract") ?? string.Empty,
                doi,
                GetString(item, "URL") ?? $"https://doi.org/{doi}",
                publishedAt.Year,
                publishedAt,
                GetString(item, "publisher") ?? "Unknown publisher",
                memberId,
                DetermineVerificationStatus(item, type))
            {
                Authors = authors,
                CoverUrl = isbn is null ? null : $"https://covers.openlibrary.org/b/isbn/{isbn}-M.jpg?default=false"
            });
        }

        return records;
    }

    internal static SourceVerificationStatus DetermineVerificationStatus(JsonElement item, string? type)
    {
        if (type?.Contains("retraction", StringComparison.OrdinalIgnoreCase) == true)
            return SourceVerificationStatus.Retracted;
        if (type?.Contains("correction", StringComparison.OrdinalIgnoreCase) == true)
            return SourceVerificationStatus.CorrectionIssued;

        if (item.TryGetProperty("relation", out var relation)
            && relation.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in relation.EnumerateObject())
            {
                if (property.Name.Contains("retract", StringComparison.OrdinalIgnoreCase))
                    return SourceVerificationStatus.Retracted;
                if (property.Name.Contains("correct", StringComparison.OrdinalIgnoreCase)
                    || property.Name.Contains("update", StringComparison.OrdinalIgnoreCase))
                    return SourceVerificationStatus.CorrectionIssued;
            }
        }

        return SourceVerificationStatus.Verified;
    }

    private static string? GetString(JsonElement item, string propertyName) =>
        item.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? GetFirstString(JsonElement item, string propertyName) =>
        item.TryGetProperty(propertyName, out var value)
        && value.ValueKind == JsonValueKind.Array
        && value.GetArrayLength() > 0
        && value[0].ValueKind == JsonValueKind.String
            ? value[0].GetString()
            : null;

    private static bool TryGetInt32(JsonElement value, out int result)
    {
        if (value.ValueKind == JsonValueKind.Number)
            return value.TryGetInt32(out result);

        if (value.ValueKind == JsonValueKind.String)
            return int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

        result = default;
        return false;
    }

    private static DateTime? GetDate(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var date)
            || !date.TryGetProperty("date-parts", out var parts)
            || parts.GetArrayLength() == 0)
            return null;

        var values = parts[0].EnumerateArray().Select(value => value.GetInt32()).ToArray();
        if (values.Length == 0) return null;
        return new DateTime(
            values[0],
            values.Length > 1 ? values[1] : 1,
            values.Length > 2 ? values[2] : 1,
            0, 0, 0,
            DateTimeKind.Utc);
    }
}

public interface ILiteratureCorpusService
{
    Task<int> RefreshAsync(CancellationToken cancellationToken = default);
}

public sealed class LiteratureCorpusService : ILiteratureCorpusService
{
    private readonly ApplicationDbContext _db;
    private readonly IEnumerable<ILiteratureProvider> _providers;
    private readonly ISourceTrustService _sourceTrustService;
    private readonly EmbeddingService _embeddingService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LiteratureCorpusService> _logger;

    public LiteratureCorpusService(
        ApplicationDbContext db,
        IEnumerable<ILiteratureProvider> providers,
        ISourceTrustService sourceTrustService,
        EmbeddingService embeddingService,
        IConfiguration configuration,
        ILogger<LiteratureCorpusService> logger)
    {
        _db = db;
        _providers = providers;
        _sourceTrustService = sourceTrustService;
        _embeddingService = embeddingService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<int> RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!_configuration.GetValue<bool>("Provenance:Enabled")) return 0;

        var topics = _configuration.GetSection("Provenance:TrackedTopics").Get<string[]>() ?? [];
        if (topics.Length == 0) return 0;

        var updatedSince = await _db.EvidenceCorpusEntries
            .MaxAsync(entry => (DateTime?)entry.UpdatedAt, cancellationToken);
        var changed = 0;

        foreach (var provider in _providers)
        {
            foreach (var topic in topics.Where(topic => !string.IsNullOrWhiteSpace(topic)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                IReadOnlyList<LiteratureRecord> records;
                try
                {
                    records = await provider.SearchAsync(topic.Trim(), updatedSince, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Provider {Provider} search failed for topic '{Topic}'.", provider.GetType().Name, topic);
                    continue;
                }

                foreach (var record in records)
                {
                    var source = await _sourceTrustService.ResolveAsync(
                        record.SourceName,
                        null,
                        record.PublisherIdentifier,
                        cancellationToken);
                    if (source is null)
                    {
                        _logger.LogWarning("Source could not be resolved for record {DOI}. SourceName='{SourceName}', PublisherIdentifier='{PublisherIdentifier}'. Skipping.", record.DOI, record.SourceName, record.PublisherIdentifier);
                        continue;
                    }
                    var entry = await _db.EvidenceCorpusEntries.FirstOrDefaultAsync(
                        candidate => candidate.Provider == record.Provider && candidate.ExternalId == record.ExternalId,
                        cancellationToken);

                    if (entry is null)
                    {
                        entry = new EvidenceCorpusEntry
                        {
                            Provider = record.Provider,
                            ExternalId = record.ExternalId
                        };
                        _db.EvidenceCorpusEntries.Add(entry);
                    }

                    entry.SourceId = source.Id;
                    entry.Title = record.Title;
                    entry.Abstract = record.Abstract;
                    entry.DOI = record.DOI;
                    entry.Uri = record.Uri;
                    entry.PublicationYear = record.PublicationYear;
                    entry.PublishedAt = record.PublishedAt;
                    entry.VerificationStatus = record.VerificationStatus;
                    entry.UpdatedAt = DateTime.UtcNow;
                    entry.Embedding = await _embeddingService.GenerateEmbeddingAsync(
                        $"{record.Title}\n{record.Abstract}",
                        cancellationToken);

                    source.LastVerifiedAt = DateTime.UtcNow;
                    changed++;
                }
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        await RefreshSourceHistoriesAsync(cancellationToken);
        return changed;
    }

    private async Task RefreshSourceHistoriesAsync(CancellationToken cancellationToken)
    {
        var histories = await _db.EvidenceCorpusEntries
            .GroupBy(entry => entry.SourceId)
            .Select(group => new
            {
                SourceId = group.Key,
                Retractions = group.Count(entry => entry.VerificationStatus == SourceVerificationStatus.Retracted),
                Corrections = group.Count(entry => entry.VerificationStatus == SourceVerificationStatus.CorrectionIssued)
            })
            .ToListAsync(cancellationToken);

        var sourceIds = histories.Select(history => history.SourceId).ToList();
        var sources = await _db.Sources
            .Where(source => sourceIds.Contains(source.Id))
            .ToDictionaryAsync(source => source.Id, cancellationToken);
        foreach (var history in histories)
        {
            var source = sources[history.SourceId];
            source.RetractionCount = history.Retractions;
            source.CorrectionCount = history.Corrections;
            source.VerificationStatus = SourceVerificationStatus.Verified;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}