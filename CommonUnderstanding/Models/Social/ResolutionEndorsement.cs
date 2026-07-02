namespace CommonUnderstanding.Models.Social;

/// <summary>
/// Tracks user endorsements of resolutions (nexus between contradictory positions).
/// Used for the consensus_builder badge and DMI Score computation.
/// Unique constraint: (ResolutionId, UserId) — one endorsement per user per resolution.
/// </summary>
public class ResolutionEndorsement : BaseEntity
{
    /// <summary>References StructuralResolution.Id</summary>
    public Guid ResolutionId { get; set; }

    /// <summary>References UserAccount.Id</summary>
    public string UserId { get; set; } = null!;
}