using CommonUnderstanding.Data;
using CommonUnderstanding.Models.Social;
using CommonUnderstanding.Services.Social.Workers;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CommonUnderstanding.Services.Social;

public interface IFeedRankingService
{
    Task<RecommendedFeedResultDto> GetFeedAsync(string? userId, int offset, int limit, string[]? tags, CancellationToken ct);
    Task<UserFeedPreferencesDto> GetPreferencesAsync(string userId, CancellationToken ct);
    Task<UserFeedPreferencesDto> SavePreferencesAsync(string userId, UpdateFeedPreferencesDto update, CancellationToken ct);
    Task RecordEngagementAsync(string userId, FeedEngagementDto engagement, CancellationToken ct);
}

public sealed class FeedRankingService : IFeedRankingService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly TimeProvider _timeProvider;
    private readonly FeedImpressionWriter _impressionWriter;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FeedRankingService> _logger;

    public FeedRankingService(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        TimeProvider timeProvider,
        FeedImpressionWriter impressionWriter,
        IConfiguration configuration,
        ILogger<FeedRankingService> logger)
    {
        _dbFactory = dbFactory;
        _timeProvider = timeProvider;
        _impressionWriter = impressionWriter;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<RecommendedFeedResultDto> GetFeedAsync(
        string? userId, int offset, int limit, string[]? tags, CancellationToken ct)
    {
        if (!_configuration.GetValue("Recommendation:UseIndexedServing", false))
            return await GetLegacyFeedAsync(userId, offset, limit, tags, ct);

        try
        {
            return await GetIndexedFeedAsync(userId, offset, limit, tags, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Indexed recommendation serving failed; using the legacy feed path.");
            return await GetLegacyFeedAsync(userId, offset, limit, tags, ct);
        }
    }

    private async Task<RecommendedFeedResultDto> GetLegacyFeedAsync(
        string? userId, int offset, int limit, string[]? tags, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var preferences = userId is null ? UserFeedPreferencesDto.Default : await LoadPreferencesAsync(db, userId, ct);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var query = db.SocialArguments.AsNoTracking()
            .Include(argument => argument.ClaimProposition)
            .Where(argument => argument.IsPublic && !argument.IsShadowBanned && argument.CreatedAt >= now.AddDays(-90));

        if (tags is { Length: > 0 })
            query = query.Where(argument => argument.Tags.Any(tag => tags.Contains(tag)));
        if (!preferences.IncludeAIGenerated)
            query = query.Where(argument => !argument.IsAIGenerated);

        var candidates = await query.OrderByDescending(argument => argument.CreatedAt).Take(200).ToListAsync(ct);
        if (candidates.Count == 0)
            return new RecommendedFeedResultDto(preferences, []);

        var candidateIds = candidates.Select(argument => argument.Id).ToList();
        var links = await db.ArgumentLinks.AsNoTracking()
            .Where(link => candidateIds.Contains(link.SourceArgumentId) || candidateIds.Contains(link.TargetArgumentId))
            .Select(link => new LinkSignal(link.SourceArgumentId, link.TargetArgumentId, link.LinkType))
            .ToListAsync(ct);
        var positiveVoterCounts = await db.ArgumentVotes.AsNoTracking()
            .Where(vote => candidateIds.Contains(vote.ArgumentId) && vote.Vote == VoteValue.Up)
            .GroupBy(vote => vote.ArgumentId)
            .Select(group => new { ArgumentId = group.Key, Count = group.Select(vote => vote.UserId).Distinct().Count() })
            .ToDictionaryAsync(item => item.ArgumentId, item => item.Count, ct);
        var argumentsWithEvidence = await db.SocialArgumentPropositions.AsNoTracking()
            .Where(item => candidateIds.Contains(item.ArgumentId)
                && item.Role == SocialPropositionType.Evidence
                && item.Proposition.SourceUrl != null
                && item.Proposition.SourceUrl != "")
            .Select(item => item.ArgumentId)
            .Distinct()
            .ToHashSetAsync(ct);

        var engagedTags = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var engagedValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var expertiseDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var recentlySeen = new HashSet<Guid>();
        if (userId is not null)
        {
            var history = await db.ArgumentVotes.AsNoTracking()
                .Where(vote => vote.UserId == userId && vote.Vote == VoteValue.Up)
                .OrderByDescending(vote => vote.CreatedAt)
                .Select(vote => new { vote.Argument.Tags, vote.Argument.SchwartzValues, vote.CreatedAt })
                .Take(100).ToListAsync(ct);
            foreach (var item in history)
            {
                var decayDays = 7 + (preferences.RecencyBias * 83);
                var weight = Math.Exp(-Math.Max(0, (now - item.CreatedAt).TotalDays) / decayDays);
                foreach (var tag in item.Tags)
                    engagedTags[tag] = engagedTags.GetValueOrDefault(tag) + weight;
                foreach (var value in item.SchwartzValues)
                    engagedValues.Add(value);
            }

            expertiseDomains.UnionWith(await db.EpistemicProfiles.AsNoTracking()
                .Where(profile => profile.UserId == userId && profile.EpistemicScore >= 2)
                .Select(profile => profile.TopicDomain).ToListAsync(ct));
            recentlySeen.UnionWith(await db.FeedImpressionEvents.AsNoTracking()
                .Where(impression => impression.UserId == userId && impression.ServedAt >= now.AddHours(-12))
                .Select(impression => impression.ArgumentId).Distinct().ToListAsync(ct));
        }

        var maxAffinity = Math.Max(1, engagedTags.Values.DefaultIfEmpty(0).Max());
        var maxLinks = Math.Max(1, candidates.Max(argument => CountLinks(argument.Id, links)));
        var daySeed = DateOnly.FromDateTime(now).DayNumber;
        var scored = candidates.Select(argument =>
        {
            var interest = ScoreInterest(argument.Tags, engagedTags, maxAffinity);
            var growth = ScoreGrowth(argument.Tags, engagedTags.Keys, expertiseDomains);
            var collective = ScoreCollective(argument.WilsonScore, argument.Id, links, positiveVoterCounts, maxLinks);
            if (preferences.EvidencePreferred && argumentsWithEvidence.Contains(argument.Id))
                collective = Math.Min(1, collective + 0.15);
            var explore = StableUnit(userId, argument.Id, daySeed) < preferences.AdventureRate;
            var challenge = preferences.ChallengeRate > 0 && argument.SchwartzValues.Length > 0
                && !argument.SchwartzValues.Any(engagedValues.Contains)
                && StableUnit(userId, argument.Id, daySeed + 17) < preferences.ChallengeRate;
            var score = Blend(preferences, interest, growth, collective)
                + (explore ? 0.35 * StableUnit(userId, argument.Id, daySeed + 31) : 0)
                + (challenge ? 0.15 : 0);
            return new ScoredCandidate(argument, interest, growth, collective, score, explore, challenge);
        }).ToList();

        var unseen = scored.Where(candidate => !recentlySeen.Contains(candidate.Argument.Id)).ToList();
        if (unseen.Count >= Math.Min(limit, candidates.Count))
            scored = unseen;

        var page = DiversityRerank(scored, Math.Max(0, offset) + Math.Clamp(limit, 1, 50))
            .Skip(Math.Max(0, offset)).Take(Math.Clamp(limit, 1, 50)).ToList();
        if (userId is not null && page.Count > 0)
        {
            var pageIds = page.Select(candidate => candidate.Argument.Id).ToList();
            var userVotes = await db.ArgumentVotes.AsNoTracking()
                .Where(vote => vote.UserId == userId && pageIds.Contains(vote.ArgumentId))
                .ToListAsync(ct);
            var votesByArgument = userVotes.ToDictionary(vote => vote.ArgumentId);
            foreach (var candidate in page)
            {
                if (votesByArgument.TryGetValue(candidate.Argument.Id, out var vote))
                    candidate.Argument.Votes.Add(vote);
            }
        }
        var items = page.Select(candidate => new RecommendedFeedItemDto(
            FeedService.MapArgumentToFeedItem(candidate.Argument, userId),
            candidate.Interest, candidate.Growth, candidate.Collective, candidate.Score,
            GetLane(candidate), GetReason(candidate, engagedTags, expertiseDomains))).ToList();

        EnqueueImpressions(userId, items, now);

        return new RecommendedFeedResultDto(preferences, items);
    }

    private async Task<RecommendedFeedResultDto> GetIndexedFeedAsync(
        string? userId, int offset, int limit, string[]? tags, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var preferences = userId is null ? UserFeedPreferencesDto.Default : await LoadPreferencesAsync(db, userId, ct);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var requestedCount = Math.Max(0, offset) + Math.Clamp(limit, 1, 50);
        var poolLimit = Math.Max(1, _configuration.GetValue("Recommendation:CandidatePoolLimit", 100));

        var engagedTags = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var engagedValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var expertiseDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var recentlySeen = new HashSet<Guid>();
        if (userId is not null)
        {
            var affinities = await db.UserRecommendationTopicAffinities.AsNoTracking()
                .Where(item => item.UserId == userId)
                .OrderByDescending(item => item.Affinity)
                .Take(50)
                .Select(item => new { item.Topic, item.Affinity })
                .ToListAsync(ct);
            foreach (var affinity in affinities)
                engagedTags[affinity.Topic] = affinity.Affinity;

            var profile = await db.UserRecommendationProfiles.AsNoTracking()
                .Where(item => item.UserId == userId)
                .Select(item => new { item.SchwartzValuesJson, item.ExpertiseDomainsJson })
                .FirstOrDefaultAsync(ct);
            if (profile is not null)
            {
                engagedValues.UnionWith(DeserializeStrings(profile.SchwartzValuesJson));
                expertiseDomains.UnionWith(DeserializeStrings(profile.ExpertiseDomainsJson));
            }

            recentlySeen.UnionWith(await db.FeedImpressionEvents.AsNoTracking()
                .Where(impression => impression.UserId == userId && impression.ServedAt >= now.AddHours(-12))
                .OrderByDescending(impression => impression.ServedAt)
                .Select(impression => impression.ArgumentId)
                .Distinct()
                .Take(500)
                .ToListAsync(ct));
        }

        var normalizedTags = (tags ?? [])
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim().ToLowerInvariant())
            .Distinct()
            .ToArray();
        IQueryable<ArgumentRecommendationFeature> eligible = db.ArgumentRecommendationFeatures.AsNoTracking()
            .Where(feature => feature.IsEligible && feature.CreatedAt >= now.AddDays(-90));
        if (!preferences.IncludeAIGenerated)
            eligible = eligible.Where(feature => !feature.IsAIGenerated);
        if (normalizedTags.Length > 0)
        {
            var matchingIds = db.ArgumentRecommendationTags.AsNoTracking()
                .Where(item => normalizedTags.Contains(item.Tag))
                .Select(item => item.ArgumentId);
            eligible = eligible.Where(feature => matchingIds.Contains(feature.ArgumentId));
        }

        var candidatePools = new List<IReadOnlyCollection<Guid>>();
        var topTopics = engagedTags.OrderByDescending(item => item.Value).Take(12).Select(item => item.Key).ToArray();
        if (topTopics.Length > 0)
        {
            candidatePools.Add(await db.ArgumentRecommendationTags.AsNoTracking()
                .Where(item => topTopics.Contains(item.Tag))
                .Join(eligible, tag => tag.ArgumentId, feature => feature.ArgumentId, (_, feature) => feature)
                .OrderByDescending(feature => feature.CreatedAt)
                .Select(feature => feature.ArgumentId)
                .Distinct()
                .Take(60)
                .ToListAsync(ct));
        }

        var growthTopics = expertiseDomains.Take(12).ToArray();
        if (growthTopics.Length > 0)
        {
            candidatePools.Add(await db.ArgumentRecommendationTags.AsNoTracking()
                .Where(item => growthTopics.Contains(item.Tag))
                .Join(eligible, tag => tag.ArgumentId, feature => feature.ArgumentId, (_, feature) => feature)
                .OrderByDescending(feature => feature.CreatedAt)
                .Select(feature => feature.ArgumentId)
                .Distinct()
                .Take(40)
                .ToListAsync(ct));
        }

        candidatePools.Add(await eligible.OrderByDescending(feature => feature.CollectiveScore)
            .Select(feature => feature.ArgumentId).Take(40).ToListAsync(ct));
        candidatePools.Add(await eligible.OrderByDescending(feature => feature.HotScore)
            .Select(feature => feature.ArgumentId).Take(20).ToListAsync(ct));
        candidatePools.Add(await eligible.OrderByDescending(feature => feature.CreatedAt)
            .Select(feature => feature.ArgumentId).Take(30).ToListAsync(ct));

        var boundedIds = MergeCandidateIds(candidatePools, poolLimit);
        if (boundedIds.Count == 0)
        {
            _logger.LogWarning("Indexed recommendation serving found no projected candidates; using the legacy feed path.");
            return await GetLegacyFeedAsync(userId, offset, limit, tags, ct);
        }

        var features = await eligible.Where(feature => boundedIds.Contains(feature.ArgumentId))
            .Take(poolLimit)
            .ToListAsync(ct);
        var maxAffinity = Math.Max(1, engagedTags.Values.DefaultIfEmpty(0).Max());
        var daySeed = DateOnly.FromDateTime(now).DayNumber;
        var scored = features.Select(feature =>
        {
            var argument = new SocialArgument
            {
                Id = feature.ArgumentId,
                CreatedAt = feature.CreatedAt,
                Tags = DeserializeStrings(feature.TagsJson),
                SchwartzValues = DeserializeStrings(feature.SchwartzValuesJson)
            };
            var interest = ScoreInterest(argument.Tags, engagedTags, maxAffinity);
            var growth = ScoreGrowth(argument.Tags, engagedTags.Keys, expertiseDomains);
            var collective = feature.CollectiveScore;
            if (preferences.EvidencePreferred && feature.HasCitedEvidence)
                collective = Math.Min(1, collective + 0.15);
            var explore = StableUnit(userId, argument.Id, daySeed) < preferences.AdventureRate;
            var challenge = preferences.ChallengeRate > 0 && argument.SchwartzValues.Length > 0
                && !argument.SchwartzValues.Any(engagedValues.Contains)
                && StableUnit(userId, argument.Id, daySeed + 17) < preferences.ChallengeRate;
            var score = Blend(preferences, interest, growth, collective)
                + (explore ? 0.35 * StableUnit(userId, argument.Id, daySeed + 31) : 0)
                + (challenge ? 0.15 : 0);
            return new ScoredCandidate(argument, interest, growth, collective, score, explore, challenge);
        }).ToList();

        var unseen = scored.Where(candidate => !recentlySeen.Contains(candidate.Argument.Id)).ToList();
        if (unseen.Count >= Math.Min(limit, scored.Count))
            scored = unseen;
        var page = DiversityRerank(scored, requestedCount)
            .Skip(Math.Max(0, offset))
            .Take(Math.Clamp(limit, 1, 50))
            .ToList();
        return await HydrateIndexedPageAsync(db, userId, preferences, page, engagedTags,
            expertiseDomains, now, stopwatch, ct);
    }

    private async Task<RecommendedFeedResultDto> HydrateIndexedPageAsync(
        ApplicationDbContext db,
        string? userId,
        UserFeedPreferencesDto preferences,
        List<ScoredCandidate> page,
        IReadOnlyDictionary<string, double> engagedTags,
        IReadOnlySet<string> expertiseDomains,
        DateTime now,
        Stopwatch stopwatch,
        CancellationToken ct)
    {
        if (page.Count == 0)
            return new RecommendedFeedResultDto(preferences, []);

        var pageIds = page.Select(candidate => candidate.Argument.Id).ToList();
        var arguments = await db.SocialArguments.AsNoTracking()
            .Include(argument => argument.ClaimProposition)
            .Where(argument => pageIds.Contains(argument.Id))
            .ToDictionaryAsync(argument => argument.Id, ct);
        if (userId is not null)
        {
            var votes = await db.ArgumentVotes.AsNoTracking()
                .Where(vote => vote.UserId == userId && pageIds.Contains(vote.ArgumentId))
                .ToListAsync(ct);
            foreach (var vote in votes)
                if (arguments.TryGetValue(vote.ArgumentId, out var argument))
                    argument.Votes.Add(vote);
        }

        var hydrated = page
            .Where(candidate => arguments.ContainsKey(candidate.Argument.Id))
            .Select(candidate => candidate with { Argument = arguments[candidate.Argument.Id] })
            .ToList();
        var items = hydrated.Select(candidate => new RecommendedFeedItemDto(
            FeedService.MapArgumentToFeedItem(candidate.Argument, userId),
            candidate.Interest, candidate.Growth, candidate.Collective, candidate.Score,
            GetLane(candidate), GetReason(candidate, engagedTags, expertiseDomains))).ToList();
        EnqueueImpressions(userId, items, now);
        _logger.LogInformation(
            "Indexed recommendation feed served {ItemCount} items from {CandidateCount} candidates in {ElapsedMs} ms.",
            items.Count, page.Count, stopwatch.ElapsedMilliseconds);
        return new RecommendedFeedResultDto(preferences, items);
    }

    private void EnqueueImpressions(string? userId, IReadOnlyCollection<RecommendedFeedItemDto> items, DateTime servedAt)
    {
        if (userId is null || items.Count == 0)
            return;

        _impressionWriter.TryEnqueue(items.Select(item => new FeedImpressionEvent
        {
            UserId = userId,
            ArgumentId = item.Argument.Id,
            InterestScoreAtServe = item.InterestScore,
            GrowthScoreAtServe = item.GrowthScore,
            CollectiveScoreAtServe = item.CollectiveScore,
            BlendedScoreAtServe = item.RecommendationScore,
            Lane = item.Lane,
            ServedAt = servedAt
        }));
    }

    private static string[] DeserializeStrings(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public async Task<UserFeedPreferencesDto> GetPreferencesAsync(string userId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await LoadPreferencesAsync(db, userId, ct);
    }

    public async Task<UserFeedPreferencesDto> SavePreferencesAsync(string userId, UpdateFeedPreferencesDto update, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var preferences = await db.UserFeedPreferences.FindAsync([userId], ct);
        if (preferences is null)
        {
            preferences = new UserFeedPreferences { UserId = userId };
            db.UserFeedPreferences.Add(preferences);
        }

        var total = update.InterestWeight + update.GrowthWeight + update.CollectiveWeight;
        preferences.InterestWeight = update.InterestWeight / total;
        preferences.GrowthWeight = update.GrowthWeight / total;
        preferences.CollectiveWeight = update.CollectiveWeight / total;
        preferences.AdventureRate = update.AdventureRate;
        preferences.ChallengeRate = update.ChallengeRate;
        preferences.RecencyBias = update.RecencyBias;
        preferences.IncludeAIGenerated = update.IncludeAIGenerated;
        preferences.EvidencePreferred = update.EvidencePreferred;
        preferences.ActivePresetName = update.ActivePresetName.Trim();
        preferences.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        return MapPreferences(preferences);
    }

    public async Task RecordEngagementAsync(string userId, FeedEngagementDto engagement, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var impression = await db.FeedImpressionEvents
            .Where(item => item.UserId == userId && item.ArgumentId == engagement.ArgumentId)
            .OrderByDescending(item => item.ServedAt).FirstOrDefaultAsync(ct);
        if (impression is null) return;
        impression.Clicked |= engagement.Clicked;
        impression.Voted |= engagement.Voted;
        impression.Commented |= engagement.Commented;
        impression.DwellMs = Math.Max(impression.DwellMs, engagement.DwellMs);
        await db.SaveChangesAsync(ct);
    }

    private static async Task<UserFeedPreferencesDto> LoadPreferencesAsync(ApplicationDbContext db, string userId, CancellationToken ct)
    {
        var preferences = await db.UserFeedPreferences.AsNoTracking().FirstOrDefaultAsync(item => item.UserId == userId, ct);
        return preferences is null ? UserFeedPreferencesDto.Default : MapPreferences(preferences);
    }

    private static UserFeedPreferencesDto MapPreferences(UserFeedPreferences preferences) => new(
        preferences.InterestWeight, preferences.GrowthWeight, preferences.CollectiveWeight,
        preferences.AdventureRate, preferences.ChallengeRate, preferences.RecencyBias,
        preferences.IncludeAIGenerated, preferences.EvidencePreferred, preferences.ActivePresetName);

    internal static double ScoreInterest(string[] tags, IReadOnlyDictionary<string, double> affinities, double maxAffinity)
    {
        if (tags.Length == 0 || affinities.Count == 0) return 0.2;
        return Math.Clamp(tags.Sum(tag => affinities.GetValueOrDefault(tag)) / (tags.Length * maxAffinity), 0, 1);
    }

    internal static double ScoreGrowth(string[] tags, IEnumerable<string> engagedTags, IReadOnlySet<string> expertiseDomains)
    {
        var known = new HashSet<string>(engagedTags, StringComparer.OrdinalIgnoreCase);
        var overlap = tags.Count(known.Contains);
        var expertiseOverlap = tags.Count(expertiseDomains.Contains);
        var novelty = tags.Length == 0 ? 0.4 : 1d - ((double)overlap / tags.Length);
        var adjacent = overlap > 0 || expertiseOverlap > 0 ? 1d : 0.35;
        return Math.Clamp((0.65 * novelty) + (0.35 * adjacent), 0, 1);
    }

    internal static double Blend(UserFeedPreferencesDto preferences, double interest, double growth, double collective) =>
        (preferences.InterestWeight * interest) + (preferences.GrowthWeight * growth) + (preferences.CollectiveWeight * collective);

    internal static List<Guid> MergeCandidateIds(IEnumerable<IReadOnlyCollection<Guid>> pools, int limit)
    {
        var merged = new List<Guid>(Math.Max(0, limit));
        var seen = new HashSet<Guid>();
        foreach (var pool in pools)
        {
            foreach (var argumentId in pool)
            {
                if (seen.Add(argumentId))
                    merged.Add(argumentId);
                if (merged.Count >= limit)
                    return merged;
            }
        }
        return merged;
    }

    private static double ScoreCollective(double wilsonScore, Guid argumentId, IReadOnlyCollection<LinkSignal> links,
        IReadOnlyDictionary<Guid, int> positiveVoterCounts, int maxLinks)
    {
        var linkCount = CountLinks(argumentId, links);
        var contradictions = links.Count(link => (link.SourceArgumentId == argumentId || link.TargetArgumentId == argumentId)
            && link.LinkType == LinkType.Contradicts);
        var structural = Math.Clamp((linkCount + contradictions) / (double)(maxLinks + 1), 0, 1);
        var voterBreadth = Math.Clamp(positiveVoterCounts.GetValueOrDefault(argumentId) / 5d, 0, 1);
        return Math.Clamp((0.55 * wilsonScore) + (0.25 * structural) + (0.20 * voterBreadth), 0, 1);
    }

    private static int CountLinks(Guid argumentId, IReadOnlyCollection<LinkSignal> links) =>
        links.Count(link => link.SourceArgumentId == argumentId || link.TargetArgumentId == argumentId);

    private static List<ScoredCandidate> DiversityRerank(List<ScoredCandidate> candidates, int count)
    {
        var remaining = candidates.ToList();
        var selected = new List<ScoredCandidate>();
        while (remaining.Count > 0 && selected.Count < count)
        {
            var next = remaining.OrderByDescending(candidate => (0.72 * candidate.Score)
                    - (0.28 * selected.Select(chosen => TagSimilarity(candidate.Argument.Tags, chosen.Argument.Tags)).DefaultIfEmpty(0).Max()))
                .ThenByDescending(candidate => candidate.Argument.CreatedAt).First();
            selected.Add(next);
            remaining.Remove(next);
        }
        return selected;
    }

    internal static double TagSimilarity(string[] left, string[] right)
    {
        if (left.Length == 0 || right.Length == 0) return 0;
        var leftSet = left.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var union = leftSet.Union(right, StringComparer.OrdinalIgnoreCase).Count();
        return union == 0 ? 0 : (double)leftSet.Intersect(right, StringComparer.OrdinalIgnoreCase).Count() / union;
    }

    private static double StableUnit(string? userId, Guid argumentId, int seed)
    {
        var input = Encoding.UTF8.GetBytes($"{userId ?? "anonymous"}|{argumentId:N}|{seed}");
        var hash = SHA256.HashData(input);
        return BitConverter.ToUInt64(hash, 0) / (double)ulong.MaxValue;
    }

    private static string GetLane(ScoredCandidate candidate)
    {
        if (candidate.Explore) return "Explore";
        if (candidate.Challenge) return "Growth";
        if (candidate.Interest >= candidate.Growth && candidate.Interest >= candidate.Collective) return "Interest";
        return candidate.Growth >= candidate.Collective ? "Growth" : "Collective";
    }

    private static string GetReason(ScoredCandidate candidate, IReadOnlyDictionary<string, double> affinities,
        IReadOnlySet<string> expertiseDomains)
    {
        if (candidate.Explore) return "A fresh perspective added for discovery.";
        if (candidate.Challenge) return "A contrasting value perspective within reach of your interests.";
        var matchingTag = candidate.Argument.Tags.OrderByDescending(tag => affinities.GetValueOrDefault(tag)).FirstOrDefault(affinities.ContainsKey);
        if (GetLane(candidate) == "Interest" && matchingTag is not null) return $"Because you engaged with {matchingTag}.";
        var expertiseTag = candidate.Argument.Tags.FirstOrDefault(expertiseDomains.Contains);
        if (GetLane(candidate) == "Growth" && expertiseTag is not null) return $"A new angle adjacent to your {expertiseTag} experience.";
        if (GetLane(candidate) == "Growth") return "A stretch into a less familiar topic.";
        return "Strong community confidence and structural relevance.";
    }

    private sealed record LinkSignal(Guid SourceArgumentId, Guid TargetArgumentId, LinkType LinkType);
    private sealed record ScoredCandidate(SocialArgument Argument, double Interest, double Growth, double Collective,
        double Score, bool Explore, bool Challenge);
}