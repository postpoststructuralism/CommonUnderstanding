using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenAI;
using System.ClientModel;

#pragma warning disable SKEXP0010  // OpenAIChatCompletionService (experimental)
#pragma warning disable SKEXP0070  // GoogleAIGeminiChatCompletionService (experimental)

namespace CommonUnderstanding.Services;

/// <summary>
/// Builds a Semantic Kernel backed by a three-tier provider fallback chain:
///   1. OpenRouter (free-tier models, with auto-cycling and retry)
///   2. Gemini     (Google AI Studio — fast free tier)
///   3. Ollama     (local, always-available fallback)
///
/// Whichever providers are configured/reachable are included automatically.
/// All call-sites continue to use <c>kernel.InvokePromptAsync()</c> unchanged.
/// </summary>
public class SemanticKernelService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SemanticKernelService> _logger;
    private Kernel? _kernel;
    private string? _configFingerprint;
    private readonly RuntimeAiConfigService _runtimeConfig;
    private readonly AiRequestTraceRecorder _traceRecorder;
    private readonly OpenRouterModelCatalogService _openRouterModelCatalog;

    public SemanticKernelService(
        IConfiguration configuration,
        ILogger<SemanticKernelService> logger,
        RuntimeAiConfigService runtimeConfig,
        AiRequestTraceRecorder traceRecorder,
        OpenRouterModelCatalogService openRouterModelCatalog)
    {
        _configuration = configuration;
        _logger = logger;
        _runtimeConfig = runtimeConfig;
        _traceRecorder = traceRecorder;
        _openRouterModelCatalog = openRouterModelCatalog;
    }

    public Kernel GetKernel()
    {
        var fingerprint = BuildFingerprint();

        if (_kernel != null && _configFingerprint == fingerprint)
            return _kernel;

        if (_kernel != null)
            _logger.LogInformation("AI configuration changed. Rebuilding kernel.");

        _kernel = null;

        try
        {
            var providers = BuildProviders();

            if (providers.Count == 0)
                throw new InvalidOperationException(
                    "No AI providers are configured. " +
                    "Set OpenRouter:ApiKey, Gemini:ApiKey, or ensure Ollama is running.");

            _logger.LogInformation("Building kernel with {Count} provider(s): {Names}",
                providers.Count, string.Join(" → ", providers.Select(p => p.Name)));

            var fallback = new FallbackChatCompletionService(providers, _logger, _traceRecorder);

            var builder = Kernel.CreateBuilder();
            builder.Services.AddSingleton<IChatCompletionService>(fallback);
            _kernel = builder.Build();
            _configFingerprint = fingerprint;

            _logger.LogInformation("Kernel built successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build kernel: {Message}", ex.Message);
            throw;
        }

        return _kernel;
    }

    // ── Provider builders ────────────────────────────────────────────────────

    private List<(string Name, Func<string> ModelResolver, IChatCompletionService Service)> BuildProviders()
    {
        var providers = new List<(string Name, Func<string> ModelResolver, IChatCompletionService Service)>();

        // 1. OpenRouter ──────────────────────────────────────────────────────
        var openRouterKey = _configuration["OpenRouter:ApiKey"];
        if (!string.IsNullOrWhiteSpace(openRouterKey))
        {
            try
            {
                var openRouterModels = _openRouterModelCatalog
                    .GetAvailableModelsAsync()
                    .GetAwaiter()
                    .GetResult();

                if (openRouterModels.Count == 0)
                {
                    _logger.LogWarning("OpenRouter live catalog returned no free models. OpenRouter provider skipped.");
                }
                else
                {
                    var model = ResolveOpenRouterModel(openRouterModels);
                    providers.Add(("OpenRouter", ResolveOpenRouterModel, BuildOpenRouterService(model, openRouterKey)));
                    _logger.LogInformation("OpenRouter provider configured (model: {Model}).", model);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not configure OpenRouter provider — skipping.");
            }
        }
        else
        {
            _logger.LogInformation("OpenRouter:ApiKey not set — OpenRouter provider skipped.");
        }

        // 2. Gemini ──────────────────────────────────────────────────────────
        var geminiKey = _configuration["Gemini:ApiKey"];
        if (!string.IsNullOrWhiteSpace(geminiKey))
        {
            try
            {
                var geminiModel = _configuration["Gemini:ModelName"]
                                  ?? GeminiModelCatalog.DefaultModelId;
                providers.Add(("Gemini", () => geminiModel, BuildGeminiService(geminiModel, geminiKey)));
                _logger.LogInformation("Gemini provider configured (model: {Model}).", geminiModel);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not configure Gemini provider — skipping.");
            }
        }
        else
        {
            _logger.LogInformation("Gemini:ApiKey not set — Gemini provider skipped.");
        }

        // 3. Ollama ──────────────────────────────────────────────────────────
        var ollamaEndpoint = _configuration["Ollama:Endpoint"] ?? "http://localhost:11434";
        var ollamaModel    = _configuration["Ollama:Model"]    ?? OllamaModelCatalog.DefaultModelId;
        try
        {
            providers.Add(("Ollama", () => ollamaModel, BuildOllamaService(ollamaModel, ollamaEndpoint)));
            _logger.LogInformation("Ollama provider configured (endpoint: {Ep}, model: {Model}).",
                ollamaEndpoint, ollamaModel);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not configure Ollama provider — skipping.");
        }

        return providers;
    }

    private IChatCompletionService BuildOpenRouterService(string model, string apiKey)
    {
        var retryHandler = new RateLimitRetryHandler(_logger, _openRouterModelCatalog, newModel =>
        {
            _logger.LogInformation(
                "OpenRouter auto-switched to model {Model}. Kernel will rebuild on next request.", newModel);
            _runtimeConfig.Model = newModel;
            _kernel = null;
            _configFingerprint = null;
        })
        {
            InnerHandler = new HttpClientHandler()
        };

        var httpClient = new HttpClient(retryHandler)
        {
            Timeout = TimeSpan.FromSeconds(45)
        };

        var clientOptions = new OpenAIClientOptions
        {
            Endpoint  = new Uri("https://openrouter.ai/api/v1"),
            Transport = new System.ClientModel.Primitives.HttpClientPipelineTransport(httpClient)
        };
        var openAIClient = new OpenAIClient(new ApiKeyCredential(apiKey), clientOptions);

        return new Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIChatCompletionService(
            model, openAIClient);
    }

    private static IChatCompletionService BuildGeminiService(string modelId, string apiKey)
    {
        return new Microsoft.SemanticKernel.Connectors.Google.GoogleAIGeminiChatCompletionService(
            modelId: modelId,
            apiKey:  apiKey);
    }

    private static IChatCompletionService BuildOllamaService(string modelId, string endpoint)
    {
        // Ollama exposes an OpenAI-compatible API at <endpoint>/v1
        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = new Uri(endpoint.TrimEnd('/') + "/v1")
        };
        var ollamaClient = new OpenAIClient(new ApiKeyCredential("ollama"), clientOptions);

        return new Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIChatCompletionService(
            modelId, ollamaClient);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private string BuildFingerprint()
    {
        return string.Join("|",
            ResolveOpenRouterModel(),
            _configuration["Gemini:ApiKey"]  ?? "",
            _configuration["Gemini:ModelName"] ?? GeminiModelCatalog.DefaultModelId,
            _configuration["Ollama:Endpoint"] ?? "http://localhost:11434",
            _configuration["Ollama:Model"]    ?? OllamaModelCatalog.DefaultModelId);
    }

    internal string ResolveOpenRouterModel()
    {
        var cachedModels = _openRouterModelCatalog.GetCachedModels();
        return ResolveOpenRouterModel(cachedModels);
    }

    private string ResolveOpenRouterModel(IReadOnlyList<string> availableModels)
    {
        if (!string.IsNullOrWhiteSpace(_runtimeConfig.Model) &&
            availableModels.Contains(_runtimeConfig.Model, StringComparer.Ordinal))
            return _runtimeConfig.Model;

        if (!string.IsNullOrWhiteSpace(_runtimeConfig.Model))
        {
            _logger.LogWarning("Discarding invalid runtime OpenRouter model override: {Model}", _runtimeConfig.Model);
            _runtimeConfig.Model = null;
        }

        var configured = _configuration["OpenRouter:ModelId"];
        if (!string.IsNullOrWhiteSpace(configured) &&
            availableModels.Contains(configured, StringComparer.Ordinal))
            return configured;

        if (availableModels.Count > 0)
            return availableModels[0];

        return configured ?? OpenRouterModelCatalog.DefaultModelId;
    }

    public async Task<bool> TestConnectionAsync()
    {
        var kernel = GetKernel();
        var response = await kernel.InvokePromptAsync("Reply with only the word: OK");
        _logger.LogInformation("AI connection test successful: {Response}", response);
        return true;
    }
}

