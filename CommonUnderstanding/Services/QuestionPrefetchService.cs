using CommonUnderstanding.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CommonUnderstanding.Services;

/// <summary>
/// On-demand question pre-generation service. Call <see cref="RequestPrefetch"/> explicitly
/// from Discovery flows — this service does NOT run continuously in the background.
/// </summary>
public class QuestionPrefetchService
{
    private readonly UserProfileStore _profileStore;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<QuestionPrefetchService> _logger;

    public QuestionPrefetchService(
        UserProfileStore profileStore,
        IServiceScopeFactory scopeFactory,
        ILogger<QuestionPrefetchService> logger)
    {
        _profileStore = profileStore;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Triggers question pre-generation for a user. Only call this from Discovery flows.
    /// Fires as a background task so the caller is not blocked, but does NOT run
    /// continuously — work happens once per explicit call.
    /// </summary>
    public void RequestPrefetch(string userId)
    {
        _ = Task.Run(() => PrefetchQuestionsForUser(userId, CancellationToken.None));
    }

    private async Task PrefetchQuestionsForUser(string userId, CancellationToken cancellationToken)
    {
        try
        {
            var profile = _profileStore.GetProfile(userId);
            if (profile == null)
            {
                _logger.LogWarning("Profile {UserId} not found for prefetch", userId);
                return;
            }
            
            if (profile.PrefetchedQuestions.Count >= 10)
            {
                _logger.LogDebug("User {UserId} already has {Count} prefetched questions, skipping", 
                    userId, profile.PrefetchedQuestions.Count);
                return; // Already has enough questions queued
            }

            _logger.LogInformation("Pre-fetching psychometric question batch for user {UserId}, current queue: {Current}", 
                userId, profile.PrefetchedQuestions.Count);

            // Calculate how many questions we need
            var questionsNeeded = 10 - profile.PrefetchedQuestions.Count;
            
            // Generate questions in batches using PsychometricianAgent
            // Batch size of 5 is optimal for psychometric analysis
            var batchSize = Math.Min(5, questionsNeeded);
            
            using var scope = _scopeFactory.CreateScope();
            var psychAgent = scope.ServiceProvider.GetRequiredService<PsychometricianAgent>();
            
            _logger.LogInformation("Requesting psychometric batch of {BatchSize} questions for user {UserId}", 
                batchSize, userId);
            
            // Generate optimized batch
            var questionBatch = await psychAgent.GenerateAdaptiveQuestionBatchAsync(profile, batchSize);
            
            if (questionBatch == null || !questionBatch.Any())
            {
                _logger.LogWarning("PsychometricianAgent returned empty batch for user {UserId}, falling back to single question generation", 
                    userId);
                
                // Fallback: Generate individual questions if batch fails
                await FallbackToIndividualGeneration(profile, questionsNeeded, cancellationToken);
                return;
            }
            
            // Add all questions from batch to prefetch queue
            var addedCount = 0;
            var duplicateCount = 0;
            foreach (var question in questionBatch)
            {
                if (cancellationToken.IsCancellationRequested) break;
                
                var questionHash = ComputeQuestionHash(question);
                
                // Check for duplicates
                if (!profile.AskedQuestionHashes.Contains(questionHash))
                {
                    profile.PrefetchedQuestions.Enqueue(question);
                    addedCount++;
                    
                    _logger.LogDebug("Added psychometric question {Index}/{Total} for user {UserId}: {QuestionPreview}", 
                        addedCount, questionBatch.Count, userId, 
                        question.Content.Question.Length > 50 
                            ? question.Content.Question.Substring(0, 47) + "..." 
                            : question.Content.Question);
                }
                else
                {
                    duplicateCount++;
                    _logger.LogWarning("Skipped duplicate question for user {UserId}. Question: {Question}, Hash: {Hash}", 
                        userId, 
                        question.Content.Question.Length > 100 
                            ? question.Content.Question.Substring(0, 97) + "..." 
                            : question.Content.Question,
                        questionHash);
                }
            }
            
            _logger.LogInformation("Successfully prefetched {AddedCount} psychometric questions for user {UserId}, skipped {DuplicateCount} duplicates, total queued: {Total}", 
                addedCount, userId, duplicateCount, profile.PrefetchedQuestions.Count);
            
            // If ALL questions were duplicates, this is a serious problem
            if (addedCount == 0 && questionBatch.Any())
            {
                _logger.LogError("CRITICAL: All {Count} generated questions were duplicates for user {UserId}! " +
                    "Profile state: Confidence={Confidence}, Stage={Stage}, Interactions={Interactions}, " +
                    "Asked hashes count={HashCount}", 
                    questionBatch.Count, userId,
                    profile.CurrentBeliefSnapshot?.OverallConfidence ?? 0,
                    profile.Stage,
                    profile.InteractionCount,
                    profile.AskedQuestionHashes.Count);
                
                // Clear the asked hashes for this user to break the cycle
                // This is a last resort, but better than being stuck
                var oldCount = profile.AskedQuestionHashes.Count;
                profile.AskedQuestionHashes.Clear();
                _logger.LogWarning("Cleared {Count} asked question hashes for user {UserId} to prevent infinite duplicate loop", 
                    oldCount, userId);
                
                // Try one more time with cleared hashes
                _logger.LogInformation("Retrying question generation for user {UserId} after clearing hashes", userId);
                var retryBatch = await psychAgent.GenerateAdaptiveQuestionBatchAsync(profile, batchSize);
                
                if (retryBatch != null && retryBatch.Any())
                {
                    foreach (var question in retryBatch)
                    {
                        if (cancellationToken.IsCancellationRequested) break;
                        
                        var hash = ComputeQuestionHash(question);
                        if (!profile.AskedQuestionHashes.Contains(hash))
                        {
                            profile.PrefetchedQuestions.Enqueue(question);
                            addedCount++;
                        }
                    }
                    
                    _logger.LogInformation("Retry successful: Added {Count} questions after clearing hashes", addedCount);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error prefetching psychometric questions for user {UserId}", userId);
            
            // Try fallback approach
            try
            {
                var profile = _profileStore.GetProfile(userId);
                if (profile != null)
                {
                    var questionsNeeded = 10 - profile.PrefetchedQuestions.Count;
                    if (questionsNeeded > 0)
                    {
                        _logger.LogWarning("Attempting fallback question generation for user {UserId}", userId);
                        await FallbackToIndividualGeneration(profile, questionsNeeded, cancellationToken);
                    }
                }
            }
            catch (Exception fallbackEx)
            {
                _logger.LogError(fallbackEx, "Fallback generation also failed for user {UserId}", userId);
            }
        }
    }

    /// <summary>
    /// Fallback method: Generate questions one-by-one if batch generation fails
    /// </summary>
    private async Task FallbackToIndividualGeneration(UserProfile profile, int questionsNeeded, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Falling back to individual question generation for user {UserId}", profile.Id);
        
        using var scope = _scopeFactory.CreateScope();
        var questionEngine = scope.ServiceProvider.GetRequiredService<DiscoveryQuestionEngine>();
        
        for (int i = 0; i < questionsNeeded; i++)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var question = await GenerateUniqueQuestion(profile, questionEngine);
            if (question != null)
            {
                profile.PrefetchedQuestions.Enqueue(question);
                _logger.LogInformation("Fallback: Prefetched question {Index} for user {UserId}, total queued: {Total}", 
                    i + 1, profile.Id, profile.PrefetchedQuestions.Count);
            }

            // Small delay between generations to not overwhelm the AI
            await Task.Delay(50, cancellationToken);
        }
    }

    /// <summary>
    /// Generate a single unique question (used for fallback)
    /// </summary>
    private async Task<UserInteraction?> GenerateUniqueQuestion(UserProfile profile, DiscoveryQuestionEngine questionEngine)
    {
        const int maxAttempts = 5;
        
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var question = await questionEngine.GenerateNextQuestionAsync(profile);
            var questionHash = ComputeQuestionHash(question);
  
            if (!profile.AskedQuestionHashes.Contains(questionHash))
            {
                // Mark this question as generated (will be marked as asked when actually presented)
                return question;
            }
            
            _logger.LogDebug("Generated duplicate question for user {UserId}, attempt {Attempt}", 
                profile.Id, attempt + 1);
        }

        _logger.LogWarning("Could not generate unique question for user {UserId} after {Attempts} attempts", 
            profile.Id, maxAttempts);
        return null;
    }

    /// <summary>
    /// Compute hash for question to detect duplicates
    /// </summary>
    private string ComputeQuestionHash(UserInteraction interaction)
    {
        // Create a hash based on question text and options to detect duplicates
        var content = interaction.Content.Question;
        if (interaction.Content.Options?.Any() == true)
        {
            content += string.Join("|", interaction.Content.Options);
        }
        if (!string.IsNullOrEmpty(interaction.Content.Context))
        {
            content += "|" + interaction.Content.Context;
        }
        return content.GetHashCode().ToString();
    }
}
