using Microsoft.AspNetCore.SignalR;
using CommonUnderstanding.Models;
using CommonUnderstanding.Services;
using System.Security.Cryptography;
using System.Text;

namespace CommonUnderstanding.Hubs;

/// <summary>
/// SignalR hub for real-time streaming of AI-powered belief discovery
/// </summary>
public class DiscoveryHub : Hub
{
    private readonly BeliefDiscoveryOrchestrator _orchestrator;
    private readonly DiscoveryQuestionEngine _questionEngine;
    private readonly ResponseProcessingQueue _responseQueue;
    private readonly UserProfileStore _profileStore;
    private readonly QuestionPrefetchService _prefetchService;
    private readonly ILogger<DiscoveryHub> _logger;

    public DiscoveryHub(
        BeliefDiscoveryOrchestrator orchestrator,
        DiscoveryQuestionEngine questionEngine,
        ResponseProcessingQueue responseQueue,
        UserProfileStore profileStore,
        QuestionPrefetchService prefetchService,
        ILogger<DiscoveryHub> logger)
    {
        _orchestrator = orchestrator;
        _questionEngine = questionEngine;
        _responseQueue = responseQueue;
        _profileStore = profileStore;
        _prefetchService = prefetchService;
        _logger = logger;
    }

    /// <summary>
    /// Stream AI-generated question with real-time token updates
    /// </summary>
    public async Task StreamQuestion(string profileId)
    {
        try
        {
            await Clients.Caller.SendAsync("StatusUpdate", "Generating question...", 0);

            var profile = _profileStore.GetProfile(profileId);
            if (profile == null)
            {
                await Clients.Caller.SendAsync("Error", "Profile not found");
                return;
            }
            
            // TODO: Stream tokens as they're generated
            // For now, generate and send in chunks
            var question = await _questionEngine.GenerateNextQuestionAsync(profile);
            
            _profileStore.SetPendingInteraction(profileId, question);

            await Clients.Caller.SendAsync("StatusUpdate", "Question ready", 100);
            await Clients.Caller.SendAsync("QuestionGenerated", question);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming question for profile {ProfileId}", profileId);
            await Clients.Caller.SendAsync("Error", $"Failed to generate question: {ex.Message}");
        }
    }

    /// <summary>
    /// Process user response with streaming status updates
    /// NOW: Queue response immediately and return next question, show progress as background processes
    /// </summary>
    public async Task ProcessResponseStreaming(string profileId, string? selectedOption, string? responseText, string? response, double? numericValue)
    {
        try
        {
            var profile = _profileStore.GetProfile(profileId);
            if (profile == null)
            {
                await Clients.Caller.SendAsync("Error", "Profile not found");
                return;
            }

            var interaction = _profileStore.GetPendingInteraction(profileId);
            if (interaction == null)
            {
                await Clients.Caller.SendAsync("Error", "No pending interaction");
                return;
            }

            // Determine the actual response based on question format
            string actualResponse = response ?? selectedOption ?? "";
            
            // For scale questions, use the numeric value as the response
            if (interaction.Content.Format == InteractionFormat.Scale && numericValue.HasValue)
            {
                actualResponse = numericValue.Value.ToString();
            }
            
            // Allow skipped responses (users keeping engagement while background processing happens)
            if (string.IsNullOrWhiteSpace(actualResponse) || actualResponse == "skipped")
            {
                actualResponse = "[SKIPPED - User chose to continue without answering]";
            }

            // Combine selected option with any additional text
            var fullResponseText = actualResponse;
            if (!string.IsNullOrWhiteSpace(responseText))
            {
                fullResponseText += "\n\nAdditional context: " + responseText;
            }

            // Fill in response
            interaction.Response = new UserResponse
            {
                RawText = fullResponseText,
                NumericValue = numericValue,
                SelectedOptions = !string.IsNullOrWhiteSpace(selectedOption) 
                    ? new List<string> { selectedOption } 
                    : null
            };
            interaction.ResponseTimeMs = (long)(DateTime.UtcNow - interaction.Timestamp).TotalMilliseconds;
            profile.Interactions.Add(interaction);

            // IMMEDIATELY queue for background processing
            await Clients.Caller.SendAsync("StatusUpdate", "✓ Response recorded", 20);
            _responseQueue.QueueResponse(profileId, interaction);
            
            var pendingCount = _responseQueue.GetPendingCountForUser(profileId);
            await Clients.Caller.SendAsync("StatusUpdate", 
                $"⚡ Queued for analysis ({pendingCount} pending)", 40);
            
            // Notify activity monitor
            var responsePreview = fullResponseText.Length > 50 
                ? fullResponseText.Substring(0, 47) + "..." 
                : fullResponseText;
            await Clients.Caller.SendAsync("ActivityQueued", new
            {
                Id = interaction.Id,
                Title = "Response Queued",
                Description = $"Analyzing: \"{responsePreview}\"",
                Status = "queued"
            });

            // IMMEDIATELY get next question from prefetch queue
            UserInteraction? nextQuestion = null;
            if (profile.PrefetchedQuestions.TryDequeue(out var prefetchedQuestion))
            {
                // Hash was already added when prefetched
                nextQuestion = prefetchedQuestion;
                await Clients.Caller.SendAsync("StatusUpdate", 
                    $"💡 Next question ready ({profile.PrefetchedQuestions.Count} queued)", 80);
            }
            else
            {
                // Fallback: generate question now if prefetch is empty
                await Clients.Caller.SendAsync("StatusUpdate", "⏳ Generating question...", 60);
                nextQuestion = await _questionEngine.GenerateNextQuestionAsync(profile);
                var hash = ComputeQuestionHash(nextQuestion);
                profile.AskedQuestionHashes.Add(hash);
            }

            _profileStore.SetPendingInteraction(profileId, nextQuestion);
            
            // Trigger prefetch to keep queue full
            _prefetchService.RequestPrefetch(profileId);

            // Complete - don't wait for AI processing
            await Clients.Caller.SendAsync("StatusUpdate", "✨ Ready!", 100);
            await Clients.Caller.SendAsync("ProcessingComplete", new
            {
                NextQuestion = nextQuestion,
                InteractionCount = profile.InteractionCount,
                Stage = profile.Stage.ToString(),
                PendingAnalysis = pendingCount,
                PrefetchedQuestions = profile.PrefetchedQuestions.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing response for profile {ProfileId}", profileId);
            await Clients.Caller.SendAsync("Error", $"Processing failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Compute stable hash for question to detect duplicates
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
        
        // Use SHA256 for stable hashing across runs
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToBase64String(hashBytes);
    }

    /// <summary>
    /// Start a new discovery session
    /// </summary>
    public async Task StartDiscovery(string userName)
    {
        try
        {
            await Clients.Caller.SendAsync("StatusUpdate", "Initializing discovery...", 10);

            var profile = new UserProfile
            {
                Name = userName,
                Stage = DiscoveryStage.Initial
            };

            _profileStore.AddProfile(profile);

            await Clients.Caller.SendAsync("StatusUpdate", "Generating first question...", 30);
            var firstQuestion = await _orchestrator.StartDiscoveryAsync(profile);
            _profileStore.SetPendingInteraction(profile.Id, firstQuestion);

            await Clients.Caller.SendAsync("StatusUpdate", "Ready to begin!", 100);
            await Clients.Caller.SendAsync("DiscoveryStarted", new
            {
                ProfileId = profile.Id,
                Question = firstQuestion
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting discovery for {UserName}", userName);
            await Clients.Caller.SendAsync("Error", $"Failed to start: {ex.Message}");
        }
    }
}