#pragma warning restore SKEXP0070
#pragma warning restore SKEXP0010

/// <summary>
/// DelegatingHandler that:
///  - caps max_tokens on every request to stay within free-tier limits
///  - auto-cycles through OpenRouterModelCatalog on 404 (model offline) — transparent to callers
///  - retries on 429 (rate limit) with exponential back-off
///  - retries on transient socket / timeout errors
/// </summary>
internal sealed class RateLimitRetryHandler : DelegatingHandler
{
    private readonly ILogger _logger;
    private readonly OpenRouterModelCatalogService _openRouterModelCatalog;
    private readonly Action<string>? _onModelFallback;

    private const int MaxRetries = 2;
    private static readonly TimeSpan[] Delays =
    [
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
    ];

    private const int MaxTokensCap = 8192;

    public RateLimitRetryHandler(
        ILogger logger,
        OpenRouterModelCatalogService openRouterModelCatalog,
        Action<string>? onModelFallback = null)
    {
        _logger = logger;
        _openRouterModelCatalog = openRouterModelCatalog;
        _onModelFallback = onModelFallback;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await CapMaxTokensAsync(request, cancellationToken);
        return await SendWithRetryAsync(request, cancellationToken);
    }

    private static async Task CapMaxTokensAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (request.Content is null) return;

