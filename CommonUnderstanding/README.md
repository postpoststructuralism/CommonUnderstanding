# Common Understanding

> *"Most people don't know themselves well enough to describe their beliefs. Let's discover them together."*

An ASP.NET MVC application powered by Semantic Kernel and Ollama that **infers belief systems through adaptive conversation**, using Bayesian statistics and AI to build mental models of individuals, then maps these onto one another to find what binds us together.

## 🎯 What Makes This Different

Instead of asking people to describe their worldview (which they struggle with), Common Understanding:

1. **Engages in thoughtful dialogue** - Moral dilemmas, scenarios, value rankings
2. **Analyzes responses statistically** - Bayesian inference, not just pattern matching  
3. **Builds evolving models** - Versioned snapshots with confidence intervals
4. **Discovers common ground** - Compares inferred belief systems to find overlap

## ✨ Core Features

### 1. **Interactive Belief Discovery** 🧠
- Adaptive questioning powered by AI
- Moral dilemmas and emotional scenarios
- Statistical analysis of each response
- Real-time confidence tracking

### 2. **Rigorous Statistical Analysis** 📊
- Bayesian inference engine
- Confidence intervals on all estimates
- Entropy, consistency, and signal-to-noise metrics
- Contradiction detection

### 3. **Moral Foundations Profiling** ❤️
- Based on Jonathan Haidt's research
- 6 dimensions: Care, Fairness, Loyalty, Authority, Sanctity, Liberty
- Scored 0-10 with standard errors

### 4. **Belief System Comparison** 🤝
- Compare discovered profiles
- Identify overlaps and divergences
- Find non-zero-sum opportunities
- Generate dialogue suggestions

### 5. **Evolution Tracking** 📈
- Versioned belief snapshots over time
- Historical analysis
- Change detection
- Belief stability metrics

## 🚀 Quick Start

### Prerequisites

