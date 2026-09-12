using Azure.Core;
using Azure.Identity;
using CommonUnderstanding.Admin.Models;
using CommonUnderstanding.Admin.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text.Json;

namespace CommonUnderstanding.Admin.Services;

public sealed class AzureMonitorService(
    HttpClient httpClient,
    TokenCredential credential,
    IMemoryCache cache,
    IOptions<AzureResourceOptions> resources,
    IOptions<DashboardOptions> dashboard,
    ILogger<AzureMonitorService> logger)
{
    private static readonly string[] WebMetricNames =
        ["Requests", "AverageResponseTime", "Http5xx", "CpuTime", "MemoryWorkingSet"];
    private static readonly string[] DatabaseMetricNames =
        ["cpu_percent", "physical_data_read_percent", "log_write_percent", "storage_percent", "sessions_percent"];
    private static readonly string[] AiComputeMetricNames =
        ["ModelRequests", "InputTokens", "OutputTokens", "TotalTokens"];

    public async Task<PlatformSnapshot> GetAsync(int rangeHours, CancellationToken cancellationToken)
    {
        var cacheKey = $"platform:{rangeHours}";
        if (cache.TryGetValue(cacheKey, out PlatformSnapshot? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            var token = await credential.GetTokenAsync(
                new TokenRequestContext(["https://management.azure.com/.default"]), cancellationToken);
            var webTask = QueryMetricsAsync(resources.Value.WebAppResourceId, WebMetricNames, rangeHours, token.Token, cancellationToken);
            var databaseTask = QueryMetricsAsync(resources.Value.SqlDatabaseResourceId, DatabaseMetricNames, rangeHours, token.Token, cancellationToken);
            await Task.WhenAll(webTask, databaseTask);

            var web = await webTask;
            var database = await databaseTask;
            var result = new PlatformSnapshot(true,
                Get(web, "Requests", "Requests"),
                Get(web, "AverageResponseTime", "Average response time"),
                Get(web, "Http5xx", "HTTP 5xx"),
                Get(web, "CpuTime", "CPU time"),
                Get(web, "MemoryWorkingSet", "Memory working set"),
                Get(database, "cpu_percent", "Database CPU"),
                Get(database, "physical_data_read_percent", "Database data I/O"),
                Get(database, "log_write_percent", "Database log I/O"),
                Get(database, "storage_percent", "Database storage"),
                Get(database, "sessions_percent", "Database sessions"));

            cache.Set(cacheKey, result, TimeSpan.FromMinutes(Math.Max(1, dashboard.Value.PlatformCacheMinutes)));
            return result;
        }
        catch (Exception exception) when (exception is CredentialUnavailableException or AuthenticationFailedException
            or HttpRequestException or JsonException or InvalidOperationException)
        {
            logger.LogWarning(exception, "Could not load Azure Monitor metrics");
            var message = exception is CredentialUnavailableException or AuthenticationFailedException
                ? "Azure authentication is unavailable. Run az login with an account that can read the configured resources."
                : "Azure Monitor metrics are unavailable. Check the configured resource names and local logs.";
            return PlatformSnapshot.Unavailable(message);
        }
    }

    public async Task<AiComputeSnapshot> GetAiComputeAsync(int rangeHours, CancellationToken cancellationToken)
    {
        var cacheKey = $"ai-compute:{rangeHours}";
        if (cache.TryGetValue(cacheKey, out AiComputeSnapshot? cached) && cached is not null)
        {
            return cached;
        }

        if (string.IsNullOrWhiteSpace(resources.Value.FoundryAccountName))
        {
            return AiComputeSnapshot.Unavailable(
                "Configure the Foundry account to enable AI compute metrics.");
        }

        try
        {
            var token = await credential.GetTokenAsync(
                new TokenRequestContext(["https://management.azure.com/.default"]), cancellationToken);
            var metrics = await QueryMetricsAsync(
                resources.Value.FoundryAccountResourceId,
                AiComputeMetricNames,
                rangeHours,
                token.Token,
                cancellationToken);
            var result = new AiComputeSnapshot(
                true,
                Get(metrics, "ModelRequests", "Model requests"),
                Get(metrics, "InputTokens", "Input tokens"),
                Get(metrics, "OutputTokens", "Output tokens"),
                Get(metrics, "TotalTokens", "Total tokens"));
            cache.Set(cacheKey, result, TimeSpan.FromMinutes(Math.Max(1, dashboard.Value.PlatformCacheMinutes)));
            return result;
        }
        catch (Exception exception) when (exception is CredentialUnavailableException or AuthenticationFailedException
            or HttpRequestException or JsonException or InvalidOperationException)
        {
            logger.LogWarning(exception, "Could not load Foundry AI compute metrics");
            return AiComputeSnapshot.Unavailable(
                "Foundry AI compute metrics are unavailable. Check the configured account and local logs.");
        }
    }

    private async Task<Dictionary<string, MetricSeries>> QueryMetricsAsync(
        string resourceId, IReadOnlyCollection<string> metricNames, int rangeHours, string accessToken,
        CancellationToken cancellationToken, string? filter = null)
    {
        var end = DateTimeOffset.UtcNow;
        var start = end.AddHours(-rangeHours);
        var interval = rangeHours <= 24 ? "PT5M" : rangeHours <= 168 ? "PT1H" : "PT6H";
        var timespan = $"{start.UtcDateTime:yyyy-MM-ddTHH:mm:ss.fffZ}/{end.UtcDateTime:yyyy-MM-ddTHH:mm:ss.fffZ}";
        var uri = $"https://management.azure.com{resourceId}/providers/microsoft.insights/metrics" +
                  $"?api-version=2023-10-01&metricnames={Uri.EscapeDataString(string.Join(',', metricNames))}" +
              $"&timespan={Uri.EscapeDataString(timespan)}&interval={interval}&aggregation=Average,Total,Maximum" +
              (string.IsNullOrWhiteSpace(filter) ? string.Empty : $"&$filter={Uri.EscapeDataString(filter)}");

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Azure Monitor returned {(int)response.StatusCode}: {content}");
        }

        using var document = JsonDocument.Parse(content);
        var result = new Dictionary<string, MetricSeries>(StringComparer.OrdinalIgnoreCase);
        foreach (var metric in document.RootElement.GetProperty("value").EnumerateArray())
        {
            var name = metric.GetProperty("name").GetProperty("value").GetString() ?? "Unknown";
            var unit = metric.TryGetProperty("unit", out var unitElement) ? unitElement.GetString() ?? string.Empty : string.Empty;
            var points = new List<MetricPoint>();
            var totals = new List<double>();
            var averages = new List<double>();
            var maxima = new List<double>();

            foreach (var timeSeries in metric.GetProperty("timeseries").EnumerateArray())
            {
                foreach (var data in timeSeries.GetProperty("data").EnumerateArray())
                {
                    if (!data.TryGetProperty("timeStamp", out var timestampElement))
                    {
                        continue;
                    }

                    var timestamp = DateTimeOffset.Parse(timestampElement.GetString()!, CultureInfo.InvariantCulture);
                    var value = ReadNumber(data, "total") ?? ReadNumber(data, "average") ?? ReadNumber(data, "maximum");
                    if (value is not null)
                    {
                        points.Add(new MetricPoint(timestamp, value.Value));
                    }
                    AddIfPresent(data, "total", totals);
                    AddIfPresent(data, "average", averages);
                    AddIfPresent(data, "maximum", maxima);
                }
            }

            result[name] = new MetricSeries(name, unit, totals.Sum(), averages.Count == 0 ? 0 : averages.Average(),
                maxima.Count == 0 ? 0 : maxima.Max(), points);
        }
        return result;
    }

    private static MetricSeries Get(IReadOnlyDictionary<string, MetricSeries> metrics, string key, string label) =>
        metrics.TryGetValue(key, out var metric) ? metric with { Name = label } : MetricSeries.Empty(label);

    private static double? ReadNumber(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number
            ? property.GetDouble()
            : null;

    private static void AddIfPresent(JsonElement element, string propertyName, ICollection<double> values)
    {
        var value = ReadNumber(element, propertyName);
        if (value is not null)
        {
            values.Add(value.Value);
        }
    }
}