using CommonUnderstanding.Data;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services.Provenance;

public sealed class EvidenceCorpusWorker : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromHours(6);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RecentUserActivity _userActivity;
    private readonly ILogger<EvidenceCorpusWorker> _logger;

    public EvidenceCorpusWorker(
        IServiceScopeFactory scopeFactory,
        RecentUserActivity userActivity,
        ILogger<EvidenceCorpusWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _userActivity = userActivity;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_userActivity.ShouldRunBackgroundWork)
                    await RefreshAndMatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Evidence corpus refresh failed.");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private async Task RefreshAndMatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var corpusService = scope.ServiceProvider.GetRequiredService<ILiteratureCorpusService>();
        var matchingService = scope.ServiceProvider.GetRequiredService<IEvidenceMatchingService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var refreshed = await corpusService.RefreshAsync(cancellationToken);
        var cutoff = DateTime.UtcNow.AddDays(-7);
        var propositionIds = await db.Propositions
            .AsNoTracking()
            .Where(proposition => proposition.Claim != null
                && proposition.Claim.Argument != null
                && proposition.Claim.Argument.CreatedAt >= cutoff
                && !proposition.EvidenceMatchSuggestions.Any())
            .OrderByDescending(proposition => proposition.Id)
            .Select(proposition => proposition.Id)
            .Take(20)
            .ToListAsync(cancellationToken);

        var suggestions = 0;
        foreach (var propositionId in propositionIds)
            suggestions += await matchingService.SuggestForPropositionAsync(propositionId, cancellationToken);

        if (refreshed > 0 || suggestions > 0)
        {
            _logger.LogInformation(
                "Evidence corpus refreshed {Entries} entries and created {Suggestions} pending suggestions.",
                refreshed,
                suggestions);
        }
    }
}