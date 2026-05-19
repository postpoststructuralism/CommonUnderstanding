using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace CommonUnderstanding.Services;

/// <summary>
/// Wraps multiple <see cref="IChatCompletionService"/> implementations and distributes
/// successive calls across providers in round-robin order, so that the N distinct AI
/// processing steps in an analysis request each land on a different backend.
/// If the assigned provider fails, the next one in rotation is tried as a fallback.
/// Configured providers: OpenRouter, Gemini, and Ollama.
/// </summary>
internal sealed class FallbackChatCompletionService : IChatCompletionService
{
    private readonly IReadOnlyList<(string Name, Func<string> ModelResolver, IChatCompletionService Service)> _providers;
    private readonly ILogger _logger;
    private readonly AiRequestTraceRecorder _traceRecorder;
    private int _callIndex = -1;

    // Per-provider rate-limit cooldown: tracks when each provider becomes available again.
    private readonly DateTimeOffset[] _availableAt;
    private static readonly TimeSpan RateLimitCooldown = TimeSpan.FromSeconds(60);

    // Global concurrency gate: caps the number of simultaneous in-flight AI requests across
    // ALL service instances (foreground + background). Without this, a burst of parallel calls
    // (decomposition + prefetch + response processing) all hit the same free-tier model at once
    // and trigger 429s even when the overall request rate is low.
    private static readonly SemaphoreSlim _globalConcurrency = new(2, 2);

    public FallbackChatCompletionService(
        IReadOnlyList<(string Name, Func<string> ModelResolver, IChatCompletionService Service)> providers,
        ILogger logger,
        AiRequestTraceRecorder traceRecorder)
    {
        if (providers.Count == 0)
            throw new ArgumentException("At least one provider must be supplied.", nameof(providers));

        _providers = providers;
        _logger = logger;
        _traceRecorder = traceRecorder;
        _availableAt = new DateTimeOffset[providers.Count];
    }

    // Expose the first provider's attributes as the service attributes.
    public IReadOnlyDictionary<string, object?> Attributes => _providers[0].Service.Attributes;

    // ── Non-streaming ────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        var promptPreview = ExtractPromptPreview(chatHistory);
        var startIdx = NextProviderIndex();

