using System.ComponentModel.DataAnnotations;

namespace CommonUnderstanding.Models.Widget;

/// <summary>
/// A detected contradiction between two comments, potentially across different threads on the same site.
/// </summary>
public class ThreadContradiction
{
    public Guid Id { get; set; }

    [Required]
    public Guid SiteId { get; set; }

    [Required]
    public Guid ThreadIdA { get; set; }

    [Required]
    public Guid ThreadIdB { get; set; }

    [Required]
    public Guid ArgumentIdA { get; set; }

    [Required]
    public Guid ArgumentIdB { get; set; }

    /// <summary>Type of contradiction: direct, implicit, value_conflict.</summary>
    [Required, MaxLength(30)]
    public string ContradictionType { get; set; } = null!;

    /// <summary>AI confidence score [0.0–1.0].</summary>
    public double Confidence { get; set; } = 0.5;

    /// <summary>AI-generated explanation of the contradiction.</summary>
    public string? Explanation { get; set; }

    public bool IsResolved { get; set; } = false;

    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
}