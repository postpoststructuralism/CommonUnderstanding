using CommonUnderstanding.Data;
using CommonUnderstanding.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CommonUnderstanding.Tests.Architecture;

public sealed class ProvenanceDeleteBehaviorTests
{
    [Fact]
    public void EvidenceSuggestion_EvidenceItemBacklink_UsesNoAction()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=ProvenanceModelTest;Trusted_Connection=True")
            .Options;
        using var db = new ApplicationDbContext(options, new DatabaseProviderInfo(isPostgres: false));

        var suggestion = db.Model.FindEntityType(typeof(EvidenceMatchSuggestion));
        var foreignKey = suggestion!.GetForeignKeys().Single(candidate =>
            candidate.PrincipalEntityType.ClrType == typeof(EvidenceItem));

        Assert.Equal(DeleteBehavior.NoAction, foreignKey.DeleteBehavior);
    }
}