using CommonUnderstanding.Models.Social;

namespace CommonUnderstanding.Services.Social;

/// <summary>
/// Pure mathematical scoring algorithms for the epistemic voting system.
/// All methods are static and have no side effects — safe to test in isolation.
/// </summary>
public static class ScoringAlgorithms
{
    // ── Wilson Score Lower Bound ──────────────────────────────────────────────

    /// <summary>
    /// Computes the Wilson score lower bound for a 95% confidence interval.
    /// Used for the "Top" sort and stored as WilsonScore on SocialArgument.
    /// </summary>
    /// <param name="upvotes">Number of positive votes (raw count, not weighted).</param>
    /// <param name="total">Total votes cast (up + down, excluding Abstain).</param>
    /// <returns>Wilson score lower bound in [0, 1].</returns>
    public static double WilsonScoreLowerBound(int upvotes, int total)
    {
        if (total == 0) return 0.0;

        const double z = 1.96; // 95% confidence z-score
        double p = (double)upvotes / total;
        double denominator = 1.0 + z * z / total;
        double centre = p + z * z / (2 * total);
        double margin = z * Math.Sqrt(p * (1 - p) / total + z * z / (4.0 * total * total));

        return (centre - margin) / denominator;
    }

    // ── Epistemic-Weighted Vote Count ─────────────────────────────────────────

    /// <summary>
    /// Sums epistemic weights for votes of the given direction.
    /// A voter with EpistemicScore = 5.0 contributes <paramref name="maxMultiplier"/> times as much as a baseline voter.
    /// </summary>
    public static double EpistemicWeightedVoteCount(
        IEnumerable<ArgumentVote> votes,
        VoteValue direction,
        double maxMultiplier = 2.0)
    {
        return votes
            .Where(v => v.Vote == direction)
            .Sum(v => 1.0 + (v.EpistemicWeight - 1.0) * (maxMultiplier - 1.0));
    }

    /// <summary>
    /// Converts an EpistemicScore (0–5) to a vote weight in [1.0, maxMultiplier].
    /// </summary>
    public static double EpistemicScoreToWeight(double epistemicScore, double maxMultiplier = 2.0)
    {
        // Clamp input to [0, 5]
        epistemicScore = Math.Clamp(epistemicScore, 0.0, 5.0);
        return 1.0 + (epistemicScore / 5.0) * (maxMultiplier - 1.0);
    }

    // ── Hot Score (Time Decay) ────────────────────────────────────────────────

    /// <summary>
    /// Reddit-style hot score with configurable gravity.
    /// Higher gravity causes faster decay of older posts.
    /// </summary>
    /// <param name="weightedUpvotes">Epistemic-weighted upvote sum.</param>
    /// <param name="weightedDownvotes">Epistemic-weighted downvote sum.</param>
    /// <param name="createdAt">UTC creation time of the argument.</param>
    /// <param name="gravity">Decay exponent. Default 1.8 (configurable).</param>
    public static double HotScore(
        double weightedUpvotes,
        double weightedDownvotes,
        DateTime createdAt,
        double gravity = 1.8)
    {
        double netVotes = weightedUpvotes - weightedDownvotes;
        double ageHours = (DateTime.UtcNow - createdAt).TotalHours;
        // Age floor of 2 hours prevents division by zero and gives new posts a boost
        return netVotes / Math.Pow(ageHours + 2.0, gravity);
    }

    // ── Controversy Score ─────────────────────────────────────────────────────

    /// <summary>
    /// Controversy is high when both up and down weighted votes are large and roughly equal.
    /// Used for the "Controversial" feed sort.
    /// </summary>
    public static double ControversyScore(double weightedUpvotes, double weightedDownvotes)
    {
        if (weightedUpvotes <= 0 || weightedDownvotes <= 0) return 0.0;

        double magnitude = weightedUpvotes + weightedDownvotes;
        double balance = Math.Min(weightedUpvotes, weightedDownvotes)
                       / Math.Max(weightedUpvotes, weightedDownvotes);

        return magnitude * balance;
    }

    // ── Epistemic Score Computation ───────────────────────────────────────────

    /// <summary>
    /// Recomputes a user's epistemic score for a domain from their vote history.
    /// Formula: clamp(VoteAccuracy * 2.5 + ContributionQuality * 2.5, 0, 5)
    /// </summary>
    /// <param name="voteAccuracy">Fraction of votes that matched community consensus (0–1).</param>
    /// <param name="avgContributionWilsonScore">Average Wilson score of user's submitted arguments in domain.</param>
    public static double ComputeEpistemicScore(double voteAccuracy, double avgContributionWilsonScore)
    {
        double raw = voteAccuracy * 2.5 + avgContributionWilsonScore * 2.5;
        return Math.Clamp(raw, 0.0, 5.0);
    }

    // ── Convergence Score ─────────────────────────────────────────────────────

    /// <summary>
    /// Computes the weighted convergence score between two Worldviews.
    /// Score ≥ 0.7 → Strong; 0.4–0.69 → Partial; &lt; 0.4 → Divergent.
    /// </summary>
    public static double ConvergenceScore(double semanticCosine, double argumentJaccard, double schwartzCosine)
    {
        return 0.4 * semanticCosine + 0.3 * argumentJaccard + 0.3 * schwartzCosine;
    }

    /// <summary>
    /// Classifies a convergence score into a human-readable tier.
    /// </summary>
    public static string ClassifyConvergence(double score) => score switch
    {
        >= 0.7 => "StrongConvergence",
        >= 0.4 => "PartialConvergence",
        _ => "Divergent"
    };

    /// <summary>
    /// Cosine similarity between two vectors (dot product / product of magnitudes).
    /// Returns 0.0 if either vector is zero.
    /// </summary>
    public static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0) return 0.0;

        double dot = 0, magA = 0, magB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        if (magA == 0 || magB == 0) return 0.0;
        return dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
    }

    /// <inheritdoc cref="CosineSimilarity(float[], float[])"/>
    public static double CosineSimilarity(double[] a, double[] b)
    {
        if (a.Length != b.Length || a.Length == 0) return 0.0;

        double dot = 0, magA = 0, magB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        if (magA == 0 || magB == 0) return 0.0;
        return dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
    }

    /// <summary>
    /// Jaccard index between two sets of Guids.
    /// J(A, B) = |A ∩ B| / |A ∪ B|
    /// </summary>
    public static double JaccardIndex(IEnumerable<Guid> setA, IEnumerable<Guid> setB)
    {
        var hashA = new HashSet<Guid>(setA);
        var hashB = new HashSet<Guid>(setB);

        int intersection = hashA.Count(x => hashB.Contains(x));
        int union = hashA.Count + hashB.Count - intersection;

        return union == 0 ? 0.0 : (double)intersection / union;
    }

    /// <summary>
    /// Computes the centroid (element-wise average) of a list of embedding vectors.
    /// Returns null if the list is empty.
    /// </summary>
    public static float[]? ComputeCentroid(IEnumerable<float[]?> embeddings)
    {
        var valid = embeddings.Where(e => e is not null).Select(e => e!).ToList();
        if (valid.Count == 0) return null;

        int dims = valid[0].Length;
        var centroid = new float[dims];

        foreach (var vec in valid)
        {
            if (vec.Length != dims) continue;
            for (int i = 0; i < dims; i++)
                centroid[i] += vec[i];
        }

        for (int i = 0; i < dims; i++)
            centroid[i] /= valid.Count;

        return centroid;
    }
}
