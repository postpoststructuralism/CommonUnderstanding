using CommonUnderstanding.Data;
using CommonUnderstanding.Models;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services.Social;

public sealed record CalibrationSummary(int ResolvedPredictionCount, double? BrierScore, double? CalibrationScore)
{
    public static CalibrationSummary Empty { get; } = new(0, null, null);
}

public readonly record struct ResolvedForecast(double Probability, bool ActualOutcome);

public sealed class CalibrationService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public CalibrationService(IDbContextFactory<ApplicationDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<Prediction> CreatePredictionAsync(
        string userId,
        int propositionId,
        Guid? worldviewId,
        double probability,
        DateTime resolutionDate,
        CancellationToken cancellationToken)
    {
        if (probability is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(probability), "Probability must be between 0 and 1.");

        var normalizedResolutionDate = resolutionDate.ToUniversalTime();
        if (normalizedResolutionDate <= DateTime.UtcNow)
            throw new ArgumentOutOfRangeException(nameof(resolutionDate), "Resolution date must be in the future.");

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.Propositions.AnyAsync(x => x.Id == propositionId, cancellationToken))
            throw new KeyNotFoundException("Proposition not found.");

        if (worldviewId.HasValue && !await db.Worldviews.AnyAsync(
                x => x.Id == worldviewId.Value && x.UserId == userId, cancellationToken))
            throw new UnauthorizedAccessException("The worldview must belong to the forecaster.");

        var hasOpenPrediction = await db.Predictions.AnyAsync(
            x => x.UserId == userId
                && x.PropositionId == propositionId
                && x.WorldviewId == worldviewId
                && x.ResolvedAt == null,
            cancellationToken);
        if (hasOpenPrediction)
            throw new InvalidOperationException("An unresolved prediction already exists for this scope.");

        var prediction = new Prediction
        {
            UserId = userId,
            PropositionId = propositionId,
            WorldviewId = worldviewId,
            Probability = probability,
            ResolutionDate = normalizedResolutionDate
        };

        db.Predictions.Add(prediction);
        await db.SaveChangesAsync(cancellationToken);
        return prediction;
    }

    public async Task<bool> ResolvePredictionAsync(
        Guid predictionId,
        bool actualOutcome,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var prediction = await db.Predictions.FirstOrDefaultAsync(x => x.Id == predictionId, cancellationToken);
        if (prediction is null) return false;
        if (prediction.ResolvedAt.HasValue)
            throw new InvalidOperationException("Prediction has already been resolved.");
        if (prediction.ResolutionDate > DateTime.UtcNow)
            throw new InvalidOperationException("Prediction cannot be resolved before its resolution date.");

        prediction.ActualOutcome = actualOutcome;
        prediction.ResolvedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> SetEpistemicStatusAsync(
        int propositionId,
        string userId,
        bool isAdmin,
        EpistemicStatus status,
        CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var proposition = await db.Propositions
            .Include(x => x.Claim)
                .ThenInclude(x => x!.Argument)
            .FirstOrDefaultAsync(x => x.Id == propositionId, cancellationToken);
        if (proposition is null) return false;
        if (!isAdmin && proposition.Claim?.Argument?.SubmittedBy != userId)
            throw new UnauthorizedAccessException("Only the argument owner or an administrator can set this status.");

        proposition.EpistemicStatus = status;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<CalibrationSummary> GetUserSummaryAsync(string userId, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var predictions = await db.Predictions.AsNoTracking()
            .Where(x => x.UserId == userId && x.ActualOutcome != null)
            .Select(x => new ResolvedForecast(x.Probability, x.ActualOutcome!.Value))
            .ToListAsync(cancellationToken);
        return CalculateSummary(predictions);
    }

    public async Task<CalibrationSummary> GetWorldviewSummaryAsync(Guid worldviewId, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var predictions = await db.Predictions.AsNoTracking()
            .Where(x => x.WorldviewId == worldviewId && x.ActualOutcome != null)
            .Select(x => new ResolvedForecast(x.Probability, x.ActualOutcome!.Value))
            .ToListAsync(cancellationToken);
        return CalculateSummary(predictions);
    }

    public static CalibrationSummary CalculateSummary(IEnumerable<ResolvedForecast> predictions)
    {
        var scores = predictions
            .Select(x => Math.Pow(x.Probability - (x.ActualOutcome ? 1.0 : 0.0), 2))
            .ToArray();
        if (scores.Length == 0) return CalibrationSummary.Empty;

        var brierScore = scores.Average();
        return new CalibrationSummary(scores.Length, brierScore, 1.0 - brierScore);
    }
}