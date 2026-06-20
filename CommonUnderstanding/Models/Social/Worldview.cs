using System.ComponentModel.DataAnnotations;

namespace CommonUnderstanding.Models.Social;

/// <summary>
/// A named, curated, user-authored collection of ArgumentChains representing
/// a coherent belief system. The unit of comparison in convergence analysis.
/// </summary>
public class Worldview : BaseEntity
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = null!;

    [MaxLength(2000)]
    public string? Description { get; set; }

    /// <summary>References UserAccount.Id</summary>
    public string UserId { get; set; } = null!;

    public bool IsPublic { get; set; } = false;

    /// <summary>Stored as text[] in PostgreSQL.</summary>
    public string[] Tags { get; set; } = Array.Empty<string>();

    /// <summary>Union of Schwartz values across all constituent arguments.</summary>
    public string[] SchwartzValues { get; set; } = Array.Empty<string>();

    /// <summary>
    /// 10-dimensional Schwartz value vector for radar chart display and cosine similarity.
    /// Dimensions: SelfDirection, Stimulation, Hedonism, Achievement, Power,
    ///             Security, Conformity, Tradition, Benevolence, Universalism.
    /// </summary>
    public double[] SchwartzVector { get; set; } = new double[10];

    /// <summary>pgvector embedding (centroid of all constituent argument embeddings).</summary>
    public float[]? Embedding { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<WorldviewChain> WorldviewChains { get; set; } = new List<WorldviewChain>();
    public ICollection<WorldviewVote> Votes { get; set; } = new List<WorldviewVote>();
}

/// <summary>Join table for Worldview ↔ ArgumentChain (many-to-many, ordered).</summary>
public class WorldviewChain
{
    public Guid WorldviewId { get; set; }
    public Guid ArgumentChainId { get; set; }
    public int OrderIndex { get; set; }

    // Navigation
    public Worldview Worldview { get; set; } = null!;
    public ArgumentChain ArgumentChain { get; set; } = null!;
}

public class WorldviewVote : BaseEntity
{
    public Guid WorldviewId { get; set; }

    /// <summary>References UserAccount.Id</summary>
    public string UserId { get; set; } = null!;

    public VoteValue Vote { get; set; }

    // Navigation
    public Worldview Worldview { get; set; } = null!;
}
