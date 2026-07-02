using CommonUnderstanding.Data;
using CommonUnderstanding.Models.Social;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services.Social;

/// <summary>
/// Awards XP to users and maintains the UserReputation entity.
/// All XP events are logged to XPTransaction for auditability.
/// Rank is recomputed synchronously on every award — no background job required.
/// </summary>
public class XPAwardService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly BadgeAwardService _badges;
    private readonly ILogger<XPAwardService> _logger;

    public XPAwardService(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        BadgeAwardService badges,
        ILogger<XPAwardService> logger)
    {
        _dbFactory = dbFactory;
        _badges = badges;
        _logger = logger;
    }

    public async Task AwardAsync(
        string userId,
        long amount,
        string reason,
        Guid? referenceEntityId = null,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // Log the transaction
        db.XPTransactions.Add(new XPTransaction
        {
            UserId = userId,
            Amount = amount,
            Reason = reason,
            ReferenceEntityId = referenceEntityId
        });

        // Upsert reputation record
        var rep = await db.UserReputations.FirstOrDefaultAsync(r => r.UserId == userId, ct);
        if (rep is null)
        {
            rep = new UserReputation { UserId = userId };
            db.UserReputations.Add(rep);
        }

        rep.XP = Math.Max(0, rep.XP + amount); // XP cannot go below 0
        rep.Rank = ComputeRank(rep.XP);
        rep.LastActiveAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        _logger.LogDebug("Awarded {Amount} XP to {UserId} for: {Reason}", amount, userId, reason);

        // Check for newly unlocked badges after each award
        await _badges.CheckAndAwardBadgesAsync(userId, db, ct);
    }

    public async Task UpdateStreakAsync(string userId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var rep = await db.UserReputations.FirstOrDefaultAsync(r => r.UserId == userId, ct);
        if (rep is null) return;

        UpdateStreakInternal(rep, DateTime.UtcNow);

        // Award streak XP (starting day 3)
        if (rep.CurrentStreak >= 3)
            await AwardAsync(userId, 5, $"Daily streak: day {rep.CurrentStreak}", null, ct);

        // Check streak badges
        await db.SaveChangesAsync(ct);
    }

    internal static void UpdateStreakInternal(UserReputation rep, DateTime today)
    {
        var todayDate = today.Date;
        var lastDate = rep.LastStreakDate?.Date;

        if (lastDate is null)
        {
            rep.CurrentStreak = 1;
            rep.LastStreakDate = today;
        }
        else if (lastDate == todayDate)
        {
            // Already counted today — no change
        }
        else if (lastDate == todayDate.AddDays(-1))
        {
            // Consecutive day
            rep.CurrentStreak++;
            rep.LastStreakDate = today;
            if (rep.CurrentStreak > rep.LongestStreak)
                rep.LongestStreak = rep.CurrentStreak;
        }
        else
        {
            // Streak broken — check for freeze
            if (rep.StreakFreezes > 0 && lastDate == todayDate.AddDays(-2))
            {
                rep.StreakFreezes--;
                rep.LastStreakDate = today; // Consumed freeze; streak survives
            }
            else
            {
                rep.CurrentStreak = 1;
                rep.LastStreakDate = today;
            }
        }
    }

    public static string ComputeRank(long xp) => xp switch
    {
        >= 50_000 => "Grandmaster",
        >= 20_000 => "Sovereign Reasoner",
        >= 8_000  => "Master Dialectician",
        >= 3_000  => "Architect of Logic",
        >= 1_000  => "Logician",
        >= 200    => "Contender",
        _         => "Strategist-in-Training"
    };
}
