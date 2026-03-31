namespace CommonUnderstanding.Models;

/// <summary>
/// Represents a single interaction with a user (question asked, response received)
/// </summary>
public class UserInteraction
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Type of interaction
    /// </summary>
    public InteractionType Type { get; set; }
    
    /// <summary>
    /// The content presented to the user
    /// </summary>
    public InteractionContent Content { get; set; } = new();
    
    /// <summary>
    /// The user's response
    /// </summary>
    public UserResponse Response { get; set; } = new();
    
    /// <summary>
    /// AI analysis of the response
    /// </summary>
    public ResponseAnalysis Analysis { get; set; } = new();
    
    /// <summary>
    /// How long the user took to respond (milliseconds)
    /// </summary>
    public long ResponseTimeMs { get; set; }
    
    /// <summary>
    /// What we were testing with this interaction
    /// </summary>
    public List<string> TargetedDimensions { get; set; } = new();
}

public enum InteractionType
{
    OpenEndedQuestion,      // "What matters most to you in life?"
    ScaleQuestion,          // "Rate 1-10: Individual freedom vs. collective good"
    MoralDilemma,          // Trolley problem, etc.
    ScenarioReaction,       // "You witness X happening. What do you do?"
    StatementAgreement,     // "Agree/Disagree: The ends justify the means"
    ValueRanking,          // Rank these 5 values in order of importance
    BinaryChoice,          // "Would you rather X or Y?"
    EmotionalPrompt        // Content designed to elicit emotional response
}

/// <summary>
/// The content presented during an interaction
/// </summary>
public class InteractionContent
{
    public string Question { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public InteractionFormat Format { get; set; }
    
    /// <summary>
    /// For structured questions (scale, ranking, etc.)
    /// </summary>
    public List<string>? Options { get; set; }
    
    /// <summary>
    /// For scale questions
    /// </summary>
    public int? MinValue { get; set; }
    public int? MaxValue { get; set; }
    public string? MinLabel { get; set; }
    public string? MaxLabel { get; set; }
}

public enum InteractionFormat
{
    OpenText,
    Scale,
    MultipleChoice,
    Ranking,
    Binary,
    ThumbsVote         // 👍 or 👎
}

/// <summary>
/// User's response to an interaction
/// </summary>
public class UserResponse
{
    public string RawText { get; set; } = string.Empty;
    
    /// <summary>
    /// For structured responses
    /// </summary>
    public double? NumericValue { get; set; }
    public List<string>? SelectedOptions { get; set; }
    public List<string>? Rankings { get; set; }
    
    /// <summary>
    /// Detected emotional indicators
    /// </summary>
    public EmotionalMarkers Emotion { get; set; } = new();
}

public class EmotionalMarkers
{
    public double Intensity { get; set; }        // 0-1
    public double Certainty { get; set; }        // 0-1 (how confident they seem)
    public List<string> DetectedEmotions { get; set; } = new(); // anger, compassion, disgust, etc.
    public double ConflictIndicator { get; set; } // Signs of internal conflict
}

/// <summary>
/// AI analysis of a user's response
/// </summary>
public class ResponseAnalysis
{
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Extracted insights about belief dimensions
    /// </summary>
    public List<DimensionUpdate> DimensionUpdates { get; set; } = new();
    
    /// <summary>
    /// Values implied or stated
    /// </summary>
    public List<string> ImpliedValues { get; set; } = new();
    
    /// <summary>
    /// Detected reasoning patterns
    /// </summary>
    public List<string> ReasoningPatterns { get; set; } = new();
    
    /// <summary>
    /// Moral foundations activated
    /// </summary>
    public Dictionary<string, double> MoralFoundationScores { get; set; } = new();
    
    /// <summary>
    /// Confidence in this analysis (0-1)
    /// </summary>
    public double AnalysisConfidence { get; set; }
    
    /// <summary>
    /// Full AI narrative analysis
    /// </summary>
    public string NarrativeAnalysis { get; set; } = string.Empty;
    
    /// <summary>
    /// Suggested follow-up areas to explore
    /// </summary>
    public List<string> SuggestedFollowUps { get; set; } = new();
}

/// <summary>
/// An update to a belief dimension based on new evidence
/// </summary>
public class DimensionUpdate
{
    public string DimensionName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    
    /// <summary>
    /// New position estimate
    /// </summary>
    public double? Position { get; set; }
    
    /// <summary>
    /// Confidence change (delta)
    /// </summary>
    public double ConfidenceChange { get; set; }
    
    /// <summary>
    /// Weight of this evidence
    /// </summary>
    public double EvidenceWeight { get; set; }
    
    /// <summary>
    /// Reasoning for this update
    /// </summary>
    public string Reasoning { get; set; } = string.Empty;
}
