# Stuck Question Diagnostic Guide

## Problem: Same Question Appearing Repeatedly

If you're stuck on a question like "When you see someone in need, what's your first instinct?", this guide will help diagnose and fix the issue.

---

## Quick Diagnostic Steps

### Step 1: Check Debug Endpoint

While the app is running, open in browser:
```
http://localhost:5220/Discovery/Debug
or
https://localhost:7187/Discovery/Debug
```

**What to look for:**

```json
{
  "profileId": "abc123",
  "stage": "Initial",
  "interactionCount": 5,
  "prefetchedQuestionsCount": 0,  ? PROBLEM if this is 0!
  "askedQuestionHashes": 15,      ? Growing each time
  "confidence": 0.0,              ? Not updating (problem!)
  "pendingResponseQueue": 5,      ? Responses queued but not processed
  "prefetchedQuestions": [],      ? Empty! That's why same question repeats
  "lastInteraction": "When you see someone in need..."
}
```

### Step 2: Check Server Logs

Look for these patterns in the console output:

**Good Signs:**
```
[Information] Triggered prefetch for user abc123 after analysis completion
[Information] Pre-fetching psychometric question batch for user abc123
[Information] Successfully prefetched 5 psychometric questions
```

**Bad Signs:**
```
[Warning] No prefetched questions available for user abc123, generating synchronously
[Error] CRITICAL: All 5 generated questions were duplicates!
[Warning] PsychometricianAgent returned empty batch
```

---

## Common Causes & Solutions

### Cause 1: Prefetch Queue Empty

**Symptoms:**
- `prefetchedQuestionsCount: 0`
- Log: "No prefetched questions available"
- Same question appears repeatedly

**Why:**
- Responses queued but not processed yet
- Psychometric agent not being called
- Prefetch service not triggered

**Solution:**
Wait a few seconds between answers to allow background processing to complete.

**Manual Fix:**
```
1. Wait 5-10 seconds after answering
2. Refresh the page
3. Next question should be different
```

---

### Cause 2: All Generated Questions Are Duplicates

**Symptoms:**
- Log: "CRITICAL: All X generated questions were duplicates"
- `askedQuestionHashes` keeps growing
- Confidence not increasing

**Why:**
- Psychometric agent generating same questions
- Profile state not changing between generations
- Hash collision

**Automated Fix:**
The system now automatically clears the hash cache when this happens:
```
[Warning] Cleared 15 asked question hashes for user abc123 to prevent infinite loop
[Information] Retrying question generation after clearing hashes
```

**Manual Fix:**
Restart with a new profile (clear cookies).

---

### Cause 3: Response Processing Stuck

**Symptoms:**
- `pendingResponseQueue > 0` and not decreasing
- `confidence: 0` after multiple questions
- Prefetch never triggers

**Why:**
- Background service crashed
- Ollama not responding
- Exception in analysis code

**Check Logs For:**
```
[Error] Error in response processing queue
[Error] Failed to connect to Ollama
[Error] Error processing response for user abc123
```

**Solution:**
1. **Check Ollama is running:**
   ```bash
   curl http://localhost:11434
   # Should return: "Ollama is running"
   ```

2. **Restart the app:**
   ```bash
   dotnet run
   ```

---

### Cause 4: Psychometric Agent Failing

**Symptoms:**
- Log: "PsychometricianAgent returned empty batch"
- Falling back to individual generation
- Questions not adaptive

**Why:**
- JSON parsing failed
- LLM not returning valid JSON
- Prompt too long for model

**Check Logs For:**
```
[Warning] Failed to parse psychometric question batch as JSON
[Debug] Extracted JSON: X characters from Y character response
[Warning] No JSON array found in AI response
```

**Solution:**
The system automatically falls back to `DiscoveryQuestionEngine`:
```
[Information] Falling back to individual question generation
[Information] Fallback: Prefetched question 1 for user abc123
```

This is slower but functional.

---

### Cause 5: Profile State Not Updating

**Symptoms:**
- `confidence: 0` after 5+ questions
- Same questions in same order
- `interactionCount` increasing but confidence not

**Why:**
- Responses queued but never processed
- Bayesian inference not running
- Profile snapshot not being created

**Check Logs For:**
```
[Information] Processed response for user abc123 - Confidence: 0.352
[Information] Updating belief model with Bayesian inference
```

**If Missing:**
Background processing isn't running. Check:
1. `ResponseProcessingQueue` service started
2. No exceptions in background thread
3. Ollama responding to requests

---

## Recovery Procedures

### Option 1: Wait and Refresh

**Best for:** Temporary delays in background processing

```
1. Stop answering questions
2. Wait 10-15 seconds
3. Refresh the browser page
4. Check /Discovery/Debug to see if prefetchedQuestionsCount > 0
5. Continue answering
```

### Option 2: Clear Hash Cache (Automated)

**Best for:** All questions duplicates

The system now does this automatically when it detects the issue:
```
[Warning] Cleared X asked question hashes to prevent infinite loop
```

