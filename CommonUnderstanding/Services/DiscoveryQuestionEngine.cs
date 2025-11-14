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
