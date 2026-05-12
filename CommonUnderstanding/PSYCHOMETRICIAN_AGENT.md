# Semantic Kernel & Psychometrician Agent Integration

## Overview

This document explains how Semantic Kernel powers the adaptive survey design in Common Understanding through the new **Psychometrician Agent** - an AI agent armed with cutting-edge psychometric measurement principles.

---

## Current Architecture

### **Before: Simple Question Generation**

```
User Profile ? Simple Prompt ? LLM ? Generic Question
```

The existing `DiscoveryQuestionEngine` uses basic prompts:
- "Ask a thoughtful question about values"
- "Ask about moral dilemmas"
- No psychometric optimization
- No information theory
- No adaptive testing strategy

### **Now: Psychometric-Optimized Generation**

```
User Profile ? Psychometric Context ? Expert Agent ? Optimal Question Batch
      ?              ?                      ?                  ?
  15 interactions  Uncertainty map    IRT principles    5 questions
  Confidence: 0.68  Contradictions    CAT strategy      Max info gain
  Entropy: 1.3      Moral foundations  MDAT design       Balanced content
```

---

## The Psychometrician Agent

### Purpose

The `PsychometricianAgent` is a specialized AI agent that applies **state-of-the-art adaptive survey design** principles to generate optimal question batches.

### Knowledge Base

The agent is equipped with expertise in:

#### 1. **Item Response Theory (IRT)**
- 1PL, 2PL, 3PL models for item difficulty and discrimination
- Item characteristic curves
- Information functions
- Ability estimation

#### 2. **Computerized Adaptive Testing (CAT)**
- Maximum Information item selection
- Bayesian ability estimation
- Stopping rules
- Content balancing

#### 3. **Multi-Dimensional Adaptive Testing (MDAT)**
- Simultaneous estimation across multiple dimensions
- Dimension-specific information
- Correlated trait models
- Profile analysis

