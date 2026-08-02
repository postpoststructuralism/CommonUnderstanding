using System.Collections.Concurrent;
namespace CommonUnderstanding.Services;

public class RuntimeAiConfigService
{
    private readonly object _lock = new();
    private string? _endpoint = null;
    private string? _model = null;
    private string? _proModel = null;
    private string? _agent = null;

    public string? Endpoint
    {
        get { lock (_lock) { return _endpoint; } }
        set { lock (_lock) { _endpoint = value; } }
    }

    public string? Model
    {
        get { lock (_lock) { return _model; } }
        set { lock (_lock) { _model = value; } }
    }

    public string? ProModel
    {
        get { lock (_lock) { return _proModel; } }
        set { lock (_lock) { _proModel = value; } }
    }

    public string? Agent
    {
        get { lock (_lock) { return _agent; } }
        set { lock (_lock) { _agent = value; } }
    }
}
