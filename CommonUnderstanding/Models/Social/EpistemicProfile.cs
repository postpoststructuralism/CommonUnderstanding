using System.ComponentModel.DataAnnotations;

namespace CommonUnderstanding.Models.Social;

/// <summary>
/// Per-user, per-topic-domain reputation score derived from vote accuracy
/// and argument quality on a rolling 90-day window.
/// Range: 0.0–5.0. Governs vote weight multiplier and Debate Room access.
/// Unique constraint: (UserId, TopicDomain).
/// </summary>
public class EpistemicProfile : BaseEntity
{
    /// <summary>References UserAccount.Id</summary>
    public string UserId { get; set; } = null!;

    [Required, MaxLength(100)]
    public string TopicDomain { get; set; } = null!;

    /// <summary>Overall epistemic score in [0.0, 5.0].</summary>
    public double EpistemicScore { get; set; } = 1.0;

    /// <summary>Fraction of user's votes that aligned with community consensus (rolling 90-day).</summary>
    public double VoteAccuracy { get; set; } = 0.5;

    public int ContributionCount { get; set; } = 0;
    public int VoteCount { get; set; } = 0;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Global XP-based reputation, rank, badges, and streaks.
/// </summary>
public class UserReputation : BaseEntity
{
    /// <summary>References UserAccount.Id</summary>
    public string UserId { get; set; } = null!;

    public long XP { get; set; } = 0;

    [MaxLength(50)]
    public string Rank { get; set; } = "Novice";

    /// <summary>Stored as jsonb text[]: array of badge identifiers.</summary>
    public string[] Badges { get; set; } = Array.Empty<string>();

    public int CurrentStreak { get; set; } = 0;
    public int LongestStreak { get; set; } = 0;
    public DateTime? LastStreakDate { get; set; }
    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;

    /// <summary>Consumable streak-freeze items. Awarded at ranks Reasoner (1) and Scholar (3).</summary>
    public int StreakFreezes { get; set; } = 0;

    /// <summary>
    /// Dialectical Mastery Index — composite score for the mastery leaderboard.
    /// Computed hourly by DmiScoreWorker. Formula:
    /// (ResolutionCount × 2.0) + (AlignmentMatricesCreated × 1.5) + (ChangedMindCount × 3.0)
    /// + (CrossAisleUpvotes × 0.5) + (ResolutionsEndorsedByOthers × 1.0)
    /// </summary>
    public double DmiScore { get; set; } = 0.0;
}

/// <summary>
/// Audit record for every XP award. Enables trend analysis and dispute resolution.
/// </summary>
public class XPTransaction : BaseEntity
{
    /// <summary>References UserAccount.Id</summary>
    public string UserId { get; set; } = null!;

    public long Amount { get; set; }

    [MaxLength(200)]
    public string Reason { get; set; } = null!;

    /// <summary>Optional reference to the entity that triggered the award (e.g., ArgumentId).</summary>
    public Guid? ReferenceEntityId { get; set; }
}
