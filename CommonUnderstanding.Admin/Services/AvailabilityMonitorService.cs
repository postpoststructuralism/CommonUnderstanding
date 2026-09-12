using CommonUnderstanding.Admin.Models;
using CommonUnderstanding.Admin.Options;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace CommonUnderstanding.Admin.Services;

public sealed class AvailabilityMonitorService(HttpClient httpClient, IOptions<AzureResourceOptions> options)
{
    private readonly Lock _lock = new();
    private readonly DateTimeOffset _startedAt = DateTimeOffset.UtcNow;
    private int _successes;
    private int _failures;
    private DateTimeOffset? _lastFailureAt;

    public async Task<AvailabilitySnapshot> CheckAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, options.Value.WebAppUrl);
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            stopwatch.Stop();
            var reachable = (int)response.StatusCode < 500;
            Record(reachable);
            return CreateSnapshot(reachable, (int)response.StatusCode, stopwatch.Elapsed.TotalMilliseconds,
                reachable ? null : $"HTTP {(int)response.StatusCode}");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            stopwatch.Stop();
            Record(false);
            return CreateSnapshot(false, 0, stopwatch.Elapsed.TotalMilliseconds, exception.Message);
        }
    }

    private void Record(bool success)
    {
        lock (_lock)
        {
            if (success)
            {
                _successes++;
            }
            else
            {
                _failures++;
                _lastFailureAt = DateTimeOffset.UtcNow;
            }
        }
    }

    private AvailabilitySnapshot CreateSnapshot(bool reachable, int statusCode, double latency, string? error)
    {
        lock (_lock)
        {
            var total = _successes + _failures;
            var percentage = total == 0 ? 0 : _successes * 100d / total;
            return new AvailabilitySnapshot(reachable, statusCode, latency, DateTimeOffset.UtcNow, _startedAt,
                percentage, _successes, _failures, _lastFailureAt, error);
        }
    }
}