1. **.NET 9.0 SDK** - [Download here](https://dotnet.microsoft.com/download/dotnet/9.0)
2. **Ollama** - [Download here](https://ollama.ai/)
3. A compatible Ollama model (e.g., llama3.2, llama3.1, mistral)

### Setup in 3 Steps

**1. Install and start Ollama**
```bash
# Download a model
ollama pull llama3.2

# Start Ollama (keep this running)
ollama serve
```

**2. Run the application**
```bash
cd CommonUnderstanding
dotnet run
```

**3. Open your browser**
```
https://localhost:5001
```

## 🎮 Your First Discovery Session

### Step 1: Begin Your Journey
Click "Begin Discovery" and enter your name. The system will introduce itself and explain the process.

### Step 2: Answer Thoughtfully
You'll receive various types of questions:
- **Open-ended**: "What principles guide your decisions?"
- **Moral dilemmas**: "A friend asks you to lie for them..."
- **Scale questions**: "Rate 1-10: Individual freedom vs. collective good"
- **Value rankings**: "Order these by importance: Justice, Mercy, Truth..."

### Step 3: Watch Your Profile Build
After 5-10 questions, view your profile to see:
- **Core values** (ranked by confidence × importance)
- **Moral foundations** (Care, Fairness, Loyalty, Authority, Sanctity, Liberty)
- **Belief dimensions** with confidence intervals
- **Statistical metrics** (entropy, consistency, signal-to-noise)

### Step 4: Explore & Compare
- Continue answering to refine your profile
- View how your beliefs have evolved over time
- Compare with other discovered profiles
- Find common ground and differences

## 📚 Two Modes of Operation

### Mode 1: Discovery (New! ⭐)
**For individuals** - Infer your own belief system

```
Start → Adaptive Questions → Statistical Analysis → Evolving Profile
```

- Answer 5-60+ questions
- AI analyzes each response
- Bayesian updates to your mental model
- Track confidence and evolution

### Mode 2: Comparison (Original)
**For known belief systems** - Compare established worldviews

```
Create Belief Systems → Compare → Find Common Ground
```

- Manually describe belief systems (Buddhism, Stoicism, etc.)
- AI analyzes and compares them
- Discover overlaps and non-zero-sum opportunities

## 🏗️ Technical Architecture

### The Discovery Pipeline

```
User Response → Response Analysis → Bayesian Inference → Model Update → Next Question
     ↓               ↓                     ↓                   ↓              ↓
  Raw Text    Extract Signals      Update Priors        New Snapshot    Adaptive
               AI Analysis         Confidence ↑         Versioned       Target Gaps
               Emotions            Uncertainty ↓        Timestamped     Find Contradictions
```

### Key Components

**1. Discovery Question Engine**
- Generates adaptive questions based on current model state
- Question types: Open-ended, moral dilemmas, scales, rankings, scenarios
- Targets uncertain areas and contradictions
- Stage-aware (Initial → Foundation → Exploration → Refinement)

**2. Response Analysis Engine**
- AI-powered extraction of belief signals
- Moral foundations scoring (Haidt's framework)
- Emotional marker detection (intensity, certainty, conflict)
- Reasoning pattern identification (consequentialist, deontological, etc.)

**3. Bayesian Inference Engine** ⭐ **Statistical Core**
- Gaussian priors and posteriors
- Precision-weighted Bayesian updates
- Confidence = f(sample_size, variance)
- Entropy, consistency, signal-to-noise calculations

**4. Belief Discovery Orchestrator**
- Coordinates the entire flow
- Manages discovery stages
- Selects question strategies adaptively
- Tracks evolution over time

### Data Models

**UserProfile**
```csharp
{
  Name: "Alice",
  Stage: DiscoveryStage.Exploration,
  InteractionCount: 15,
  CurrentBeliefSnapshot: { ... },
  HistoricalSnapshots: [ ... ],
  Interactions: [ ... ]
}
```

**BeliefSnapshot** (Versioned Mental Model)
```csharp
{
  Timestamp: "2025-11-10T14:30:00Z",
  OverallConfidence: 0.73,
  Dimensions: [
    { Name: "Care/Harm", Position: 0.8, Confidence: 0.85, Uncertainty: 0.15 }
  ],
  Values: [
    { Name: "Compassion", ImportanceScore: 8.5, Confidence: 0.78 }
  ],
  MoralFoundations: { Care: 8.2, Fairness: 7.1, ... },
  Statistics: { Entropy: 1.2, Consistency: 0.82, ... }
}
```

### Statistical Rigor

**Bayesian Update Formula**
```
Posterior = (PriorPrecision × Prior + Evidence × Likelihood) / TotalPrecision
Confidence = (1 - e^(-n/10)) × e^(-variance)
```

**Quality Metrics**
- **Entropy**: Information content / uncertainty
- **Consistency**: Temporal stability of responses  
- **Signal-to-Noise**: Confidence / Uncertainty ratio
- **Contradiction Detection**: Opposing high-confidence beliefs

## 🎓 Theoretical Foundations

This system builds on established research:

### Psychology
- **Moral Foundations Theory** (Jonathan Haidt) - 6 moral dimensions
- **Value Theory** (Schwartz) - Universal human values
- **Personality Psychology** - Big Five correlations with beliefs

### Statistics & Math
- **Bayesian Epistemology** - Rational belief updating under uncertainty
- **Information Theory** - Shannon entropy, mutual information
- **Psychometrics** - Reliable measurement of psychological constructs

### AI & Machine Learning
- **Active Learning** - Optimal question selection to reduce uncertainty
- **Probabilistic Models** - Gaussian processes for belief distributions
- **Natural Language Processing** - Semantic analysis of responses

## 💡 Example Discovery Session

### Question 1 (Initial, Open-ended)
**System**: "What principles guide your decisions in difficult situations?"

**User**: "I try to be honest and fair, but also consider people's feelings..."

**Analysis**:
- Values detected: Honesty (0.7 confidence), Fairness (0.7), Compassion (0.6)
- Moral foundations: Care ↑, Fairness ↑
- Reasoning: Mixed deontological/virtue ethics

**Model Update**: Initial snapshot created, confidence: 0.15

---

### Question 5 (Foundation, Moral Dilemma)
**System**: "A close friend asks you to lie to protect them from consequences of their mistake..."

**User**: "I'd try to find a way to help without lying directly. Maybe help them face it..."

**Analysis**:
- Conflict resolution: Honesty + Loyalty tension
- Moral foundations: Care (8.0), Fairness (7.5), Loyalty (6.0)
- Reasoning: Consequentialist with deontological constraints

**Model Update**: Confidence: 0.42, identified honesty > loyalty hierarchy

---

### Question 15 (Exploration, Scale)
**System**: "Rate 1-10: Individual freedom vs. Collective good"

**User**: [Rating: 6] "Both matter, but freedom enables people to contribute better..."

**Analysis**:
- Political dimension: Moderate-libertarian (0.4 position)
- Liberty foundation ↑
- Instrumental reasoning detected

**Model Update**: Confidence: 0.68, political dimension added

---

### Question 30 (Refinement)
**System**: "Earlier you valued honesty highly. In this scenario [describes complex situation], your response suggested flexibility. Help us understand..."

**Analysis**:
- Context-sensitivity detected
- Honesty is principle, not absolute rule
- Refines understanding of value application

**Model Update**: Confidence: 0.81, reduced uncertainty in ethical framework

## 🔬 Statistical Insights

### How Confidence Grows

```
Interactions  Confidence  Entropy  Dimensions
    0           0.10       2.5         0
    5           0.35       2.1         8
   10           0.55       1.7        15
   20           0.72       1.3        22
   50           0.87       0.9        35
  100           0.93       0.6        45
```

### Quality Indicators

**Good Model** ✅
- Confidence > 0.7
- Consistency > 0.75
- Signal-to-Noise > 1.5
- Few contradictions
- User recognizes themselves

**Needs More Data** ⚠️
- Confidence < 0.5
- High entropy (> 2.0)
- Many uncertain areas
- Low signal-to-noise (< 1.0)

## Current Limitations

- **In-Memory Storage**: Data is stored in memory and will be lost when the app restarts. For production, implement database persistence.
- **Basic Parsing**: AI responses are currently stored as raw text. Future versions should parse structured data.
- **Single User**: No authentication or multi-user support yet.
- **No Export**: Results cannot be exported or shared externally yet.

## Future Enhancements

- [ ] Database persistence (Entity Framework Core)
- [ ] Structured data extraction from AI responses
- [ ] User authentication and personal libraries
- [ ] Export comparisons as PDF or markdown
- [ ] Visual mapping of belief systems
- [ ] Community sharing and collaboration features
- [ ] Multi-way comparisons (more than 2 systems)
- [ ] Historical tracking of how beliefs evolve

## Philosophy

This application is built on the premise that:
- Most conflicts arise from misunderstanding rather than fundamental incompatibility
- Finding common ground doesn't require abandoning differences
- Non-zero-sum solutions often exist when we look beyond binary thinking
- What binds us together is often stronger than what divides us
- Respectful dialogue is the path to mutual understanding

## Contributing

This is a demonstration project. Feel free to fork and extend it with:
- Better data models and persistence
- More sophisticated AI prompts and agents
- Visualization features
- Additional analysis dimensions
- Export and sharing capabilities

## License

This project is provided as-is for educational and demonstration purposes.

---

**Remember**: The goal is not to prove one belief system "right" or "wrong," but to build bridges of understanding and identify opportunities for collaboration and mutual flourishing.
