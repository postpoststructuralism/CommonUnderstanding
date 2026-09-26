using CommonUnderstanding.Models;
using CommonUnderstanding.Services;

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
        Assert.Equal("View", action.Action);
        Assert.Equal(10, Convert.ToInt32(action.RouteValues["id"]));
        Assert.Equal(30, Convert.ToInt32(action.RouteValues["propositionId"]));
        Assert.Equal("evidence", action.RouteValues["tab"]);
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