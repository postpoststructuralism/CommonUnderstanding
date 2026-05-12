using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CommonUnderstanding.Models;

// ─────────────────────────────────────────────
//  Enumerations
// ─────────────────────────────────────────────

public enum ArgumentStatus
{
    Draft,
    Decomposing,
    Adjudicating,
    Complete
}

public enum PropositionStatus
{
    Unevaluated,
    Settled,       // High confidence, broad agreement
    Contested,     // Conflicting evidence or stakeholder disagreement
    Unknown        // Insufficient evidence
}

public enum InferenceType
{
    Deductive,
    Inductive,
    Abductive,
    Analogical
}

public enum EvidenceDirection
{
    Supports,
    Opposes,
    Neutral
}

public enum EvidenceTier
{
    T1_SystematicReview = 1,    // 0.90–1.00
    T2_RCT = 2,                 // 0.70–0.89
    T3_Observational = 3,       // 0.50–0.69
    T4_ExpertConsensus = 4,     // 0.30–0.49
    T5_CaseStudy = 5,           // 0.20–0.29
    T6_AnecdoteOpinion = 6      // 0.00–0.09
}

public enum DecisionRecommendation
{
    Proceed,
    Investigate,
    Defer,
    Reject
}

// ─────────────────────────────────────────────
//  Core Argument Structure
// ─────────────────────────────────────────────

/// <summary>
/// Top-level container for a claim or proposal under analysis.
/// </summary>
public class Argument
{
    public int Id { get; set; }

    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string RawText { get; set; } = string.Empty;

    public ArgumentStatus Status { get; set; } = ArgumentStatus.Draft;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    [MaxLength(100)]
    public string? SubmittedBy { get; set; }

    // Navigation
    public ICollection<Claim> Claims { get; set; } = new List<Claim>();
    public AdjudicationSummary? AdjudicationSummary { get; set; }
}

/// <summary>
/// The top-level assertion the argument is making.
/// </summary>
public class Claim
{
    public int Id { get; set; }

    [Required]
    public int ArgumentId { get; set; }

    [Required]
    public string Text { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? ClaimType { get; set; }  // e.g., "normative", "empirical", "causal"

    // Navigation
    [ForeignKey(nameof(ArgumentId))]
    public Argument? Argument { get; set; }

    public ICollection<Proposition> Premises { get; set; } = new List<Proposition>();
    public ICollection<Assumption> Assumptions { get; set; } = new List<Assumption>();
    public ICollection<Qualifier> Qualifiers { get; set; } = new List<Qualifier>();
    public ICollection<Rebuttal> Rebuttals { get; set; } = new List<Rebuttal>();
    public ICollection<Syllogism> Syllogisms { get; set; } = new List<Syllogism>();
}

/// <summary>
/// An atomic, truth-evaluable statement (a premise or conclusion).
/// </summary>
public class Proposition
{
    public int Id { get; set; }

    [Required]
    public int ClaimId { get; set; }

    [Required]
    public string Text { get; set; } = string.Empty;

    public PropositionStatus Status { get; set; } = PropositionStatus.Unevaluated;

    /// <summary>
    /// Bayesian confidence score [0.0–1.0] after evidence adjudication.
    /// </summary>
    public double ConfidenceScore { get; set; } = 0.5;

    public int EvidenceCount { get; set; }

    /// <summary>
    /// AI-generated preliminary assessment of the proposition's truth value
    /// based on the model's knowledge base (before user-provided evidence).
    /// </summary>
    [MaxLength(500)]
    public string? ProvisionalAssessment { get; set; }

    /// <summary>
    /// Provisional confidence [0.0–1.0] from AI knowledge-base assessment.
    /// </summary>
    public double? ProvisionalConfidence { get; set; }

    public int SortOrder { get; set; }

    // Navigation
    [ForeignKey(nameof(ClaimId))]
    public Claim? Claim { get; set; }

    public ICollection<EvidenceItem> EvidenceItems { get; set; } = new List<EvidenceItem>();
}

/// <summary>
/// A formal deductive chain: major premise → minor premise → conclusion.
/// </summary>
public class Syllogism
{
    public int Id { get; set; }

    [Required]
    public int ClaimId { get; set; }

    [Required]
    public string MajorPremise { get; set; } = string.Empty;

