using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenAI;
using System.ClientModel;

#pragma warning disable SKEXP0010  // OpenAIChatCompletionService (experimental)
#pragma warning disable SKEXP0070  // GoogleAIGeminiChatCompletionService (experimental)

namespace CommonUnderstanding.Services;

/// <summary>
/// Builds a Semantic Kernel backed by an Azure-first provider chain:
///   1. Azure Foundry primary model
///   2. Azure Foundry secondary model (optional)
///   3. Ollama fallback (optional, typically for local/dev resiliency)
///
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
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public SemanticKernelService(
        IConfiguration configuration,
        ILogger<SemanticKernelService> logger,
        RuntimeAiConfigService runtimeConfig,
        AiRequestTraceRecorder traceRecorder,
        IServiceScopeFactory scopeFactory,
        IHttpContextAccessor httpContextAccessor)
    {
        _configuration = configuration;
        _logger = logger;
        _runtimeConfig = runtimeConfig;
        _traceRecorder = traceRecorder;
        _scopeFactory = scopeFactory;
        _httpContextAccessor = httpContextAccessor;
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
                    "Set AzureFoundry:Endpoint + AzureFoundry:ApiKey, or enable reachable Ollama fallback.");

            _logger.LogInformation("Building kernel with {Count} provider(s): {Names}",
                providers.Count, string.Join(" → ", providers.Select(p => p.Name)));

            var fallback = new FallbackChatCompletionService(
                providers,
                _logger,
                _traceRecorder,
                _configuration,
                _scopeFactory,
                _httpContextAccessor);

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

        // 1. Azure Foundry primary model ────────────────────────────────────
        var azureEndpoint = _runtimeConfig.Endpoint ?? _configuration["AzureFoundry:Endpoint"];
        var azureApiKey = _configuration["AzureFoundry:ApiKey"];
        var azurePrimaryModel = ResolveAzurePrimaryModel();

        var azureApiVersion = _configuration["AzureFoundry:ApiVersion"] ?? "2024-12-01-preview";

        if (!string.IsNullOrWhiteSpace(azureEndpoint) &&
            !string.IsNullOrWhiteSpace(azureApiKey) &&
            !string.IsNullOrWhiteSpace(azurePrimaryModel))
        {
            try
            {
                providers.Add((
                    "AzureFoundryPrimary",
                    ResolveAzurePrimaryModel,
                    BuildAzureFoundryService(azurePrimaryModel, azureEndpoint, azureApiKey, azureApiVersion)));
                _logger.LogInformation("Azure Foundry primary provider configured (model: {Model}).", azurePrimaryModel);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not configure Azure Foundry primary provider — skipping.");
            }
        }
        else
        {
            _logger.LogInformation("AzureFoundry endpoint/key/model not fully configured — primary Azure provider skipped.");
        }

        // 2. Azure Foundry secondary model (optional) ───────────────────────
        var useSecondaryFallback = bool.TryParse(_configuration["AzureFoundry:UseSecondaryFallback"], out var parsedUseSecondary)
            ? parsedUseSecondary
            : true;
        var secondaryModel = _configuration["AzureFoundry:SecondaryModelId"];

        if (useSecondaryFallback &&
            !string.IsNullOrWhiteSpace(azureEndpoint) &&
            !string.IsNullOrWhiteSpace(azureApiKey) &&
            !string.IsNullOrWhiteSpace(secondaryModel) &&
            !string.Equals(secondaryModel, azurePrimaryModel, StringComparison.Ordinal))
        {
            try
            {
                providers.Add((
                    "AzureFoundrySecondary",
                    () => secondaryModel,
                    BuildAzureFoundryService(secondaryModel, azureEndpoint!, azureApiKey!, azureApiVersion)));
                _logger.LogInformation("Azure Foundry secondary provider configured (model: {Model}).", secondaryModel);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not configure Azure Foundry secondary provider — skipping.");
            }
        }

        // 3. Ollama ──────────────────────────────────────────────────────────
        // Only include Ollama if the endpoint is actually reachable.
        // This prevents the provider from being added in production when Ollama
        // is not deployed (e.g. pointing at a non-existent host).
        var enableOllamaFallback = bool.TryParse(_configuration["Ollama:EnableFallback"], out var parsedEnableOllama)
            ? parsedEnableOllama
            : true;
        var ollamaEndpoint = _configuration["Ollama:Endpoint"] ?? "http://localhost:11434";
        var ollamaModel    = _configuration["Ollama:Model"]    ?? OllamaModelCatalog.DefaultModelId;
        if (enableOllamaFallback && IsOllamaReachable(ollamaEndpoint))
        {
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
        }
        else
        {
            _logger.LogInformation("Ollama endpoint {Ep} is not reachable — Ollama provider skipped.", ollamaEndpoint);
        }

        return providers;
    }

    private static IChatCompletionService BuildAzureFoundryService(
        string model, string endpoint, string apiKey, string apiVersion)
    {
        // Azure AI endpoints require ?api-version=... on every request.
        // We inject it via a DelegatingHandler so the generic OpenAI client works
        // with both Azure OpenAI Service and Azure AI Foundry inference endpoints.
        var apiVersionHandler = new AzureApiVersionHandler(apiVersion)
        {
            InnerHandler = new HttpClientHandler()
        };

        var httpClient = new HttpClient(apiVersionHandler)
        {
            Timeout = TimeSpan.FromSeconds(120)
        };

        var clientOptions = new OpenAIClientOptions
        {
            Endpoint  = new Uri(endpoint.TrimEnd('/')),
            Transport = new System.ClientModel.Primitives.HttpClientPipelineTransport(httpClient)
        };
        var openAIClient = new OpenAIClient(new ApiKeyCredential(apiKey), clientOptions);

        return new Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIChatCompletionService(
            model, openAIClient);
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

    /// <summary>
    /// Performs a fast TCP probe to see whether the Ollama endpoint is reachable
    /// before wiring it up as a provider.  Times out after 2 seconds so it doesn't
    /// stall kernel construction in cloud environments where Ollama is not deployed.
    /// </summary>
    private static bool IsOllamaReachable(string endpoint)
    {
        try
        {
            var uri = new Uri(endpoint.TrimEnd('/'));
            var host = uri.Host;
            var port = uri.Port > 0 ? uri.Port : (uri.Scheme == "https" ? 443 : 80);

            using var client = new System.Net.Sockets.TcpClient();
            var connected = client.ConnectAsync(host, port)
                .Wait(TimeSpan.FromSeconds(2));
            return connected && client.Connected;
        }
        catch
        {
            return false;
        }
    }

    private string BuildFingerprint()
    {
        return string.Join("|",
            _runtimeConfig.Endpoint ?? _configuration["AzureFoundry:Endpoint"] ?? "",
            _configuration["AzureFoundry:ApiKey"] ?? "",
            ResolveAzurePrimaryModel(),
            _configuration["AzureFoundry:SecondaryModelId"] ?? "",
            _configuration["AzureFoundry:UseSecondaryFallback"] ?? "true",
            _configuration["Ollama:EnableFallback"] ?? "true",
            _configuration["Ollama:Endpoint"] ?? "http://localhost:11434",
            _configuration["Ollama:Model"]    ?? OllamaModelCatalog.DefaultModelId);
    }

    internal string ResolveAzurePrimaryModel()
    {
        if (!string.IsNullOrWhiteSpace(_runtimeConfig.Model))
            return _runtimeConfig.Model;

        return _configuration["AzureFoundry:ModelId"] ?? "DeepSeek-V3-0324";
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
    private const int MaxRouteFallbackModels = 3;

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
        await ApplyNativeFallbackRoutingAsync(request, cancellationToken);
        await CapMaxTokensAsync(request, cancellationToken);
        return await SendWithRetryAsync(request, cancellationToken);
    }

    private async Task ApplyNativeFallbackRoutingAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content is null)
            return;

        var body = await request.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(body))
            return;

        var patchedBody = await PatchModelRoutingAsync(body, primaryModelOverride: null, cancellationToken);
        if (patchedBody is null || string.Equals(patchedBody, body, StringComparison.Ordinal))
            return;

        request.Content = new StringContent(patchedBody, System.Text.Encoding.UTF8, "application/json");
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
        const int MaxModelCycles = 5; // max models to try on 429 before handing off to next provider

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
                        var patched400 = await PatchModelRoutingAsync(currentBody, nextModel400, cancellationToken);
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
                    var patched = await PatchModelRoutingAsync(currentBody, nextModel, cancellationToken);
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
            response.Dispose();

            if (body429.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "OpenRouter daily quota exhausted. Add credits or switch to a different model.");

            // Try cycling to the next free model, but only up to MaxModelCycles attempts.
            // Exceeding the cap throws immediately so FallbackChatCompletionService can
            // hand off to Gemini rather than spending minutes trying every catalog model.
            if (currentBody is not null && triedModels.Count < MaxModelCycles)
            {
                var saturatedModel = ExtractModel(currentBody);
                if (saturatedModel is not null) triedModels.Add(saturatedModel);

                var nextModel429 = await _openRouterModelCatalog.GetNextModelAsync(triedModels, cancellationToken);
                if (nextModel429 is not null)
                {
                    var patched429 = await PatchModelRoutingAsync(currentBody, nextModel429, cancellationToken);
                    if (patched429 is not null)
                    {
                        currentBody = patched429;
                        _logger.LogWarning(
                            "OpenRouter model {Bad} rate-limited (429). Auto-switching to {Next} ({Tried}/{Max} cycles).",
                            saturatedModel, nextModel429, triedModels.Count, MaxModelCycles);
                        _onModelFallback?.Invoke(nextModel429);
                        continue; // does NOT consume a retry slot
                    }
                }
            }

            // All model cycles exhausted — throw so the next provider (Gemini) is tried.
            throw new InvalidOperationException(
                $"OpenRouter rate-limited on all attempted models ({string.Join(", ", triedModels)}). Body: {body429[..Math.Min(body429.Length, 200)]}");
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

    private async Task<string?> PatchModelRoutingAsync(string body, string? primaryModelOverride, CancellationToken cancellationToken)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            var primaryModel = primaryModelOverride ?? ExtractModel(body);
            if (string.IsNullOrWhiteSpace(primaryModel))
                return null;

            var availableModels = _openRouterModelCatalog.GetCachedModels();
            if (availableModels.Count == 0)
                availableModels = await _openRouterModelCatalog.GetAvailableModelsAsync(cancellationToken: cancellationToken);

            var routedModels = BuildRouteFallbackModels(primaryModel, availableModels);

            using var ms = new System.IO.MemoryStream();
            using var writer = new System.Text.Json.Utf8JsonWriter(ms);
            writer.WriteStartObject();

            writer.WriteString("model", primaryModel);
            if (routedModels.Count > 1)
            {
                writer.WritePropertyName("models");
                writer.WriteStartArray();
                foreach (var model in routedModels)
                    writer.WriteStringValue(model);
                writer.WriteEndArray();
                writer.WriteString("route", "fallback");
            }

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Name is "model" or "models" or "route")
                    continue;

                prop.WriteTo(writer);
            }

            writer.WriteEndObject();
            writer.Flush();
            return System.Text.Encoding.UTF8.GetString(ms.ToArray());
        }
        catch { return null; }
    }

    private static IReadOnlyList<string> BuildRouteFallbackModels(string primaryModel, IReadOnlyList<string> availableModels)
    {
        var models = new List<string>(MaxRouteFallbackModels) { primaryModel };

        foreach (var candidate in availableModels)
        {
            if (string.Equals(candidate, primaryModel, StringComparison.Ordinal))
                continue;

            models.Add(candidate);
            if (models.Count >= MaxRouteFallbackModels)
                break;
        }

        return models;
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

/// <summary>
/// Injects the Azure-required <c>api-version</c> query parameter into every
/// outgoing HTTP request.  This is needed because the generic OpenAI client
/// does not add it automatically, but Azure OpenAI Service and Azure AI Foundry
/// inference endpoints both require it.
/// </summary>
internal sealed class AzureApiVersionHandler : DelegatingHandler
{
    private readonly string _apiVersion;

    public AzureApiVersionHandler(string apiVersion)
    {
        _apiVersion = apiVersion;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri;
        if (uri is not null)
        {
            var uriBuilder = new UriBuilder(uri);
            var query = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);
            if (string.IsNullOrEmpty(query["api-version"]))
            {
                query["api-version"] = _apiVersion;
                uriBuilder.Query = query.ToString();
                request.RequestUri = uriBuilder.Uri;
            }
        }
        return base.SendAsync(request, cancellationToken);
    }
}
