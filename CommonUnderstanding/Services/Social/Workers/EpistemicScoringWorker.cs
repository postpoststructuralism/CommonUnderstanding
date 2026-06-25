using CommonUnderstanding.Data;
using CommonUnderstanding.Models.Social;
using CommonUnderstanding.Services.Social;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services.Social.Workers;

/// <summary>
/// Background service that recalculates EpistemicScores for users who have had
/// recent vote or contribution activity. Runs every 15 minutes.
///
/// EpistemicScore governs vote weight multipliers in the voting system,
/// so accuracy is important but doesn't need to be real-time.
/// </summary>
public class EpistemicScoringWorker : BackgroundService
{
    private readonly SingletonDbContextFactory _dbFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EpistemicScoringWorker> _logger;

    private static readonly TimeSpan WorkInterval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(15);

    public EpistemicScoringWorker(
        SingletonDbContextFactory dbFactory,
        IServiceScopeFactory scopeFactory,
        ILogger<EpistemicScoringWorker> logger)
    {
        _dbFactory = dbFactory;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EpistemicScoringWorker starting.");

        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessStaleProfilesAsync(stoppingToken);
                await CreateMissingProfilesAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EpistemicScoringWorker encountered an error.");
            }

            await Task.Delay(WorkInterval, stoppingToken);
        }

        _logger.LogInformation("EpistemicScoringWorker stopping.");
    }

    /// <summary>
    /// Updates profiles that are stale (UpdatedAt older than StaleThreshold)
    /// and belong to users who have had recent activity.
    /// </summary>
    private async Task ProcessStaleProfilesAsync(CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var staleProfiles = await db.EpistemicProfiles
            .AsNoTracking()
            .Where(p => p.UpdatedAt < DateTime.UtcNow - StaleThreshold)
            .Take(50) // Process in batches
            .ToListAsync(ct);

        foreach (var profile in staleProfiles)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var scoringService = scope.ServiceProvider.GetRequiredService<EpistemicScoringService>();
                await scoringService.RecalculateAsync(profile.UserId, profile.TopicDomain, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to recalculate epistemic score for {UserId}/{Domain}",
                    profile.UserId, profile.TopicDomain);
            }
        }

        if (staleProfiles.Count > 0)
            _logger.LogDebug("Recalculated epistemic scores for {Count} profiles.", staleProfiles.Count);
    }

    /// <summary>
    /// Creates EpistemicProfile stubs for users who have voted but have no profile yet.
    /// This ensures new users get their vote weight computed on next recalculation.
    /// </summary>
    private async Task CreateMissingProfilesAsync(CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // Find users who have voted in the last 24 hours but may not have a profile
        var recentVoters = await db.ArgumentVotes
            .AsNoTracking()
            .Include(v => v.Argument)
            .Where(v => v.CreatedAt >= DateTime.UtcNow.AddHours(-24))
            .Select(v => new { v.UserId, Domain = v.Argument.Tags.Length > 0 ? v.Argument.Tags[0] : "General" })
            .Distinct()
            .Take(100)
            .ToListAsync(ct);

        foreach (var voter in recentVoters)
        {
            var exists = await db.EpistemicProfiles
                .AnyAsync(p => p.UserId == voter.UserId && p.TopicDomain == voter.Domain, ct);

            if (!exists)
            {
                db.EpistemicProfiles.Add(new EpistemicProfile
                {
                    UserId = voter.UserId,
                    TopicDomain = voter.Domain
                });
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
