using System.ComponentModel.DataAnnotations;

namespace CommonUnderstanding.Models.Social;

public class ArgumentRecommendationFeature
{
    public Guid ArgumentId { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsEligible { get; set; }
    public bool IsAIGenerated { get; set; }
    public string TagsJson { get; set; } = "[]";
    public string SchwartzValuesJson { get; set; } = "[]";
    public bool HasCitedEvidence { get; set; }
    public double WilsonScore { get; set; }
    public double HotScore { get; set; }
    public int PositiveVoterBreadth { get; set; }
    public int LinkCount { get; set; }
    public int ContradictionCount { get; set; }
    public double StructuralScore { get; set; }
    public double CollectiveScore { get; set; }
    public int FeatureVersion { get; set; } = 1;
    public DateTime ComputedAt { get; set; }
}

public class ArgumentRecommendationTag
{
    public Guid ArgumentId { get; set; }

    [MaxLength(100)]
    public string Tag { get; set; } = null!;
}

public class UserRecommendationProfile
{
    [Key, MaxLength(450)]
    public string UserId { get; set; } = null!;

    public string SchwartzValuesJson { get; set; } = "[]";
    public string ExpertiseDomainsJson { get; set; } = "[]";
    public DateTime? HistoryWatermark { get; set; }
    public int FeatureVersion { get; set; } = 1;
    public DateTime ComputedAt { get; set; }
}

public class UserRecommendationTopicAffinity
{
    [MaxLength(450)]
    public string UserId { get; set; } = null!;

    [MaxLength(100)]
    public string Topic { get; set; } = null!;

    public double Affinity { get; set; }
    public DateTime LastEngagedAt { get; set; }
    public DateTime ComputedAt { get; set; }
}

public class CommunityRecommendationFeature
{
    [MaxLength(20)]
    public string ScopeType { get; set; } = null!;

    [MaxLength(100)]
    public string ScopeKey { get; set; } = null!;

    public double TrendingScore { get; set; }
    public double UnderexposedQualityScore { get; set; }
    public double UnresolvedContradictionScore { get; set; }
    public DateTime ComputedAt { get; set; }
}

public class RecommendationArgumentWork
{
    public Guid ArgumentId { get; set; }
    public DateTime RequestedAt { get; set; }
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
}

public class RecommendationUserWork
{
    [Key, MaxLength(450)]
    public string UserId { get; set; } = null!;

    public DateTime RequestedAt { get; set; }
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
}