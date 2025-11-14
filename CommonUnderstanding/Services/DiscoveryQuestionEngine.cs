using Microsoft.SemanticKernel;
using CommonUnderstanding.Models;
using System.Text.Json;

namespace CommonUnderstanding.Services;

/// <summary>
/// Generates adaptive questions and scenarios to discover user beliefs
/// </summary>
public class DiscoveryQuestionEngine
{
    private readonly SemanticKernelService _kernelService;
    private readonly ILogger<DiscoveryQuestionEngine> _logger;

    public DiscoveryQuestionEngine(
        SemanticKernelService kernelService,
        ILogger<DiscoveryQuestionEngine> logger)
    {
        _kernelService = kernelService;
        _logger = logger;
    }

    /// <summary>
    /// Generate the next best question based on current knowledge and gaps
    /// </summary>
    public async Task<UserInteraction> GenerateNextQuestionAsync(UserProfile profile)
    {
        var kernel = _kernelService.GetKernel();
        
        var stage = DetermineStage(profile);
        
        // For initial survey, use predefined multiple choice questions
        if (stage == DiscoveryStage.Initial && profile.InteractionCount < 5)
        {
            return GenerateInitialSurveyQuestion(profile);
        }
        
        var uncertainAreas = profile.CurrentBeliefSnapshot?.Statistics.UncertainAreas ?? new List<string>();
        var previousQuestions = profile.Interactions.Select(i => i.Content.Question).ToList();

        var prompt = BuildQuestionPrompt(stage, uncertainAreas, previousQuestions, profile);

        _logger.LogInformation("Generating question for user {UserId} at stage {Stage}", profile.Id, stage);
        
        var result = await kernel.InvokePromptAsync(prompt);
        var questionData = result.ToString();

        _logger.LogInformation("Successfully generated question for user {UserId}", profile.Id);

        return ParseQuestionResponse(questionData, profile.Id);
    }

    /// <summary>
    /// Generate a moral dilemma to test specific dimensions
    /// </summary>
    public async Task<UserInteraction> GenerateMoralDilemmaAsync(
        UserProfile profile, 
        List<string> targetDimensions)
    {
        var kernel = _kernelService.GetKernel();

        var prompt = $$$"""
        You are a moral psychologist designing dilemmas to understand someone's ethical framework.
        
        Create a compelling moral dilemma that will reveal beliefs about: {{{string.Join(", ", targetDimensions)}}}
        
        The dilemma should:
        1. Present a realistic, relatable scenario
        2. Have no clear "right" answer
        3. Force a choice between competing values
        4. Elicit genuine emotional and moral reasoning
        5. Be culturally sensitive and thought-provoking
        
        Current understanding of this person:
        {{{profile.CurrentBeliefSnapshot?.NarrativeSummary ?? "Unknown - this is an early interaction"}}}
        
        Format your response as:
        SCENARIO: [Describe the situation in 2-3 paragraphs]
        QUESTION: [The specific choice they must make]
        FOLLOW_UP: [A follow-up question asking them to explain their reasoning]
        """;

        _logger.LogInformation("Generating moral dilemma for dimensions: {Dimensions}", string.Join(", ", targetDimensions));
        
        var result = await kernel.InvokePromptAsync(prompt);
        var dilemmaText = result.ToString();

        return new UserInteraction
        {
            UserId = profile.Id,
            Type = InteractionType.MoralDilemma,
            Content = ParseDilemmaContent(dilemmaText),
            TargetedDimensions = targetDimensions
        };
    }

    /// <summary>
    /// Generate a scenario designed to elicit emotional response
    /// </summary>
    public async Task<UserInteraction> GenerateEmotionalScenarioAsync(
        UserProfile profile,
        string targetEmotion)
    {
        var kernel = _kernelService.GetKernel();

        var prompt = $$$"""
        Create a brief scenario designed to elicit {{{targetEmotion}}} and reveal underlying values.
        
        The scenario should:
        1. Be realistic and relatable
        2. Naturally evoke {{{targetEmotion}}}
        3. Require the person to make a judgment or decision
        4. Reveal something about their values and priorities
        
        Keep it under 100 words. End with a question asking what they would do or how they feel about it.
        """;

        _logger.LogInformation("Generating emotional scenario targeting: {Emotion}", targetEmotion);
        
        var result = await kernel.InvokePromptAsync(prompt);
        
        return new UserInteraction
        {
            UserId = profile.Id,
            Type = InteractionType.EmotionalPrompt,
            Content = new InteractionContent
            {
                Question = result.ToString(),
                Format = InteractionFormat.OpenText
            }
        };
    }

