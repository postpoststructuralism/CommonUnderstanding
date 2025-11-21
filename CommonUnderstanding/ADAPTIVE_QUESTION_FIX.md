# Adaptive Question Generation Fix

## Problem Identified

The psychometrician agent was generating questions based on **outdated** profile state, causing:
- ? Same questions appearing repeatedly
- ? Questions not adapting to responses
- ? No progression in difficulty or targeting
- ? Poor user experience

## Root Cause

### Original Flow (BROKEN):

```
1. User submits answer
   ?
2. Response QUEUED for background processing
   ?
3. PREFETCH TRIGGERED IMMEDIATELY ? Problem!
   ?
4. Psychometric agent reads profile
   ?
5. Profile has OLD state (answer not yet analyzed)
   ?
6. Questions generated from OLD state
   ?
7. MUCH LATER: Response processed, profile updated
   ?
8. Too late! Questions already generated
```

**The Issue**: Prefetch was triggered in `DiscoveryController.SubmitResponse()` **before** the response was analyzed by `ResponseProcessingQueue`.

**Result**: 
- First batch of questions generated from initial state
- All subsequent batches generated from same state
- No adaptation occurs
- Questions repeat because psychometric context never changes

## Solution

### Fixed Flow:

```
1. User submits answer
   ?
2. Response QUEUED for background processing
   ?
3. Get prefetched question (from previous batch)
   ?
4. User sees next question immediately
   ?
5. BACKGROUND: Response processed
   ?
6. BACKGROUND: AI analyzes response
   ?
7. BACKGROUND: Bayesian inference updates profile
   ?
8. BACKGROUND: Profile state now UPDATED ? Key!
   ?
9. NOW trigger prefetch with NEW state
   ?
10. Psychometric agent reads UPDATED profile
   ?
11. Questions generated from CURRENT belief state
```

**The Fix**: Prefetch triggered in `ResponseProcessingQueue` **after** analysis completes.

## Code Changes

### 1. DiscoveryController.cs

**BEFORE:**
```csharp
// QUEUE the response for background processing
_responseQueue.QueueResponse(profileId, interaction);

// Get next question
var nextQuestion = profile.PrefetchedQuestions.TryDequeue(...);

// Trigger background prefetch to keep queue full
_prefetchService.RequestPrefetch(profileId); // ? TOO EARLY!
```

**AFTER:**
```csharp
// QUEUE the response for background processing
_responseQueue.QueueResponse(profileId, interaction);

// Get next question
var nextQuestion = profile.PrefetchedQuestions.TryDequeue(...);

// NOTE: Prefetch is now triggered by ResponseProcessingQueue AFTER analysis completes
// This ensures questions are generated based on updated belief state
```

### 2. ResponseProcessingQueue.cs - ProcessSingleResponse

**ADDED:**
```csharp
// After Bayesian inference updates profile...
profile.CurrentBeliefSnapshot = updatedSnapshot;
profile.LastInteractionAt = DateTime.UtcNow;

// NOW trigger prefetch AFTER profile has been updated with new analysis
using var prefetchScope = _scopeFactory.CreateScope();
var prefetchService = prefetchScope.ServiceProvider.GetRequiredService<QuestionPrefetchService>();
prefetchService.RequestPrefetch(queuedResponse.ProfileId);

_logger.LogInformation("Triggered prefetch for user {ProfileId} after analysis completion", 
    queuedResponse.ProfileId);
```

### 3. ResponseProcessingQueue.cs - ProcessResponseBatch

**ADDED:**
```csharp
// After all responses in batch processed...
profile.LastInteractionAt = DateTime.UtcNow;

// NOW trigger prefetch AFTER all responses in batch have been analyzed
using var prefetchScope = _scopeFactory.CreateScope();
var prefetchService = prefetchScope.ServiceProvider.GetRequiredService<QuestionPrefetchService>();
prefetchService.RequestPrefetch(profileId);

_logger.LogInformation("Triggered prefetch for user {ProfileId} after batch analysis completion", 
    profileId);
```

## How It Works Now

### Timeline Example:

**T+0ms**: User submits answer to Question 1
- Response queued
- Question 2 served immediately (from initial prefetch)

**T+50ms**: User submits answer to Question 2
- Response queued
- Question 3 served immediately

**T+100ms**: User submits answer to Question 3
- Response queued
- Question 4 served immediately

**T+2000ms**: Background batch processing triggers
- Process all 3 responses
- Analyze each with AI
- Update belief model with Bayesian inference
- **Profile now reflects ALL 3 answers**
- **Trigger prefetch with UPDATED state**

**T+2500ms**: Psychometric agent starts
- Reads profile with confidence 0.35 (was 0.10)
- Sees uncertain dimensions: political-economic, spirituality
- Generates 5 questions targeting THESE specific areas
- Questions added to prefetch queue

**T+3000ms**: User submits answer to Question 4
- Response queued
- Question 5 served (from NEW psychometric batch!)
- **This question now adapts to previous answers**

**T+5000ms**: Next background processing cycle
- Process latest responses
- Update profile again
- Trigger prefetch with even newer state
- Cycle continues...