#### 4. **Psychometric Measurement**
- Reliability (Cronbach's ?, test-retest)
- Validity (content, construct, criterion)
- Measurement error
- Standard error of measurement

#### 5. **Survey Design Best Practices**
- Question wording (avoiding bias, leading questions)
- Response format optimization
- Cognitive load management
- Engagement and fatigue prevention

---

## How It Works

### Step 1: Build Psychometric Context

The agent receives comprehensive context about the user:

```csharp
var psychometricContext = BuildPsychometricContext(profile);
```

**Context includes:**

- **Assessment Stage:** Initial / Foundation / Exploration / Refinement
- **Overall Confidence:** 0-1 scale
- **Model Entropy:** Lower = more certain
- **Response Consistency:** Higher = more reliable

- **High-Uncertainty Dimensions (Priority Targets):**
  ```
  - political-orientation: Confidence=0.42, Uncertainty=0.58, Position=0.2
  - spirituality: Confidence=0.31, Uncertainty=0.69, Position=unknown
  - moral-absolutism: Confidence=0.28, Uncertainty=0.72, Position=-0.4
  ```

- **Well-Estimated Dimensions (For Validation):**
  ```
  - compassion: Confidence=0.87, Position=0.8
  - fairness: Confidence=0.82, Position=0.6
  ```

- **Detected Contradictions:**
  ```
  - High authority respect (0.7) vs Low rule-following (0.3)
  - Strong individual freedom (0.9) vs High collective responsibility (0.8)
  ```

- **Moral Foundations Profile:**
  ```
  - Care: 8.2 (SE=0.15)
  - Fairness: 7.1 (SE=0.22)
  - Loyalty: 4.3 (SE=0.38) ? High uncertainty
  ```

- **Previous Questions:** Last 5 to avoid duplication

---

### Step 2: Generate Expert Prompt

The agent receives a comprehensive prompt with:

```
You are an expert psychometrician specializing in adaptive belief assessment...

**Optimization Criteria:**
1. Maximize Information Gain - Target dimensions with highest uncertainty
2. Content Balance - Cover diverse belief domains
3. Difficulty Calibration - Match complexity to current understanding
4. Cognitive Load - Vary question types to prevent fatigue
5. Engagement - Include compelling scenarios
6. Discrimination - High-discrimination items
7. Avoid Floor/Ceiling Effects

**Question Design Standards:**
- Forced-choice formats for psychometric rigor
- Each option maps to distinct belief position
- Context/scenarios for ecological validity
- Mutually exclusive and exhaustive options
- No leading questions or social desirability bias
- Balance positive/negative framing

**Output Format:**
[
  {
    "question_type": "multiple_choice|scale|ranking",
    "target_dimensions": ["dimension1", "dimension2"],
    "information_potential": 0.85,
    "difficulty_level": "medium",
    "context": "Scenario text...",
    "question": "Clear, unbiased question",
    "options": ["Option 1", "Option 2", ...],
    "dimension_mapping": {
      "Option 1": {"authority": 0.8, "liberty": -0.3},
      "Option 2": {"authority": -0.5, "liberty": 0.7}
    },
    "psychometric_rationale": "Targets authority/liberty conflict..."
  }
]
```

---

### Step 3: Semantic Kernel Invocation

```csharp
var kernel = _kernelService.GetKernel();
var result = await kernel.InvokePromptAsync(prompt);
var questionBatchJson = result.ToString();
```

**What happens:**
1. Prompt sent to Ollama via Semantic Kernel
2. LLM generates structured JSON with optimal questions
3. Each question designed to maximize information gain
4. Questions balanced across content areas
5. Difficulty calibrated to current ability estimate

---

### Step 4: Parse and Validate

```csharp
var questions = ParsePsychometricQuestionBatch(questionBatchJson, profile);
```

**Parsing includes:**
- JSON extraction
- Validation of question structure
- Mapping to `UserInteraction` model
- Fallback handling if parsing fails

---

## Example Output

### Context Snapshot
```
Assessment Stage: Exploration (15 interactions)
Overall Confidence: 0.68
Entropy: 1.32
Consistency: 0.79

High-Uncertainty Dimensions:
- political-economic: Conf=0.41, Unc=0.59, Pos=0.2
- religious-literal: Conf=0.33, Unc=0.67, Pos=unknown
- consequentialism: Conf=0.38, Unc=0.62, Pos=-0.3

Contradictions:
- High compassion (8.2) vs Low in-group loyalty (3.1)
```

### Generated Question Batch (5 questions)

#### Question 1: Multiple Choice (Targets: political-economic, fairness)
```json
{
  "question_type": "multiple_choice",
  "target_dimensions": ["political-economic", "fairness"],
  "information_potential": 0.89,
  "difficulty_level": "medium",
  "context": "A pharmaceutical company develops a life-saving drug but prices it at $100,000/year, making it unaffordable for most patients.",
  "question": "What should be done?",
  "options": [
    "The company has the right to charge market price - they took the risk",
    "Government should regulate pricing of essential medicines",
    "Company should recoup costs but limit profit margins",
    "Drug should be publicly funded and universally available",
    "Allow high pricing but require charity access for poor"
  ],
  "dimension_mapping": {
    "The company has the right...": {"political-economic": 0.9, "fairness": -0.3},
    "Government should regulate...": {"political-economic": -0.4, "fairness": 0.6},
    "Company should recoup...": {"political-economic": 0.2, "fairness": 0.5},
    "Drug should be publicly...": {"political-economic": -0.9, "fairness": 0.8},
    "Allow high pricing but...": {"political-economic": 0.5, "fairness": 0.3}
  },
  "psychometric_rationale": "High-discrimination item targeting political-economic position with secondary fairness measurement. Each option represents distinct position on individualism-collectivism continuum."
}
```

#### Question 2: Scale (Targets: religious-literal)
```json
{
  "question_type": "scale",
  "target_dimensions": ["religious-literal", "epistemology"],
  "information_potential": 0.82,
  "difficulty_level": "medium",
  "question": "Religious texts should be interpreted:",
  "options": [
    "1 - Literally and absolutely",
    "2 - Mostly literally with some context",
    "3 - Balance literal and metaphorical",
    "4 - Mostly metaphorically",
    "5 - Entirely as human-written metaphor"
  ],
  "psychometric_rationale": "Direct measurement of religious literalism with clear anchor points spanning full dimension."
}
```

#### Question 3: Multiple Choice (Targets: consequentialism, in-group-loyalty - Addresses Contradiction)
```json
{
  "question_type": "multiple_choice",
  "target_dimensions": ["consequentialism", "in-group-loyalty"],
  "information_potential": 0.91,
  "difficulty_level": "high",
  "context": "Your close friend plagiarized their thesis. If exposed, they'll be expelled and lose their scholarship. They confide in you.",
  "question": "What do you do?",
  "options": [
    "Report them immediately - rules are rules regardless of friendship",
    "Say nothing - loyalty to friends comes first",
    "Urge them to confess but won't force it",
    "Help them avoid detection - you'd want the same",
    "Weigh consequences: If small harm, protect them; if large, intervene"
  ],
  "dimension_mapping": {
    "Report them immediately...": {"consequentialism": -0.8, "in-group-loyalty": -0.9},
    "Say nothing...": {"consequentialism": 0.3, "in-group-loyalty": 0.9},
    "Urge them to confess...": {"consequentialism": -0.3, "in-group-loyalty": 0.4},
    "Help them avoid...": {"consequentialism": 0.6, "in-group-loyalty": 0.95},
    "Weigh consequences...": {"consequentialism": 0.9, "in-group-loyalty": 0.5}
  },
  "psychometric_rationale": "Resolves detected contradiction between high compassion and low loyalty by presenting forced-choice dilemma. High information potential due to current uncertainty in both dimensions."
}
```

#### Question 4: Ranking (Targets: Multiple dimensions for calibration)
```json
{
  "question_type": "ranking",
  "target_dimensions": ["values-hierarchy", "moral-foundations"],
  "information_potential": 0.76,
  "difficulty_level": "medium",
  "question": "Rank these principles in order of importance (1=most):",
  "options": [
    "Individual Liberty",
    "Social Equality",
    "National Security",
    "Economic Prosperity",
    "Environmental Sustainability"
  ],
  "psychometric_rationale": "Multi-dimensional measurement establishing value hierarchy. Lower information potential but high content validity for cross-validation."
}
```

#### Question 5: Multiple Choice (Targets: spirituality, human-nature)
```json
{
  "question_type": "multiple_choice",
  "target_dimensions": ["spirituality", "human-nature"],
  "information_potential": 0.85,
  "difficulty_level": "medium",
  "question": "Which statement best describes your view of human consciousness?",
  "options": [
    "Purely biological - neurons firing, nothing more",
    "Biological but gives rise to something special/emergent",
    "Connected to something beyond the physical",
    "Part of a universal consciousness",
    "I don't know and don't think we can know"
  ],
  "dimension_mapping": {
    "Purely biological...": {"spirituality": -0.9, "materialist": 0.9},
    "Biological but...": {"spirituality": 0.2, "materialist": 0.4},
    "Connected to...": {"spirituality": 0.7, "materialist": -0.3},
    "Part of universal...": {"spirituality": 0.95, "materialist": -0.8},
    "I don't know...": {"spirituality": 0.0, "epistemology": -0.4}
  },
  "psychometric_rationale": "Targets spirituality dimension with nuanced options spanning materialism-spiritualism continuum. Includes epistemic humility option to detect response style."
}
```

---

## Advantages Over Previous System

### Before (DiscoveryQuestionEngine):
- ? Generic prompts: "Ask about morality"
- ? No information theory
- ? Random question selection
- ? No difficulty calibration
- ? No content balancing
- ? No psychometric validation
- ? Single question at a time

### Now (PsychometricianAgent):
- ? Expert psychometric principles
- ? Information gain optimization
- ? Adaptive difficulty calibration
- ? Content balancing across domains
- ? Contradiction resolution
- ? Multi-dimensional targeting
- ? Batch generation (5 questions)
- ? Validation & fallback handling

---

## Integration Points

### 1. **QuestionPrefetchService**

Instead of generating questions one-by-one, call the Psychometrician Agent:

```csharp
// BEFORE
var question = await questionEngine.GenerateNextQuestionAsync(profile);

// NOW (Option 1: Batch prefetch)
var psychAgent = scope.ServiceProvider.GetRequiredService<PsychometricianAgent>();
var batch = await psychAgent.GenerateAdaptiveQuestionBatchAsync(profile, batchSize: 5);
foreach (var q in batch)
{
    profile.PrefetchedQuestions.Enqueue(q);
}
```

### 2. **BeliefDiscoveryOrchestrator**

Update `StartDiscoveryAsync` to use psychometric batches:

```csharp
// Generate initial psychometrically-optimized batch
var psychAgent = scope.ServiceProvider.GetRequiredService<PsychometricianAgent>();
var initialBatch = await psychAgent.GenerateAdaptiveQuestionBatchAsync(profile, batchSize: 10);

// Queue all but first
for (int i = 1; i < initialBatch.Count; i++)
{
    profile.PrefetchedQuestions.Enqueue(initialBatch[i]);
}

return initialBatch[0]; // Return first question
```

### 3. **ResponseProcessingQueue**

After processing responses, trigger psychometric batch generation:

```csharp
// After successful response processing
if (processedCount > 0 && processedCount % 5 == 0)
{
    // Every 5 responses, generate new optimized batch
    var psychAgent = scope.ServiceProvider.GetRequiredService<PsychometricianAgent>();
    var newBatch = await psychAgent.GenerateAdaptiveQuestionBatchAsync(profile, 5);
    
    foreach (var q in newBatch)
    {
        profile.PrefetchedQuestions.Enqueue(q);
    }
}
```

---

## Psychometric Recommendations System

The agent provides **context-aware recommendations** based on assessment state:

### Initial Stage (0-5 interactions)
- Use broad, foundational questions
- Include both cognitive and affective items
- Start with moderate difficulty

### Foundation Stage (5-15 interactions)
- Narrow focus to high-variance dimensions
- Introduce moral dilemmas for value hierarchies
- Use forced-choice items for relative importance

### Exploration Stage (15-30 interactions)
- Target specific uncertain dimensions
- Probe contradictions with consistency checks
- Situational judgment tests for validity

### Refinement Stage (30-60 interactions)
- High-specificity items to reduce uncertainty
- Nuanced scenarios for boundary testing
- Validate well-estimated dimensions

### Continuous Stage (60+ interactions)
- Monitor for belief evolution
- Longitudinal consistency checks
- Periodic re-assessment of foundations
- Novel dimension exploration

---

## Monitoring & Validation

### Question Quality Metrics

Each generated question includes:
- **Information Potential (0-1):** Expected information gain
- **Difficulty Level:** Low/Medium/High
- **Target Dimensions:** Which beliefs being measured
- **Discrimination:** How well options separate positions
- **Psychometric Rationale:** Why this question is optimal

### Batch Quality Metrics

- **Content Coverage:** % of uncertain dimensions targeted
- **Difficulty Distribution:** Balanced across user ability
- **Question Type Diversity:** Prevents fatigue
- **Information Efficiency:** Total expected information gain
- **Contradiction Resolution:** % of known contradictions addressed

---

## Future Enhancements

### Phase 1: Enhanced Psychometrics
- [ ] Implement actual IRT parameter estimation
- [ ] Calculate Fisher Information for each item
- [ ] Optimize with formal CAT algorithms
- [ ] Estimate standard error of measurement

### Phase 2: Machine Learning Integration
- [ ] Train item bank on historical data
- [ ] Predict response probabilities
- [ ] Optimize using RL (Reinforcement Learning)
- [ ] Automated item calibration

### Phase 3: Advanced Features
- [ ] Multi-stage adaptive testing
- [ ] Branching scenarios (decision trees)
- [ ] Implicit Association Tests (IAT)
- [ ] Adaptive conjoint analysis

---

## Configuration

### Batch Size
```csharp
// Small batch for frequent updates (more adaptive)
var batch = await psychAgent.GenerateAdaptiveQuestionBatchAsync(profile, batchSize: 3);

// Large batch for efficiency (less API calls)
var batch = await psychAgent.GenerateAdaptiveQuestionBatchAsync(profile, batchSize: 10);
```

### Question Type Preferences
```csharp
// Can be extended to specify preferences
var batch = await psychAgent.GenerateAdaptiveQuestionBatchAsync(
    profile, 
    batchSize: 5,
    preferMultipleChoice: true,  // Easier for users
    includeScales: 2,             // Include 2 scale questions
    includeDilemmas: 1            // Include 1 moral dilemma
);
```

---

## Semantic Kernel's Role

### What Semantic Kernel Provides:

1. **Prompt Engineering Framework**
   - Structured prompt templates
   - Variable substitution
   - Prompt composition

2. **LLM Abstraction**
   - Works with Ollama, OpenAI, Azure OpenAI, etc.
   - Consistent interface across providers
   - Easy model switching

3. **AI Orchestration**
   - Function calling
   - Agent coordination
   - Memory management
   - Plugin system

4. **Production-Ready Features**
   - Error handling
   - Retry logic
   - Logging
   - Telemetry

### In Our System:

```csharp
// Semantic Kernel manages:
var kernel = _kernelService.GetKernel();  // ? LLM abstraction
var result = await kernel.InvokePromptAsync(expertPrompt);  // ? Orchestration
var questions = result.ToString();  // ? Response parsing
```

---

## Comparison: Simple vs. Expert System

| Aspect | Old System | New Psychometrician Agent |
|--------|-----------|---------------------------|
| **Prompt Length** | ~50 words | ~1000 words |
| **Context** | None | Full psychometric state |
| **Output** | 1 question | 5 optimized questions |
| **Question Quality** | Generic | Psychometrically validated |
| **Information Gain** | Random | Maximized |
| **Difficulty** | Fixed | Adaptive |
| **Content Balance** | Random | Systematic |
| **Contradictions** | Ignored | Actively resolved |
| **Validation** | None | Multi-metric |
| **Expertise** | General | Specialist |

---

## Sample Usage

```csharp
// 1. Get the agent from DI
var psychAgent = scope.ServiceProvider.GetRequiredService<PsychometricianAgent>();

// 2. Generate optimized batch
var questions = await psychAgent.GenerateAdaptiveQuestionBatchAsync(
    profile,
    batchSize: 5
);

// 3. Queue for delivery
foreach (var question in questions)
{
    profile.PrefetchedQuestions.Enqueue(question);
}

// 4. User answers questions
// 5. Responses analyzed via BayesianInferenceEngine
// 6. Model updated with new confidence estimates
// 7. Next batch adapts to new state

// Cycle repeats for optimal assessment
```

---

## Benefits Summary

### For Users:
- ? **Better Questions:** More relevant, engaging, thought-provoking
- ? **Faster Assessment:** Higher information per question
- ? **Less Fatigue:** Varied question types, appropriate difficulty
- ? **More Accurate:** Psychometrically validated measurement

### For System:
- ? **Higher Quality Data:** Better discrimination, less noise
- ? **Faster Convergence:** Reach target confidence in fewer questions
- ? **Systematic Coverage:** No dimension neglected
- ? **Adaptive Optimization:** Continuously improving

### For Research:
- ? **Psychometric Rigor:** IRT, CAT, MDAT principles
- ? **Reproducible:** Documented methodology
- ? **Extensible:** Easy to add new psychometric features
- ? **Validated:** Each question has quality metrics

---

## Conclusion

The **Psychometrician Agent** transforms Common Understanding from a generic survey system into a **state-of-the-art adaptive assessment platform** powered by:

- Latest psychometric research
- Information theory
- Bayesian inference
- Semantic Kernel orchestration
- Expert AI agent design

This represents a **quantum leap** in belief discovery quality, bringing academic-grade measurement to an open-source platform.

---

**Next Steps:**

1. Review `PsychometricianAgent.cs` implementation
2. Integrate into `QuestionPrefetchService`
3. Update `BeliefDiscoveryOrchestrator`
4. Test with real users
5. Monitor quality metrics
6. Iterate and improve

**The agent is ready to deploy! ??**