    /// <summary>
    /// Generate a scale question for a specific dimension
    /// </summary>
    public UserInteraction GenerateScaleQuestion(
        UserProfile profile,
        string dimension,
        string lowLabel,
        string highLabel)
    {
        var questions = new Dictionary<string, string>
        {
            ["political-economic"] = "In general, do you favor individual freedom or collective welfare?",
            ["political-social"] = "Should society prioritize tradition and stability or progress and change?",
            ["moral-consequentialist"] = "What matters more: the intentions behind an action or its outcomes?",
            ["authority"] = "How important is respect for authority and hierarchy?",
            ["spirituality"] = "To what extent do you believe in forces beyond the material world?",
            ["human-nature"] = "Are people fundamentally good, bad, or neither?",
            ["trust"] = "In general, how much do you trust other people?"
        };

        return new UserInteraction
        {
            UserId = profile.Id,
            Type = InteractionType.ScaleQuestion,
            Content = new InteractionContent
            {
                Question = questions.GetValueOrDefault(dimension, 
                    $"On a scale from 1-10, where do you stand on {dimension}?"),
                Format = InteractionFormat.Scale,
                MinValue = 1,
                MaxValue = 10,
                MinLabel = lowLabel,
                MaxLabel = highLabel
            },
            TargetedDimensions = new List<string> { dimension }
        };
    }

    /// <summary>
    /// Generate value ranking exercise
    /// </summary>
    public UserInteraction GenerateValueRankingQuestion(UserProfile profile)
    {
        var valueSets = new List<List<string>>
        {
            new() { "Freedom", "Security", "Equality", "Prosperity", "Community" },
            new() { "Justice", "Mercy", "Truth", "Loyalty", "Compassion" },
            new() { "Wisdom", "Courage", "Temperance", "Authenticity", "Humility" },
            new() { "Individual Rights", "Social Harmony", "Environmental Protection", "Economic Growth", "Cultural Preservation" }
        };

        var random = new Random();
        var selectedSet = valueSets[random.Next(valueSets.Count)];

        return new UserInteraction
        {
            UserId = profile.Id,
            Type = InteractionType.ValueRanking,
            Content = new InteractionContent
            {
                Question = "Rank these values in order of personal importance (1 = most important):",
                Format = InteractionFormat.Ranking,
                Options = selectedSet
            }
        };
    }

    /// <summary>
    /// Generate initial survey multiple choice question
    /// </summary>
    public UserInteraction GenerateInitialSurveyQuestion(UserProfile profile)
    {
        var questions = new List<(string Question, List<string> Options, List<string> Dimensions)>
        {
            (
                "What do you value most in life?",
                new List<string> 
                { 
                    "Personal freedom and independence",
                    "Strong relationships and community",
                    "Achievement and success",
                    "Knowledge and understanding",
                    "Helping others and making a difference"
                },
                new List<string> { "values", "individualism-collectivism" }
            ),
            (
                "When making an important decision, what matters most to you?",
                new List<string>
                {
                    "What will produce the best outcome for everyone involved",
                    "Whether it aligns with my core principles",
                    "What feels right in my gut",
                    "What authority figures or tradition suggest",
                    "What benefits me and those closest to me"
                },
                new List<string> { "moral-reasoning", "consequentialism-deontology" }
            ),
            (
                "How do you view human nature?",
                new List<string>
                {
                    "People are fundamentally good and trustworthy",
                    "People are naturally selfish but can be taught cooperation",
                    "People are shaped by their environment and circumstances",
                    "Human nature is complex and varies greatly by individual",
                    "People need structure and rules to behave well"
                },
                new List<string> { "human-nature", "trust" }
            ),
            (
                "What role should government play in society?",
                new List<string>
                {
                    "Minimal - protect basic rights and stay out of the way",
                    "Active - ensure fairness and support those in need",
                    "Balanced - provide infrastructure but respect freedom",
                    "Strong - maintain order and preserve tradition",
                    "Democratic - reflect the will of the majority"
                },
                new List<string> { "political-economic", "authority" }
            ),
            (
                "When you see someone in need, what's your first instinct?",
                new List<string>
                {
                    "Help immediately without hesitation",
                    "Assess whether they deserve help",
                    "Consider whether helping would create dependency",
                    "Feel sympathy but look to institutions to help",
                    "Help if I have the resources and it won't harm me"
                },
                new List<string> { "compassion", "moral-foundations" }
            )
        };

        var questionIndex = profile.InteractionCount % questions.Count;
        var selectedQuestion = questions[questionIndex];

        return new UserInteraction
        {
            UserId = profile.Id,
            Type = InteractionType.StatementAgreement,
            Content = new InteractionContent
            {
                Question = selectedQuestion.Question,
                Format = InteractionFormat.MultipleChoice,
                Options = selectedQuestion.Options
            },
            TargetedDimensions = selectedQuestion.Dimensions
        };
    }

