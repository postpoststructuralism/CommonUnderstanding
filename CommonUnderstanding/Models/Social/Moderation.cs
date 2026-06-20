using System.ComponentModel.DataAnnotations;

namespace CommonUnderstanding.Models.Social;

/// <summary>
/// A user granted moderation powers, optionally scoped to a topic domain.
/// TopicDomain = null means global moderator.
/// </summary>
public class Moderator : BaseEntity
{
    /// <summary>References UserAccount.Id</summary>
    public string UserId { get; set; } = null!;

    [MaxLength(100)]
    public string? TopicDomain { get; set; }

    /// <summary>References UserAccount.Id of the admin who granted this role.</summary>
    public string GrantedByUserId { get; set; } = null!;

    public bool IsActive { get; set; } = true;
}

public enum FlagReason
{
    Fallacious,
    Toxic,
    Spam,
    OffTopic,
    Misinformation,
    Other
}

public enum FlagStatus
{
    Pending,
    UnderReview,
    Dismissed,
    ActionTaken
}

/// <summary>
/// A community flag on a SocialArgument, SocialProposition, or DebateContribution.
/// Three unique-user flags within 24 hours → entity enters UnderReview.
/// </summary>
public class ModerationFlag : BaseEntity
{
    /// <summary>"SocialArgument" | "SocialProposition" | "DebateContribution"</summary>
    [Required, MaxLength(50)]
    public string EntityType { get; set; } = null!;

    public Guid EntityId { get; set; }

    /// <summary>References UserAccount.Id</summary>
    public string FlaggingUserId { get; set; } = null!;

    public FlagReason Reason { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public FlagStatus Status { get; set; } = FlagStatus.Pending;

    /// <summary>References UserAccount.Id of the moderator who reviewed this flag.</summary>
    public string? ReviewedByUserId { get; set; }

    public DateTime? ReviewedAt { get; set; }
}

/// <summary>
/// An appeal submitted by a user whose content was shadow-banned or removed.
/// </summary>
public class ModerationAppeal : BaseEntity
{
    [Required, MaxLength(50)]
    public string EntityType { get; set; } = null!;

    public Guid EntityId { get; set; }

    /// <summary>References UserAccount.Id</summary>
    public string AppellantUserId { get; set; } = null!;

    [Required]
    public string Justification { get; set; } = null!;

    /// <summary>Pending | Upheld | Denied</summary>
    [MaxLength(20)]
    public string Status { get; set; } = "Pending";

    /// <summary>References UserAccount.Id</summary>
    public string? ReviewedByUserId { get; set; }

    public DateTime? ReviewedAt { get; set; }
}
