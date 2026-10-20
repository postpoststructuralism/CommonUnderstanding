using CommonUnderstanding.Data;
using CommonUnderstanding.Models;
using CommonUnderstanding.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CommonUnderstanding.Tests.Services;

public class TensorConstructionServiceTests
{
    [Theory]
    [InlineData("semantic_similarity")]
    [InlineData("co-occurrence")]
    public async Task BuildTensorAsync_AutomaticallyDetectedRelationsDoNotChangeTensorValues(string detectedBy)
    {
        var withoutDetectedRelation = await BuildTensorWithDetectedRelationAsync(includeRelation: false, detectedBy: detectedBy);
        var withDetectedRelation = await BuildTensorWithDetectedRelationAsync(includeRelation: true, detectedBy: detectedBy);

        Assert.NotEmpty(withoutDetectedRelation.Entries);
        Assert.NotEmpty(withDetectedRelation.Entries);
        Assert.Equal(withoutDetectedRelation.Entries, withDetectedRelation.Entries);
    }

    [Fact]
    public async Task AutomaticallyDetectedRelationCannotIntroduceTensorEntries()
    {
        var baseline = await BuildTensorWithDetectedRelationAsync(false, "semantic_similarity", false);
        var proposed = await BuildTensorWithDetectedRelationAsync(true, "semantic_similarity", false);

        Assert.Empty(baseline.Entries);
        Assert.Equal(baseline.Entries, proposed.Entries);
    }

    [Fact]
    public async Task BuildTensorAsync_EvidenceDirectionRelationsContinueToAffectTensorValues()
    {
        var withoutRelation = await BuildTensorWithDetectedRelationAsync(includeRelation: false, detectedBy: "evidence_direction");
        var withRelation = await BuildTensorWithDetectedRelationAsync(includeRelation: true, detectedBy: "evidence_direction");

        Assert.NotEqual(withoutRelation.Entries, withRelation.Entries);
        Assert.All(withRelation.Entries, entry => Assert.Equal(0.9, entry.Value, precision: 5));
    }

    private static async Task<SparseTensor> BuildTensorWithDetectedRelationAsync(bool includeRelation, string detectedBy,
        bool includeBaseline = true)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var factory = new TestDbContextFactory(options);

        await using (var db = await factory.CreateDbContextAsync())
        {
            db.Arguments.Add(new Argument { Id = 41, SubmittedBy = "owner" });
            db.UnderstandingNodes.AddRange(
                new UnderstandingNode
                {
                    Id = 1,
                    CanonicalText = "First proposition",
                    Confidence = 0.5,
                    ArgumentIdsJson = "[41]"
                },
                new UnderstandingNode
                {
                    Id = 2,
                    CanonicalText = "Second proposition",
                    Confidence = 0.5,
                    ArgumentIdsJson = "[41]"
                });

            if (includeBaseline) db.UnderstandingEdges.Add(new UnderstandingEdge
            {
                Id = 1,
                SourceNodeId = 1,
                TargetNodeId = 2,
                Relationship = "analogous",
                Weight = 0.5,
                BaseWeight = 0.5,
                ProvenanceJson = "{\"argumentId\":\"41\"}"
            });

            if (includeRelation)
            {
                db.UnderstandingEdges.Add(new UnderstandingEdge
                {
                    Id = 2,
                    SourceNodeId = 1,
                    TargetNodeId = 2,
                    Relationship = "supports",
                    Weight = 0.8,
                    BaseWeight = 0.8,
                    ProvenanceJson = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        detectedBy,
                        sharedArgumentIds = new[] { 41 }
                    })
                });
            }

            await db.SaveChangesAsync();
        }

        var service = new TensorConstructionService(factory, NullLogger<TensorConstructionService>.Instance);
        return await service.BuildTensorAsync();
    }

    private sealed class TestDbContextFactory(DbContextOptions<ApplicationDbContext> options)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() =>
            new(options, new DatabaseProviderInfo(isPostgres: false));

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
