namespace CommonUnderstanding.Services.Social.Workers;

/// <summary>
/// Background service that recalculates DMI scores for all users every hour.
/// </summary>
public class DmiScoreWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DmiScoreWorker> _logger;

    public DmiScoreWorker(
        IServiceProvider serviceProvider,
        ILogger<DmiScoreWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DmiScoreWorker started");

        // Delay initial run by 5 minutes to let the app warm up
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dmiService = scope.ServiceProvider.GetRequiredService<DmiScoreService>();
                await dmiService.RecomputeAllAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recomputing DMI scores");
            }

            // Run every hour
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }

        _logger.LogInformation("DmiScoreWorker stopped");
    }
}