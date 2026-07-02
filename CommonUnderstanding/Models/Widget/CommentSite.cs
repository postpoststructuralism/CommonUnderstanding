using System.ComponentModel.DataAnnotations;

namespace CommonUnderstanding.Models.Widget;

/// <summary>
/// A registered publisher site that embeds the Common Understanding widget.
/// Each site gets a unique API key and configuration.
/// </summary>
public class CommentSite
{
    public Guid Id { get; set; }

    /// <summary>References UserAccount.Id — the site owner.</summary>
    [Required]
    public string OwnerUserId { get; set; } = null!;

    /// <summary>The publisher's domain (e.g., "example.com").</summary>
    [Required, MaxLength(500)]
    public string Domain { get; set; } = null!;

    /// <summary>Display name for the site.</summary>
    [Required, MaxLength(200)]
    public string SiteName { get; set; } = null!;

    /// <summary>Pricing tier: free, pro, enterprise.</summary>
    [Required, MaxLength(20)]
    public string PlanTier { get; set; } = "free";

    /// <summary>Unique API key for authentication.</summary>
    [Required, MaxLength(64)]
    public string ApiKey { get; set; } = null!;

    /// <summary>Allowed CORS origins for the widget.</summary>
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();

    /// <summary>Moderation mode: ai, manual, hybrid.</summary>
    [Required, MaxLength(20)]
    public string ModerationMode { get; set; } = "ai";

    /// <summary>Optional URL to custom CSS for the widget.</summary>
    [MaxLength(1000)]
    public string? CustomCssUrl { get; set; }

    /// <summary>Optional logo URL for branding.</summary>
    [MaxLength(1000)]
    public string? LogoUrl { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<CommentThread> Threads { get; set; } = new List<CommentThread>();
    public ICollection<WidgetUsage> UsageRecords { get; set; } = new List<WidgetUsage>();
    public ICollection<CommentModerationItem> ModerationQueue { get; set; } = new List<CommentModerationItem>();
}