    [Required]
    public string MinorPremise { get; set; } = string.Empty;

    [Required]
    public string Conclusion { get; set; } = string.Empty;

    public InferenceType InferenceType { get; set; } = InferenceType.Deductive;

    /// <summary>True if the logical form is valid (independent of truth of premises).</summary>
    public bool IsValidForm { get; set; }

    /// <summary>Identified logical fallacies, newline-separated.</summary>
    public string? FallaciesDetected { get; set; }

    public int SortOrder { get; set; }

    // Navigation
    [ForeignKey(nameof(ClaimId))]
    public Claim? Claim { get; set; }
}

/// <summary>
/// An unstated premise the argument implicitly depends on.
/// </summary>
public class Assumption
{
    public int Id { get; set; }

    [Required]
    public int ClaimId { get; set; }

    [Required]
    public string Text { get; set; } = string.Empty;

    /// <summary>Whether invalidating this assumption defeats the conclusion.</summary>
    public bool IsCritical { get; set; }

    /// <summary>Whether this assumption has supporting evidence.</summary>
    public bool IsSupported { get; set; }

    // Navigation
    [ForeignKey(nameof(ClaimId))]
    public Claim? Claim { get; set; }
}

/// <summary>
/// A scope or strength limitation on a claim.
/// </summary>
public class Qualifier
{
    public int Id { get; set; }

    [Required]
    public int ClaimId { get; set; }

    [Required]
    public string Text { get; set; } = string.Empty;

    // e.g., "contextual", "temporal", "population"
    [MaxLength(60)]
    public string? QualifierType { get; set; }

    // Navigation
    [ForeignKey(nameof(ClaimId))]
    public Claim? Claim { get; set; }
}

/// <summary>
/// A condition or counter-argument that could defeat the conclusion.
/// </summary>
public class Rebuttal
{
    public int Id { get; set; }

    [Required]
    public int ClaimId { get; set; }

    [Required]
    public string Text { get; set; } = string.Empty;

    /// <summary>Estimated strength of this rebuttal: low | medium | high</summary>
    [MaxLength(20)]
    public string? Strength { get; set; }

    // Navigation
    [ForeignKey(nameof(ClaimId))]
    public Claim? Claim { get; set; }
}

// ─────────────────────────────────────────────
//  Evidence
// ─────────────────────────────────────────────

/// <summary>
/// A piece of evidence linked to a specific proposition.
/// </summary>
public class EvidenceItem
{
    public int Id { get; set; }

    [Required]
    public int PropositionId { get; set; }

    [Required]
    public string Citation { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? SourceUri { get; set; }

    [MaxLength(100)]
    public string? DOI { get; set; }

    public EvidenceTier Tier { get; set; } = EvidenceTier.T6_AnecdoteOpinion;

    public EvidenceDirection Direction { get; set; } = EvidenceDirection.Neutral;

    /// <summary>Effect size (Cohen's d, odds ratio, etc.) if available.</summary>
    public double? EffectSize { get; set; }

    public int? SampleSize { get; set; }

    /// <summary>Replication status: unreplicated | partial | replicated | contradicted</summary>
    [MaxLength(40)]
    public string? ReplicationStatus { get; set; }

    public int? PublicationYear { get; set; }

    [MaxLength(100)]
    public string? AddedBy { get; set; }

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(PropositionId))]
    public Proposition? Proposition { get; set; }
}

// ─────────────────────────────────────────────
//  Adjudication
// ─────────────────────────────────────────────

/// <summary>
/// Overall adjudication result for an argument.
/// </summary>
public class AdjudicationSummary
{
    public int Id { get; set; }

    [Required]
    public int ArgumentId { get; set; }

    public double OverallConfidence { get; set; }

    public DecisionRecommendation Recommendation { get; set; }

    public string? ReasoningTrace { get; set; }

    /// <summary>Propositions with no or weak evidence (JSON array of proposition IDs).</summary>
    public string? EvidenceGapsJson { get; set; }

    /// <summary>Propositions where evidence conflicts (JSON array of proposition IDs).</summary>
    public string? ConflictingEvidenceJson { get; set; }

    /// <summary>Specific evidence requests that would change the recommendation.</summary>
    public string? NextSteps { get; set; }

