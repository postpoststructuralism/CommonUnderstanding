using System.Text.Json;
using CommonUnderstanding.Data;
using CommonUnderstanding.Models;
using CommonUnderstanding.Services.Provenance;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace CommonUnderstanding.Tests.Services;

public sealed class CrossrefLiteratureProviderTests
{
        [Fact]
        public async Task SearchAsync_ParsesMemberIdentifier_WhenCrossrefReturnsString()
        {
                const string responseJson = """
                        {
                            "message": {
                                "items": [{
                                    "DOI": "10.1000/example",
                                    "title": ["Example study"],
                                    "publisher": "Example Publisher",
                                    "member": "1234",
                                    "type": "journal-article",
                                    "published-print": {"date-parts": [[2024, 6, 1]]}
                                }]
                            }
                        }
                        """;
                var provider = new CrossrefLiteratureProvider(
                        new StubHttpClientFactory(responseJson),
                        new ConfigurationBuilder().Build());

                var records = await provider.SearchAsync("example", null);

                var record = Assert.Single(records);
                Assert.Equal("crossref:1234", record.PublisherIdentifier);
        }

    [Fact]
    public void DetermineVerificationStatus_ReturnsRetracted_ForRetractionType()
    {
        using var document = JsonDocument.Parse("{} ");

        var status = CrossrefLiteratureProvider.DetermineVerificationStatus(
            document.RootElement,
            "retraction");

        Assert.Equal(SourceVerificationStatus.Retracted, status);
    }

    [Fact]
    public void DetermineVerificationStatus_ReturnsCorrectionIssued_ForCorrectionRelation()
    {
        using var document = JsonDocument.Parse("""
            {"relation":{"is-correction-of":[{"id":"10.1000/example"}]}}
            """);

        var status = CrossrefLiteratureProvider.DetermineVerificationStatus(
            document.RootElement,
            "journal-article");

        Assert.Equal(SourceVerificationStatus.CorrectionIssued, status);
    }

    [Fact]
    public void DetermineVerificationStatus_ReturnsVerified_ForOrdinaryWork()
    {
        using var document = JsonDocument.Parse("{} ");

        var status = CrossrefLiteratureProvider.DetermineVerificationStatus(
            document.RootElement,
            "journal-article");

        Assert.Equal(SourceVerificationStatus.Verified, status);
    }

    [Fact]
    public async Task RefreshAsync_ProviderFailure_ReturnsZeroAndLeavesCorpusEmpty()
    {
        await using var db = CreateDbContext();
        var service = CreateCorpusService(db, new ThrowingLiteratureProvider(new HttpRequestException("Crossref unavailable")));

        var changed = await service.RefreshAsync();

        Assert.Equal(0, changed);
        Assert.Empty(await db.EvidenceCorpusEntries.ToListAsync());
    }

    [Fact]
    public async Task RefreshAsync_RequestCancellation_Propagates()
    {
        await using var db = CreateDbContext();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var service = CreateCorpusService(db, new ThrowingLiteratureProvider(new OperationCanceledException(cancellation.Token)));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.RefreshAsync(cancellation.Token));
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options, new DatabaseProviderInfo(isPostgres: false));
    }

    private static LiteratureCorpusService CreateCorpusService(
        ApplicationDbContext db,
        ILiteratureProvider provider)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Provenance:Enabled"] = "true",
                ["Provenance:TrackedTopics:0"] = "test topic"
            })
            .Build();

        return new LiteratureCorpusService(
            db,
            [provider],
            null!,
            null!,
            configuration,
            NullLogger<LiteratureCorpusService>.Instance);
    }

    private sealed class StubHttpClientFactory(string responseJson) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new StubHttpMessageHandler(responseJson));
    }

    private sealed class StubHttpMessageHandler(string responseJson) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson)
            });
    }

    private sealed class ThrowingLiteratureProvider(Exception exception) : ILiteratureProvider
    {
        public Task<IReadOnlyList<LiteratureRecord>> SearchAsync(
            string topic,
            DateTime? updatedSince,
            CancellationToken cancellationToken = default) => Task.FromException<IReadOnlyList<LiteratureRecord>>(exception);
    }
}