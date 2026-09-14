using CommonUnderstanding.Models;
using CommonUnderstanding.Services;

namespace CommonUnderstanding.Tests.Services;

public sealed class AdjudicationSourceTrustTests
{
    [Fact]
    public void CalculateSourceTrustMultiplier_UsesIndependentReliabilityScore()
    {
        var source = new Source
        {
            ReliabilityScore = 0.8,
            VerificationStatus = SourceVerificationStatus.Verified
        };

        Assert.Equal(0.85, AdjudicationEngine.CalculateSourceTrustMultiplier(source), 10);
    }

    [Fact]
    public void CalculateSourceTrustMultiplier_RetractionAlmostEliminatesWeight()
    {
        var source = new Source
        {
            ReliabilityScore = 1,
            VerificationStatus = SourceVerificationStatus.Retracted
        };

        Assert.Equal(0.02, AdjudicationEngine.CalculateSourceTrustMultiplier(source), 10);
    }

    [Fact]
    public void CalculateSourceTrustMultiplier_LegacyUnregisteredSourceIsConservative()
    {
        Assert.Equal(0.5, AdjudicationEngine.CalculateSourceTrustMultiplier(null), 10);
    }
}