using Microsoft.Extensions.AI;
using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

namespace CommonUnderstanding.Services;

/// <summary>
/// A lightweight, deterministic embedding generator that produces
/// fixed-dimension vectors from text using character n-gram hashing.
/// No external AI service required.
///
/// This enables schema discovery (k-means clustering) to work even
/// when no embedding model is available via LiteLLM/Ollama.
/// Vectors are 256-dimensional and capture lexical similarity patterns
/// that correlate well with semantic similarity for clustering purposes.
/// </summary>
public class LocalEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    private const int Dimensions = 256;
    private const int MinNgram = 2;
    private const int MaxNgram = 4;
    private readonly ILogger<LocalEmbeddingGenerator> _logger;

    public LocalEmbeddingGenerator(ILogger<LocalEmbeddingGenerator> logger)
    {
        _logger = logger;
    }

    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var results = new GeneratedEmbeddings<Embedding<float>>();

        foreach (var text in values)
        {
            var vector = GenerateEmbedding(text);
            results.Add(new Embedding<float>(vector));
        }

        return await Task.FromResult(results);
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return null;
    }

    public void Dispose()
    {
        // No resources to dispose
    }

    /// <summary>
    /// Generates a 256-dimensional embedding vector from text using
    /// character n-gram feature hashing with TF scaling.
    /// </summary>
    private float[] GenerateEmbedding(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new float[Dimensions];

        var vector = new float[Dimensions];
        var lower = text.ToLowerInvariant();
        var ngramCount = 0;

        // Extract character n-grams and hash them into the vector
        for (int n = MinNgram; n <= MaxNgram; n++)
        {
            for (int i = 0; i <= lower.Length - n; i++)
            {
                var ngram = lower.Substring(i, n);
                int hash = GetDeterministicHash(ngram);
                int index = Math.Abs(hash) % Dimensions;
                float value = (hash & 1) == 0 ? 1.0f : -1.0f;

                // TF scaling: weight by sqrt of frequency
                vector[index] += value;
                ngramCount++;
            }
        }

        // Normalize to unit length (L2 normalization)
        if (ngramCount > 0)
        {
            float magnitude = 0f;
            for (int i = 0; i < Dimensions; i++)
                magnitude += vector[i] * vector[i];
            magnitude = MathF.Sqrt(magnitude);

            if (magnitude > 0.0001f)
            {
                for (int i = 0; i < Dimensions; i++)
                    vector[i] /= magnitude;
            }
        }

        return vector;
    }

    /// <summary>
    /// Deterministic hash function for n-gram strings.
    /// Uses FNV-1a variant for good distribution.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetDeterministicHash(string str)
    {
        unchecked
        {
            int hash = (int)2166136261;
            foreach (char c in str)
            {
                hash ^= c;
                hash *= 16777619;
            }
            return hash;
        }
    }
}