using CommonUnderstanding.Models;

namespace CommonUnderstanding.Services;

/// <summary>
/// Generates fun, bite-sized personality insights based on the user's
/// belief model and response patterns. These are shown as micro-insights
/// during the discovery process to keep users engaged.
/// </summary>
public class PersonalityInsightGenerator
{
    private readonly ILogger<PersonalityInsightGenerator> _logger;
    private readonly Random _random = new();

    public PersonalityInsightGenerator(ILogger<PersonalityInsightGenerator> logger)
    {
        _logger = logger;
    }

    // Insight templates keyed by trigger condition
    private static readonly List<InsightTemplate> Templates = new()
    {
        // High compassion insights
        new InsightTemplate
        {
            Condition = (snapshot, profile) =>
                snapshot.MoralFoundations.Care.Score > 7.5,
            Templates = new[]
            {
                "🫶 You have a deeply empathetic worldview — you genuinely care about others' wellbeing.",
                "💚 Your compassion score is off the charts. The world needs more of that.",
                "🤗 You're what psychologists call a 'high-empathy individual' — you feel others' emotions deeply."
            },
            Category = "Compassion"
        },
        // Low compassion insights
        new InsightTemplate
        {
            Condition = (snapshot, profile) =>
                snapshot.MoralFoundations.Care.Score < 3.0 && snapshot.MoralFoundations.Care.Confidence > 0.4,
            Templates = new[]
            {
                "🧠 You tend to approach moral questions with logic rather than emotion. That's rare and valuable.",
                "⚖️ You believe people should be accountable for their own outcomes — a perspective shared by many philosophers."
            },
            Category = "Pragmatism"
        },
        // High fairness insights
        new InsightTemplate
        {
            Condition = (snapshot, profile) =>
                snapshot.MoralFoundations.Fairness.Score > 7.5,
            Templates = new[]
            {
                "⚖️ Justice and fairness are clearly important to you. You'd make a great mediator.",
                "📏 You have a strong internal sense of right and wrong — a classic 'moral compass' personality."
            },
            Category = "Justice"
        },
        // High liberty insights
        new InsightTemplate
        {
            Condition = (snapshot, profile) =>
                snapshot.MoralFoundations.Liberty.Score > 7.5,
            Templates = new[]
            {
                "🦅 Personal freedom is a core value for you. You resist anything that feels like control.",
                "🔓 You're what psychologists call 'autonomy-oriented' — you thrive when you have choices."
            },
            Category = "Freedom"
        },
        // High tradition/authority
        new InsightTemplate
        {
            Condition = (snapshot, profile) =>
                snapshot.MoralFoundations.Authority.Score > 7.0 && snapshot.MoralFoundations.Sanctity.Score > 6.0,
            Templates = new[]
            {
                "🏛️ You value structure, tradition, and order — the bedrock of stable societies.",
                "📜 You have a conservative temperament: you believe proven institutions exist for good reason."
            },
            Category = "Traditional"
        },
        // Rapid responder
        new InsightTemplate
        {
            Condition = (snapshot, profile) =>
            {
                var recent = profile.Interactions.TakeLast(3).ToList();
                return recent.Count >= 3 && recent.Average(i => i.ResponseTimeMs) < 5000;
            },
            Templates = new[]
            {
                "⚡ You're a quick thinker! Your rapid responses suggest strong intuitive decision-making.",
                "🚀 You don't overthink things — you trust your gut and move forward."
            },
            Category = "Decision Style"
        },
        // Deep thinker
        new InsightTemplate
        {
            Condition = (snapshot, profile) =>
            {
                var recent = profile.Interactions.TakeLast(3).ToList();
                return recent.Count >= 3 && recent.Average(i => i.ResponseTimeMs) > 30000;
            },
            Templates = new[]
            {
                "🤔 You're a deep thinker — you take time to carefully consider your responses.",
                "📝 You reflect before answering. That thoughtfulness is a sign of high cognitive complexity."
            },
            Category = "Decision Style"
        },
        // Diverse thinker (many dimensions)
        new InsightTemplate
        {
            Condition = (snapshot, profile) =>
                snapshot.Dimensions.Count >= 8,
            Templates = new[]
            {
                "🌈 Your belief system spans many dimensions — you're what researchers call 'cognitively complex'.",
                "🎨 You don't fit neatly into any one box. Your worldview is nuanced and multi-faceted."
            },
            Category = "Complexity"
        },
        // Consistent thinker
        new InsightTemplate
        {
            Condition = (snapshot, profile) =>
                snapshot.Statistics.Consistency > 0.8 && profile.InteractionCount >= 6,
            Templates = new[]
            {
                "🎯 Your beliefs are remarkably consistent — you have a coherent worldview.",
                "🧩 Your values fit together like puzzle pieces. That internal consistency is rare."
            },
            Category = "Consistency"
        },
        // Explorer (many interactions)
        new InsightTemplate
        {
            Condition = (snapshot, profile) =>
                profile.InteractionCount >= 15,
            Templates = new[]
            {
                "🗺️ You're a true explorer of ideas — your willingness to engage deeply is admirable.",
                "🔍 Your intellectual curiosity shines through. You're not afraid to examine your own beliefs."
            },
            Category = "Engagement"
        },
        // Balanced moral foundations
        new InsightTemplate
        {
            Condition = (snapshot, profile) =>
            {
                var scores = new[]
                {
                    snapshot.MoralFoundations.Care.Score,
                    snapshot.MoralFoundations.Fairness.Score,
                    snapshot.MoralFoundations.Loyalty.Score,
                    snapshot.MoralFoundations.Authority.Score,
                    snapshot.MoralFoundations.Sanctity.Score,
                    snapshot.MoralFoundations.Liberty.Score
                };
                var avg = scores.Average();
                return scores.All(s => Math.Abs(s - avg) < 2.0) && avg > 4.0;
            },
            Templates = new[]
            {
                "☯️ Your moral foundations are beautifully balanced — you see value in all ethical dimensions.",
                "⚖️ You have a rare ability to appreciate multiple moral perspectives simultaneously."
            },
            Category = "Balance"
        },
        // Milestone: 10 questions
        new InsightTemplate
        {
            Condition = (snapshot, profile) =>
                profile.InteractionCount == 10,
            Templates = new[]
            {
                "🎉 You've reached 10 questions! Your belief profile is really taking shape now.",
                "⭐ Double digits! We're getting a clear picture of your unique worldview."
            },
            Category = "Milestone"
        },
        // Milestone: 20 questions
        new InsightTemplate
        {
            Condition = (snapshot, profile) =>
                profile.InteractionCount == 20,
            Templates = new[]
            {
                "🏆 20 questions! You're in the top tier of self-reflective individuals.",
                "💪 Most people stop at 10. You're going deep — and that says a lot about you."
            },
            Category = "Milestone"
        }
    };

