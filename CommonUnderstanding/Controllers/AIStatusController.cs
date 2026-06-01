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
        private readonly IConfiguration _configuration;
        private readonly RuntimeAiConfigService _runtimeConfig;

        public AIStatusController(
            SemanticKernelService semanticKernelService,
            IConfiguration configuration,
            RuntimeAiConfigService runtimeConfig)
        {
            _semanticKernelService = semanticKernelService;
            _configuration = configuration;
            _runtimeConfig = runtimeConfig;
        }

        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            var azureEndpoint = _runtimeConfig.Endpoint ?? _configuration["AzureFoundry:Endpoint"];
            var azureConfigured = !string.IsNullOrWhiteSpace(_configuration["AzureFoundry:ApiKey"]) &&
                                  !string.IsNullOrWhiteSpace(azureEndpoint);
            var azureModel = _semanticKernelService.ResolveAzurePrimaryModel();
            var secondaryModel = _configuration["AzureFoundry:SecondaryModelId"] ?? string.Empty;
            var useSecondaryFallback = bool.TryParse(_configuration["AzureFoundry:UseSecondaryFallback"], out var useSecondary)
                ? useSecondary
                : true;
            var ollamaEndpoint      = _configuration["Ollama:Endpoint"] ?? "http://localhost:11434";
            var ollamaModel         = _configuration["Ollama:Model"]    ?? OllamaModelCatalog.DefaultModelId;
            var ollamaFallbackEnabled = bool.TryParse(_configuration["Ollama:EnableFallback"], out var parsedEnableOllama)
                ? parsedEnableOllama
                : true;

            var activeProviders = new List<string>();
            if (azureConfigured) activeProviders.Add("AzureFoundryPrimary");
            if (azureConfigured && useSecondaryFallback && !string.IsNullOrWhiteSpace(secondaryModel))
                activeProviders.Add("AzureFoundrySecondary");
            if (ollamaFallbackEnabled) activeProviders.Add("Ollama");

            var systemStatus = activeProviders.Count > 0 ? "ready"
                             : "no-cloud-keys";

            try
            {
                return Ok(new
                {
                    Timestamp = DateTime.UtcNow,
                    SystemStatus = systemStatus,
                    FallbackChain = activeProviders,
                    AzureFoundry = new
                    {
                        Configured = azureConfigured,
                        Endpoint = azureEndpoint,
                        Model = azureModel,
                        SecondaryModel = secondaryModel,
                        UseSecondaryFallback = useSecondaryFallback
                    },
                    Ollama = new
                    {
                        Enabled = ollamaFallbackEnabled,
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
        public IActionResult SwitchModel([FromBody] SwitchModelRequest request)
        {
            if (string.IsNullOrEmpty(request.ModelName))
            {
                return BadRequest(new { Success = false, Message = "ModelName is required" });
            }

            _runtimeConfig.Model = request.ModelName;

            return Ok(new
            {
                Success = true,
                Message = "Runtime model override set",
                CurrentModel = _configuration["AzureFoundry:ModelId"],
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
