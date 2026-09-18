using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CommonUnderstanding.Models.Social;

namespace CommonUnderstanding.Models;

/// <summary>
/// A probability forecast for a falsifiable proposition, scored once its outcome is known.
/// </summary>
public class Prediction
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public int PropositionId { get; set; }

    /// <summary>References UserAccount.Id.</summary>
    [Required, MaxLength(450)]
    public string UserId { get; set; } = null!;

    /// <summary>Optional worldview under which the forecast was made.</summary>
    public Guid? WorldviewId { get; set; }

    [Range(0.0, 1.0)]
    public double Probability { get; set; }

    public DateTime ResolutionDate { get; set; }
    public bool? ActualOutcome { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(PropositionId))]
    public Proposition Proposition { get; set; } = null!;

    [ForeignKey(nameof(WorldviewId))]
    public Worldview? Worldview { get; set; }
}