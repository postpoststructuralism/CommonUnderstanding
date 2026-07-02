using CommonUnderstanding.Data;
using CommonUnderstanding.Models.Social;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services.Social;

/// <summary>
/// Checks badge trigger conditions and awards badges to users.
/// Called from XPAwardService after every XP award.
/// Each badge is unique per user — duplicates are silently skipped.
/// All badge awards are recorded in BadgeAwardLog for auditability.
/// </summary>
public class BadgeAwardService
{
    private readonly ILogger<BadgeAwardService> _logger;

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
        var newlyAwarded = new List<string>();

        // ── Onboarding badges ─────────────────────────────────────────────────
        var argCount = await db.SocialArguments
            .CountAsync(a => a.UserId == userId && a.IsPublic && !a.IsShadowBanned, ct);

        if (argCount >= 1 && TryAward(existing, "first_argument"))
            newlyAwarded.Add("first_argument");

        var hasUpvote = await db.ArgumentVotes
            .AnyAsync(v => v.Argument.UserId == userId && v.Vote == VoteValue.Up, ct);

        if (hasUpvote && TryAward(existing, "first_upvote"))
            newlyAwarded.Add("first_upvote");

        var hasChain = await db.ArgumentChains
            .AnyAsync(c => c.UserId == userId && c.ArgumentIds.Length >= 2, ct);

        if (hasChain && TryAward(existing, "first_chain"))
            newlyAwarded.Add("first_chain");

        // ── Streak badges ─────────────────────────────────────────────────────
        if (rep.LongestStreak >= 3 && TryAward(existing, "streak_3"))
            newlyAwarded.Add("streak_3");
        if (rep.LongestStreak >= 7 && TryAward(existing, "streak_7"))
            newlyAwarded.Add("streak_7");
        if (rep.LongestStreak >= 30 && TryAward(existing, "streak_30"))
            newlyAwarded.Add("streak_30");
        if (rep.LongestStreak >= 100 && TryAward(existing, "streak_100"))
            newlyAwarded.Add("streak_100");

        // ── Engagement badges ─────────────────────────────────────────────────
        var voteCount = await db.ArgumentVotes
            .CountAsync(v => v.UserId == userId, ct);

        if (voteCount >= 50 && TryAward(existing, "voter_50"))
            newlyAwarded.Add("voter_50");
        if (voteCount >= 500 && TryAward(existing, "voter_500"))
            newlyAwarded.Add("voter_500");

        var replyCount = await db.SocialArguments
            .CountAsync(a => a.UserId == userId && a.SourceArgumentId != null, ct);

        if (replyCount >= 10 && TryAward(existing, "commenter_10"))
            newlyAwarded.Add("commenter_10");
        if (replyCount >= 50 && TryAward(existing, "commenter_50"))
            newlyAwarded.Add("commenter_50");

        // ── Quality badges ────────────────────────────────────────────────────
        var hasTopArg25 = await db.SocialArguments
            .AnyAsync(a => a.UserId == userId && a.UpvoteCount >= 25, ct);
        if (hasTopArg25 && TryAward(existing, "top_argument_25"))
            newlyAwarded.Add("top_argument_25");

        var hasTopArg100 = await db.SocialArguments
            .AnyAsync(a => a.UserId == userId && a.UpvoteCount >= 100, ct);
        if (hasTopArg100 && TryAward(existing, "top_argument_100"))
            newlyAwarded.Add("top_argument_100");

        var hasTopArg500 = await db.SocialArguments
            .AnyAsync(a => a.UserId == userId && a.UpvoteCount >= 500, ct);
        if (hasTopArg500 && TryAward(existing, "top_argument_500"))
            newlyAwarded.Add("top_argument_500");

        var wilsonCount = await db.SocialArguments
            .CountAsync(a => a.UserId == userId && a.WilsonScore >= 0.85, ct);
        if (wilsonCount >= 3 && TryAward(existing, "wilson_champion"))
            newlyAwarded.Add("wilson_champion");

        var fallacyFreeCount = await db.SocialArguments
            .CountAsync(a => a.UserId == userId
                          && a.AIValidityScore >= 0.9
                          && (a.AIFallacyFlags == null || a.AIFallacyFlags == "[]"), ct);
        if (fallacyFreeCount >= 5 && TryAward(existing, "fallacy_free_5"))
            newlyAwarded.Add("fallacy_free_5");
        if (fallacyFreeCount >= 25 && TryAward(existing, "fallacy_free_25"))
            newlyAwarded.Add("fallacy_free_25");

