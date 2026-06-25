using Microsoft.Extensions.DependencyInjection;

namespace CommonUnderstanding.Data;

/// <summary>
/// Singleton-friendly wrapper that creates ApplicationDbContext instances via IServiceScopeFactory.
/// Safe for use in HostedService workers and SignalR hubs that run as singletons.
/// Each call creates a new DI scope; disposing the returned context also disposes the scope.
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
    /// Dispose the returned context to also dispose the scope.
    /// </summary>
    public ApplicationDbContext CreateDbContext()
    {
        var scope = _scopeFactory.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }

    /// <summary>
    /// Async version of CreateDbContext for convenience.
    /// </summary>
    public ValueTask<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(CreateDbContext());
    }
}