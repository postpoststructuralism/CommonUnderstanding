# A Common Understanding - Project Summary

## Overview
**A Common Understanding** is an ASP.NET Core MVC application that uses AI to help people discover and map their belief systems through interactive conversation, with the ultimate goal of identifying common ground between individuals with different worldviews.

## The Core Problem
Most people struggle to articulate their own beliefs accurately. Rather than asking users to self-report their values, this system uses an AI-driven discovery process to *infer* belief systems through adaptive questioning and statistical analysis.

## Technical Architecture

### Stack
- **ASP.NET Core 9.0 MVC** - Web framework
- **Microsoft Semantic Kernel** - AI orchestration
- **Ollama (qwen2.5:3b)** - Local LLM for privacy-preserving AI analysis
- **Bayesian Inference Engine** - Statistical modeling of beliefs
- **Moral Foundations Theory** - Psychological framework (Jonathan Haidt)

### Core Components

**Discovery System** (Interactive AI Interview)
- Adaptive question generation based on knowledge gaps
- Response analysis using natural language understanding
- Emotional content detection
- Moral foundations scoring

**Bayesian Inference Engine** (Statistical Rigor)
- Gaussian distributions for belief dimensions
- Precision-weighted updates as evidence accumulates
- Confidence calculation and uncertainty tracking
- Contradiction detection across response history

**Belief Mapping** (The Original Goal)
- Multi-dimensional belief profiles
- Versioned snapshots showing evolution of understanding
- Comparative analysis to identify overlap and divergence
- Non-zero-sum opportunity identification

## How It Works

1. **User onboarding** - Simple name entry, no registration
2. **Adaptive questioning** - AI generates contextual questions (moral dilemmas, scale questions, value rankings)
3. **Response analysis** - AI extracts belief signals, emotional markers, moral foundations
4. **Bayesian updating** - Statistical model updates confidence scores and dimensional beliefs
5. **Progressive discovery** - System identifies gaps and generates increasingly targeted questions
6. **Profile visualization** - Rich view of inferred beliefs with confidence intervals

## Development Philosophy Connection
This aligns with your "consciousness-first" agentic framework concept - the system doesn't assume knowledge of the user's beliefs but rather *discovers* them through iterative dialogue, similar to how Ghost in the Machine agents would discover project requirements through role-based questioning.

## Current Status
- ✅ Full statistical engine with Bayesian inference
- ✅ AI-powered adaptive questioning system
- ✅ Interactive web UI with progress tracking
- ✅ Local LLM integration (privacy-preserving)
- ✅ Multi-stage discovery flow (Initial → Foundation → Exploration → Refinement)
- 🔄 Ready for belief comparison features
- 📋 Future: Database persistence, multi-user comparison views

## Value Proposition
Transforms vague ideological differences into quantifiable, mapped belief systems that reveal unexpected areas of agreement - enabling more productive dialogue in polarized environments.

---

**Time Investment**: One day/week micro-mission format
**Open Source**: Yes (repository: CommonUnderstanding)
**Privacy**: Fully local AI processing, no external API calls
