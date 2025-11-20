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
    /// Start a new user's discovery journey
    /// </summary>
    public async Task<UserInteraction> StartDiscoveryAsync(UserProfile profile)
    {
        _logger.LogInformation("Starting discovery journey for user {UserId}", profile.Id);

        // IMMEDIATELY generate 10 questions + 10 jokes - don't wait for background service
        _logger.LogInformation("Pre-generating 20 interactions (10 questions + 10 jokes) for user {UserId}", profile.Id);
        
        var questions = new List<UserInteraction>();
        var jokes = new List<UserInteraction>();
        
        // Generate 10 questions
        for (int i = 0; i < 10; i++)
        {
            var question = await _questionEngine.GenerateNextQuestionAsync(profile);
            var hash = ComputeQuestionHash(question);
            if (!profile.AskedQuestionHashes.Contains(hash))
            {
                questions.Add(question);
                profile.AskedQuestionHashes.Add(hash);
            }
        }
        
        // Generate 10 jokes
        for (int i = 0; i < 10; i++)
        {
            var joke = GenerateJoke(profile, i);
            jokes.Add(joke);
        }
        
        // Interleave them: Q, Q, J, Q, Q, J, Q, Q, J...
        // Pattern: 2 questions, then 1 joke, repeat
        var interleaved = new List<UserInteraction>();
        int qIndex = 1; // Start at 1 since we'll return the first question separately
        int jIndex = 0;
        
        while (qIndex < questions.Count || jIndex < jokes.Count)
        {
            // Add 2 questions
            for (int i = 0; i < 2 && qIndex < questions.Count; i++)
            {
                interleaved.Add(questions[qIndex++]);
            }
            
            // Add 1 joke
            if (jIndex < jokes.Count)
            {
                interleaved.Add(jokes[jIndex++]);
            }
        }
        
        // Queue them all up
        foreach (var item in interleaved)
        {
            profile.PrefetchedQuestions.Enqueue(item);
        }
        
        _logger.LogInformation("Queued {Count} interactions for user {UserId} (Questions first, then alternating)", interleaved.Count, profile.Id);
        
        // Return the FIRST question (never a joke)
        return questions[0];
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
    /// Determine what type of question to ask next
    /// </summary>
    private QuestionStrategy DetermineNextQuestionType(UserProfile profile, BeliefSnapshot snapshot)
    {
        var interactionCount = profile.InteractionCount;
        var uncertainAreas = snapshot.Statistics.UncertainAreas;
        var contradictions = snapshot.Statistics.DetectedContradictions;

        // First 5 interactions: multiple choice foundation building (less intimidating)
        if (interactionCount < 5)
            return QuestionStrategy.MultipleChoice;

        // Every 7th interaction: value ranking for calibration
        if (interactionCount % 7 == 0)
            return QuestionStrategy.ValueRanking;

        // Every 5th interaction: scale question (easy to answer)
        if (interactionCount % 5 == 0)
            return QuestionStrategy.ScaleQuestion;

        // If contradictions detected: multiple choice to clarify
        if (contradictions.Any())
            return QuestionStrategy.MultipleChoice;

        // If high uncertainty in certain areas: use structured questions
        if (uncertainAreas.Any())
        {
            // Cycle through easy question types
            var cycle = interactionCount % 3;
            return cycle switch
            {
                0 => QuestionStrategy.MultipleChoice,
                1 => QuestionStrategy.ScaleQuestion,
                _ => QuestionStrategy.ValueRanking
            };
        }

        // Default: multiple choice (less intimidating than open-ended)
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
    MultipleChoice,
    MoralDilemma,
    EmotionalProbe,
    ScaleQuestion,
    ValueRanking,
    FollowUp
}
