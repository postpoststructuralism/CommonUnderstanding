using CommonUnderstanding.Models;

namespace CommonUnderstanding.Models.Social;

public sealed class ClaimStateViewModel
{
    public required SocialArgument FocusArgument { get; init; }
    public required SocialProposition Proposition { get; init; }
    public required IReadOnlyList<SocialArgument> SupportingArguments { get; init; }
    public required IReadOnlyList<SocialArgument> OpposingArguments { get; init; }
    public required IReadOnlyList<SocialArgument> ContributionHistory { get; init; }
    public required IReadOnlyList<EvidenceItem> Evidence { get; init; }
    public required IReadOnlyList<Assumption> RemainingQuestions { get; init; }
    public Argument? SourceArgument { get; init; }
    public AdjudicationSummary? Adjudication { get; init; }
    public ArgumentVote? UserVote { get; init; }
}