        var body = await request.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(body)) return;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            var root = doc.RootElement;

            // Only patch if it's a chat/completions JSON object
            if (root.ValueKind != System.Text.Json.JsonValueKind.Object) return;

            using var ms = new System.IO.MemoryStream();
            using var writer = new System.Text.Json.Utf8JsonWriter(ms);
            writer.WriteStartObject();
            bool patched = false;

            foreach (var prop in root.EnumerateObject())
            {
                if (prop.Name is "max_tokens" or "max_completion_tokens")
                {
                    int current = prop.Value.ValueKind == System.Text.Json.JsonValueKind.Number
                        ? prop.Value.GetInt32() : int.MaxValue;
                    writer.WriteNumber(prop.Name, Math.Min(current, MaxTokensCap));
                    patched = true;
                }
                else
                {
                    prop.WriteTo(writer);
                }
            }

            // If neither field was present, inject max_tokens
            if (!patched)
                writer.WriteNumber("max_tokens", MaxTokensCap);

            writer.WriteEndObject();
            await writer.FlushAsync(ct);

            var patched_body = System.Text.Encoding.UTF8.GetString(ms.ToArray());
            request.Content = new StringContent(patched_body, System.Text.Encoding.UTF8, "application/json");
        }
        catch { /* if JSON parsing fails, send as-is */ }
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Read the (already token-capped) body once so we can patch the model field on 404 fallback.
        string? currentBody = null;
        string contentType = "application/json";
        if (request.Content is not null)
        {
            currentBody = await request.Content.ReadAsStringAsync(cancellationToken);
            contentType = request.Content.Headers.ContentType?.ToString() ?? "application/json";
        }

        var triedModels = new HashSet<string>(StringComparer.Ordinal);
        int retryCount = 0;

        while (true)
        {
            // Stop immediately if the caller has already cancelled — avoids attempting a new
            // HTTP request with a dead token which would throw an uncaught TaskCanceledException.
            cancellationToken.ThrowIfCancellationRequested();

            using var clone = BuildClone(request, currentBody, contentType);
            HttpResponseMessage response;

            try
            {
                response = await base.SendAsync(clone, cancellationToken);
            }
            catch (TaskCanceledException ex) when (
                !cancellationToken.IsCancellationRequested ||
                ex.InnerException is System.IO.IOException)
            {
                // A socket abort (IOException inner) can cancel the outer token as a side-effect.
                // If the *caller* genuinely cancelled, don't retry — propagate cleanly.
                cancellationToken.ThrowIfCancellationRequested();

                if (++retryCount > MaxRetries)
                    throw new InvalidOperationException(
                        $"OpenRouter request timed out or connection was aborted after {MaxRetries + 1} attempts. " +
                        "The remote AI provider may be overloaded — try again shortly.", ex);
                var d = Delays[Math.Min(retryCount - 1, Delays.Length - 1)];
                _logger.LogWarning("OpenRouter timed out or connection aborted. Retry {R}/{Max} after {D}s.", retryCount, MaxRetries, d.TotalSeconds);
                await LinkedDelay(d, cancellationToken);
                continue;
            }
            catch (HttpRequestException ex) when (
                ex.InnerException is System.IO.IOException ||
                ex.InnerException is System.Net.Sockets.SocketException)
            {
                if (++retryCount > MaxRetries)
                    throw new InvalidOperationException(
                        $"OpenRouter connection failed after {MaxRetries + 1} attempts. " +
                        "The free-tier provider may be overloaded — try again shortly.", ex);
                var d = Delays[Math.Min(retryCount - 1, Delays.Length - 1)];
                _logger.LogWarning("OpenRouter socket error. Retry {R}/{Max} after {D}s: {Msg}", retryCount, MaxRetries, d.TotalSeconds, ex.Message);
                await LinkedDelay(d, cancellationToken);
                continue;
            }

            var status = (int)response.StatusCode;

            // ── 400 Bad Request ─────────────────────────────────────────────
            if (status == 400)
            {
                var err = await response.Content.ReadAsStringAsync(cancellationToken);
                response.Dispose();

                // OpenRouter returns 400 "not a valid model ID" for models it no longer recognises.
                // Treat these the same as 404 — cycle to the next catalog model.
                bool isModelError = err.Contains("not a valid model", StringComparison.OrdinalIgnoreCase)
                    || err.Contains("invalid model", StringComparison.OrdinalIgnoreCase)
                    || err.Contains("model not found", StringComparison.OrdinalIgnoreCase)
                    || err.Contains("unknown model", StringComparison.OrdinalIgnoreCase);

                if (isModelError && currentBody is not null)
                {
                    var badModel400 = ExtractModel(currentBody);
                    if (badModel400 is not null) triedModels.Add(badModel400);

                    var nextModel400 = await _openRouterModelCatalog.GetNextModelAsync(triedModels, cancellationToken);
                    if (nextModel400 is not null)
                    {
                        var patched400 = PatchModel(currentBody, nextModel400);
                        if (patched400 is not null)
                        {
                            currentBody = patched400;
                            _logger.LogWarning(
                                "OpenRouter model {Bad} not recognised (400). Auto-switching to {Next}.",
                                badModel400, nextModel400);
                            _onModelFallback?.Invoke(nextModel400);
                            continue;
                        }
                    }

                    throw new InvalidOperationException(
                        $"All OpenRouter free models are currently unavailable. " +
                        $"Tried: {string.Join(", ", triedModels.Count > 0 ? (IEnumerable<string>)triedModels : ["unknown"])}. " +
                        $"Last error: {err[..Math.Min(err.Length, 200)]}.");
                }

                throw new InvalidOperationException($"OpenRouter 400 error: {err[..Math.Min(err.Length, 400)]}");
            }

            // ── 404 Model Unavailable → auto-cycle through catalog ──────────
            if (status == 404)
            {
                var err = await response.Content.ReadAsStringAsync(cancellationToken);
                response.Dispose();

                var badModel = ExtractModel(currentBody);
                if (badModel is not null) triedModels.Add(badModel);

                var nextModel = await _openRouterModelCatalog.GetNextModelAsync(triedModels, cancellationToken);
                if (nextModel is not null && currentBody is not null)
                {
                    var patched = PatchModel(currentBody, nextModel);
                    if (patched is not null)
                    {
                        currentBody = patched;
                        _logger.LogWarning(
                            "OpenRouter model {Bad} unavailable (404). Auto-switching to {Next}.",
                            badModel, nextModel);
                        _onModelFallback?.Invoke(nextModel);
                        continue; // does NOT consume a retry slot
                    }
                }

                throw new InvalidOperationException(
                    $"All OpenRouter free models are currently unavailable. " +
                    $"Tried: {string.Join(", ", triedModels.Count > 0 ? (IEnumerable<string>)triedModels : ["unknown"])}. " +
                    $"Last error: {err[..Math.Min(err.Length, 200)]}. " +
                    "Visit https://openrouter.ai/models?max_price=0 to see what is currently live.");
            }

            // ── Success ──────────────────────────────────────────────────────
            if (status != 429) return response;

            // ── 429 Rate Limit ───────────────────────────────────────────────
            var body429 = await response.Content.ReadAsStringAsync(cancellationToken);
            if (body429.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "OpenRouter daily quota exhausted. Add credits or switch to a different model.");

            if (++retryCount > MaxRetries)
                throw new InvalidOperationException(
                    $"OpenRouter rate-limited after {MaxRetries + 1} attempts. Body: {body429[..Math.Min(body429.Length, 200)]}");

            var rateLimitDelay = GetDelay(response, retryCount - 1);
            response.Dispose();
            _logger.LogWarning("OpenRouter 429. Retry {R}/{Max} after {D}s.", retryCount, MaxRetries, rateLimitDelay.TotalSeconds);
            await LinkedDelay(rateLimitDelay, cancellationToken);
        }
    }

    /// <summary>
    /// Delays using a <em>linked</em> CancellationTokenSource so that if the caller's token
    /// fires during the wait, the resulting <see cref="TaskCanceledException"/> carries the
    /// linked token — not the caller's token. This lets <see cref="FallbackChatCompletionService"/>
    /// distinguish "provider delay was interrupted" (try next provider) from "caller cancelled"
    /// (stop the chain immediately).
    /// </summary>
    private static async Task LinkedDelay(TimeSpan delay, CancellationToken callerToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
        await Task.Delay(delay, cts.Token);
    }

    private static HttpRequestMessage BuildClone(HttpRequestMessage original, string? body, string contentType)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri) { Version = original.Version };
        foreach (var (key, value) in original.Headers)
            clone.Headers.TryAddWithoutValidation(key, value);
        if (body is not null)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(body);
            var content = new ByteArrayContent(bytes);
            // Copy content headers except Content-Length — ByteArrayContent calculates it
            // from the actual bytes, so carrying over the old value causes a mismatch after patching.
            if (original.Content is not null)
                foreach (var (key, value) in original.Content.Headers)
                    if (!string.Equals(key, "Content-Length", StringComparison.OrdinalIgnoreCase))
                        content.Headers.TryAddWithoutValidation(key, value);
            else
                content.Headers.TryAddWithoutValidation("Content-Type", contentType);
            clone.Content = content;
        }
        return clone;
    }

    private static string? ExtractModel(string? body)
    {
        if (body is null) return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("model", out var m)) return m.GetString();
        }
        catch { }
        return null;
    }

    private static string? PatchModel(string body, string newModel)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            using var ms = new System.IO.MemoryStream();
            using var writer = new System.Text.Json.Utf8JsonWriter(ms);
            writer.WriteStartObject();
            bool found = false;
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Name == "model") { writer.WriteString("model", newModel); found = true; }
                else prop.WriteTo(writer);
            }
            if (!found) writer.WriteString("model", newModel);
            writer.WriteEndObject();
            writer.Flush();
            return System.Text.Encoding.UTF8.GetString(ms.ToArray());
        }
        catch { return null; }
    }

    private static TimeSpan GetDelay(HttpResponseMessage response, int attempt)
    {
        try
        {
            if (response.Headers.RetryAfter?.Delta is { } delta && delta > TimeSpan.Zero) return delta;
            if (response.Headers.RetryAfter?.Date is { } date)
            {
                var wait = date - DateTimeOffset.UtcNow;
                if (wait > TimeSpan.Zero) return wait;
            }
        }
        catch { }
        return Delays[Math.Min(attempt, Delays.Length - 1)];
    }
}
