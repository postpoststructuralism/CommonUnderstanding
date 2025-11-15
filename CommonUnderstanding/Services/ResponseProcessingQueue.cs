using CommonUnderstanding.Models;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

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
        ILogger<ResponseProcessingQueue> logger)
    {
        _profileStore = profileStore;
        _scopeFactory = scopeFactory;
        _logger = logger;
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
        _logger.LogInformation("Response Processing Queue started");

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
                _logger.LogError(ex, "Error in response processing queue");
                await Task.Delay(1000, stoppingToken);
            }
        }

        // Process remaining items on shutdown
        _logger.LogInformation("Response Processing Queue shutting down, processing remaining {Count} items", 
            _responseQueue.Count);
        
        while (_responseQueue.TryDequeue(out var response))
        {
            await ProcessSingleResponse(response, CancellationToken.None);
        }

        _logger.LogInformation("Response Processing Queue stopped");
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
                _logger.LogWarning("Profile {ProfileId} not found, dropping response", queuedResponse.ProfileId);
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var analysisEngine = scope.ServiceProvider.GetRequiredService<ResponseAnalysisEngine>();
            var inferenceEngine = scope.ServiceProvider.GetRequiredService<BayesianInferenceEngine>();

            // Analyze response
            var analysis = await analysisEngine.AnalyzeResponseAsync(queuedResponse.Interaction, profile);
            queuedResponse.Interaction.Analysis = analysis;

            // Analyze emotional content
            var emotionalMarkers = await analysisEngine.AnalyzeEmotionalContentAsync(
                queuedResponse.Interaction.Response.RawText);
            queuedResponse.Interaction.Response.Emotion = emotionalMarkers;

            // Update belief model
            var updatedSnapshot = inferenceEngine.UpdateModel(profile, queuedResponse.Interaction, analysis);
            
            if (profile.CurrentBeliefSnapshot != null)
            {
                profile.HistoricalSnapshots.Add(profile.CurrentBeliefSnapshot);
            }
            profile.CurrentBeliefSnapshot = updatedSnapshot;
            profile.LastInteractionAt = DateTime.UtcNow;

            var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
            var queueTime = (startTime - queuedResponse.QueuedAt).TotalMilliseconds;
            
            _logger.LogInformation(
                "Processed response for user {ProfileId} - Queue time: {QueueTime}ms, Processing time: {ProcessTime}ms, Confidence: {Confidence:F3}",
                queuedResponse.ProfileId, queueTime, processingTime, updatedSnapshot.OverallConfidence);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing response for user {ProfileId}", queuedResponse.ProfileId);
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

            // Process each response in sequence (for now - could parallelize analysis later)
            foreach (var queuedResponse in responses)
            {
                if (cancellationToken.IsCancellationRequested) break;

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
            }

            profile.LastInteractionAt = DateTime.UtcNow;

            var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
            var avgQueueTime = responses.Average(r => (startTime - r.QueuedAt).TotalMilliseconds);
            
            _logger.LogInformation(
                "Batch processed {Count} responses for user {ProfileId} - Avg queue time: {QueueTime}ms, Total processing time: {ProcessTime}ms",
                responses.Count, profileId, avgQueueTime, processingTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error batch processing responses for user {ProfileId}", profileId);
        }
    }
}
