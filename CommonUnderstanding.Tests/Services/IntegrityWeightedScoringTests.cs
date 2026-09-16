using CommonUnderstanding.Models.Social;
using CommonUnderstanding.Services.Social;

namespace CommonUnderstanding.Tests.Services;

public sealed class IntegrityWeightedScoringTests
{
    [Fact]
    public void WeightedVoteCount_OrdinaryVotePreservesEpistemicWeight()
    {
        var votes = new[]
        {
            new ArgumentVote
            {
                Vote = VoteValue.Up,
                EpistemicWeight = 1.5,
                IntegrityMultiplier = 1.0
            }
        };

        Assert.Equal(1.5, ScoringAlgorithms.EpistemicWeightedVoteCount(votes, VoteValue.Up));
    }

    [Fact]
    public void WeightedVoteCount_SuspiciousVoteHasReducedInfluence()
    {
        var votes = new[]
        {
            new ArgumentVote
            {
                Vote = VoteValue.Up,
                EpistemicWeight = 1.5,
                IntegrityMultiplier = 0.2
            }
        };

        Assert.Equal(0.3, ScoringAlgorithms.EpistemicWeightedVoteCount(votes, VoteValue.Up), 10);
    }

    [Fact]
    public void WilsonScore_AcceptsFractionalEffectiveVotes()
    {
        double ordinary = ScoringAlgorithms.WilsonScoreLowerBound(8.0, 10.0);
        double reduced = ScoringAlgorithms.WilsonScoreLowerBound(1.6, 3.6);

        Assert.True(reduced < ordinary);
        Assert.InRange(reduced, 0, 1);
    }
}