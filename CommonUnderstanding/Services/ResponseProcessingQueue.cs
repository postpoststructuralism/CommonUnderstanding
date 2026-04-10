using CommonUnderstanding.Models;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.SignalR;
using CommonUnderstanding.Hubs;

namespace CommonUnderstanding.Services;

/// <summary>
/// Queued response for background processing
/// </summary>
public class QueuedResponse
{
    public string ProfileId { get; set; } = string.Empty;
    public UserInteraction Interaction { get; set; } = null!;
    public DateTime QueuedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Background service that processes user responses asynchronously
/// This allows the UI to immediately proceed to the next question while
/// AI analysis happens in the background
/// </summary>
public class ResponseProcessingQueue : BackgroundService
{
    private readonly UserProfileStore _profileStore;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ResponseProcessingQueue> _logger;
    private readonly IHubContext<DiscoveryHub> _hubContext;
    private readonly ConcurrentQueue<QueuedResponse> _responseQueue = new();
    private readonly ConcurrentDictionary<string, List<QueuedResponse>> _batchBuffer = new();
    private DateTime _lastBatchProcess = DateTime.UtcNow;
    
    // Configuration
    private const int MaxQueueSize = 1000;
    private const int BatchSize = 5; // Process up to 5 responses at once
    private const int BatchWindowMs = 2000; // Wait up to 2 seconds to fill a batch

    public ResponseProcessingQueue(
        UserProfileStore profileStore,
        IServiceScopeFactory scopeFactory,
        ILogger<ResponseProcessingQueue> logger,
        IHubContext<DiscoveryHub> hubContext)
    {
        _profileStore = profileStore;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _hubContext = hubContext;
    }

    /// <summary>
    /// Queue a response for background processing
    /// </summary>
    public bool QueueResponse(string profileId, UserInteraction interaction)
    {
        if (_responseQueue.Count >= MaxQueueSize)
        {
            _logger.LogWarning("Response queue full, dropping response for user {ProfileId}", profileId);
            return false;
        }

        var queuedResponse = new QueuedResponse
        {
            ProfileId = profileId,
            Interaction = interaction
        };

        _responseQueue.Enqueue(queuedResponse);
        _logger.LogInformation("Queued response for user {ProfileId}, queue size: {QueueSize}", 
            profileId, _responseQueue.Count);
        
        // IMMEDIATELY notify UI that response is queued (synchronously, don't wait)
        // Don't use Task.Run - just fire it off
        _ = NotifyActivity(profileId, interaction.Id, 
            "Response Queued", 
            "Your response is waiting for AI analysis", 
            "queued", 0);
        
        return true;
    }

    /// <summary>
    /// Get queue depth for monitoring
    /// </summary>
    public int GetQueueDepth() => _responseQueue.Count;

    /// <summary>
    /// Get pending response count for a specific user
    /// </summary>
    public int GetPendingCountForUser(string profileId)
    {
        return _responseQueue.Count(r => r.ProfileId == profileId) +
               (_batchBuffer.TryGetValue(profileId, out var batch) ? batch.Count : 0);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🚀 Response Processing Queue started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Process individual responses or batches
                await ProcessNextBatch(stoppingToken);
                
                // Small delay to prevent tight loop
                await Task.Delay(50, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in response processing queue");
                await Task.Delay(1000, stoppingToken);
            }
        }

        // Process remaining items on shutdown
        _logger.LogInformation("🛑 Response Processing Queue shutting down, processing remaining {Count} items", 
            _responseQueue.Count);
        
        while (_responseQueue.TryDequeue(out var response))
        {
            await ProcessSingleResponse(response, CancellationToken.None);
        }

        _logger.LogInformation("✅ Response Processing Queue stopped");
    }

    private async Task ProcessNextBatch(CancellationToken cancellationToken)
    {
        // Collect responses into batches by user
        var batchReady = false;
        var timeSinceLastBatch = (DateTime.UtcNow - _lastBatchProcess).TotalMilliseconds;

        // Dequeue items into batch buffer
        while (_responseQueue.TryDequeue(out var queuedResponse))
        {
            if (!_batchBuffer.ContainsKey(queuedResponse.ProfileId))
            {
                _batchBuffer[queuedResponse.ProfileId] = new List<QueuedResponse>();
            }

            _batchBuffer[queuedResponse.ProfileId].Add(queuedResponse);

            // If any user has enough responses, process batch
            if (_batchBuffer[queuedResponse.ProfileId].Count >= BatchSize)
            {
                batchReady = true;
                break;
            }
        }

        // Process batches if ready or time window expired
        if (batchReady || (timeSinceLastBatch > BatchWindowMs && _batchBuffer.Any()))
        {
            await ProcessAllBatches(cancellationToken);
            _lastBatchProcess = DateTime.UtcNow;
        }
    }

