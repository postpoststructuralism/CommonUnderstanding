using Microsoft.Extensions.AI;

namespace CommonUnderstanding.Services.Social;

/// <summary>
/// Wraps the Microsoft.Extensions.AI embedding generator for use in Phase 2 plugins.
/// Falls back gracefully when the embedding model is unavailable.
/// </summary>
public class EmbeddingService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EmbeddingService> _logger;

    public EmbeddingService(IServiceProvider serviceProvider, ILogger<EmbeddingService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Generates a 1536-dimensional embedding for the given text.
    /// Returns null if the embedding generator is unavailable.
    /// </summary>
    public async Task<float[]?> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        try
        {
            var generator = _serviceProvider.GetService<IEmbeddingGenerator<string, Embedding<float>>>();
            if (generator is null)
            {
                _logger.LogDebug("Embedding generator not registered; skipping embedding generation.");
                return null;
            }

            var result = await generator.GenerateAsync(new[] { text }, cancellationToken: ct);
            return result.Count > 0 ? result[0].Vector.ToArray() : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Embedding generation failed for text of length {Length}.", text.Length);
            return null;
        }
    }

    /// <summary>
    /// Generates embeddings for multiple texts in a single batch call.
    /// </summary>
    public async Task<float[]?[]> GenerateEmbeddingsAsync(IList<string> texts, CancellationToken ct = default)
    {
        if (texts.Count == 0) return Array.Empty<float[]?>();

        try
        {
            var generator = _serviceProvider.GetService<IEmbeddingGenerator<string, Embedding<float>>>();
            if (generator is null) return new float[]?[texts.Count];

            var results = await generator.GenerateAsync(texts, cancellationToken: ct);
            return results.Select(e => (float[]?)e.Vector.ToArray()).ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Batch embedding generation failed.");
            return new float[]?[texts.Count];
        }
    }
}
