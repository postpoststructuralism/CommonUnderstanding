using CommonUnderstanding.Models;
using CommonUnderstanding.Models.Social;
using CommonUnderstanding.Services;
using CommonUnderstanding.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace CommonUnderstanding.Tests.Services;

public sealed class EmergentFindingActionServiceTests
{
    private readonly EmergentFindingActionService _service = new();

    [Theory]
    [InlineData(EmergentCategory.EvidenceDesert, EmergentFindingActionKind.AddEvidence, "Add evidence")]
    [InlineData(EmergentCategory.ConfidenceIllusion, EmergentFindingActionKind.ReviewEvidence, "Review evidence quality")]
    [InlineData(EmergentCategory.AssumptionCascade, EmergentFindingActionKind.TestAssumption, "Test assumption")]
    [InlineData(EmergentCategory.UnaddressedRebuttal, EmergentFindingActionKind.RespondToRebuttal, "Respond to rebuttal")]
    [InlineData(EmergentCategory.SilentContradiction, EmergentFindingActionKind.CompareClaims, "Compare claims")]
    [InlineData(EmergentCategory.ConvergentGround, EmergentFindingActionKind.StartFromCommonGround, "Start from common ground")]
    [InlineData(EmergentCategory.ComplementaryChains, EmergentFindingActionKind.BuildSynthesis, "Build a synthesis")]
    [InlineData(EmergentCategory.EmergentConsensus, EmergentFindingActionKind.ReviewConsensus, "Review consensus")]
    [InlineData(EmergentCategory.SharedValueCore, EmergentFindingActionKind.DraftBridgeArgument, "Draft bridge argument")]
    [InlineData(EmergentCategory.CrossDomainReinforcement, EmergentFindingActionKind.ConnectArguments, "Connect arguments")]
    public void GetPrimaryAction_MapsEveryCategory(
        EmergentCategory category,
        EmergentFindingActionKind expectedKind,
        string expectedLabel)
    {
        var finding = CreateFinding(category);

        var action = _service.GetPrimaryAction(finding, "/EmergentConclusions#finding");

        Assert.True(action.Enabled);
        Assert.Equal(expectedKind, action.Kind);
        Assert.Equal(expectedLabel, action.Label);
        Assert.Equal(finding.FindingKey, action.RouteValues["findingKey"]);
    }

    [Fact]
    public void GetPrimaryAction_DisablesEvidenceActionWithoutProposition()
    {
        var finding = CreateFinding(EmergentCategory.EvidenceDesert);
        finding.InvolvedPropositionIds.Clear();

        var action = _service.GetPrimaryAction(finding, "/EmergentConclusions");

        Assert.False(action.Enabled);
        Assert.Contains("argument and proposition", action.DisabledReason);
    }

    [Fact]
    public void GetPrimaryAction_TargetsEvidenceFormAndReturnLocation()
    {
        var finding = CreateFinding(EmergentCategory.EvidenceDesert);

        var action = _service.GetPrimaryAction(finding, "/EmergentConclusions#finding-key");

        Assert.Equal("Argument", action.Controller);
        Assert.Equal("AddEvidence", action.Action);
        Assert.Equal(10, Convert.ToInt32(action.RouteValues["argumentId"]));
        Assert.Equal(30, Convert.ToInt32(action.RouteValues["propositionId"]));
        Assert.False(action.RouteValues.ContainsKey("tab"));
        Assert.Equal("/EmergentConclusions#finding-key", action.RouteValues["returnUrl"]);
    }

    [Fact]
    public void GetPrimaryAction_PreselectsBothArgumentsForComparison()
    {
        var finding = CreateFinding(EmergentCategory.SilentContradiction);

        var action = _service.GetPrimaryAction(finding, "/EmergentConclusions#finding-key");

        Assert.Equal("Argument", action.Controller);
        Assert.Equal("Compare", action.Action);
        Assert.Equal(10, Convert.ToInt32(action.RouteValues["argumentAId"]));
        Assert.Equal(20, Convert.ToInt32(action.RouteValues["argumentBId"]));
        Assert.Equal("/EmergentConclusions#finding-key", action.RouteValues["returnUrl"]);
    }

    [Fact]
    public void FindingKey_IsStableAcrossEntityOrderingAndWhitespace()
    {
        var first = CreateFinding(EmergentCategory.SilentContradiction);
        first.Title = "Conflicting   claims";
        first.InvolvedArgumentIds = [20, 10];

        var second = CreateFinding(EmergentCategory.SilentContradiction);
        second.Title = " conflicting claims ";
        second.InvolvedArgumentIds = [10, 20];

        Assert.Equal(first.FindingKey, second.FindingKey);
    }

    [Fact]
    public void PublicNodes_ExcludesMixedAndUnknownArgumentProvenance()
    {
        var nodes = new[]
        {
            new CommonUnderstandingNode { Id = 1, ArgumentIdsJson = "[10,20]" },
            new CommonUnderstandingNode { Id = 2, Text = "Private contribution", ArgumentIdsJson = "[10,30]" },
            new CommonUnderstandingNode { Id = 3, ArgumentIdsJson = "[30]" },
            new CommonUnderstandingNode { Id = 4, ArgumentIdsJson = "[]" },
            new CommonUnderstandingNode { Id = 5, ArgumentIdsJson = "not json" }
        };

        var visible = CommunityReportScope.PublicNodes(nodes, new HashSet<int> { 10, 20 });

        Assert.Equal(1, Assert.Single(visible).Id);
        Assert.Equal(new[] { 10, 20 }, CommunityReportScope.ParsePublicIds(visible[0], new HashSet<int> { 10, 20 }));
    }

