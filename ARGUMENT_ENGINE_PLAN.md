# Argument Analysis & Evidence-Based Decision Engine — Architecture Plan

## Vision

Transform CommonUnderstanding from a belief-discovery tool into an **organizational decision-support platform** that provides robust, objective, evidence-based decision making. The system will decompose arguments into their logical structure, map them to relevant evidence, and help organizations converge on shared understanding through transparent reasoning.

---

## Core Concepts

### 1. Argument Decomposition

Every claim or proposal submitted to the system is broken into a formal structure:

| Element | Description | Example |
|---------|-------------|---------|
| **Claim** | The top-level assertion being evaluated | "Remote work increases productivity" |
| **Proposition** | An atomic, truth-evaluable statement | "Employees complete more tasks per hour when working from home" |
| **Premise** | A supporting statement in an argument chain | "Commuting causes fatigue that reduces afternoon output" |
| **Syllogism** | A formal deductive chain (major premise → minor premise → conclusion) | "All fatigue reduces output; commuting causes fatigue; therefore commuting reduces output" |
| **Inference** | The logical step connecting premises to conclusion | Deductive, inductive, abductive, or analogical |
| **Assumption** | An unstated premise the argument depends on | "Output is the correct measure of productivity" |
| **Qualifier** | Strength/scope limitation | "In knowledge-work roles", "For self-directed employees" |
| **Rebuttal** | A condition that defeats the conclusion | "Unless the home environment has more distractions than the office" |

This follows the **Toulmin model** (Claim, Grounds, Warrant, Backing, Qualifier, Rebuttal) extended with formal syllogistic structure.

### 2. Evidence Adjudication

Each proposition is evaluated against data sources ranked by **epistemic weight**:

| Tier | Source Type | Weight | Example |
|------|-----------|---------|---------|
| **T1** | Systematic reviews, meta-analyses | 0.9–1.0 | Cochrane review, Campbell Collaboration |
| **T2** | Peer-reviewed empirical studies (RCTs) | 0.7–0.9 | Published experiment with control group |
| **T3** | Observational/correlational studies | 0.5–0.7 | Survey data, longitudinal cohort study |
| **T4** | Expert consensus, institutional reports | 0.3–0.5 | WHO guidance, professional body position |
| **T5** | Case studies, qualitative evidence | 0.2–0.3 | Single-org case study, interview data |
| **T6** | Anecdote, opinion, unverified claims | 0.0–0.1 | Blog post, personal testimony |

**Adjudication** produces a **Confidence Score** per proposition: a Bayesian posterior combining the evidence tier weights, sample sizes, effect sizes, recency, and replication status.

### 3. Common Understanding

The system maintains an evolving **Common Understanding Model** — a shared knowledge graph for the organization that:

- Tracks which propositions are **settled** (high confidence, broad agreement)
- Surfaces which propositions are **contested** (conflicting evidence or stakeholder disagreement)
- Identifies **unknown** areas (insufficient evidence, no adjudication yet)
- Records the **provenance** of every conclusion (which evidence, which reasoning chain)

---

## Domain Model

```
Argument (top-level container)
├── Claim (the assertion under evaluation)
├── ArgumentStructure
│   ├── Premises[] (each a Proposition)
│   ├── Syllogisms[] (formal deductive chains)
│   ├── InferenceType (deductive | inductive | abductive | analogical)
│   ├── Assumptions[] (implicit premises)
│   ├── Qualifiers[] (scope/strength limits)
│   └── Rebuttals[] (defeaters)
├── EvidenceMap
│   ├── EvidenceItems[] (linked to specific propositions)
│   │   ├── Source (citation, URI, DOI)
│   │   ├── Tier (T1–T6)
│   │   ├── Direction (supports | opposes | neutral)
│   │   ├── EffectSize (if applicable)
│   │   ├── SampleSize
│   │   ├── ReplicationStatus
│   │   └── Recency
│   └── AdjudicationResult
│       ├── PropositionConfidence (0.0–1.0 per proposition)
│       ├── OverallClaimConfidence
│       ├── EvidenceGaps[] (propositions with no/weak evidence)
│       └── ConflictingEvidence[] (propositions with opposing data)
├── StakeholderPositions[]
│   ├── StakeholderId
│   ├── Position (support | oppose | undecided)
│   ├── Reasoning (which premises they accept/reject)
│   └── WeightedConcerns[]
└── DecisionRecommendation
    ├── Recommendation (proceed | defer | reject | investigate)
    ├── ConfidenceLevel
    ├── KeyUncertainties[]
    ├── RiskFactors[]
    └── NextSteps[] (what evidence would change the recommendation)
```

