using System.ComponentModel.DataAnnotations;

namespace CommonUnderstanding.Models.Social;

public enum VoteValue
{
    Up,
    Down,
    Abstain
}

public enum VoteRationale
{
    WellSourced,
    LogicallyValid,
    ChangedMyView,
    Fallacious,
    OffTopic,
    Abstained
}

/// <summary>
/// A typed, rationale-backed vote on a SocialArgument.
/// Unique constraint: (ArgumentId, UserId) — one vote per user per argument (upsert).
/// EpistemicWeight is computed at insert time from the voter's EpistemicProfile.
/// </summary>
public class ArgumentVote : BaseEntity
{
    public Guid ArgumentId { get; set; }

    /// <summary>References UserAccount.Id</summary>
    public string UserId { get; set; } = null!;

    public VoteValue Vote { get; set; }
    public VoteRationale Rationale { get; set; }

    [MaxLength(500)]
    public string? Comment { get; set; }

    /// <summary>
    /// Computed at insert time from the user's EpistemicProfile in the argument's primary topic.
    /// Range: [1.0, maxMultiplier]. Default 1.0 (no domain history).
    /// </summary>
    public double EpistemicWeight { get; set; } = 1.0;

    // Navigation
    public SocialArgument Argument { get; set; } = null!;
}
