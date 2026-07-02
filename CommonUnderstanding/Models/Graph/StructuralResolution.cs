using CommonUnderstanding.Models.Social;

namespace CommonUnderstanding.Models.Graph;

/// <summary>
/// A user-proposed resolution that reconciles two contradictory positions in the graph.
/// Stored as part of the Understanding Graph but tracked socially for badge/XP purposes.
/// </summary>
public class StructuralResolution : BaseEntity
{
    /// <summary>References UnderstandingNode.Id — the source node (one side of contradiction).</summary>
    public Guid SourceNodeId { get; set; }

    /// <summary>References UnderstandingNode.Id — the target node (other side of contradiction).</summary>
    public Guid TargetNodeId { get; set; }

    /// <summary>The resolution text that reconciles the two positions.</summary>
    public string ResolutionText { get; set; } = null!;

    /// <summary>References UserAccount.Id — who created this resolution.</summary>
    public string? AuthorId { get; set; }

    /// <summary>Denormalized counter of endorsements from other users.</summary>
    public int EndorsementCount { get; set; } = 0;

    // Navigation
    public ICollection<ResolutionEndorsement> Endorsements { get; set; } = new List<ResolutionEndorsement>();
}