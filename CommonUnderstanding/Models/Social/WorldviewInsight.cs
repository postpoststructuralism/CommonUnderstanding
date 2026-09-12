namespace CommonUnderstanding.Models.Social;

public class WorldviewInsight
{
    public bool HasBeliefProfile { get; init; }
    public int InteractionCount { get; init; }
    public int SubmittedArgumentCount { get; init; }
    public double ProfileConfidence { get; init; }
    public string Summary { get; init; } = string.Empty;
    public string MappingNarrative { get; init; } = string.Empty;
    public List<WorldviewInsightValue> TopValues { get; init; } = new();
    public List<WorldviewInsightDimension> Dimensions { get; init; } = new();
    public List<WorldviewInsightArgument> Arguments { get; init; } = new();
    public List<WorldviewCommunityView> CommunityViews { get; init; } = new();
    public List<WorldviewInsightMatch> CanonicalMatches { get; init; } = new();
    public List<WorldviewInsightPeer> Peers { get; init; } = new();
    public string HorizontalAxis { get; init; } = "Belief dimension 1";
    public string VerticalAxis { get; init; } = "Belief dimension 2";
    public double UserX { get; init; }
    public double UserY { get; init; }
    public double[] MoralFoundations { get; init; } = new double[6];
}

public record WorldviewInsightValue(string Name, double Importance, double Confidence);
public record WorldviewInsightDimension(string Name, string Category, double Position, double Confidence);
public record WorldviewInsightArgument(
    string Id,
    string DetailController,
    string DetailAction,
    string Title,
    string Claim,
    string[] Values,
    DateTime SubmittedAt);
public record WorldviewInsightMatch(string Name, string Category, double Match, string[] SharedValues, string[] Differences);
public record WorldviewInsightPeer(string Label, double Similarity, double X, double Y);
public record WorldviewCommunityView(
    string ArgumentId,
    string Title,
    string InterestPrevalence,
    int SimilarArgumentCount,
    int SimilarAuthorCount,
    int CommunityAuthorCount,
    string OpinionStanding,
    double? SupportShare,
    int VoteCount,
    string ConfidenceNote);