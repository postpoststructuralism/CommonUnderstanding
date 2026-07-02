using System.ComponentModel.DataAnnotations;

namespace CommonUnderstanding.Models.Widget;

/// <summary>
/// Join table linking a CommentThread to its SocialArguments.
/// A thread is a curated view over existing SocialArgument entities.
/// </summary>
public class ThreadArgument
{
    [Required]
    public Guid ThreadId { get; set; }

    [Required]
    public Guid ArgumentId { get; set; }

    /// <summary>True if this is a top-level comment (not a reply).</summary>
    public bool IsTopLevel { get; set; } = true;

    /// <summary>Display ordering within the thread.</summary>
    public int SortOrder { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public CommentThread Thread { get; set; } = null!;
    public Social.SocialArgument Argument { get; set; } = null!;
}