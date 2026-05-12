using Microsoft.SemanticKernel;
using OpenAI;
using System.ClientModel;

namespace CommonUnderstanding.Services;

/// <summary>
/// Configuration and factory for Semantic Kernel with OpenRouter integration
/// </summary>
public class SemanticKernelService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SemanticKernelService> _logger;
    private Kernel? _kernel;
    private string? _currentModel;
    private readonly RuntimeAiConfigService _runtimeConfig;

    public SemanticKernelService(
        IConfiguration configuration,
        ILogger<SemanticKernelService> logger,
        RuntimeAiConfigService runtimeConfig)
    {
        _configuration = configuration;
        _logger = logger;
        _runtimeConfig = runtimeConfig;
    }

    public Kernel GetKernel()
    {
        // Rebuild kernel if runtime model has changed
        var runtimeModel = ResolveModelId();

        if (_kernel != null && _currentModel != runtimeModel)
        {
            _logger.LogInformation("Runtime AI configuration changed. Rebuilding kernel.");
            _kernel = null;
        }

        if (_kernel != null)
            return _kernel;

        var openRouterModel = ResolveModelId();
        var apiKey = _configuration["OpenRouter:ApiKey"]
            ?? throw new InvalidOperationException("OpenRouter:ApiKey is not configured.");

        _logger.LogInformation("Initializing Semantic Kernel with OpenRouter model {Model}", openRouterModel);

        try
        {
            var builder = Kernel.CreateBuilder();

            var retryHandler = new RateLimitRetryHandler(_logger, model =>
            {
                // A 404 auto-fallback fired: record the working model so future
                // GetKernel() calls rebuild with it instead of the unavailable one.
                _logger.LogInformation("Auto-switched to OpenRouter model {Model}. Kernel will rebuild on next request.", model);
                _runtimeConfig.Model = model;
                _kernel = null;
            })
            {
                InnerHandler = new HttpClientHandler()
            };
            var httpClient = new HttpClient(retryHandler)
            {
                // Free-tier inference can be slow; give each individual attempt up to 5 minutes.
                Timeout = TimeSpan.FromMinutes(5)
            };

            var openAIClientOptions = new OpenAIClientOptions 
            { 
                Endpoint = new Uri("https://openrouter.ai/api/v1"),
                Transport = new System.ClientModel.Primitives.HttpClientPipelineTransport(httpClient)
            };
            var openAIClient = new OpenAIClient(new ApiKeyCredential(apiKey), openAIClientOptions);

            builder.AddOpenAIChatCompletion(
                modelId: openRouterModel,
                openAIClient: openAIClient);

            _currentModel = openRouterModel;
            _kernel = builder.Build();

            _logger.LogInformation("Kernel built successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building kernel: {Message}", ex.Message);
            throw;
        }

        return _kernel;
    }

    private string ResolveModelId()
    {
        if (OpenRouterModelCatalog.IsValid(_runtimeConfig.Model))
            return _runtimeConfig.Model!;

        if (!string.IsNullOrWhiteSpace(_runtimeConfig.Model))
        {
            _logger.LogWarning(
                "Ignoring invalid runtime OpenRouter model override {Model}. Resetting to configured/default model.",
                _runtimeConfig.Model);
            _runtimeConfig.Model = null;
        }

        var configuredModel = _configuration["OpenRouter:ModelId"];
        if (OpenRouterModelCatalog.IsValid(configuredModel))
            return configuredModel!;

        if (!string.IsNullOrWhiteSpace(configuredModel))
        {
            _logger.LogWarning(
                "Configured OpenRouter model {Model} is not in the supported allow-list. Falling back to {Fallback}.",
                configuredModel,
                OpenRouterModelCatalog.DefaultModelId);
        }

        return OpenRouterModelCatalog.DefaultModelId;
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            _logger.LogInformation("Testing Gemini connection");

            var kernel = GetKernel();
            var response = await kernel.InvokePromptAsync("Say 'OK'");
            _logger.LogInformation("Gemini connection test successful: {Response}", response);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Gemini. Error: {Message}", ex.Message);
            throw;
        }
    }
}

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
    private readonly Action<string>? _onModelFallback;

    private const int MaxRetries = 4;
    private static readonly TimeSpan[] Delays =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(60),
    ];

    private const int MaxTokensCap = 8192;

    public RateLimitRetryHandler(ILogger logger, Action<string>? onModelFallback = null)
    {
        _logger = logger;
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
            using var clone = BuildClone(request, currentBody, contentType);
            HttpResponseMessage response;

            try
            {
                response = await base.SendAsync(clone, cancellationToken);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                if (++retryCount > MaxRetries)
                    throw new InvalidOperationException(
                        $"OpenRouter request timed out after {MaxRetries + 1} attempts. " +
                        "The free-tier provider may be overloaded — try again shortly.", ex);
                var d = Delays[Math.Min(retryCount - 1, Delays.Length - 1)];
                _logger.LogWarning("OpenRouter timed out. Retry {R}/{Max} after {D}s.", retryCount, MaxRetries, d.TotalSeconds);
                await Task.Delay(d, cancellationToken);
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
                await Task.Delay(d, cancellationToken);
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

                    var nextModel400 = OpenRouterModelCatalog.GetNextModel(triedModels);
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

                var nextModel = OpenRouterModelCatalog.GetNextModel(triedModels);
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
            await Task.Delay(rateLimitDelay, cancellationToken);
        }
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
