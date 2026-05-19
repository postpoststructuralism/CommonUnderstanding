using Microsoft.SemanticKernel;
using CommonUnderstanding.Models;
using System.Text.RegularExpressions;

namespace CommonUnderstanding.Services;

/// <summary>
/// Decomposes a natural-language argument into formal logical structure using
/// a multi-step Semantic Kernel prompt chain (Toulmin model + syllogistic form).
/// </summary>
public class ArgumentDecompositionService
{
    private static readonly TimeSpan ClaimTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ParallelPromptTimeout = TimeSpan.FromSeconds(20);

    private readonly SemanticKernelService _kernelService;
    private readonly ILogger<ArgumentDecompositionService> _logger;

    public ArgumentDecompositionService(
        SemanticKernelService kernelService,
        ILogger<ArgumentDecompositionService> logger)
    {
        _kernelService = kernelService;
        _logger = logger;
    }

    /// <summary>
    /// Decomposes an argument into claims, premises, syllogisms, assumptions,
    /// qualifiers and rebuttals.
    /// </summary>
    public async Task<DecompositionResult> DecomposeAsync(
        string argumentText,
        Func<string, int, int, Task>? onProgress = null,
        Func<string, object?, Task>? onDebug = null,
        CancellationToken cancellationToken = default)
    {
        // ── Pre-processing: normalise text before any AI call ─────────────────
        argumentText = PreprocessText(argumentText);

        if (onDebug != null)
        {
            await onDebug("Decomposition text preprocessed", new
            {
                length = argumentText.Length,
                preview = argumentText.Length > 160 ? argumentText[..160] + "..." : argumentText
            });
        }

        _logger.LogInformation("Beginning argument decomposition ({Length} chars)", argumentText.Length);

        var kernel = _kernelService.GetKernel();

        // ── Step 1: Extract the central claim ────────────────────────────────
        // Try heuristics first (free); fall back to an LLM call only when needed.
        if (onProgress != null) await onProgress("Extracting central claim…", 1, 2);
        var claimStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var heuristicClaim = TryExtractClaimHeuristically(argumentText);
        if (onDebug != null)
            await onDebug("Claim extraction strategy selected", new { usedHeuristic = heuristicClaim is not null });

        var claimText = heuristicClaim
            ?? await InvokePromptWithTimeoutAsync(
                operationName: "claim extraction",
                timeout: ClaimTimeout,
                promptAction: promptCt => ExtractClaimAsync(kernel, argumentText, promptCt),
                fallbackValue: DeriveFallbackClaim(argumentText),
                onDebug: onDebug,
                cancellationToken: cancellationToken);
        claimStopwatch.Stop();

        if (onDebug != null)
        {
            await onDebug("Claim extraction completed", new
            {
                elapsedMs = claimStopwatch.ElapsedMilliseconds,
                claimLength = claimText.Length,
                claimPreview = claimText.Length > 160 ? claimText[..160] + "..." : claimText
            });
        }

        // ── Step 2: Parallel structural decomposition ─────────────────────────
        // Two focused prompts run concurrently on different providers (round-robin).
        if (onProgress != null) await onProgress("Decomposing argument structure & assessing premises…", 2, 2);
        var (premisesRaw, structureRaw) = await ParallelDecompositionAsync(
            kernel,
            argumentText,
            claimText,
            onDebug,
            cancellationToken);

        // ── Parse ────────────────────────────────────────────────────────────
        var result = ParseFullDecomposition(claimText, premisesRaw + "\n" + structureRaw);

        _logger.LogInformation(
            "Decomposition complete: {Premises} premises, {Syllogisms} syllogisms, {Assumptions} assumptions, {Assessments} assessments",
            result.Premises.Count, result.Syllogisms.Count, result.Assumptions.Count, result.ProvisionalAssessments.Count);

        return result;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Pre-AI processing helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Normalises argument text before sending it to any AI provider:
    /// collapses whitespace, strips HTML artefacts, and hard-caps length.
    /// </summary>
    private static string PreprocessText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        // Normalise line endings, then collapse runs of blank lines
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        // Collapse horizontal whitespace runs (but preserve single newlines)
        text = Regex.Replace(text, @"[ \t]{2,}", " ");

        // Strip HTML tags if the text came from a rich-text editor
        text = Regex.Replace(text, @"<[^>]{0,200}>", " ");
        text = Regex.Replace(text, @"&amp;",  "&")
                    .Replace("&lt;",   "<")
                    .Replace("&gt;",   ">")
                    .Replace("&quot;", "\"")
                    .Replace("&#39;",  "'")
                    .Replace("&nbsp;", " ");
        // Re-collapse any whitespace introduced by tag removal
        text = Regex.Replace(text, @"[ \t]{2,}", " ");

        // Hard cap: ~3 500 chars keeps most models well inside their input window.
        // Truncate at the nearest sentence boundary to avoid cutting mid-thought.
        const int MaxChars = 3_500;
        if (text.Length > MaxChars)
        {
            var boundary = text.LastIndexOfAny(['.', '!', '?'], MaxChars);
            text = boundary > MaxChars - 500
                ? text[..(boundary + 1)] + "\n[…argument truncated for analysis]"
                : text[..MaxChars]       + "\n[…argument truncated for analysis]";
        }

        return text.Trim();
    }

