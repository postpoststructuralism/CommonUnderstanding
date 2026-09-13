using CommonUnderstanding.Models;
using CommonUnderstanding.Services;

namespace CommonUnderstanding.Tests.Services;

public sealed class ReferenceRelationshipClassifierTests
{
    [Theory]
    [InlineData(0.85, "supports")]
    [InlineData(0.84, "refines")]
    [InlineData(0.65, "refines")]
    [InlineData(0.64, "qualifies")]
    [InlineData(0.45, "qualifies")]
    [InlineData(0.44, "assumes")]
    public void Classify_UsesSharedSimilarityThresholds(double similarity, string expected)
    {
        var relationship = ReferenceRelationshipClassifier.Classify(similarity);

        Assert.Equal(expected, relationship);
    }

    [Theory]
    [InlineData(0.29)]
    [InlineData(-0.50)]
    public void Classify_LowSimilarityWithSharedContextContradicts(double similarity)
    {
        var relationship = ReferenceRelationshipClassifier.Classify(
            similarity,
            sharesContext: true);

        Assert.Equal("contradicts", relationship);
    }

    [Fact]
    public void Classify_OpposingStatusesWithSharedContextContradictBelowThreshold()
    {
        var relationship = ReferenceRelationshipClassifier.Classify(
            0.40,
            PropositionStatus.Settled,
            PropositionStatus.Contested,
            sharesContext: true);

        Assert.Equal("contradicts", relationship);
    }

    [Fact]
    public void Classify_OpposingStatusesWithoutSharedContextDoNotContradict()
    {
        var relationship = ReferenceRelationshipClassifier.Classify(
            0.40,
            PropositionStatus.Settled,
            PropositionStatus.Contested);

        Assert.Equal("assumes", relationship);
    }

    [Fact]
    public void CosineSimilarity_ReturnsExpectedValues()
    {
        Assert.Equal(1, ReferenceRelationshipClassifier.CosineSimilarity([1, 2], [1, 2]), 10);
        Assert.Equal(0, ReferenceRelationshipClassifier.CosineSimilarity([1, 0], [0, 1]), 10);
        Assert.Equal(-1, ReferenceRelationshipClassifier.CosineSimilarity([1, 0], [-1, 0]), 10);
    }

    [Fact]
    public void CosineSimilarity_InvalidOrZeroVectorsReturnZero()
    {
        Assert.Equal(0, ReferenceRelationshipClassifier.CosineSimilarity([], []));
        Assert.Equal(0, ReferenceRelationshipClassifier.CosineSimilarity([0, 0], [1, 1]));
        Assert.Equal(0, ReferenceRelationshipClassifier.CosineSimilarity([1], [1, 1]));
    }
}
