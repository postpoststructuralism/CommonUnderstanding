using CommonUnderstanding.Data;
using CommonUnderstanding.Models.Social;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services.Social;

/// <summary>
/// Checks badge trigger conditions and awards badges to users.
/// Called from XPAwardService after every XP award.
/// Each badge is unique per user — duplicates are silently skipped.
/// </summary>
public class BadgeAwardService
{
    private readonly ILogger<BadgeAwardService> _logger;

    // Badge IDs as constants to prevent typos
    public static class Badges
    {
        public const string FirstArgument       = "first_argument";
        public const string FirstUpvote         = "first_upvote";
        public const string ChainBuilder        = "chain_builder";
        public const string WorldviewAuthor     = "worldview_author";
        public const string DebateWinner        = "debate_winner";
        public const string BridgeBuilder       = "bridge_builder";
        public const string ChangedMind         = "changed_mind";
        public const string EpistemicExpert     = "epistemic_expert";
        public const string Streak7             = "streak_7";
        public const string Streak30            = "streak_30";
        public const string TopArgument         = "top_argument";
        public const string ConvergenceCatalyst = "convergence_catalyst";
        public const string FallacyFree         = "fallacy_free";
        public const string Judge               = "judge";
    }

    public BadgeAwardService(ILogger<BadgeAwardService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Evaluates all badge trigger conditions for a user and awards any newly earned badges.
    /// This is called after every XP-affecting event.
    /// </summary>
    public async Task CheckAndAwardBadgesAsync(
        string userId,
        ApplicationDbContext db,
        CancellationToken ct = default)
    {
        var rep = await db.UserReputations
            .FirstOrDefaultAsync(r => r.UserId == userId, ct);

        if (rep is null) return;

        var existing = new HashSet<string>(rep.Badges);

        // ── Streak badges ─────────────────────────────────────────────────────
        if (rep.LongestStreak >= 7)  TryAward(existing, Badges.Streak7);
        if (rep.LongestStreak >= 30) TryAward(existing, Badges.Streak30);

        // ── Argument count badges ─────────────────────────────────────────────
        var argCount = await db.SocialArguments
            .CountAsync(a => a.UserId == userId && a.IsPublic && !a.IsShadowBanned, ct);

        if (argCount >= 1) TryAward(existing, Badges.FirstArgument);

        // ── Upvote badges ─────────────────────────────────────────────────────
        var hasUpvote = await db.ArgumentVotes
            .AnyAsync(v => v.Argument.UserId == userId && v.Vote == VoteValue.Up, ct);

        if (hasUpvote) TryAward(existing, Badges.FirstUpvote);

        // ── Chain badges ──────────────────────────────────────────────────────
        var bigChainExists = await db.ArgumentChains
            .AnyAsync(c => c.UserId == userId && c.ArgumentIds.Length >= 5, ct);

        if (bigChainExists) TryAward(existing, Badges.ChainBuilder);

        // ── Worldview badges ──────────────────────────────────────────────────
        var hasPublicWorldview = await db.Worldviews
            .AnyAsync(w => w.UserId == userId && w.IsPublic, ct);

        if (hasPublicWorldview) TryAward(existing, Badges.WorldviewAuthor);

        // ── Top argument badge (100 upvotes) ──────────────────────────────────
        var hasTopArg = await db.SocialArguments
            .AnyAsync(a => a.UserId == userId && a.UpvoteCount >= 100, ct);

        if (hasTopArg) TryAward(existing, Badges.TopArgument);

        // ── Epistemic expert badge ────────────────────────────────────────────
        var isExpert = await db.EpistemicProfiles
            .AnyAsync(p => p.UserId == userId && p.EpistemicScore >= 4.0, ct);

        if (isExpert) TryAward(existing, Badges.EpistemicExpert);

        // ── Changed Mind badge (5 ChangedMyView rationales on user's args) ────
        var changedMindCount = await db.ArgumentVotes
            .CountAsync(v => v.Argument.UserId == userId
                          && v.Rationale == VoteRationale.ChangedMyView, ct);

        if (changedMindCount >= 5) TryAward(existing, Badges.ChangedMind);

        // Persist any new badges
        var newBadges = existing.Except(rep.Badges).ToArray();
        if (newBadges.Length > 0)
        {
            rep.Badges = existing.ToArray();
            await db.SaveChangesAsync(ct);

            foreach (var badge in newBadges)
                _logger.LogInformation("Badge awarded to {UserId}: {Badge}", userId, badge);
        }
    }

    /// <summary>Adds badge to set only if not already present.</summary>
    private static void TryAward(HashSet<string> existing, string badge) =>
        existing.Add(badge); // HashSet.Add is idempotent
}
