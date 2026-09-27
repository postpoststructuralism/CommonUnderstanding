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
                                    "author": [{"given": "Ada", "family": "Example"}],
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
                Assert.Equal("Ada Example", record.Authors);
        }

    [Fact]
    public async Task SearchAsync_ParsesBookTitleAndAuthor()
    {
        const string responseJson = """
            {
                "message": {
                    "items": [{
                        "DOI": "10.1000/book",
                        "title": ["The Example Book"],
                        "author": [{"given": "Ada", "family": "Example"}],
                        "ISBN": ["9780123456786"],
                        "publisher": "Example Press",
                        "type": "book",
                        "published-print": {"date-parts": [[2020]]}
                    }]
                }
            }
            """;
        var provider = new CrossrefLiteratureProvider(
            new StubHttpClientFactory(responseJson),
            new ConfigurationBuilder().Build());

        var record = Assert.Single(await provider.SearchAsync("example book", null));

        Assert.Equal("The Example Book", record.Title);
        Assert.Equal("Ada Example", record.Authors);
        Assert.Equal(2020, record.PublicationYear);
        Assert.Equal("https://covers.openlibrary.org/b/isbn/9780123456786-M.jpg?default=false", record.CoverUrl);
    }

    [Fact]
    public async Task OpenLibrarySearch_ParsesBookCoverAndAuthor()
    {
        const string responseJson = """
            {"docs":[{"key":"/works/OL123W","title":"Example Book",
                "author_name":["Ada Example"],"first_publish_year":2020,
                "cover_i":12345,"publisher":["Example Press"]}]}
            """;
        var search = new OpenLibraryBookSearch(new StubHttpClientFactory(responseJson));

        var book = Assert.Single(await search.SearchAsync("Example Book"));

        Assert.Equal("Example Book", book.Title);
        Assert.Equal("Ada Example", book.Authors);
        Assert.Equal("https://openlibrary.org/works/OL123W", book.Uri);
        Assert.Equal("https://covers.openlibrary.org/b/id/12345-M.jpg?default=false", book.CoverUrl);
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