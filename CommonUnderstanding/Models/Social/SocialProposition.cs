using System.ComponentModel.DataAnnotations;

namespace CommonUnderstanding.Models.Social;

public enum SocialPropositionType
{
    Claim,
    Evidence,
    Warrant,
    Rebuttal
}

/// <summary>
/// An atomic, single truth-claim unit — the fundamental building block of social arguments.
/// Note: named SocialProposition to avoid conflict with the existing analytical Proposition entity.
/// </summary>
public class SocialProposition : BaseEntity
{
    [Required, MaxLength(2000)]
    public string Text { get; set; } = null!;

    public SocialPropositionType Type { get; set; }

    [MaxLength(2048)]
    public string? SourceUrl { get; set; }

    /// <summary>References UserAccount.Id</summary>
    public string UserId { get; set; } = null!;

    public bool IsAIGenerated { get; set; } = false;

    /// <summary>AI-generated propositions require explicit user confirmation before going public.</summary>
    public bool IsConfirmed { get; set; } = false;

    /// <summary>pgvector embedding — 1536 dims. Column type set in OnModelCreating.</summary>
    public float[]? Embedding { get; set; }

    // Navigation
    public ICollection<SocialArgumentProposition> ArgumentPropositions { get; set; } = new List<SocialArgumentProposition>();
}
