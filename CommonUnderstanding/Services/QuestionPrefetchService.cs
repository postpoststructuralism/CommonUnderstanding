using CommonUnderstanding.Models;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace CommonUnderstanding.Services;

/// <summary>
/// Background service that pre-generates questions while AI is processing responses
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
        _logger.LogInformation("Question Prefetch Service started");

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
            if (profile == null || profile.PrefetchedQuestions.Count >= 10)
            {
                return; // Already has enough questions queued (increased from 3 to 10)
            }

            _logger.LogInformation("Pre-fetching questions for user {UserId}", userId);

            // Generate up to 10 questions ahead for rapid-fire answering
            var questionsToGenerate = 10 - profile.PrefetchedQuestions.Count;
            
            for (int i = 0; i < questionsToGenerate; i++)
            {
                if (cancellationToken.IsCancellationRequested) break;

                var question = await GenerateUniqueQuestion(profile);
                if (question != null)
                {
                    profile.PrefetchedQuestions.Enqueue(question);
                    _logger.LogInformation("Prefetched question {Index} for user {UserId}, total queued: {Total}", 
                        i + 1, userId, profile.PrefetchedQuestions.Count);
                }

                // Small delay between generations to not overwhelm the AI
                await Task.Delay(50, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error prefetching questions for user {UserId}", userId);
        }
    }

    private async Task<UserInteraction?> GenerateUniqueQuestion(UserProfile profile)
    {
      const int maxAttempts = 5;
        
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
      // Create a scope to access scoped services
     using var scope = _scopeFactory.CreateScope();
         var questionEngine = scope.ServiceProvider.GetRequiredService<DiscoveryQuestionEngine>();
            
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

    private string ComputeQuestionHash(UserInteraction interaction)
    {
        // Create a hash based on question text and options to detect duplicates
    var content = interaction.Content.Question;
        if (interaction.Content.Options?.Any() == true)
     {
            content += string.Join("|", interaction.Content.Options);
        }
  return content.GetHashCode().ToString();
    }
}