    /// <summary>
    /// LLM-generated multi-paragraph narrative explaining the adjudication reasoning,
    /// evidence evaluation, and how the recommendation was reached.
    /// </summary>
    public string? DetailedNarrative { get; set; }

    public DateTime ComputedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(ArgumentId))]
    public Argument? Argument { get; set; }
}

// ─────────────────────────────────────────────
//  Decomposition Result DTO (not persisted)
// ─────────────────────────────────────────────

/// <summary>
/// Transient result from the ArgumentDecompositionService before persistence.
/// </summary>
public class DecompositionResult
{
    public string ClaimText { get; set; } = string.Empty;
    public string ClaimType { get; set; } = "empirical";
    public List<string> Premises { get; set; } = new();
    public List<SyllogismDto> Syllogisms { get; set; } = new();
    public List<string> Assumptions { get; set; } = new();
    public List<CriticalAssumptionDto> CriticalAssumptions { get; set; } = new();
    public List<string> Qualifiers { get; set; } = new();
    public List<RebuttalDto> Rebuttals { get; set; } = new();
    public InferenceType InferenceType { get; set; } = InferenceType.Deductive;
    public string? ValidationNotes { get; set; }

    /// <summary>Provisional truth-value assessments for each premise (parallel list).</summary>
    public List<ProvisionalAssessmentDto> ProvisionalAssessments { get; set; } = new();
}

public class SyllogismDto
{
    public string MajorPremise { get; set; } = string.Empty;
    public string MinorPremise { get; set; } = string.Empty;
    public string Conclusion { get; set; } = string.Empty;
    public InferenceType InferenceType { get; set; } = InferenceType.Deductive;
}

public class CriticalAssumptionDto
{
    public string Text { get; set; } = string.Empty;
    public bool IsCritical { get; set; }
}

public class RebuttalDto
{
    public string Text { get; set; } = string.Empty;
    public string Strength { get; set; } = "medium";
}

public class ProvisionalAssessmentDto
{
    public string PremiseText { get; set; } = string.Empty;
    public double Confidence { get; set; } = 0.5;
    public string Assessment { get; set; } = string.Empty;
}

// ─────────────────────────────────────────────
//  Comparative Analysis
// ─────────────────────────────────────────────

public enum NetDirection
{
    FavoursA,
    FavoursB,
    Balanced,
    Insufficient
}

/// <summary>
/// Stores the result of a head-to-head comparison between two arguments.
/// </summary>
public class ArgumentComparison
{
    public int Id { get; set; }

    [Required]
    public int ArgumentAId { get; set; }

    [Required]
    public int ArgumentBId { get; set; }

    /// <summary>JSON array of ConflictingPremisePair objects.</summary>
    public string? ConflictingPremisesJson { get; set; }

    /// <summary>JSON array of strings — premises both arguments share or agree on.</summary>
    public string? ComplementaryPremisesJson { get; set; }

    /// <summary>JSON array of strings — premises unique to Argument A.</summary>
    public string? UniqueToPremisesAJson { get; set; }

    /// <summary>JSON array of strings — premises unique to Argument B.</summary>
    public string? UniqueToPremisesBJson { get; set; }

    public string? SynthesisNarrative { get; set; }

    public NetDirection NetDirection { get; set; } = NetDirection.Insufficient;

    /// <summary>How strongly the evidence tips in the net direction [0.0–1.0].</summary>
    public double NetConfidence { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(ArgumentAId))]
    public Argument? ArgumentA { get; set; }

    [ForeignKey(nameof(ArgumentBId))]
    public Argument? ArgumentB { get; set; }
}

// ─────────────────────────────────────────────
//  Comparison DTOs
// ─────────────────────────────────────────────

public class ConflictingPremisePair
{
    public string PremiseA { get; set; } = string.Empty;
    public string PremiseB { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
}

public class ComparisonResult
{
    public int ArgumentAId { get; set; }
    public int ArgumentBId { get; set; }
    public List<ConflictingPremisePair> ConflictingPremises { get; set; } = new();
    public List<string> ComplementaryPremises { get; set; } = new();
    public List<string> UniqueToPremisesA { get; set; } = new();
    public List<string> UniqueToPremisesB { get; set; } = new();
    public string SynthesisNarrative { get; set; } = string.Empty;
    public NetDirection NetDirection { get; set; } = NetDirection.Insufficient;
    public double NetConfidence { get; set; }
}
