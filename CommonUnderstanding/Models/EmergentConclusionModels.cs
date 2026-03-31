using System.ComponentModel.DataAnnotations;

namespace CommonUnderstanding.Models;

// ─────────────────────────────────────────────
//  Enumerations
// ─────────────────────────────────────────────

public enum EmergentType
{
    Blindspot,
    Harmony
}

public enum EmergentCategory
{
    // Blindspots
    AssumptionCascade,       // Shared untested critical assumptions across arguments
    EvidenceDesert,          // High-stakes propositions with no or only anecdotal evidence
    SilentContradiction,     // Settled propositions that contradict each other (Phase 2 LLM)
    UnaddressedRebuttal,     // Strong rebuttals no argument has engaged with
    ConfidenceIllusion,      // High confidence built entirely on low-tier evidence

    // Harmonies
    ConvergentGround,        // Opposing stakeholders who accept specific shared premises
    ComplementaryChains,     // Arguments sharing high-confidence graph nodes
    EmergentConsensus,       // Propositions trending from contested toward settled
    CrossDomainReinforcement,// Different arguments feeding each other's conclusions (Phase 2 LLM)
    SharedValueCore          // Underlying values consistently appealed to (Phase 2 LLM)
}

// ─────────────────────────────────────────────
//  Core Result Model
// ─────────────────────────────────────────────

/// <summary>
/// A single emergent finding — either a blindspot (gap/risk) or a harmony
/// (convergence/opportunity) identified by cross-argument analysis.
/// </summary>
public class EmergentConclusion
{
    /// <summary>Whether this is a blindspot or a harmony.</summary>
    public EmergentType Type { get; set; }

    /// <summary>The specific category of emergence detected.</summary>
    public EmergentCategory Category { get; set; }

    /// <summary>Short human-readable title for the finding.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Detailed explanation of what was detected and why it matters.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Significance score [0.0–1.0]: how impactful this finding is.
    /// Higher = more arguments affected, higher confidence stakes.
    /// </summary>
    public double Significance { get; set; }

    /// <summary>
    /// Detection confidence [0.0–1.0]: how certain we are this pattern is real.
    /// </summary>
    public double Confidence { get; set; }

    // ── Provenance ──────────────────────────────────────────────────────────

    public List<int> InvolvedArgumentIds { get; set; } = new();
    public List<int> InvolvedPropositionIds { get; set; } = new();
    public List<int> InvolvedNodeIds { get; set; } = new();
    public List<int> InvolvedStakeholderIds { get; set; } = new();

    // ── Provenance labels (human-readable, parallel to Id lists) ────────────

    public List<string> InvolvedArgumentTitles { get; set; } = new();
    public List<string> InvolvedPropositionTexts { get; set; } = new();

    // ── Actionable output ───────────────────────────────────────────────────

    /// <summary>For blindspots: what action could resolve or illuminate the gap.</summary>
    public string? SuggestedAction { get; set; }

    /// <summary>For harmonies: what opportunity or collaboration this reveals.</summary>
    public string? OpportunityDescription { get; set; }

    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
}

// ─────────────────────────────────────────────
//  Graph Health Summary
// ─────────────────────────────────────────────

/// <summary>
/// Aggregate health metrics for the Common Understanding Graph.
/// </summary>
public class GraphHealthSummary
{
    public int TotalArguments { get; set; }
    public int TotalPropositions { get; set; }
    public int TotalEvidenceItems { get; set; }
    public int TotalStakeholders { get; set; }
    public int TotalComparisons { get; set; }
    public double AverageConfidence { get; set; }
    public int SettledCount { get; set; }
    public int ContestedCount { get; set; }
    public int UnknownCount { get; set; }
    public int UnevaluatedCount { get; set; }

    /// <summary>Percentage of graph nodes that have at least one evidence item.</summary>
    public double EvidenceCoveragePercent { get; set; }

    /// <summary>Count of critical assumptions that are unsupported across all arguments.</summary>
    public int CriticalAssumptionsUntested { get; set; }

    /// <summary>Count of high-strength rebuttals that appear unaddressed.</summary>
    public int HighStrengthRebuttals { get; set; }
}

// ─────────────────────────────────────────────
//  Report Container
// ─────────────────────────────────────────────

/// <summary>
/// Full emergent conclusions report produced by the engine.
/// </summary>
public class EmergentConclusionsReport
{
    public List<EmergentConclusion> Blindspots { get; set; } = new();
    public List<EmergentConclusion> Harmonies { get; set; } = new();
    public GraphHealthSummary GraphHealth { get; set; } = new();

    /// <summary>
    /// LLM-generated executive summary narrative (populated in deep-analysis mode only).
    /// </summary>
    public string? ExecutiveSummary { get; set; }

    /// <summary>True when the graph has enough data for meaningful analysis.</summary>
    public bool HasSufficientData { get; set; }

    /// <summary>Human-readable explanation when the graph lacks sufficient data.</summary>
    public string? InsufficientDataReason { get; set; }

    /// <summary>True when LLM-powered detectors were included in this run.</summary>
    public bool IsDeepAnalysis { get; set; }

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    // ── Convenience projections ─────────────────────────────────────────────

    public IEnumerable<EmergentConclusion> TopBlindspots(int n = 5) =>
        Blindspots.OrderByDescending(b => b.Significance).Take(n);

    public IEnumerable<EmergentConclusion> TopHarmonies(int n = 5) =>
        Harmonies.OrderByDescending(h => h.Significance).Take(n);
}

// ─────────────────────────────────────────────
//  Persisted History
// ─────────────────────────────────────────────

/// <summary>
/// A snapshot of an emergent conclusions report, persisted so the community
/// can track how blindspots and harmonies evolve over time.
/// </summary>
public class PersistedEmergentReport
{
    public int Id { get; set; }

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    public bool IsDeepAnalysis { get; set; }

    // ── Graph health at snapshot time ────────────────────────────────────────
    public int TotalArguments { get; set; }
    public int TotalPropositions { get; set; }
    public int TotalEvidenceItems { get; set; }
    public double AverageConfidence { get; set; }
    public int SettledCount { get; set; }
    public int ContestedCount { get; set; }

    // ── Finding counts ───────────────────────────────────────────────────────
    public int BlindspotCount { get; set; }
    public int HarmonyCount { get; set; }
    public int CriticalAssumptionsUntested { get; set; }

    /// <summary>Full JSON serialization of the findingss list (for diff/comparison).</summary>
    public string? BlindspotsSummaryJson { get; set; }
    public string? HarmoniesSummaryJson { get; set; }

    /// <summary>LLM executive summary, if generated.</summary>
    public string? ExecutiveSummary { get; set; }

    /// <summary>
    /// Full JSON serialization of the complete EmergentConclusionsReport,
    /// enabling the report to be reconstructed and displayed after the SSE stream finishes.
    /// </summary>
    public string? FullReportJson { get; set; }
}
