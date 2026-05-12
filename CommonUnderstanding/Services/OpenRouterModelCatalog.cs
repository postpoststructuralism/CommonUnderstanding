namespace CommonUnderstanding.Services;

public static class OpenRouterModelCatalog
{
    public const string DefaultModelId = "meta-llama/llama-3.1-8b-instruct:free";

    /// <summary>
    /// Ordered list of free models to try. The HTTP handler automatically cycles through
    /// these on 404 so a model going offline is transparent to the application.
    /// Smaller/faster models are listed first to minimise free-tier latency.
    /// </summary>
    public static readonly string[] AvailableModels =
    [
        DefaultModelId,
        "mistralai/mistral-7b-instruct:free",
        "qwen/qwen-2.5-7b-instruct:free",
        "google/gemma-2-9b-it:free",
        "nousresearch/hermes-3-llama-3.1-8b:free",
        "microsoft/phi-3-mini-128k-instruct:free",
        "openchat/openchat-7b:free",
        "meta-llama/llama-3.3-70b-instruct:free"
    ];

    public static bool IsValid(string? modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId)) return false;
        return AvailableModels.Contains(modelId, StringComparer.Ordinal);
    }

    /// <summary>Returns the first catalog model not yet tried, or null if all are exhausted.</summary>
    public static string? GetNextModel(IReadOnlySet<string> triedModels)
        => AvailableModels.FirstOrDefault(m => !triedModels.Contains(m));
}