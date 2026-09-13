using CommonUnderstanding.Models;

namespace CommonUnderstanding.Services;

public static class ReferenceRelationshipClassifier
{
    public static string Classify(
        double similarity,
        PropositionStatus firstStatus = PropositionStatus.Unevaluated,
        PropositionStatus secondStatus = PropositionStatus.Unevaluated,
        bool sharesContext = false)
    {
        if (similarity >= 0.85) return "supports";
        if (similarity >= 0.65) return "refines";
        if (similarity >= 0.45) return "qualifies";
        if (sharesContext && similarity < 0.30) return "contradicts";

        if (sharesContext && similarity < 0.55 &&
            ((firstStatus == PropositionStatus.Contested && secondStatus == PropositionStatus.Settled) ||
             (firstStatus == PropositionStatus.Settled && secondStatus == PropositionStatus.Contested)))
            return "contradicts";

        return "assumes";
    }

    public static double CosineSimilarity(float[] first, float[] second)
    {
        if (first.Length != second.Length || first.Length == 0) return 0;

        double dot = 0;
        double firstMagnitude = 0;
        double secondMagnitude = 0;
        for (var index = 0; index < first.Length; index++)
        {
            dot += first[index] * second[index];
            firstMagnitude += first[index] * first[index];
            secondMagnitude += second[index] * second[index];
        }

        return firstMagnitude == 0 || secondMagnitude == 0
            ? 0
            : dot / (Math.Sqrt(firstMagnitude) * Math.Sqrt(secondMagnitude));
    }
}