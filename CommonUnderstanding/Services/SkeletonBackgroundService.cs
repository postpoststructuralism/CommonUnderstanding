namespace CommonUnderstanding.Services;

/// <summary>
/// Hosted service that regenerates the static skeleton JSON file on a schedule.
/// Runs nightly at 3:00 AM UTC by default. Also supports on-demand regeneration
/// via the controller after graph rebuilds.
/// </summary>
public class SkeletonBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SkeletonBackgroundService> _logger;

    public SkeletonBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<SkeletonBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SkeletonBackgroundService started. Will regenerate nightly at 3:00 AM UTC.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            // Schedule next run at 3:00 AM UTC
            var nextRun = now.Date.AddHours(3);
            if (now >= nextRun)
                nextRun = nextRun.AddDays(1);

            var delay = nextRun - now;
            _logger.LogInformation("Next skeleton regeneration scheduled at {NextRun} UTC (in {Delay}).",
                nextRun, delay);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await RegenerateAsync(stoppingToken);
        }
    }

    private async Task RegenerateAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var generator = scope.ServiceProvider.GetRequiredService<SkeletonGeneratorService>();
            await generator.GenerateAsync(ct);
            _logger.LogInformation("Nightly skeleton regeneration completed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Nightly skeleton regeneration failed.");
        }
    }
}