        // ── Bridge-building badges ────────────────────────────────────────────
        var resolutionCount = await db.StructuralResolutions
            .CountAsync(r => r.AuthorId == userId, ct);

        if (resolutionCount >= 1 && TryAward(existing, "bridge_1"))
            newlyAwarded.Add("bridge_1");
        if (resolutionCount >= 5 && TryAward(existing, "bridge_5"))
            newlyAwarded.Add("bridge_5");
        if (resolutionCount >= 25 && TryAward(existing, "bridge_25"))
            newlyAwarded.Add("bridge_25");
        if (resolutionCount >= 100 && TryAward(existing, "bridge_100"))
            newlyAwarded.Add("bridge_100");

        // ── Changed Mind badges ───────────────────────────────────────────────
        var changedMindCount = await db.ArgumentVotes
            .CountAsync(v => v.Argument.UserId == userId
                          && v.Rationale == VoteRationale.ChangedMyView, ct);

        if (changedMindCount >= 5 && TryAward(existing, "changed_mind"))
            newlyAwarded.Add("changed_mind");
        if (changedMindCount >= 25 && TryAward(existing, "changed_mind_25"))
            newlyAwarded.Add("changed_mind_25");

        // ── Epistemic badges ──────────────────────────────────────────────────
        var isExpert = await db.EpistemicProfiles
            .AnyAsync(p => p.UserId == userId && p.EpistemicScore >= 4.0, ct);
        if (isExpert && TryAward(existing, "epistemic_expert"))
            newlyAwarded.Add("epistemic_expert");

        var masterDomainCount = await db.EpistemicProfiles
            .CountAsync(p => p.UserId == userId && p.EpistemicScore >= 4.5, ct);
        if (masterDomainCount >= 3 && TryAward(existing, "epistemic_master"))
            newlyAwarded.Add("epistemic_master");

        // ── Volume badges (hidden/easter egg) ─────────────────────────────────
        if (argCount >= 100 && TryAward(existing, "century_club"))
            newlyAwarded.Add("century_club");
        if (argCount >= 1000 && TryAward(existing, "millennium_club"))
            newlyAwarded.Add("millennium_club");

        // ── Persist any new badges ────────────────────────────────────────────
        if (newlyAwarded.Count > 0)
        {
            rep.Badges = existing.ToArray();

            // Record each award in the audit log
            foreach (var badgeId in newlyAwarded)
            {
                db.BadgeAwardLogs.Add(new BadgeAwardLog
                {
                    UserId = userId,
                    BadgeId = badgeId,
                    AwardedAt = DateTime.UtcNow,
                    TriggerSummary = $"Awarded via CheckAndAwardBadgesAsync"
                });
            }

            await db.SaveChangesAsync(ct);

            foreach (var badge in newlyAwarded)
                _logger.LogInformation("Badge awarded to {UserId}: {Badge}", userId, badge);
        }
    }

    /// <summary>
    /// Awards a specific badge to a user with a trigger summary.
    /// Used for badges that require external triggers (e.g., community_pick, debate_champion).
    /// Returns true if the badge was newly awarded, false if already held.
    /// </summary>
    public async Task<bool> AwardBadgeAsync(
        string userId,
        string badgeId,
        string? triggerSummary,
        ApplicationDbContext db,
        CancellationToken ct = default)
    {
        var rep = await db.UserReputations
            .FirstOrDefaultAsync(r => r.UserId == userId, ct);

        if (rep is null) return false;

        if (rep.Badges.Contains(badgeId))
            return false; // Already held

        rep.Badges = rep.Badges.Append(badgeId).ToArray();

        db.BadgeAwardLogs.Add(new BadgeAwardLog
        {
            UserId = userId,
            BadgeId = badgeId,
            AwardedAt = DateTime.UtcNow,
            TriggerSummary = triggerSummary
        });

        await db.SaveChangesAsync(ct);

        _logger.LogInformation("Badge awarded to {UserId}: {Badge} — {Summary}",
            userId, badgeId, triggerSummary);

        return true;
    }

    /// <summary>Adds badge to set only if not already present. Returns true if added.</summary>
    private static bool TryAward(HashSet<string> existing, string badge) =>
        existing.Add(badge); // HashSet.Add returns true if added, false if already present
}
