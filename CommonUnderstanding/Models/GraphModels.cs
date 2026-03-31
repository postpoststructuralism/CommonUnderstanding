using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CommonUnderstanding.Models;

/// <summary>
/// A node in the organizational Common Understanding Graph.
/// Each node represents a proposition (an atomic knowledge claim) that has
/// appeared in one or more arguments submitted to the system.
/// </summary>
public class CommonUnderstandingNode
{
    public int Id { get; set; }

    /// <summary>
    /// Canonical text of the proposition.
    /// First occurrence wins; subsequent matches are linked via ArgumentIds.
    /// </summary>
    [Required]
    public string Text { get; set; } = string.Empty;

    /// <summary>Normalized key used for deduplication (lowercase, trimmed).</summary>
    [MaxLength(500)]
    public string NormalizedKey { get; set; } = string.Empty;

    public PropositionStatus Status { get; set; } = PropositionStatus.Unevaluated;

    /// <summary>Bayesian confidence score [0.0–1.0].</summary>
    public double Confidence { get; set; } = 0.5;

    public int EvidenceCount { get; set; }

    /// <summary>JSON array of argument IDs that reference this proposition.</summary>
    public string ArgumentIdsJson { get; set; } = "[]";

    public int Version { get; set; } = 1;

    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Domain tags: empirical, normative, causal, definitional, etc.</summary>
    [MaxLength(200)]
    public string? Tags { get; set; }

    // Navigation
    public ICollection<CommonUnderstandingEdge> OutboundEdges { get; set; } = new List<CommonUnderstandingEdge>();
    public ICollection<CommonUnderstandingEdge> InboundEdges { get; set; } = new List<CommonUnderstandingEdge>();
}

/// <summary>
/// A directed edge in the Common Understanding Graph representing
/// a logical relationship between two proposition nodes.
/// </summary>
public class CommonUnderstandingEdge
{
    public int Id { get; set; }

    [Required]
    public int SourceNodeId { get; set; }

    [Required]
    public int TargetNodeId { get; set; }

    /// <summary>Relationship type: supports | contradicts | qualifies | assumes | equivalent</summary>
    [Required, MaxLength(40)]
    public string Relationship { get; set; } = string.Empty;

    /// <summary>Strength of the relationship [0.0–1.0].</summary>
    public double Strength { get; set; } = 0.5;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(SourceNodeId))]
    public CommonUnderstandingNode? SourceNode { get; set; }

    [ForeignKey(nameof(TargetNodeId))]
    public CommonUnderstandingNode? TargetNode { get; set; }
}

// ─────────────────────────────────────────────
//  Stakeholder models (Phase 4)
// ─────────────────────────────────────────────

public enum StakeholderPositionType
{
    Support,
    Oppose,
    Undecided
}

/// <summary>
/// An organizational stakeholder who can register positions on arguments.
/// </summary>
public class Stakeholder
{
    public int Id { get; set; }

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Role { get; set; }

    [MaxLength(150)]
    public string? Organization { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<StakeholderPosition> Positions { get; set; } = new List<StakeholderPosition>();
}

/// <summary>
/// A stakeholder's position on a specific argument, including which
/// premises they accept or reject and their reasoning.
/// </summary>
public class StakeholderPosition
{
    public int Id { get; set; }

    [Required]
    public int StakeholderId { get; set; }

    [Required]
    public int ArgumentId { get; set; }

    public StakeholderPositionType Position { get; set; } = StakeholderPositionType.Undecided;

    [MaxLength(2000)]
    public string? Reasoning { get; set; }

    /// <summary>JSON array of proposition IDs the stakeholder accepts.</summary>
    public string AcceptedPremiseIdsJson { get; set; } = "[]";

    /// <summary>JSON array of proposition IDs the stakeholder rejects.</summary>
    public string RejectedPremiseIdsJson { get; set; } = "[]";

    public bool IsAnonymous { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(StakeholderId))]
    public Stakeholder? StakeholderRef { get; set; }

    [ForeignKey(nameof(ArgumentId))]
    public Argument? Argument { get; set; }
}