    /// <summary>
    /// Generate a general multiple choice question based on uncertain areas
    /// </summary>
    public UserInteraction GenerateMultipleChoiceQuestion(UserProfile profile, BeliefSnapshot snapshot)
    {
        var questions = new List<(string Question, List<string> Options, List<string> Dimensions)>
        {
            (
                "Which statement best describes your view on wealth inequality?",
                new List<string>
                {
                    "Inequality is natural and motivates people to work harder",
                    "Some inequality is okay, but extremes should be reduced",
                    "Society should actively work toward economic equality",
                    "Inequality is acceptable as long as everyone has basic needs met",
                    "The focus should be on equal opportunity, not equal outcomes"
                },
                new List<string> { "political-economic", "fairness" }
            ),
            (
                "How do you feel about breaking rules for a good cause?",
                new List<string>
                {
                    "Rules exist for a reason and should always be followed",
                    "Sometimes breaking rules is justified if the cause is important enough",
                    "The outcome matters more than following rules",
                    "It depends entirely on the specific situation",
                    "I'd feel uncomfortable breaking rules even for good reasons"
                },
                new List<string> { "moral-reasoning", "rule-following" }
            ),
            (
                "What's your view on tradition and cultural heritage?",
                new List<string>
                {
                    "Traditions should be preserved and respected",
                    "Keep good traditions, discard harmful ones",
                    "Society should constantly evolve beyond old traditions",
                    "Traditions are interesting but shouldn't dictate modern life",
                    "Each generation should create its own values"
                },
                new List<string> { "political-social", "tradition" }
            ),
            (
                "How much do you trust your intuition versus logical analysis?",
                new List<string>
                {
                    "I trust my gut feelings - they're usually right",
                    "I prefer to think things through logically",
                    "I balance both intuition and analysis",
                    "It depends on the situation",
                    "I trust data and facts over feelings"
                },
                new List<string> { "epistemology", "decision-making" }
            ),
            (
                "What motivates you to be a good person?",
                new List<string>
                {
                    "It's the right thing to do regardless of consequences",
                    "I want to make the world a better place",
                    "It makes me feel good about myself",
                    "It's what my community/faith expects",
                    "I'd want others to treat me the same way"
                },
                new List<string> { "moral-foundations", "motivation" }
            ),
            (
                "How do you view experts and authority figures?",
                new List<string>
                {
                    "They deserve respect and should generally be followed",
                    "I respect expertise but question authority",
                    "Everyone's opinion is equally valid",
                    "Authority should always be questioned",
                    "I defer to experts in their fields"
                },
                new List<string> { "authority", "epistemology" }
            ),
            (
                "What's most important for a good life?",
                new List<string>
                {
                    "Personal happiness and fulfillment",
                    "Contributing to something larger than myself",
                    "Strong relationships with family and friends",
                    "Success and achievement",
                    "Living according to my principles"
                },
                new List<string> { "values", "life-purpose" }
            ),
            (
                "How do you view change in society?",
                new List<string>
                {
                    "Change is exciting and usually positive",
                    "Change is necessary but should happen gradually",
                    "Too much change is destabilizing",
                    "Change should only happen when absolutely necessary",
                    "We need radical change to fix current problems"
                },
                new List<string> { "political-social", "change-orientation" }
            )
        };

        var random = new Random();
        var selectedQuestion = questions[random.Next(questions.Count)];

        return new UserInteraction
        {
            UserId = profile.Id,
            Type = InteractionType.StatementAgreement,
            Content = new InteractionContent
            {
                Question = selectedQuestion.Question,
                Format = InteractionFormat.MultipleChoice,
                Options = selectedQuestion.Options
            },
            TargetedDimensions = selectedQuestion.Dimensions
        };
    }