---

## New Services Architecture

Building on the existing service layer, the following services are introduced:

### Layer 1 — Argument Decomposition

#### `ArgumentDecompositionService`
- **Input**: Natural-language argument (text, document, or structured claim)
- **Output**: `ArgumentStructure` with identified propositions, premises, syllogisms
- **Method**: LLM-powered extraction using Semantic Kernel with a structured prompt chain:
  1. **Claim Extraction** — Identify the central claim(s)
  2. **Premise Mining** — Extract stated and implied premises
  3. **Syllogism Construction** — Arrange premises into formal logical chains
  4. **Assumption Surfacing** — Identify unstated premises the argument depends on
  5. **Qualifier/Rebuttal Detection** — Find scope limits and potential defeaters
- **Validation**: Each syllogism is checked for logical validity (valid form) independent of truth

#### `LogicalValidationService`
- **Input**: `Syllogism` or `InferenceChain`
- **Output**: Validity assessment (valid/invalid form, identified fallacies)
- **Method**: Rule-based checker for common syllogistic forms + LLM for informal fallacy detection
- **Fallacy Detection**: Ad hominem, straw man, false dichotomy, appeal to authority, circular reasoning, slippery slope, equivocation, hasty generalization, etc.

### Layer 2 — Evidence Integration

#### `EvidenceRetrievalService`
- **Input**: `Proposition` to evaluate
- **Output**: `EvidenceItem[]` from configured data sources
- **Data Sources** (pluggable adapter pattern):
  - **Internal**: Organization's own data, reports, prior decisions
  - **Academic**: Semantic Scholar API, CrossRef, PubMed (via DOI/abstract retrieval)
  - **Institutional**: Configured feeds from trusted institutions
  - **Web**: Curated RSS/API feeds from vetted sources
  - **Manual**: Human-submitted evidence with provenance metadata
- **Deduplication**: Same study found via multiple sources is merged

#### `EvidenceClassificationService`
- **Input**: Raw `EvidenceItem`
- **Output**: Classified evidence with tier assignment, direction, and quality metrics
- **Method**: LLM-assisted classification validated against metadata heuristics (DOI presence, journal impact, study design keywords)

#### `AdjudicationEngine`
- **Input**: `Proposition` + `EvidenceItem[]`
- **Output**: `AdjudicationResult` with confidence score and reasoning trace
- **Method**: Extends the existing `BayesianInferenceEngine`:
  - Prior: uniform (0.5) or informed by the Common Understanding Model
  - Likelihood: weighted by evidence tier, effect size, sample size, replication
  - Posterior: updated confidence per proposition
  - Propagation: proposition-level confidence flows up through syllogisms to update overall claim confidence
- **Conflict Resolution**: When evidence conflicts, the system flags it and weights by tier rather than averaging

### Layer 3 — Common Understanding

#### `CommonUnderstandingGraph`
- **Purpose**: Persistent knowledge graph of adjudicated propositions for the organization
- **Structure**: Directed graph where nodes are propositions and edges are logical relationships (supports, contradicts, qualifies, assumes)
- **States**: Each proposition node has a status: `settled`, `contested`, `unknown`, `deprecated`
- **Evolution**: Every new argument adjudication updates the graph; changes are versioned
- **Consensus Tracking**: When multiple stakeholders agree on a proposition's status, it advances toward `settled`

#### `DecisionSupportService`
- **Input**: `Argument` + `AdjudicationResult` + `StakeholderPositions[]`
- **Output**: `DecisionRecommendation`
- **Logic**:
  - If overall claim confidence > threshold and no high-risk uncertainties → **Proceed**
  - If confidence is moderate but key uncertainties are resolvable → **Investigate** (with specific evidence requests)
  - If evidence conflicts at high tiers → **Defer** pending resolution
  - If claim confidence is low or critical assumptions are unsupported → **Reject** (with explanation)

### Layer 4 — Stakeholder & Collaboration

#### `StakeholderService`
- **Purpose**: Manage organizational roles, positions, and weighted concerns
- **Features**:
  - Stakeholders can register their position on an argument
  - Each stakeholder can mark which premises they accept/reject with reasoning
  - The system visualizes where agreement and disagreement lie
  - Anonymous mode for sensitive decisions

#### `ArgumentSessionHub` (extends existing SignalR `DiscoveryHub`)
- **Purpose**: Real-time collaborative argument analysis
- **Features**:
  - Live decomposition: stakeholders see the argument being broken down in real time
  - Evidence submission: participants can add evidence during a session
  - Position tracking: live dashboard of stakeholder positions
  - Guided deliberation: system suggests which contested propositions to discuss next

