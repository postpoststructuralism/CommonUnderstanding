# AI Batch Processing Strategy

## Overview

The system now processes responses in **batches** to optimize AI/Ollama usage. Instead of making individual AI calls for each response, we collect responses and process them together.

## Current Implementation

### Response Batching

**File**: `Services/ResponseProcessingQueue.cs`

**Strategy**: Collect up to 5 responses from the same user, then process together

```csharp
BatchSize = 5            // Process 5 responses at once
BatchWindowMs = 2000     // Wait max 2 seconds to fill batch
```

### How It Works

1. **Collection Phase**: Responses added to batch buffer
2. **Trigger Conditions**: 
   - Batch reaches 5 responses, OR
   - 2 seconds elapsed since last batch
3. **Processing**: All responses in batch processed sequentially
4. **Update**: Belief model updated after each response in batch

### Current Sequence (Per Response in Batch)

```csharp
foreach (var response in batch)
{
    // 1. Analyze response
    var analysis = await analysisEngine.AnalyzeResponseAsync(response, profile);
    
    // 2. Analyze emotions
    var emotions = await analysisEngine.AnalyzeEmotionalContentAsync(response.Text);
    
    // 3. Update belief model
    var snapshot = inferenceEngine.UpdateModel(profile, response, analysis);
}
```

## Future Enhancement: True Batch Processing

### Goal: Single AI Call for Multiple Responses

Instead of:
```
AI Call 1 → Analyze Response 1
AI Call 2 → Analyze Response 2  
AI Call 3 → Analyze Response 3
```

Do:
```
AI Call 1 → Analyze Responses 1, 2, 3 together
```

### Implementation Strategy

#### 1. Batch Analysis Prompt

```csharp
public async Task<List<ResponseAnalysis>> AnalyzeResponseBatchAsync(
    List<UserInteraction> interactions,
    UserProfile profile)
{
    var prompt = $$$"""
    You are analyzing multiple responses from the same user to build their belief profile.
    
    User Background:
    - Current confidence level: {{{profile.CurrentBeliefSnapshot?.OverallConfidence ?? 0:F2}}}
    - Stage: {{{profile.Stage}}}
    - Response count: {{{profile.InteractionCount}}}
    
    Analyze these {{{interactions.Count}}} responses together:
    
    {{{BuildBatchResponseList(interactions)}}}
    
    For each response, provide:
    1. Implied values and beliefs
    2. Moral foundation activations (Care, Fairness, Loyalty, Authority, Sanctity, Liberty)
    3. Reasoning patterns (Consequentialist, Deontological, Virtue Ethics)
    4. Emotional markers (intensity, certainty, detected emotions)
    5. Confidence in analysis (0-1)
    
    Return as JSON array with one object per response:
    [
      {
        "responseIndex": 0,
        "impliedValues": ["freedom", "equality"],
        "moralFoundations": {"Care": 7.5, "Fairness": 8.0, ...},
        "reasoningPatterns": ["Consequentialist"],
        "emotionalMarkers": {"intensity": 0.7, "certainty": 0.8, "emotions": ["compassion"]},
        "confidence": 0.75
      },
      ...
    ]
    """;
    
    var result = await kernel.InvokePromptAsync(prompt);
    return ParseBatchAnalysis(result.ToString(), interactions.Count);
}
```

#### 2. Batch Emotional Analysis

```csharp
public async Task<List<EmotionalMarkers>> AnalyzeEmotionalBatchAsync(
    List<string> responseTexts)
{
    var prompt = $$$"""
    Analyze the emotional content of these {{{responseTexts.Count}}} responses.
    
    {{{string.Join("\n\n", responseTexts.Select((t, i) => 
        $"Response {i + 1}:\n{t}"))}}}
    
    For each response, rate:
    - Intensity (0-1): How strong are the emotions?
    - Certainty (0-1): How confident is the speaker?
    - Emotions: List detected emotions
    - Conflict (0-1): Internal conflict level
    
    Return as JSON array.
    """;
    
    var result = await kernel.InvokePromptAsync(prompt);
    return ParseEmotionalBatch(result.ToString());
}
```

#### 3. Context-Aware Batch Processing

**Key Insight**: Later responses in the batch can reference earlier ones

```csharp
var prompt = $$$"""
Analyze these responses IN SEQUENCE - later responses may build on earlier ones.

Response 1 (about individual freedom):
"{{{responses[0].Text}}}"

Response 2 (about collective good):
"{{{responses[1].Text}}}"

Response 3 (about balancing freedom and community):
"{{{responses[2].Text}}}"

Notice how Response 3 might reconcile or contradict Responses 1 and 2.
Look for patterns across all responses.
""";
```

### Benefits of Batch Processing

