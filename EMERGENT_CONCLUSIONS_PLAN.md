# Emergent Conclusions Engine — Work Plan

## Purpose

The community has built a rich inventory of decomposed arguments, adjudicated propositions, tiered evidence, stakeholder positions, and head-to-head comparisons. What's missing is a **synthesis layer** — one that traverses this accumulated knowledge to surface patterns no single argument or comparison reveals on its own.

The Emergent Conclusions Engine will identify:

- **Blindspots** — critical gaps, unstated dependencies, and unexamined assumptions hiding in plain sight across the collective body of arguments
- **Harmonies** — surprising convergences, complementary positions, and non-zero-sum opportunities that are invisible when viewing arguments in isolation

This is not a feature that generates new arguments. It is an **analytical lens** placed over everything the community has already contributed — a meta-reasoner that reads the graph the way no single participant can.

---

## Conceptual Framework

### What counts as an "emergent" conclusion?

An emergent conclusion is a pattern that:
1. **Cannot be seen from any single argument** — it only appears when traversing multiple arguments, stakeholder positions, or graph relationships
2. **Has evidentiary grounding** — it's rooted in the propositions, evidence, and positions already in the system (not speculation)
3. **Is actionable** — it either reveals something the community should investigate (blindspot) or leverage (harmony)

### The five categories of emergence

| Category | Type | What it reveals |
|----------|------|-----------------|
| **Assumption Cascades** | Blindspot | Multiple arguments silently depend on the same untested assumption — if it's wrong, a whole cluster of conclusions collapses |
| **Evidence Deserts** | Blindspot | Propositions that anchor decisions but have no evidence, or only T5/T6 evidence — the community is flying blind |
| **Silent Contradictions** | Blindspot | Two settled propositions actually contradict each other, but no one has filed a comparison or challenged either — latent logical debt |
| **Convergent Ground** | Harmony | Opposing stakeholders who reject each other's overall arguments actually accept specific shared premises — unstated common ground |
| **Complementary Inference Chains** | Harmony | Arguments on different topics share syllogistic structure or feed each other's conclusions — the community's reasoning is more coherent than it appears |

---

## Architecture

### New Components

```
Services/
  EmergentConclusionsEngine.cs      — Core analysis engine (orchestrator)
  BlindspotDetector.cs              — Graph traversal for blindspot patterns
  HarmonyDetector.cs                — Graph traversal for harmony patterns

Models/
  EmergentConclusionModels.cs       — DTOs for emergent findings

Controllers/
  EmergentConclusionsController.cs  — API + MVC endpoints

Views/
  EmergentConclusions/
    Index.cshtml                    — Dashboard with blindspots & harmonies
    _BlindspotCard.cshtml           — Partial for blindspot rendering
    _HarmonyCard.cshtml             — Partial for harmony rendering
```

### Integration Points

The engine reads from (never writes to) existing data:

| Source | What it provides |
|--------|-----------------|
| `ApplicationDbContext.Propositions` | All atomic premises with confidence, status, evidence count |
| `ApplicationDbContext.Assumptions` | Critical/supported flags per argument |
| `ApplicationDbContext.CommonUnderstandingNodes` | Deduplicated proposition graph with merged confidence |
| `ApplicationDbContext.CommonUnderstandingEdges` | Logical relationships (supports/contradicts/qualifies/assumes) |
| `ApplicationDbContext.StakeholderPositions` | Who accepts/rejects which premises, with reasoning |
| `ApplicationDbContext.ArgumentComparisons` | Conflicting/complementary premise pairs, synthesis narratives |
| `ApplicationDbContext.AdjudicationSummaries` | Evidence gaps, conflicting evidence, recommendation, narrative |
| `ApplicationDbContext.EvidenceItems` | Tier, direction, replication status per proposition |
| `ApplicationDbContext.Syllogisms` | Formal inference chains across arguments |
| `ApplicationDbContext.Rebuttals` | Unaddressed counter-arguments |

---

## Data Model

