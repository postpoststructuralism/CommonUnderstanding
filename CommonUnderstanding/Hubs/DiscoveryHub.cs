using Microsoft.AspNetCore.SignalR;
using CommonUnderstanding.Models;
using CommonUnderstanding.Services;

namespace CommonUnderstanding.Hubs;

/// <summary>
/// SignalR hub for real-time streaming of AI-powered belief discovery
/// </summary>
public class DiscoveryHub : Hub
{
    private readonly BeliefDiscoveryOrchestrator _orchestrator;
    private readonly DiscoveryQuestionEngine _questionEngine;
    private readonly ResponseAnalysisEngine _analysisEngine;
    private readonly BayesianInferenceEngine _inferenceEngine;
    private readonly UserProfileStore _profileStore;
    private readonly ILogger<DiscoveryHub> _logger;

    public DiscoveryHub(
        BeliefDiscoveryOrchestrator orchestrator,
        DiscoveryQuestionEngine questionEngine,
        ResponseAnalysisEngine analysisEngine,
        BayesianInferenceEngine inferenceEngine,
        UserProfileStore profileStore,
        ILogger<DiscoveryHub> logger)
    {
        _orchestrator = orchestrator;
        _questionEngine = questionEngine;
        _analysisEngine = analysisEngine;
        _inferenceEngine = inferenceEngine;
        _profileStore = profileStore;
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
            
            // Validate we have some kind of response
            if (string.IsNullOrWhiteSpace(actualResponse))
            {
                await Clients.Caller.SendAsync("Error", "Please provide a response");
                return;
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

            // Stage 1: Analyze response
            await Clients.Caller.SendAsync("StatusUpdate", "🔍 Analyzing your response...", 15);
            await Task.Delay(300); // Small delay for UX
            var analysis = await _analysisEngine.AnalyzeResponseAsync(interaction, profile);
            interaction.Analysis = analysis;

            // Stage 2: Emotional analysis
            await Clients.Caller.SendAsync("StatusUpdate", "💭 Understanding emotional content...", 35);
            await Task.Delay(300);
            var emotionalMarkers = await _analysisEngine.AnalyzeEmotionalContentAsync(fullResponseText);
            interaction.Response.Emotion = emotionalMarkers;

            // Stage 3: Bayesian update
            await Clients.Caller.SendAsync("StatusUpdate", "🧮 Updating belief model with Bayesian inference...", 55);
            await Task.Delay(300);
            var updatedSnapshot = _inferenceEngine.UpdateModel(profile, interaction, analysis);
            
            if (profile.CurrentBeliefSnapshot != null)
            {
                profile.HistoricalSnapshots.Add(profile.CurrentBeliefSnapshot);
            }
            profile.CurrentBeliefSnapshot = updatedSnapshot;
            profile.LastInteractionAt = DateTime.UtcNow;
            profile.Stage = DetermineStage(profile);

            // Stage 4: Generate next question
            await Clients.Caller.SendAsync("StatusUpdate", "💡 Generating your next question...", 80);
            await Task.Delay(300);
            var nextQuestion = await _questionEngine.GenerateNextQuestionAsync(profile);
            _profileStore.SetPendingInteraction(profileId, nextQuestion);

            // Complete
            await Clients.Caller.SendAsync("StatusUpdate", "✨ Complete!", 100);
            await Clients.Caller.SendAsync("ProcessingComplete", new
            {
                UpdatedConfidence = updatedSnapshot.OverallConfidence,
                NextQuestion = nextQuestion,
                InteractionCount = profile.InteractionCount,
                Stage = profile.Stage.ToString()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing response for profile {ProfileId}", profileId);
            await Clients.Caller.SendAsync("Error", $"Processing failed: {ex.Message}");
        }
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

            await Clients.Caller.SendAsync("StatusUpdate", "Generating first question...", 50);
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
