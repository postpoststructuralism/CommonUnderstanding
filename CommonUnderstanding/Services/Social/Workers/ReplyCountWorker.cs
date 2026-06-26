using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CommonUnderstanding.Services.Social.Workers;

/// <summary>
/// Background worker that periodically reconciles reply counts on SocialArguments
/// to ensure they match the actual count of Reply-type ArgumentLinks.
/// Runs every 15 minutes.
/// </summary>
public class ReplyCountWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReplyCountWorker> _logger;

    public ReplyCountWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<ReplyCountWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ReplyCountWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);

                using var scope = _scopeFactory.CreateScope();
                var followUpService = scope.ServiceProvider
                    .GetRequiredService<FollowUpArgumentService>();

                await followUpService.UpdateAllReplyCountsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ReplyCountWorker while reconciling reply counts");
            }
        }

        _logger.LogInformation("ReplyCountWorker stopped");
    }
}