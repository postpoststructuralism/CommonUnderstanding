# Background Processing Architecture

## Overview

Every user interaction triggers sophisticated AI analysis that happens **in the background** while users continue answering questions. This keeps the UI responsive while maximizing AI processing.

## How It Works

### 1. **Response Queue**
When a user submits a response:
- Response is immediately queued via `ResponseProcessingQueue.QueueResponse()`
- User gets next question instantly (from prefetch cache)
- Background service processes queued responses

### 2. **AI Analysis Pipeline**

Each response goes through multiple AI analysis stages:

```
User Response
    ↓
Queue Response (instant)
    ↓
[Background Processing Thread]
    ↓
Semantic Analysis ────→ ActivityUpdated: "Analyzing Response"
    ↓                      (0% → 33%)
Emotional Markers ────→ ActivityUpdated: "Computing Emotional Markers"
    ↓                      (33% → 50%)
Bayesian Inference ───→ ActivityUpdated: "Updating Belief Model"
    ↓                      (50% → 75%)
Update Profile ────────→ ActivityUpdated: "Analysis Complete"
    ↓                      (75% → 100%)
Remove from Queue
```

### 3. **Real-Time Activity Monitor**

The activity monitor shows what AI is doing in real-time:

- **Queued**: Response waiting for processing
- **Processing**: Active AI analysis with progress bar
- **Completed**: Analysis finished, confidence score shown
- **Error**: Processing failed (rare, logged for debugging)

## Implementation Details

### ResponseProcessingQueue Service

**File**: `Services/ResponseProcessingQueue.cs`

```csharp
public class ResponseProcessingQueue : BackgroundService
{
    // Runs continuously in background
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessNextBatch(stoppingToken);
            await Task.Delay(500, stoppingToken); // Check every 500ms
        }
    }
    
    // Processes single response with AI
    private async Task ProcessSingleResponse(QueuedResponse queuedResponse, ...)
    {
        // 1. Semantic analysis of response
        var analysis = await analysisEngine.AnalyzeResponseAsync(...);
        await NotifyActivity(..., "Analyzing Response", ..., 33);
        
        // 2. Emotional content detection
        var emotionalMarkers = await analysisEngine.AnalyzeEmotionalContentAsync(...);
        await NotifyActivity(..., "Computing Emotional Markers", ..., 50);
        
        // 3. Bayesian belief model update
        var updatedSnapshot = inferenceEngine.UpdateModel(...);
        await NotifyActivity(..., "Updating Belief Model", ..., 75);
        
        // 4. Save to profile
        profile.CurrentBeliefSnapshot = updatedSnapshot;
        await NotifyActivity(..., "Analysis Complete", ..., 100);
    }
}
```

### SignalR Real-Time Updates

**File**: `Hubs/DiscoveryHub.cs`

```csharp
// When response received from user
public async Task ProcessResponseStreaming(...)
{
    // Queue for background processing
    _responseQueue.QueueResponse(profileId, interaction);
    
    // Immediately return next question (from prefetch)
    await Clients.Caller.SendAsync("ReceiveQuestion", nextQuestion);
}
```

**File**: `Services/ResponseProcessingQueue.cs`

```csharp
// Notify clients of processing progress
private async Task NotifyActivity(...)
{
    await _hubContext.Clients.All.SendAsync("ActivityUpdated", new
    {
        Id = activityId,
        Title = title,
        Description = description,
        Status = status,      // queued/processing/completed/error
        Progress = progress   // 0-100
    });
}
```

### UI Activity Monitor

**File**: `Views/Shared/_ActivityMonitor.cshtml`

```javascript
// Listen for real-time updates
window.discoveryConnection.on('ActivityUpdated', function(activity) {
    // Update or add activity to monitor
    if (existingActivity) {
        existingActivity.title = activity.Title;
        existingActivity.description = activity.Description;
        existingActivity.status = activity.Status;
        existingActivity.progress = activity.Progress;
    } else {
        activities.unshift({
            id: activity.Id,
            title: activity.Title,
            description: activity.Description,
            status: activity.Status,
            progress: activity.Progress,
            startTime: Date.now()
        });
    }
    
    renderActivities(); // Update UI
});
```

## Performance Characteristics

### Batching
- Responses batched by user (configurable batch size)
- If 5+ responses queued: process as batch
- If 30s elapsed: process whatever is queued
- Single responses: processed immediately

### Queue Metrics (logged)
```
Processed response for user {ProfileId}
  - Queue time: 523ms
  - Processing time: 1,847ms
  - Confidence: 0.723
```

### Parallel Processing
- Multiple users: processed in parallel
- Single user: sequential (maintains coherence)

## Configuration

**File**: `Services/ResponseProcessingQueue.cs`

```csharp
private const int BatchSize = 5;          // Responses per batch
private const int BatchWindowMs = 30000;  // Max wait time (30s)
```

## Monitoring

### Logs to Watch
```
info: ResponseProcessingQueue[0]
      Response Processing Queue started
      
info: ResponseProcessingQueue[0]
      Analyzing response {InteractionId} for user {ProfileId}
      
info: ResponseProcessingQueue[0]
      Analyzing emotional content for interaction {InteractionId}
      
info: ResponseProcessingQueue[0]
      Updating belief model with Bayesian inference for user {ProfileId}
      
info: ResponseProcessingQueue[0]
      Processed response for user {ProfileId} - Queue time: 234ms, Processing time: 1,234ms, Confidence: 0.789
```

### UI Indicators
- **Activity Monitor Badge**: Shows count of active processing tasks
- **Activity Monitor Panel**: Click to expand and see details
- **Progress Bars**: Show real-time analysis progress (0-100%)
- **Status Dots**: 
  - 🟡 Queued (waiting)
  - 🔵 Processing (pulsing animation)
  - 🟢 Completed (fades after 5s)
  - 🔴 Error (shows error message)

## Skipped Responses

Even skipped responses get analyzed:

```csharp
if (isSkipped)
{
    response.RawText = "[SKIPPED]";
}

// Still queued for processing
_responseQueue.QueueResponse(profileId, interaction);
```

The AI learns from:
- What users choose to skip
- Patterns in skipping behavior
- Topic avoidance signals

## Benefits

1. **Responsive UI**: Users never wait for AI processing
2. **Continuous Learning**: Every interaction improves the model
3. **Transparency**: Activity monitor shows what AI is doing
4. **Asynchronous**: Multiple analyses can run simultaneously
5. **Resilient**: Errors don't block user flow

## Future Enhancements

- [ ] User-specific activity feed (currently broadcasts to all)
- [ ] Persistence of analysis results to database
- [ ] Adaptive batch sizing based on response complexity
- [ ] Priority queue for different interaction types
- [ ] Parallel emotional + semantic analysis
- [ ] Long-term belief evolution visualization

---

**Key Takeaway**: The system maintains a responsive UI while performing deep AI analysis on every interaction. The Activity Monitor provides transparency into what's happening behind the scenes.
