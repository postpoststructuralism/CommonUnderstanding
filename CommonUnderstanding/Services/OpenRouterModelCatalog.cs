namespace CommonUnderstanding.Services;

/// <summary>
/// Bootstrap defaults used only before the live OpenRouter catalog can be fetched.
/// </summary>
public static class OpenRouterModelCatalog
{
    public const string DefaultModelId = "deepseek/deepseek-r1-distill-llama-8b:free";

    public static readonly string[] BootstrapModels =
    [
        // Less-congested models come first — they see far fewer free-tier users
        "deepseek/deepseek-r1-distill-llama-8b:free",  // DeepSeek R1 distill — low traffic
        "deepseek/deepseek-r1-distill-qwen-14b:free",  // 14B distill — quality + low traffic
        "nvidia/llama-3.1-nemotron-nano-8b-v1:free",   // Nemotron — rarely hammered
        "qwen/qwen3-8b:free",                           // Qwen3 8B — moderate traffic
        "google/gemma-3-12b-it:free",                   // Gemma 3 12B — moderate traffic
        "qwen/qwen-2.5-7b-instruct:free",               // Qwen 2.5 7B
        "nousresearch/hermes-3-llama-3.1-8b:free",     // instruction-tuned 8B
        "mistralai/mistral-7b-instruct:free",           // classic 7B — high traffic, last resort
        "meta-llama/llama-3.1-8b-instruct:free",        // very popular, most likely 429'd
        "meta-llama/llama-3.3-70b-instruct:free",       // 70B — most capable, slowest
    ];

    public static IReadOnlyList<string> AvailableModels => BootstrapModels;
}

/// <summary>
/// Free Gemini models available via Google AI Studio, fastest first.
/// </summary>
public static class GeminiModelCatalog
{
    public const string DefaultModelId = "gemini-2.0-flash-lite";

    public static readonly string[] AvailableModels =
    [
        "gemini-2.0-flash-lite",  // fastest, most generous free quota
        "gemini-2.0-flash",       // fast, slightly higher quality
        "gemini-1.5-flash-8b",    // smallest 1.5 model
        "gemini-1.5-flash",       // reliable 1.5 flash
    ];
}

/// <summary>
/// Recommended local Ollama models, fastest/smallest first.
/// Run: <c>ollama pull &lt;model&gt;</c> to download.
/// </summary>
public static class OllamaModelCatalog
{
    public const string DefaultModelId = "llama3.2:3b";

    public static readonly string[] AvailableModels =
    [
        "llama3.2:3b",   // 2 GB — fastest Llama
        "qwen2.5:3b",    // 2 GB — fast + strong reasoning
        "gemma3:4b",     // 3.3 GB — Google Gemma 3
        "phi4-mini:3.8b",// 2.5 GB — Microsoft Phi-4 Mini
        "llama3.2:1b",   // 1.3 GB — smallest possible
    ];
}
