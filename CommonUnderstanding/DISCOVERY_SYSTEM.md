# Common Understanding - Discovery System

## 🎯 Overview

The **Discovery System** is the heart of Common Understanding - an AI-powered, statistically rigorous platform for **inferring belief systems through conversation**. Instead of asking users to describe their worldview (which most people struggle with), the system engages in adaptive dialogue, analyzing responses using Bayesian inference to build an evolving mental model.

## 🧠 Core Innovation

**Problem**: People don't know themselves well enough to articulate their belief systems accurately.

**Solution**: Interactive discovery through:
- Adaptive questioning (moral dilemmas, scenarios, value rankings)
- Statistical analysis of responses
- Bayesian belief inference
- Continuous model refinement

## 📊 Technical Architecture

### 1. Domain Models

**UserProfile** - Tracks each individual user
- Current belief snapshot
- Historical snapshots (versioned over time)
- Interaction history
- Discovery stage progression

**BeliefSnapshot** - Point-in-time mental model
- Belief dimensions with confidence intervals
- Inferred values (ranked by confidence × importance)
- Moral foundations profile (Haidt's framework)
- Statistical metadata (entropy, consistency, signal-to-noise)
- Narrative summary (AI-generated)

**UserInteraction** - Each question/response pair
- Question content (type, format, context)
- User response (text, numeric, emotional markers)
- AI analysis (dimension updates, values, reasoning patterns)
- Response time and targeted dimensions

### 2. AI Services

**DiscoveryQuestionEngine**
- Generates adaptive questions based on current model state
- Types: Open-ended, moral dilemmas, scale questions, value rankings, emotional scenarios
- Targets uncertain areas and contradictions
- Stage-aware (Initial → Foundation → Exploration → Refinement → Continuous)

**ResponseAnalysisEngine**
- Analyzes responses using AI
- Extracts belief dimensions, values, reasoning patterns
- Scores moral foundations
- Detects emotional markers
- Suggests follow-up areas

**BayesianInferenceEngine** ⭐ **Statistical Core**
- Bayesian updating of belief distributions
- Gaussian priors and posteriors
- Precision-weighted averaging
- Confidence calculation (sample size × variance)
- Entropy, consistency, and signal-to-noise metrics
- Contradiction detection

**BeliefDiscoveryOrchestrator**
- Coordinates entire discovery process
- Processes responses → Updates model → Generates next question
- Adaptive question strategy selection
- Manages discovery stages

### 3. Statistical Rigor

#### Bayesian Update Formula
```
Posterior Mean = (PriorPrecision × PriorMean + LikelihoodPrecision × ObservationMean) / PosteriorPrecision

PosteriorPrecision = PriorPrecision + LearningRate × LikelihoodPrecision
```

#### Confidence Calculation
```
DimensionConfidence = (1 - e^(-n/10)) × e^(-variance)
- Increases with sample size (n)
- Decreases with uncertainty (variance)
```

#### Model Statistics
- **Entropy**: Shannon entropy of belief distribution
- **Consistency**: Temporal stability of responses
- **Signal-to-Noise**: Confidence / Uncertainty ratio
- **Contradiction Detection**: Opposing high-confidence dimensions

## 🎮 User Experience Flow

### 1. Initial Discovery (Questions 1-5)
- Welcoming, foundational questions
- Open-ended to establish baseline
- Build rapport and trust

**Example**: "What matters most to you in life?"

### 2. Foundation Building (Questions 5-15)
- Explore core values and principles
- Mix of question types
- Build confidence in major dimensions

**Example**: "Consider a scenario where..."

### 3. Exploration (Questions 15-30)
- Target uncertain areas
- Present moral dilemmas
- Test edge cases

**Example**: "Rank these values: Freedom, Security, Equality, Justice, Community"

### 4. Refinement (Questions 30-60)
- Address contradictions
- Fine-tune boundaries
- Emotional probes

**Example**: "Earlier you said X, but this response suggests Y. Help us understand..."

### 5. Continuous Learning (60+)
- Maintain and update model
- Track belief evolution over time
- Detect changes in thinking

## 📈 Metrics & Analytics

### Per-User Metrics
- **Overall Confidence**: 0-1 (how well we understand this person)
- **Sample Size**: Per dimension
- **Uncertainty**: Standard deviation of belief position
- **Consistency Score**: Internal coherence
- **Coverage**: Number of dimensions explored

### Model Quality
- **Entropy**: Information content
- **Signal-to-Noise Ratio**: Quality of inferences
- **Contradiction Rate**: % of incompatible beliefs detected

## 🔬 Moral Foundations Theory Integration

Based on Jonathan Haidt's research, we track six foundations:

1. **Care/Harm** - Compassion, nurturing
2. **Fairness/Cheating** - Justice, rights, equality
3. **Loyalty/Betrayal** - Group solidarity
4. **Authority/Subversion** - Respect for hierarchy
5. **Sanctity/Degradation** - Purity, sacredness
6. **Liberty/Oppression** - Freedom from domination

Each scored 0-10 with confidence intervals and standard error.

## 🎯 Question Strategy

The system selects question types adaptively:

| Condition | Strategy |
|-----------|----------|
| < 3 interactions | Open-ended foundation building |
| Every 5th interaction | Value ranking (calibration) |
| Contradictions detected | Follow-up clarification |
| High uncertainty areas | Moral dilemma or scale question |
| Default | Moral dilemma for depth |

## 💾 Data Structure

### Snapshot Evolution
```
UserProfile
├── Interaction 1 → Response → Analysis → Snapshot v1 (confidence: 0.1)
├── Interaction 2 → Response → Analysis → Snapshot v2 (confidence: 0.2)
├── Interaction 3 → Response → Analysis → Snapshot v3 (confidence: 0.3)
...
└── Interaction N → Current Snapshot (confidence: 0.8)
```

Each snapshot is versioned and stored, allowing:
- Historical analysis
- Evolution tracking
- Regression detection
- A/B testing of inference algorithms

## 🚀 Key Features

### 1. Adaptive Questioning
- Context-aware question generation
- Targets knowledge gaps
- Addresses contradictions
- Emotional intelligence

### 2. Statistical Rigor
- Bayesian inference (not just heuristics)
- Confidence intervals on all estimates
- Proper uncertainty quantification
- Mathematically sound updates

### 3. Multi-Dimensional Profiling
- Values (importance × confidence)
- Moral foundations (6 dimensions)
- Custom belief dimensions (unlimited)
- Reasoning patterns

### 4. Evolution Tracking
- Versioned snapshots
- Historical comparison
- Change detection
- Belief stability metrics

### 5. Transparency
- Show confidence levels
- Explain uncertain areas
- Display evidence for each inference
- Allow users to see the model

## 🔮 Future Enhancements

### Statistical
- [ ] Implement full Bayesian networks (belief dependencies)
- [ ] Add hierarchical models for value systems
- [ ] Implement change-point detection (belief shifts)
- [ ] Add predictive modeling (forecast responses)

### Question Types
- [ ] Implicit Association Tests (IAT)
- [ ] Time-based response analysis
- [ ] Visual/image-based scenarios
- [ ] Multi-party dilemmas

### Analysis
- [ ] Natural language processing for nuance
- [ ] Sentiment analysis beyond keywords
- [ ] Argument structure analysis
- [ ] Rhetorical pattern detection

### Comparison
- [ ] Compare users to known belief systems
- [ ] Cluster analysis (find similar profiles)
- [ ] Compatibility scoring
- [ ] Bridge-building recommendations

## 📖 Example Interaction Sequence

**Q1** (Initial, open-ended):
> "What principles guide your decisions in difficult situations?"

**Analysis**: Extracts values like "honesty", "compassion", "fairness"
**Update**: Initialize value distributions

---

**Q2** (Foundation, scenario):
> "You witness a friend shoplifting. What do you do?"

**Analysis**: Tests Loyalty vs. Authority vs. Fairness
**Update**: Update moral foundation scores with high weight

---

**Q3** (Exploration, scale):
> "Rate 1-10: 'The ends justify the means'"

**Analysis**: Consequentialism vs. Deontology dimension
**Update**: Bayesian update on ethical framework position

---

**Q10** (Refinement, dilemma):
> "You previously valued honesty highly, but indicated loyalty to friends. 
> How do you balance these when they conflict?"

**Analysis**: Resolve contradiction, establish priority hierarchy
**Update**: Refine both dimensions with reduced uncertainty

## 🎓 Academic Foundations

This system builds on:
- **Bayesian Epistemology**: Rational belief updating
- **Moral Foundations Theory**: Haidt's psychological research
- **Information Theory**: Entropy and uncertainty quantification
- **Psychometrics**: Reliable measurement of psychological constructs
- **Active Learning**: Optimal question selection

## 🛠️ Implementation Notes

**In-Memory Storage**: Current implementation uses dictionaries for demo purposes. 
For production:
- Replace with Entity Framework Core + SQL Server
- Add proper authentication
- Implement data export/privacy controls

**AI Parsing**: Currently uses regex and keyword matching to parse AI responses.
For production:
- Use structured JSON output from Ollama
- Implement robust NLP parsing
- Add validation and error handling

**Scalability**: Current architecture supports:
- Concurrent users (stateless services)
- Horizontal scaling (session affinity)
- Background processing (queue-based analysis)

## 📚 Usage

```csharp
// Start discovery
var profile = new UserProfile { Name = "Alice" };
var firstQuestion = await orchestrator.StartDiscoveryAsync(profile);

// Process response
interaction.Response = new UserResponse { RawText = userAnswer };
var (updatedModel, nextQuestion) = await orchestrator.ProcessResponseAndContinueAsync(
    profile, interaction);

// Access current understanding
var confidence = updatedModel.OverallConfidence;
var topValues = updatedModel.Values.OrderByDescending(v => v.ImportanceScore).Take(5);
var moralProfile = updatedModel.MoralFoundations;
```

## 🎯 Success Metrics

The system is working well when:
- ✅ Confidence increases steadily with interactions
- ✅ Entropy decreases over time (more certainty)
- ✅ Consistency score remains high (>0.7)
- ✅ Signal-to-noise ratio > 1.5
- ✅ Users recognize themselves in the profile
- ✅ Contradictions are rare and explainable

---

**Remember**: This is not about judging beliefs, but understanding them with precision, empathy, and statistical rigor.