    /// <summary>
    /// Generate a moral dilemma as multiple choice
    /// </summary>
    public UserInteraction GenerateMoralDilemmaMultipleChoice(UserProfile profile, BeliefSnapshot snapshot)
    {
        var dilemmas = new List<(string Scenario, string Question, List<string> Options, List<string> Dimensions)>
        {
            (
                "You find a wallet with $500 and the owner's ID. You're struggling financially this month.",
                "What would you do?",
                new List<string>
                {
                    "Return it immediately with all the money",
                    "Return it but keep some money as a 'finder's fee'",
                    "Keep the money - they probably won't miss it",
                    "Turn it in to the police",
                    "Try to return it directly, but keep it if that's too difficult"
                },
                new List<string> { "honesty", "consequentialism" }
            ),
            (
                "A close friend confides they've been having an affair. Their spouse, who you also know, seems unaware.",
                "How do you respond?",
                new List<string>
                {
                    "Tell the spouse - they deserve to know",
                    "Stay out of it completely",
                    "Urge my friend to come clean but don't intervene",
                    "Support my friend but express disapproval",
                    "It's not my place to judge their choices"
                },
                new List<string> { "loyalty", "honesty", "moral-judgment" }
            ),
            (
                "You witness your employer doing something unethical but not illegal. Speaking up could cost you your job.",
                "What do you do?",
                new List<string>
                {
                    "Report it regardless of consequences",
                    "Report it anonymously if possible",
                    "Keep quiet - I need this job",
                    "Confront my employer privately first",
                    "Document it but wait for the right moment"
                },
                new List<string> { "integrity", "self-interest", "courage" }
            ),
            (
                "A self-driving car must choose: swerve and kill one pedestrian, or continue and kill five passengers.",
                "What should it be programmed to do?",
                new List<string>
                {
                    "Save the most lives (kill the one pedestrian)",
                    "Protect its passengers (kill the five pedestrians)",
                    "Do nothing and let chance decide",
                    "There's no right answer to this",
                    "The car should protect those not responsible for the situation"
                },
                new List<string> { "consequentialism", "moral-calculus", "utilitarianism" }
            ),
            (
                "Your country is considering accepting refugees, but polls show most citizens are opposed.",
                "What should the government do?",
                new List<string>
                {
                    "Accept refugees - it's the moral thing to do",
                    "Follow the will of the people",
                    "Accept some but with strict limits",
                    "Prioritize citizens' concerns over refugees",
                    "Only accept refugees if it benefits the country"
                },
                new List<string> { "compassion", "democracy", "group-loyalty" }
            )
        };

        var random = new Random();
        var selectedDilemma = dilemmas[random.Next(dilemmas.Count)];

        return new UserInteraction
        {
            UserId = profile.Id,
            Type = InteractionType.MoralDilemma,
            Content = new InteractionContent
            {
                Context = selectedDilemma.Scenario,
                Question = selectedDilemma.Question,
                Format = InteractionFormat.MultipleChoice,
                Options = selectedDilemma.Options
            },
            TargetedDimensions = selectedDilemma.Dimensions
        };
    }

