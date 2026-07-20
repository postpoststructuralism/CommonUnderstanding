using CommonUnderstanding.Data;
using CommonUnderstanding.Models.Social;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services.Social;

/// <summary>
/// Computes and updates per-user, per-domain EpistemicScores.
/// Pure C# arithmetic — no LLM or embedding calls.
/// </summary>
public class EpistemicScoringService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly ILogger<EpistemicScoringService> _logger;

    // How far back to look for votes (rolling window)
    private static readonly TimeSpan RollingWindow = TimeSpan.FromDays(90);

    // Community consensus threshold: if > 60% of weighted votes go one direction, that's the consensus
    private const double ConsensusThreshold = 0.60;

    public EpistemicScoringService(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        ILogger<EpistemicScoringService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    /// <summary>
    /// Returns the vote weight for a user in a given domain.
    /// Falls back to global average if no domain-specific profile exists.
    /// </summary>
    public async Task<double> GetVoteWeightAsync(
        string userId,
        string topicDomain,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        double maxMultiplier = 2.0;

        // Try domain-specific profile first
        var profile = await db.EpistemicProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId && p.TopicDomain == topicDomain, ct);

        if (profile is not null)
            return ScoringAlgorithms.EpistemicScoreToWeight(profile.EpistemicScore, maxMultiplier);

        // Fall back to global average across all domains
        var allProfiles = await db.EpistemicProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .ToListAsync(ct);

        if (allProfiles.Count > 0)
            return ScoringAlgorithms.EpistemicScoreToWeight(
                allProfiles.Average(p => p.EpistemicScore),
                maxMultiplier);

        return 1.0; // New user: no multiplier
    }

    /// <summary>
    /// Recalculates and persists the epistemic score for a user in a topic domain.
    /// Called by EpistemicScoringWorker on a schedule.
    /// </summary>
    public async Task RecalculateAsync(
        string userId,
        string topicDomain,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var cutoff = DateTime.UtcNow - RollingWindow;

        // Fetch user's votes in this domain (within rolling window)
        // We determine domain from the tags of the voted argument
        var userVotes = await db.ArgumentVotes
            .AsNoTracking()
            .Where(v => v.UserId == userId && v.CreatedAt >= cutoff)
            .Include(v => v.Argument)
            .Where(v => v.Argument.Tags.Contains(topicDomain))
            .ToListAsync(ct);

        if (userVotes.Count == 0)
        {
            // Nothing to update; ensure profile exists with defaults
            await EnsureProfileExistsAsync(db, userId, topicDomain, ct);
            return;
        }

        // Compute vote accuracy: what fraction of this user's votes matched community consensus?
        int matchingConsensus = 0;

        // Pre-load all votes for the user's voted arguments in a single query (avoids N+1)
        var relevantArgIds = userVotes.Select(v => v.ArgumentId).Distinct().ToList();
        var allVotesByArg = await db.ArgumentVotes
            .AsNoTracking()
            .Where(v => relevantArgIds.Contains(v.ArgumentId))
            .ToListAsync(ct);
        var votesLookup = allVotesByArg
            .GroupBy(v => v.ArgumentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var vote in userVotes.Where(v => v.Vote != VoteValue.Abstain))
        {
            if (!votesLookup.TryGetValue(vote.ArgumentId, out var allVotesOnArg))
                continue;

            if (allVotesOnArg.Count < 3) continue; // Insufficient data for consensus

            double maxMultiplier = 2.0;
            double totalWeightedUp = ScoringAlgorithms.EpistemicWeightedVoteCount(allVotesOnArg, VoteValue.Up, maxMultiplier);
            double totalWeightedDown = ScoringAlgorithms.EpistemicWeightedVoteCount(allVotesOnArg, VoteValue.Down, maxMultiplier);
            double totalWeighted = totalWeightedUp + totalWeightedDown;

            if (totalWeighted == 0) continue;

            bool consensusIsUp = (totalWeightedUp / totalWeighted) > ConsensusThreshold;
            bool consensusIsDown = (totalWeightedDown / totalWeighted) > ConsensusThreshold;

            if ((vote.Vote == VoteValue.Up && consensusIsUp) ||
                (vote.Vote == VoteValue.Down && consensusIsDown))
                matchingConsensus++;
        }

        int totalNonAbstain = userVotes.Count(v => v.Vote != VoteValue.Abstain);
        double voteAccuracy = totalNonAbstain > 0
            ? (double)matchingConsensus / totalNonAbstain
            : 0.5;

        // Compute average Wilson score of user's submitted arguments in this domain
        var userArgs = await db.SocialArguments
            .AsNoTracking()
            .Where(a => a.UserId == userId
                     && a.IsPublic
                     && !a.IsShadowBanned
                     && a.CreatedAt >= cutoff
                     && a.Tags.Contains(topicDomain))
            .ToListAsync(ct);

        double avgWilson = userArgs.Count > 0
            ? userArgs.Average(a => a.WilsonScore)
            : 0.5;

        double newScore = ScoringAlgorithms.ComputeEpistemicScore(voteAccuracy, avgWilson);

        // Upsert EpistemicProfile
        var profile = await db.EpistemicProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId && p.TopicDomain == topicDomain, ct);

        if (profile is null)
        {
            profile = new EpistemicProfile
            {
                UserId = userId,
                TopicDomain = topicDomain
            };
            db.EpistemicProfiles.Add(profile);
        }

        profile.EpistemicScore = newScore;
        profile.VoteAccuracy = voteAccuracy;
        profile.ContributionCount = userArgs.Count;
        profile.VoteCount = totalNonAbstain;
        profile.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        _logger.LogDebug(
            "Epistemic score updated for user {UserId} in domain {Domain}: {Score:F2}",
            userId, topicDomain, newScore);
    }

    private static async Task EnsureProfileExistsAsync(
        ApplicationDbContext db,
        string userId,
        string topicDomain,
        CancellationToken ct)
    {
        var exists = await db.EpistemicProfiles
            .AnyAsync(p => p.UserId == userId && p.TopicDomain == topicDomain, ct);

        if (!exists)
        {
            db.EpistemicProfiles.Add(new EpistemicProfile
            {
                UserId = userId,
                TopicDomain = topicDomain
            });
            await db.SaveChangesAsync(ct);
        }
    }
}
