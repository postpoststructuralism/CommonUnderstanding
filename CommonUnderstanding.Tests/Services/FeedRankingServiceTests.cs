using CommonUnderstanding.Services.Social;

namespace CommonUnderstanding.Tests.Services;

public class FeedRankingServiceTests
{
    [Fact]
    public void Blend_UsesConfiguredLaneWeights()
    {
        var preferences = UserFeedPreferencesDto.Default with
        {
            InterestWeight = 0.5,
            GrowthWeight = 0.25,
            CollectiveWeight = 0.25
        };

        var score = FeedRankingService.Blend(preferences, 0.8, 0.4, 0.2);

        Assert.Equal(0.55, score, precision: 10);
    }

    [Fact]
    public void ScoreInterest_UsesMatchingAffinityAndColdStartFloor()
    {
        var affinities = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["housing"] = 2,
            ["energy"] = 1
        };

        Assert.Equal(0.75, FeedRankingService.ScoreInterest(["housing", "energy"], affinities, 2), precision: 10);
        Assert.Equal(0, FeedRankingService.ScoreInterest(["health"], affinities, 2), precision: 10);
        Assert.Equal(0.2, FeedRankingService.ScoreInterest(["health"], new Dictionary<string, double>(), 1), precision: 10);
    }

    [Fact]
    public void TagSimilarity_IsCaseInsensitiveJaccardSimilarity()
    {
        var similarity = FeedRankingService.TagSimilarity(["Housing", "Tax"], ["housing", "Energy"]);

        Assert.Equal(1d / 3d, similarity, precision: 10);
    }

    [Fact]
    public void MergeCandidateIds_DeduplicatesAndHonorsLimit()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();

        var merged = FeedRankingService.MergeCandidateIds([[first, second], [second, third]], 2);

        Assert.Equal([first, second], merged);
    }
}