using System.Text.Json;
using CommonUnderstanding.Data;
using CommonUnderstanding.Models.Social;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services.Social;

public sealed class AdversarialIntegrityService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public AdversarialIntegrityService(IDbContextFactory<ApplicationDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<IntegrityRiskAssessment> AssessUserAsync(
        string userId,
        string? pendingVoteTargetUserId = null,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var now = DateTime.UtcNow;
        var oneHourAgo = now.AddHours(-1);
        var account = await db.UserAccounts.AsNoTracking().SingleOrDefaultAsync(user => user.Id == userId, ct);
        if (account is null)
            return AdversarialIntegrityPolicy.Assess(new BehavioralIntegritySignals(0, 0, 0, 0, 0));

        int postsLastHour = await db.SocialArguments.AsNoTracking()
            .CountAsync(argument => argument.UserId == userId && argument.CreatedAt >= oneHourAgo, ct);

        var recentVoteTargets = await db.ArgumentVotes.AsNoTracking()
            .Where(vote => vote.UserId == userId && vote.CreatedAt >= oneHourAgo)
            .Select(vote => vote.Argument.UserId)
            .ToListAsync(ct);

        if (!string.IsNullOrWhiteSpace(pendingVoteTargetUserId))
            recentVoteTargets.Add(pendingVoteTargetUserId);

        double targetConcentration = recentVoteTargets.Count == 0
            ? 0
            : recentVoteTargets.GroupBy(targetUserId => targetUserId).Max(group => group.Count())
                / (double)recentVoteTargets.Count;

        var latestUserEmbedding = await db.SocialArguments.AsNoTracking()
            .Where(argument => argument.UserId == userId && argument.Embedding != null)
            .OrderByDescending(argument => argument.CreatedAt)
            .Select(argument => argument.Embedding)
            .FirstOrDefaultAsync(ct);

        double crossAccountSimilarity = 0;
        if (latestUserEmbedding is not null)
        {
            var comparisonEmbeddings = await db.SocialArguments.AsNoTracking()
                .Where(argument => argument.UserId != userId && argument.Embedding != null)
                .OrderByDescending(argument => argument.CreatedAt)
                .Take(200)
                .Select(argument => argument.Embedding!)
                .ToListAsync(ct);

            crossAccountSimilarity = comparisonEmbeddings.Count == 0
                ? 0
                : comparisonEmbeddings.Max(embedding =>
                    ScoringAlgorithms.CosineSimilarity(latestUserEmbedding, embedding));
        }

        var assessment = AdversarialIntegrityPolicy.Assess(new BehavioralIntegritySignals(
            AccountAgeDays: Math.Max(0, (int)(now - account.CreatedAt).TotalDays),
            PostsLastHour: postsLastHour,
            CrossAccountArgumentSimilarity: crossAccountSimilarity,
            VotesLastHour: recentVoteTargets.Count,
            VoteTargetConcentration: targetConcentration));

        var persisted = await db.UserIntegrityAssessments.SingleOrDefaultAsync(item => item.UserId == userId, ct);
        if (persisted is null)
        {
            persisted = new UserIntegrityAssessment { UserId = userId };
            db.UserIntegrityAssessments.Add(persisted);
        }

        persisted.RiskScore = assessment.RiskScore;
        persisted.InfluenceMultiplier = assessment.InfluenceMultiplier;
        persisted.SignalsJson = JsonSerializer.Serialize(assessment.Signals);
        persisted.LastAssessedAt = now;
        await db.SaveChangesAsync(ct);

        return assessment;
    }
}