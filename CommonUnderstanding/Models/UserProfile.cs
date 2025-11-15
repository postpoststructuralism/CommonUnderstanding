namespace CommonUnderstanding.Models;

/// <summary>
/// Represents a user being profiled by the system
/// </summary>
public class UserProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastInteractionAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// The current (most recent) snapshot of our mental model of this user
    /// </summary>
    public BeliefSnapshot? CurrentBeliefSnapshot { get; set; }
    
    /// <summary>
    /// Historical snapshots showing how our understanding has evolved
    /// </summary>
    public List<BeliefSnapshot> HistoricalSnapshots { get; set; } = new();
    
    /// <summary>
    /// All interactions we've had with this user
    /// </summary>
    public List<UserInteraction> Interactions { get; set; } = new();
    
    /// <summary>
    /// Current stage in the discovery process
    /// </summary>
    public DiscoveryStage Stage { get; set; } = DiscoveryStage.Initial;
    
    /// <summary>
    /// Number of interactions completed
    /// </summary>
    public int InteractionCount => Interactions.Count;
    
    /// <summary>
    /// Track which questions have been asked to prevent repetition
    /// </summary>
    public HashSet<string> AskedQuestionHashes { get; set; } = new();
    
    /// <summary>
    /// Pre-fetched questions ready to be served
    /// </summary>
    public Queue<UserInteraction> PrefetchedQuestions { get; set; } = new();
    
    /// <summary>
    /// Dimensions that have been explored sufficiently
    /// </summary>
    public HashSet<string> ExploredDimensions { get; set; } = new();
}

/// <summary>
/// Stages in the belief discovery process
/// </summary>
public enum DiscoveryStage
{
    Initial,           // Just started
    Foundation,        // Basic worldview questions (5-10 interactions)
    Exploration,       // Broader belief exploration (10-25 interactions)
    Refinement,        // Testing edge cases and boundaries (25-50 interactions)
    Continuous         // Ongoing refinement
}

/// <summary>
/// A point-in-time snapshot of our mental model of a user's belief system
/// </summary>
public class BeliefSnapshot
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public int InteractionCount { get; set; }
    
    /// <summary>
    /// Our inferred belief dimensions with confidence scores
    /// </summary>
    public List<BeliefDimension> Dimensions { get; set; } = new();
    
    /// <summary>
    /// Detected values ranked by confidence
    /// </summary>
    public List<InferredValue> Values { get; set; } = new();
    
    /// <summary>
    /// Moral foundations scores (Haidt's framework)
    /// </summary>
    public MoralFoundationsProfile MoralFoundations { get; set; } = new();
    
    /// <summary>
    /// Overall confidence in this model (0-1)
    /// </summary>
    public double OverallConfidence { get; set; }
    
    /// <summary>
    /// AI-generated narrative summary of this person's worldview
    /// </summary>
    public string NarrativeSummary { get; set; } = string.Empty;
    
    /// <summary>
    /// Statistical metadata about the model
    /// </summary>
    public ModelStatistics Statistics { get; set; } = new();
}

/// <summary>
/// A dimension of belief (e.g., political, religious, ethical orientation)
/// </summary>
public class BeliefDimension
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // Political, Religious, Ethical, Metaphysical, etc.
    
    /// <summary>
    /// Position on this dimension (-1 to 1 scale, or null if unclear)
    /// </summary>
    public double? Position { get; set; }
    
    /// <summary>
    /// Confidence in this position (0-1)
    /// </summary>
    public double Confidence { get; set; }
    
    /// <summary>
    /// Standard deviation / uncertainty
    /// </summary>
    public double Uncertainty { get; set; }
    
    /// <summary>
    /// Evidence supporting this inference
    /// </summary>
    public List<string> EvidenceIds { get; set; } = new(); // References to interaction IDs
    
    /// <summary>
    /// Number of data points contributing to this dimension
    /// </summary>
    public int SampleSize { get; set; }
}

/// <summary>
/// An inferred value the user holds
/// </summary>
public class InferredValue
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Importance score (0-10)
    /// </summary>
    public double ImportanceScore { get; set; }
    
    /// <summary>
    /// Confidence in this inference (0-1)
    /// </summary>
    public double Confidence { get; set; }
    
    /// <summary>
    /// Supporting evidence
    /// </summary>
    public List<string> EvidenceIds { get; set; } = new();
}

/// <summary>
/// Moral Foundations Theory scores (Jonathan Haidt)
/// </summary>
public class MoralFoundationsProfile
{
    // Each foundation scored 0-10 with confidence intervals
    public Foundation Care { get; set; } = new();
    public Foundation Fairness { get; set; } = new();
    public Foundation Loyalty { get; set; } = new();
    public Foundation Authority { get; set; } = new();
    public Foundation Sanctity { get; set; } = new();
    public Foundation Liberty { get; set; } = new();
}

public class Foundation
{
    public double Score { get; set; }
    public double Confidence { get; set; }
    public double StandardError { get; set; }
}

/// <summary>
/// Statistical metadata about the belief model
/// </summary>
public class ModelStatistics
{
    public double Entropy { get; set; }           // Information entropy
    public double Consistency { get; set; }       // Internal consistency (0-1)
    public int TotalEvidence { get; set; }        // Total data points
    public double SignalToNoise { get; set; }     // Quality metric
    public DateTime LastUpdated { get; set; }
    
    /// <summary>
    /// Areas where we need more data
    /// </summary>
    public List<string> UncertainAreas { get; set; } = new();
    
    /// <summary>
    /// Contradictions detected in responses
    /// </summary>
    public List<string> DetectedContradictions { get; set; } = new();
}
