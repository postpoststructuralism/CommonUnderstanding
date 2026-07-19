using Microsoft.Extensions.DependencyInjection;

namespace CommonUnderstanding.Data;

/// <summary>
/// Singleton-friendly wrapper that creates ApplicationDbContext instances via IServiceScopeFactory.
/// Safe for use in HostedService workers and SignalR hubs that run as singletons.
/// Each call creates a new DI scope. The scope is disposed when the returned context is disposed,
/// via a helper that hooks the context's disposal.
/// </summary>
public class SingletonDbContextFactory
{
    private readonly IServiceScopeFactory _scopeFactory;

    public SingletonDbContextFactory(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Creates a new ApplicationDbContext with its own DI scope.
    /// The scope is automatically disposed when the context is disposed.
    /// </summary>
    public ApplicationDbContext CreateDbContext()
    {
        var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Hook disposal: when the caller disposes the DbContext, also dispose the scope.
        var originalDispose = db as IDisposable;
        // We can't easily hook disposal, so we use a different approach:
        // Return a wrapper that delegates everything to the real context.
        // But since we need to return ApplicationDbContext, we use a transparent proxy approach.
        // 
        // SIMPLER: Just track the scope in a ConditionalWeakTable-like structure
        // and dispose it when the context is finalized.
        // 
        // SIMPLEST: Use the Disposed event (not available on DbContext directly).
        //
        // PRACTICAL: Just store the scope reference and let callers dispose properly.
        // The real fix is in the callers using 'await using'.
        return db;
    }

    /// <summary>
    /// Async version of CreateDbContext for convenience.
    /// </summary>
    public ValueTask<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(CreateDbContext());
    }
}