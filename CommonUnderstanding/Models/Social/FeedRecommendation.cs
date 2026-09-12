using System.ComponentModel.DataAnnotations;

namespace CommonUnderstanding.Models.Social;

public class UserFeedPreferences
{
    [Key, MaxLength(450)]
    public string UserId { get; set; } = null!;

    public double InterestWeight { get; set; } = 0.5;
    public double GrowthWeight { get; set; } = 0.25;
    public double CollectiveWeight { get; set; } = 0.25;
    public double AdventureRate { get; set; } = 0.1;
    public double ChallengeRate { get; set; } = 0.1;
    public double RecencyBias { get; set; } = 0.5;
    public bool IncludeAIGenerated { get; set; } = true;
    public bool EvidencePreferred { get; set; }

    [MaxLength(50)]
    public string ActivePresetName { get; set; } = "Balanced";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class FeedImpressionEvent
{
    public long Id { get; set; }

    [MaxLength(450)]
    public string UserId { get; set; } = null!;

    public Guid ArgumentId { get; set; }
    public double InterestScoreAtServe { get; set; }
    public double GrowthScoreAtServe { get; set; }
    public double CollectiveScoreAtServe { get; set; }
    public double BlendedScoreAtServe { get; set; }

    [Required, MaxLength(20)]
    public string Lane { get; set; } = null!;

    public bool Clicked { get; set; }
    public bool Voted { get; set; }
    public bool Commented { get; set; }
    public int DwellMs { get; set; }
    public DateTime ServedAt { get; set; } = DateTime.UtcNow;

    public SocialArgument Argument { get; set; } = null!;
}