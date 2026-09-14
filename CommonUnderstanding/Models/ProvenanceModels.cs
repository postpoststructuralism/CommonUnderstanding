using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CommonUnderstanding.Models;

public enum SourceVerificationStatus
{
    Unverified,
    Verified,
    CorrectionIssued,
    Retracted
}

public enum EvidenceMatchDirection
{
    Supports,
    Contradicts,
    Irrelevant
}

public enum EvidenceMatchStatus
{
    Pending,
    Confirmed,
    Rejected
}

public class Source
{
    public int Id { get; set; }

    [Required, MaxLength(300)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(253)]
    public string? Domain { get; set; }

    [MaxLength(100)]
    public string? PublisherIdentifier { get; set; }

    public double ReliabilityScore { get; set; } = 0.5;
    public double? BiasLean { get; set; }

    [MaxLength(500)]
    public string? ExpertiseDomains { get; set; }

    public int RetractionCount { get; set; }
    public int CorrectionCount { get; set; }
    public SourceVerificationStatus VerificationStatus { get; set; }
    public DateTime? LastVerifiedAt { get; set; }

    [MaxLength(1000)]
    public string? VerificationNote { get; set; }

    public ICollection<EvidenceItem> EvidenceItems { get; set; } = new List<EvidenceItem>();
    public ICollection<EvidenceCorpusEntry> CorpusEntries { get; set; } = new List<EvidenceCorpusEntry>();
}

public class EvidenceCorpusEntry
{
    public long Id { get; set; }
    public int SourceId { get; set; }

    [Required, MaxLength(40)]
    public string Provider { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string ExternalId { get; set; } = string.Empty;

    [Required, MaxLength(1000)]
    public string Title { get; set; } = string.Empty;

    public string Abstract { get; set; } = string.Empty;
    public string? KeyFindings { get; set; }

    [MaxLength(100)]
    public string? DOI { get; set; }

    [MaxLength(1000)]
    public string? Uri { get; set; }

    public int? PublicationYear { get; set; }
    public EvidenceTier SuggestedTier { get; set; } = EvidenceTier.T6_AnecdoteOpinion;
    public SourceVerificationStatus VerificationStatus { get; set; }
    public float[]? Embedding { get; set; }
    public DateTime PublishedAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(SourceId))]
    public Source? Source { get; set; }

    public ICollection<EvidenceMatchSuggestion> MatchSuggestions { get; set; } = new List<EvidenceMatchSuggestion>();
}

public class EvidenceMatchSuggestion
{
    public long Id { get; set; }
    public int PropositionId { get; set; }
    public long EvidenceCorpusEntryId { get; set; }
    public EvidenceMatchDirection Direction { get; set; }
    public EvidenceMatchStatus Status { get; set; } = EvidenceMatchStatus.Pending;
    public double SimilarityScore { get; set; }
    public double ClassificationConfidence { get; set; }

    [Required, MaxLength(500)]
    public string Rationale { get; set; } = string.Empty;

    public EvidenceTier SuggestedTier { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }

    [MaxLength(100)]
    public string? ReviewedBy { get; set; }

    public int? EvidenceItemId { get; set; }

    [ForeignKey(nameof(PropositionId))]
    public Proposition? Proposition { get; set; }

    [ForeignKey(nameof(EvidenceCorpusEntryId))]
    public EvidenceCorpusEntry? EvidenceCorpusEntry { get; set; }

    [ForeignKey(nameof(EvidenceItemId))]
    public EvidenceItem? EvidenceItem { get; set; }
}