1. **Fewer AI Calls**: 5 responses → 1 AI call (5x reduction)
2. **Context Awareness**: AI sees responses together, spots patterns
3. **Consistency**: Single analysis pass = more consistent scoring
4. **Speed**: One large call faster than 5 small calls (less overhead)
5. **Cost**: Reduced API calls (if using cloud AI)

### Challenges to Address

1. **Token Limits**: Large batches may exceed context window
   - **Solution**: Limit batch size based on total tokens
   - **Fallback**: Split into smaller sub-batches if needed

2. **Parsing Complexity**: Extract multiple structured results
   - **Solution**: Use JSON output format
   - **Validation**: Check array length matches input count

3. **Partial Failures**: One response fails to parse
   - **Solution**: Return best-effort results
   - **Fallback**: Re-analyze failed items individually

4. **Model Quality**: Does batching reduce accuracy?
   - **Testing**: A/B test batch vs individual analysis
   - **Metrics**: Compare confidence scores and user satisfaction

## Recommended Next Steps

### Phase 1: Add Batch Analysis Methods
- [ ] Create `AnalyzeResponseBatchAsync()` in ResponseAnalysisEngine
- [ ] Create `AnalyzeEmotionalBatchAsync()` in ResponseAnalysisEngine
- [ ] Add JSON parsing for batch results

### Phase 2: Update ResponseProcessingQueue
- [ ] Call batch methods instead of individual analysis
- [ ] Add error handling for batch failures
- [ ] Implement fallback to individual processing

### Phase 3: Optimize Batch Size
- [ ] Monitor token usage per batch
- [ ] Add dynamic batch sizing based on response length
- [ ] Tune BatchSize and BatchWindowMs parameters

### Phase 4: Advanced Features
- [ ] Cross-response pattern detection
- [ ] Contradiction detection across batch
- [ ] Evolution tracking within batch (response 1 → response 5)

## Example Batch Prompt

```
SYSTEM: You are an expert in analyzing belief systems and moral psychology.

USER: Analyze these 5 responses from a user exploring their beliefs:

Q1: "What matters most in a fair society?"
A1: "Everyone should have equal opportunities, but people should be rewarded for hard work."

Q2: "How do you feel about wealth inequality?"
A2: "Some inequality is natural and motivating, but extreme gaps are unhealthy."

Q3: "Should the government redistribute wealth?"
A3: "Limited redistribution for basic needs, but not full equality of outcomes."

Q4: "What about inherited wealth?"
A4: "People should be able to pass on what they earn, but maybe with estate taxes."

Q5: "Is meritocracy possible?"
A5: "It's an ideal to strive for, even if we never fully achieve it."

ANALYSIS TASK:
Detect overall patterns:
1. This user values: [fairness, merit, pragmatism, nuance]
2. Moral foundations activated: Fairness (8/10), Liberty (7/10), Care (6/10)
3. Political leanings: Center-left on economics (values equality AND merit)
4. Reasoning style: Pragmatic consequentialist (focuses on outcomes, not ideology)
5. Certainty: Moderate (0.6) - open to trade-offs and complexity
6. Evolution: Responses show consistent worldview, not contradictory

This demonstrates the USER'S TRUE BELIEF: "Balance fairness with merit through limited redistribution"
```

## Token Budget Management

### Estimating Batch Size

**Average response length**: ~100 tokens  
**Question context**: ~50 tokens  
**Analysis prompt**: ~200 tokens

**Per response**: 150 tokens input  
**Batch of 5**: 750 tokens input  
**Expected output**: ~500 tokens (100 tokens × 5 responses)

**Total per batch**: ~1250 tokens

**Model limits**:
- llama2:7b: 4096 tokens → 3 responses per batch
- llama2:13b: 4096 tokens → 3 responses per batch  
- mixtral:8x7b: 32768 tokens → 20+ responses per batch

**Recommendation**: Set `BatchSize = 3` for safety with 7B models

## Performance Metrics to Track

```csharp
// Log these for monitoring
_logger.LogInformation(
    "Batch analysis - Responses: {Count}, " +
    "Input tokens: ~{InputTokens}, " +
    "Processing time: {Ms}ms, " +
    "Per-response time: {PerMs}ms",
    batchSize,
    estimatedTokens,
    totalMs,
    totalMs / batchSize
);
```

## Configuration

```csharp
// appsettings.json
{
  "ResponseProcessing": {
    "BatchSize": 3,           // Smaller for 7B models
    "BatchWindowMs": 2000,    // 2 seconds to collect
    "MaxBatchTokens": 3000,   // Safety limit
    "EnableBatchAnalysis": true,  // Feature flag
    "FallbackToIndividual": true  // If batch fails
  }
}
```