```csharp
public enum EmergentType
{
    Blindspot,
    Harmony
}

public enum EmergentCategory
{
    // Blindspots
    AssumptionCascade,       // Shared untested assumptions across arguments
    EvidenceDesert,          // High-stakes propositions with no/weak evidence
    SilentContradiction,     // Settled propositions that contradict each other
    UnaddressedRebuttal,     // Strong rebuttals that no argument has answered
    ConfidenceIllusion,      // High confidence built on low-tier evidence only

    // Harmonies
    ConvergentGround,        // Opposing stakeholders sharing accepted premises
    ComplementaryChains,     // Arguments whose conclusions reinforce each other
    EmergentConsensus,       // Propositions trending from Contested → Settled
    CrossDomainReinforcement,// Unrelated arguments providing mutual evidence
    SharedValueCore          // Different arguments revealing same underlying values
}

public class EmergentConclusion
{
    public int Id { get; set; }
    public EmergentType Type { get; set; }
    public EmergentCategory Category { get; set; }
    public string Title { get; set; }           // Human-readable finding title
    public string Description { get; set; }     // Detailed explanation
    public double Significance { get; set; }    // 0.0–1.0 (how impactful is this)
    public double Confidence { get; set; }      // 0.0–1.0 (how certain are we)

    // Provenance — which entities ground this conclusion
    public List<int> InvolvedArgumentIds { get; set; }
    public List<int> InvolvedPropositionIds { get; set; }
    public List<int> InvolvedNodeIds { get; set; }
    public List<int> InvolvedStakeholderIds { get; set; }

    // For blindspots: what action could resolve it
    public string SuggestedAction { get; set; }
    // For harmonies: what opportunity does it create
    public string OpportunityDescription { get; set; }

    public DateTime DetectedAt { get; set; }
}

public class EmergentConclusionsReport
{
    public List<EmergentConclusion> Blindspots { get; set; }
    public List<EmergentConclusion> Harmonies { get; set; }
    public GraphHealthSummary GraphHealth { get; set; }
    public DateTime GeneratedAt { get; set; }
}

public class GraphHealthSummary
{
    public int TotalArguments { get; set; }
    public int TotalPropositions { get; set; }
    public int TotalEvidenceItems { get; set; }
    public int TotalStakeholders { get; set; }
    public double AverageConfidence { get; set; }
    public int SettledCount { get; set; }
    public int ContestedCount { get; set; }
    public int UnevaluatedCount { get; set; }
    public double EvidenceCoverage { get; set; }  // % of propositions with ≥1 evidence
    public int CriticalAssumptionsUntested { get; set; }
}
```

---

## Detection Algorithms

### Phase 1 — Blindspot Detection (`BlindspotDetector.cs`)

#### 1.1 Assumption Cascade Detection

**Goal:** Find critical assumptions that appear (by normalized text similarity) across multiple arguments but have never been evidenced or tested.

```
Algorithm:
1. Load all Assumptions where IsCritical = true AND IsSupported = false
2. Group by NormalizedText (lowercase, trim, collapse whitespace)
3. For each group with Count ≥ 2:
   - Significance = Count / TotalArguments (more arguments affected → higher stakes)
   - Identify all downstream Syllogisms that depend on this assumption
   - Flag as AssumptionCascade with involved argument IDs
4. Additionally, use Semantic Kernel to cluster semantically similar
   (but not textually identical) assumptions → surface "hidden shared assumptions"
```

**Significance formula:**
$$S_{\text{cascade}} = \min\left(1.0,\ \frac{n_{\text{arguments}}}{N_{\text{total}}} \times \left(1 + \sum_{i} w_{\text{downstream}_i}\right)\right)$$

where $w_{\text{downstream}_i}$ is the confidence of conclusions that depend on the assumption.

#### 1.2 Evidence Desert Detection

**Goal:** Find propositions that anchor high-confidence conclusions but lack sufficient evidence.

```
Algorithm:
1. Load all CommonUnderstandingNodes
2. For each node where EvidenceCount = 0 OR all evidence is T5/T6:
   - Check how many arguments reference this node (via ArgumentIdsJson)
   - Check if any Syllogism uses it as a major/minor premise
   - Significance = ArgumentReferenceCount × ConfidenceGap
     where ConfidenceGap = ProvisionalConfidence − EvidenceBasedConfidence
3. Sort by significance — top items are propositions where the community
   is most over-confident given evidence quality
```

#### 1.3 Silent Contradiction Detection

**Goal:** Find pairs of settled/high-confidence propositions that logically contradict each other but have no existing "contradicts" edge or comparison.

```
Algorithm:
1. Load all CommonUnderstandingNodes with Status = Settled OR Confidence ≥ 0.7
2. Load existing contradiction edges (Relationship = "contradicts")
3. For all pairs NOT already linked by a contradiction edge:
   - Use Semantic Kernel to evaluate: "Do these two propositions contradict?"
   - If yes with high confidence → flag as SilentContradiction
4. Batch in groups for LLM efficiency (compare N propositions at once)
5. Significance = avg(Confidence_A, Confidence_B) — higher confidence pairs
   are more dangerous contradictions
```

#### 1.4 Unaddressed Rebuttal Detection

**Goal:** Find strong rebuttals that no subsequent argument, evidence, or stakeholder has engaged with.

```
Algorithm:
1. Load all Rebuttals with Strength = "high"
2. For each rebuttal, check if any:
   - Proposition in the graph addresses it (semantic match)
   - Subsequent argument references a similar claim
   - Evidence item addresses the rebuttal's concern
3. Unaddressed rebuttals with high-confidence parent claims → highest significance
```

#### 1.5 Confidence Illusion Detection

