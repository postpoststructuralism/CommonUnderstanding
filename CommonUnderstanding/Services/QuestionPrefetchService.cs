using CommonUnderstanding.Models;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace CommonUnderstanding.Services;

/// <summary>
/// Background service that pre-generates questions while AI is processing responses
/// NOW ENHANCED: Uses PsychometricianAgent for optimized batch generation
/// </summary>
public class QuestionPrefetchService : BackgroundService
{
    private readonly UserProfileStore _profileStore;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<QuestionPrefetchService> _logger;
    private readonly ConcurrentQueue<string> _prefetchQueue = new();

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
    /// Request prefetch of questions for a user
    /// </summary>
    public void RequestPrefetch(string userId)
    {
        _prefetchQueue.Enqueue(userId);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Question Prefetch Service started (Psychometric Mode)");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_prefetchQueue.TryDequeue(out var userId))
                {
                    await PrefetchQuestionsForUser(userId, stoppingToken);
                }
                else
                {
                    // No work to do, wait a bit
                    await Task.Delay(100, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in question prefetch service");
                await Task.Delay(1000, stoppingToken);
            }
        }

        _logger.LogInformation("Question Prefetch Service stopped");
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
                    _logger.LogDebug("Skipped duplicate question for user {UserId}", userId);
                }
            }
            
            _logger.LogInformation("Successfully prefetched {AddedCount} psychometric questions for user {UserId}, total queued: {Total}", 
                addedCount, userId, profile.PrefetchedQuestions.Count);
            
            // If we still need more questions and have room, generate another batch
            if (profile.PrefetchedQuestions.Count < 10 && !cancellationToken.IsCancellationRequested)
            {
                var remainingNeeded = 10 - profile.PrefetchedQuestions.Count;
                if (remainingNeeded > 0)
                {
                    _logger.LogInformation("Still need {Remaining} more questions for user {UserId}, generating additional batch", 
                        remainingNeeded, userId);
                    
                    // Small delay before next batch to avoid overwhelming the LLM
                    await Task.Delay(500, cancellationToken);
                    
                    var additionalBatch = await psychAgent.GenerateAdaptiveQuestionBatchAsync(
                        profile, 
                        Math.Min(5, remainingNeeded)
                    );
                    
                    if (additionalBatch != null && additionalBatch.Any())
                    {
                        foreach (var question in additionalBatch)
                        {
                            if (cancellationToken.IsCancellationRequested) break;
                            
                            var questionHash = ComputeQuestionHash(question);
                            if (!profile.AskedQuestionHashes.Contains(questionHash))
                            {
                                profile.PrefetchedQuestions.Enqueue(question);
                            }
                        }
                        
                        _logger.LogInformation("Added {Count} more questions, total queued: {Total}", 
                            additionalBatch.Count, profile.PrefetchedQuestions.Count);
                    }
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
