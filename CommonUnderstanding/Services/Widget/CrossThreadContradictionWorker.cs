using CommonUnderstanding.Data;
using CommonUnderstanding.Models.Widget;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services.Widget;

/// <summary>
/// Background worker that detects contradictions between comments across threads
/// on the same publisher site.
/// </summary>
public class CrossThreadContradictionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CrossThreadContradictionWorker> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    public CrossThreadContradictionWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<CrossThreadContradictionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CrossThreadContradictionWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DetectContradictionsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error detecting cross-thread contradictions");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task DetectContradictionsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Get all active sites
        var sites = await db.CommentSites
            .Where(s => s.IsActive)
            .ToListAsync(ct);

        foreach (var site in sites)
        {
            // Get threads with recent activity
            var recentThreads = await db.CommentThreads
                .Where(t => t.SiteId == site.Id && t.TotalComments > 0)
                .OrderByDescending(t => t.UpdatedAt)
                .Take(20)
                .ToListAsync(ct);

            if (recentThreads.Count < 2) continue;

            // For each pair of threads, check for contradictions
            for (int i = 0; i < recentThreads.Count; i++)
            {
                for (int j = i + 1; j < recentThreads.Count; j++)
                {
                    await CheckThreadPairAsync(db, site.Id,
                        recentThreads[i], recentThreads[j], ct);
                }
            }
        }
    }

    private async Task CheckThreadPairAsync(
        ApplicationDbContext db, Guid siteId,
        CommentThread threadA, CommentThread threadB,
        CancellationToken ct)
    {
        // Check if we already have a contradiction for this pair
        var existing = await db.ThreadContradictions
            .AnyAsync(c =>
                (c.ThreadIdA == threadA.Id && c.ThreadIdB == threadB.Id) ||
                (c.ThreadIdA == threadB.Id && c.ThreadIdB == threadA.Id),
                ct);

        if (existing) return;

        // Get arguments from both threads
        var argsA = await db.ThreadArguments
            .Where(ta => ta.ThreadId == threadA.Id)
            .Select(ta => ta.ArgumentId)
            .ToListAsync(ct);

        var argsB = await db.ThreadArguments
            .Where(ta => ta.ThreadId == threadB.Id)
            .Select(ta => ta.ArgumentId)
            .ToListAsync(ct);

        if (argsA.Count == 0 || argsB.Count == 0) return;

        // Simple heuristic: if threads have opposing vote patterns on similar topics,
        // flag as potential contradiction. In production, this would use AI embedding similarity.
        // For now, we create a placeholder that the AI plugin can enrich later.
        var contradiction = new ThreadContradiction
        {
            SiteId = siteId,
            ThreadIdA = threadA.Id,
            ThreadIdB = threadB.Id,
            ArgumentIdA = argsA.First(),
            ArgumentIdB = argsB.First(),
            ContradictionType = "implicit",
            Confidence = 0.3, // Low confidence — AI will re-evaluate
            Explanation = "Potential cross-thread tension detected. AI analysis pending.",
            DetectedAt = DateTime.UtcNow
        };

        db.ThreadContradictions.Add(contradiction);
        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Detected potential contradiction between threads {ThreadA} and {ThreadB} on site {Site}",
            threadA.Id, threadB.Id, siteId);
    }
}