---

## Data Model — New Entities

```csharp
// Core argument structure
public class Argument { Id, Title, Description, SubmittedBy, CreatedAt, Status }
public class Claim { Id, ArgumentId, Text, ClaimType }
public class Proposition { Id, Text, Status, ConfidenceScore, EvidenceCount }
public class Syllogism { Id, MajorPremise (Proposition), MinorPremise (Proposition), Conclusion (Proposition), IsValid, Fallacies[] }
public class Assumption { Id, PropositionId, Text, IsCritical, IsSupported }
public class Qualifier { Id, ClaimId, Text, Scope }
public class Rebuttal { Id, ClaimId, Text, Strength, EvidenceSupport }

// Evidence
public class EvidenceItem { Id, PropositionId, Source, Citation, DOI, Tier, Direction, EffectSize, SampleSize, ReplicationStatus, Recency, AddedBy }
public class AdjudicationResult { Id, PropositionId, Confidence, EvidenceGaps[], ConflictingEvidence[], ReasoningTrace }

// Stakeholder
public class Stakeholder { Id, OrganizationId, Name, Role }
public class StakeholderPosition { Id, StakeholderId, ArgumentId, Position, AcceptedPremises[], RejectedPremises[], Reasoning }

// Common Understanding
public class CommonUnderstandingNode { Id, PropositionId, Status, Confidence, Version, LastUpdated }
public class CommonUnderstandingEdge { SourceId, TargetId, Relationship, Strength }

// Decision
public class DecisionRecommendation { Id, ArgumentId, Recommendation, ConfidenceLevel, KeyUncertainties[], RiskFactors[], NextSteps[] }
```

---

## Integration with Existing System

The current belief-discovery system becomes one **input channel** for the argument engine:

| Existing Capability | New Role |
|---------------------|----------|
| `BayesianInferenceEngine` | Extended to power `AdjudicationEngine` — same Gaussian posterior math, new evidence-tier weighting |
| `ResponseAnalysisEngine` | Reused for extracting reasoning patterns from stakeholder free-text positions |
| `BeliefSystemKnowledgeBase` | Becomes a reference layer — canonical worldviews provide context for understanding stakeholder framing |
| `PsychometricianAgent` | Adapted to generate **clarifying questions** when the argument has ambiguous premises |
| `DiscoveryQuestionEngine` | Powers guided deliberation — asks stakeholders targeted questions about contested propositions |
| `SemanticKernelService` | Shared AI backbone for all LLM-powered services (decomposition, classification, validation) |
| `DiscoveryHub` (SignalR) | Extended into `ArgumentSessionHub` for real-time collaborative analysis |
| Moral Foundations model | Used to detect when disagreements are rooted in different moral foundations rather than factual disputes |

---

## Phased Implementation

### Phase 1 — Argument Decomposition (Foundation)
**Goal**: Accept a natural-language argument and produce a structured decomposition.

- [ ] Define domain models: `Argument`, `Claim`, `Proposition`, `Syllogism`, `Assumption`, `Qualifier`, `Rebuttal`
- [ ] Implement `ArgumentDecompositionService` with Semantic Kernel prompt chain
- [ ] Implement `LogicalValidationService` (syllogism form validation + fallacy detection)
- [ ] Create `ArgumentController` with endpoints: Submit, View, Decompose
- [ ] Build Razor views: argument submission form, structured decomposition view
- [ ] Add persistence (EF Core with SQLite for dev, SQL Server/PostgreSQL for production)

### Phase 2 — Evidence Integration
**Goal**: Link propositions to evidence and produce confidence scores.

- [ ] Define `EvidenceItem`, `AdjudicationResult` models
- [ ] Implement `EvidenceRetrievalService` with pluggable source adapters
- [ ] Implement `EvidenceClassificationService` (tier assignment, quality assessment)
- [ ] Extend `BayesianInferenceEngine` into `AdjudicationEngine` (evidence-weighted posteriors)
- [ ] Build evidence submission UI and evidence-map visualization
- [ ] Implement confidence propagation (proposition → syllogism → claim)

### Phase 3 — Common Understanding Graph
**Goal**: Maintain an evolving organizational knowledge graph of adjudicated propositions.

- [ ] Implement `CommonUnderstandingGraph` (graph storage, versioning, status transitions)
- [ ] Build graph visualization (interactive node-edge view of propositions and relationships)
- [ ] Implement cross-argument linking (same proposition appears in multiple arguments)
- [ ] Add search/browse interface for the common understanding
- [ ] Implement graph evolution tracking (what changed, when, based on what evidence)

