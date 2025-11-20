# Common Understanding

> *"Most people don't know themselves well enough to describe their beliefs. Let's discover them together."*

An AI-powered belief discovery system that helps people understand their own worldviews and find common ground with others through statistical inference and adaptive conversation.

---

## 🚀 Quick Start

**New to the project?** Start here:

1. **[Quick Start Guide](CommonUnderstanding/QUICKSTART.md)** - Get up and running in 10 minutes
2. **[Ollama Checklist](OLLAMA_CHECKLIST.md)** - Verify your Ollama setup before running
3. **[Full Documentation](CommonUnderstanding/README.md)** - Comprehensive project overview

**Already familiar?** Jump to:
- [Ollama Setup Guide](OLLAMA_SETUP.md) - Detailed installation and troubleshooting
- [Self-Hosting Guide](SELF-HOSTING-GUIDE.md) - Deploy on your own hardware
- [Azure Deployment](AZURE_DEPLOYMENT.md) - Deploy to the cloud

---

## 📋 What is This?

Common Understanding is an **interactive belief discovery system** that:

1. **Engages you in thoughtful dialogue** using AI-powered adaptive questions
2. **Analyzes your responses statistically** using Bayesian inference
3. **Builds an evolving model** of your belief system with confidence tracking
4. **Finds common ground** by comparing inferred beliefs with others

### Why It Exists

Most people struggle to articulate their worldview. Instead of asking "what do you believe?", this system **discovers** your beliefs through conversation, then maps them to reveal unexpected areas of agreement with people who seem to disagree.

---

## 🎯 Key Features

- ✨ **AI-Powered Discovery** - Adaptive questioning that evolves with your responses
- 📊 **Statistical Rigor** - Bayesian inference with confidence intervals
- ❤️ **Moral Foundations** - Based on Jonathan Haidt's psychological research
- 🔒 **Privacy-First** - All AI processing happens locally via Ollama
- 📈 **Evolution Tracking** - Versioned snapshots show how understanding deepens
- 🤝 **Comparison Tools** - Find common ground between different worldviews

---

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    YOUR BROWSER                         │
│                                                          │
│  ┌────────────────────────────────────────────────┐   │
│  │         Interactive Discovery Interface         │   │
│  │  • Answer thoughtful questions                  │   │
│  │  • View evolving belief profile                 │   │
│  │  • Track confidence & evolution                 │   │
│  └─────────────────┬──────────────────────────────┘   │
└────────────────────┼────────────────────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────────────────────┐
│              ASP.NET Core 9.0 MVC App                   │
│                                                          │
│  ┌──────────────────┐  ┌──────────────────┐           │
│  │ Discovery Engine │  │ Bayesian Inference│           │
│  │ • Adaptive Q's   │  │ • Statistical     │           │
│  │ • Response       │  │   modeling        │           │
│  │   analysis       │  │ • Confidence      │           │
│  └────────┬─────────┘  └──────────────────┘           │
│           │                                             │
│           ▼                                             │
│  ┌─────────────────────────────────────────────────┐  │
│  │     Microsoft Semantic Kernel                    │  │
│  │     • AI orchestration                           │  │
│  │     • Prompt management                          │  │
│  └───────────────────┬─────────────────────────────┘  │
└────────────────────┼─────────────────────────────────┘
                     │
                     ▼
          ┌──────────────────┐
          │      Ollama      │
          │ localhost:11434  │
          │                  │
          │ • llama3.2:3b    │
          │ • Local LLM      │
          │ • Privacy-first  │
          └──────────────────┘
```

**Key Principle**: Every deployment (local, self-hosted, Azure) runs its own Ollama instance. No remote AI APIs, no data sharing.

---

## 📚 Documentation

### Getting Started
- **[QUICKSTART.md](CommonUnderstanding/QUICKSTART.md)** - 10-minute quick start guide
- **[OLLAMA_CHECKLIST.md](OLLAMA_CHECKLIST.md)** - Pre-flight verification checklist
- **[OLLAMA_SETUP.md](OLLAMA_SETUP.md)** - Comprehensive Ollama installation guide

### Core Documentation
- **[README.md](CommonUnderstanding/README.md)** - Full project documentation and technical details
- **[DISCOVERY_SYSTEM.md](CommonUnderstanding/DISCOVERY_SYSTEM.md)** - Deep dive into the belief discovery system
- **[PROJECT_SUMMARY.md](PROJECT_SUMMARY.md)** - High-level project overview

### Deployment
- **[SELF-HOSTING-GUIDE.md](SELF-HOSTING-GUIDE.md)** - Deploy on your own hardware (Windows/Linux/Raspberry Pi)
- **[AZURE_DEPLOYMENT.md](AZURE_DEPLOYMENT.md)** - Deploy to Microsoft Azure (includes VM setup for Ollama)
- **[DEPLOYMENT_SUMMARY.md](DEPLOYMENT_SUMMARY.md)** - Deployment status and notes

### Architecture & Design
- **[ASYNC-ARCHITECTURE.md](ASYNC-ARCHITECTURE.md)** - Asynchronous processing design
- **[BATCH-PROCESSING.md](BATCH-PROCESSING.md)** - Batch processing patterns

---

## 🛠️ Technology Stack

- **ASP.NET Core 9.0** - Web framework
- **C# 13** - Programming language
- **Microsoft Semantic Kernel** - AI orchestration
- **Ollama** - Local LLM runtime
- **Bootstrap 5** - UI framework
- **SignalR** - Real-time communication
- **Bayesian Statistics** - Belief modeling

**AI Models Supported:**
- llama3.2:1b, llama3.2:3b (recommended)
- llama3.1:8b (high quality)
- qwen2.5:7b (excellent reasoning)
- phi3:3.8b (fast alternative)

---

## 💻 System Requirements

### Minimum
- .NET 9.0 SDK
- 4 GB RAM available
- 5 GB disk space (for models)
- Windows 10+, macOS 12+, or Linux

### Recommended
- 8 GB+ RAM available
- 10 GB disk space
- SSD for better performance

---

## 🚦 Quick Verification

Before running the app, verify your setup:

```bash
# 1. Check Ollama is running
curl http://localhost:11434  # Should return: "Ollama is running"

