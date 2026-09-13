namespace CommonUnderstanding.Models;

public enum ReferenceSourceType
{
    Legal,
    Policy,
    Organizational,
    Scientific,
    Ethical
}

public enum ReferenceRelationshipType
{
    Supports,
    Tensions,
    Unclassified
}

public class ReferenceFramework
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ReferenceSourceType SourceType { get; set; }
    public string Version { get; set; } = string.Empty;
    public string JurisdictionScope { get; set; } = string.Empty;
    public bool IsShared { get; set; }
    public string SourceFileName { get; set; } = string.Empty;
    public string SourceContentType { get; set; } = string.Empty;
    public long SourceFileSize { get; set; }
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
    public string ImportedByUserId { get; set; } = string.Empty;

    public ICollection<ReferenceFrameworkOwner> Owners { get; set; } = [];
    public ICollection<ReferenceProposition> Propositions { get; set; } = [];
}

public class ReferenceFrameworkOwner
{
    public Guid ReferenceFrameworkId { get; set; }
    public string UserId { get; set; } = string.Empty;

    public ReferenceFramework ReferenceFramework { get; set; } = null!;
    public UserAccount User { get; set; } = null!;
}

public class ReferenceProposition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReferenceFrameworkId { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Assessment { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public int SortOrder { get; set; }
    public float[]? Embedding { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ReferenceFramework ReferenceFramework { get; set; } = null!;
    public ICollection<ReferenceFrameworkRelationship> Relationships { get; set; } = [];
}

public class ReferenceFrameworkRelationship
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReferencePropositionId { get; set; }
    public int? PropositionId { get; set; }
    public Guid? SocialArgumentId { get; set; }
    public ReferenceRelationshipType RelationshipType { get; set; }
    public double Score { get; set; }
    public DateTime ComputedAt { get; set; } = DateTime.UtcNow;

    public ReferenceProposition ReferenceProposition { get; set; } = null!;
    public Proposition? Proposition { get; set; }
    public Social.SocialArgument? SocialArgument { get; set; }
}