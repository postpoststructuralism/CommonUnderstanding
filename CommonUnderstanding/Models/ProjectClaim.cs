namespace CommonUnderstanding.Models;

public enum ClaimOrigin { Unknown, Authored, Imported, Generated }
public enum ClaimKind { Empirical, Normative, Predictive, Interpretive, Philosophical, Attributed, Mixed }
public enum ReviewDisposition { Unreviewed, Accepted, AcceptedWithObjections, Rejected }
public enum EvidenceCoverage { Unknown, None, Partial, Complete }
public enum ClaimLifecycle { Active, Withdrawn, Superseded }

public sealed class ProjectSourceVersion
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ProjectId { get; init; }
    public string SourceKey { get; init; } = string.Empty;
    public int Version { get; init; }
    public string Text { get; init; } = string.Empty;
    public string Sha256 { get; init; } = string.Empty;
    public string CreatedBy { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public bool Retracted { get; init; }
}

public sealed class ProjectClaim
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid ChangeToken { get; set; } = Guid.NewGuid();
    public List<ProjectClaimRevision> Revisions { get; set; } = new();
}

public sealed class ProjectClaimRevision
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ClaimId { get; init; }
    public int Number { get; init; }
    public string DisplayText { get; init; } = string.Empty;
    public string MatchingText { get; init; } = string.Empty;
    public ClaimKind Kind { get; init; }
    public ClaimOrigin Origin { get; init; }
    public string Scope { get; init; } = string.Empty;
    public string Attribution { get; init; } = string.Empty;
    public string Objections { get; init; } = string.Empty;
    public Guid? SourceVersionId { get; init; }
    public int? PassageStart { get; init; }
    public int? PassageLength { get; init; }
    public string? PassageText { get; init; }
    public Guid? PreviousRevisionId { get; init; }
    public string Transformation { get; init; } = string.Empty;
    public string CreatedBy { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public EvidenceCoverage Coverage { get; init; } = EvidenceCoverage.Unknown;
    public ClaimLifecycle Lifecycle { get; init; } = ClaimLifecycle.Active;
    public bool RequiresRevalidation { get; init; }
    public string? FormerStatus { get; init; }
    public string InputHash { get; init; } = string.Empty;
    public List<ProjectClaimReview> Reviews { get; set; } = new();
}

public sealed class ProjectClaimReview
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid RevisionId { get; init; }
    public ReviewDisposition Disposition { get; init; }
    public string ReviewerId { get; init; } = string.Empty;
    public string Rationale { get; init; } = string.Empty;
    public string Conditions { get; init; } = string.Empty;
    public string ObjectionTreatment { get; init; } = string.Empty;
    public string InputHash { get; init; } = string.Empty;
    public string PolicyVersion { get; init; } = string.Empty;
    public DateTimeOffset ReviewedAt { get; init; } = DateTimeOffset.UtcNow;
}