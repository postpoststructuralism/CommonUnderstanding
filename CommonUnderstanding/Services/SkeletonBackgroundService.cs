using System.Text.Json;

namespace CommonUnderstanding.Services;

/// <summary>
/// Hosted service that regenerates the static skeleton JSON file on a schedule.
/// Refreshes stale skeletons after user activity has gone quiet. Also supports
/// on-demand regeneration via the controller after graph rebuilds.
/// </summary>
public class SkeletonBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RecentUserActivity _userActivity;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SkeletonBackgroundService> _logger;
    private readonly TimeSpan _refreshInterval;
    private readonly TimeSpan _retryDelay;
    private readonly string _manifestPath;

    public SkeletonBackgroundService(
        IServiceScopeFactory scopeFactory,
        RecentUserActivity userActivity,
        TimeProvider timeProvider,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<SkeletonBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _userActivity = userActivity;
        _timeProvider = timeProvider;
        _logger = logger;
        _refreshInterval = TimeSpan.FromHours(Math.Max(
            1,
            configuration.GetValue("BackgroundWorkers:SkeletonRefreshHours", 24)));
        _retryDelay = TimeSpan.FromMinutes(Math.Max(
            1,
            configuration.GetValue("BackgroundWorkers:SkeletonRetryMinutes", 15)));
        var webRootPath = environment.WebRootPath
            ?? Path.Combine(environment.ContentRootPath, "wwwroot");
        _manifestPath = Path.Combine(webRootPath, "data", "skeleton-manifest.json");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "SkeletonBackgroundService started. Skeletons refresh every {RefreshInterval} after user activity becomes quiet.",
            _refreshInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = _timeProvider.GetUtcNow();
            var nextRun = GetNextRefresh(_manifestPath, now, _refreshInterval);
            var delay = nextRun - now;
            if (delay < TimeSpan.Zero)
                delay = TimeSpan.Zero;

            _logger.LogInformation("Next skeleton regeneration scheduled at {NextRun} (in {Delay}).",
                nextRun, delay);

            try
            {
                await Task.Delay(delay, _timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (_userActivity.ShouldRunBackgroundWork)
            {
                if (!await RegenerateAsync(stoppingToken))
                {
                    await DelayBeforeRetryAsync(stoppingToken);
                }
            }
            else
            {
                _logger.LogInformation(
                    "Skeleton regeneration deferred until user activity becomes quiet. Retrying in {RetryDelay}.",
                    _retryDelay);
                await DelayBeforeRetryAsync(stoppingToken);
            }
        }
    }

    private async Task DelayBeforeRetryAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(_retryDelay, _timeProvider, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Application shutdown ends the worker loop.
        }
    }

    internal static DateTimeOffset GetNextRefresh(
        string manifestPath,
        DateTimeOffset now,
        TimeSpan refreshInterval)
    {
        try
        {
            using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
            if (manifest.RootElement.TryGetProperty("generatedAt", out var generatedAtElement)
                && DateTimeOffset.TryParse(generatedAtElement.GetString(), out var generatedAt))
            {
                var refreshAt = generatedAt.Add(refreshInterval);
                return refreshAt > now ? refreshAt : now;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // A missing or unreadable manifest is stale and should be regenerated.
        }

        return now;
    }

    private async Task<bool> RegenerateAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var generator = scope.ServiceProvider.GetRequiredService<SkeletonGeneratorService>();
            await generator.GenerateAsync(ct);
            _logger.LogInformation("Periodic skeleton regeneration completed.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Periodic skeleton regeneration failed. Retrying in {RetryDelay}.", _retryDelay);
            return false;
        }
    }
}