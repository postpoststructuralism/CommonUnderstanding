using CommonUnderstanding.Services.Social;

namespace CommonUnderstanding.Tests.Services;

public sealed class AdversarialIntegrityPolicyTests
{
    [Fact]
    public void Assess_EstablishedOrdinaryAccount_PreservesInfluence()
    {
        var result = AdversarialIntegrityPolicy.Assess(new BehavioralIntegritySignals(
            AccountAgeDays: 180,
            PostsLastHour: 2,
            CrossAccountArgumentSimilarity: 0.35,
            VotesLastHour: 4,
            VoteTargetConcentration: 0.25));

        Assert.Equal(0, result.RiskScore);
        Assert.Equal(1, result.InfluenceMultiplier);
        Assert.False(result.RequiresReview);
        Assert.Empty(result.Signals);
    }

    [Fact]
    public void Assess_CoordinatedHighVelocityBehavior_ReducesInfluenceAndRequiresReview()
    {
        var result = AdversarialIntegrityPolicy.Assess(new BehavioralIntegritySignals(
            AccountAgeDays: 0,
            PostsLastHour: 12,
            CrossAccountArgumentSimilarity: 0.97,
            VotesLastHour: 24,
            VoteTargetConcentration: 0.9));

        Assert.Equal(1, result.RiskScore);
        Assert.Equal(0.2, result.InfluenceMultiplier);
        Assert.True(result.RequiresReview);
        Assert.Equal(5, result.Signals.Count);
    }

    [Fact]
    public void Assess_SingleWeakSignal_DoesNotPunishInfluence()
    {
        var result = AdversarialIntegrityPolicy.Assess(new BehavioralIntegritySignals(
            AccountAgeDays: 1,
            PostsLastHour: 1,
            CrossAccountArgumentSimilarity: 0,
            VotesLastHour: 1,
            VoteTargetConcentration: 1));

        Assert.Equal(0.2, result.RiskScore, 10);
        Assert.Equal(1, result.InfluenceMultiplier);
        Assert.False(result.RequiresReview);
    }
}