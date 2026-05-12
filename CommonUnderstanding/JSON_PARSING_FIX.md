# JSON Parsing Fix for PsychometricianAgent

## Issue

The AI (Ollama LLM) was returning responses with preamble text before the JSON:

```
Based on the provided requirements and standards, I've designed five optimal questions for the user's next assessment batch. Here they are:
```json
[
  {
    "question_type": "multiple_choice",
    ...
  }
]
```
```

Instead of just:

```json
[
  {
    "question_type": "multiple_choice",
    ...
  }
]
```

This caused `JsonDocument.Parse()` to fail because it received the entire response including the preamble.

---

## Solution

### 1. **Extract JSON from Response**

Updated `ParsePsychometricQuestionBatch` to extract just the JSON array:

```csharp
// Extract JSON array from response (AI may include preamble text)
var jsonStart = questionBatchJson.IndexOf('[');
var jsonEnd = questionBatchJson.LastIndexOf(']');

if (jsonStart == -1 || jsonEnd == -1 || jsonEnd <= jsonStart)
{
    _logger.LogWarning("No JSON array found in AI response");
    return FallbackQuestionGeneration(profile);
}

var jsonText = questionBatchJson.Substring(jsonStart, jsonEnd - jsonStart + 1);

// Now parse the extracted JSON
var jsonDoc = JsonDocument.Parse(jsonText);
```

**What this does:**
- Finds first `[` character
- Finds last `]` character
- Extracts everything between them
- Parses only the JSON portion

### 2. **Improve Prompt Instructions**

Added explicit instructions in the prompt:

```
**CRITICAL: Output ONLY a valid JSON array with NO preamble, explanation, or markdown formatting.**
**Start your response with [ and end with ]**

Generate EXACTLY {batchSize} questions as a valid JSON array. Do not include any text before or after the JSON array.
```

**Why this helps:**
- Makes it crystal clear what format is expected
- Reduces likelihood of preamble text
- Still works even if AI ignores instruction (extraction handles it)

### 3. **Better Error Handling**

```csharp
catch (JsonException ex)
{
    _logger.LogWarning(ex, "Failed to parse JSON. Response preview: {Preview}", 
        questionBatchJson.Substring(0, Math.Min(300, questionBatchJson.Length)));
    return FallbackQuestionGeneration(profile);
}
```

**Benefits:**
- Logs the actual problematic response
- Returns empty list ? triggers fallback in QuestionPrefetchService
- System continues working even if psychometric generation fails

---

## Why This Happens

### LLM Behavior

Most LLMs (including Ollama models) are trained to be conversational and helpful, so they often:

1. **Add context:** "Here are the questions you requested..."
2. **Use markdown:** ` ```json ... ``` `
3. **Explain their output:** "Based on the requirements..."
4. **Format for readability:** Pretty-printed with explanations

### Our Requirements

We need **structured data**, not conversational output:
- Raw JSON only
- No markdown code blocks
- No explanations
- Parseable by `JsonDocument.Parse()`

---

## Testing the Fix

### Good Response (No Preamble)

```json
[
  {
    "question_type": "multiple_choice",
    "target_dimensions": ["political-economic"],
    "question": "What should the government do?",
    "options": ["Option 1", "Option 2"]
  }
]
```

**Result:** ? Parses directly

### Response with Preamble

```
Based on the analysis, here are 5 questions:
[
  {
    "question_type": "multiple_choice",
    "target_dimensions": ["political-economic"],
    "question": "What should the government do?",
    "options": ["Option 1", "Option 2"]
  }
]
```

**Result:** ? Extracts JSON, then parses

### Response with Markdown

````
```json
[
  {
    "question_type": "multiple_choice",
    ...
  }
]
```
````

**Result:** ? Extracts content between `[` and `]`, ignores markdown

### Malformed Response (No JSON)

```
I cannot generate questions right now.
```

**Result:** ? Returns empty list ? Fallback triggered

---

## Monitoring

### Log Messages to Watch

**Success:**
```
[Information] Successfully parsed 5 psychometric questions for user abc123
```

**Extraction:**
```
[Debug] Extracted JSON: 1234 characters from 1500 character response
```

**Warning:**
```
[Warning] No JSON array found in AI response for user abc123
[Warning] Using fallback question generation for user abc123
```

**Error:**
```
[Warning] Failed to parse psychometric question batch as JSON
```

---

## Configuration Options

### Adjust Extraction Logic

If you need to handle different formats:

```csharp
// Option 1: Also handle markdown code blocks
var jsonStart = questionBatchJson.IndexOf("```json");
if (jsonStart != -1)
{
    jsonStart = questionBatchJson.IndexOf('[', jsonStart);
}
else
{
    jsonStart = questionBatchJson.IndexOf('[');
}

// Option 2: Use regex for more sophisticated extraction
var match = Regex.Match(questionBatchJson, @"\[[\s\S]*\]");
if (match.Success)
{
    var jsonText = match.Value;
}
```

### Stricter Validation

```csharp
// Validate question count matches request
if (questions.Count != batchSize)
{
    _logger.LogWarning("Expected {Expected} questions but got {Actual}", 
        batchSize, questions.Count);
}

// Validate required fields
foreach (var q in questions)
{
    if (string.IsNullOrEmpty(q.Content.Question))
    {
        _logger.LogWarning("Question has empty text");
    }
}
```

---

## Alternative Solutions Considered

### 1. **Structured Output API** (Not Available in Ollama)

Some LLM providers (OpenAI, Anthropic) support structured output:
```csharp
// Hypothetical API
var response = await client.GenerateStructuredAsync<QuestionBatch>(prompt);
```

**Status:** Not available in Ollama yet

### 2. **JSON Mode** (Model-Dependent)

Some models support JSON mode:
```csharp
var settings = new OllamaSettings { Format = "json" };
```

**Status:** Limited support, not reliable across models

### 3. **Post-Processing Always**

Current solution: Always extract JSON from response
**Status:** ? Implemented and working

---

## Best Practices

### For Prompts:

1. ? **Be explicit:** "Output ONLY JSON"
2. ? **Show format:** Provide exact structure
3. ? **Use imperatives:** "Do not include..."
4. ? **Repeat instructions:** Multiple reminders

### For Parsing:

1. ? **Extract first:** Find JSON in response
2. ? **Validate:** Check structure before parsing
3. ? **Log errors:** Include actual response in logs
4. ? **Fallback:** Always have a backup plan

### For Testing:

1. ? **Test variations:** Preamble, markdown, clean JSON
2. ? **Test failures:** Malformed JSON, no JSON
3. ? **Monitor logs:** Watch for parsing warnings
4. ? **Measure success rate:** Track % successful parses

---

## Success Metrics

Track these in production:

```csharp
// Success Rate
var successRate = SuccessfulParses / TotalAttempts;
// Target: > 90%

// Average Response Length
var avgLength = TotalResponseChars / TotalResponses;
// Target: < 5000 chars (indicates minimal preamble)

// Extraction Required Rate
var extractionRate = ExtractionsRequired / TotalAttempts;
// Target: < 50% (indicates prompt is working)
```

---

## Related Files

- `PsychometricianAgent.cs` - The agent generating questions
- `QuestionPrefetchService.cs` - Calls the agent and handles fallback
- `PSYCHOMETRICIAN_AGENT.md` - Full documentation
- `PREFETCH_INTEGRATION.md` - Integration details

---

**The fix is deployed and tested! ??**

The system now handles:
- ? Clean JSON responses
- ? Responses with preamble text
- ? Markdown-formatted responses
- ? Malformed or missing JSON (fallback)
