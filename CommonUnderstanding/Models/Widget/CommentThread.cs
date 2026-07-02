using System.ComponentModel.DataAnnotations;

namespace CommonUnderstanding.Models.Widget;

/// <summary>
/// A comment thread on a specific page of a publisher's site.
/// Maps to a single URL and aggregates all SocialArguments posted to that page.
/// </summary>
public class CommentThread
{
    public Guid Id { get; set; }

    /// <summary>References CommentSite.Id.</summary>
    [Required]
    public Guid SiteId { get; set; }

    /// <summary>The full URL of the page this thread belongs to.</summary>
    [Required, MaxLength(2000)]
    public string PageUrl { get; set; } = null!;

    /// <summary>Optional page title for display.</summary>
    [MaxLength(500)]
    public string? PageTitle { get; set; }

    /// <summary>URL-derived slug for lookups (e.g., "/article/123").</summary>
    [Required, MaxLength(500)]
    public string ThreadSlug { get; set; } = null!;

    /// <summary>Whether the thread is locked (no new comments).</summary>
    public bool IsLocked { get; set; } = false;

    /// <summary>Whether the thread is in moderated mode.</summary>
    public bool IsModerated { get; set; } = false;

    /// <summary>Default sort order for comments.</summary>
    [Required, MaxLength(20)]
    public string SortOrder { get; set; } = "hot";

    /// <summary>Denormalized total comment count.</summary>
    public int TotalComments { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public CommentSite Site { get; set; } = null!;
    public ICollection<ThreadArgument> ThreadArguments { get; set; } = new List<ThreadArgument>();
}