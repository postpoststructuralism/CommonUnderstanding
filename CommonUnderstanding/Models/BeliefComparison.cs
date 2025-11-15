namespace CommonUnderstanding.Models;

/// <summary>
/// Represents the analysis result of comparing two belief systems
/// </summary>
public class BeliefComparison
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string BeliefSystem1Id { get; set; } = string.Empty;
    public string BeliefSystem2Id { get; set; } = string.Empty;
    public string BeliefSystem1Name { get; set; } = string.Empty;
    public string BeliefSystem2Name { get; set; } = string.Empty;
    
    public List<CommonGround> AreasOfOverlap { get; set; } = new();
    public List<Divergence> AreasOfDivergence { get; set; } = new();
    public List<NonZeroSumOpportunity> NonZeroSumOpportunities { get; set; } = new();
    
    public double OverlapScore { get; set; } // 0-100
    public string SynthesisSummary { get; set; } = string.Empty;
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents an area where two belief systems share common ground
/// </summary>
public class CommonGround
{
    public string Theme { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> SharedValues { get; set; } = new();
    public List<string> SharedPrinciples { get; set; } = new();
    public int StrengthScore { get; set; } // 1-10 scale
}

/// <summary>
/// Represents an area where two belief systems diverge
/// </summary>
public class Divergence
{
    public string Theme { get; set; } = string.Empty;
    public string BeliefSystem1Perspective { get; set; } = string.Empty;
    public string BeliefSystem2Perspective { get; set; } = string.Empty;
    public bool IsFundamental { get; set; }
    public string PotentialBridgeIdeas { get; set; } = string.Empty;
}

/// <summary>
/// Represents an opportunity for non-zero-sum collaboration
/// </summary>
public class NonZeroSumOpportunity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string BenefitToBeliefSystem1 { get; set; } = string.Empty;
    public string BenefitToBeliefSystem2 { get; set; } = string.Empty;
    public List<string> ActionableSteps { get; set; } = new();
    public int PotentialImpactScore { get; set; } // 1-10 scale
}

/// <summary>
/// Represents a comparison between two canonical belief systems
/// </summary>
public class BeliefSystemComparison
{
    public string System1 { get; set; } = string.Empty;
    public string System2 { get; set; } = string.Empty;
    public double OverallSimilarity { get; set; } // 0-1 scale
    public List<string> SharedValues { get; set; } = new();
    public List<string> DifferingValues { get; set; } = new();
    public List<string> PotentialSynergies { get; set; } = new();
    public List<string> HistoricalInteractions { get; set; } = new();
}
