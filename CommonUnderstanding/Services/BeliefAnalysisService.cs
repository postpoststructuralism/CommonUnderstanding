using Microsoft.SemanticKernel;
using CommonUnderstanding.Models;
using System.Text.Json;

namespace CommonUnderstanding.Services;

/// <summary>
/// Service for analyzing and comparing belief systems using AI agents
/// </summary>
public class BeliefAnalysisService
{
    private static readonly TimeSpan AnalysisTimeout = TimeSpan.FromSeconds(20);

    private readonly SemanticKernelService _kernelService;
    private readonly ILogger<BeliefAnalysisService> _logger;

    public BeliefAnalysisService(
        SemanticKernelService kernelService,
        ILogger<BeliefAnalysisService> logger)
    {
        _kernelService = kernelService;
        _logger = logger;
    }

    /// <summary>
    /// Analyzes a belief system to extract and structure its core components
    /// </summary>
    public async Task<BeliefSystem> AnalyzeBeliefSystemAsync(string name, string description)
    {
        var kernel = _kernelService.GetKernel();

        var prompt = $$$"""
        Always respond in English.
        You are an expert at analyzing belief systems, philosophies, and worldviews.
        
        Analyze the following belief system and extract:
        1. Core beliefs and tenets (with categories like ethics, metaphysics, epistemology, etc.)
        2. Fundamental values
        3. Guiding principles
        
        Belief System Name: {{{name}}}
        Description: {{{description}}}
        
        Provide a structured analysis that identifies the most important beliefs, values, and principles.
        Rate the importance of each core belief on a scale of 1-10.
        Be objective and comprehensive.
        
        Format your response as a clear, structured analysis.
        """;

        try
        {
            var result = await InvokePromptWithTimeoutAsync(kernel, prompt, AnalysisTimeout);
            var analysisText = result.ToString();

            _logger.LogInformation("Completed analysis of belief system: {Name}", name);

            // For now, return a belief system with the AI's analysis in the description
            // In a more advanced version, we would parse the AI response into structured data
            return new BeliefSystem
            {
                Name = name,
                Description = description + "\n\nAI Analysis:\n" + analysisText
            };
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Belief system analysis timed out for {Name}; returning minimal fallback", name);
            return new BeliefSystem
            {
                Name = name,
                Description = description + "\n\nAI Analysis: Timed out. Showing original description only."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing belief system: {Name}", name);
            throw;
        }
    }

    /// <summary>
    /// Compares two belief systems to identify overlaps, divergences, and non-zero-sum opportunities
    /// </summary>
    public async Task<BeliefComparison> CompareBeliefSystemsAsync(
        BeliefSystem beliefSystem1, 
        BeliefSystem beliefSystem2)
    {
        var kernel = _kernelService.GetKernel();

        var prompt = $$$"""
        Always respond in English.
        You are an expert mediator and analyst of belief systems. Your goal is to find common ground
        and identify opportunities for mutual understanding and collaboration.
        
        Compare these two belief systems:
        
        BELIEF SYSTEM 1: {{{beliefSystem1.Name}}}
        {{{beliefSystem1.Description}}}
        
        BELIEF SYSTEM 2: {{{beliefSystem2.Name}}}
        {{{beliefSystem2.Description}}}
        
        Please provide a comprehensive analysis that includes:
        
        1. AREAS OF OVERLAP - Where do these belief systems share common ground?
           - Identify shared values, principles, and goals
           - Rate the strength of each overlap (1-10)
        
        2. AREAS OF DIVERGENCE - Where do they differ?
           - Identify key differences
           - Distinguish between fundamental vs. superficial differences
           - Suggest potential bridge ideas that could help mutual understanding
        
        3. NON-ZERO-SUM OPPORTUNITIES - Where could both benefit from collaboration?
           - Identify concrete opportunities where both sides could gain
           - Explain specific benefits to each belief system
           - Suggest actionable steps
        
        4. SYNTHESIS SUMMARY - What binds these belief systems together?
           - Focus on shared humanity, values, and aspirations
           - Calculate an overall overlap score (0-100)
        
        Be objective, respectful, and focus on building bridges rather than highlighting divisions.
        The goal is to emphasize what unites us while acknowledging honest differences.
        """;

        try
        {
            var result = await InvokePromptWithTimeoutAsync(kernel, prompt, AnalysisTimeout);
            var analysisText = result.ToString();

            _logger.LogInformation("Completed comparison of {BS1} and {BS2}", 
                beliefSystem1.Name, beliefSystem2.Name);

            // For now, store the raw analysis - in production, we'd parse this into structured data
            return new BeliefComparison
            {
                BeliefSystem1Id = beliefSystem1.Id,
                BeliefSystem2Id = beliefSystem2.Id,
                BeliefSystem1Name = beliefSystem1.Name,
                BeliefSystem2Name = beliefSystem2.Name,
                SynthesisSummary = analysisText,
                OverlapScore = 0 // Would be extracted from AI response in production
            };
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Belief system comparison timed out for {BS1} and {BS2}", beliefSystem1.Name, beliefSystem2.Name);
            return new BeliefComparison
            {
                BeliefSystem1Id = beliefSystem1.Id,
                BeliefSystem2Id = beliefSystem2.Id,
                BeliefSystem1Name = beliefSystem1.Name,
                BeliefSystem2Name = beliefSystem2.Name,
                SynthesisSummary = "Comparison timed out. Review the two belief systems manually for shared themes and differences.",
                OverlapScore = 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error comparing belief systems");
            throw;
        }
    }

    /// <summary>
    /// Generates suggestions for dialogue and mutual understanding
    /// </summary>
    public async Task<string> GenerateDialogueSuggestionsAsync(BeliefComparison comparison)
    {
        var kernel = _kernelService.GetKernel();

        var prompt = $$$"""
        Always respond in English.
        Based on this comparison of {{{comparison.BeliefSystem1Name}}} and {{{comparison.BeliefSystem2Name}}}:
        
        {{{comparison.SynthesisSummary}}}
        
        Generate practical suggestions for constructive dialogue between adherents of these belief systems:
        1. Conversation starters that emphasize common ground
        2. Questions that promote mutual understanding
        3. Activities or projects they could collaborate on
        4. Frameworks for discussing differences respectfully
        
        Focus on building bridges and finding win-win scenarios.
        """;

        try
        {
            var result = await InvokePromptWithTimeoutAsync(kernel, prompt, AnalysisTimeout);
            return result.ToString();
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Dialogue suggestion generation timed out for {BS1} and {BS2}", comparison.BeliefSystem1Name, comparison.BeliefSystem2Name);
            return "Dialogue suggestions timed out. Start by identifying one shared value, one respectful question, and one small collaborative activity both sides can support.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating dialogue suggestions");
            throw;
        }
    }

    private async Task<FunctionResult> InvokePromptWithTimeoutAsync(Kernel kernel, string prompt, TimeSpan timeout)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);
        try
        {
            return await kernel.InvokePromptAsync(prompt, cancellationToken: timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Belief analysis prompt timed out after {Seconds}s", timeout.TotalSeconds);
            throw new TimeoutException($"Belief analysis timed out after {timeout.TotalSeconds} seconds.");
        }
    }
}
