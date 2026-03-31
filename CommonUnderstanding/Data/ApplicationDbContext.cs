using Microsoft.EntityFrameworkCore;
using CommonUnderstanding.Models;

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

    // Phase 3 — Common Understanding Graph
    public DbSet<CommonUnderstandingNode> CommonUnderstandingNodes => Set<CommonUnderstandingNode>();
    public DbSet<CommonUnderstandingEdge> CommonUnderstandingEdges => Set<CommonUnderstandingEdge>();

    // Phase 4 — Stakeholders
    public DbSet<Stakeholder> Stakeholders => Set<Stakeholder>();
    public DbSet<StakeholderPosition> StakeholderPositions => Set<StakeholderPosition>();

    // Phase 5 — Comparative Analysis
    public DbSet<ArgumentComparison> ArgumentComparisons => Set<ArgumentComparison>();

    // Emergent Conclusions — historical snapshots
    public DbSet<PersistedEmergentReport> PersistedEmergentReports => Set<PersistedEmergentReport>();

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
    }
}
