namespace CommonUnderstanding.Models.Social;

/// <summary>
/// Base class for all Phase 2 social platform entities.
/// Uses Guid IDs to support distributed generation without DB round-trips.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
