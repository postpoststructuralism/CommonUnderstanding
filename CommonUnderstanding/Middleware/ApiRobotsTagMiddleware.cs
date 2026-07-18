using Microsoft.AspNetCore.Http.Features;

namespace CommonUnderstanding.Middleware;

/// <summary>
/// Middleware that adds X-Robots-Tag: noindex to API routes to prevent
/// search engines and crawlers from indexing JSON API responses.
/// This stops crawlers from triggering expensive graph data queries.
/// </summary>
public class ApiRobotsTagMiddleware
{
    private readonly RequestDelegate _next;

    public ApiRobotsTagMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Add X-Robots-Tag to all /api/ routes to prevent crawler indexing
        if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.OnStarting(() =>
            {
                context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
                return Task.CompletedTask;
            });
        }

        await _next(context);
    }
}