    /// <summary>
    /// Returns the central claim without an LLM call when the text structure
    /// makes it unambiguous.  Returns <c>null</c> when a model is needed.
    /// </summary>
    private static string? TryExtractClaimHeuristically(string text)
    {
        var sentences = SplitIntoSentences(text);
        if (sentences.Count == 0) return null;

        // Single sentence or two-sentence arguments: the last sentence is the claim.
        if (sentences.Count <= 2) return sentences[^1].Trim();

        // Explicit conclusion markers at the start of a sentence.
        ReadOnlySpan<string> markers =
        [
            "therefore", "thus,", "hence,", "hence ", "consequently,", "consequently ",
            "in conclusion,", "in summary,", "to conclude,", "it follows that",
            "this proves", "this shows", "this means", "as a result,",
            "my claim is", "the conclusion is", "i argue that", "i contend that",
            "i submit that", "in short,", "to summarise,", "to summarize,"
        ];

        foreach (var sentence in sentences)
        {
            var lower = sentence.TrimStart().ToLowerInvariant();
            foreach (var marker in markers)
                if (lower.StartsWith(marker, StringComparison.Ordinal))
                    return sentence.Trim();
        }

        return null; // Structure is ambiguous — let the LLM decide
    }

    private static List<string> SplitIntoSentences(string text)
    {
        // Split on terminal punctuation followed by whitespace.
        var parts = Regex.Split(text.Trim(), @"(?<=[.!?])\s+");
        // Filter out very short fragments (abbreviations, initials, etc.)
        return [.. parts.Where(s => s.Trim().Length > 12)];
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  LLM calls
    // ─────────────────────────────────────────────────────────────────────────

    private async Task<string> ExtractClaimAsync(Kernel kernel, string argumentText, CancellationToken ct)
    {
        var prompt = $$$"""
        Always respond in English.
        You are an expert in argument analysis and formal logic.

        Read the following argument and identify the SINGLE central claim it is making.
        The claim is the top-level conclusion the author wants the reader to accept.

        Argument:
        ---
        {{{argumentText}}}
        ---

        Respond with ONLY the claim as one concise sentence. No preamble.
        """;

        var result = await kernel.InvokePromptAsync(prompt, cancellationToken: ct);
        return result.ToString().Trim();
    }

    /// <summary>
    /// Fires two focused prompts in parallel on different providers (via the
    /// round-robin service).  One extracts premises + confidence assessments;
    /// the other extracts syllogisms, assumptions, qualifiers, and rebuttals.
    /// Splitting the mega-prompt this way roughly halves latency.
    /// </summary>
    private async Task<(string PremisesRaw, string StructureRaw)> ParallelDecompositionAsync(
        Kernel kernel,
        string argumentText,
        string claimText,
        Func<string, object?, Task>? onDebug,
        CancellationToken ct)
    {
        var premisesPrompt = $$$"""
        Always respond in English.
        You are an expert in argument analysis and epistemology.

        CENTRAL CLAIM: {{{claimText}}}

        ARGUMENT TEXT:
        ---
        {{{argumentText}}}
        ---

        List every distinct supporting proposition (explicit or strongly implied).
        For each premise provide a provisional truth-value assessment from your knowledge base.
        Output ONLY lines in this exact format — no preamble or explanation:

        PREMISE: [premise text] | CONFIDENCE: [0.0-1.0] | ASSESSMENT: [brief explanation]

        Confidence guide: 0.9-1.0 = established fact; 0.7-0.89 = strong evidence; 0.5-0.69 = plausible but debated; 0.3-0.49 = weak evidence; 0.0-0.29 = dubious/contradicted.
        """;

        var structurePrompt = $$$"""
        Always respond in English.
        You are an expert in formal logic and argument structure.

        CENTRAL CLAIM: {{{claimText}}}

        ARGUMENT TEXT:
        ---
        {{{argumentText}}}
        ---

        Output ONLY the sections below — no preamble, no commentary.

        ═══ SYLLOGISMS ═══
        SYLLOGISM:
        MAJOR: [general rule or principle]
        MINOR: [specific case]
        CONCLUSION: [what follows]
        TYPE: [deductive | inductive | abductive | analogical]

        ═══ ASSUMPTIONS ═══
        ASSUMPTION: [text] | CRITICAL: yes/no

        ═══ QUALIFIERS ═══
        QUALIFIER: [text]

        ═══ REBUTTALS ═══
        REBUTTAL: [text] | STRENGTH: [low/medium/high]
        """;

        // Fire both prompts concurrently — the round-robin service routes each
        // to a different provider automatically.
        if (onDebug != null)
            await onDebug("Starting parallel decomposition prompts", new { claimLength = claimText.Length });

        var premisesStopwatch = System.Diagnostics.Stopwatch.StartNew();
        var structureStopwatch = System.Diagnostics.Stopwatch.StartNew();

        var premisesTask = InvokePromptWithTimeoutAsync(
            operationName: "premises decomposition",
            timeout: ParallelPromptTimeout,
            promptAction: async promptCt =>
            {
                var result = await kernel.InvokePromptAsync(premisesPrompt, cancellationToken: promptCt);
                return result.ToString().Trim();
            },
            fallbackValue: string.Empty,
            onDebug: onDebug,
            cancellationToken: ct);

        var structureTask = InvokePromptWithTimeoutAsync(
            operationName: "structure decomposition",
            timeout: ParallelPromptTimeout,
            promptAction: async promptCt =>
            {
                var result = await kernel.InvokePromptAsync(structurePrompt, cancellationToken: promptCt);
                return result.ToString().Trim();
            },
            fallbackValue: string.Empty,
            onDebug: onDebug,
            cancellationToken: ct);

        _ = premisesTask.ContinueWith(async task =>
        {
            premisesStopwatch.Stop();
            if (onDebug == null) return;

            if (task.IsCompletedSuccessfully)
            {
                var text = task.Result.ToString().Trim();
                await onDebug("Premises prompt completed", new
                {
                    elapsedMs = premisesStopwatch.ElapsedMilliseconds,
                    resultLength = text.Length,
                    preview = text.Length > 160 ? text[..160] + "..." : text
                });
            }
            else if (task.IsFaulted)
            {
                await onDebug("Premises prompt failed", new
                {
                    elapsedMs = premisesStopwatch.ElapsedMilliseconds,
                    error = task.Exception?.GetBaseException().Message
                });
            }
            else if (task.IsCanceled)
            {
                await onDebug("Premises prompt canceled", new { elapsedMs = premisesStopwatch.ElapsedMilliseconds });
            }
        }, TaskScheduler.Default).Unwrap();

        _ = structureTask.ContinueWith(async task =>
        {
            structureStopwatch.Stop();
            if (onDebug == null) return;

            if (task.IsCompletedSuccessfully)
            {
                var text = task.Result.ToString().Trim();
                await onDebug("Structure prompt completed", new
                {
                    elapsedMs = structureStopwatch.ElapsedMilliseconds,
                    resultLength = text.Length,
                    preview = text.Length > 160 ? text[..160] + "..." : text
                });
            }
            else if (task.IsFaulted)
            {
                await onDebug("Structure prompt failed", new
                {
                    elapsedMs = structureStopwatch.ElapsedMilliseconds,
                    error = task.Exception?.GetBaseException().Message
                });
            }
            else if (task.IsCanceled)
            {
                await onDebug("Structure prompt canceled", new { elapsedMs = structureStopwatch.ElapsedMilliseconds });
            }
        }, TaskScheduler.Default).Unwrap();

        await Task.WhenAll(premisesTask, structureTask);

        if (onDebug != null)
        {
            await onDebug("Parallel decomposition prompts completed", new
            {
                premisesElapsedMs = premisesStopwatch.ElapsedMilliseconds,
                structureElapsedMs = structureStopwatch.ElapsedMilliseconds
            });
        }

        return (premisesTask.Result,
                structureTask.Result);
    }

    private async Task<string> InvokePromptWithTimeoutAsync(
        string operationName,
        TimeSpan timeout,
        Func<CancellationToken, Task<string>> promptAction,
        string fallbackValue,
        Func<string, object?, Task>? onDebug,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var result = await promptAction(timeoutCts.Token);
            stopwatch.Stop();

            if (onDebug != null)
            {
                await onDebug($"{operationName} finished", new
                {
                    elapsedMs = stopwatch.ElapsedMilliseconds,
                    resultLength = result.Length
                });
            }

            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            _logger.LogWarning(
                "Argument decomposition {Operation} timed out after {Seconds}s. Continuing with fallback.",
                operationName, timeout.TotalSeconds);

            if (onDebug != null)
            {
                await onDebug($"{operationName} timed out", new
                {
                    elapsedMs = stopwatch.ElapsedMilliseconds,
                    timeoutMs = timeout.TotalMilliseconds,
                    fallbackApplied = true
                });
            }

            return fallbackValue;
        }
    }

