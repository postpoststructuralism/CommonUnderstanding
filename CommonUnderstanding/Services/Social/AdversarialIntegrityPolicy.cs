namespace CommonUnderstanding.Services.Social;

public sealed record BehavioralIntegritySignals(
    int AccountAgeDays,
    int PostsLastHour,
    double CrossAccountArgumentSimilarity,
    int VotesLastHour,
    double VoteTargetConcentration);

public sealed record IntegrityRiskAssessment(
    double RiskScore,
    double InfluenceMultiplier,
    IReadOnlyList<string> Signals)
{
    public bool RequiresReview => RiskScore >= 0.65;
}

public static class AdversarialIntegrityPolicy
{
    public static IntegrityRiskAssessment Assess(BehavioralIntegritySignals input)
    {
        var signals = new List<string>();
        double risk = 0;

        if (input.AccountAgeDays < 2)
        {
            risk += 0.2;
            signals.Add("new-account");
        }

        if (input.PostsLastHour >= 8)
        {
            risk += 0.2;
            signals.Add("high-posting-cadence");
        }

        if (input.CrossAccountArgumentSimilarity >= 0.92)
        {
            risk += 0.3;
            signals.Add("cross-account-content-similarity");
        }

        if (input.VotesLastHour >= 20)
        {
            risk += 0.15;
            signals.Add("high-voting-cadence");
        }

        if (input.VotesLastHour >= 8 && input.VoteTargetConcentration >= 0.8)
        {
            risk += 0.25;
            signals.Add("concentrated-vote-pattern");
        }

        risk = Math.Clamp(risk, 0, 1);
        double multiplier = risk switch
        {
            >= 0.85 => 0.2,
            >= 0.65 => 0.4,
            >= 0.4 => 0.7,
            _ => 1.0
        };

        return new IntegrityRiskAssessment(risk, multiplier, signals);
    }
}