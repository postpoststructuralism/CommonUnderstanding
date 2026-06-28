using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CommonUnderstanding.Models;

// ─────────────────────────────────────────────────────────────────────────────
//  Phase 3 — Understanding Graph
//  Enhanced graph entities for the map of human understanding.
//  See UNDERSTANDING_GRAPH_ARCHITECTURE.md for full design document.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Enhanced graph node — successor to CommonUnderstandingNode.
/// Carries multi-vector representation, graph topology metrics, and schema membership.
/// </summary>
public class UnderstandingNode
{
    public int Id { get; set; }

    // ── Core text ──────────────────────────────────────────────────────────
    [Required]
    public string CanonicalText { get; set; } = string.Empty;

    [MaxLength(500)]
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

    /// <summary>JSON: variable-length dimensional coordinates from tensor decomposition.</summary>
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
/// Relationship types: supports, contradicts, qualifies, assumes, refines,
/// extends, rebuts, entails, exemplifies, analogous, synthesizes.
/// </summary>
public class UnderstandingEdge
{
    public int Id { get; set; }

    [Required]
    public int SourceNodeId { get; set; }

    [Required]
    public int TargetNodeId { get; set; }

    /// <summary>Relationship type from the extended ontology.</summary>
    [Required, MaxLength(40)]
    public string Relationship { get; set; } = string.Empty;

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
    [ForeignKey(nameof(SourceNodeId))]
    public UnderstandingNode? SourceNode { get; set; }

    [ForeignKey(nameof(TargetNodeId))]
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
    [Required, MaxLength(300)]
    public string Label { get; set; } = string.Empty;

    /// <summary>Detailed description of what this schema represents.</summary>
    [MaxLength(4000)]
    public string Description { get; set; } = string.Empty;

    /// <summary>How this schema was discovered: spectral_clustering | tensor_decomposition | fca_lattice | tda | manual</summary>
    [MaxLength(40)]
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
    [ForeignKey(nameof(NodeId))]
    public UnderstandingNode Node { get; set; } = null!;

    [ForeignKey(nameof(SchemaId))]
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
    [ForeignKey(nameof(SynthesisNodeId))]
    public UnderstandingNode? SynthesisNode { get; set; }
}

/// <summary>
/// A point-in-time snapshot of the Understanding Graph's topological structure.
/// Enables tracking schema evolution over time.
/// </summary>
public class GraphSnapshot
{
    public int Id { get; set; }

    /// <summary>Human-readable label for this snapshot (e.g., "Pre-debate baseline").</summary>
    [MaxLength(300)]
    public string Label { get; set; } = string.Empty;

    /// <summary>Total node count at snapshot time.</summary>
    public int NodeCount { get; set; }

    /// <summary>Total edge count at snapshot time.</summary>
    public int EdgeCount { get; set; }

    /// <summary>Number of conceptual schemas discovered.</summary>
    public int SchemaCount { get; set; }

    /// <summary>JSON: full topological summary (average centrality, clustering coefficient, etc.).</summary>
    public string TopologySummaryJson { get; set; } = "{}";

    /// <summary>JSON array of schema IDs active at this snapshot.</summary>
    public string SchemaIdsJson { get; set; } = "[]";

    /// <summary>JSON array of synthesis IDs active at this snapshot.</summary>
    public string SynthesisIdsJson { get; set; } = "[]";

    /// <summary>JSON: average dialectical temperature across all nodes.</summary>
    public double AverageDialecticalTemperature { get; set; }

    /// <summary>JSON: graph density (edges / possible edges).</summary>
    public double GraphDensity { get; set; }

    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
}