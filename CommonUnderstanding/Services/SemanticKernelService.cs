using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Ollama;
using OllamaSharp;

namespace CommonUnderstanding.Services;

/// <summary>
/// Configuration and factory for Semantic Kernel with Ollama integration
/// </summary>
public class SemanticKernelService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SemanticKernelService> _logger;
    private Kernel? _kernel;
    private string? _currentEndpoint;
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
        // Rebuild kernel if runtime configuration has changed
        var runtimeEndpoint = _runtimeConfig.Endpoint ?? _configuration["Ollama:Endpoint"] ?? "http://localhost:11434";
        var runtimeModel = _runtimeConfig.Model ?? _configuration["Ollama:ModelName"] ?? "llama3.2:1b";

        if (_kernel != null && (_currentEndpoint != runtimeEndpoint || _currentModel != runtimeModel))
        {
            _logger.LogInformation("Runtime AI configuration changed. Rebuilding kernel.");
            _kernel = null;
        }

        if (_kernel != null)
            return _kernel;

        var ollamaEndpoint = _runtimeConfig.Endpoint ?? _configuration["Ollama:Endpoint"] ?? "http://localhost:11434";
        var ollamaModel = _runtimeConfig.Model ?? _configuration["Ollama:ModelName"] ?? "llama3.2:1b";

        _logger.LogInformation("Initializing Semantic Kernel with Ollama at {Endpoint} using model {Model}", 
            ollamaEndpoint, ollamaModel);

        try
        {
            var builder = Kernel.CreateBuilder();

            // Use a custom HttpClient with a generous timeout for large models
            var httpClient = new HttpClient
            {
                BaseAddress = new Uri(ollamaEndpoint),
                Timeout = TimeSpan.FromMinutes(5)
            };

            builder.AddOllamaChatCompletion(
                modelId: ollamaModel,
                httpClient: httpClient);

            // cache the current values
            _currentEndpoint = ollamaEndpoint;
            _currentModel = ollamaModel;

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

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            // First test direct connection to Ollama
            var ollamaEndpoint = _configuration["Ollama:Endpoint"] ?? "http://localhost:11434";
            var ollamaModel = _configuration["Ollama:ModelName"] ?? "llama3.2:1b";
            
            _logger.LogInformation("Testing direct Ollama connection to {Endpoint}", ollamaEndpoint);
            
            var ollama = new OllamaApiClient(new Uri(ollamaEndpoint));
            var isRunning = await ollama.IsRunningAsync();
            
            if (!isRunning)
            {
                _logger.LogError("Ollama is not running at {Endpoint}", ollamaEndpoint);
                return false;
            }
            
            _logger.LogInformation("Ollama is running. Testing with model {Model}", ollamaModel);
            
            // Now test Semantic Kernel
            var kernel = GetKernel();
            var response = await kernel.InvokePromptAsync("Say 'OK'");
            _logger.LogInformation("Ollama connection test successful: {Response}", response);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Ollama. Error: {Message}", ex.Message);
            throw;
        }
    }
}
