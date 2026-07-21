using CommonUnderstanding.Data;
using CommonUnderstanding.Models.Social;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services.Social;

/// <summary>
/// Helper service for building feed results with various sort and filter options.
/// Encapsulates the complex query logic for the feed endpoint.
/// </summary>
public class FeedService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public FeedService(IDbContextFactory<ApplicationDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<FeedResultDto> GetFeedAsync(
        string? userId,
        string sort = "recent",
        string? domain = null,
        string[]? tags = null,
        int limit = 20,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var query = db.SocialArguments
            .AsNoTracking()
            .Include(a => a.ClaimProposition)
            .Include(a => a.Votes)
            .Where(a => a.IsPublic && !a.IsShadowBanned);

        // Filter by domain (topic tag)
        if (!string.IsNullOrEmpty(domain))
            query = query.Where(a => a.Tags.Contains(domain));

        // Filter by multiple tags (any match)
        if (tags is { Length: > 0 })
            query = query.Where(a => a.Tags.Any(t => tags.Contains(t)));

        // Feed ordering is intentionally limited to chronological and structural signals.
        query = sort switch
        {
            "updated" => query
                .OrderByDescending(a => a.UpdatedAt)
                .ThenByDescending(a => a.CreatedAt),
            "underrepresented" => query
                .Where(a => db.ArgumentLinks.Any(link =>
                    link.LinkType == LinkType.Contradicts &&
                    (link.SourceArgumentId == a.Id || link.TargetArgumentId == a.Id)))
                .OrderBy(a => db.SocialArguments.Count(other =>
                    other.IsPublic &&
                    !other.IsShadowBanned &&
                    other.ClaimPropositionId == a.ClaimPropositionId))
                .ThenByDescending(a => a.UpdatedAt),
            _ => query.OrderByDescending(a => a.CreatedAt)
        };

        var items = await query
            .Take(Math.Min(limit, 100))
            .Select(a => MapArgumentToFeedItem(a, userId))
            .ToListAsync(ct);

        return new FeedResultDto(NormalizeSort(sort), items);
    }

    public async Task<List<FeedItemDto>> GetUserFeedAsync(
        string userId,
        int limit = 20,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // User feed: arguments from followed users or relevant to user's epistemic domains
        var userDomains = await db.EpistemicProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId && p.EpistemicScore >= 2.0)
            .Select(p => p.TopicDomain)
            .ToListAsync(ct);

        var query = db.SocialArguments
            .AsNoTracking()
            .Include(a => a.ClaimProposition)
            .Where(a => a.IsPublic && !a.IsShadowBanned);

        if (userDomains.Count > 0)
            query = query.Where(a => a.Tags.Any(t => userDomains.Contains(t)));

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Take(Math.Min(limit, 100))
            .Select(a => MapArgumentToFeedItem(a, userId))
            .ToListAsync(ct);

        return items;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string NormalizeSort(string sort) => sort switch
    {
        "updated" => "updated",
        "underrepresented" => "underrepresented",
        _ => "recent"
    };

    private static FeedItemDto MapArgumentToFeedItem(SocialArgument a, string? currentUserId)
    {
        var userVote = a.Votes.FirstOrDefault(v => v.UserId == currentUserId);

        return new FeedItemDto(
            Id: a.Id,
            Title: a.Title,
            ClaimText: a.ClaimProposition?.Text,
            WarrantText: a.WarrantText,
            UserId: a.UserId,
            Tags: a.Tags,
            SchwartzValues: a.SchwartzValues,
            Upvotes: a.UpvoteCount,
            Downvotes: a.DownvoteCount,
            ReplyCount: a.ReplyCount,
            WilsonScore: a.WilsonScore,
            HotScore: a.HotScore,
            ControversyScore: a.ControversyScore,
            IsAIValidated: a.IsAIValidated,
            AIValidityScore: a.AIValidityScore,
            UserVote: userVote is not null ? new
            {
                vote = userVote.Vote.ToString(),
                rationale = userVote.Rationale.ToString(),
                epistemicWeight = userVote.EpistemicWeight
            } : null,
            CreatedAt: a.CreatedAt);
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record FeedResultDto(string Sort, List<FeedItemDto> Items);

public record FeedItemDto(
    Guid Id,
    string Title,
    string? ClaimText,
    string WarrantText,
    string UserId,
    string[] Tags,
    string[] SchwartzValues,
    int Upvotes,
    int Downvotes,
    int ReplyCount,
    double WilsonScore,
    double HotScore,
    double ControversyScore,
    bool IsAIValidated,
    double? AIValidityScore,
    object? UserVote,
    DateTime CreatedAt);
