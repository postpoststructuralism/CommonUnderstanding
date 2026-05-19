using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using CommonUnderstanding.Services;

namespace CommonUnderstanding.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AIStatusController : ControllerBase
    {
        private readonly SemanticKernelService _semanticKernelService;
        private readonly OpenRouterModelCatalogService _openRouterModelCatalog;
        private readonly IConfiguration _configuration;
        private readonly RuntimeAiConfigService _runtimeConfig;

        public AIStatusController(
            SemanticKernelService semanticKernelService,
            OpenRouterModelCatalogService openRouterModelCatalog,
            IConfiguration configuration,
            RuntimeAiConfigService runtimeConfig)
        {
            _semanticKernelService = semanticKernelService;
            _openRouterModelCatalog = openRouterModelCatalog;
            _configuration = configuration;
            _runtimeConfig = runtimeConfig;
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            var openRouterModel = _semanticKernelService.ResolveOpenRouterModel();
            var openRouterConfigured = !string.IsNullOrWhiteSpace(_configuration["OpenRouter:ApiKey"]);
            var geminiConfigured    = !string.IsNullOrWhiteSpace(_configuration["Gemini:ApiKey"]);
            var geminiModel         = _configuration["Gemini:ModelName"] ?? GeminiModelCatalog.DefaultModelId;
            var ollamaEndpoint      = _configuration["Ollama:Endpoint"] ?? "http://localhost:11434";
            var ollamaModel         = _configuration["Ollama:Model"]    ?? OllamaModelCatalog.DefaultModelId;
            var openRouterModels    = openRouterConfigured
                ? await _openRouterModelCatalog.GetAvailableModelsAsync()
                : [];

            var activeProviders = new List<string>();
            if (openRouterConfigured) activeProviders.Add("OpenRouter");
            if (geminiConfigured)    activeProviders.Add("Gemini");
            activeProviders.Add("Ollama"); // always listed (may not be running)

            var systemStatus = activeProviders.Count > 1 ? "ready"
                             : openRouterConfigured || geminiConfigured ? "ready"
                             : "no-cloud-keys";

            try
            {
                return Ok(new
                {
                    Timestamp = DateTime.UtcNow,
                    SystemStatus = systemStatus,
                    FallbackChain = activeProviders,
                    OpenRouter = new
                    {
                        Configured     = openRouterConfigured,
                        Model          = openRouterModel,
                        AvailableModels = openRouterModels
                    },
                    Gemini = new
                    {
                        Configured     = geminiConfigured,
                        Model          = geminiModel,
                        AvailableModels = GeminiModelCatalog.AvailableModels
                    },
                    Ollama = new
                    {
                        Endpoint       = ollamaEndpoint,
                        Model          = ollamaModel,
                        AvailableModels = OllamaModelCatalog.AvailableModels
                    }
                });
            }
            catch (Exception ex)
            {
                return Ok(new { Timestamp = DateTime.UtcNow, SystemStatus = "error", Error = ex.Message });
            }
        }

        [HttpPost("switch-model")]
        public async Task<IActionResult> SwitchModel([FromBody] SwitchModelRequest request)
        {
            if (string.IsNullOrEmpty(request.ModelName))
            {
                return BadRequest(new { Success = false, Message = "ModelName is required" });
            }

            var openRouterModels = await _openRouterModelCatalog.GetAvailableModelsAsync();
            if (!openRouterModels.Contains(request.ModelName, StringComparer.Ordinal))
            {
                _runtimeConfig.Model = null;
                return BadRequest(new
                {
                    Success = false,
                    Message = $"Unsupported OpenRouter model: {request.ModelName}",
                    AvailableModels = openRouterModels
                });
            }

            _runtimeConfig.Model = request.ModelName;

            return Ok(new
            {
                Success = true,
                Message = "Runtime model override set",
                CurrentModel = _configuration["OpenRouter:ModelId"],
                RuntimeModel = _runtimeConfig.Model
            });
        }

        [HttpPost("switch-agent")]
        public IActionResult SwitchAgent([FromBody] SwitchAgentRequest request)
        {
            if (string.IsNullOrEmpty(request.Agent))
                return BadRequest(new { Success = false, Message = "Agent is required" });

            _runtimeConfig.Agent = request.Agent;

            return Ok(new { Success = true, Message = $"Runtime agent set to {request.Agent}", Agent = request.Agent });
        }

        [HttpGet("test-ai")]
        public async Task<IActionResult> TestAi()
        {
            try
            {
                var kernel = _semanticKernelService.GetKernel();
                var result = await kernel.InvokePromptAsync("Reply with only the word: OK");
                return Ok(new { Success = true, Response = result.ToString() });
            }
            catch (Exception ex)
            {
                return Ok(new { Success = false, Error = ex.Message });
            }
        }

        [HttpPost("test")]
        public async Task<IActionResult> Test()
        {
            try
            {
                var kernel = _semanticKernelService.GetKernel();
                var result = await kernel.InvokePromptAsync("Reply with only the word: OK");
                var response = result.ToString().Trim();
                return Ok(new { Success = true, Message = $"Connection successful — model responded: {response}" });
            }
            catch (Exception ex)
            {
                return Ok(new { Success = false, Message = ex.Message });
            }
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