        await _globalConcurrency.WaitAsync(cancellationToken);
        try
        {
            Exception? lastEx = null;
            TimeSpan? shortestCooldown = null;
            for (int i = 0; i < _providers.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int idx = (startIdx + i) % _providers.Count;
                var provider = _providers[idx];
                if (TryGetProviderCooldown(idx, out var wait))
                {
                    shortestCooldown = shortestCooldown is null || wait < shortestCooldown
                        ? wait
                        : shortestCooldown;

                    _logger.LogInformation(
                        "AI provider [{Name}] is rate-limited; skipping for this pass ({Ms}ms remaining).",
                        provider.Name,
                        (int)wait.TotalMilliseconds);
                    continue;
                }

                var model = provider.ModelResolver();
                try
                {
                    return await ExecuteNonStreamingAsync(
                        provider.Name, model, provider.Service,
                        chatHistory, executionSettings, kernel,
                        promptPreview, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex) when (IsRateLimitException(ex))
                {
                    MarkRateLimited(idx, provider.Name, model);
                    lastEx = ex;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "AI provider [{Name}] model [{Model}] failed; trying next provider. Reason: {Msg}",
                        provider.Name, model, ex.Message);
                    lastEx = ex;
                }
            }

            if (lastEx is null && shortestCooldown is not null)
            {
                throw new InvalidOperationException(
                    $"All AI providers are cooling down after rate limits. Earliest retry in {Math.Ceiling(shortestCooldown.Value.TotalSeconds)} second(s).");
            }

            throw new InvalidOperationException(
                $"All AI providers failed. Last error: {lastEx?.Message}", lastEx);
        }
        finally
        {
            _globalConcurrency.Release();
        }
    }

    // ── Streaming ────────────────────────────────────────────────────────────
    // yield return cannot appear inside a try/catch block, so we buffer the
    // full stream from each provider attempt via a helper, then yield the buffer.

    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var chunks = await BufferStreamAsync(chatHistory, executionSettings, kernel, cancellationToken);
        foreach (var chunk in chunks)
            yield return chunk;
    }

    private async Task<List<StreamingChatMessageContent>> BufferStreamAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings,
        Kernel? kernel,
        CancellationToken cancellationToken)
    {
        var promptPreview = ExtractPromptPreview(chatHistory);
        var startIdx = NextProviderIndex();

        await _globalConcurrency.WaitAsync(cancellationToken);
        try
        {
            Exception? lastEx = null;
            TimeSpan? shortestCooldown = null;
            for (int i = 0; i < _providers.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int idx = (startIdx + i) % _providers.Count;
                var provider = _providers[idx];
                if (TryGetProviderCooldown(idx, out var wait))
                {
                    shortestCooldown = shortestCooldown is null || wait < shortestCooldown
                        ? wait
                        : shortestCooldown;

                    _logger.LogInformation(
                        "AI provider [{Name}] is rate-limited; skipping for this pass ({Ms}ms remaining).",
                        provider.Name,
                        (int)wait.TotalMilliseconds);
                    continue;
                }

                var model = provider.ModelResolver();
                try
                {
                    return await ExecuteStreamingAsync(
                        provider.Name, model, provider.Service,
                        chatHistory, executionSettings, kernel,
                        promptPreview, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex) when (IsRateLimitException(ex))
                {
                    MarkRateLimited(idx, provider.Name, model);
                    lastEx = ex;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "AI provider [{Name}] model [{Model}] failed (streaming); trying next provider. Reason: {Msg}",
                        provider.Name, model, ex.Message);
                    lastEx = ex;
                }
            }

            if (lastEx is null && shortestCooldown is not null)
            {
                throw new InvalidOperationException(
                    $"All AI providers are cooling down after rate limits. Earliest retry in {Math.Ceiling(shortestCooldown.Value.TotalSeconds)} second(s).");
            }

            throw new InvalidOperationException(
                $"All AI providers failed (streaming). Last error: {lastEx?.Message}", lastEx);
        }
        finally
        {
            _globalConcurrency.Release();
        }
    }

    private int NextProviderIndex()
    {
        var raw = Interlocked.Increment(ref _callIndex);
        return (int)((uint)raw % (uint)_providers.Count);
    }

    private void MarkRateLimited(int idx, string name, string model)
    {
        _availableAt[idx] = DateTimeOffset.UtcNow + RateLimitCooldown;
        _logger.LogWarning(
            "AI provider [{Name}] model [{Model}] rate-limited (429); cooling down for {Seconds}s.",
            name, model, RateLimitCooldown.TotalSeconds);
    }

    private bool TryGetProviderCooldown(int idx, out TimeSpan wait)
    {
        wait = _availableAt[idx] - DateTimeOffset.UtcNow;
        return wait > TimeSpan.Zero;
    }

    private static bool IsRateLimitException(Exception ex)
    {
        return ex.Message.Contains("429") ||
               ex.InnerException?.Message.Contains("429") == true;
    }

    private async Task<IReadOnlyList<ChatMessageContent>> ExecuteNonStreamingAsync(
        string name,
        string model,
        IChatCompletionService service,
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings,
        Kernel? kernel,
        string promptPreview,
        CancellationToken cancellationToken)
    {
        _traceRecorder.RecordAttempt(name, model, promptPreview, isStreaming: false);
        _logger.LogDebug("AI provider [{Name}] model [{Model}]: sending request.", name, model);

        var result = await service.GetChatMessageContentsAsync(
            chatHistory,
            executionSettings,
            kernel,
            cancellationToken);

        _logger.LogDebug("AI provider [{Name}] model [{Model}]: success.", name, model);
        return result;
    }

    private async Task<List<StreamingChatMessageContent>> ExecuteStreamingAsync(
        string name,
        string model,
        IChatCompletionService service,
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings,
        Kernel? kernel,
        string promptPreview,
        CancellationToken cancellationToken)
    {
        _traceRecorder.RecordAttempt(name, model, promptPreview, isStreaming: true);
        _logger.LogDebug("AI provider [{Name}] model [{Model}]: sending streaming request.", name, model);

        var buffer = new List<StreamingChatMessageContent>();
        await foreach (var chunk in service.GetStreamingChatMessageContentsAsync(
            chatHistory,
            executionSettings,
            kernel,
            cancellationToken))
        {
            buffer.Add(chunk);
        }

        _logger.LogDebug("AI provider [{Name}] model [{Model}]: streaming success ({N} chunks).", name, model, buffer.Count);
        return buffer;
    }

    private static string ExtractPromptPreview(ChatHistory chatHistory)
    {
        var lastContent = chatHistory
            .Select(message => message.Content)
            .LastOrDefault(content => !string.IsNullOrWhiteSpace(content));

        return lastContent ?? "(no chat content)";
    }
}
