using Microsoft.EntityFrameworkCore;
using CommonUnderstanding.Models;
using CommonUnderstanding.Models.Social;
using CommonUnderstanding.Models.Graph;

namespace CommonUnderstanding.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    public DbSet<Argument> Arguments => Set<Argument>();
    public DbSet<Claim> Claims => Set<Claim>();
    public DbSet<Proposition> Propositions => Set<Proposition>();
    public DbSet<Syllogism> Syllogisms => Set<Syllogism>();
    public DbSet<Assumption> Assumptions => Set<Assumption>();
    public DbSet<Qualifier> Qualifiers => Set<Qualifier>();
    public DbSet<Rebuttal> Rebuttals => Set<Rebuttal>();
    public DbSet<EvidenceItem> EvidenceItems => Set<EvidenceItem>();
    public DbSet<AdjudicationSummary> AdjudicationSummaries => Set<AdjudicationSummary>();

    // Phase 3 — Common Understanding Graph (legacy)
    public DbSet<CommonUnderstandingNode> CommonUnderstandingNodes => Set<CommonUnderstandingNode>();
    public DbSet<CommonUnderstandingEdge> CommonUnderstandingEdges => Set<CommonUnderstandingEdge>();

    // Phase 3 — Understanding Graph (enhanced successor)
    public DbSet<UnderstandingNode> UnderstandingNodes => Set<UnderstandingNode>();
    public DbSet<UnderstandingEdge> UnderstandingEdges => Set<UnderstandingEdge>();
    public DbSet<ConceptualSchema> ConceptualSchemas => Set<ConceptualSchema>();
    public DbSet<SchemaMembership> SchemaMemberships => Set<SchemaMembership>();
    public DbSet<DialecticalSynthesis> DialecticalSyntheses => Set<DialecticalSynthesis>();
    public DbSet<GraphSnapshot> GraphSnapshots => Set<GraphSnapshot>();

    // Phase 4 — Stakeholders
    public DbSet<Stakeholder> Stakeholders => Set<Stakeholder>();
    public DbSet<StakeholderPosition> StakeholderPositions => Set<StakeholderPosition>();

    // Phase 5 — Comparative Analysis
    public DbSet<ArgumentComparison> ArgumentComparisons => Set<ArgumentComparison>();

    // Emergent Conclusions — historical snapshots
    public DbSet<PersistedEmergentReport> PersistedEmergentReports => Set<PersistedEmergentReport>();

    // Phase 6 — Multi-User Convergence
    public DbSet<UserConnection> UserConnections => Set<UserConnection>();
    public DbSet<SharedItem> SharedItems => Set<SharedItem>();
    public DbSet<ConvergenceMap> ConvergenceMaps => Set<ConvergenceMap>();
    public DbSet<CollaborativeSession> CollaborativeSessions => Set<CollaborativeSession>();

    // Persisted user profiles (durable identity across sessions)
    public DbSet<PersistedUserProfile> UserProfiles => Set<PersistedUserProfile>();

    // Account system (manually-managed, ADFS-ready)
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<AiUsageCounter> AiUsageCounters => Set<AiUsageCounter>();

    // ── Phase 2: Social Platform ─────────────────────────────────────────────
    public DbSet<SocialProposition> SocialPropositions => Set<SocialProposition>();
    public DbSet<SocialArgumentProposition> SocialArgumentPropositions => Set<SocialArgumentProposition>();
    public DbSet<SocialArgument> SocialArguments => Set<SocialArgument>();
    public DbSet<ArgumentLink> ArgumentLinks => Set<ArgumentLink>();
    public DbSet<ArgumentVote> ArgumentVotes => Set<ArgumentVote>();
    public DbSet<ArgumentChain> ArgumentChains => Set<ArgumentChain>();
    public DbSet<Worldview> Worldviews => Set<Worldview>();
    public DbSet<WorldviewChain> WorldviewChains => Set<WorldviewChain>();
    public DbSet<WorldviewVote> WorldviewVotes => Set<WorldviewVote>();
    public DbSet<DebateRoom> DebateRooms => Set<DebateRoom>();
    public DbSet<DebateContribution> DebateContributions => Set<DebateContribution>();
    public DbSet<EpistemicProfile> EpistemicProfiles => Set<EpistemicProfile>();
    public DbSet<UserReputation> UserReputations => Set<UserReputation>();
    public DbSet<XPTransaction> XPTransactions => Set<XPTransaction>();
    public DbSet<Moderator> Moderators => Set<Moderator>();
    public DbSet<ModerationFlag> ModerationFlags => Set<ModerationFlag>();
    public DbSet<ModerationAppeal> ModerationAppeals => Set<ModerationAppeal>();

    // ── Badge System Entities ────────────────────────────────────────────────
    public DbSet<ResolutionEndorsement> ResolutionEndorsements => Set<ResolutionEndorsement>();
    public DbSet<BadgeAwardLog> BadgeAwardLogs => Set<BadgeAwardLog>();
    public DbSet<StructuralResolution> StructuralResolutions => Set<StructuralResolution>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Argument
        modelBuilder.Entity<Argument>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(300).IsRequired();
            e.Property(x => x.Status).HasConversion<string>();
            e.HasMany(x => x.Claims)
             .WithOne(c => c.Argument)
             .HasForeignKey(c => c.ArgumentId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.AdjudicationSummary)
             .WithOne(a => a.Argument)
             .HasForeignKey<AdjudicationSummary>(a => a.ArgumentId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // Claim
        modelBuilder.Entity<Claim>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasMany(x => x.Premises)
             .WithOne(p => p.Claim)
             .HasForeignKey(p => p.ClaimId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Syllogisms)
             .WithOne(s => s.Claim)
             .HasForeignKey(s => s.ClaimId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Assumptions)
             .WithOne(a => a.Claim)
             .HasForeignKey(a => a.ClaimId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Qualifiers)
             .WithOne(q => q.Claim)
             .HasForeignKey(q => q.ClaimId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Rebuttals)
             .WithOne(r => r.Claim)
             .HasForeignKey(r => r.ClaimId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // Proposition
        modelBuilder.Entity<Proposition>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<string>();
            e.HasMany(x => x.EvidenceItems)
             .WithOne(ev => ev.Proposition)
             .HasForeignKey(ev => ev.PropositionId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // Syllogism
        modelBuilder.Entity<Syllogism>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.InferenceType).HasConversion<string>();
        });

        // EvidenceItem
        modelBuilder.Entity<EvidenceItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Tier).HasConversion<string>();
            e.Property(x => x.Direction).HasConversion<string>();
        });

        // AdjudicationSummary
        modelBuilder.Entity<AdjudicationSummary>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Recommendation).HasConversion<string>();
        });

        // CommonUnderstandingNode
        modelBuilder.Entity<CommonUnderstandingNode>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<string>();
            e.HasIndex(x => x.NormalizedKey);
        });

        // CommonUnderstandingEdge
        modelBuilder.Entity<CommonUnderstandingEdge>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.SourceNode)
             .WithMany(n => n.OutboundEdges)
             .HasForeignKey(x => x.SourceNodeId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.TargetNode)
             .WithMany(n => n.InboundEdges)
             .HasForeignKey(x => x.TargetNodeId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // Stakeholder
        modelBuilder.Entity<Stakeholder>(e =>
        {
            e.HasKey(x => x.Id);
        });

        // StakeholderPosition
        modelBuilder.Entity<StakeholderPosition>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Position).HasConversion<string>();
            e.HasOne(x => x.StakeholderRef)
             .WithMany(s => s.Positions)
             .HasForeignKey(x => x.StakeholderId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Argument)
             .WithMany()
             .HasForeignKey(x => x.ArgumentId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ArgumentComparison
        modelBuilder.Entity<ArgumentComparison>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.NetDirection).HasConversion<string>();
            e.HasOne(x => x.ArgumentA)
             .WithMany()
             .HasForeignKey(x => x.ArgumentAId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ArgumentB)
             .WithMany()
             .HasForeignKey(x => x.ArgumentBId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // UserConnection
        modelBuilder.Entity<UserConnection>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<string>();
            e.HasIndex(x => new { x.InitiatorUserId, x.RecipientUserId });
        });

        // SharedItem
        modelBuilder.Entity<SharedItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ItemType).HasConversion<string>();
            e.Property(x => x.Visibility).HasConversion<string>();
            e.HasIndex(x => x.SharedByUserId);
        });

        // ConvergenceMap
        modelBuilder.Entity<ConvergenceMap>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.User1Id, x.User2Id });
        });

        // CollaborativeSession
        modelBuilder.Entity<CollaborativeSession>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<string>();
        });

        // PersistedUserProfile
        modelBuilder.Entity<PersistedUserProfile>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Stage).HasMaxLength(50);
        });

        // UserAccount
        modelBuilder.Entity<UserAccount>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Username).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.Username).IsUnique();
            e.Property(x => x.DisplayName).HasMaxLength(200);
        });

        // AiUsageCounter
        modelBuilder.Entity<AiUsageCounter>(e =>
        {
            e.HasKey(x => x.CounterKey);
            e.Property(x => x.CounterKey).HasMaxLength(200).IsRequired();
            e.Property(x => x.RequestCount).IsRequired();
            e.HasIndex(x => x.LastRequestAt);
        });

        // ── Phase 2: Social Platform Entities ───────────────────────────────

        // SocialProposition
        modelBuilder.Entity<SocialProposition>(e =>
        {
            e.ToTable("SocialPropositions");
            e.HasKey(x => x.Id);
            // pgvector: column type set here; requires Npgsql.EntityFrameworkCore.PostgreSQL with vector support
            // When pgvector NuGet is added, change to HasColumnType("vector(1536)")
            e.Property(x => x.Embedding).HasColumnType("float4[]");
        });

        // SocialArgument
        modelBuilder.Entity<SocialArgument>(e =>
        {
            e.ToTable("SocialArguments");
            e.HasKey(x => x.Id);
            e.Property(x => x.Embedding).HasColumnType("float4[]");
            e.Property(x => x.Tags).HasColumnType("text[]");
            e.Property(x => x.SchwartzValues).HasColumnType("text[]");
            e.HasOne(x => x.ClaimProposition)
             .WithMany()
             .HasForeignKey(x => x.ClaimPropositionId)
             .OnDelete(DeleteBehavior.Restrict)
             .IsRequired(false);
            e.HasIndex(x => x.HotScore)
             .HasFilter("\"IsPublic\" = true AND \"IsShadowBanned\" = false")
             .HasDatabaseName("idx_socialarguments_hotscore");
            e.HasIndex(x => x.WilsonScore)
             .HasFilter("\"IsPublic\" = true AND \"IsShadowBanned\" = false")
             .HasDatabaseName("idx_socialarguments_wilsonscore");
            e.HasIndex(x => x.CreatedAt)
             .HasFilter("\"IsPublic\" = true AND \"IsShadowBanned\" = false")
             .HasDatabaseName("idx_socialarguments_createdat");
            // One social post per source Phase 1 argument (partial unique: ignores native posts).
            e.HasIndex(x => x.SourceArgumentId)
             .IsUnique()
             .HasFilter("\"SourceArgumentId\" IS NOT NULL")
             .HasDatabaseName("idx_socialarguments_sourceargumentid");
        });

        // SocialArgumentProposition (join table)
        modelBuilder.Entity<SocialArgumentProposition>(e =>
        {
            e.ToTable("SocialArgumentPropositions");
            e.HasKey(x => new { x.ArgumentId, x.PropositionId });
            e.HasOne(x => x.Argument)
             .WithMany(a => a.ArgumentPropositions)
             .HasForeignKey(x => x.ArgumentId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Proposition)
             .WithMany(p => p.ArgumentPropositions)
             .HasForeignKey(x => x.PropositionId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ArgumentLink
        modelBuilder.Entity<ArgumentLink>(e =>
        {
            e.ToTable("ArgumentLinks");
            e.HasKey(x => x.Id);
            e.Property(x => x.LinkType).HasConversion<string>();
            e.HasOne(x => x.SourceArgument)
             .WithMany(a => a.OutboundLinks)
             .HasForeignKey(x => x.SourceArgumentId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.TargetArgument)
             .WithMany(a => a.InboundLinks)
             .HasForeignKey(x => x.TargetArgumentId)
             .OnDelete(DeleteBehavior.Restrict);
            // Check constraint: no self-loops
            e.ToTable(t => t.HasCheckConstraint(
                "CK_ArgumentLinks_NoSelfLoop",
                "\"SourceArgumentId\" <> \"TargetArgumentId\""));
        });

        // ArgumentVote
        modelBuilder.Entity<ArgumentVote>(e =>
        {
            e.ToTable("ArgumentVotes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Vote).HasConversion<string>();
            e.Property(x => x.Rationale).HasConversion<string>();
            e.HasIndex(x => new { x.ArgumentId, x.UserId }).IsUnique();
            e.HasOne(x => x.Argument)
             .WithMany(a => a.Votes)
             .HasForeignKey(x => x.ArgumentId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ArgumentChain
        modelBuilder.Entity<ArgumentChain>(e =>
        {
            e.ToTable("ArgumentChains");
            e.HasKey(x => x.Id);
            e.Property(x => x.Tags).HasColumnType("text[]");
            e.Property(x => x.ArgumentIds).HasColumnType("uuid[]");
            e.Property(x => x.Embedding).HasColumnType("float4[]");
            e.HasOne(x => x.RootArgument)
             .WithMany()
             .HasForeignKey(x => x.RootArgumentId)
             .OnDelete(DeleteBehavior.Restrict)
             .IsRequired(false);
        });

        // Worldview
        modelBuilder.Entity<Worldview>(e =>
        {
            e.ToTable("Worldviews");
            e.HasKey(x => x.Id);
            e.Property(x => x.Tags).HasColumnType("text[]");
            e.Property(x => x.SchwartzValues).HasColumnType("text[]");
            e.Property(x => x.SchwartzVector).HasColumnType("float8[]");
            e.Property(x => x.Embedding).HasColumnType("float4[]");
        });

        // WorldviewChain (join table)
        modelBuilder.Entity<WorldviewChain>(e =>
        {
            e.ToTable("WorldviewChains");
            e.HasKey(x => new { x.WorldviewId, x.ArgumentChainId });
            e.HasOne(x => x.Worldview)
             .WithMany(w => w.WorldviewChains)
             .HasForeignKey(x => x.WorldviewId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ArgumentChain)
             .WithMany(c => c.WorldviewChains)
             .HasForeignKey(x => x.ArgumentChainId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // WorldviewVote
        modelBuilder.Entity<WorldviewVote>(e =>
        {
            e.ToTable("WorldviewVotes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Vote).HasConversion<string>();
            e.HasIndex(x => new { x.WorldviewId, x.UserId }).IsUnique();
            e.HasOne(x => x.Worldview)
             .WithMany(w => w.Votes)
             .HasForeignKey(x => x.WorldviewId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // DebateRoom
        modelBuilder.Entity<DebateRoom>(e =>
        {
            e.ToTable("DebateRooms");
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<string>();
            e.Property(x => x.Format).HasConversion<string>();
            e.Property(x => x.JudgeUserIds).HasColumnType("text[]");
            e.HasOne(x => x.MotionProposition)
             .WithMany()
             .HasForeignKey(x => x.MotionPropositionId)
             .OnDelete(DeleteBehavior.SetNull)
             .IsRequired(false);
        });

        // DebateContribution
        modelBuilder.Entity<DebateContribution>(e =>
        {
            e.ToTable("DebateContributions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Role).HasConversion<string>();
            e.HasOne(x => x.DebateRoom)
             .WithMany(d => d.Contributions)
             .HasForeignKey(x => x.DebateRoomId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Argument)
             .WithMany()
             .HasForeignKey(x => x.ArgumentId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // EpistemicProfile
        modelBuilder.Entity<EpistemicProfile>(e =>
        {
            e.ToTable("EpistemicProfiles");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.UserId, x.TopicDomain }).IsUnique();
        });

        // UserReputation
        modelBuilder.Entity<UserReputation>(e =>
        {
            e.ToTable("UserReputations");
            e.HasKey(x => x.Id);
            e.Property(x => x.Badges).HasColumnType("text[]");
            e.HasIndex(x => x.UserId).IsUnique();
        });

        // XPTransaction
        modelBuilder.Entity<XPTransaction>(e =>
        {
            e.ToTable("XPTransactions");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.UserId);
        });

        // Moderator
        modelBuilder.Entity<Moderator>(e =>
        {
            e.ToTable("Moderators");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.UserId, x.TopicDomain });
        });

        // ModerationFlag
        modelBuilder.Entity<ModerationFlag>(e =>
        {
            e.ToTable("ModerationFlags");
            e.HasKey(x => x.Id);
            e.Property(x => x.Reason).HasConversion<string>();
            e.Property(x => x.Status).HasConversion<string>();
            e.HasIndex(x => new { x.EntityType, x.EntityId });
            e.HasIndex(x => x.Status);
        });

        // ModerationAppeal
        modelBuilder.Entity<ModerationAppeal>(e =>
        {
            e.ToTable("ModerationAppeals");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.AppellantUserId);
        });

        // ── Badge System Entities ─────────────────────────────────────────────

        // ResolutionEndorsement
        modelBuilder.Entity<ResolutionEndorsement>(e =>
        {
            e.ToTable("ResolutionEndorsements");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ResolutionId, x.UserId }).IsUnique();
        });

        // BadgeAwardLog
        modelBuilder.Entity<BadgeAwardLog>(e =>
        {
            e.ToTable("BadgeAwardLogs");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => x.BadgeId);
        });

        // StructuralResolution
        modelBuilder.Entity<StructuralResolution>(e =>
        {
            e.ToTable("StructuralResolutions");
            e.HasKey(x => x.Id);
            e.Property(x => x.ResolutionText).IsRequired();
            e.HasIndex(x => x.AuthorId);
            e.HasMany(x => x.Endorsements)
             .WithOne()
             .HasForeignKey(x => x.ResolutionId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Phase 3: Understanding Graph Entities ─────────────────────────

        // UnderstandingNode
        modelBuilder.Entity<UnderstandingNode>(e =>
        {
            e.ToTable("UnderstandingNodes");
            e.HasKey(x => x.Id);
            e.Property(x => x.SemanticEmbedding).HasColumnType("float4[]");
            e.Property(x => x.GraphEmbedding).HasColumnType("float4[]");
            e.Property(x => x.SchwartzVector).HasColumnType("float8[]");
            e.Property(x => x.MoralFoundationsVector).HasColumnType("float8[]");
            e.HasIndex(x => x.NormalizedKey);
            e.HasIndex(x => x.Confidence);
            e.HasIndex(x => x.ControversyScore);
            e.HasMany(x => x.OutboundEdges)
             .WithOne(edge => edge.SourceNode)
             .HasForeignKey(edge => edge.SourceNodeId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.InboundEdges)
             .WithOne(edge => edge.TargetNode)
             .HasForeignKey(edge => edge.TargetNodeId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // UnderstandingEdge
        modelBuilder.Entity<UnderstandingEdge>(e =>
        {
            e.ToTable("UnderstandingEdges");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.SourceNodeId, x.TargetNodeId, x.Relationship });
            e.HasIndex(x => x.Weight);
            e.ToTable(t => t.HasCheckConstraint(
                "CK_UnderstandingEdges_NoSelfLoop",
                "\"SourceNodeId\" <> \"TargetNodeId\""));
        });

        // ConceptualSchema
        modelBuilder.Entity<ConceptualSchema>(e =>
        {
            e.ToTable("ConceptualSchemas");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Coherence);
            e.HasIndex(x => x.DiscoveryMethod);
        });

        // SchemaMembership (join table)
        modelBuilder.Entity<SchemaMembership>(e =>
        {
            e.ToTable("SchemaMemberships");
            e.HasKey(x => new { x.NodeId, x.SchemaId });
            e.HasOne(x => x.Node)
             .WithMany(n => n.SchemaMemberships)
             .HasForeignKey(x => x.NodeId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Schema)
             .WithMany(s => s.Memberships)
             .HasForeignKey(x => x.SchemaId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // DialecticalSynthesis
        modelBuilder.Entity<DialecticalSynthesis>(e =>
        {
            e.ToTable("DialecticalSyntheses");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.SynthesisNode)
             .WithMany()
             .HasForeignKey(x => x.SynthesisNodeId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.Depth);
        });

        // GraphSnapshot
        modelBuilder.Entity<GraphSnapshot>(e =>
        {
            e.ToTable("GraphSnapshots");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.CapturedAt);
        });
    }
}
