using CommonUnderstanding.Data;
using CommonUnderstanding.Models.Social;
using CommonUnderstanding.Services.Social;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services.Social.Workers;

/// <summary>
/// Background service that backfills embeddings for SocialArguments that lack them.
/// Runs once at startup and then periodically to catch new arguments that were saved
/// without embeddings (e.g., when the embedding service was temporarily unavailable).
/// </summary>
public class EmbeddingBackfillWorker : BackgroundService
{
    private readonly SingletonDbContextFactory _dbFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmbeddingBackfillWorker> _logger;

    private static readonly TimeSpan PollingInterval = TimeSpan.FromMinutes(10);

    public EmbeddingBackfillWorker(
        SingletonDbContextFactory dbFactory,
        IServiceScopeFactory scopeFactory,
        ILogger<EmbeddingBackfillWorker> logger)
    {
        _dbFactory = dbFactory;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EmbeddingBackfillWorker starting.");

        // Run initial backfill at startup
        await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                int processed = await BackfillArgumentEmbeddingsAsync(stoppingToken);
                int processedWorldviews = await BackfillWorldviewEmbeddingsAsync(stoppingToken);

                if (processed + processedWorldviews > 0)
                    _logger.LogInformation(
                        "EmbeddingBackfill: processed {Args} arguments, {WVs} worldviews.",
                        processed, processedWorldviews);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EmbeddingBackfillWorker encountered an error.");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private async Task<int> BackfillArgumentEmbeddingsAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var embeddingService = scope.ServiceProvider.GetRequiredService<EmbeddingService>();
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var args = await db.SocialArguments
            .Include(a => a.ClaimProposition)
            .Where(a => a.Embedding == null && a.IsPublic)
            .Take(20)
            .ToListAsync(ct);

        if (args.Count == 0) return 0;

        var texts = args.Select(a => $"{a.ClaimProposition?.Text ?? string.Empty} {a.WarrantText}".Trim()).ToList();
        var embeddings = await embeddingService.GenerateEmbeddingsAsync(texts, ct);

        for (int i = 0; i < args.Count; i++)
        {
            if (embeddings[i] is not null)
                args[i].Embedding = embeddings[i];
        }

        await db.SaveChangesAsync(ct);
        return args.Count;
    }

    private async Task<int> BackfillWorldviewEmbeddingsAsync(CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // Use projection to avoid loading full WorldviewChain and ArgumentChain entities
        var worldviewProjections = await db.Worldviews
            .Where(w => w.Embedding == null)
            .Take(10)
            .Select(w => new
            {
                w.Id,
                ArgumentIds = w.WorldviewChains
                    .Where(wc => wc.ArgumentChain != null)
                    .SelectMany(wc => wc.ArgumentChain.ArgumentIds)
            })
            .ToListAsync(ct);

        if (worldviewProjections.Count == 0) return 0;

        foreach (var wv in worldviewProjections)
        {
            var argIds = wv.ArgumentIds.Distinct().ToList();
            if (argIds.Count == 0) continue;

            var embeddings = await db.SocialArguments
                .AsNoTracking()
                .Where(a => argIds.Contains(a.Id) && a.Embedding != null)
                .Select(a => a.Embedding)
                .ToListAsync(ct);

            var centroid = ScoringAlgorithms.ComputeCentroid(embeddings);
            if (centroid is not null)
            {
                var worldview = await db.Worldviews.FindAsync(new object[] { wv.Id }, ct);
                if (worldview != null)
                    worldview.Embedding = centroid;
            }
        }

        await db.SaveChangesAsync(ct);
        return worldviewProjections.Count;
    }
}
