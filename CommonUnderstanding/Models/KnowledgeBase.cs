using System.Text.Json.Serialization;

namespace CommonUnderstanding.Models;

/// <summary>
/// Represents a well-defined belief system in the knowledge base
/// </summary>
public class CanonicalBeliefSystem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    // URL-friendly slug derived from the Name
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // Religion, Philosophy, Political, Scientific
    public string Culture { get; set; } = string.Empty;
    public string Era { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Key texts, founding documents, or primary sources
    /// </summary>
    public List<string> Sources { get; set; } = new();
    
    /// <summary>
    /// The dimensional profile of this belief system
    /// </summary>
    public BeliefSnapshot Profile { get; set; } = new();
    
    /// <summary>
    /// Creation myth or origin story (if applicable)
    /// </summary>
    public string? CreationMyth { get; set; }
    
    /// <summary>
    /// Core tenets or principles
    /// </summary>
    public List<string> CorePrinciples { get; set; } = new();
    
    /// <summary>
    /// Related or derivative belief systems
    /// </summary>
    public List<string> RelatedSystems { get; set; } = new();
    
    /// <summary>
    /// Historical context
    /// </summary>
    public string? HistoricalContext { get; set; }
    
    /// <summary>
    /// Notable figures associated with this belief system
    /// </summary>
    public List<string> NotableFigures { get; set; } = new();
    
    /// <summary>
    /// Geographic regions where this belief system is/was prevalent
    /// </summary>
    public List<string> Regions { get; set; } = new();
}

/// <summary>
/// Represents the relationship/distance between two belief systems
/// </summary>
public class BeliefSystemRelationship
{
    public string System1Id { get; set; } = string.Empty;
    public string System2Id { get; set; } = string.Empty;
    
    /// <summary>
    /// Overall similarity (0-1, where 1 is identical)
    /// </summary>
    public double Similarity { get; set; }
    
    /// <summary>
    /// Dimensional distances for each dimension
    /// </summary>
    public Dictionary<string, double> DimensionalDistances { get; set; } = new();
    
    /// <summary>
    /// Areas of convergence
    /// </summary>
    public List<string> Commonalities { get; set; } = new();
    
    /// <summary>
    /// Areas of divergence
    /// </summary>
    public List<string> Differences { get; set; } = new();
    
    /// <summary>
    /// Historical relationship (if any)
    /// </summary>
    public string? HistoricalRelationship { get; set; }
}

/// <summary>
/// Represents where a user fits in the belief universe
/// </summary>
public class BeliefUniversePosition
{
    public string UserId { get; set; } = string.Empty;
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Closest matching canonical belief systems
    /// </summary>
    public List<BeliefSystemMatch> NearestSystems { get; set; } = new();
    
    /// <summary>
    /// User's unique position (might be between systems)
    /// </summary>
    public Dictionary<string, double> UniverseCoordinates { get; set; } = new();
    
    /// <summary>
    /// Narrative explanation of where they fit
    /// </summary>
    public string PositionNarrative { get; set; } = string.Empty;
}

/// <summary>
/// A match between a user and a canonical belief system
/// </summary>
public class BeliefSystemMatch
{
    public string SystemId { get; set; } = string.Empty;
    public string SystemName { get; set; } = string.Empty;
    public double MatchPercentage { get; set; }
    public Dictionary<string, double> DimensionalAlignment { get; set; } = new();
    public List<string> SharedValues { get; set; } = new();
    public List<string> KeyDifferences { get; set; } = new();
}

/// <summary>
/// Category of knowledge
/// </summary>
public enum KnowledgeCategory
{
    Religion,
    Philosophy,
    Political,
    Scientific,
    Mathematical,
    Historical,
    Cultural,
    Mythological,
    Ethical,
    Economic
}
