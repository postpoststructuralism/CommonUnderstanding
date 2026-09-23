using CommonUnderstanding.Services.Social.Plugins;

namespace CommonUnderstanding.Tests.Services;

public sealed class SteelmanServiceTests
{
    [Fact]
    public void ParseSuggestion_AcceptsMarkdownJsonAndBoundsCollections()
    {
        var argumentId = Guid.NewGuid();
        var content = """
            ```json
            {
              "counterPosition": "Targeted safeguards are preferable",
              "claim": "A narrower intervention can address the risk without a broad prohibition.",
              "warrant": "Proportionate controls preserve legitimate benefits while reducing demonstrated harms.",
              "strongestEvidenceNeeded": ["Comparative outcome data", "Implementation costs", "Failure rates", "Distributional effects", "Long-term outcomes", "Ignored sixth item"],
              "sharedValues": ["Security", "SelfDirection", "security"],
              "concessions": ["The source identifies a real risk."],
              "uncertainties": ["The effectiveness of narrower controls is context dependent."],
              "fairnessRationale": "It accepts the stated risk and presents the strongest proportionate alternative."
            }
            ```
            """;

        var result = SteelmanService.ParseSuggestion(argumentId, content);

        Assert.NotNull(result);
        Assert.Equal(argumentId, result.SourceArgumentId);
        Assert.True(result.IsAIGenerated);
        Assert.Equal(5, result.StrongestEvidenceNeeded.Count);
        Assert.Equal(["Security", "SelfDirection"], result.SharedValues);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"counterPosition\":\"Position\",\"claim\":\"Claim\",\"warrant\":\"Warrant\"}")]
    public void ParseSuggestion_RejectsMalformedOrIncompleteOutput(string content)
    {
        Assert.Null(SteelmanService.ParseSuggestion(Guid.NewGuid(), content));
    }

    [Fact]
    public void ParseSuggestion_RejectsOversizedRequiredFields()
    {
        var content = $$"""
            {
              "counterPosition": "Position",
              "claim": "{{new string('x', 1001)}}",
              "warrant": "Warrant",
              "fairnessRationale": "Rationale"
            }
            """;

        Assert.Null(SteelmanService.ParseSuggestion(Guid.NewGuid(), content));
    }
}