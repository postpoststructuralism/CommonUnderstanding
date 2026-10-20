using System.Globalization;
using System.Text.Json;
using CommonUnderstanding.Models.Social;

namespace CommonUnderstanding.Tests.Services;

public sealed class WorldviewAffinityScaleTests
{
    [Theory]
    [InlineData(0, 0, "0%")]
    [InlineData(50, 0.5, "50%")]
    [InlineData(100, 1, "100%")]
    public void PercentageConversionPreservesScaleThroughSerializationAndFormatting(
        double percentage, double fraction, string formatted)
    {
        var match = new WorldviewInsightMatch("Fixture", "Fixture",
            WorldviewInsightMatch.PercentageToFraction(percentage), [], []);
        var restored = JsonSerializer.Deserialize<WorldviewInsightMatch>(JsonSerializer.Serialize(match))!;
        Assert.Equal(fraction, restored.Match);
        Assert.Equal(percentage, restored.Match * 100);
        Assert.Equal(formatted, restored.Match.ToString("P0", CultureInfo.GetCultureInfo("en-US")));
    }

    [Fact]
    public void AggregationUsesFractions()
    {
        Assert.Equal(0.5, new[] { 0d, 50d, 100d }
            .Select(WorldviewInsightMatch.PercentageToFraction).Average());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidUpstreamPercentagesAreRejectedNotClamped(double percentage)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => WorldviewInsightMatch.PercentageToFraction(percentage));
    }
}