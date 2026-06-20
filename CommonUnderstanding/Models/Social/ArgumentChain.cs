using System.ComponentModel.DataAnnotations;

namespace CommonUnderstanding.Models.Social;

/// <summary>
/// A directed acyclic graph (DAG) of linked SocialArguments forming a multi-step
/// reasoning chain from premises to a terminal conclusion (RootArgumentId).
/// </summary>
public class ArgumentChain : BaseEntity
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = null!;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public Guid RootArgumentId { get; set; }
    public bool IsPublic { get; set; } = false;

    /// <summary>References UserAccount.Id</summary>
    public string UserId { get; set; } = null!;

    /// <summary>Stored as text[] in PostgreSQL.</summary>
    public string[] Tags { get; set; } = Array.Empty<string>();

    /// <summary>Ordered list of ArgumentIds in the chain — stored as uuid[] in PostgreSQL.</summary>
    public Guid[] ArgumentIds { get; set; } = Array.Empty<Guid>();

    /// <summary>pgvector embedding (centroid of argument embeddings).</summary>
    public float[]? Embedding { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public SocialArgument? RootArgument { get; set; }
    public ICollection<WorldviewChain> WorldviewChains { get; set; } = new List<WorldviewChain>();
}