    private async Task ProcessAllBatches(CancellationToken cancellationToken)
    {
        var tasks = new List<Task>();

        foreach (var (profileId, responses) in _batchBuffer.ToArray())
        {
            _batchBuffer.TryRemove(profileId, out _);
            
            if (responses.Count == 1)
            {
                // Single response - process immediately
                tasks.Add(ProcessSingleResponse(responses[0], cancellationToken));
            }
            else
            {
                // Multiple responses - batch process
                tasks.Add(ProcessResponseBatch(profileId, responses, cancellationToken));
            }
        }

        // Process all batches in parallel
        await Task.WhenAll(tasks);
    }

    private async Task ProcessSingleResponse(QueuedResponse queuedResponse, CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            var profile = _profileStore.GetProfile(queuedResponse.ProfileId);
            if (profile == null)
            {
                _logger.LogWarning("⚠️ Profile {ProfileId} not found, dropping response", queuedResponse.ProfileId);
                return;
            }

            _logger.LogInformation("🔍 Processing response for user {UserId}, queue depth: {Depth}", 
                queuedResponse.ProfileId, _responseQueue.Count);

            using var scope = _scopeFactory.CreateScope();
            var analysisEngine = scope.ServiceProvider.GetRequiredService<ResponseAnalysisEngine>();
            var inferenceEngine = scope.ServiceProvider.GetRequiredService<BayesianInferenceEngine>();

            // Notify: Starting processing
            await NotifyActivity(queuedResponse.ProfileId, queuedResponse.Interaction.Id, 
                "Analyzing Response", 
                "AI is analyzing your response with semantic understanding", 
                "processing", 0);

            // Analyze response
            _logger.LogInformation("Analyzing response {InteractionId} for user {ProfileId}", 
                queuedResponse.Interaction.Id, queuedResponse.ProfileId);
            var analysis = await analysisEngine.AnalyzeResponseAsync(queuedResponse.Interaction, profile);
            queuedResponse.Interaction.Analysis = analysis;
            
            await NotifyActivity(queuedResponse.ProfileId, queuedResponse.Interaction.Id,
                "Analyzing Response", "Response analyzed", "processing", 33);

            // Analyze emotional content
            _logger.LogInformation("Analyzing emotional content for interaction {InteractionId}", 
                queuedResponse.Interaction.Id);
            var emotionalMarkers = await analysisEngine.AnalyzeEmotionalContentAsync(
                queuedResponse.Interaction.Response.RawText);
            queuedResponse.Interaction.Response.Emotion = emotionalMarkers;
            
            await NotifyActivity(queuedResponse.ProfileId, queuedResponse.Interaction.Id,
                "Computing Emotional Markers", 
                $"Detected: {string.Join(", ", emotionalMarkers.DetectedEmotions.Take(3))}", 
                "processing", 50);

            // Update belief model
            _logger.LogInformation("Updating belief model with Bayesian inference for user {ProfileId}", 
                queuedResponse.ProfileId);
            var updatedSnapshot = inferenceEngine.UpdateModel(profile, queuedResponse.Interaction, analysis);
            
            await NotifyActivity(queuedResponse.ProfileId, queuedResponse.Interaction.Id,
                "Updating Belief Model", 
                "Bayesian inference on belief dimensions", 
                "processing", 75);
            
            if (profile.CurrentBeliefSnapshot != null)
            {
                profile.HistoricalSnapshots.Add(profile.CurrentBeliefSnapshot);
            }
            profile.CurrentBeliefSnapshot = updatedSnapshot;
            profile.LastInteractionAt = DateTime.UtcNow;
            
            await NotifyActivity(queuedResponse.ProfileId, queuedResponse.Interaction.Id,
                "Analysis Complete", 
                $"Confidence: {updatedSnapshot.OverallConfidence:P0}", 
                "completed", 100);

            var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
            var queueTime = (startTime - queuedResponse.QueuedAt).TotalMilliseconds;
            
            _logger.LogInformation(
                "Processed response for user {ProfileId} - Queue time: {QueueTime}ms, Processing time: {ProcessTime}ms, Confidence: {Confidence:F3}",
                queuedResponse.ProfileId, queueTime, processingTime, updatedSnapshot.OverallConfidence);
            
            // NOW trigger prefetch AFTER profile has been updated with new analysis
            // This ensures psychometric agent generates questions based on latest belief state
            using var prefetchScope = _scopeFactory.CreateScope();
            var prefetchService = prefetchScope.ServiceProvider.GetRequiredService<QuestionPrefetchService>();
            prefetchService.RequestPrefetch(queuedResponse.ProfileId);

            // Persist the updated profile to the database so it survives restarts
            await _profileStore.SaveProfileAsync(queuedResponse.ProfileId);

            _logger.LogInformation("Triggered prefetch for user {ProfileId} after analysis completion", 
                queuedResponse.ProfileId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing response for user {ProfileId}", queuedResponse.ProfileId);
            await NotifyActivity(queuedResponse.ProfileId, queuedResponse.Interaction.Id,
                "Analysis Failed", 
                $"Error: {ex.Message}", 
                "error", 0);
        }
    }
    