**Goal:** Find propositions with high confidence scores built entirely on low-tier evidence.

```
Algorithm:
1. Load Propositions where Confidence ≥ 0.7
2. For each, load associated EvidenceItems
3. If MAX(evidence tier) ≤ T4_ExpertConsensus (no empirical studies):
   - Flag as ConfidenceIllusion
   - Significance = Confidence × (1 - MaxTierWeight)
   - Higher confidence + lower evidence quality = bigger illusion
```

---

### Phase 2 — Harmony Detection (`HarmonyDetector.cs`)

#### 2.1 Convergent Ground Detection

**Goal:** Find premises accepted by stakeholders who oppose each other's overall arguments.

```
Algorithm:
1. Load all StakeholderPositions
2. Group by ArgumentId → identify opposing pairs (one Supports, one Opposes)
3. For each opposing pair:
   - Parse AcceptedPremiseIdsJson for both stakeholders
   - Intersection of accepted premises = Convergent Ground
4. Significance = |shared premises| / |total premises| for the argument
5. Use LLM to generate a synthesis narrative for the shared ground
```

**This is the most actionable harmony** — it tells facilitators exactly where to start a productive dialogue.

#### 2.2 Complementary Inference Chain Detection

**Goal:** Find arguments whose conclusions serve as premises for other arguments, creating a reinforcing web.

```
Algorithm:
1. Load all Syllogisms across all arguments
2. For each Syllogism.Conclusion, search CommonUnderstandingNodes
   for a matching node that also appears as a premise in another argument
3. Build a directed graph of argument-to-argument reinforcement:
   Arg_A.conclusion → Arg_B.premise
4. Detect cycles (mutual reinforcement) and long chains (dependency depth)
5. Significance for reinforcement = min(Confidence_conclusion, Confidence_premise)
6. Significance for cycles = product of confidences around the cycle
```

#### 2.3 Emergent Consensus Detection

**Goal:** Find propositions that have moved from Contested → higher confidence over time, suggesting the community is naturally converging.

```
Algorithm:
1. Load CommonUnderstandingNodes with Version > 1 (has been updated)
2. Compare current Confidence with historical pattern:
   - If confidence trending upward AND multiple arguments contributed → emerging consensus
   - If stakeholder acceptance is growing (more AcceptedPremiseIds across positions) → strengthening
3. Significance = confidence_delta × argument_count
```

#### 2.4 Cross-Domain Reinforcement Detection

**Goal:** Find arguments on different topics that provide mutual evidentiary support.

```
Algorithm:
1. Load all ArgumentComparisons
2. For each comparison with ComplementaryPremises.Count > 0 AND
   NetDirection = Balanced:
   - These arguments aren't competing — they're reinforcing
3. Additionally, scan for arguments NOT yet compared that share
   high-confidence CommonUnderstandingNodes:
   - Suggest new comparisons that would reveal synergies
4. Significance = count of shared high-confidence nodes
```

#### 2.5 Shared Value Core Detection

**Goal:** Across all arguments and stakeholder positions, find the underlying values that the community implicitly agrees on even while disagreeing on specifics.

```
Algorithm:
1. Gather all stakeholder reasoning texts + adjudication narratives
2. Use Semantic Kernel to extract implied values from reasoning:
   - "fairness", "efficiency", "safety", "autonomy", etc.
3. Count value frequency across all stakeholders and arguments
4. Values mentioned by ≥ 60% of stakeholders → Shared Value Core
5. Generate a narrative: "Despite disagreement on X, Y, Z,
   the community consistently appeals to [shared values]"
```

---

## Implementation Phases

### Phase 1: Foundation (Graph-Only Analysis)
**Scope:** Blindspots and harmonies detectable purely from graph structure and database queries — no LLM calls required.

| Task | Description | Depends On |
|------|-------------|------------|
| 1.1 | Create `EmergentConclusionModels.cs` with all DTOs | — |
| 1.2 | Create `BlindspotDetector.cs` — Assumption Cascade (1.1) and Evidence Desert (1.2) detection | 1.1, existing DB |
| 1.3 | Create `HarmonyDetector.cs` — Convergent Ground (2.1) and Emergent Consensus (2.3) detection | 1.1, existing DB |
| 1.4 | Create `EmergentConclusionsEngine.cs` — orchestrator that calls both detectors and builds the report | 1.2, 1.3 |
| 1.5 | Create `EmergentConclusionsController.cs` with `GenerateReport` action | 1.4 |
| 1.6 | Create `Views/EmergentConclusions/Index.cshtml` — dashboard with blindspot/harmony cards | 1.5 |
| 1.7 | Add `GraphHealthSummary` calculation to the engine | 1.4 |
| 1.8 | Add navigation link to existing layout | 1.6 |
| 1.9 | Register services in DI (`Program.cs`) | 1.4 |

