# The Understanding Graph — Architecture & Theoretical Foundations

> **Phase 3 Design Document**
> *Mapping the topology of human understanding through the totality of arguments, their dialectical relationships, and the latent schemas that emerge.*

---

## Table of Contents

1. [Vision & Core Thesis](#1-vision--core-thesis)
2. [Theoretical Foundations](#2-theoretical-foundations)
3. [What Exists Today](#3-what-exists-today)
4. [The Understanding Graph — Core Architecture](#4-the-understanding-graph--core-architecture)
5. [Tensor Embedding Layer](#5-tensor-embedding-layer)
6. [Dialectical Provenance & Argument Topology](#6-dialectical-provenance--argument-topology)
7. [Schema Emergence & Clustering](#7-schema-emergence--clustering)
8. [Data Model](#8-data-model)
9. [Service Architecture](#9-service-architecture)
10. [Query & Navigation](#10-query--navigation)
11. [Visualization](#11-visualization)
12. [Implementation Roadmap](#12-implementation-roadmap)
13. [Open Research Questions](#13-open-research-questions)

---

## 1. Vision & Core Thesis

### The Problem

The system today has a rich inventory of:
- **Belief profiles** — individual users' inferred worldviews (Bayesian dimensions, moral foundations, values)
- **Decomposed arguments** — formal logical structures (claims, premises, syllogisms, assumptions, rebuttals)
- **Adjudicated propositions** — atomic truth-claims with evidence tiers and confidence scores
- **Common Understanding Graph** — a directed graph of deduplicated proposition nodes with `supports`/`contradicts`/`qualifies`/`assumes` edges
- **Emergent conclusions** — cross-argument blindspots and harmonies detected by graph traversal
- **Convergence maps** — pairwise user comparisons across profile, argument, and graph layers
- **Social arguments** — user-authored reasoning units with embeddings, chains, worldviews, and debate rooms

**What's missing** is a unified, high-dimensional representation of *understanding itself* — a map that doesn't just track individual propositions or pairwise comparisons, but reveals the **latent conceptual schemas** that organize all of these artifacts into a coherent topology of human thought.

### The Thesis

> **The totality of arguments, their dialectical relationships, and the AI-generated semantic summaries of both, form a high-dimensional manifold. The latent structure of this manifold *is* the map of human understanding.**

Concretely:

1. Every argument, proposition, belief profile, and worldview can be represented as a **vector embedding** in a shared semantic space.
2. The **dialectical relationships** between them (supports, contradicts, refines, extends, assumes, rebuts) define a **directed graph** whose edges carry typed, weighted relationships.
3. By combining the **embedding space** (semantic proximity) with the **graph topology** (dialectical structure), we can apply **tensor decomposition** and **spectral clustering** to discover:
   - **Conceptual schemas** — clusters of semantically related propositions that form coherent belief frameworks
   - **Dialectical attractors** — propositions or argument clusters that draw disproportionate engagement (support or rebuttal)
   - **Epistemic fault lines** — boundaries where high-confidence propositions on both sides contradict each other, revealing fundamental worldview cleavages
   - **Schema evolution** — how conceptual clusters grow, merge, split, or decay over time as new arguments are added

---

## 2. Theoretical Foundations

### 2.1 Conceptual Spaces Theory (Gärdenfors, 2000)

Peter Gärdenfors proposed that concepts are not defined by necessary-and-sufficient conditions (classical view) nor by prototypes alone, but by **regions in a high-dimensional quality space**. Each dimension corresponds to a "quality" (e.g., moral weight, empirical verifiability, emotional valence).

**Application**: Our belief dimensions (political, religious, ethical, metaphysical) and Schwartz value vectors already define a quality space. The Understanding Graph extends this by treating *propositions* as points in this space, where proximity indicates conceptual similarity.

### 2.2 Formal Concept Analysis (Wille, 1982)

FCA discovers **concept lattices** from a matrix of objects × attributes. A concept is a maximal set of objects sharing a maximal set of attributes.

**Application**: Arguments × Propositions forms a natural object-attribute matrix. FCA can discover formal concepts — clusters of arguments that share the same premises — revealing the *implicit conceptual structure* of the debate space.

### 2.3 Topological Data Analysis (Carlsson, 2009)

TDA uses persistent homology to find **topological features** (connected components, loops, voids) in point-cloud data that persist across scales.

**Application**: The embedding manifold of propositions may contain **loops** (circular reasoning patterns that span multiple arguments) and **voids** (conceptual gaps where no argument has been made — a formalization of blindspots).

### 2.4 Tensor Decomposition / PARAFAC (Harshman, 1970)

A tensor is a multi-dimensional array. A 3rd-order tensor of shape `(Arguments × Propositions × Dimensions)` can be decomposed into latent factors that reveal how arguments, propositions, and conceptual dimensions co-vary.

**Application**: Rather than flattening the argument-proposition-dimension space into a matrix, we keep it as a **3-way tensor** and apply CANDECOMP/PARAFAC (CP) or Tucker decomposition to extract:
- **Argument factors** — latent argument archetypes
- **Proposition factors** — latent conceptual primitives
- **Dimension factors** — latent axes of understanding

### 2.5 Spectral Graph Theory (Chung, 1997)

The eigenvectors of a graph's Laplacian matrix encode its **harmonic structure**. The second eigenvector (Fiedler vector) gives the optimal partition of the graph into two clusters.

**Application**: The Common Understanding Graph's Laplacian reveals the natural **epistemic communities** — clusters of propositions that form coherent, internally-consistent belief systems. Spectral clustering is more principled than k-means on embeddings because it respects graph topology.

### 2.6 Dialectical Logic & Hegelian Triads

Hegel's dialectic (thesis → antithesis → synthesis) describes how understanding evolves through contradiction and resolution.

**Application**: The `contradicts` edges in the graph define **dialectical pairs**. When a thesis-proposition and antithesis-proposition are both high-confidence, the system should look for (or generate) a **synthesis-proposition** that resolves the contradiction at a higher level of abstraction. This is the engine of *understanding evolution*.

---

## 3. What Exists Today

### 3.1 Existing Graph Infrastructure

| Component | What it provides | Limitation |
|-----------|-----------------|------------|
| `CommonUnderstandingNode` | Deduplicated proposition nodes with normalized text, status, confidence, evidence count | Single text field; no embedding storage; no multi-dimensional representation |
| `CommonUnderstandingEdge` | Directed edges: `supports`, `contradicts`, `qualifies`, `assumes` | Only 4 relationship types; no weight decay; no temporal dimension |
| `CommonUnderstandingService` | Sync from argument, query, search, status grouping | No graph algorithms (traversal, clustering, centrality) |
| `SocialProposition.Embedding` | pgvector float4[] embedding (1536d) | Only on social propositions, not on analytical propositions or graph nodes |
| `SocialArgument.Embedding` | pgvector float4[] embedding (1536d) | Only on social arguments |
| `ArgumentChain.Embedding` | Centroid embedding of chain | Only on chains |
| `Worldview.SchwartzVector` | 10-dimensional Schwartz value vector | Only on worldviews; not integrated with graph |
| `BeliefSnapshot.Dimensions` | Multi-dimensional belief positions with confidence | Per-user; not integrated with proposition graph |

### 3.2 Existing Analytical Pipeline

```
User Belief Discovery → Bayesian Inference → BeliefSnapshot (per-user)
         ↓
Argument Submission → Decomposition → Propositions + Syllogisms + Assumptions
         ↓
Evidence Adjudication → AdjudicationSummary → Confidence Scores
         ↓
CommonUnderstandingService → Graph Sync → Nodes + Edges
         ↓
ComparativeAnalysis → ArgumentComparison → Conflicting/Complementary Premises
         ↓
EmergentConclusionsEngine → Blindspots + Harmonies + GraphHealth
         ↓
ConvergenceMapService → Pairwise Convergence Maps
```

### 3.3 What's Missing for the Understanding Graph

1. **Embedding storage on graph nodes** — `CommonUnderstandingNode` has no embedding column
2. **Multi-dimensional node representation** — nodes are single-text, not multi-vector
3. **Tensor construction** — no mechanism to build the Arguments × Propositions × Dimensions tensor
4. **Graph algorithms** — no spectral clustering, centrality, community detection, or persistent homology
5. **Schema discovery** — no clustering or concept lattice extraction
6. **Dialectical synthesis** — no mechanism to detect thesis-antithesis pairs and propose syntheses
7. **Temporal evolution** — no versioned snapshots of the graph's topological structure
8. **Query layer** — no way to ask "what is the conceptual schema around proposition X?" or "which arguments bridge these two clusters?"

---

## 4. The Understanding Graph — Core Architecture

### 4.1 Layered Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                     LAYER 4: QUERY & NAVIGATION                  │
│  Schema Explorer  |  Dialectical Navigator  |  Evolution Viewer │
├─────────────────────────────────────────────────────────────────┤
│                     LAYER 3: SCHEMA EMERGENCE                    │
│  Spectral Clustering  |  Tensor Decomposition  |  FCA Lattice   │
│  TDA (Persistent Homology)  |  Dialectical Synthesis Engine     │
├─────────────────────────────────────────────────────────────────┤
│                     LAYER 2: TENSOR & GRAPH                      │
│  Tensor Construction  |  Graph Algorithms  |  Centrality Scores  │
│  Community Detection  |  Path Analysis  |  Attractor Detection  │
├─────────────────────────────────────────────────────────────────┤
│                     LAYER 1: EMBEDDING & PROVENANCE              │
│  Multi-vector Embedding  |  Dialectical Provenance  |  Temporal │
│  Semantic Summaries  |  Cross-entity Linking                    │
├─────────────────────────────────────────────────────────────────┤
│                     EXISTING DATA LAYER                          │
│  Arguments | Propositions | Graph Nodes/Edges | Belief Profiles │
│  Social Arguments | Worldviews | Debate Rooms | Convergence Maps│
└─────────────────────────────────────────────────────────────────┘
```

### 4.2 Key Design Principles

1. **Multi-vector representation**: Every node in the Understanding Graph has *multiple* vector representations — semantic (text embedding), dimensional (belief coordinates), and structural (graph embedding via node2vec/GraphSAGE).

2. **Dialectical provenance**: Every edge carries not just a relationship type, but the *argument context* that produced it — which argument, which claim, which syllogism, which stakeholder position.

3. **Temporal versioning**: The graph is snapshot-able. Schema evolution is tracked across time, so we can ask "how did this conceptual cluster change after argument X was added?"

4. **Schema emergence is bottom-up**: Schemas are not predefined categories. They emerge from the data through clustering, tensor decomposition, and topological analysis. The system discovers the conceptual structure; it does not impose one.

5. **Everything is a tensor**: The core mathematical object is a 3rd-order tensor `T[a, p, d]` where `a` indexes arguments, `p` indexes propositions, and `d` indexes conceptual dimensions. Tensor decomposition reveals latent structure.

---

## 5. Tensor Embedding Layer

### 5.1 Multi-Vector Representation

Every `UnderstandingNode` (the enhanced successor to `CommonUnderstandingNode`) carries:

| Vector | Dimension | Source | Purpose |
|--------|-----------|--------|---------|
| **Semantic Embedding** | 1536 | LLM text embedding of canonical proposition text | Semantic similarity search |
| **Dimensional Vector** | Variable (10–50) | Aggregated belief dimensions from all users who hold this proposition | Position in belief space |
| **Schwartz Vector** | 10 | Schwartz value profile aggregated from arguments referencing this node | Value-space position |
| **Moral Foundations Vector** | 6 | Haidt foundation scores aggregated from arguments | Moral-space position |
| **Graph Embedding** | 128 | node2vec or GraphSAGE on the graph topology | Structural role in the graph |
| **Temporal Vector** | 8 | Time-series features: creation rate, update frequency, confidence trajectory | Evolution pattern |

### 5.2 Tensor Construction

The core tensor `T` is constructed as follows:

```
T ∈ ℝ^(A × P × D)

Where:
  A = number of Arguments (both analytical and social)
  P = number of Propositions (deduplicated UnderstandingNodes)
  D = number of Dimensions (belief dimensions + Schwartz + moral foundations)

T[a, p, d] = {
    +1 if argument a asserts proposition p along dimension d,
    -1 if argument a negates proposition p along dimension d,
     0 if argument a does not reference proposition p along dimension d,
    ±confidence_score if the proposition has been adjudicated
}
```

This is a **sparse tensor** — most entries are 0. But its latent structure is dense.

### 5.3 Tensor Decomposition Strategy

**Phase 1 — CP Decomposition (PARAFAC)**

Factorize `T` into rank-`R` components:

```
T ≈ Σᵣ λᵣ · aᵣ ∘ pᵣ ∘ dᵣ

Where:
  aᵣ ∈ ℝ^A  — argument factor (which arguments load onto latent factor r)
  pᵣ ∈ ℝ^P  — proposition factor (which propositions load onto latent factor r)
  dᵣ ∈ ℝ^D  — dimension factor (which dimensions load onto latent factor r)
  λᵣ        — strength of latent factor r
```

The latent factors `aᵣ`, `pᵣ`, `dᵣ` are the **emergent conceptual schemas**. Each factor `r` represents a latent "topic" or "conceptual primitive" that:
- Manifests in a specific subset of arguments (`aᵣ`)
- Expresses through a specific subset of propositions (`pᵣ`)
- Loads on a specific subset of conceptual dimensions (`dᵣ`)

**Phase 2 — Tucker Decomposition**

For richer interactions:

```
T ≈ G ×₁ A ×₂ P ×₃ D

Where G ∈ ℝ^(R₁×R₂×R₃) is the core tensor encoding interactions
between argument factors, proposition factors, and dimension factors.
```

Tucker decomposition captures **multi-way interactions** that CP cannot — e.g., a proposition that means different things in different argument contexts.

### 5.4 Implementation Approach

Since full tensor decomposition on large sparse tensors is computationally expensive, we use a **streaming/incremental approach**:

1. **Initial construction**: Build the tensor from all existing data at deployment time. Use `scikit-tensor` or `TensorLy` (Python) or a C# sparse tensor library with ALS (Alternating Least Squares) for CP decomposition.

2. **Incremental updates**: When new arguments or propositions are added, update the decomposition using **online CP decomposition** (O-CP) or **incremental SVD** on the mode-1 (argument) unfolding.

3. **Approximation for real-time**: For interactive querying, maintain a **low-rank approximation** (rank R = 50–200) that can be queried in O(R) time per node.

4. **Batch recomputation**: Full decomposition runs nightly or weekly, depending on data volume.

---

## 6. Dialectical Provenance & Argument Topology

### 6.1 Enhanced Edge Types

The current 4 edge types (`supports`, `contradicts`, `qualifies`, `assumes`) are extended to a richer ontology:

| Edge Type | Symmetric? | Description |
|-----------|-----------|-------------|
| `supports` | No | Source proposition provides evidence/reasoning for target |
| `contradicts` | Yes | Source and target cannot both be true |
| `qualifies` | No | Source limits the scope or strength of target |
| `assumes` | No | Source depends on target as an unstated premise |
| `refines` | No | Source is a more precise version of target |
| `extends` | No | Source builds on target to reach a further conclusion |
| `rebuts` | No | Source is a counter-argument to target |
| `entails` | No | Source logically implies target (deductive) |
| `exemplifies` | No | Source is a concrete instance of target (abstract) |
| `analogous` | Yes | Source and target share structural similarity |
| `synthesizes` | No | Source resolves a contradiction between two other nodes |

### 6.2 Edge Weighting

Each edge carries a **composite weight** `w ∈ [0, 1]`:

```
w = α · w_semantic + β · w_structural + γ · w_epistemic

Where:
  w_semantic  = cosine similarity of node embeddings
  w_structural = graph-based weight (e.g., Jaccard similarity of neighborhoods)
  w_epistemic  = min(confidence_source, confidence_target)
  α, β, γ     = tunable hyperparameters (default: 0.4, 0.3, 0.3)
```

Edge weights **decay over time** unless reinforced by new arguments:

```
w(t) = w₀ · exp(-λ · Δt)

Where λ is a decay constant (default: 0.01 per day)
```

### 6.3 Dialectical Provenance

Every edge stores its **provenance** — the chain of reasoning that produced it:

```json
{
  "edgeId": "...",
  "sourceNodeId": 42,
  "targetNodeId": 87,
  "relationship": "contradicts",
  "weight": 0.82,
  "provenance": {
    "sourceArgumentIds": [15, 23],
    "sourceClaimIds": [7, 12],
    "sourceSyllogismIds": [3],
    "sourceStakeholderIds": [5, 8],
    "adjudicationId": 19,
    "detectedBy": "comparative_analysis | emergent_engine | manual | llm",
    "detectedAt": "2026-06-27T12:00:00Z",
    "reinforcementCount": 3,
    "lastReinforcedAt": "2026-06-27T12:00:00Z"
  }
}
```

### 6.4 Argument Topology Metrics

For each node in the Understanding Graph, we compute:

| Metric | Formula | Meaning |
|--------|---------|---------|
| **Degree Centrality** | `deg(v) / (N-1)` | How many other propositions this one connects to |
| **Betweenness Centrality** | Σ σ(s,t\|v) / σ(s,t) | How often this proposition lies on paths between others (bridge concept) |
| **Eigenvector Centrality** | Leading eigenvector of adjacency matrix | How "influential" this proposition is (connected to other well-connected nodes) |
| **PageRank** | Standard PageRank on directed graph | Authority of the proposition in the dialectical network |
| **Clustering Coefficient** | 2·E(v) / (deg(v)·(deg(v)-1)) | How tightly clustered the proposition's neighborhood is |
| **Controversy Score** | min(support_weight, oppose_weight) / max(support_weight, oppose_weight) | How evenly split the evidence is (0 = unanimous, 1 = evenly split) |
| **Dialectical Temperature** | Σ contradict_weights / Σ total_weights | Proportion of a node's edges that are contradictions — how "hot" this concept is |
| **Schema Entropy** | -Σ p(c) · log p(c) | How many different conceptual schemas this node participates in |

---

## 7. Schema Emergence & Clustering

### 7.1 Spectral Clustering on the Graph Laplacian

The **graph Laplacian** `L = D - A` (where `D` is the degree matrix and `A` is the weighted adjacency matrix) encodes the graph's harmonic structure.

**Algorithm**:
1. Compute the normalized Laplacian `L_norm = I - D^(-1/2) · A · D^(-1/2)`
2. Compute the first `k` eigenvectors of `L_norm`
3. Treat each node's eigenvector components as coordinates in `ℝ^k`
4. Apply k-means on these coordinates to get `k` clusters

**The clusters are conceptual schemas** — groups of propositions that are more densely connected internally than to the rest of the graph. The number of clusters `k` is determined by the **eigengap heuristic** (largest gap between consecutive eigenvalues).

### 7.2 Tensor Decomposition for Latent Schemas

The CP decomposition of tensor `T` yields `R` latent factors. Each factor `r` defines a **soft cluster**:

- Arguments load onto factor `r` with weight `aᵣ[i]`
- Propositions load onto factor `r` with weight `pᵣ[j]`
- Dimensions load onto factor `r` with weight `dᵣ[k]`

A **latent schema** is the triple `(aᵣ, pᵣ, dᵣ)` — it tells us:
- *Which arguments* participate in this schema
- *Which propositions* define it
- *Which dimensions* it operates on

**Schema labeling**: The top-loading propositions and dimensions for each factor are fed to an LLM to generate a human-readable label:

> "Factor 3 loads on propositions about economic redistribution (p₁₂, p₁₅, p₂₁), dimensions of fairness and authority (d₃, d₇), and appears in arguments about tax policy and social welfare. Label: **'Distributive Justice Schema'**"

### 7.3 Formal Concept Analysis (FCA) Lattice

The **Arguments × Propositions** incidence matrix `M[a, p] = 1` if argument `a` references proposition `p` defines a formal context.

**Algorithm**:
1. Compute all formal concepts (maximal rectangles of 1s in permuted M)
2. Build the concept lattice (partial order by subset inclusion)
3. Extract the **concept lattice diagram** — a Hasse diagram showing how concepts generalize and specialize

**Application**: The concept lattice reveals the **hierarchical structure of understanding** — broad, abstract concepts at the top (shared by many arguments) and narrow, specific concepts at the bottom (unique to few arguments).

### 7.4 Topological Data Analysis (TDA)

**Persistent homology** on the embedding point cloud:

1. Build a Vietoris-Rips complex at increasing distance scales ε
2. Track when topological features (connected components, loops, voids) appear and disappear
3. Features that persist across a wide range of ε are **significant topological structures**

**What to look for**:
- **Loops (H₁)**: Circular argument patterns — e.g., A supports B, B supports C, C supports A. These indicate **self-referential reasoning chains** or **closed ideological systems**.
- **Voids (H₂)**: Regions of the embedding space with no propositions — these are **conceptual blindspots** (formalized).
- **Connected components (H₀)**: At small ε, these are individual propositions. As ε grows, they merge into schemas. The merging pattern reveals the **hierarchical taxonomy of concepts**.

### 7.5 Dialectical Synthesis Engine

For each pair of propositions `(P₁, P₂)` connected by a `contradicts` edge where both have confidence > 0.7:

1. **Check if a synthesis already exists**: Search for a proposition `P₃` that has `synthesizes` edges to both `P₁` and `P₂`
2. **If not, generate one**: Use an LLM with a prompt that presents both propositions, their evidence, and asks for a higher-level framing that resolves the contradiction
3. **Add the synthesis** as a new node with `synthesizes` edges to both parents
4. **Track synthesis chains**: A synthesis may itself be contradicted by another proposition, leading to a higher-order synthesis — creating a **dialectical hierarchy**

This is the engine of **understanding evolution** — the graph grows not just by adding new arguments, but by resolving contradictions at progressively higher levels of abstraction.

---

## 8. Data Model

### 8.1 New Entities

```csharp
/// <summary>
/// Enhanced graph node — successor to CommonUnderstandingNode.
/// Carries multi-vector representation and schema membership.
/// </summary>
public class UnderstandingNode
{
    public int Id { get; set; }

    // ── Core text ──────────────────────────────────────────────────────────
    public string CanonicalText { get; set; } = string.Empty;
    public string NormalizedKey { get; set; } = string.Empty;

    // ── Status & confidence ────────────────────────────────────────────────
    public PropositionStatus Status { get; set; } = PropositionStatus.Unevaluated;
    public double Confidence { get; set; } = 0.5;
    public int EvidenceCount { get; set; }

    // ── Multi-vector embeddings ────────────────────────────────────────────
    /// <summary>1536-dim semantic embedding (pgvector).</summary>
    public float[]? SemanticEmbedding { get; set; }

    /// <summary>128-dim graph embedding (node2vec or GraphSAGE).</summary>
    public float[]? GraphEmbedding { get; set; }

    /// <summary>10-dim Schwartz value vector.</summary>
    public double[]? SchwartzVector { get; set; }

    /// <summary>6-dim Moral Foundations vector.</summary>
    public double[]? MoralFoundationsVector { get; set; }

    /// <summary>JSON: variable-length dimensional coordinates.</summary>
    public string? DimensionalCoordinatesJson { get; set; }

    // ── Graph topology metrics ─────────────────────────────────────────────
    public double DegreeCentrality { get; set; }
    public double BetweennessCentrality { get; set; }
    public double EigenvectorCentrality { get; set; }
    public double PageRank { get; set; }
    public double ClusteringCoefficient { get; set; }
    public double ControversyScore { get; set; }
    public double DialecticalTemperature { get; set; }
    public double SchemaEntropy { get; set; }

    // ── Provenance ─────────────────────────────────────────────────────────
    /// <summary>JSON array of argument IDs that reference this node.</summary>
    public string ArgumentIdsJson { get; set; } = "[]";

    /// <summary>JSON array of user IDs whose belief profiles include this node.</summary>
    public string UserIdsJson { get; set; } = "[]";

    /// <summary>JSON array of schema IDs this node belongs to.</summary>
    public string SchemaIdsJson { get; set; } = "[]";

    // ── Temporal ───────────────────────────────────────────────────────────
    public int Version { get; set; } = 1;
    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<UnderstandingEdge> OutboundEdges { get; set; } = new List<UnderstandingEdge>();
    public ICollection<UnderstandingEdge> InboundEdges { get; set; } = new List<UnderstandingEdge>();
    public ICollection<SchemaMembership> SchemaMemberships { get; set; } = new List<SchemaMembership>();
}

/// <summary>
/// Enhanced graph edge with dialectical provenance and temporal decay.
/// </summary>
public class UnderstandingEdge
{
    public int Id { get; set; }

    public int SourceNodeId { get; set; }
    public int TargetNodeId { get; set; }

    /// <summary>Relationship type from the extended ontology.</summary>
    public string Relationship { get; set; } = string.Empty;  // supports, contradicts, qualifies, assumes, refines, extends, rebuts, entails, exemplifies, analogous, synthesizes

    /// <summary>Composite weight [0.0–1.0] with temporal decay.</summary>
    public double Weight { get; set; } = 0.5;

    /// <summary>Base weight before decay (for reinforcement).</summary>
    public double BaseWeight { get; set; } = 0.5;

    /// <summary>JSON: dialectical provenance record.</summary>
    public string ProvenanceJson { get; set; } = "{}";

    /// <summary>Number of times this edge has been independently detected/reinforced.</summary>
    public int ReinforcementCount { get; set; } = 1;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastReinforcedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public UnderstandingNode? SourceNode { get; set; }
    public UnderstandingNode? TargetNode { get; set; }
}

/// <summary>
/// An emergent conceptual schema — a cluster of nodes that form a coherent
/// belief framework, discovered through spectral clustering or tensor decomposition.
/// </summary>
public class ConceptualSchema
{
    public int Id { get; set; }

    /// <summary>LLM-generated human-readable label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Detailed description of what this schema represents.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>How this schema was discovered: spectral_clustering | tensor_decomposition | fca_lattice | tda | manual</summary>
    public string DiscoveryMethod { get; set; } = string.Empty;

    /// <summary>Coherence score [0.0–1.0]: how tightly clustered the member nodes are.</summary>
    public double Coherence { get; set; }

    /// <summary>Stability score [0.0–1.0]: how consistent this schema is across multiple runs.</summary>
    public double Stability { get; set; }

    /// <summary>For tensor decomposition: the rank-r factor index.</summary>
    public int? FactorIndex { get; set; }

    /// <summary>JSON: top-loading dimension names and weights for this schema.</summary>
    public string? DimensionLoadingsJson { get; set; }

    /// <summary>JSON: top-loading argument IDs and weights for this schema.</summary>
    public string? ArgumentLoadingsJson { get; set; }

    /// <summary>JSON: top-loading proposition IDs and weights for this schema.</summary>
    public string? PropositionLoadingsJson { get; set; }

    public DateTime DiscoveredAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<SchemaMembership> Memberships { get; set; } = new List<SchemaMembership>();
}

/// <summary>Join table: UnderstandingNode ↔ ConceptualSchema (many-to-many with membership weight).</summary>
public class SchemaMembership
{
    public int NodeId { get; set; }
    public int SchemaId { get; set; }

    /// <summary>Membership weight [0.0–1.0]: how strongly this node belongs to this schema.</summary>
    public double Weight { get; set; }

    // Navigation
    public UnderstandingNode Node { get; set; } = null!;
    public ConceptualSchema Schema { get; set; } = null!;
}

/// <summary>
/// A dialectical synthesis — a proposition that resolves a contradiction
/// between two or more parent propositions at a higher level of abstraction.
/// </summary>
public class DialecticalSynthesis
{
    public int Id { get; set; }

    /// <summary>The synthesized proposition node ID.</summary>
    public int SynthesisNodeId { get; set; }

    /// <summary>JSON array of parent node IDs that are resolved by this synthesis.</summary>
    public string ParentNodeIdsJson { get; set; } = "[]";

    /// <summary>JSON array of contradiction edge IDs that this synthesis addresses.</summary>
    public string ResolvedContradictionIdsJson { get; set; } = "[]";

    /// <summary>Depth in the dialectical hierarchy (0 = base proposition, 1 = first-order synthesis, etc.).</summary>
    public int Depth { get; set; } = 0;

    /// <summary>LLM-generated explanation of how this synthesis resolves the contradiction.</summary>
    public string ResolutionNarrative { get; set; } = string.Empty;

    /// <summary>Whether this synthesis was accepted by the community (voted or confirmed).</summary>
    public bool IsAccepted { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public UnderstandingNode? SynthesisNode { get; set; }
}

/// <summary>
/// A point-in-time snapshot of the Understanding Graph's topological structure.
/// Enables tracking schema evolution over time.
/// </summary>
public class GraphSnapshot
{
    public int Id { get; set; }

    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Number of nodes at snapshot time.</summary>
    public int NodeCount { get; set; }

    /// <summary>Number of edges at snapshot time.</summary>
    public int EdgeCount { get; set; }

    /// <summary>Number of schemas detected.</summary>
    public int SchemaCount { get; set; }

    /// <summary>JSON: full schema inventory at snapshot time.</summary>
    public string? SchemaInventoryJson { get; set; }

    /// <summary>JSON: graph-level metrics (average clustering coefficient, diameter, etc.).</summary>
    public string? GraphMetricsJson { get; set; }

    /// <summary>JSON: tensor decomposition factors at snapshot time.</summary>
    public string? TensorFactorsJson { get; set; }

    /// <summary>JSON: TDA persistence diagram.</summary>
    public string? PersistenceDiagramJson { get; set; }
}
```

### 8.2 DbContext Additions

```csharp
// In ApplicationDbContext:
public DbSet<UnderstandingNode> UnderstandingNodes => Set<UnderstandingNode>();
public DbSet<UnderstandingEdge> UnderstandingEdges => Set<UnderstandingEdge>();
public DbSet<ConceptualSchema> ConceptualSchemas => Set<ConceptualSchema>();
public DbSet<SchemaMembership> SchemaMemberships => Set<SchemaMembership>();
public DbSet<DialecticalSynthesis> DialecticalSyntheses => Set<DialecticalSynthesis>();
public DbSet<GraphSnapshot> GraphSnapshots => Set<GraphSnapshot>();
```

---

## 9. Service Architecture

### 9.1 New Services

```
Services/
  UnderstandingGraph/
    UnderstandingGraphService.cs        — Core CRUD + sync + query
    EmbeddingService.cs                 — Multi-vector embedding generation
    TensorConstructionService.cs        — Build & maintain the 3-way tensor
    TensorDecompositionService.cs       — CP & Tucker decomposition (ALS)
    SpectralClusteringService.cs        — Graph Laplacian + spectral clustering
    FcaLatticeService.cs                — Formal Concept Analysis
    TdaService.cs                       — Persistent homology (ripser)
    DialecticalSynthesisService.cs      — Thesis-antithesis-synthesis engine
    SchemaLabelingService.cs            — LLM-based schema labeling
    GraphSnapshotService.cs             — Periodic graph topology snapshots
    UnderstandingQueryService.cs        — High-level query API
```

### 9.2 Service Responsibilities

#### `UnderstandingGraphService`
- **Sync pipeline**: `Argument → Propositions → UnderstandingNodes` (enhances existing `CommonUnderstandingService`)
- **Edge detection**: Runs comparative analysis between all pairs of new propositions and existing nodes to detect `supports`, `contradicts`, `qualifies`, `assumes` relationships
- **Weight computation**: Calculates composite edge weights with temporal decay
- **Reinforcement tracking**: When multiple independent analyses detect the same edge, increments `ReinforcementCount` and refreshes `LastReinforcedAt`
- **Migration**: Backfill from existing `CommonUnderstandingNode`/`CommonUnderstandingEdge` tables

#### `EmbeddingService`
- **Semantic embeddings**: Uses `IEmbeddingGenerator<string, Embedding<float>>` (same pattern as existing `EmbeddingService` in Social layer) to generate 1536-dim embeddings for all new `UnderstandingNode` canonical texts
- **Graph embeddings**: Runs node2vec or GraphSAGE on the graph topology to produce 128-dim structural embeddings
- **Dimensional coordinates**: Aggregates belief dimensions from all users whose profiles reference a given node, producing a weighted centroid in belief space
- **Batch backfill**: Background worker processes nodes without embeddings

#### `TensorConstructionService`
- Builds the sparse 3rd-order tensor `T ∈ ℝ^(A × P × D)` from the current graph state
- Maintains a **low-rank approximation** for real-time querying
- Supports **incremental updates** — when new arguments or propositions are added, the tensor is updated without full recomputation
- Exports tensor to Python for heavy decomposition (via file or in-process via ML.NET)

#### `TensorDecompositionService`
- Runs CP decomposition via **Alternating Least Squares (ALS)**
- Determines optimal rank `R` via cross-validation (reconstruction error on held-out entries)
- Runs Tucker decomposition for deeper analysis (nightly batch)
- Stores factor matrices for querying

#### `SpectralClusteringService`
- Builds the normalized graph Laplacian
- Computes top-`k` eigenvectors (using sparse eigensolver — ARPACK or Lanczos)
- Determines `k` via eigengap heuristic
- Runs k-means on eigenvector coordinates
- Creates `ConceptualSchema` records for each cluster

#### `FcaLatticeService`
- Builds the Arguments × Propositions incidence matrix
- Computes all formal concepts (NextClosure algorithm)
- Builds the concept lattice (Hasse diagram)
- Identifies **top concepts** (most general) and **bottom concepts** (most specific)

#### `TdaService`
- Builds Vietoris-Rips complex from embedding point cloud
- Computes persistent homology (H₀, H₁, H₂) using ripser or gudhi
- Extracts significant features (persistence > threshold)
- Maps topological features back to graph nodes

#### `DialecticalSynthesisService`
- Scans for high-confidence `contradicts` edges without existing `synthesizes` resolution
- For each unresolved contradiction, invokes LLM to generate a synthesis proposition
- Creates the synthesis node and `synthesizes` edges
- Tracks synthesis depth (recursive: a synthesis may itself be contradicted)

#### `SchemaLabelingService`
- For each `ConceptualSchema`, collects top-loading propositions and dimensions
- Invokes LLM with a structured prompt to generate:
  - A concise label (2–5 words)
  - A detailed description (2–3 sentences)
  - Key themes and tensions within the schema
- Stores the label and description on the `ConceptualSchema` record

#### `GraphSnapshotService`
- Runs on a configurable schedule (default: daily)
- Captures full graph topology metrics
- Runs all detection algorithms and stores results
- Computes **delta from previous snapshot** — which schemas grew, shrank, merged, split, or appeared/disappeared
- Persists `GraphSnapshot` records for temporal analysis

#### `UnderstandingQueryService`
High-level query API for the frontend:

```csharp
// Find the conceptual schema around a proposition
Task<ConceptualSchema?> GetSchemaForNodeAsync(int nodeId);

// Find all propositions in a schema, ordered by centrality
Task<List<UnderstandingNode>> GetSchemaNodesAsync(int schemaId, string orderBy = "centrality");

// Find dialectical pairs (contradictions) within or across schemas
Task<List<DialecticalPair>> GetDialecticalPairsAsync(int? schemaId = null);

// Trace the dialectical hierarchy from a proposition upward
Task<List<DialecticalSynthesis>> GetSynthesisChainAsync(int nodeId);

// Find bridging propositions between two schemas
Task<List<UnderstandingNode>> GetBridgeNodesAsync(int schemaId1, int schemaId2);

// Get temporal evolution of a schema
Task<List<SchemaEvolutions>> GetSchemaEvolutionAsync(int schemaId);

// Find conceptual blindspots (TDA voids or FCA gaps)
Task<List<ConceptualBlindspot>> GetBlindspotsAsync();

// Get the full understanding map for visualization
Task<UnderstandingMap> GetMapAsync(MapQuery query);
```

---

## 10. Query & Navigation

### 10.1 The Schema Explorer

A UI that visualizes the Understanding Graph as an interactive map:

- **Nodes** = propositions, sized by centrality, colored by schema membership
- **Edges** = relationships, colored by type, weighted by thickness
- **Clusters** = schemas, shown as colored regions (Voronoi or convex hull)
- **Search** = semantic search across all nodes
- **Filter** = by schema, relationship type, confidence range, time range
- **Click** = show node detail: text, confidence, evidence, schema memberships, connected nodes
- **Path finder** = "Show me the shortest path between these two propositions"

### 10.2 The Dialectical Navigator

A focused view for exploring contradictions and syntheses:

- **Thesis-antithesis-synthesis triads** shown as triangular structures
- **Dialectical hierarchy** shown as a tree (depth 0 → depth 1 → depth 2 syntheses)
- **Unresolved contradictions** highlighted in red
- **Synthesis suggestions** shown as dashed nodes (not yet accepted)
- **Click a contradiction** → show both sides with evidence, then the synthesis (if exists)

### 10.3 The Evolution Viewer

A temporal view showing how the Understanding Graph changes:

- **Timeline slider** — drag to see the graph at different points in time
- **Schema lifecycle** — which schemas appeared, merged, split, or disappeared
- **Growth animation** — nodes and edges appearing over time
- **Hot/cold zones** — areas of the graph with high/low recent activity

### 10.4 Query Examples

```
Q: "What conceptual schema does proposition X belong to?"
A: Schema "Distributive Justice" (coherence: 0.87, 42 member propositions)

Q: "Which arguments bridge the gap between Schema A and Schema B?"
A: Argument "Universal Basic Income" (ID 15) references 3 propositions in Schema A
   and 2 propositions in Schema B — it's a bridging argument.

Q: "Show me the dialectical hierarchy around proposition X."
A: Proposition X → Synthesis Y (depth 1) → Synthesis Z (depth 2)

Q: "What are the top 5 most central propositions in the graph?"
A: 1. "Human rights are universal" (betweenness: 0.42)
   2. "Markets allocate resources efficiently" (betweenness: 0.38)
   ...

Q: "Which conceptual blindspots exist?"
A: TDA detected a void in the embedding space near propositions about
   "digital privacy" — no arguments address the intersection of privacy
   and algorithmic accountability.

Q: "How has Schema X evolved over the last month?"
A: Schema X grew from 12 to 28 nodes, split into two sub-schemas on day 14,
   coherence increased from 0.72 to 0.81.
```

---

## 11. Visualization

### 11.1 Graph Rendering

The Understanding Graph is a large, complex structure. Visualization requires:

- **Force-directed layout** (d3-force or vis-network) for interactive exploration
- **WebGL rendering** (three.js or pixi.js) for performance with 1000+ nodes
- **Level-of-detail**: Show schemas as aggregated nodes at zoom-out, individual propositions at zoom-in
- **Semantic zoom**: At schema level, show schema labels and inter-schema edges; at node level, show proposition text and intra-schema edges

### 11.2 Schema View

```
┌─────────────────────────────────────────────────────────────┐
│  UNDERSTANDING MAP                                          │
│                                                             │
│  ┌──────────┐    ┌──────────┐    ┌──────────┐              │
│  │ Distrib. │◄──►│ Economic │◄──►│  Rights  │              │
│  │ Justice  │    │  Freedom │    │  Based   │              │
│  │ (42)     │    │ (38)     │    │ (55)     │              │
│  └──────────┘    └──────────┘    └──────────┘              │
│       │               │               │                    │
│       ▼               ▼               ▼                    │
│  ┌──────────┐    ┌──────────┐    ┌──────────┐              │
│  │  Social  │◄──►│  Market  │◄──►│Individual│              │
│  │ Welfare  │    │Efficiency│    │Autonomy  │              │
│  │ (23)     │    │ (31)     │    │ (47)     │              │
│  └──────────┘    └──────────┘    └──────────┘              │
│       │               │               │                    │
│       └───────────────┼───────────────┘                    │
│                       ▼                                    │
│                  ┌──────────┐                               │
│                  │  Common  │  ← Bridging schema            │
│                  │  Ground  │                               │
│                  │  (12)    │                               │
│                  └──────────┘                               │
│                                                             │
│  [Search] [Filter] [Timeline: ████████░░] [Legend]         │
└─────────────────────────────────────────────────────────────┘
```

### 11.3 Dialectical Hierarchy View

```
Depth 0:  "Markets are efficient" ──contradicts── "Markets fail systematically"
                        \                             /
                         ▼                           ▼
Depth 1:          "Markets are efficient under specific conditions"
                        \                             /
                         ▼                           ▼
Depth 2:    "Regulated markets balance efficiency and equity"
```

---

## 12. Implementation Roadmap

### Phase 3a — Foundation (Weeks 1–3)

| Step | Description | Key Files |
|------|-------------|-----------|
| 1 | Create new entity models (`UnderstandingNode`, `UnderstandingEdge`, `ConceptualSchema`, etc.) | `Models/UnderstandingGraph/` |
| 2 | Add DbContext sets and migrations | `Data/ApplicationDbContext.cs` |
| 3 | Build `UnderstandingGraphService` — sync from existing arguments, edge detection | `Services/UnderstandingGraph/` |
| 4 | Build `EmbeddingService` — semantic + graph embeddings | `Services/UnderstandingGraph/` |
| 5 | Migrate existing `CommonUnderstandingNode`/`Edge` data to new tables | Migration script |
| 6 | Add pgvector column for `SemanticEmbedding` on `UnderstandingNode` | Migration |

**Deliverable**: Understanding Graph populated with existing data, embeddings generated, basic CRUD working.

### Phase 3b — Graph Algorithms (Weeks 4–6)

| Step | Description | Key Files |
|------|-------------|-----------|
| 7 | Build `SpectralClusteringService` — Laplacian + eigenvectors + k-means | `Services/UnderstandingGraph/` |
| 8 | Build `TensorConstructionService` — sparse tensor from graph | `Services/UnderstandingGraph/` |
| 9 | Build `TensorDecompositionService` — CP decomposition via ALS | `Services/UnderstandingGraph/` |
| 10 | Build `FcaLatticeService` — formal concept analysis | `Services/UnderstandingGraph/` |
| 11 | Build `TdaService` — persistent homology (Python bridge or C# port) | `Services/UnderstandingGraph/` |

**Deliverable**: Schemas detected, tensor factors computed, concept lattice built.

### Phase 3c — Dialectical Engine (Weeks 7–8)

| Step | Description | Key Files |
|------|-------------|-----------|
| 12 | Build `DialecticalSynthesisService` — contradiction detection + LLM synthesis | `Services/UnderstandingGraph/` |
| 13 | Build `SchemaLabelingService` — LLM-based schema naming | `Services/UnderstandingGraph/` |
| 14 | Build `GraphSnapshotService` — periodic topology capture | `Services/UnderstandingGraph/` |
| 15 | Build `UnderstandingQueryService` — high-level query API | `Services/UnderstandingGraph/` |

**Deliverable**: Dialectical synthesis working, schemas labeled, temporal tracking active.

### Phase 3d — Visualization & API (Weeks 9–10)

| Step | Description | Key Files |
|------|-------------|-----------|
| 16 | Build Understanding Map API endpoints | `Controllers/UnderstandingGraph/` |
| 17 | Build Schema Explorer UI (force-directed graph) | `Views/UnderstandingGraph/` |
| 18 | Build Dialectical Navigator UI | `Views/UnderstandingGraph/` |
| 19 | Build Evolution Viewer UI (timeline) | `Views/UnderstandingGraph/` |
| 20 | Integration testing and performance tuning | — |

**Deliverable**: Full interactive Understanding Graph visualization.

---

## 13. Open Research Questions

### 13.1 Scalability

- **Tensor size**: With 10,000 arguments and 50,000 propositions, the tensor has 500M entries (mostly zero). Sparse tensor decomposition at this scale requires distributed computing (Spark or Dask). Is a single-node ALS implementation sufficient for our expected data volume?
- **Graph size**: Spectral clustering requires O(N³) eigendecomposition in the naive case. For N > 10,000 nodes, we need randomized SVD or Nyström approximation. What threshold should trigger the approximation?
- **Real-time updates**: The low-rank approximation can be updated incrementally, but how often should full recomputation run? Trade-off between accuracy and compute cost.

### 13.2 Mathematical Choices

- **Optimal rank R**: How to determine the rank for CP decomposition? Cross-validation on held-out tensor entries? Bayesian nonparametric methods (automatic relevance determination)?
- **Number of clusters k**: The eigengap heuristic works well for well-separated clusters, but real conceptual schemas may overlap. Should we use soft clustering (fuzzy c-means) instead of k-means?
- **Edge weight decay**: What is the optimal decay constant λ? Should it vary by relationship type (contradictions decay slower than supports)?
- **TDA noise threshold**: What persistence threshold separates genuine topological features from noise? Bootstrap methods?

### 13.3 Validation

- **Schema quality**: How do we validate that a discovered schema is "real" and not an artifact of the algorithm? Human evaluation? Predictive validity (does schema membership predict future argument positions)?
- **Synthesis quality**: How do we evaluate LLM-generated dialectical syntheses? Are they genuinely resolving the contradiction or just papering over it? Community voting on synthesis quality?
- **Ground truth**: Unlike supervised learning, there is no ground truth for "conceptual schemas of human understanding." The system is inherently exploratory. How do we communicate this to users?

### 13.4 Integration with Existing Features

- **Emergent Conclusions**: The Understanding Graph subsumes and extends the Emergent Conclusions Engine. Blindspots become TDA voids. Harmonies become bridging nodes between schemas. Should we deprecate the standalone engine or keep it as a simplified view?
- **Convergence Maps**: Pairwise user convergence can be enriched by schema membership — "You both participate in Schema X, but diverge on Schema Y." Should convergence scores be schema-weighted?
- **Belief Discovery**: The discovery system's adaptive questioning could target schema-level gaps — "We notice you haven't expressed a position on any proposition in Schema X. Would you like to explore it?"
- **Social Platform**: User-authored worldviews can be mapped onto the Understanding Graph — "Your worldview aligns most closely with Schema X (78% overlap) and Schema Y (62% overlap)."

### 13.5 Philosophical Considerations

- **Objectivity vs. emergence**: The Understanding Graph does not claim to represent objective truth. It represents the *structure of arguments that have been made*. This is a map of discourse, not of reality. The distinction must be clear to users.
- **Bias in embeddings**: LLM embeddings carry their own biases. If the semantic embedding space over-represents Western philosophical traditions, the discovered schemas will reflect that. How do we detect and mitigate embedding bias?
- **Synthesis as intervention**: Generating a dialectical synthesis is not a neutral act — it shapes the discourse. Should syntheses be clearly labeled as AI-generated? Should they require community acceptance before being added to the graph?
- **The map is not the territory**: The Understanding Graph is a model of understanding, not understanding itself. Users should be able to see the raw arguments behind every schema, not just the abstracted view.

---

## Appendix A: Glossary

| Term | Definition |
|------|-----------|
| **Understanding Graph** | The full system of nodes (propositions), edges (relationships), schemas (clusters), and syntheses (resolutions) |
| **Node** | A canonical proposition — an atomic truth-claim |
| **Edge** | A typed, weighted, provenance-tracked relationship between two nodes |
| **Schema** | A cluster of nodes that form a coherent conceptual framework |
| **Dialectical Synthesis** | A node that resolves a contradiction between two or more parent nodes |
| **Tensor** | A multi-dimensional array encoding Arguments × Propositions × Dimensions |
| **Tensor Decomposition** | Factorizing the tensor into latent components that reveal conceptual structure |
| **Spectral Clustering** | Clustering nodes using the eigenvectors of the graph Laplacian |
| **Formal Concept** | A maximal set of arguments sharing a maximal set of propositions |
| **Persistent Homology** | A TDA method that finds topological features (loops, voids) in point-cloud data |
| **Dialectical Temperature** | The proportion of a node's edges that are contradictions |
| **Schema Entropy** | How many different schemas a node participates in |
| **Bridge Node** | A node that connects two otherwise separate schemas |
| **Conceptual Blindspot** | A TDA-detected void in the embedding space — a region with no propositions |
| **Epistemic Fault Line** | A boundary between schemas with many cross-schema contradictions |

## Appendix B: Mathematical Formulae Reference

### Edge Weight (Composite)

$$w = \alpha \cdot \cos(\mathbf{e}_i, \mathbf{e}_j) + \beta \cdot J(N_i, N_j) + \gamma \cdot \min(c_i, c_j)$$

Where:
- $\mathbf{e}_i$ = semantic embedding of node $i$
- $J(N_i, N_j)$ = Jaccard similarity of neighborhoods
- $c_i$ = confidence of node $i$
- $\alpha = 0.4, \beta = 0.3, \gamma = 0.3$ (default)

### Temporal Decay

$$w(t) = w_0 \cdot \exp(-\lambda \cdot \Delta t)$$

Where $\lambda = 0.01$ per day (default), $\Delta t$ = days since last reinforcement.

### CP Decomposition

$$T \approx \sum_{r=1}^{R} \lambda_r \cdot \mathbf{a}_r \circ \mathbf{p}_r \circ \mathbf{d}_r$$

Minimizing reconstruction error:

$$\min_{\mathbf{A}, \mathbf{P}, \mathbf{D}} \| T - \sum_r \lambda_r \cdot \mathbf{a}_r \circ \mathbf{p}_r \circ \mathbf{d}_r \|_F^2$$

### Normalized Graph Laplacian

$$L_{\text{norm}} = I - D^{-1/2} A D^{-1/2}$$

### Controversy Score

$$C(v) = \frac{\min(w_{\text{support}}, w_{\text{oppose}})}{\max(w_{\text{support}}, w_{\text{oppose}})}$$

### Dialectical Temperature

$$T_d(v) = \frac{\sum_{u \in N(v)} w_{vu} \cdot \mathbb{1}[\text{rel}(v,u) = \text{contradicts}]}{\sum_{u \in N(v)} w_{vu}}$$

### Schema Entropy

$$H(v) = -\sum_{s \in S} p(s|v) \cdot \log p(s|v)$$

Where $p(s|v)$ is the membership weight of node $v$ in schema $s$.

### Convergence Score (Schema-Weighted)

$$C(u_1, u_2) = \sum_{s \in S} \theta_s \cdot J_s(u_1, u_2)$$

Where $\theta_s$ is the importance weight of schema $s$ and $J_s$ is the Jaccard similarity of the two users' proposition sets within schema $s$.

---

## Appendix C: Comparison to Existing Systems

| Feature | Our System | Wikipedia Knowledge Graph | ConceptNet | Google Knowledge Graph |
|---------|-----------|--------------------------|------------|----------------------|
| **Nodes** | Propositions (truth-claims) | Articles | Concepts | Entities |
| **Edges** | Dialectical relationships | Hyperlinks + categories | Semantic relations | Entity relationships |
| **Schema discovery** | Spectral + tensor + FCA + TDA | Manual categorization | Manual | Manual + ML |
| **Temporal** | Versioned snapshots | Edit history | Static | Static |
| **Dialectical** | Thesis-antithesis-synthesis | No | No | No |
| **Embedding** | Multi-vector (semantic + graph + dimensional) | Single (text) | Single (concept) | Single (entity) |
| **Provenance** | Full dialectical chain | Edit history | No | No |
| **Synthesis** | AI-generated resolution of contradictions | No | No | No |

---

## Appendix D: Key Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Tensor too large for single-node ALS | Cannot compute decomposition | Start with rank-R approximation; use randomized SVD; defer full Tucker to batch |
| Spectral clustering O(N³) | Slow on large graphs | Use Nyström approximation or randomized SVD for N > 10,000 |
| LLM synthesis quality unreliable | Poor dialectical resolutions | Require community voting; show confidence; allow rejection |
| Embedding bias distorts schemas | Schemas reflect LLM bias, not discourse | Bias audit; multiple embedding models; user-controllable embedding weights |
| Users don't understand the visualization | Feature unused | Progressive disclosure: start simple, add complexity on demand |
| Schema instability across runs | Confusing user experience | Track stability score; only promote stable schemas to UI |

---

*"The map of understanding is not understanding itself — but it is the best tool we have for navigating the space of what we know, what we contest, and what we have yet to discover."*