# Windows PowerShell:
Invoke-WebRequest http://localhost:11434

# 2. Check you have a model
ollama list

# 3. Run the app
cd CommonUnderstanding
dotnet run

# 4. Open browser
# https://localhost:7187 or http://localhost:5220
```

**Problems?** See [OLLAMA_CHECKLIST.md](OLLAMA_CHECKLIST.md)

---

## 🎓 How It Works

### 1. Discovery Phase
The system asks you questions - not generic surveys, but thoughtful scenarios:
- Moral dilemmas: "A close friend asks you to lie for them..."
- Value rankings: "Order these by importance: Justice, Mercy, Truth..."
- Scale questions: "Rate 1-10: Individual freedom vs. collective good"

### 2. Analysis Phase
Each response is analyzed using AI to extract:
- **Belief signals** - What values are reflected in your answer?
- **Moral foundations** - Care, Fairness, Loyalty, Authority, Sanctity, Liberty
- **Emotional markers** - Certainty, intensity, internal conflict
- **Reasoning patterns** - Consequentialist, deontological, virtue ethics

### 3. Bayesian Update
Your belief model is updated statistically:
- **Priors** are updated with new evidence
- **Confidence scores** increase with consistent responses
- **Uncertainty** decreases as more data accumulates
- **Contradictions** are detected and explored

### 4. Evolving Understanding
After 10-20 questions, you'll see:
- Your core values ranked by confidence × importance
- Moral foundations scores with confidence intervals
- Belief dimensions mapped in multi-dimensional space
- Statistical metrics (entropy, consistency, signal-to-noise)

### 5. Comparison & Common Ground
Compare your discovered beliefs with:
- Other users' profiles
- Established belief systems (religions, philosophies, ideologies)
- Historical figures and movements

The system identifies:
- **Overlaps** - Where you agree
- **Complementary aspects** - Different but compatible views
- **Non-zero-sum opportunities** - Both perspectives add value
- **True divergences** - Honest differences

---

## 🌟 Philosophy

### Core Insight
Most people cannot accurately articulate their own belief systems. They haven't done the introspective work, lack the vocabulary, or hold contradictory beliefs without realizing it.

### The Solution
Don't ask. **Discover** through thoughtful conversation, rigorous analysis, and statistical inference.

### The Goal
Build bridges of understanding between different worldviews by finding the common humanity beneath surface disagreements.

### The Method
Mathematical rigor + Psychological insight + AI assistance + Epistemic humility

---

## 🤝 Contributing

This is an open demonstration project. Contributions welcome!

**Areas for contribution:**
- Database persistence layer
- Enhanced AI response parsing
- UI/UX improvements
- Additional question types
- Visualization features
- Testing and documentation

---

## 📄 License

This project is provided as-is for educational and demonstration purposes.

---

## 🙏 Acknowledgments

Built on the shoulders of giants:
- **Jonathan Haidt** - Moral Foundations Theory
- **Microsoft** - Semantic Kernel framework
- **Ollama Team** - Local LLM runtime
- **Shalom Schwartz** - Universal Values Theory
- **Thomas Bayes** - Probability theory

---

## 📧 Questions?

- Check the [documentation](#-documentation) section above
- Review the [troubleshooting guide](OLLAMA_SETUP.md#health-check--troubleshooting)
- Open an issue on GitHub

---

**🌍 Finding what binds us together through the mathematics of belief.**

---

## 📂 Project Structure

```
CommonUnderstanding/
├── CommonUnderstanding/          # Main application
│   ├── Controllers/              # MVC controllers
│   ├── Services/                 # Core business logic
│   │   ├── BeliefDiscoveryOrchestrator.cs
│   │   ├── BayesianInferenceEngine.cs
│   │   ├── DiscoveryQuestionEngine.cs
│   │   └── SemanticKernelService.cs
│   ├── Models/                   # Data models
│   ├── Views/                    # Razor views
│   ├── wwwroot/                  # Static files
│   ├── README.md                 # Full documentation
│   ├── QUICKSTART.md             # Quick start guide
│   └── DISCOVERY_SYSTEM.md       # Technical deep dive
│
├── OLLAMA_SETUP.md               # Comprehensive Ollama guide
├── OLLAMA_CHECKLIST.md           # Quick verification checklist
├── SELF-HOSTING-GUIDE.md         # Self-hosting instructions
├── AZURE_DEPLOYMENT.md           # Azure deployment guide
├── PROJECT_SUMMARY.md            # High-level overview
└── README.md                     # This file
```

---

**Ready to discover your beliefs? Let's begin! 🚀**

```bash
cd CommonUnderstanding
dotnet run
```

Then visit: `https://localhost:7187`