No manual intervention needed.

### Option 3: Restart Profile

**Best for:** Profile corrupted or stuck

```
1. Clear browser cookies for localhost
2. Go to /Discovery/Start
3. Fresh profile created
4. Start answering from beginning
```

### Option 4: Restart Application

**Best for:** Background services crashed

```bash
# Stop the app (Ctrl+C in terminal)
# Restart
dotnet run
```

---

## Preventing the Issue

### 1. Slower Pacing

**Don't rapid-fire questions:**
```
Answer question ? Wait 2-3 seconds ? Answer next question
```

This allows:
- Background processing to complete
- Profile updates to happen
- Prefetch to generate new questions

### 2. Monitor Debug Endpoint

**Periodically check:**
```
http://localhost:5220/Discovery/Debug
```

**Watch for:**
- `prefetchedQuestionsCount` should be 5-10
- `confidence` should increase over time
- `pendingResponseQueue` should be 0 or small

### 3. Check Logs

**Keep console visible:**
- Watch for error messages
- Verify prefetch triggers
- Confirm analysis completes

---

## Enhanced Logging

### Added in This Fix

**Duplicate Detection:**
```
[Warning] Skipped duplicate question for user abc123. 
  Question: "When you see someone in need...", Hash: 12345
```

**Critical State:**
```
[Error] CRITICAL: All 5 generated questions were duplicates! 
  Profile state: Confidence=0.35, Stage=Foundation, Interactions=12, HashCount=47
```

**Recovery:**
```
[Warning] Cleared 47 asked question hashes to prevent infinite loop
[Information] Retry successful: Added 5 questions after clearing hashes
```

---

## Understanding the Hash System

### How It Works

```csharp
// Hash includes question + options + context
var content = "When you see someone in need, what's your first instinct?";
content += "|I want to help - everyone deserves compassion|They should get a job...";
var hash = content.GetHashCode().ToString();
```

**Purpose:**
- Prevent exact duplicate questions
- Allow slight variations (different wording)
- Track what's been asked

### When Hashes Get Cleared

**Automatically when:**
- ALL questions in a batch are duplicates
- System detects infinite loop
- Recovery procedure triggered

**Manually when:**
- User starts new profile
- Cookies cleared
- Profile recreated

---

## Testing the Fix

### Verify Adaptive Behavior

1. **Start new profile**
2. **Answer first question:** Note the question
3. **Wait 3 seconds**
4. **Check debug endpoint:** Should see prefetch happening
5. **Answer second question:** Should be different
6. **Check logs:** Look for "Triggered prefetch after analysis"
7. **Answer 5 more questions:** Each should be unique and adaptive

### Expected Pattern

**Q1-Q5 (Initial):**
- From predefined question bank
- May cycle through same 5 questions
- Building baseline

**Q6+ (Foundation+):**
- Psychometrically generated
- Adaptive to your responses
- Unique and targeted

### Success Criteria

? No question repeats  
? Questions become more specific over time  
? `confidence` increases (check /Discovery/Debug)  
? `prefetchedQuestionsCount` stays 5-10  
? No error logs  

---

## Manual Intervention (Advanced)

### Force Prefetch Trigger

If you're comfortable with code:

```csharp
// In DiscoveryController.SubmitResponse(), temporarily add:
_prefetchService.RequestPrefetch(profileId);

// This forces immediate prefetch (don't leave this in production)
```

### Disable Duplicate Detection

If hash system is too aggressive:

```csharp
// In QuestionPrefetchService.PrefetchQuestionsForUser():
// Comment out:
// if (!profile.AskedQuestionHashes.Contains(questionHash))

// This allows all questions (including duplicates)
```

### Force Hash Clear

```csharp
// In DiscoveryController.SubmitResponse(), add:
if (profile.InteractionCount % 10 == 0)
{
    profile.AskedQuestionHashes.Clear();
    _logger.LogInformation("Cleared hashes at interaction {Count}", 
        profile.InteractionCount);
}
```

---

## When to Escalate

### Contact Support If:

- ? Same question appears 3+ times in a row
- ? Debug endpoint shows `confidence: 0` after 10+ questions
- ? Log shows repeated errors
- ? Ollama running but responses not processing
- ? Prefetch never triggers (check logs)

### Include in Report:

1. **Debug endpoint output** (JSON from /Discovery/Debug)
2. **Last 50 lines of logs** (from console)
3. **Steps to reproduce**
4. **How many questions answered before stuck**

---

## Summary

**Most Common Fix:**

```
1. Wait 5-10 seconds between answers
2. Check /Discovery/Debug - should see prefetch working
3. If stuck, refresh page
4. System will auto-clear hashes if needed
5. Continue answering
```

**The system now has:**
- ? Automatic hash clearing when stuck
- ? Better logging for diagnosis
- ? Debug endpoint for live monitoring
- ? Graceful fallback mechanisms

**You should rarely get stuck on the same question anymore!** ??
