using System.ComponentModel.DataAnnotations;

namespace CommonUnderstanding.Models.Social;

public enum LinkType
{
    Supports,
    Contradicts,
    Refines,
    Extends,
    Reply
}

/// <summary>
/// A typed directed edge connecting two SocialArguments in a reasoning graph.
/// Constraint: SourceArgumentId ≠ TargetArgumentId (DB check constraint).
/// Cycles are rejected at the API layer via BFS before insert.
/// </summary>
public class ArgumentLink : BaseEntity
{
    public Guid SourceArgumentId { get; set; }
    public Guid TargetArgumentId { get; set; }
    public LinkType LinkType { get; set; }

    [MaxLength(500)]
    public string? Annotation { get; set; }

    /// <summary>References UserAccount.Id</summary>
    public string UserId { get; set; } = null!;

    // Navigation
    public SocialArgument SourceArgument { get; set; } = null!;
    public SocialArgument TargetArgument { get; set; } = null!;
}