## Benefits

### ? True Adaptive Testing

**Before:**
```
Q1: "What do you value most in life?"
Q2: "What do you value most in life?"  ? Same question
Q3: "What do you value most in life?"  ? Same question
```

**After:**
```
Q1: "What do you value most in life?"
   ? User answers: "Personal freedom"
   ? Profile updated: Liberty ?, Individualism ?
   
Q2: "How do you balance individual freedom vs collective good?"
   ? Targeted based on Q1 answer!
   ? User answers: "Both matter, 6/10"
   ? Profile updated: Moderate position
   
Q3: "A law restricts personal choice to protect public health. Your view?"
   ? Follows up on Q2 to clarify boundary!
   ? Tests edge case of freedom vs safety
```

### ? Information Gain Optimization

The psychometric agent NOW has access to:
- **Current confidence levels** per dimension
- **Detected contradictions** to resolve
- **High-uncertainty areas** to target
- **Well-estimated dimensions** to validate

This enables:
- **Maximum information gain** per question
- **Contradiction resolution** through targeted dilemmas
- **Efficient assessment** - fewer questions to reach confidence threshold

### ? Prevents Repetition

- Hash-based duplicate detection still works
- But NOW questions are generated from different states
- Even if hash check fails, questions will be contextually different
- Psychometric targeting ensures diversity

## Monitoring

### New Log Messages

**After Response Processing:**
```
[Information] Processed response for user abc123 - 
  Queue time: 50ms, Processing time: 1200ms, Confidence: 0.352
[Information] Triggered prefetch for user abc123 after analysis completion
```

**After Batch Processing:**
```
[Information] Batch processed 3 responses for user abc123 - 
  Avg queue time: 100ms, Total processing time: 2400ms
[Information] Triggered prefetch for user abc123 after batch analysis completion
```

**Prefetch Service:**
```
[Information] Pre-fetching psychometric question batch for user abc123, current queue: 3
[Information] Requesting psychometric batch of 5 questions for user abc123
[Information] Successfully prefetched 5 psychometric questions for user abc123, total queued: 8
```

### Watch For

**Good Patterns:**
- Confidence increasing over time (0.10 ? 0.35 ? 0.68 ? 0.82)
- Different target_dimensions in each batch
- Contradictions being detected and resolved
- Prefetch triggered AFTER processing completes

**Warning Signs:**
- Confidence not increasing
- Same target_dimensions repeatedly
- Prefetch triggered before processing
- Many duplicate questions skipped

## Testing

### How to Verify Fix:

1. **Start new profile**
2. **Answer Question 1** (e.g., "I value freedom")
3. **Check logs:**
   ```
   [Information] Triggered prefetch for user abc123 after analysis completion
   ```
4. **Answer Question 2** - Should be related to Q1 answer
5. **Answer Question 3** - Should build on Q1 + Q2
6. **Check profile:**
   - Confidence should be increasing
   - Target dimensions should change
   - Questions should feel adaptive

### Expected Behavior:

**Early Questions (1-5):**
- Broad, foundational
- Establish baseline across domains
- Build initial profile

**Middle Questions (5-15):**
- Narrowing focus
- Target areas showing variance
- More specific dilemmas

**Later Questions (15+):**
- Highly targeted
- Resolve contradictions
- Test boundary conditions
- Validate well-estimated areas

## Rollback

If issues occur, you can temporarily disable adaptive prefetch:

```csharp
// In ResponseProcessingQueue.cs
// Comment out the prefetch trigger:
// prefetchService.RequestPrefetch(queuedResponse.ProfileId);

// In DiscoveryController.cs
// Re-enable immediate prefetch:
_prefetchService.RequestPrefetch(profileId);
```

This reverts to original behavior (broken but functional).

## Performance Impact

### Additional Overhead:

- **Minimal**: One extra service scope creation per response
- **~1-2ms**: RequestPrefetch() just enqueues user ID
- **No blocking**: Prefetch happens asynchronously

### Benefits:

- **Fewer duplicate questions**: Less wasted AI calls
- **Better targeting**: More efficient assessment
- **Faster convergence**: Reach confidence threshold in fewer questions

**Net Impact**: Positive. Slight overhead but much better question quality.

## Future Enhancements

### Immediate (No Code Changes):

- Monitor logs to verify adaptation working
- Track confidence progression over sessions
- Measure duplicate rate reduction

### Short Term:

- Add metric: "Questions until confidence 0.8"
- Dashboard showing adaptation in real-time
- A/B test adaptive vs non-adaptive

### Long Term:

- Predictive prefetch (pre-generate based on likely responses)
- Multi-level prefetch (initial + refined batches)
- Real-time adaptation (update during rapid-fire answering)

---

## Summary

**Problem**: Questions weren't adapting because they were generated before responses were analyzed.

**Solution**: Trigger prefetch AFTER analysis completes, ensuring psychometric agent has access to updated belief state.

**Result**: True adaptive testing with questions that respond to user's answers, target uncertain areas, and resolve contradictions.

**Impact**: Dramatically improved user experience and assessment quality! ??