    /// <summary>
    /// Generate a scenario with multiple choice responses
    /// </summary>
    public UserInteraction GenerateScenarioMultipleChoice(UserProfile profile, BeliefSnapshot snapshot)
    {
        var scenarios = new List<(string Scenario, string Question, List<string> Options, List<string> Dimensions)>
        {
            (
                "You see a homeless person asking for money. They appear able-bodied.",
                "What's your first thought?",
                new List<string>
                {
                    "I want to help - everyone deserves compassion",
                    "They should get a job instead of begging",
                    "I wonder what circumstances led them here",
                    "I feel uncomfortable and try to avoid eye contact",
                    "I'd rather donate to organizations that help them"
                },
                new List<string> { "compassion", "judgment", "social-issues" }
            ),
            (
                "A coworker takes credit for your idea in a meeting. Everyone seems to believe them.",
                "How do you feel?",
                new List<string>
                {
                    "Angry and betrayed - I need to correct this immediately",
                    "Disappointed but I'll let it go to avoid conflict",
                    "Upset, but I'll address it with them privately later",
                    "Surprised they'd do that - maybe it was a misunderstanding",
                    "Focused on making sure it doesn't happen again"
                },
                new List<string> { "justice", "conflict-resolution", "self-advocacy" }
            ),
            (
                "Someone cuts in front of you in a long line at the store.",
                "What do you do?",
                new List<string>
                {
                    "Politely point out they cut in line",
                    "Say nothing but feel annoyed",
                    "Assume they didn't realize and give them the benefit of the doubt",
                    "Loudly call them out",
                    "Let it go - it's not worth the confrontation"
                },
                new List<string> { "fairness", "conflict-avoidance", "assertiveness" }
            )
        };

        var random = new Random();
        var selectedScenario = scenarios[random.Next(scenarios.Count)];

        return new UserInteraction
        {
            UserId = profile.Id,
            Type = InteractionType.ScenarioReaction,
            Content = new InteractionContent
            {
                Context = selectedScenario.Scenario,
                Question = selectedScenario.Question,
                Format = InteractionFormat.MultipleChoice,
                Options = selectedScenario.Options
            },
            TargetedDimensions = selectedScenario.Dimensions
        };
    }

    #region Private Helpers

    private DiscoveryStage DetermineStage(UserProfile profile)
    {
        var count = profile.InteractionCount;
        return count switch
        {
            < 5 => DiscoveryStage.Initial,
            < 15 => DiscoveryStage.Foundation,
            < 30 => DiscoveryStage.Exploration,
            < 60 => DiscoveryStage.Refinement,
            _ => DiscoveryStage.Continuous
        };
    }

    private string BuildQuestionPrompt(
        DiscoveryStage stage,
        List<string> uncertainAreas,
        List<string> previousQuestions,
        UserProfile profile)
    {
        // SIMPLIFIED for smaller models - just ask for a single thoughtful question
        var basePrompt = stage switch
        {
            DiscoveryStage.Initial => "Ask a thoughtful question about what someone values most in life.",
            DiscoveryStage.Foundation => "Ask a question about someone's views on human nature and society.",
            DiscoveryStage.Exploration => "Ask a moral dilemma question with no clear right answer.",
            DiscoveryStage.Refinement => "Ask a challenging question about ethics and values.",
            _ => "Ask an open-ended question about beliefs and worldview."
        };

        return basePrompt + " Keep it under 50 words.";
    }

    private UserInteraction ParseQuestionResponse(string questionData, string userId)
    {
        // For now, treat the entire response as an open-ended question
        // In production, you might parse structured JSON
        return new UserInteraction
        {
            UserId = userId,
            Type = InteractionType.OpenEndedQuestion,
            Content = new InteractionContent
            {
                Question = questionData.Trim(),
                Format = InteractionFormat.OpenText
            }
        };
    }

    private InteractionContent ParseDilemmaContent(string dilemmaText)
    {
        // Simple parsing - in production, use more robust parsing
        var lines = dilemmaText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var scenario = "";
        var question = "";

        foreach (var line in lines)
        {
            if (line.StartsWith("SCENARIO:", StringComparison.OrdinalIgnoreCase))
                scenario = line.Substring(9).Trim();
            else if (line.StartsWith("QUESTION:", StringComparison.OrdinalIgnoreCase))
                question = line.Substring(9).Trim();
            else if (string.IsNullOrEmpty(question))
                scenario += "\n" + line;
        }

        return new InteractionContent
        {
            Question = question,
            Context = scenario,
            Format = InteractionFormat.OpenText
        };
    }

    #endregion
}