    private static string DeriveFallbackClaim(string text)
    {
        var sentences = SplitIntoSentences(text);
        if (sentences.Count > 0)
            return sentences[^1].Trim();

        return text.Length > 240 ? text[..240].Trim() : text.Trim();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Parsing (unified for the single mega-prompt response)
    // ─────────────────────────────────────────────────────────────────────────

    private DecompositionResult ParseFullDecomposition(string claimText, string rawAnalysis)
    {
        var result = new DecompositionResult { ClaimText = claimText };

        var lines = rawAnalysis.Split('\n');

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            // ── PREMISE lines (include provisional assessment) ──
            if (line.StartsWith("PREMISE:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split('|');
                var premiseText = parts[0].Replace("PREMISE:", "", StringComparison.OrdinalIgnoreCase).Trim();

                if (string.IsNullOrWhiteSpace(premiseText)) continue;
                result.Premises.Add(premiseText);

                double confidence = 0.5;
                string assessment = string.Empty;

                if (parts.Length >= 2)
                {
                    var confStr = parts[1].Replace("CONFIDENCE:", "", StringComparison.OrdinalIgnoreCase).Trim();
                    if (double.TryParse(confStr, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var c))
                        confidence = Math.Clamp(c, 0.0, 1.0);
                }
                if (parts.Length >= 3)
                    assessment = parts[2].Replace("ASSESSMENT:", "", StringComparison.OrdinalIgnoreCase).Trim();

                result.ProvisionalAssessments.Add(new ProvisionalAssessmentDto
                {
                    PremiseText = premiseText,
                    Confidence = confidence,
                    Assessment = assessment
                });
                continue;
            }

            // ── ASSUMPTION lines ──
            if (line.StartsWith("ASSUMPTION:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split('|');
                var text = parts[0].Replace("ASSUMPTION:", "", StringComparison.OrdinalIgnoreCase).Trim();
                var isCritical = parts.Length > 1 &&
                                 parts[1].Contains("yes", StringComparison.OrdinalIgnoreCase);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    result.Assumptions.Add(text);
                    result.CriticalAssumptions.Add(new CriticalAssumptionDto { Text = text, IsCritical = isCritical });
                }
                continue;
            }

            // ── QUALIFIER lines ──
            if (line.StartsWith("QUALIFIER:", StringComparison.OrdinalIgnoreCase))
            {
                var text = line.Split('|')[0].Replace("QUALIFIER:", "", StringComparison.OrdinalIgnoreCase).Trim();
                if (!string.IsNullOrWhiteSpace(text))
                    result.Qualifiers.Add(text);
                continue;
            }

            // ── REBUTTAL lines ──
            if (line.StartsWith("REBUTTAL:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split('|');
                var text = parts[0].Replace("REBUTTAL:", "", StringComparison.OrdinalIgnoreCase).Trim();
                var strength = "medium";
                if (parts.Length > 1)
                {
                    var sp = parts[1];
                    if (sp.Contains("high", StringComparison.OrdinalIgnoreCase)) strength = "high";
                    else if (sp.Contains("low", StringComparison.OrdinalIgnoreCase)) strength = "low";
                }
                if (!string.IsNullOrWhiteSpace(text))
                    result.Rebuttals.Add(new RebuttalDto { Text = text, Strength = strength });
                continue;
            }
        }

        // ── Parse syllogism blocks (multi-line) ──
        result.Syllogisms = ParseSyllogismBlocks(rawAnalysis);

        return result;
    }

    private static List<SyllogismDto> ParseSyllogismBlocks(string text)
    {
        var syllogisms = new List<SyllogismDto>();
        var blocks = text.Split("SYLLOGISM:", StringSplitOptions.RemoveEmptyEntries);

        foreach (var block in blocks)
        {
            if (string.IsNullOrWhiteSpace(block)) continue;

            var dto = new SyllogismDto();
            foreach (var line in block.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("MAJOR:", StringComparison.OrdinalIgnoreCase))
                    dto.MajorPremise = trimmed[6..].Trim();
                else if (trimmed.StartsWith("MINOR:", StringComparison.OrdinalIgnoreCase))
                    dto.MinorPremise = trimmed[6..].Trim();
                else if (trimmed.StartsWith("CONCLUSION:", StringComparison.OrdinalIgnoreCase))
                    dto.Conclusion = trimmed[11..].Trim();
                else if (trimmed.StartsWith("TYPE:", StringComparison.OrdinalIgnoreCase))
                {
                    var typeStr = trimmed[5..].Trim().ToLowerInvariant();
                    dto.InferenceType = typeStr switch
                    {
                        "inductive" => InferenceType.Inductive,
                        "abductive" => InferenceType.Abductive,
                        "analogical" => InferenceType.Analogical,
                        _ => InferenceType.Deductive
                    };
                }
            }

            if (!string.IsNullOrWhiteSpace(dto.Conclusion))
                syllogisms.Add(dto);
        }

        return syllogisms;
    }
}
