using System.ComponentModel.DataAnnotations;

namespace CommonUnderstanding.Models.Social;

public enum DebateStatus
{
    Open,
    Active,
    Concluded,
    Cancelled
}

public enum DebateFormat
{
    Oxford,
    LincolnDouglas,
    Custom
}

public enum DebateRole
{
    Proponent,
    Opponent,
    Rebuttal,
    JudgeComment
}

/// <summary>
/// A bounded, structured, real-time debate session between a Proponent and Opponent.
/// All contributions reference existing SocialArguments — no ad-hoc text-only posts.
/// </summary>
public class DebateRoom : BaseEntity
{
    [Required, MaxLength(300)]
    public string Title { get; set; } = null!;

    [Required, MaxLength(200)]
    public string Topic { get; set; } = null!;

    [Required]
    public string MotionText { get; set; } = null!;

    public Guid? MotionPropositionId { get; set; }

    /// <summary>References UserAccount.Id</summary>
    public string ProponentUserId { get; set; } = null!;

    /// <summary>References UserAccount.Id. Null until someone joins.</summary>
    public string? OpponentUserId { get; set; }

    /// <summary>Judge user IDs stored as text[] in PostgreSQL.</summary>
    public string[] JudgeUserIds { get; set; } = Array.Empty<string>();

    public DebateStatus Status { get; set; } = DebateStatus.Open;
    public DebateFormat Format { get; set; } = DebateFormat.Oxford;

    /// <summary>Per-contribution time limit in seconds.</summary>
    public int TimeLimitSeconds { get; set; } = 300;
    public int MaxContributionsPerSide { get; set; } = 5;

    public DateTime? ConcludedAt { get; set; }

    public double? ProponentScore { get; set; }
    public double? OpponentScore { get; set; }

    public bool AIRefereeEnabled { get; set; } = true;

    // Navigation
    public SocialProposition? MotionProposition { get; set; }
    public ICollection<DebateContribution> Contributions { get; set; } = new List<DebateContribution>();
}

/// <summary>
/// A single contribution within a DebateRoom — references an existing SocialArgument.
/// AI referee outputs are stored as JSON in FallacyFlags.
/// </summary>
public class DebateContribution : BaseEntity
{
    public Guid DebateRoomId { get; set; }

    /// <summary>References UserAccount.Id</summary>
    public string UserId { get; set; } = null!;

    public Guid ArgumentId { get; set; }
    public DebateRole Role { get; set; }
    public int OrderIndex { get; set; }

    /// <summary>JSON: array of { Name, Description, QuotedText }</summary>
    public string? FallacyFlags { get; set; }

    public double? ValidityScore { get; set; }

    [MaxLength(2000)]
    public string? AIRefereeComment { get; set; }

    // Navigation
    public DebateRoom DebateRoom { get; set; } = null!;
    public SocialArgument Argument { get; set; } = null!;
}
