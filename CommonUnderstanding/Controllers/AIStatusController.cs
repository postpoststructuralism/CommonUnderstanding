using Microsoft.AspNetCore.Mvc;
using CommonUnderstanding.Services;

namespace CommonUnderstanding.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AIStatusController : ControllerBase
    {
        private readonly SemanticKernelService _semanticKernelService;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly RuntimeAiConfigService _runtimeConfig;

        public AIStatusController(
            SemanticKernelService semanticKernelService, 
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            RuntimeAiConfigService runtimeConfig)
        {
            _semanticKernelService = semanticKernelService;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _runtimeConfig = runtimeConfig;
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            var endpoint = _runtimeConfig.Endpoint ?? _configuration["Ollama:Endpoint"] ?? "http://localhost:11434";
            var modelName = _runtimeConfig.Model ?? _configuration["Ollama:ModelName"] ?? "llama3.2:1b";
            var agent = _runtimeConfig.Agent ?? _configuration["AIAgent"] ?? "ollama";

            var status = new
            {
                Timestamp = DateTime.UtcNow,
                Endpoint = endpoint,
                ModelName = modelName,
                OllamaConnected = await CheckOllamaConnection(endpoint),
                ModelAvailable = false,
                AvailableModels = new List<string>(),
                SystemStatus = "unknown"
            };

            try
            {
                if (status.OllamaConnected)
                {
                    var models = await GetAvailableModels(endpoint);
                    var modelAvailable = models.Contains(modelName);
                    
                    return Ok(new
                    {
                        status.Timestamp,
                        status.Endpoint,
                        status.ModelName,
                        status.OllamaConnected,
                        ModelAvailable = modelAvailable,
                        AvailableModels = models,
                        SystemStatus = modelAvailable ? "ready" : "model-missing",
                        Agent = agent
                    });
                }
                else
                {
                    return Ok(new
                    {
                        status.Timestamp,
                        status.Endpoint,
                        status.ModelName,
                        status.OllamaConnected,
                        status.ModelAvailable,
                        status.AvailableModels,
                        SystemStatus = "ollama-offline",
                        Agent = agent
                    });
                }
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    status.Timestamp,
                    status.Endpoint,
                    status.ModelName,
                    OllamaConnected = false,
                    ModelAvailable = false,
                    AvailableModels = new List<string>(),
                    SystemStatus = "error",
                    Error = ex.Message
                    , Agent = agent
                });
            }
        }

        [HttpPost("switch-model")]
        public IActionResult SwitchModel([FromBody] SwitchModelRequest request)
        {
            // Update runtime override and rebuild kernel if necessary
            if (string.IsNullOrEmpty(request.ModelName))
            {
                return BadRequest(new { Success = false, Message = "ModelName is required" });
            }

            _runtimeConfig.Model = request.ModelName;

            // Optionally, we could return whether the model is available (check via Ollama later)
            return Ok(new
            {
                Success = true,
                Message = "Runtime model override set",
                CurrentModel = _configuration["Ollama:ModelName"],
                RuntimeModel = _runtimeConfig.Model
            });
        }

        [HttpPost("switch-agent")]
        public IActionResult SwitchAgent([FromBody] SwitchAgentRequest request)
        {
            if (string.IsNullOrEmpty(request.Agent))
                return BadRequest(new { Success = false, Message = "Agent is required" });

            _runtimeConfig.Agent = request.Agent;

            // Note: actually switching to a different backend (Azure OpenAI) would require code changes; for now we
            // store the preference and show guidance.
            return Ok(new { Success = true, Message = $"Runtime agent set to {request.Agent}", Agent = request.Agent });
        }

        private async Task<bool> CheckOllamaConnection(string endpoint)
        {
            try
            {
                using var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                var response = await client.GetAsync(endpoint);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private async Task<List<string>> GetAvailableModels(string endpoint)
        {
            try
            {
                using var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                var response = await client.GetAsync($"{endpoint}/api/tags");
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var json = System.Text.Json.JsonDocument.Parse(content);
                    var models = new List<string>();
                    
                    if (json.RootElement.TryGetProperty("models", out var modelsArray))
                    {
                        foreach (var model in modelsArray.EnumerateArray())
                        {
                            if (model.TryGetProperty("name", out var name))
                            {
                                models.Add(name.GetString() ?? "");
                            }
                        }
                    }
                    
                    return models;
                }
            }
            catch
            {
                // Swallow exception
            }
            
            return new List<string>();
        }
    }

    public class SwitchModelRequest
    {
        public string ModelName { get; set; } = "";
    }

    public class SwitchAgentRequest
    {
        public string Agent { get; set; } = "";
    }
}
