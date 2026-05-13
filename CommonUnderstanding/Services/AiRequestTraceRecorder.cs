using System.Text;
using System.Text.Json;

namespace CommonUnderstanding.Services;

public sealed class AiRequestTraceRecorder
{
    private const string ItemKey = "AiRequestTrace";
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AiRequestTraceRecorder(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void RecordAttempt(string provider, string model, string promptPreview, bool isStreaming)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
            return;

        if (httpContext.Items[ItemKey] is not List<AiTraceEntry> entries)
        {
            entries = [];
            httpContext.Items[ItemKey] = entries;
        }

        entries.Add(new AiTraceEntry(
            Provider: provider,
            Model: model,
            PromptPreview: TrimPrompt(promptPreview),
            IsStreaming: isStreaming,
            TimestampUtc: DateTime.UtcNow));
    }

    public void WriteResponseHeaders(HttpContext httpContext)
    {
        if (httpContext.Items[ItemKey] is not List<AiTraceEntry> entries || entries.Count == 0)
            return;

        var payload = JsonSerializer.Serialize(entries);
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));

        httpContext.Response.Headers["X-AI-Trace"] = encoded;
        httpContext.Response.Headers.Append("Access-Control-Expose-Headers", "X-AI-Trace");
    }

    private static string TrimPrompt(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return "(empty prompt)";

        var normalized = prompt.Replace("\r", " ").Replace("\n", " ").Trim();
        return normalized.Length <= 500
            ? normalized
            : normalized[..500] + "...";
    }
}

public sealed record AiTraceEntry(
    string Provider,
    string Model,
    string PromptPreview,
    bool IsStreaming,
    DateTime TimestampUtc);