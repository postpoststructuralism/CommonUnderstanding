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
    private readonly QuestionPrefetchService _prefetchService;
    private readonly ILogger<BeliefDiscoveryOrchestrator> _logger;

    public BeliefDiscoveryOrchestrator(
        DiscoveryQuestionEngine questionEngine,
        ResponseAnalysisEngine analysisEngine,
        BayesianInferenceEngine inferenceEngine,
        QuestionPrefetchService prefetchService,
        ILogger<BeliefDiscoveryOrchestrator> logger)
    {
        _questionEngine = questionEngine;
        _analysisEngine = analysisEngine;
        _inferenceEngine = inferenceEngine;
        _prefetchService = prefetchService;
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

        // 7. Trigger background prefetch of additional questions
        _prefetchService.RequestPrefetch(profile.Id);

        var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
        _logger.LogInformation("Completed processing cycle for user {UserId} in {Time}ms. " +
                             "Confidence: {Confidence:F3}",
            profile.Id, processingTime, updatedSnapshot.OverallConfidence);

        return (updatedSnapshot, nextQuestion);
    }

    /// <summary>
    /// Start a new user's discovery journey.
    /// Seeds the first 2 baseline questions and lets the adaptive engine
    /// select questions 3-5 based on emerging belief patterns.
    /// </summary>
    public async Task<UserInteraction> StartDiscoveryAsync(UserProfile profile)
    {
        _logger.LogInformation("Starting discovery journey for user {UserId}", profile.Id);

        // Seed only the first 2 baseline questions. After those are answered,
        // the adaptive engine will select questions 3-5 based on which
        // dimensions show the highest uncertainty.
        const int baselineQuestionCount = 2;
        _logger.LogInformation("Pre-generating {Count} baseline survey questions for user {UserId}", 
            baselineQuestionCount, profile.Id);
        
        var questions = new List<UserInteraction>();
        for (int i = 0; i < baselineQuestionCount; i++)
        {
            var question = _questionEngine.GenerateInitialSurveyQuestion(profile, forcedIndex: i);
            var hash = ComputeQuestionHash(question);
            if (!profile.AskedQuestionHashes.Contains(hash))
            {
                questions.Add(question);
                profile.AskedQuestionHashes.Add(hash);
            }
        }
        
        // Queue the second question (if any)
        for (int i = 1; i < questions.Count; i++)
        {
            profile.PrefetchedQuestions.Enqueue(questions[i]);
        }
        
        _logger.LogInformation("Queued {Count} baseline questions for user {UserId}. " +
            "Questions 3-5 will be adaptively selected.", 
            questions.Count - 1, profile.Id);
        
        // Return the first question, or fall back to adaptive generation if all were already asked
        return questions.Count > 0 ? questions[0] : await GetNextQuestionAsync(profile);
    }

    /// <summary>
    /// Generate a corny joke with thumbs up/down voting
    /// </summary>
    private UserInteraction GenerateJoke(UserProfile profile, int index)
    {
        var jokes = new[]
        {
            "Why don't scientists trust atoms? Because they make up everything!",
            "What do you call a fake noodle? An impasta!",
            "Why did the scarecrow win an award? He was outstanding in his field!",
            "What do you call a bear with no teeth? A gummy bear!",
            "Why don't eggs tell jokes? They'd crack each other up!",
            "What did the ocean say to the beach? Nothing, it just waved!",
            "Why did the bicycle fall over? It was two tired!",
            "What do you call cheese that isn't yours? Nacho cheese!",
            "Why couldn't the leopard play hide and seek? Because he was always spotted!",
            "What did one wall say to the other wall? I'll meet you at the corner!",
            "Why did the math book look so sad? Because it had too many problems!",
            "What do you call a dinosaur with an extensive vocabulary? A thesaurus!"
        };
        
        return new UserInteraction
        {
            UserId = profile.Id,
            Type = InteractionType.Joke,
            Content = new InteractionContent
            {
                Question = jokes[index % jokes.Length],
                Format = InteractionFormat.ThumbsVote,
                Options = new List<string> { "👍", "👎" }
            },
            TargetedDimensions = new List<string> { "humor", "engagement" }
        };
    }

    /// <summary>
    /// Generate the best next question for an existing profile without the startup bulk-seeding
    /// overhead of <see cref="StartDiscoveryAsync"/>. Use this as the fallback when the
    /// prefetch queue is empty mid-session.
    /// 
    /// For questions 3-5 (the "smart initial survey" phase), adaptively selects
    /// questions that target the highest-uncertainty dimensions.
    /// </summary>
    public async Task<UserInteraction> GetNextQuestionAsync(UserProfile profile)
    {
        // Questions 1-2: baseline hardcoded questions
        if (profile.InteractionCount < 2)
            return _questionEngine.GenerateInitialSurveyQuestion(profile);

        // Questions 3-5: smart adaptive survey — target high-uncertainty dimensions
        if (profile.InteractionCount < 5)
            return GenerateSmartInitialSurveyQuestion(profile);

        return await GenerateAdaptiveQuestionAsync(profile);
    }

    /// <summary>
    /// For the smart initial survey phase (questions 3-5), select a question
    /// that targets the dimensions with the highest uncertainty in the current model.
    /// Falls back to a random initial survey question if no model exists yet.
    /// </summary>
    private UserInteraction GenerateSmartInitialSurveyQuestion(UserProfile profile)
    {
        var snapshot = profile.CurrentBeliefSnapshot;
        
        // If we have a model with dimensions, find the highest-uncertainty ones
        if (snapshot?.Dimensions.Any() == true)
        {
            var highUncertaintyDimensions = snapshot.Dimensions
                .Where(d => d.Confidence < 0.4)
                .OrderBy(d => d.Confidence)
                .Select(d => d.Name)
                .Take(3)
                .ToList();

            if (highUncertaintyDimensions.Any())
            {
                _logger.LogInformation(
                    "Smart survey: targeting high-uncertainty dimensions for user {UserId}: {Dimensions}",
                    profile.Id, string.Join(", ", highUncertaintyDimensions));

                // Try to get a question targeting these dimensions
                var targetedQuestion = _questionEngine.GenerateInitialSurveyQuestionTargeting(
                    profile, highUncertaintyDimensions);
                if (targetedQuestion != null)
                    return targetedQuestion;
            }
        }

        // Fallback: use a random initial survey question beyond the first 2
        _logger.LogInformation("Smart survey: no model yet, using fallback initial question for user {UserId}", profile.Id);
        return _questionEngine.GenerateInitialSurveyQuestion(profile);
    }

    /// <summary>
    /// Generate an adaptive question based on current model state
    /// </summary>
    private async Task<UserInteraction> GenerateAdaptiveQuestionAsync(UserProfile profile)
    {
        // First, check if we have prefetched questions available
        if (profile.PrefetchedQuestions.TryDequeue(out var prefetchedQuestion))
        {
            var hash = ComputeQuestionHash(prefetchedQuestion);
            profile.AskedQuestionHashes.Add(hash);
            _logger.LogInformation("Using prefetched question for user {UserId}", profile.Id);
            return prefetchedQuestion;
        }

        // No prefetched questions, generate one now
        var snapshot = profile.CurrentBeliefSnapshot;
        if (snapshot == null)
            return await GenerateUniqueQuestionAsync(profile);

        // Decide what type of question to ask next
        var questionType = DetermineNextQuestionType(profile, snapshot);

        var question = questionType switch
        {
            QuestionStrategy.MultipleChoice => _questionEngine.GenerateMultipleChoiceQuestion(profile, snapshot),
            QuestionStrategy.ScaleQuestion => GenerateScaleQuestion(profile, snapshot),
            QuestionStrategy.ValueRanking => _questionEngine.GenerateValueRankingQuestion(profile),
            QuestionStrategy.MoralDilemma => _questionEngine.GenerateMoralDilemmaMultipleChoice(profile, snapshot),
            QuestionStrategy.EmotionalProbe => _questionEngine.GenerateScenarioMultipleChoice(profile, snapshot),
            QuestionStrategy.FollowUp => _questionEngine.GenerateMultipleChoiceQuestion(profile, snapshot),
            _ => await GenerateUniqueQuestionAsync(profile)
        };

        // Mark question as asked
        var questionHash = ComputeQuestionHash(question);
        profile.AskedQuestionHashes.Add(questionHash);

        return question;
    }

    /// <summary>
    /// Generate a unique question that hasn't been asked before
    /// </summary>
    private async Task<UserInteraction> GenerateUniqueQuestionAsync(UserProfile profile)
    {
        const int maxAttempts = 10;
        
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var question = await _questionEngine.GenerateNextQuestionAsync(profile);
            var questionHash = ComputeQuestionHash(question);
            
            if (!profile.AskedQuestionHashes.Contains(questionHash))
            {
                profile.AskedQuestionHashes.Add(questionHash);
                return question;
            }
            
            _logger.LogWarning("Generated duplicate question for user {UserId}, attempt {Attempt}", 
                profile.Id, attempt + 1);
        }

        // If we still get duplicates after max attempts, allow it but log warning
        _logger.LogError("Could not generate unique question for user {UserId} after {Attempts} attempts - allowing duplicate", 
            profile.Id, maxAttempts);
        
        var fallbackQuestion = await _questionEngine.GenerateNextQuestionAsync(profile);
        var fallbackHash = ComputeQuestionHash(fallbackQuestion);
        profile.AskedQuestionHashes.Add(fallbackHash);
        return fallbackQuestion;
    }

    /// <summary>
    /// Compute hash for question to detect duplicates
    /// </summary>
    private string ComputeQuestionHash(UserInteraction interaction)
    {
        var content = interaction.Content.Question ?? "";
        if (interaction.Content.Options?.Any() == true)
        {
            content += "|" + string.Join("|", interaction.Content.Options);
        }
        if (!string.IsNullOrEmpty(interaction.Content.Context))
        {
            content += "|" + interaction.Content.Context;
        }
        return content.GetHashCode().ToString();
    }

    /// <summary>
    /// Determine what type of question to ask next using entropy-based selection.
    /// Prioritizes question types that maximize information gain given the
    /// current state of the belief model. Moral dilemmas are used more
    /// aggressively since they provide rich multi-dimensional data.
    /// </summary>
    private QuestionStrategy DetermineNextQuestionType(UserProfile profile, BeliefSnapshot snapshot)
    {
        var interactionCount = profile.InteractionCount;
        var uncertainAreas = snapshot.Statistics.UncertainAreas;
        var contradictions = snapshot.Statistics.DetectedContradictions;

        // First 3 interactions: multiple choice foundation building
        if (interactionCount < 3)
            return QuestionStrategy.MultipleChoice;

        // Questions 3-5: mix in moral dilemmas for richer data
        if (interactionCount < 5)
            return interactionCount % 2 == 0 
                ? QuestionStrategy.MoralDilemma 
                : QuestionStrategy.MultipleChoice;

        // If contradictions detected: use moral dilemmas to resolve them
        if (contradictions.Any())
            return QuestionStrategy.MoralDilemma;

        // Calculate entropy to guide selection
        var entropy = snapshot.Statistics.Entropy;
        var overallConfidence = snapshot.OverallConfidence;

        // High entropy + low confidence: moral dilemmas give richest multi-dimension data
        if (entropy > 0.5 && overallConfidence < 0.5)
            return QuestionStrategy.MoralDilemma;

        // Medium entropy: value ranking helps calibrate multiple dimensions at once
        if (entropy > 0.3 && interactionCount % 4 == 0)
            return QuestionStrategy.ValueRanking;

        // Many uncertain areas: use emotional probes to surface hidden values
        if (uncertainAreas.Count >= 4 && interactionCount % 5 == 0)
            return QuestionStrategy.EmotionalProbe;

        // Low entropy but low confidence: need more data points → scale questions
        if (entropy < 0.3 && overallConfidence < 0.5)
            return QuestionStrategy.ScaleQuestion;

        // Periodic calibration with value ranking
        if (interactionCount % 8 == 0)
            return QuestionStrategy.ValueRanking;

        // Periodic scale questions for precision
        if (interactionCount % 6 == 0)
            return QuestionStrategy.ScaleQuestion;

        // Every 3rd question after question 5: moral dilemma for engagement
        if (interactionCount % 3 == 0)
            return QuestionStrategy.MoralDilemma;

        // Default: multiple choice (most engaging, good information density)
        return QuestionStrategy.MultipleChoice;
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
        // Prefer the dimension with the lowest confidence that hasn't been asked yet
        var uncertainDimensions = snapshot.Dimensions
            .Where(d => d.Confidence < 0.6)
            .OrderBy(d => d.Confidence)
            .ToList();

        var preferDimension = uncertainDimensions.FirstOrDefault()?.Name;

        // Let the engine pick an unseen question, optionally biased toward the uncertain dimension
        return _questionEngine.GenerateScaleQuestion(profile, preferDimension);
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
    MultipleChoice,
    MoralDilemma,
    EmotionalProbe,
    ScaleQuestion,
    ValueRanking,
    FollowUp
}
