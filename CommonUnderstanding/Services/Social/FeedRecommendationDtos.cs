using System.ComponentModel.DataAnnotations;

namespace CommonUnderstanding.Services.Social;

public record UserFeedPreferencesDto(
    double InterestWeight,
    double GrowthWeight,
    double CollectiveWeight,
    double AdventureRate,
    double ChallengeRate,
    double RecencyBias,
    bool IncludeAIGenerated,
    bool EvidencePreferred,
    string ActivePresetName)
{
    public static UserFeedPreferencesDto Default { get; } = new(
        0.5, 0.25, 0.25, 0.1, 0.1, 0.5, true, false, "Balanced");
}

public sealed class UpdateFeedPreferencesDto : IValidatableObject
{
    [Range(0, 1)] public double InterestWeight { get; set; } = 0.5;
    [Range(0, 1)] public double GrowthWeight { get; set; } = 0.25;
    [Range(0, 1)] public double CollectiveWeight { get; set; } = 0.25;
    [Range(0, 0.5)] public double AdventureRate { get; set; } = 0.1;
    [Range(0, 0.5)] public double ChallengeRate { get; set; } = 0.1;
    [Range(0, 1)] public double RecencyBias { get; set; } = 0.5;
    public bool IncludeAIGenerated { get; set; } = true;
    public bool EvidencePreferred { get; set; }
    [Required, MaxLength(50)] public string ActivePresetName { get; set; } = "Balanced";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (InterestWeight + GrowthWeight + CollectiveWeight <= 0)
            yield return new ValidationResult(
                "At least one recommendation lane must have weight.",
                [nameof(InterestWeight), nameof(GrowthWeight), nameof(CollectiveWeight)]);
    }
}

public record FeedEngagementDto(
    Guid ArgumentId,
    bool Clicked,
    bool Voted,
    bool Commented,
    [property: Range(0, 3_600_000)] int DwellMs);

public record RecommendedFeedResultDto(
    UserFeedPreferencesDto Preferences,
    List<RecommendedFeedItemDto> Items);

public record RecommendedFeedItemDto(
    FeedItemDto Argument,
    double InterestScore,
    double GrowthScore,
    double CollectiveScore,
    double RecommendationScore,
    string Lane,
    string Reason);