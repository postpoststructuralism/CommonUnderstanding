using System.Text.Json;
using CommonUnderstanding.Models;
using Microsoft.SemanticKernel;

namespace CommonUnderstanding.Services;

public sealed record GeneratedBaselineArgument(
    string Title,
    string Claim,
    string Warrant,
    string? Resolution,
    string[] Tags);

public sealed class BaselineContentGenerationService
{
    public const string PromptVersion = "belief-system-v1";

    private readonly SemanticKernelService _kernelService;
    private readonly ILogger<BaselineContentGenerationService> _logger;

    public BaselineContentGenerationService(
        SemanticKernelService kernelService,
        ILogger<BaselineContentGenerationService> logger)
    {
        _kernelService = kernelService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<GeneratedBaselineArgument>> GenerateAsync(
        CanonicalBeliefSystem beliefSystem,
        int count,
        CancellationToken cancellationToken)
    {
        var boundedCount = Math.Clamp(count, 1, 5);
        var result = await _kernelService.GetKernel().InvokePromptAsync(
            BuildPrompt(beliefSystem, boundedCount),
            cancellationToken: cancellationToken);

        var raw = StripCodeFence(result.ToString());
        try
        {
            var generated = JsonSerializer.Deserialize<List<GeneratedBaselineArgument>>(
                raw,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return (generated ?? [])
                .Where(IsUsable)
                .Take(boundedCount)
                .Select(Normalize)
                .ToList();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Baseline content generation returned malformed JSON for belief system {BeliefSystem}",
                beliefSystem.Slug);
            throw new InvalidOperationException(
                $"The AI response for belief system '{beliefSystem.Name}' was not valid JSON.",
                ex);
        }
    }

    private static string BuildPrompt(CanonicalBeliefSystem system, int count)
    {
        var principles = system.CorePrinciples.Count == 0
            ? "(none supplied)"
            : string.Join("; ", system.CorePrinciples.Take(12));
        var sources = system.Sources.Count == 0
            ? "(none supplied)"
            : string.Join("; ", system.Sources.Take(8));

        return $$"""
            Generate {{count}} distinct, natural arguments that people commonly encounter from or about the belief system below.

            Belief system: {{system.Name}}
            Category: {{system.Category}}
            Description: {{system.Description}}
            Core principles: {{principles}}
            Canonical sources: {{sources}}

            Requirements:
            - Represent the belief system charitably and accurately without impersonating a specific person.
            - Choose consequential claims that can be reviewed, debated, confirmed, or disconfirmed.
            - Make the reasoning self-contained. Do not invent quotations, citations, statistics, or historical events.
            - Vary the claims rather than paraphrasing one position.
            - Keep each title under 120 characters, claim under 300 characters, and warrant between 80 and 500 words.
            - A resolution is an optional practical implication or condition that would change the conclusion.
            - Include 2-5 short topic tags. Include "{{system.Slug}}" as one tag.

            Return only a JSON array with this exact shape:
            [{"title":"...","claim":"...","warrant":"...","resolution":"... or null","tags":["..."]}]
            """;
    }

    private static bool IsUsable(GeneratedBaselineArgument argument) =>
        !string.IsNullOrWhiteSpace(argument.Title) &&
        !string.IsNullOrWhiteSpace(argument.Claim) &&
        !string.IsNullOrWhiteSpace(argument.Warrant);

    private static GeneratedBaselineArgument Normalize(GeneratedBaselineArgument argument) =>
        argument with
        {
            Title = Truncate(argument.Title.Trim(), 300),
            Claim = Truncate(argument.Claim.Trim(), 300),
            Warrant = argument.Warrant.Trim(),
            Resolution = string.IsNullOrWhiteSpace(argument.Resolution)
                ? null
                : argument.Resolution.Trim(),
            Tags = (argument.Tags ?? [])
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag.Trim().ToLowerInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .ToArray()
        };

    private static string StripCodeFence(string value)
    {
        var trimmed = value.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
            return trimmed;

        var firstNewline = trimmed.IndexOf('\n');
        var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        return firstNewline >= 0 && lastFence > firstNewline
            ? trimmed[(firstNewline + 1)..lastFence].Trim()
            : trimmed;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}