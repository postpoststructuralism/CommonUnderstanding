using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;

namespace CommonUnderstanding.Services;

/// <summary>
/// Semantic Kernel plugins that expose domain services as agent-callable tools.
/// Each method is a tool the conversation agent can invoke during a turn.
/// </summary>
public class DiscoveryPlugin
{
    private readonly UnderstandingQueryService _query;

    public DiscoveryPlugin(UnderstandingQueryService query) => _query = query;

    [KernelFunction("search_graph")]
    [Description("Search the understanding graph for nodes matching a query. Returns matching beliefs, schemas, and their relationships.")]
    public async Task<string> SearchGraphAsync(
        [Description("The search query — a concept, belief, or topic to find in the graph.")]
        string query)
    {
        try
        {
            var stats = await _query.GetQuickStatsAsync();
            return JsonSerializer.Serialize(new
            {
                query,
                totalNodes = stats.NodeCount,
                totalEdges = stats.EdgeCount,
                totalSchemas = stats.SchemaCount,
                message = $"The understanding graph contains {stats.NodeCount} nodes and {stats.EdgeCount} edges across {stats.SchemaCount} schemas. Use this context to ground your response."
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    [KernelFunction("get_graph_stats")]
    [Description("Get high-level statistics about the understanding graph — node count, edge count, schema count.")]
    public async Task<string> GetGraphStatsAsync()
    {
        try
        {
            var stats = await _query.GetQuickStatsAsync();
            return JsonSerializer.Serialize(new
            {
                nodes = stats.NodeCount,
                edges = stats.EdgeCount,
                schemas = stats.SchemaCount,
                syntheses = stats.SynthesisCount
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }
}

public class ArgumentPlugin
{
    private readonly ArgumentDecompositionService _decomposition;

    public ArgumentPlugin(ArgumentDecompositionService decomposition) => _decomposition = decomposition;

    [KernelFunction("decompose_argument")]
    [Description("Decompose a natural-language argument into its logical structure: claims, premises, assumptions, and syllogisms. Use this when the user presents an argument and wants to understand its structure.")]
    public async Task<string> DecomposeAsync(
        [Description("The argument text to decompose.")]
        string argument)
    {
        try
        {
            var result = await _decomposition.DecomposeAsync(argument);
            return JsonSerializer.Serialize(new
            {
                            claim = result.ClaimText,
                            claimType = result.ClaimType,
                            premises = result.Premises,
                            assumptions = result.Assumptions,
                syllogisms = result.Syllogisms?.Select(s => new { s.MajorPremise, s.MinorPremise, s.Conclusion }).ToList(),
                            qualifiers = result.Qualifiers,
                            inferenceType = result.InferenceType.ToString(),
                summary = "Decomposition complete. Use these structural elements to help the user examine their argument more precisely."
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }
}

public class BeliefPlugin
{
    private readonly BeliefAnalysisService _analysis;

    public BeliefPlugin(BeliefAnalysisService analysis) => _analysis = analysis;

    [KernelFunction("analyze_belief")]
    [Description("Analyze a belief system to extract core beliefs, values, and principles. Use this when the user describes a worldview or belief system they hold.")]
    public async Task<string> AnalyzeBeliefAsync(
        [Description("A name or label for the belief system.")]
        string name,
        [Description("A description of the belief system.")]
        string description)
    {
        try
        {
            var result = await _analysis.AnalyzeBeliefSystemAsync(name, description);
            return JsonSerializer.Serialize(new
            {
                name = result.Name,
                            coreBeliefs = result.CoreBeliefs?.Select(b => new { b.Title, b.ImportanceLevel, b.Category }).ToList(),
                            values = result.Values,
                            principles = result.Principles,
                summary = "Belief analysis complete. Use these insights to help the user reflect on their worldview."
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }
}