### Phase 4 — Stakeholder Collaboration & Decision Support
**Goal**: Multi-stakeholder deliberation with real-time analysis and decision recommendations.

- [ ] Implement `StakeholderService` and `StakeholderPosition` models
- [ ] Extend SignalR hub for real-time argument sessions
- [ ] Implement `DecisionSupportService` (recommendation generation)
- [ ] Build stakeholder dashboard (where do we agree/disagree, and why)
- [ ] Add guided deliberation flow (system-suggested discussion topics)
- [ ] Implement anonymous position submission for sensitive decisions

### Phase 5 — External Data Sources & Scale
**Goal**: Production-grade evidence retrieval and organizational deployment.

- [ ] Integrate Semantic Scholar / CrossRef / PubMed APIs
- [ ] Implement organizational data source adapters (internal reports, prior decisions)
- [ ] Add authentication and organization management (multi-tenant)
- [ ] Migrate to production database (PostgreSQL / SQL Server)
- [ ] Add audit trail (who submitted what, when, decisions made)
- [ ] Performance optimization for large argument graphs

---

## Example Walkthrough

**Scenario**: An organization debates whether to adopt a 4-day work week.

1. **Submission**: A stakeholder submits: *"We should move to a 4-day work week because it improves employee wellbeing, maintains productivity, and reduces operational costs."*

2. **Decomposition**:
   - **Claim**: "The organization should adopt a 4-day work week"
   - **Premise 1** (Proposition): "A 4-day work week improves employee wellbeing"
   - **Premise 2** (Proposition): "A 4-day work week maintains or improves productivity"
   - **Premise 3** (Proposition): "A 4-day work week reduces operational costs"
   - **Assumption**: "Employee wellbeing, productivity, and cost are the relevant decision factors"
   - **Assumption**: "Our work is compatible with a compressed schedule"
   - **Qualifier**: Applies to office/knowledge workers (may not apply to shift-based roles)
   - **Rebuttal**: "Unless client-facing roles require 5-day availability"

3. **Evidence Adjudication**:
   - P1 (wellbeing): T2 evidence from 4-Day Week Global pilot (n=903 employees, significant wellbeing improvement) → **Confidence: 0.82**
   - P2 (productivity): T3 evidence from Iceland trials (n=2,500, productivity maintained) + T5 case studies → **Confidence: 0.68**
   - P3 (cost): T5 evidence (reduced energy costs in case studies, but limited data) → **Confidence: 0.41**
   - **Overall Claim Confidence: 0.63** (moderate, dragged down by weak cost evidence)

4. **Decision Recommendation**: **Investigate** — confidence is moderate; the cost proposition needs stronger evidence. Suggested next step: pilot program to generate internal T3 evidence on productivity and cost.

5. **Common Understanding Update**:
   - "Compressed schedules improve employee wellbeing" → `settled` (high confidence, replicated evidence)
   - "Compressed schedules maintain productivity" → `contested` (moderate evidence, context-dependent)
   - "Compressed schedules reduce costs" → `unknown` (insufficient evidence)

---

## Technical Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| **Persistence** | EF Core + SQLite (dev) / PostgreSQL (prod) | Current in-memory store won't support graph queries or multi-session arguments |
| **Graph Storage** | EF Core with adjacency list + materialized paths | Keeps stack simple; upgrade to dedicated graph DB (Neo4j) if graph queries become bottleneck |
| **LLM Provider** | Continue Semantic Kernel + Ollama | Local-first privacy; can swap to Azure OpenAI for production via existing `RuntimeAiConfigService` |
| **Evidence APIs** | Adapter pattern with `IEvidenceSource` interface | Pluggable; start with manual entry, add APIs incrementally |
| **Real-time** | Extend existing SignalR hub | Already in place; proven pattern |
| **Multi-tenancy** | Organization concept added in Phase 4/5 | Don't over-engineer early; single-org is fine for Phase 1–3 |

---

## Open Questions

1. **Evidence Authority**: Who in the organization can submit/classify evidence? Should there be reviewer roles?
2. **Threshold Calibration**: What confidence thresholds map to proceed/defer/reject? Should they be configurable per organization?
3. **LLM Trust Boundary**: The decomposition and classification are LLM-assisted — should there be mandatory human review checkpoints?
4. **Scope of Data Sources**: Should Phase 2 start with manual evidence only, or immediately integrate one external API?
5. **Existing Belief Discovery**: Should the current discovery flow remain as a separate feature, or fully merge into the argument engine as a "stakeholder worldview profiling" step?
