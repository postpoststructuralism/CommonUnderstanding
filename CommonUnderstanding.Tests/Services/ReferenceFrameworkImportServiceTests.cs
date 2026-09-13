using CommonUnderstanding.Services;

namespace CommonUnderstanding.Tests.Services;

public sealed class ReferenceFrameworkImportServiceTests
{
    [Fact]
    public void SplitIntoChunks_KeepsEveryChunkWithinDecompositionLimit()
    {
        var text = $"{new string('a', 2_000)}\n\n{new string('b', 2_000)}";

        var chunks = ReferenceFrameworkImportService.SplitIntoChunks(text);

        Assert.Equal(2, chunks.Count);
        Assert.All(chunks, chunk => Assert.InRange(chunk.Length, 1, 3_200));
        Assert.Equal(new string('a', 2_000), chunks[0]);
        Assert.Equal(new string('b', 2_000), chunks[1]);
    }

    [Fact]
    public void SplitIntoChunks_SplitsOversizedParagraphWithoutLosingText()
    {
        var text = new string('x', 7_001);

        var chunks = ReferenceFrameworkImportService.SplitIntoChunks(text);

        Assert.Equal([3_200, 3_200, 601], chunks.Select(chunk => chunk.Length));
        Assert.Equal(text, string.Concat(chunks));
    }

    [Fact]
    public void SplitIntoChunks_NormalizesLineEndingsAndHorizontalWhitespace()
    {
        const string text = "First   clause.\r\n\r\nSecond\tclause.";

        var chunks = ReferenceFrameworkImportService.SplitIntoChunks(text);

        Assert.Single(chunks);
        Assert.Equal($"First clause.{Environment.NewLine}{Environment.NewLine}Second clause.", chunks[0]);
    }

    [Fact]
    public void ReconcileOwnerIds_AcceptsImporterAsOnlyOwner()
    {
        var ownerIds = ReferenceFrameworkImportService.ReconcileOwnerIds(
            "importer-id",
            ["importer-id"],
            ["importer-id"]);

        Assert.Equal(["importer-id"], ownerIds);
    }

    [Fact]
    public void ReconcileOwnerIds_RejectsMissingImporterAccount()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ReferenceFrameworkImportService.ReconcileOwnerIds(
                "stale-importer-id",
                ["stale-importer-id"],
                []));

        Assert.Contains("signed-in account no longer exists", exception.Message);
    }

    [Fact]
    public void ReconcileOwnerIds_RejectsMissingAdditionalOwner()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ReferenceFrameworkImportService.ReconcileOwnerIds(
                "importer-id",
                ["importer-id", "stale-owner-id"],
                ["importer-id"]));

        Assert.Contains("selected additional owners no longer exist", exception.Message);
    }
}
