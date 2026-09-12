using System.Text.Json;
using CommonUnderstanding.Data;
using CommonUnderstanding.Models.Social;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services.Social;

public sealed class RecommendationProjectionService
{
    private const int FeatureVersion = 1;
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly TimeProvider _timeProvider;

    public RecommendationProjectionService(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        TimeProvider timeProvider)
    {
        _dbFactory = dbFactory;
        _timeProvider = timeProvider;
    }

    public async Task RebuildArgumentAsync(Guid argumentId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var argument = await db.SocialArguments.AsNoTracking()
            .Where(item => item.Id == argumentId)
            .Select(item => new
            {
                item.Id,
                item.CreatedAt,
                item.IsPublic,
                item.IsShadowBanned,
                item.IsAIGenerated,
                item.Tags,
                item.SchwartzValues,
                item.WilsonScore,
                item.HotScore
            })
            .FirstOrDefaultAsync(ct);

        if (argument is null)
        {
            await db.ArgumentRecommendationFeatures.Where(item => item.ArgumentId == argumentId).ExecuteDeleteAsync(ct);
            await db.ArgumentRecommendationTags.Where(item => item.ArgumentId == argumentId).ExecuteDeleteAsync(ct);
            await db.RecommendationArgumentWork.Where(item => item.ArgumentId == argumentId).ExecuteDeleteAsync(ct);
            return;
        }

        var linkCounts = await db.ArgumentLinks.AsNoTracking()
            .Where(link => link.SourceArgumentId == argumentId || link.TargetArgumentId == argumentId)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Total = group.Count(),
                Contradictions = group.Count(link => link.LinkType == LinkType.Contradicts)
            })
            .FirstOrDefaultAsync(ct);
        var positiveVoterBreadth = await db.ArgumentVotes.AsNoTracking()
            .Where(vote => vote.ArgumentId == argumentId && vote.Vote == VoteValue.Up)
            .Select(vote => vote.UserId)
            .Distinct()
            .CountAsync(ct);
        var hasCitedEvidence = await db.SocialArgumentPropositions.AsNoTracking()
            .AnyAsync(item => item.ArgumentId == argumentId
                && item.Role == SocialPropositionType.Evidence
                && item.Proposition.SourceUrl != null
                && item.Proposition.SourceUrl != "", ct);

        var linkCount = linkCounts?.Total ?? 0;
        var contradictionCount = linkCounts?.Contradictions ?? 0;
        var structuralScore = Math.Clamp((linkCount + contradictionCount) / 12d, 0, 1);
        var voterBreadth = Math.Clamp(positiveVoterBreadth / 5d, 0, 1);
        var collectiveScore = Math.Clamp(
            (0.55 * argument.WilsonScore) + (0.25 * structuralScore) + (0.20 * voterBreadth), 0, 1);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var feature = await db.ArgumentRecommendationFeatures.FindAsync([argumentId], ct);
        if (feature is null)
        {
            feature = new ArgumentRecommendationFeature { ArgumentId = argumentId };
            db.ArgumentRecommendationFeatures.Add(feature);
        }

        feature.CreatedAt = argument.CreatedAt;
        feature.IsEligible = argument.IsPublic && !argument.IsShadowBanned;
        feature.IsAIGenerated = argument.IsAIGenerated;
        feature.TagsJson = JsonSerializer.Serialize(argument.Tags);
        feature.SchwartzValuesJson = JsonSerializer.Serialize(argument.SchwartzValues);
        feature.HasCitedEvidence = hasCitedEvidence;
        feature.WilsonScore = argument.WilsonScore;
        feature.HotScore = argument.HotScore;
        feature.PositiveVoterBreadth = positiveVoterBreadth;
        feature.LinkCount = linkCount;
        feature.ContradictionCount = contradictionCount;
        feature.StructuralScore = structuralScore;
        feature.CollectiveScore = collectiveScore;
        feature.FeatureVersion = FeatureVersion;
        feature.ComputedAt = now;

        await db.ArgumentRecommendationTags.Where(item => item.ArgumentId == argumentId).ExecuteDeleteAsync(ct);
        db.ArgumentRecommendationTags.AddRange(argument.Tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim().ToLowerInvariant())
            .Distinct()
            .Select(tag => new ArgumentRecommendationTag { ArgumentId = argumentId, Tag = tag }));
        await db.RecommendationArgumentWork.Where(item => item.ArgumentId == argumentId).ExecuteDeleteAsync(ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task RebuildUserAsync(string userId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var history = await db.ArgumentVotes.AsNoTracking()
            .Where(vote => vote.UserId == userId && vote.Vote == VoteValue.Up)
            .OrderByDescending(vote => vote.CreatedAt)
            .Select(vote => new { vote.Argument.Tags, vote.Argument.SchwartzValues, vote.CreatedAt })
            .Take(200)
            .ToListAsync(ct);
        var expertise = await db.EpistemicProfiles.AsNoTracking()
            .Where(profile => profile.UserId == userId && profile.EpistemicScore >= 2)
            .Select(profile => profile.TopicDomain)
            .ToListAsync(ct);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var affinities = new Dictionary<string, (double Score, DateTime LastSeen)>(StringComparer.OrdinalIgnoreCase);
        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in history)
        {
            var weight = Math.Exp(-Math.Max(0, (now - item.CreatedAt).TotalDays) / 30d);
            foreach (var tag in item.Tags.Where(tag => !string.IsNullOrWhiteSpace(tag)))
            {
                var normalized = tag.Trim().ToLowerInvariant();
                var current = affinities.GetValueOrDefault(normalized);
                affinities[normalized] = (current.Score + weight,
                    current.LastSeen > item.CreatedAt ? current.LastSeen : item.CreatedAt);
            }
            values.UnionWith(item.SchwartzValues);
        }

        var profile = await db.UserRecommendationProfiles.FindAsync([userId], ct);
        if (profile is null)
        {
            profile = new UserRecommendationProfile { UserId = userId };
            db.UserRecommendationProfiles.Add(profile);
        }
        profile.SchwartzValuesJson = JsonSerializer.Serialize(values);
        profile.ExpertiseDomainsJson = JsonSerializer.Serialize(expertise.Distinct(StringComparer.OrdinalIgnoreCase));
        profile.HistoryWatermark = history.Select(item => (DateTime?)item.CreatedAt).Max();
        profile.FeatureVersion = FeatureVersion;
        profile.ComputedAt = now;

        await db.UserRecommendationTopicAffinities.Where(item => item.UserId == userId).ExecuteDeleteAsync(ct);
        db.UserRecommendationTopicAffinities.AddRange(affinities.Select(item => new UserRecommendationTopicAffinity
        {
            UserId = userId,
            Topic = item.Key,
            Affinity = item.Value.Score,
            LastEngagedAt = item.Value.LastSeen,
            ComputedAt = now
        }));
        await db.RecommendationUserWork.Where(item => item.UserId == userId).ExecuteDeleteAsync(ct);
        await db.SaveChangesAsync(ct);
    }
}