namespace CommonUnderstanding.Models;

/// <summary>
/// Represents a belief system or worldview with its core tenets and values
/// </summary>
public class BeliefSystem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<CoreBelief> CoreBeliefs { get; set; } = new();
    public List<string> Values { get; set; } = new();
    public List<string> Principles { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a core belief or tenet within a belief system
/// </summary>
public class CoreBelief
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int ImportanceLevel { get; set; } // 1-10 scale
}
