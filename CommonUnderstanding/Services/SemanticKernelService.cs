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

    public SemanticKernelService(
        IConfiguration configuration,
        ILogger<SemanticKernelService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Kernel GetKernel()
    {
        if (_kernel != null)
            return _kernel;

        var ollamaEndpoint = _configuration["Ollama:Endpoint"] ?? "http://localhost:11434";
        var ollamaModel = _configuration["Ollama:ModelName"] ?? "llama3.2:1b";

        _logger.LogInformation("Initializing Semantic Kernel with Ollama at {Endpoint} using model {Model}", 
            ollamaEndpoint, ollamaModel);

        try
        {
            var builder = Kernel.CreateBuilder();
            
            builder.AddOllamaChatCompletion(
                modelId: ollamaModel,
                endpoint: new Uri(ollamaEndpoint));

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
