using CommonUnderstanding.Data;
using CommonUnderstanding.Models.Social;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services.Social.Workers;

public sealed class RecommendationProjectionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SingletonDbContextFactory _dbFactory;
    private readonly RecentUserActivity _userActivity;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RecommendationProjectionWorker> _logger;

    public RecommendationProjectionWorker(
        IServiceScopeFactory scopeFactory,
        SingletonDbContextFactory dbFactory,
        RecentUserActivity userActivity,
        IConfiguration configuration,
        ILogger<RecommendationProjectionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _dbFactory = dbFactory;
        _userActivity = userActivity;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_userActivity.IsActive)
                    await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Recommendation projection worker failed.");
            }
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        var argumentBatchSize = _configuration.GetValue("Recommendation:ArgumentFeatureBatchSize", 100);
        var userBatchSize = _configuration.GetValue("Recommendation:UserFeatureBatchSize", 50);
        var staleBefore = DateTime.UtcNow.AddMinutes(
            -_configuration.GetValue("Recommendation:ArgumentMaxAgeMinutes", 30));
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var argumentIds = await db.RecommendationArgumentWork.AsNoTracking()
            .Where(item => item.AttemptCount < 5)
            .OrderBy(item => item.RequestedAt)
            .Select(item => item.ArgumentId)
            .Take(argumentBatchSize)
            .ToListAsync(ct);
        if (argumentIds.Count < argumentBatchSize)
        {
            var staleIds = await db.SocialArguments.AsNoTracking()
                .Where(argument => argument.IsPublic && !argument.IsShadowBanned
                    && !argumentIds.Contains(argument.Id)
                    && !db.ArgumentRecommendationFeatures.Any(feature =>
                        feature.ArgumentId == argument.Id && feature.ComputedAt >= staleBefore))
                .OrderByDescending(argument => argument.UpdatedAt)
                .Select(argument => argument.Id)
                .Take(argumentBatchSize - argumentIds.Count)
                .ToListAsync(ct);
            argumentIds.AddRange(staleIds);
        }

        var userIds = await db.RecommendationUserWork.AsNoTracking()
            .Where(item => item.AttemptCount < 5)
            .OrderBy(item => item.RequestedAt)
            .Select(item => item.UserId)
            .Take(userBatchSize)
            .ToListAsync(ct);

        using var scope = _scopeFactory.CreateScope();
        var projector = scope.ServiceProvider.GetRequiredService<RecommendationProjectionService>();
        foreach (var argumentId in argumentIds.Distinct())
            await TryProjectArgumentAsync(projector, argumentId, ct);
        foreach (var userId in userIds.Distinct())
            await TryProjectUserAsync(projector, userId, ct);
    }

    private async Task TryProjectArgumentAsync(
        RecommendationProjectionService projector, Guid argumentId, CancellationToken ct)
    {
        try
        {
            await projector.RebuildArgumentAsync(argumentId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to project recommendation features for argument {ArgumentId}.", argumentId);
            await RecordArgumentFailureAsync(argumentId, ex, ct);
        }
    }

    private async Task TryProjectUserAsync(
        RecommendationProjectionService projector, string userId, CancellationToken ct)
    {
        try
        {
            await projector.RebuildUserAsync(userId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to project recommendation features for user {UserId}.", userId);
            await RecordUserFailureAsync(userId, ex, ct);
        }
    }

    private async Task RecordArgumentFailureAsync(Guid argumentId, Exception ex, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var work = await db.RecommendationArgumentWork.FindAsync([argumentId], ct);
        if (work is null)
        {
            work = new RecommendationArgumentWork { ArgumentId = argumentId, RequestedAt = DateTime.UtcNow };
            db.Add(work);
        }
        work.AttemptCount++;
        work.LastError = ex.Message[..Math.Min(ex.Message.Length, 2000)];
        await db.SaveChangesAsync(ct);
    }

    private async Task RecordUserFailureAsync(string userId, Exception ex, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var work = await db.RecommendationUserWork.FindAsync([userId], ct);
        if (work is null)
        {
            work = new RecommendationUserWork { UserId = userId, RequestedAt = DateTime.UtcNow };
            db.Add(work);
        }
        work.AttemptCount++;
        work.LastError = ex.Message[..Math.Min(ex.Message.Length, 2000)];
        await db.SaveChangesAsync(ct);
    }
}