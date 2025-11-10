using CommonUnderstanding.Models;

namespace CommonUnderstanding.Services;

/// <summary>
/// Orchestrates the entire belief discovery process
/// </summary>
public class BeliefDiscoveryOrchestrator
{
    private readonly DiscoveryQuestionEngine _questionEngine;
    private readonly ResponseAnalysisEngine _analysisEngine;
    private readonly BayesianInferenceEngine _inferenceEngine;
    private readonly ILogger<BeliefDiscoveryOrchestrator> _logger;

    public BeliefDiscoveryOrchestrator(
        DiscoveryQuestionEngine questionEngine,
        ResponseAnalysisEngine analysisEngine,
        BayesianInferenceEngine inferenceEngine,
        ILogger<BeliefDiscoveryOrchestrator> logger)
    {
        _questionEngine = questionEngine;
        _analysisEngine = analysisEngine;
        _inferenceEngine = inferenceEngine;
        _logger = logger;
    }

    /// <summary>
    /// Process a user's response and generate the next question
    /// </summary>
    public async Task<(BeliefSnapshot UpdatedModel, UserInteraction NextQuestion)> ProcessResponseAndContinueAsync(
        UserProfile profile,
        UserInteraction completedInteraction)
    {
        var startTime = DateTime.UtcNow;

        // 1. Analyze the response
        _logger.LogInformation("Analyzing response for user {UserId}", profile.Id);
        var analysis = await _analysisEngine.AnalyzeResponseAsync(completedInteraction, profile);
        completedInteraction.Analysis = analysis;

        // 2. Analyze emotional content
        var emotionalMarkers = await _analysisEngine.AnalyzeEmotionalContentAsync(
            completedInteraction.Response.RawText);
        completedInteraction.Response.Emotion = emotionalMarkers;

        // 3. Update the belief model using Bayesian inference
        _logger.LogInformation("Updating belief model for user {UserId}", profile.Id);
        var updatedSnapshot = _inferenceEngine.UpdateModel(profile, completedInteraction, analysis);

        // 4. Store the snapshot
        if (profile.CurrentBeliefSnapshot != null)
        {
            profile.HistoricalSnapshots.Add(profile.CurrentBeliefSnapshot);
        }
        profile.CurrentBeliefSnapshot = updatedSnapshot;

        // 5. Update profile metadata
        profile.LastInteractionAt = DateTime.UtcNow;
        profile.Stage = DetermineStage(profile);

        // 6. Generate next question based on updated model
        _logger.LogInformation("Generating next question for user {UserId}", profile.Id);
        var nextQuestion = await GenerateAdaptiveQuestionAsync(profile);

        var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
        _logger.LogInformation("Completed processing cycle for user {UserId} in {Time}ms. " +
                             "Confidence: {Confidence:F3}",
            profile.Id, processingTime, updatedSnapshot.OverallConfidence);

        return (updatedSnapshot, nextQuestion);
    }

    /// <summary>
    /// Start a new user's discovery journey
    /// </summary>
    public async Task<UserInteraction> StartDiscoveryAsync(UserProfile profile)
    {
        _logger.LogInformation("Starting discovery journey for user {UserId}", profile.Id);

        // Initialize with a welcoming, foundational question
        var initialQuestion = await _questionEngine.GenerateNextQuestionAsync(profile);
        
        return initialQuestion;
    }

    /// <summary>
    /// Generate an adaptive question based on current model state
    /// </summary>
    private async Task<UserInteraction> GenerateAdaptiveQuestionAsync(UserProfile profile)
    {
        var snapshot = profile.CurrentBeliefSnapshot;
        if (snapshot == null)
            return await _questionEngine.GenerateNextQuestionAsync(profile);

        // Decide what type of question to ask next
        var questionType = DetermineNextQuestionType(profile, snapshot);

        return questionType switch
        {
            QuestionStrategy.MoralDilemma => await GenerateMoralDilemmaQuestion(profile, snapshot),
            QuestionStrategy.EmotionalProbe => await GenerateEmotionalQuestion(profile, snapshot),
            QuestionStrategy.ScaleQuestion => GenerateScaleQuestion(profile, snapshot),
            QuestionStrategy.ValueRanking => _questionEngine.GenerateValueRankingQuestion(profile),
            QuestionStrategy.FollowUp => await GenerateFollowUpQuestion(profile, snapshot),
            _ => await _questionEngine.GenerateNextQuestionAsync(profile)
        };
    }

