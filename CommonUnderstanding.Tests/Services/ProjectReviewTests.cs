using CommonUnderstanding.Models;
using CommonUnderstanding.Services;
using CommonUnderstanding.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CommonUnderstanding.Tests.Services;

public class ProjectReviewTests
{
    [Fact]
    public async Task SourceCorrectionAndReviewRetainExactHistoryAndRejectStaleReview()
    {
        var factory = CreateFactory();
        var service = new ProjectReviewService(factory);
        await SeedAsync(factory);
        var project = await service.CreateAsync("owner", "Private review");
        await service.SetMemberAsync(project.Id, "owner", "editor", ProjectRole.Editor);
        await service.SetMemberAsync(project.Id, "owner", "reviewer", ProjectRole.Reviewer);
        var source = await service.AddSourceAsync(project.Id, "editor", "synthetic:v1", "Some residents prefer transit.");
        var input = new ClaimRevisionInput(source.Text, ClaimKind.Attributed, ClaimOrigin.Imported,
            "Some residents; present", "Synthetic survey", source.Id, 0, source.Text.Length, source.Text, "Extracted exact passage");
        var original = await service.SaveClaimAsync(project.Id, "editor", input);
        Assert.Equal(ReviewDisposition.Unreviewed, original.Disposition);
        await service.ReviewAsync(project.Id, original.RevisionId, "reviewer", ReviewDisposition.AcceptedWithObjections,
            "Passage verified", "Bounded population only", "Sampling objection retained", original.InputHash);
        var correction = await service.SaveClaimAsync(project.Id, "editor", input with { Text = "Some surveyed residents prefer transit.", Transformation = "Scope correction" },
            original.Id, original.RevisionId);
        Assert.Equal(ReviewDisposition.Unreviewed, correction.Disposition);
        Assert.True(correction.RequiresRevalidation);
        Assert.Equal(original.PassageText, correction.PassageText);
        var history = await service.ReadClaimsAsync(project.Id, "owner", true);
        Assert.Equal(2, history.Count);
        Assert.Equal(ReviewDisposition.AcceptedWithObjections, history.Single(item => item.Revision == 1).Disposition);
        Assert.Equal("reviewer", Assert.Single(history.Single(item => item.Revision == 1).Reviews).ReviewerId);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => service.ReviewAsync(project.Id, original.RevisionId,
            "reviewer", ReviewDisposition.Accepted, "Stale", "", "", original.InputHash));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => service.SaveClaimAsync(project.Id, "editor", input,
            original.Id, original.RevisionId));
    }

    [Fact]
    public async Task RolesAndCrossProjectSourcesFailClosedWithoutWrites()
    {
        var factory = CreateFactory();
        var service = new ProjectReviewService(factory);
        await SeedAsync(factory);
        var first = await service.CreateAsync("owner", "First");
        var second = await service.CreateAsync("outsider", "Second");
        await service.SetMemberAsync(first.Id, "owner", "reader", ProjectRole.Reader);
        await service.SetMemberAsync(first.Id, "owner", "editor", ProjectRole.Editor);
        var source = await service.AddSourceAsync(second.Id, "outsider", "private", "Other project text");
        var input = new ClaimRevisionInput("A normative claim", ClaimKind.Normative, ClaimOrigin.Authored, "Local", "Author");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ReadClaimsAsync(first.Id, "outsider"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SaveClaimAsync(first.Id, "reader", input));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ReadClaimsAsync(first.Id, "reader", operation: ProjectOperation.Export));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SetPolicyAsync(first.Id, "editor", "2"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveClaimAsync(first.Id, "editor", input with
            { SourceVersionId = source.Id, PassageStart = 0, PassageLength = source.Text.Length, PassageText = source.Text }));
        Assert.Empty(await service.ReadClaimsAsync(first.Id, "reader"));
        Assert.Single(await service.ListAsync("outsider"));
        var authored = await service.SaveClaimAsync(first.Id, "editor", input);
        Assert.Null(authored.SourceVersionId);
        Assert.Equal(ClaimKind.Normative, authored.Kind);
        await service.SetMemberAsync(first.Id, "owner", "reader", null);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ReadClaimsAsync(first.Id, "reader"));
        await service.DeleteAsync(first.Id, "owner");
        Assert.Single(await service.ListAsync("outsider"));
    }

    [Fact]
    public async Task MissingOwnerDeniesExistingMemberAndHidesProject()
    {
        var factory = CreateFactory();
        await SeedAsync(factory);
        var service = new ProjectReviewService(factory);
        var project = await service.CreateAsync("owner", "Legacy project");
        await service.SetMemberAsync(project.Id, "owner", "reader", ProjectRole.Reader);
        await using (var db = factory.CreateDbContext())
        {
            var owner = await db.UserAccounts.SingleAsync(user => user.Id == "owner");
            db.UserAccounts.Remove(owner);
            await db.SaveChangesAsync();
        }
        Assert.Empty(await service.ListAsync("reader"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ReadClaimsAsync(project.Id, "reader"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RoleAsync(project.Id, "reader"));
    }

    [Fact]
    public async Task PolicyAndSourceChangesInvalidateReviewWithoutRewritingHistory()
    {
        var factory = CreateFactory();
        await SeedAsync(factory);
        var service = new ProjectReviewService(factory);
        var project = await service.CreateAsync("owner", "Versioned review");
        var source = await service.AddSourceAsync(project.Id, "owner", "report", "Some residents prefer transit.");
        var claim = await service.SaveClaimAsync(project.Id, "owner", new(source.Text, ClaimKind.Attributed,
            ClaimOrigin.Imported, "Some residents", "Synthetic report", source.Id, 0, source.Text.Length, source.Text));
        await service.ReviewAsync(project.Id, claim.RevisionId, "owner", ReviewDisposition.Accepted, "Verified", "", "", claim.InputHash);
        await service.SetPolicyAsync(project.Id, "owner", "2");
        var changed = Assert.Single(await service.ReadClaimsAsync(project.Id, "owner"));
        Assert.Equal(ReviewDisposition.Unreviewed, changed.Disposition);
        Assert.True(changed.RequiresRevalidation);
        Assert.Equal("1", Assert.Single(changed.Reviews).PolicyVersion);
        await service.ReviewAsync(project.Id, claim.RevisionId, "owner", ReviewDisposition.Accepted, "Rechecked", "", "", claim.InputHash);
        await service.AddSourceAsync(project.Id, "owner", "report", source.Text, true);
        var retracted = Assert.Single(await service.ReadClaimsAsync(project.Id, "owner"));
        Assert.Equal(ReviewDisposition.Unreviewed, retracted.Disposition);
        Assert.Equal(2, retracted.Reviews.Count);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => service.ReviewAsync(project.Id, claim.RevisionId,
            "owner", ReviewDisposition.Accepted, "Stale source", "", "", claim.InputHash));
        var export = await service.ExportAsync(project.Id, "owner");
        Assert.Equal(2, export.Sources.Count);
        Assert.Equal(source.Text, Assert.Single(export.Claims).PassageText);
    }

    [Fact]
    public async Task HistoricalRecordsRejectMutationAndObjectionsRequireTreatment()
    {
        var factory = CreateFactory();
        await SeedAsync(factory);
        var service = new ProjectReviewService(factory);
        var project = await service.CreateAsync("owner", "Objections");
        var claim = await service.SaveClaimAsync(project.Id, "owner", new("Transit should be affordable.",
            ClaimKind.Normative, ClaimOrigin.Authored, "Local policy", "Author", Objections: "Budget constraint"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ReviewAsync(project.Id, claim.RevisionId,
            "owner", ReviewDisposition.Accepted, "Accepted", "", "", claim.InputHash));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ReviewAsync(project.Id, claim.RevisionId,
            "owner", ReviewDisposition.AcceptedWithObjections, "Accepted", "", "", claim.InputHash));
        await service.ReviewAsync(project.Id, claim.RevisionId, "owner", ReviewDisposition.AcceptedWithObjections,
            "Bounded acceptance", "Budget review required", "Retained pending budget review", claim.InputHash);
        await using var db = factory.CreateDbContext();
        var revision = await db.ProjectClaimRevisions.SingleAsync();
        db.Entry(revision).Property(item => item.DisplayText).CurrentValue = "Silently rewritten";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Equal(claim.Text, Assert.Single(await service.ReadClaimsAsync(project.Id, "owner")).Text);
    }

    [Fact]
    public async Task SupersededSourceSaveAndReopenAgreeWithoutCouplingCoverageToAcceptance()
    {
        var factory = CreateFactory();
        await SeedAsync(factory);
        var service = new ProjectReviewService(factory);
        var project = await service.CreateAsync("owner", "Independent dimensions");
        var source = await service.AddSourceAsync(project.Id, "owner", "report", "Some residents prefer transit.");
        await service.AddSourceAsync(project.Id, "owner", "report", "Some residents prefer walking.");
        var claim = await service.SaveClaimAsync(project.Id, "owner", new(source.Text, ClaimKind.Attributed,
            ClaimOrigin.Generated, "Some residents", "Report", source.Id, 0, source.Text.Length, source.Text,
            Coverage: EvidenceCoverage.Complete, Lifecycle: ClaimLifecycle.Withdrawn));
        Assert.True(claim.RequiresRevalidation);
        Assert.Equal(ReviewDisposition.Unreviewed, claim.Disposition);
        Assert.Equal(EvidenceCoverage.Complete, claim.Coverage);
        Assert.Equal(ClaimLifecycle.Withdrawn, claim.Lifecycle);
        Assert.Equal(ClaimOrigin.Generated, claim.Origin);
        var reopened = Assert.Single(await service.ReadClaimsAsync(project.Id, "owner"));
        Assert.Equal(claim, reopened with { Reviews = claim.Reviews });
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => service.ReviewAsync(project.Id,
            claim.RevisionId, "owner", ReviewDisposition.Accepted, "Stale source", "", "", claim.InputHash));
    }

    [Fact]
    public async Task LegacyImportPreservesFormerStatusWithoutInventingReviewOrSource()
    {
        var factory = CreateFactory();
        await SeedAsync(factory);
        await using (var db = factory.CreateDbContext())
        {
            db.Arguments.Add(new Argument { Id = 71, Title = "Legacy", RawText = "Original argument", SubmittedBy = "owner",
                Claims = { new CommonUnderstanding.Models.Claim { Id = 72, Text = "Legacy claim",
                    Premises = { new Proposition { Id = 73, Text = "Transit should be affordable.", Status = PropositionStatus.Settled } } } } });
            await db.SaveChangesAsync();
        }
        var service = new ProjectReviewService(factory);
        var project = await service.CreateAsync("owner", "Legacy import");
        var other = await service.CreateAsync("outsider", "Other");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ImportLegacyAsync(other.Id, "outsider", 73,
            ClaimKind.Normative, "Local", "Unknown"));
        var imported = await service.ImportLegacyAsync(project.Id, "owner", 73, ClaimKind.Normative, "Local", "Unknown");
        Assert.Equal("Settled", imported.FormerStatus);
        Assert.Equal(ReviewDisposition.Unreviewed, imported.Disposition);
        Assert.Equal(ClaimOrigin.Unknown, imported.Origin);
        Assert.Null(imported.SourceVersionId);
        Assert.Empty(imported.Reviews);
        Assert.Equal("Settled", Assert.Single((await service.ExportAsync(project.Id, "owner")).Claims).FormerStatus);
        await using var unchanged = factory.CreateDbContext();
        Assert.Equal(PropositionStatus.Settled, (await unchanged.Propositions.SingleAsync()).Status);
    }

    public static TheoryData<string, ClaimKind, string, string, string> SyntheticClaimsV1 => new()
    {
        { "Transit reduces emissions.", ClaimKind.Empirical, "Local; present", "Author", "Baseline empirical claim; no evidence supplied." },
        { "Transit reduces emissions.", ClaimKind.Empirical, "Local; present", "Author", "Duplicate wording does not transfer review." },
        { "Public transport lowers emissions.", ClaimKind.Empirical, "Local; present", "Author", "Paraphrase does not establish support or equivalence." },
        { "Transit does not reduce emissions.", ClaimKind.Empirical, "Local; present", "Author", "Negation must remain in display and matching text." },
        { "Transit reduced emissions in 2010.", ClaimKind.Empirical, "Local; 2010", "Author", "Historical scope cannot be silently generalized." },
        { "Some residents prefer transit.", ClaimKind.Empirical, "Some surveyed residents", "Author", "Population and quantifier remain bounded." },
        { "If fares were free, ridership might rise.", ClaimKind.Predictive, "Hypothetical free fares", "Author", "Hypothetical prediction is not an observed fact." },
        { "Transit should be affordable.", ClaimKind.Normative, "Local policy", "Author", "Normative claims are not coerced to empirical claims." },
        { "Ridership will rise next year.", ClaimKind.Predictive, "Local; next year", "Author", "Prediction remains distinct from present observation." },
        { "Transit reduces emissions and should be funded.", ClaimKind.Mixed, "Local policy", "Author", "Mixed factual and normative wording is preserved." },
        { "Most residents prefer transit.", ClaimKind.Empirical, "Most surveyed residents", "Author", "Most does not mean all or deductive certainty." },
        { "The mayor says transit is affordable.", ClaimKind.Attributed, "Statement only", "Mayor", "Attributed assertion does not become the author's assertion." },
        { "Transit reduces emissions in a city with clean electricity.", ClaimKind.Empirical, "Clean-electricity city", "Author", "Context-dependent wording cannot support an unbounded claim." },
        { "The summary repeats the same transit study.", ClaimKind.Attributed, "One underlying study", "Synthetic summary", "Dependent evidence is not a second independent source." },
        { "Report A cites report B, which cites report A.", ClaimKind.Attributed, "Circular citation chain", "Synthetic reports", "Circular citation is not independent support." },
        { "No participants have responded about transit.", ClaimKind.Empirical, "Zero responses", "Synthetic participation count", "Non-response cannot become agreement or acceptance." },
        { "Every resident prefers transit.", ClaimKind.Empirical, "All residents", "Author", "Universal quantification cannot be inferred from a bounded sample." }
    };

    [Theory]
    [MemberData(nameof(SyntheticClaimsV1))]
    public async Task SyntheticClaimsV1RetainExplainedDimensionsWithoutAutomaticAcceptance(string text, ClaimKind kind,
        string scope, string attribution, string reason)
    {
        Assert.False(string.IsNullOrWhiteSpace(reason));
        var factory = CreateFactory();
        await SeedAsync(factory);
        var service = new ProjectReviewService(factory);
        var project = await service.CreateAsync("owner", "Synthetic claims v1");
        var claim = await service.SaveClaimAsync(project.Id, "owner", new(text, kind, ClaimOrigin.Authored, scope, attribution));
        var reopened = Assert.Single(await service.ReadClaimsAsync(project.Id, "owner"));
        var exported = Assert.Single((await service.ExportAsync(project.Id, "owner")).Claims);
        Assert.Equal(claim, reopened with { Reviews = claim.Reviews });
        Assert.Equal(text, exported.Text);
        Assert.Equal(kind, exported.Kind);
        Assert.Equal(scope, exported.Scope);
        Assert.Equal(attribution, exported.Attribution);
        Assert.Equal(ReviewDisposition.Unreviewed, exported.Disposition);
        Assert.Equal(EvidenceCoverage.Unknown, exported.Coverage);
        Assert.Null(exported.SourceVersionId);
        Assert.Empty(exported.Reviews);
    }

    [LocalSqlServerFact]
    public async Task LocalSqlServerProjectSchemaSupportsIsolationDeletionAndRollback()
    {
        var database = $"CU_ProjectReviewTest_{Guid.NewGuid():N}";
        var factory = new TestFactory(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer($"Server=(localdb)\\MSSQLLocalDB;Database={database};Integrated Security=true;TrustServerCertificate=true").Options);
        await using var db = factory.CreateDbContext();
        try
        {
            await db.Database.EnsureCreatedAsync();
            await SeedAsync(factory);
            var service = new ProjectReviewService(factory);
            var project = await service.CreateAsync("owner", "Relational review");
            await service.SetMemberAsync(project.Id, "owner", "reader", ProjectRole.Reader);
            var original = await service.SaveClaimAsync(project.Id, "owner", new("Transit should be affordable.",
                ClaimKind.Normative, ClaimOrigin.Authored, "Local", "Author"));
            await service.SaveClaimAsync(project.Id, "owner", new("Transit should be affordable for residents.",
                ClaimKind.Normative, ClaimOrigin.Authored, "Residents", "Author"), original.Id, original.RevisionId);
            Assert.Single(await service.ListAsync("reader"));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ExportAsync(project.Id, "reader"));
            Assert.Equal(2, (await service.ExportAsync(project.Id, "owner")).Claims.Count);
            await service.DeleteAsync(project.Id, "owner");
            Assert.Empty(await service.ListAsync("reader"));
            var migration = new CommonUnderstanding.Migrations.AddPrivateProjectReview();
            var generator = db.GetService<IMigrationsSqlGenerator>();
            foreach (var command in generator.Generate(migration.DownOperations, db.Model))
                await db.Database.ExecuteSqlRawAsync(command.CommandText);
            foreach (var command in generator.Generate(migration.UpOperations, db.Model))
                await db.Database.ExecuteSqlRawAsync(command.CommandText);
            Assert.Empty(await db.ReviewProjects.AsNoTracking().ToListAsync());
            Assert.Equal(5, await db.UserAccounts.CountAsync());
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task NamedChoicesRespectMembershipAndContributionOwnership()
    {
        var factory = CreateFactory();
        await SeedAsync(factory);
        var service = new ProjectReviewService(factory);
        var project = await service.CreateAsync("owner", "Pickers");
        await service.SetMemberAsync(project.Id, "owner", "reader", ProjectRole.Reader);
        await service.AddSourceAsync(project.Id, "owner", "Survey", "Exact passage");
        await using (var db = factory.CreateDbContext())
        {
            db.Arguments.Add(new Argument { Title = "My contribution", RawText = "Original", SubmittedBy = "owner",
                Claims = { new CommonUnderstanding.Models.Claim { Text = "Claim",
                    Premises = { new Proposition { Text = "My premise" } } } } });
            await db.SaveChangesAsync();
        }
        var owner = await service.ChoicesAsync(project.Id, "owner", "read");
        Assert.Contains("@reader", Assert.Single(owner.People).Label);
        Assert.Contains("Reader", Assert.Single(owner.Members).Label);
        Assert.Single((await service.ChoicesAsync(project.Id, "owner")).Contributions);
        var reader = await service.ChoicesAsync(project.Id, "reader", "owner");
        Assert.Empty(reader.People);
        Assert.Empty(reader.Contributions);
        Assert.Empty(reader.Members);
        Assert.Equal("Survey", Assert.Single(reader.Sources).SourceKey);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ChoicesAsync(project.Id, "outsider"));
    }

    private static TestFactory CreateFactory() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task SeedAsync(TestFactory factory)
    {
        await using var db = factory.CreateDbContext();
        db.UserAccounts.AddRange(new[] { "owner", "editor", "reviewer", "reader", "outsider" }
            .Select(id => new UserAccount { Id = id, Username = id }));
        await db.SaveChangesAsync();
    }

    private sealed class TestFactory(DbContextOptions<ApplicationDbContext> options) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options, new DatabaseProviderInfo(false));
        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }

    public sealed class LocalSqlServerFactAttribute : FactAttribute
    {
        public LocalSqlServerFactAttribute()
        {
            if (!OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("CU_PROJECT_LOCAL_SQL_TESTS") != "1")
                Skip = "Set CU_PROJECT_LOCAL_SQL_TESTS=1 on Windows with MSSQLLocalDB to run the disposable SQL Server test.";
        }
    }

    [Fact]
    public void PermissionsAllowOnlySpecifiedOperations()
    {
        foreach (var role in Enum.GetValues<ProjectRole>())
        foreach (var operation in Enum.GetValues<ProjectOperation>())
        {
            var expected = role == ProjectRole.Owner || operation == ProjectOperation.Read
                || role == ProjectRole.Editor && operation is ProjectOperation.Contribute or ProjectOperation.Correct
                || role == ProjectRole.Reviewer && operation == ProjectOperation.Adjudicate;
            Assert.Equal(expected, ProjectPermissions.Allows(role, operation));
        }
        Assert.False(ProjectPermissions.Allows((ProjectRole)999, ProjectOperation.Read));
        Assert.False(ProjectPermissions.Allows(ProjectRole.Owner, (ProjectOperation)999));
    }
}