    private async Task NotifyActivity(string profileId, string activityId, string title, string description, string status, int progress)
    {
        try
        {
            _logger.LogInformation("Sending ActivityUpdated: {Title} ({Progress}%) - Status: {Status}", title, progress, status);
            
            // This would ideally be sent to specific user, but for now send to all
            await _hubContext.Clients.All.SendAsync("ActivityUpdated", new
            {
                Id = activityId,
                ProfileId = profileId,
                Title = title,
                Description = description,
                Status = status,
                Progress = progress
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify activity update");
        }
    }

    private async Task ProcessResponseBatch(string profileId, List<QueuedResponse> responses, CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            _logger.LogInformation("Batch processing {Count} responses for user {ProfileId}", responses.Count, profileId);

            var profile = _profileStore.GetProfile(profileId);
            if (profile == null)
            {
                _logger.LogWarning("Profile {ProfileId} not found, dropping {Count} responses", profileId, responses.Count);
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var analysisEngine = scope.ServiceProvider.GetRequiredService<ResponseAnalysisEngine>();
            var inferenceEngine = scope.ServiceProvider.GetRequiredService<BayesianInferenceEngine>();

            // Notify batch started
            await NotifyActivity(profileId, $"batch-{startTime.Ticks}",
                "Batch Processing Started", 
                $"Processing {responses.Count} responses together", 
                "processing", 0);

            // Process each response in sequence (for now - could parallelize analysis later)
            int processedCount = 0;
            foreach (var queuedResponse in responses)
            {
                if (cancellationToken.IsCancellationRequested) break;

                // Notify individual response processing
                await NotifyActivity(profileId, queuedResponse.Interaction.Id,
                    $"Analyzing Response ({processedCount + 1}/{responses.Count})", 
                    "AI analysis in progress", 
                    "processing", (processedCount * 100) / responses.Count);

                var analysis = await analysisEngine.AnalyzeResponseAsync(queuedResponse.Interaction, profile);
                queuedResponse.Interaction.Analysis = analysis;

                var emotionalMarkers = await analysisEngine.AnalyzeEmotionalContentAsync(
                    queuedResponse.Interaction.Response.RawText);
                queuedResponse.Interaction.Response.Emotion = emotionalMarkers;

                var updatedSnapshot = inferenceEngine.UpdateModel(profile, queuedResponse.Interaction, analysis);
                
                if (profile.CurrentBeliefSnapshot != null)
                {
                    profile.HistoricalSnapshots.Add(profile.CurrentBeliefSnapshot);
                }
                profile.CurrentBeliefSnapshot = updatedSnapshot;

                // Mark individual response as completed
                await NotifyActivity(profileId, queuedResponse.Interaction.Id,
                    "Analysis Complete", 
                    $"Confidence: {updatedSnapshot.OverallConfidence:P0}", 
                    "completed", 100);

                processedCount++;
            }

            profile.LastInteractionAt = DateTime.UtcNow;

            // Notify batch completed
            await NotifyActivity(profileId, $"batch-{startTime.Ticks}",
                "Batch Processing Complete", 
                $"Processed {processedCount} responses", 
                "completed", 100);

            var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
            var avgQueueTime = responses.Average(r => (startTime - r.QueuedAt).TotalMilliseconds);
            
            _logger.LogInformation(
                "Batch processed {Count} responses for user {ProfileId} - Avg queue time: {QueueTime}ms, Total processing time: {ProcessTime}ms",
                responses.Count, profileId, avgQueueTime, processingTime);
            
            // NOW trigger prefetch AFTER all responses in batch have been analyzed
            // This ensures psychometric agent generates questions based on fully updated belief state
            using var prefetchScope = _scopeFactory.CreateScope();
            var prefetchService = prefetchScope.ServiceProvider.GetRequiredService<QuestionPrefetchService>();
            prefetchService.RequestPrefetch(profileId);
            
            _logger.LogInformation("Triggered prefetch for user {ProfileId} after batch analysis completion", 
                profileId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error batch processing responses for user {ProfileId}", profileId);
            
            // Notify error for batch
            foreach (var response in responses)
            {
                await NotifyActivity(profileId, response.Interaction.Id,
                    "Batch Processing Failed", 
                    $"Error: {ex.Message}", 
                    "error", 0);
            }
        }
    }
}