    /// <summary>
    /// Generate a personality insight if any conditions are met.
    /// Returns null if no insight is triggered.
    /// </summary>
    public PersonalityInsight? GenerateInsight(BeliefSnapshot snapshot, UserProfile profile)
    {
        // Don't generate insights too frequently — only on certain interactions
        if (profile.InteractionCount < 3)
            return null;

        // Only generate on ~40% of eligible interactions to avoid spam
        if (_random.NextDouble() > 0.4)
            return null;

        var eligible = Templates
            .Where(t =>
            {
                try
                {
                    return t.Condition(snapshot, profile);
                }
                catch
                {
                    return false;
                }
            })
            .ToList();

        if (!eligible.Any())
            return null;

        // Pick a random eligible template
        var chosen = eligible[_random.Next(eligible.Count)];
        var message = chosen.Templates[_random.Next(chosen.Templates.Length)];

        _logger.LogInformation(
            "Generated personality insight for user {UserId}: [{Category}] {Message}",
            profile.Id, chosen.Category, message);

        return new PersonalityInsight
        {
            Message = message,
            Category = chosen.Category,
            GeneratedAt = DateTime.UtcNow
        };
    }

    private class InsightTemplate
    {
        public Func<BeliefSnapshot, UserProfile, bool> Condition { get; init; } = null!;
        public string[] Templates { get; init; } = Array.Empty<string>();
        public string Category { get; init; } = string.Empty;
    }
}

/// <summary>
/// A fun micro-insight about the user's personality or thinking style.
/// </summary>
public class PersonalityInsight
{
    public string Message { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}