**Deliverable:** A working dashboard that identifies assumption cascades, evidence deserts, convergent ground, and emerging consensus from existing data.

### Phase 2: LLM-Enhanced Detection
**Scope:** Detections that require semantic understanding — contradiction detection, value extraction, narrative generation.

| Task | Description | Depends On |
|------|-------------|------------|
| 2.1 | Add Silent Contradiction detection to `BlindspotDetector` — uses Semantic Kernel to compare proposition pairs | Phase 1, SemanticKernelService |
| 2.2 | Add Confidence Illusion detection to `BlindspotDetector` | Phase 1 |
| 2.3 | Add Unaddressed Rebuttal detection to `BlindspotDetector` — semantic search for rebuttal responses | Phase 1, SemanticKernelService |
| 2.4 | Add Complementary Chain detection to `HarmonyDetector` — cross-argument syllogism linking | Phase 1 |
| 2.5 | Add Cross-Domain Reinforcement detection to `HarmonyDetector` | Phase 1 |
| 2.6 | Add Shared Value Core extraction to `HarmonyDetector` — LLM value extraction from stakeholder reasoning | Phase 1, SemanticKernelService |
| 2.7 | Add LLM narrative generation to `EmergentConclusionsEngine` — produce a readable executive summary of all findings | 2.1–2.6 |
| 2.8 | Add significance ranking and filtering to the report (top-N by significance, filter by category) | 2.7 |

**Deliverable:** A full-featured emergent conclusions engine with semantic analysis, narrative summaries, and ranked findings.

### Phase 3: Visualization & Interaction
**Scope:** Rich UI for exploring emergent conclusions and acting on them.

| Task | Description | Depends On |
|------|-------------|------------|
| 3.1 | Graph visualization showing blindspot clusters (highlight nodes involved in cascades, deserts, contradictions) | Phase 2, existing graph view |
| 3.2 | Harmony map — visual overlay showing convergent ground and reinforcement chains | Phase 2 |
| 3.3 | "Investigate" action buttons — click a blindspot to create a new argument, add evidence, or request stakeholder input | Phase 2 |
| 3.4 | "Leverage" action buttons — click a harmony to generate a dialogue guide or synthesis proposal | Phase 2 |
| 3.5 | SignalR integration — real-time updates when new arguments change the emergent landscape | Phase 2, DiscoveryHub |
| 3.6 | Historical tracking — store past reports and show how blindspots/harmonies evolve over time | Phase 2 |
| 3.7 | Export — generate a PDF/markdown report of emergent conclusions for sharing outside the platform | Phase 2 |

**Deliverable:** An interactive, visual, actionable emergent conclusions system integrated into the existing collaboration workflow.

---

## Key Design Decisions

### Why read-only?
The engine never modifies arguments, propositions, or graph nodes. It only reads and computes transient reports. This means:
- No risk of corrupting existing data
- Reports can be regenerated at any time
- Multiple analyses can run concurrently

### Why significance scoring?
A naive scan will produce hundreds of findings. Significance scoring ensures the community sees the **highest-impact blindspots and most promising harmonies first**. The formula weighs:
- How many arguments are affected
- How high the confidence stakes are
- How many stakeholders are involved

### Why separate Blindspot/Harmony detectors?
- Different traversal patterns (blindspots scan for absence/gaps; harmonies scan for overlap/reinforcement)
- Different data access patterns (blindspots focus on assumptions, evidence, contradictions; harmonies focus on stakeholder positions, comparisons, shared nodes)
- Can be extended independently as new detection patterns emerge

### Why LLM in Phase 2 only?
Phase 1 delivers immediate value from pure graph/database analysis. Phase 2 adds semantic depth but introduces LLM latency and cost. This staged approach means the feature is useful before LLM integration is complete.

---

## Success Criteria

| Criterion | Measure |
|-----------|---------|
| Blindspot detection accuracy | ≥ 80% of flagged blindspots are confirmed relevant by human review |
| Harmony detection accuracy | ≥ 70% of flagged harmonies represent genuine common ground |
| Report generation time (Phase 1) | < 2 seconds for a graph of 100 arguments |
| Report generation time (Phase 2) | < 30 seconds including LLM calls |
| User engagement | Users act on ≥ 30% of flagged items (investigate blindspot or leverage harmony) |
| New evidence submitted | Flagged evidence deserts trigger ≥ 1 new evidence submission within 7 days |

---

## Estimated Effort

| Phase | Tasks | Notes |
|-------|-------|-------|
| Phase 1: Foundation | 9 tasks | Pure C# + EF Core queries, Razor views |
| Phase 2: LLM-Enhanced | 8 tasks | Semantic Kernel integration, prompt engineering |
| Phase 3: Visualization | 7 tasks | JavaScript graph rendering, SignalR, export |
