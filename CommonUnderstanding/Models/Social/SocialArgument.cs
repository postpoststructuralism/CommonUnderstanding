using System.ComponentModel.DataAnnotations;

namespace CommonUnderstanding.Models.Social;

/// <summary>
/// A structured set of Propositions forming a complete reasoning unit.
/// This is the primary social object in the feed: votable, linkable, chainable, taggable.
///
/// Note: named SocialArgument to avoid conflict with the existing analytical Argument entity.
/// Phase 2 spec calls this "Argument" — in controllers and DTOs the social naming convention is preserved.
/// </summary>
public class SocialArgument : BaseEntity
{
    [Required, MaxLength(300)]
    public string Title { get; set; } = null!;

    public Guid ClaimPropositionId { get; set; }

    [Required]
    public string WarrantText { get; set; } = null!;

    public string? ResolutionText { get; set; }

    public double Weight { get; set; } = 1.0;
    public bool IsPublic { get; set; } = false;

    /// <summary>Set to true if flagged for shadow-banning (low validity or community flags).</summary>
    public bool IsShadowBanned { get; set; } = false;

    /// <summary>References UserAccount.Id</summary>
    public string UserId { get; set; } = null!;

    /// <summary>
    /// When this social argument was published from an existing Phase 1 analytical
    /// Argument, this links back to that Argument.Id. Null for natively-created posts.
    /// Used to keep the two layers in sync and prevent duplicate publishes.
    /// </summary>
    public int? SourceArgumentId { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // ── Generation provenance ──
    public bool IsAIGenerated { get; set; }

    [MaxLength(300)]
    public string? GenerationSourceKey { get; set; }

    [MaxLength(100)]
    public string? GeneratorProvider { get; set; }

    [MaxLength(200)]
    public string? GeneratorModel { get; set; }

    [MaxLength(50)]
    public string? GeneratorPromptVersion { get; set; }

    public string? GenerationProvenanceJson { get; set; }

    // ── Denormalized vote tallies — updated asynchronously by VotingHub consumer ──
    public int UpvoteCount { get; set; } = 0;
    public int DownvoteCount { get; set; } = 0;
    public double HotScore { get; set; } = 0.0;
    public double WilsonScore { get; set; } = 0.0;
    public double ControversyScore { get; set; } = 0.0;

    // ── Reply tracking ──
    public int ReplyCount { get; set; } = 0;

    // ── AI validation ──
    public bool IsAIValidated { get; set; } = false;
    public double? AIValidityScore { get; set; }
    public string? AIFallacyFlags { get; set; }  // JSON array of fallacy names

    // ── Follow-up (reply) relevance assessment ──
    /// <summary>
    /// For follow-up arguments: how relevant and effective this reply is at
    /// addressing the parent argument (0.0 = irrelevant, 1.0 = highly effective).
    /// Null for top-level (non-reply) arguments.
    /// </summary>
    public double? FollowUpRelevanceScore { get; set; }

    /// <summary>AI-generated notes explaining the relevance/effectiveness assessment.</summary>
    public string? FollowUpEffectivenessNotes { get; set; }

    // ── Metadata stored as PostgreSQL text[] ──
    public string[] Tags { get; set; } = Array.Empty<string>();
    public string[] SchwartzValues { get; set; } = Array.Empty<string>();

    /// <summary>pgvector embedding — 1536 dims.</summary>
    public float[]? Embedding { get; set; }

    // Navigation
    public SocialProposition? ClaimProposition { get; set; }
    public ICollection<SocialArgumentProposition> ArgumentPropositions { get; set; } = new List<SocialArgumentProposition>();
    public ICollection<ArgumentVote> Votes { get; set; } = new List<ArgumentVote>();
    public ICollection<ArgumentLink> OutboundLinks { get; set; } = new List<ArgumentLink>();
    public ICollection<ArgumentLink> InboundLinks { get; set; } = new List<ArgumentLink>();
}

/// <summary>Join table for SocialArgument ↔ SocialProposition (many-to-many).</summary>
public class SocialArgumentProposition
{
    public Guid ArgumentId { get; set; }
    public Guid PropositionId { get; set; }
    public SocialPropositionType Role { get; set; }
    public int OrderIndex { get; set; }

    // Navigation
    public SocialArgument Argument { get; set; } = null!;
    public SocialProposition Proposition { get; set; } = null!;
}
