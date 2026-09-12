using Azure.Core;
using Azure.Identity;
using CommonUnderstanding.Admin.Models;
using CommonUnderstanding.Admin.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CommonUnderstanding.Admin.Services;

public sealed class EndpointTrafficService(
    HttpClient httpClient,
    TokenCredential credential,
    IMemoryCache cache,
    IOptions<AzureResourceOptions> resources,
    IOptions<DashboardOptions> dashboard,
    ILogger<EndpointTrafficService> logger)
{
    private const string Query = """
        let FilteredRequests = AppServiceHTTPLogs
        | where TimeGenerated >= ago({0}h)
        | where _ResourceId =~ "{1}";
        FilteredRequests
        | extend NormalizedPath = replace_regex(CsUriStem, @"/[0-9a-fA-F]{{8}}-[0-9a-fA-F-]{{27}}", "/{{id}}")
        | extend NormalizedPath = replace_regex(NormalizedPath, @"/\d+", "/{{id}}")
        | summarize Requests=count(), ServerErrors=countif(ScStatus >= 500), AverageDurationMs=avg(TimeTaken) by Method=CsMethod, Path=NormalizedPath
        | top 50 by Requests desc
        | extend CapturedRequests=toscalar(FilteredRequests | count),
                 FirstObservedAt=toscalar(FilteredRequests | summarize min(TimeGenerated)),
                 LastObservedAt=toscalar(FilteredRequests | summarize max(TimeGenerated))
        | project Method, Path, Requests, ServerErrors, AverageDurationMs, CapturedRequests, FirstObservedAt, LastObservedAt
        """;

    public async Task<EndpointTrafficSnapshot> GetAsync(int rangeHours, CancellationToken cancellationToken)
    {
        var cacheKey = $"endpoint-traffic:{rangeHours}";
        if (cache.TryGetValue(cacheKey, out EndpointTrafficSnapshot? cached) && cached is not null)
        {
            return cached;
        }

        if (string.IsNullOrWhiteSpace(resources.Value.LogAnalyticsWorkspaceId))
        {
            return EndpointTrafficSnapshot.Unavailable("Configure the Log Analytics workspace to enable endpoint traffic details.");
        }

        try
        {
            var token = await credential.GetTokenAsync(
                new TokenRequestContext(["https://api.loganalytics.io/.default"]), cancellationToken);
            var query = string.Format(Query, rangeHours, resources.Value.WebAppResourceId);
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"https://api.loganalytics.azure.com/v1/workspaces/{resources.Value.LogAnalyticsWorkspaceId}/query");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
            request.Content = new StringContent(JsonSerializer.Serialize(new { query }), Encoding.UTF8, "application/json");

            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Log Analytics returned {(int)response.StatusCode}: {content}");
            }

            using var document = JsonDocument.Parse(content);
            var tables = document.RootElement.GetProperty("tables");
            var endpoints = tables.GetArrayLength() == 0
                ? []
                : ParseRows(tables[0].GetProperty("rows"));
            var firstRow = tables.GetArrayLength() == 0 || tables[0].GetProperty("rows").GetArrayLength() == 0
                ? (JsonElement?)null
                : tables[0].GetProperty("rows")[0];
            var result = new EndpointTrafficSnapshot(
                true,
                endpoints,
                firstRow?[5].GetInt64() ?? 0,
                ReadTimestamp(firstRow, 6),
                ReadTimestamp(firstRow, 7));
            cache.Set(cacheKey, result, TimeSpan.FromMinutes(Math.Max(1, dashboard.Value.PlatformCacheMinutes)));
            return result;
        }
        catch (Exception exception) when (exception is CredentialUnavailableException or AuthenticationFailedException
            or HttpRequestException or JsonException or InvalidOperationException)
        {
            logger.LogWarning(exception, "Could not load endpoint traffic from Log Analytics");
            return EndpointTrafficSnapshot.Unavailable(
                "Endpoint traffic details are unavailable. HTTP log collection may still be initializing; check local logs for details.");
        }
    }

    private static IReadOnlyList<EndpointRequestCount> ParseRows(JsonElement rows)
    {
        var endpoints = new List<EndpointRequestCount>();
        foreach (var row in rows.EnumerateArray())
        {
            endpoints.Add(new EndpointRequestCount(
                row[0].GetString() ?? "-",
                row[1].GetString() ?? "/",
                row[2].GetInt64(),
                row[3].GetInt64(),
                row[4].ValueKind == JsonValueKind.Number ? row[4].GetDouble() : 0));
        }
        return endpoints;
    }

    private static DateTimeOffset? ReadTimestamp(JsonElement? row, int index) =>
        row is not null && row.Value[index].ValueKind == JsonValueKind.String
            ? DateTimeOffset.Parse(row.Value[index].GetString()!)
            : null;
}