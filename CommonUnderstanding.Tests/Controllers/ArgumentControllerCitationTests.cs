using System.Security.Claims;
using System.Text.Json;
using CommonUnderstanding.Controllers;
using CommonUnderstanding.Data;
using CommonUnderstanding.Models;
using CommonUnderstanding.Services.Provenance;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace CommonUnderstanding.Tests.Controllers;

public sealed class ArgumentControllerCitationTests
{
    [Fact]
    public void DetermineVerificationStatus_NullRelation_ReturnsVerified()
    {
        using var document = JsonDocument.Parse("""{"type":"journal-article","relation":null}""");

        var status = CrossrefLiteratureProvider.DetermineVerificationStatus(
            document.RootElement,
            "journal-article");

        Assert.Equal(SourceVerificationStatus.Verified, status);
    }

    [Fact]
    public async Task SuggestEvidence_EmptyCorpus_RefreshesBeforeMatchingAndReportsSuccess()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new ApplicationDbContext(
            options,
            new DatabaseProviderInfo(isPostgres: false));

        db.Arguments.Add(new Argument
        {
            Id = 133,
            Title = "Test argument",
            RawText = "Test argument",
            SubmittedBy = "owner",
            Claims =
            {
                new CommonUnderstanding.Models.Claim
                {
                    Id = 12,
                    Text = "Test claim",
                    Premises =
                    {
                        new Proposition { Id = 50, Text = "Test proposition" }
                    }
                }
            }
        });
        await db.SaveChangesAsync();

        var corpus = new RecordingCorpusService();
        var matching = new RecordingMatchingService(result: 2);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Provenance:Enabled"] = "true",
                ["Provenance:TrackedTopics:0"] = "test topic"
            })
            .Build();
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new System.Security.Claims.Claim(ClaimTypes.NameIdentifier, "owner")],
                "Test"))
        };
        var controller = new ArgumentController(
            db,
            null!,
            null!,
            null!,
            null!,
            null!,
            matching,
            corpus,
            configuration,
            null!,
            null!,
            null!,
            null!,
            NullLogger<ArgumentController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new InMemoryTempDataProvider())
        };

        var result = await controller.SuggestEvidence(argumentId: 133, propositionId: 50);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ArgumentController.View), redirect.ActionName);
        Assert.Equal(1, corpus.RefreshCalls);
        Assert.Equal(50, matching.PropositionId);
        Assert.Equal(
            "Found 2 citation suggestions. Review them under the premise below.",
            controller.TempData["CitationInfo"]);
    }

    private sealed class RecordingCorpusService : ILiteratureCorpusService
    {
        public int RefreshCalls { get; private set; }

        public Task<int> RefreshAsync(CancellationToken cancellationToken = default)
        {
            RefreshCalls++;
            return Task.FromResult(1);
        }
    }

    private sealed class RecordingMatchingService(int result) : IEvidenceMatchingService
    {
        public int? PropositionId { get; private set; }

        public Task<int> SuggestForPropositionAsync(
            int propositionId,
            CancellationToken cancellationToken = default)
        {
            PropositionId = propositionId;
            return Task.FromResult(result);
        }

        public Task<int?> ReviewAsync(
            long suggestionId,
            bool confirm,
            string? reviewedBy,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class InMemoryTempDataProvider : ITempDataProvider
    {
        private Dictionary<string, object> _values = [];

        public IDictionary<string, object> LoadTempData(HttpContext context) => _values;

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
            _values = new Dictionary<string, object>(values);
        }
    }
}