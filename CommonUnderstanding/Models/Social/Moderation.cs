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

public class UserIntegrityAssessment : BaseEntity
{
    public string UserId { get; set; } = null!;
    public double RiskScore { get; set; }
    public double InfluenceMultiplier { get; set; } = 1.0;
    public string SignalsJson { get; set; } = "[]";
    public DateTime LastAssessedAt { get; set; } = DateTime.UtcNow;
}

public enum DisputeStatus
{
    Open,
    UnderReview,
    Resolved,
    Dismissed
}

public enum DisputeResolution
{
    None,
    ClassificationUpheld,
    ClassificationChanged,
    InsufficientEvidence
}

public class ClassificationDispute : BaseEntity
{
    [Required, MaxLength(50)]
    public string TargetType { get; set; } = null!;

    [Required, MaxLength(100)]
    public string TargetId { get; set; } = null!;

    [Required, MaxLength(100)]
    public string ClassificationType { get; set; } = null!;

    public string RaisedByUserId { get; set; } = null!;

    [Required, MaxLength(2000)]
    public string Rationale { get; set; } = null!;

    public DisputeStatus Status { get; set; } = DisputeStatus.Open;
    public DisputeResolution Resolution { get; set; } = DisputeResolution.None;
    public string? ReviewedByUserId { get; set; }

    [MaxLength(2000)]
    public string? ResolutionNotes { get; set; }

    public DateTime? ResolvedAt { get; set; }
    public ICollection<DisputeEvidence> Evidence { get; set; } = new List<DisputeEvidence>();
}

public class DisputeEvidence : BaseEntity
{
    public Guid DisputeId { get; set; }
    public string SubmittedByUserId { get; set; } = null!;

    [Required, MaxLength(4000)]
    public string Statement { get; set; } = null!;

    [MaxLength(2000)]
    public string? SourceUrl { get; set; }

    public ClassificationDispute Dispute { get; set; } = null!;
}

public class ClassificationAuditEntry : BaseEntity
{
    [Required, MaxLength(50)]
    public string TargetType { get; set; } = null!;

    [Required, MaxLength(100)]
    public string TargetId { get; set; } = null!;

    [Required, MaxLength(50)]
    public string Action { get; set; } = null!;

    [Required, MaxLength(200)]
    public string ChangedBy { get; set; } = null!;

    [Required, MaxLength(500)]
    public string Reason { get; set; } = null!;

    public string? PreviousValueJson { get; set; }
    public string? NewValueJson { get; set; }
}
