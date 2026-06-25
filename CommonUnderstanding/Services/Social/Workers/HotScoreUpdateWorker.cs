using CommonUnderstanding.Data;
using CommonUnderstanding.Models.Social;
using CommonUnderstanding.Services.Social;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services.Social.Workers;

/// <summary>
/// Background service that periodically recomputes HotScore for recently active arguments.
/// - Arguments modified in the last 24 hours: updated every 5 minutes.
/// - Older arguments: updated every 60 minutes.
///
/// Hot scores decay over time so this worker must run continuously.
/// </summary>
public class HotScoreUpdateWorker : BackgroundService
{
    private readonly SingletonDbContextFactory _dbFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<HotScoreUpdateWorker> _logger;

    private static readonly TimeSpan RecentInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan OldInterval = TimeSpan.FromMinutes(60);

    public HotScoreUpdateWorker(
        SingletonDbContextFactory dbFactory,
        IConfiguration configuration,
        ILogger<HotScoreUpdateWorker> logger)
    {
        _dbFactory = dbFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HotScoreUpdateWorker starting.");

        // Stagger startup to avoid stampede with other workers
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        var lastOldUpdate = DateTime.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await UpdateRecentArgumentsAsync(stoppingToken);

                if (DateTime.UtcNow - lastOldUpdate > OldInterval)
                {
                    await UpdateOlderArgumentsAsync(stoppingToken);
                    lastOldUpdate = DateTime.UtcNow;
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HotScoreUpdateWorker encountered an error.");
            }

            await Task.Delay(RecentInterval, stoppingToken);
        }

        _logger.LogInformation("HotScoreUpdateWorker stopping.");
    }

    private async Task UpdateRecentArgumentsAsync(CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        double gravity = _configuration.GetValue("Voting:HotScoreGravity", 1.8);
        double maxMultiplier = _configuration.GetValue("Voting:EpistemicMaxMultiplier", 2.0);
        var cutoff = DateTime.UtcNow.AddHours(-24);

        var recentArgs = await db.SocialArguments
            .Where(a => a.IsPublic && !a.IsShadowBanned && a.UpdatedAt >= cutoff)
            .ToListAsync(ct);

        if (recentArgs.Count == 0) return;

        var argIds = recentArgs.Select(a => a.Id).ToList();
        var votesByArg = await db.ArgumentVotes
            .Where(v => argIds.Contains(v.ArgumentId))
            .GroupBy(v => v.ArgumentId)
            .ToDictionaryAsync(g => g.Key, g => g.ToList(), ct);

        foreach (var arg in recentArgs)
        {
            var votes = votesByArg.GetValueOrDefault(arg.Id, new List<ArgumentVote>());
            double weightedUp = ScoringAlgorithms.EpistemicWeightedVoteCount(votes, VoteValue.Up, maxMultiplier);
            double weightedDown = ScoringAlgorithms.EpistemicWeightedVoteCount(votes, VoteValue.Down, maxMultiplier);
            arg.HotScore = ScoringAlgorithms.HotScore(weightedUp, weightedDown, arg.CreatedAt, gravity);
        }

        await db.SaveChangesAsync(ct);
        _logger.LogDebug("Updated hot scores for {Count} recent arguments.", recentArgs.Count);
    }

    private async Task UpdateOlderArgumentsAsync(CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        double gravity = _configuration.GetValue("Voting:HotScoreGravity", 1.8);
        double maxMultiplier = _configuration.GetValue("Voting:EpistemicMaxMultiplier", 2.0);
        var cutoff = DateTime.UtcNow.AddHours(-24);

        // Only update older args that still have non-zero votes to decay
        var oldArgs = await db.SocialArguments
            .Where(a => a.IsPublic && !a.IsShadowBanned
                     && a.UpdatedAt < cutoff
                     && (a.UpvoteCount > 0 || a.DownvoteCount > 0))
            .Take(500) // Batch limit
            .ToListAsync(ct);

        if (oldArgs.Count == 0) return;

        foreach (var arg in oldArgs)
        {
            double weightedUp = arg.UpvoteCount; // Use cached counts for old args (good enough)
            double weightedDown = arg.DownvoteCount;
            arg.HotScore = ScoringAlgorithms.HotScore(weightedUp, weightedDown, arg.CreatedAt, gravity);
        }

        await db.SaveChangesAsync(ct);
        _logger.LogDebug("Updated hot scores for {Count} older arguments.", oldArgs.Count);
    }
}
