using System.ComponentModel.DataAnnotations;

namespace CommonUnderstanding.Models;

// ─────────────────────────────────────────────
//  Enumerations
// ─────────────────────────────────────────────

public enum SessionStatus
{
    Active,
    Analyzing,
    Concluded
}

// ─────────────────────────────────────────────
//  CollaborativeSession
// ─────────────────────────────────────────────

/// <summary>
/// A multi-user analytical session in which participants contribute arguments,
/// which are merged into a joint proposition graph and analyzed collectively.
/// The session produces a joint EmergentConclusionsReport and a multi-party ConvergenceMap.
/// </summary>
public class CollaborativeSession
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>JSON array of user IDs who are participants.</summary>
    public string ParticipantIdsJson { get; set; } = "[]";

    /// <summary>JSON dictionary: userId → [argumentId, argumentId, ...]</summary>
    public string ContributedArgumentIdsJson { get; set; } = "{}";

    /// <summary>JSON array of CommonUnderstandingNode IDs in the merged joint graph.</summary>
    public string MergedNodeIdsJson { get; set; } = "[]";

    public SessionStatus Status { get; set; } = SessionStatus.Active;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ConcludedAt { get; set; }

    /// <summary>FK to the ConvergenceMap produced for this session (null until analysis runs).</summary>
    public int? JointConvergenceMapId { get; set; }

    /// <summary>JSON-serialized EmergentConclusionsReport produced for this session.</summary>
    public string? ConsolidatedReportJson { get; set; }

    /// <summary>AI-generated executive summary of the session findings.</summary>
    public string? ExecutiveSummary { get; set; }
}
