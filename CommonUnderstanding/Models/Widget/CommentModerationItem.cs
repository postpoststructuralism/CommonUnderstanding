using System.ComponentModel.DataAnnotations;

namespace CommonUnderstanding.Models.Widget;

/// <summary>
/// An item in the moderation queue for a publisher site.
/// Created when AI flags a comment or a user reports it.
/// </summary>
public class CommentModerationItem
{
    public Guid Id { get; set; }

    [Required]
    public Guid SiteId { get; set; }

    [Required]
    public Guid ArgumentId { get; set; }

    /// <summary>pending, approved, rejected.</summary>
    [Required, MaxLength(20)]
    public string Status { get; set; } = "pending";

    /// <summary>Reason for flagging: ai_fallacy, toxicity, spam, user_report.</summary>
    [MaxLength(30)]
    public string? FlagReason { get; set; }

    /// <summary>AI confidence in the flag [0.0–1.0].</summary>
    public double? AiConfidence { get; set; }

    /// <summary>References UserAccount.Id of the moderator who reviewed this.</summary>
    public string? ReviewedByUserId { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}