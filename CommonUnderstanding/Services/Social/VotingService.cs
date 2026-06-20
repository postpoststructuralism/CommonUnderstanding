using CommonUnderstanding.Data;
using CommonUnderstanding.Models.Social;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services.Social;

/// <summary>
/// Encapsulates all vote-related business logic: casting votes, score computation,
/// rate limiting checks, and tally retrieval.
/// </summary>
public class VotingService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly EpistemicScoringService _epistemicScoring;
    private readonly XPAwardService _xpAwards;
    private readonly IConfiguration _configuration;
    private readonly ILogger<VotingService> _logger;

    // In-memory sliding window for rate limiting (production should use Redis).
    // Key: userId, Value: sorted list of vote timestamps.
    private static readonly Dictionary<string, Queue<DateTime>> _votingWindows = new();
    private static readonly object _windowLock = new();

    public VotingService(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        EpistemicScoringService epistemicScoring,
        XPAwardService xpAwards,
        IConfiguration configuration,
        ILogger<VotingService> logger)
    {
        _dbFactory = dbFactory;
        _epistemicScoring = epistemicScoring;
        _xpAwards = xpAwards;
        _configuration = configuration;
        _logger = logger;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public async Task<VoteCastResult> CastVoteAsync(
        string userId,
        Guid argumentId,
        VoteValue vote,
        VoteRationale rationale,
        string? comment = null,
        CancellationToken ct = default)
    {
        // Rate limit: 30 votes per hour per user
        int maxVotesPerHour = _configuration.GetValue("Voting:MaxVotesPerHour", 30);
        if (!CheckRateLimit(userId, maxVotesPerHour))
            return VoteCastResult.Rejected("Rate limit exceeded. You may cast up to 30 votes per hour.");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var argument = await db.SocialArguments
            .FirstOrDefaultAsync(a => a.Id == argumentId, ct);

        if (argument is null)
            return VoteCastResult.Rejected("Argument not found.");

        if (argument.UserId == userId)
            return VoteCastResult.Rejected("You cannot vote on your own argument.");

        // Compute epistemic weight for this user in the argument's primary topic domain
        string primaryDomain = argument.Tags.FirstOrDefault() ?? "General";
        double epistemicWeight = await _epistemicScoring.GetVoteWeightAsync(userId, primaryDomain, ct);

        // Upsert vote
        var existing = await db.ArgumentVotes
            .FirstOrDefaultAsync(v => v.ArgumentId == argumentId && v.UserId == userId, ct);

        VoteValue? previousVote = null;

        if (existing is null)
        {
            var newVote = new ArgumentVote
            {
                ArgumentId = argumentId,
                UserId = userId,
                Vote = vote,
                Rationale = rationale,
                Comment = comment,
                EpistemicWeight = epistemicWeight
            };
            db.ArgumentVotes.Add(newVote);
        }
        else
        {
            previousVote = existing.Vote;
            existing.Vote = vote;
            existing.Rationale = rationale;
            existing.Comment = comment;
            existing.EpistemicWeight = epistemicWeight;
        }

        await db.SaveChangesAsync(ct);
        RecordVoteTimestamp(userId);

        // Recompute and persist denormalized scores
        var tally = await RecomputeScoresAsync(argumentId, db, ct);

        // Award XP to argument author for ChangedMyView rationale (high-value signal)
        if (rationale == VoteRationale.ChangedMyView && previousVote != vote)
            await _xpAwards.AwardAsync(argument.UserId, 25, "ChangedMyView rationale received", argumentId, ct);

        // Award XP to argument author for upvote received
        if (vote == VoteValue.Up && previousVote != VoteValue.Up)
            await _xpAwards.AwardAsync(argument.UserId, 5, "Argument upvoted", argumentId, ct);
        else if (vote == VoteValue.Down && previousVote != VoteValue.Down)
            await _xpAwards.AwardAsync(argument.UserId, -2, "Argument downvoted", argumentId, ct);

        return VoteCastResult.Success(tally);
    }

    public async Task<VoteTallyDto> RevokeVoteAsync(
        string userId,
        Guid argumentId,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var vote = await db.ArgumentVotes
            .FirstOrDefaultAsync(v => v.ArgumentId == argumentId && v.UserId == userId, ct);

        if (vote is not null)
        {
            vote.Vote = VoteValue.Abstain;
            vote.Rationale = VoteRationale.Abstained;
            await db.SaveChangesAsync(ct);
        }

        return await RecomputeScoresAsync(argumentId, db, ct);
    }

    public async Task<VoteTallyDto?> GetTallyAsync(Guid argumentId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var argument = await db.SocialArguments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == argumentId, ct);

        if (argument is null) return null;

        var votes = await db.ArgumentVotes
            .AsNoTracking()
            .Where(v => v.ArgumentId == argumentId)
            .ToListAsync(ct);

        return BuildTally(argument, votes);
    }

    public async Task<ArgumentVote?> GetUserVoteAsync(string userId, Guid argumentId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.ArgumentVotes
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.ArgumentId == argumentId && v.UserId == userId, ct);
    }

    // ── Score computation ─────────────────────────────────────────────────────

    /// <summary>
    /// Recomputes Wilson score, hot score, and raw tallies for the given argument,
    /// persists them to the database, and returns the updated tally DTO.
    /// </summary>
    public async Task<VoteTallyDto> RecomputeScoresAsync(
        Guid argumentId,
        ApplicationDbContext db,
        CancellationToken ct = default)
    {
        var argument = await db.SocialArguments
            .FirstOrDefaultAsync(a => a.Id == argumentId, ct);

        if (argument is null)
            throw new InvalidOperationException($"Argument {argumentId} not found.");

        var votes = await db.ArgumentVotes
            .AsNoTracking()
            .Where(v => v.ArgumentId == argumentId)
            .ToListAsync(ct);

        double maxMultiplier = _configuration.GetValue("Voting:EpistemicMaxMultiplier", 2.0);
        double aiBonus = _configuration.GetValue("Voting:AIValidationBonus", 0.05);
        double gravity = _configuration.GetValue("Voting:HotScoreGravity", 1.8);

        double weightedUp = ScoringAlgorithms.EpistemicWeightedVoteCount(votes, VoteValue.Up, maxMultiplier);
        double weightedDown = ScoringAlgorithms.EpistemicWeightedVoteCount(votes, VoteValue.Down, maxMultiplier);

        int rawUp = votes.Count(v => v.Vote == VoteValue.Up);
        int rawDown = votes.Count(v => v.Vote == VoteValue.Down);
        int total = rawUp + rawDown;

        double wilsonBase = ScoringAlgorithms.WilsonScoreLowerBound(rawUp, total);
        double wilsonScore = wilsonBase + (argument.IsAIValidated && argument.AIValidityScore >= 0.8 ? aiBonus : 0.0);
        double hotScore = ScoringAlgorithms.HotScore(weightedUp, weightedDown, argument.CreatedAt, gravity);

        argument.UpvoteCount = rawUp;
        argument.DownvoteCount = rawDown;
        argument.WilsonScore = wilsonScore;
        argument.HotScore = hotScore;
        argument.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return BuildTally(argument, votes);
    }

    // ── Rate limiting ─────────────────────────────────────────────────────────

    private static bool CheckRateLimit(string userId, int maxPerHour)
    {
        lock (_windowLock)
        {
            if (!_votingWindows.TryGetValue(userId, out var window))
            {
                window = new Queue<DateTime>();
                _votingWindows[userId] = window;
            }

            var cutoff = DateTime.UtcNow.AddHours(-1);

            // Evict entries older than 1 hour
            while (window.Count > 0 && window.Peek() < cutoff)
                window.Dequeue();

            if (window.Count >= maxPerHour)
                return false;

            return true;
        }
    }

    private static void RecordVoteTimestamp(string userId)
    {
        lock (_windowLock)
        {
            if (!_votingWindows.TryGetValue(userId, out var window))
            {
                window = new Queue<DateTime>();
                _votingWindows[userId] = window;
            }
            window.Enqueue(DateTime.UtcNow);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static VoteTallyDto BuildTally(SocialArgument argument, List<ArgumentVote> votes)
    {
        double maxMultiplier = 2.0;
        return new VoteTallyDto(
            ArgumentId: argument.Id,
            Upvotes: argument.UpvoteCount,
            Downvotes: argument.DownvoteCount,
            EpistemicWeightedUpvotes: ScoringAlgorithms.EpistemicWeightedVoteCount(votes, VoteValue.Up, maxMultiplier),
            WilsonScore: argument.WilsonScore,
            HotScore: argument.HotScore,
            TotalVotes: argument.UpvoteCount + argument.DownvoteCount,
            ControversyScore: ScoringAlgorithms.ControversyScore(
                ScoringAlgorithms.EpistemicWeightedVoteCount(votes, VoteValue.Up, maxMultiplier),
                ScoringAlgorithms.EpistemicWeightedVoteCount(votes, VoteValue.Down, maxMultiplier))
        );
    }
}

// ── Result / DTO types ────────────────────────────────────────────────────────

public record VoteTallyDto(
    Guid ArgumentId,
    int Upvotes,
    int Downvotes,
    double EpistemicWeightedUpvotes,
    double WilsonScore,
    double HotScore,
    int TotalVotes,
    double ControversyScore);

public class VoteCastResult
{
    public bool IsSuccess { get; private init; }
    public string? ErrorMessage { get; private init; }
    public VoteTallyDto? Tally { get; private init; }

    public static VoteCastResult Rejected(string reason) =>
        new() { IsSuccess = false, ErrorMessage = reason };

    public static VoteCastResult Success(VoteTallyDto tally) =>
        new() { IsSuccess = true, Tally = tally };
}
