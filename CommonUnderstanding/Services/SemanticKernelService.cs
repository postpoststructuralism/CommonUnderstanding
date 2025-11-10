using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Ollama;

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
        var ollamaModel = _configuration["Ollama:ModelName"] ?? "llama3.2";

        _logger.LogInformation("Initializing Semantic Kernel with Ollama at {Endpoint} using model {Model}", 
            ollamaEndpoint, ollamaModel);

        var builder = Kernel.CreateBuilder();
        
        builder.AddOllamaChatCompletion(
            modelId: ollamaModel,
            endpoint: new Uri(ollamaEndpoint));

        _kernel = builder.Build();

        return _kernel;
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var kernel = GetKernel();
            var response = await kernel.InvokePromptAsync("Reply with 'OK' if you can read this.");
            _logger.LogInformation("Ollama connection test successful");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Ollama");
            return false;
        }
    }
}