    [Fact]
    public void OpenGraphWork_DoesNotDoubleCountEvidenceDesertNodes()
    {
        var report = new EmergentConclusionsReport
        {
            GraphHealth = new GraphHealthSummary { NodesWithoutEvidence = 12, NodesWithoutEvidenceIds = [1] },
            Blindspots =
            [
                new EmergentConclusion { Category = EmergentCategory.EvidenceDesert, InvolvedNodeIds = [1] },
                new EmergentConclusion { Category = EmergentCategory.EvidenceDesert, InvolvedNodeIds = [1] },
                new EmergentConclusion { Category = EmergentCategory.UnaddressedRebuttal }
            ]
        };

        Assert.Equal(14, report.OpenGraphWorkCount);
        report.Blindspots.Add(new EmergentConclusion
        {
            Category = EmergentCategory.EvidenceDesert, InvolvedNodeIds = [2]
        });
        Assert.Equal(15, report.OpenGraphWorkCount);
        report.GraphHealth.NodesWithoutEvidence = 0;
        report.Blindspots.Clear();
        Assert.Equal(0, report.OpenGraphWorkCount);
    }

    [Fact]
    public async Task ComplementaryChains_IncludesPublicComparisonsWithoutSharedGraphNodes()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ApplicationDbContext(options, new DatabaseProviderInfo(isPostgres: false));
        db.Arguments.AddRange(
            new Argument { Id = 10, Title = "First" },
            new Argument { Id = 20, Title = "Second" });
        db.SocialArguments.AddRange(
            new SocialArgument { Title = "First", WarrantText = "Reason", UserId = "author", SourceArgumentId = 10, IsPublic = true },
            new SocialArgument { Title = "Second", WarrantText = "Reason", UserId = "author", SourceArgumentId = 20, IsPublic = true });
        db.ArgumentComparisons.Add(new ArgumentComparison
        {
            ArgumentAId = 10, ArgumentBId = 20,
            ComplementaryPremisesJson = "[\"Shared premise one\",\"Shared premise two\"]"
        });
        await db.SaveChangesAsync();

        var detector = new HarmonyDetector(db, null!, NullLogger<HarmonyDetector>.Instance);
        var findings = await detector.DetectAllAsync();

        Assert.Contains(findings, finding => finding.Category == EmergentCategory.ComplementaryChains &&
            finding.InvolvedArgumentIds.SequenceEqual(new[] { 10, 20 }));
    }

    [Fact]
    public async Task PersistedReports_RejectOldAndUnpublishedSnapshots()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ApplicationDbContext(options, new DatabaseProviderInfo(isPostgres: false));
        var publicPost = new SocialArgument
        {
            Title = "Published", WarrantText = "Reason", UserId = "author",
            SourceArgumentId = 10, IsPublic = true
        };
        db.SocialArguments.Add(publicPost);
        db.PersistedEmergentReports.AddRange(
            new PersistedEmergentReport { Id = 1, FullReportJson = "{}" },
            new PersistedEmergentReport
            {
                Id = 2,
                FullReportJson = JsonSerializer.Serialize(new EmergentConclusionsReport { PublicArgumentIds = [10] })
            },
            new PersistedEmergentReport
            {
                Id = 3,
                FullReportJson = JsonSerializer.Serialize(new EmergentConclusionsReport
                    { CommunityScopeVersion = 1, PublicArgumentIds = [10] })
            },
            new PersistedEmergentReport
            {
                Id = 4,
                FullReportJson = JsonSerializer.Serialize(new EmergentConclusionsReport
                {
                    PublicArgumentIds = [10],
                    GraphHealth = new GraphHealthSummary { NodesWithoutEvidence = 1, NodesWithoutEvidenceIds = [7] }
                })
            },
            new PersistedEmergentReport
            {
                Id = 5,
                FullReportJson = JsonSerializer.Serialize(new EmergentConclusionsReport
                {
                    PublicArgumentIds = [10],
                    GraphHealth = new GraphHealthSummary { NodesWithoutEvidence = 1, NodesWithoutEvidenceIds = [7] },
                    UncoveredEvidenceGaps = [new GraphEvidenceGap { NodeId = 7, Text = "Public claim" }]
                })
            });
        await db.SaveChangesAsync();

        var engine = new EmergentConclusionsEngine(db, null!, null!, null!,
            NullLogger<EmergentConclusionsEngine>.Instance);

        Assert.Null(await engine.LoadPersistedReportAsync(1));
        Assert.Null(await engine.LoadPersistedReportAsync(3));
        Assert.Null(await engine.LoadPersistedReportAsync(4));
        Assert.NotNull(await engine.LoadPersistedReportAsync(2));
        Assert.Equal(7, Assert.Single((await engine.LoadPersistedReportAsync(5))!.UncoveredEvidenceGaps).NodeId);
        Assert.Equal(new[] { 5, 2 }, (await engine.GetHistoryAsync()).Select(item => item.Id));

        publicPost.IsPublic = false;
        await db.SaveChangesAsync();

        Assert.Null(await engine.LoadPersistedReportAsync(2));
        Assert.Null(await engine.LoadLatestPersistedReportAsync());
        Assert.Empty(await engine.GetHistoryAsync());
    }

    private static EmergentConclusion CreateFinding(EmergentCategory category) => new()
    {
        Category = category,
        Title = "Finding title",
        InvolvedArgumentIds = [10, 20],
        InvolvedPropositionIds = [30, 40],
        InvolvedNodeIds = [50],
        InvolvedStakeholderIds = [60]
    };
}