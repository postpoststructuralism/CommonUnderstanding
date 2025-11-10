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

        try
        {
            var result = await kernel.InvokePromptAsync(prompt);
            var questionData = result.ToString();

            _logger.LogInformation("Generated question for user {UserId} at stage {Stage}", 
                profile.Id, stage);

            return ParseQuestionResponse(questionData, profile.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating question for user {UserId}", profile.Id);
            throw;
        }
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

        try
        {
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating moral dilemma");
            throw;
        }
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

        try
        {
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating emotional scenario");
            throw;
        }
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
        var basePrompt = $$$"""
        You are a skilled interviewer discovering someone's belief system and worldview.
        
        Current stage: {{{stage}}}
        Interactions completed: {{{profile.InteractionCount}}}
        
        """;

        basePrompt += stage switch
        {
            DiscoveryStage.Initial => """
                This is an early interaction. Ask foundational questions about:
                - What they value most in life
                - Their general worldview orientation
                - Basic moral intuitions
                Keep it approachable and non-threatening.
                """,
            
            DiscoveryStage.Foundation => """
                Build on initial understanding. Explore:
                - Sources of meaning and purpose
                - Views on human nature
                - Role of community vs. individual
                - Basic ethical frameworks
                """,
            
            DiscoveryStage.Exploration => $$$"""
                Dig deeper into uncertain areas: {{{string.Join(", ", uncertainAreas)}}}
                Use scenarios and hypotheticals to reveal nuanced beliefs.
                """,
            
            DiscoveryStage.Refinement => """
                Test edge cases and contradictions. Present challenging scenarios
                that reveal boundaries and exceptions to previously stated beliefs.
                """,
            
            _ => """
                Maintain and update the model. Focus on areas showing inconsistency
                or recent changes in thinking.
                """
        };

        if (previousQuestions.Any())
        {
            basePrompt += $$$"""
                
                
                Previous questions asked (don't repeat):
                {{{string.Join("\n", previousQuestions.TakeLast(5))}}}
                """;
        }

        basePrompt += """
            
            
            Generate ONE question that will provide maximum insight.
            Make it thought-provoking but accessible.
            Format: Just provide the question text.
            """;

        return basePrompt;
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
