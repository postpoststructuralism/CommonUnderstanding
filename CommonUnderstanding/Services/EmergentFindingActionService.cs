using CommonUnderstanding.Models;

namespace CommonUnderstanding.Services;

public enum EmergentFindingActionKind
{
    AddEvidence,
    ReviewEvidence,
    TestAssumption,
    RespondToRebuttal,
    CompareClaims,
    StartFromCommonGround,
    BuildSynthesis,
    ReviewConsensus,
    DraftBridgeArgument,
    ConnectArguments
}

public sealed record EmergentFindingActionDescriptor(
    EmergentFindingActionKind Kind,
    string Label,
    string Description,
    string Icon,
    string Controller,
    string Action,
    IReadOnlyDictionary<string, string> RouteValues,
    bool Enabled,
    string? DisabledReason = null);

public interface IEmergentFindingActionService
{
    EmergentFindingActionDescriptor GetPrimaryAction(EmergentConclusion finding, string returnUrl);
}

public sealed class EmergentFindingActionService : IEmergentFindingActionService
{
    public EmergentFindingActionDescriptor GetPrimaryAction(EmergentConclusion finding, string returnUrl)
    {
        ArgumentNullException.ThrowIfNull(finding);

        var context = new Dictionary<string, string>
        {
            ["findingKey"] = finding.FindingKey,
            ["returnUrl"] = returnUrl
        };

        return finding.Category switch
        {
            EmergentCategory.EvidenceDesert => ForProposition(
                finding, context, EmergentFindingActionKind.AddEvidence, "Add evidence",
                "Add evidence to the proposition that triggered this finding.", "bi-journal-plus"),
            EmergentCategory.ConfidenceIllusion => ForProposition(
                finding, context, EmergentFindingActionKind.ReviewEvidence, "Review evidence quality",
                "Review the proposition's current sources and add stronger evidence.", "bi-clipboard2-check"),
            EmergentCategory.AssumptionCascade => ForArgument(
                finding, context, EmergentFindingActionKind.TestAssumption, "Test assumption",
                "Open the affected argument and test its shared critical assumption.", "bi-layers"),
            EmergentCategory.UnaddressedRebuttal => ForArgument(
                finding, context, EmergentFindingActionKind.RespondToRebuttal, "Respond to rebuttal",
                "Open the affected argument and address the unhandled rebuttal.", "bi-reply"),
            EmergentCategory.SilentContradiction => ForComparison(
                finding, context, EmergentFindingActionKind.CompareClaims, "Compare claims",
                "Compare the arguments containing the conflicting claims.", "bi-arrow-left-right"),
            EmergentCategory.ConvergentGround => ForArgument(
                finding, context, EmergentFindingActionKind.StartFromCommonGround, "Start from common ground",
                "Open a related argument and use the shared premises as the starting point.", "bi-intersect"),
            EmergentCategory.ComplementaryChains => ForNodeOrArgument(
                finding, context, EmergentFindingActionKind.BuildSynthesis, "Build a synthesis",
                "Inspect the connected reasoning before drafting a synthesis.", "bi-link-45deg"),
            EmergentCategory.EmergentConsensus => ForNodeOrArgument(
                finding, context, EmergentFindingActionKind.ReviewConsensus, "Review consensus",
                "Inspect the proposition and stakeholder convergence behind this finding.", "bi-graph-up-arrow"),
            EmergentCategory.SharedValueCore => ForSubmission(
                context, EmergentFindingActionKind.DraftBridgeArgument, "Draft bridge argument",
                "Create an argument that connects stakeholders through the shared value.", "bi-signpost-split"),
            EmergentCategory.CrossDomainReinforcement => ForComparison(
                finding, context, EmergentFindingActionKind.ConnectArguments, "Connect arguments",
                "Compare the reinforcing arguments before making their relationship explicit.", "bi-diagram-3"),
            _ => Disabled(EmergentFindingActionKind.TestAssumption, "Action unavailable", "bi-slash-circle", context,
                "This finding category does not have a configured workflow.")
        };
    }

    private static EmergentFindingActionDescriptor ForProposition(
        EmergentConclusion finding,
        Dictionary<string, string> context,
        EmergentFindingActionKind kind,
        string label,
        string description,
        string icon)
    {
        var argumentId = finding.InvolvedArgumentIds.FirstOrDefault();
        var propositionId = finding.InvolvedPropositionIds.FirstOrDefault();
        if (argumentId <= 0 || propositionId <= 0)
        {
            return Disabled(kind, label, icon, context,
                "This finding does not identify both an argument and proposition.");
        }

        context["argumentId"] = argumentId.ToString();
        context["propositionId"] = propositionId.ToString();
        return Enabled(kind, label, description, icon, "Argument", "AddEvidence", context);
    }

    private static EmergentFindingActionDescriptor ForArgument(
        EmergentConclusion finding,
        Dictionary<string, string> context,
        EmergentFindingActionKind kind,
        string label,
        string description,
        string icon)
    {
        var argumentId = finding.InvolvedArgumentIds.FirstOrDefault();
        if (argumentId <= 0)
            return Disabled(kind, label, icon, context, "This finding does not identify an affected argument.");

        context["id"] = argumentId.ToString();
        return Enabled(kind, label, description, icon, "Argument", "View", context);
    }

    private static EmergentFindingActionDescriptor ForComparison(
        EmergentConclusion finding,
        Dictionary<string, string> context,
        EmergentFindingActionKind kind,
        string label,
        string description,
        string icon)
    {
        var argumentIds = finding.InvolvedArgumentIds.Distinct().Take(2).ToArray();
        if (argumentIds.Length < 2)
            return Disabled(kind, label, icon, context, "This finding does not identify two arguments to compare.");

        context["argumentAId"] = argumentIds[0].ToString();
        context["argumentBId"] = argumentIds[1].ToString();
        return Enabled(kind, label, description, icon, "Argument", "Compare", context);
    }

    private static EmergentFindingActionDescriptor ForNodeOrArgument(
        EmergentConclusion finding,
        Dictionary<string, string> context,
        EmergentFindingActionKind kind,
        string label,
        string description,
        string icon)
    {
        var nodeId = finding.InvolvedNodeIds.FirstOrDefault();
        if (nodeId > 0)
        {
            context["id"] = nodeId.ToString();
            return Enabled(kind, label, description, icon, "UnderstandingGraph", "Node", context);
        }

        return ForArgument(finding, context, kind, label, description, icon);
    }

    private static EmergentFindingActionDescriptor ForSubmission(
        Dictionary<string, string> context,
        EmergentFindingActionKind kind,
        string label,
        string description,
        string icon) =>
        Enabled(kind, label, description, icon, "Argument", "Submit", context);

    private static EmergentFindingActionDescriptor Enabled(
        EmergentFindingActionKind kind,
        string label,
        string description,
        string icon,
        string controller,
        string action,
        IReadOnlyDictionary<string, string> routeValues) =>
        new(kind, label, description, icon, controller, action, routeValues, true);

    private static EmergentFindingActionDescriptor Disabled(
        EmergentFindingActionKind kind,
        string label,
        string icon,
        IReadOnlyDictionary<string, string> routeValues,
        string reason) =>
        new(kind, label, reason, icon, string.Empty, string.Empty, routeValues, false, reason);
}