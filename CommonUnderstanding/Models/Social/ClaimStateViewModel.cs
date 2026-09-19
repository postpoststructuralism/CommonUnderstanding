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

public sealed class ClaimAnalysisSummaryViewModel
{
    public required string PropositionText { get; init; }
    public required string StateLabel { get; init; }
    public required string CurrentUnderstanding { get; init; }
    public int PremiseCount { get; init; }
    public int EvidenceCount { get; init; }
    public int CriticalGapCount { get; init; }
    public double? OverallConfidence { get; init; }
    public DecisionRecommendation? Recommendation { get; init; }
}

public sealed class ClaimAnalysisDetailViewModel
{
    public Argument? SourceArgument { get; init; }
    public required string PropositionText { get; init; }
    public required IReadOnlyList<Claim> Claims { get; init; }
    public required IReadOnlyList<Proposition> Premises { get; init; }
    public required IReadOnlyList<EvidenceItem> Evidence { get; init; }
    public required IReadOnlyList<Syllogism> Syllogisms { get; init; }
    public required IReadOnlyList<Assumption> Assumptions { get; init; }
    public required IReadOnlyList<Qualifier> Qualifiers { get; init; }
    public required IReadOnlyList<Rebuttal> Rebuttals { get; init; }
    public AdjudicationSummary? Adjudication { get; init; }
}

public sealed class ClaimSocialContextViewModel
{
    public required Guid FocusArgumentId { get; init; }
    public required Guid PropositionId { get; init; }
    public required string PropositionText { get; init; }
    public required IReadOnlyList<SocialContextArgumentViewModel> SupportingArguments { get; init; }
    public required IReadOnlyList<SocialContextArgumentViewModel> OpposingArguments { get; init; }
    public required IReadOnlyList<SocialContextHistoryViewModel> ContributionHistory { get; init; }
}

public sealed class SocialContextArgumentViewModel
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public required string WarrantText { get; init; }
}

public sealed class SocialContextHistoryViewModel
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public DateTime CreatedAt { get; init; }
}