    /// <summary>
    /// Determine what type of question to ask next
    /// </summary>
    private QuestionStrategy DetermineNextQuestionType(UserProfile profile, BeliefSnapshot snapshot)
    {
        var interactionCount = profile.InteractionCount;
        var uncertainAreas = snapshot.Statistics.UncertainAreas;
        var contradictions = snapshot.Statistics.DetectedContradictions;

        // First 3 interactions: open-ended foundation building
        if (interactionCount < 3)
            return QuestionStrategy.OpenEnded;

        // Every 5th interaction: value ranking for calibration
        if (interactionCount % 5 == 0)
            return QuestionStrategy.ValueRanking;

        // If contradictions detected: follow-up to clarify
        if (contradictions.Any())
            return QuestionStrategy.FollowUp;

        // If high uncertainty in certain areas: targeted probing
        if (uncertainAreas.Any())
        {
            // Alternate between dilemmas and emotional probes
            return interactionCount % 2 == 0 
                ? QuestionStrategy.MoralDilemma 
                : QuestionStrategy.ScaleQuestion;
        }

        // Default: moral dilemma for depth
        return QuestionStrategy.MoralDilemma;
    }

    private async Task<UserInteraction> GenerateMoralDilemmaQuestion(
        UserProfile profile,
        BeliefSnapshot snapshot)
    {
        var uncertainAreas = snapshot.Statistics.UncertainAreas.Any()
            ? snapshot.Statistics.UncertainAreas.Take(2).ToList()
            : new List<string> { "ethics", "values" };

        return await _questionEngine.GenerateMoralDilemmaAsync(profile, uncertainAreas);
    }

    private async Task<UserInteraction> GenerateEmotionalQuestion(
        UserProfile profile,
        BeliefSnapshot snapshot)
    {
        var targetEmotions = new[] { "compassion", "outrage", "pride", "disgust" };
        var random = new Random();
        var emotion = targetEmotions[random.Next(targetEmotions.Length)];

        return await _questionEngine.GenerateEmotionalScenarioAsync(profile, emotion);
    }

    private UserInteraction GenerateScaleQuestion(
        UserProfile profile,
        BeliefSnapshot snapshot)
    {
        var uncertainDimensions = snapshot.Dimensions
            .Where(d => d.Confidence < 0.6)
            .OrderBy(d => d.Confidence)
            .ToList();

        if (uncertainDimensions.Any())
        {
            var dim = uncertainDimensions.First();
            return _questionEngine.GenerateScaleQuestion(
                profile, 
                dim.Name, 
                "Strongly Disagree", 
                "Strongly Agree");
        }

        // Default scale question
        return _questionEngine.GenerateScaleQuestion(
            profile,
            "individual-collective",
            "Individual Freedom",
            "Collective Good");
    }

    private async Task<UserInteraction> GenerateFollowUpQuestion(
        UserProfile profile,
        BeliefSnapshot snapshot)
    {
        // Generate a question that addresses contradictions
        var contradiction = snapshot.Statistics.DetectedContradictions.FirstOrDefault();
        if (contradiction != null)
        {
            var parts = contradiction.Split(" vs ");
            var targetDimensions = parts.ToList();
            return await _questionEngine.GenerateMoralDilemmaAsync(profile, targetDimensions);
        }

        return await _questionEngine.GenerateNextQuestionAsync(profile);
    }

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
}

/// <summary>
/// Strategies for selecting next question type
/// </summary>
internal enum QuestionStrategy
{
    OpenEnded,
    MoralDilemma,
    EmotionalProbe,
    ScaleQuestion,
    ValueRanking,
    FollowUp
}
