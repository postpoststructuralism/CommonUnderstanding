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
            var modelName = OpenRouterModelCatalog.IsValid(_runtimeConfig.Model)
                ? _runtimeConfig.Model!
                : (_configuration["OpenRouter:ModelId"] ?? OpenRouterModelCatalog.DefaultModelId);
            var agent = _runtimeConfig.Agent ?? _configuration["AIAgent"] ?? "openrouter";
            var geminiConfigured = !string.IsNullOrWhiteSpace(_configuration["OpenRouter:ApiKey"]);
            var modelAvailable = OpenRouterModelCatalog.IsValid(modelName);

            try
            {
                return Ok(new
                {
                    Timestamp = DateTime.UtcNow,
                    ModelName = modelName,
                    GeminiConfigured = geminiConfigured,
                    ModelAvailable = modelAvailable,
                    AvailableModels = OpenRouterModelCatalog.AvailableModels,
                    SystemStatus = geminiConfigured ? (modelAvailable ? "ready" : "ready") : "api-key-missing",
                    Agent = agent
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    Timestamp = DateTime.UtcNow,
                    ModelName = modelName,
                    GeminiConfigured = false,
                    ModelAvailable = false,
                    AvailableModels = new List<string>(),
                    SystemStatus = "error",
                    Error = ex.Message,
                    Agent = agent
                });
            }
        }

        [HttpPost("switch-model")]
        public IActionResult SwitchModel([FromBody] SwitchModelRequest request)
        {
            if (string.IsNullOrEmpty(request.ModelName))
            {
                return BadRequest(new { Success = false, Message = "ModelName is required" });
            }

            if (!OpenRouterModelCatalog.IsValid(request.ModelName))
            {
                _runtimeConfig.Model = null;
                return BadRequest(new
                {
                    Success = false,
                    Message = $"Unsupported OpenRouter model: {request.ModelName}",
                    AvailableModels = OpenRouterModelCatalog.AvailableModels
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
