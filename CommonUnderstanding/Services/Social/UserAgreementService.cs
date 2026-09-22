using CommonUnderstanding.Data;
using CommonUnderstanding.Models.Social;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services.Social;

public sealed class UserAgreementService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public UserAgreementService(IDbContextFactory<ApplicationDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<IReadOnlyList<UserAgreementSummary>> GetRankingsAsync(
        string currentUserId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var comparisons = await GetComparisonsAsync(db, currentUserId, null, cancellationToken);

        return comparisons
            .GroupBy(comparison => new { comparison.OtherUserId, comparison.OtherUserName })
            .Select(group =>
            {
                var agreements = group.Count(comparison => comparison.Agrees);
                var total = group.Count();
                return new UserAgreementSummary
                {
                    UserId = group.Key.OtherUserId,
                    DisplayName = group.Key.OtherUserName,
                    AgreementCount = agreements,
                    DisagreementCount = total - agreements,
                    ComparableVoteCount = total,
                    AgreementRate = (double)agreements / total,
                    RankingScore = ScoringAlgorithms.WilsonScoreLowerBound(agreements, total)
                };
            })
            .OrderByDescending(summary => summary.RankingScore)
            .ThenByDescending(summary => summary.ComparableVoteCount)
            .ThenBy(summary => summary.DisplayName)
            .ToList();
    }

    public async Task<UserAgreementDetail?> GetDetailAsync(
        string currentUserId,
        string otherUserId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var otherUserName = await db.UserAccounts
            .AsNoTracking()
            .Where(user => user.Id == otherUserId && user.IsActive && !user.IsServiceAccount)
            .Select(user => user.DisplayName)
            .FirstOrDefaultAsync(cancellationToken);

        if (otherUserName is null || otherUserId == currentUserId)
            return null;

        var comparisons = await GetComparisonsAsync(db, currentUserId, otherUserId, cancellationToken);
        var agreementCount = comparisons.Count(comparison => comparison.Agrees);

        return new UserAgreementDetail
        {
            UserId = otherUserId,
            DisplayName = otherUserName,
            AgreementCount = agreementCount,
            DisagreementCount = comparisons.Count - agreementCount,
            Comparisons = comparisons
        };
    }

    private static async Task<List<UserAgreementComparison>> GetComparisonsAsync(
        ApplicationDbContext db,
        string currentUserId,
        string? otherUserId,
        CancellationToken cancellationToken)
    {
        var currentVotes = await db.ArgumentVotes
            .AsNoTracking()
            .Where(vote => vote.UserId == currentUserId
                && vote.Vote != VoteValue.Abstain
                && vote.Argument.IsPublic
                && !vote.Argument.IsShadowBanned)
            .Select(vote => new { vote.ArgumentId, vote.Vote, vote.Rationale })
            .ToListAsync(cancellationToken);

        if (currentVotes.Count == 0)
            return [];

        var currentVoteByArgument = currentVotes.ToDictionary(vote => vote.ArgumentId);
        var argumentIds = currentVoteByArgument.Keys.ToList();

        var peerVotesQuery = db.ArgumentVotes
            .AsNoTracking()
            .Where(vote => vote.UserId != currentUserId
                && vote.Vote != VoteValue.Abstain
                && argumentIds.Contains(vote.ArgumentId)
                && vote.Argument.IsPublic
                && !vote.Argument.IsShadowBanned
                && vote.Argument.UserId != vote.UserId);

        if (otherUserId is not null)
            peerVotesQuery = peerVotesQuery.Where(vote => vote.UserId == otherUserId);

        var peerVotes = await peerVotesQuery
            .Join(
                db.UserAccounts.Where(user => user.IsActive && !user.IsServiceAccount),
                vote => vote.UserId,
                user => user.Id,
                (vote, user) => new
                {
                    vote.ArgumentId,
                    vote.UserId,
                    OtherUserName = user.DisplayName,
                    vote.Vote,
                    vote.Rationale,
                    vote.Argument.Title,
                    vote.Argument.UpdatedAt
                })
            .ToListAsync(cancellationToken);

        return peerVotes
            .Select(peerVote =>
            {
                var currentVote = currentVoteByArgument[peerVote.ArgumentId];
                return new UserAgreementComparison
                {
                    ArgumentId = peerVote.ArgumentId,
                    ArgumentTitle = peerVote.Title,
                    ArgumentUpdatedAt = peerVote.UpdatedAt,
                    OtherUserId = peerVote.UserId,
                    OtherUserName = peerVote.OtherUserName,
                    CurrentUserVote = currentVote.Vote,
                    CurrentUserRationale = currentVote.Rationale,
                    OtherUserVote = peerVote.Vote,
                    OtherUserRationale = peerVote.Rationale,
                    Agrees = currentVote.Vote == peerVote.Vote
                };
            })
            .OrderByDescending(comparison => comparison.ArgumentUpdatedAt)
            .ToList();
    }
}

public sealed class UserAgreementSummary
{
    public string UserId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public int AgreementCount { get; init; }
    public int DisagreementCount { get; init; }
    public int ComparableVoteCount { get; init; }
    public double AgreementRate { get; init; }
    public double RankingScore { get; init; }
}

public sealed class UserAgreementDetail
{
    public string UserId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public int AgreementCount { get; init; }
    public int DisagreementCount { get; init; }
    public int ComparableVoteCount => AgreementCount + DisagreementCount;
    public double AgreementRate => ComparableVoteCount == 0
        ? 0
        : (double)AgreementCount / ComparableVoteCount;
    public IReadOnlyList<UserAgreementComparison> Comparisons { get; init; } = [];
}

public sealed class UserAgreementComparison
{
    public Guid ArgumentId { get; init; }
    public string ArgumentTitle { get; init; } = string.Empty;
    public DateTime ArgumentUpdatedAt { get; init; }
    public string OtherUserId { get; init; } = string.Empty;
    public string OtherUserName { get; init; } = string.Empty;
    public VoteValue CurrentUserVote { get; init; }
    public VoteRationale CurrentUserRationale { get; init; }
    public VoteValue OtherUserVote { get; init; }
    public VoteRationale OtherUserRationale { get; init; }
    public bool Agrees { get; init; }
}