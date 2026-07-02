namespace CommonUnderstanding.Models.Social;

/// <summary>
/// Audit trail for every badge awarded.
/// Enables transparency, dispute resolution, and trend analysis.
/// </summary>
public class BadgeAwardLog : BaseEntity
{
    /// <summary>References UserAccount.Id</summary>
    public string UserId { get; set; } = null!;

    /// <summary>Badge identifier (matches BadgeRegistry keys).</summary>
    public string BadgeId { get; set; } = null!;

    /// <summary>When the badge was awarded.</summary>
    public DateTime AwardedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Human-readable summary of what triggered the award, e.g. "5th synthesis created (ID: abc-123)".</summary>
    public string? TriggerSummary { get; set; }
}