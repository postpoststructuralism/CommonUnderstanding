using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace CommonUnderstanding.Services;

/// <summary>
/// One-time generator for synthetic decorative node/edge clusters.
/// Creates procedurally-generated nodes and edges that are visually indistinguishable
/// from real data at a glance, using the same color groups. Every synthetic node/edge
/// is tagged with synthetic:true and a stable id prefix so they can be filtered or
/// swapped out later.
///
/// Run once manually: dotnet run --generate-synthetic-filler
/// The output file (wwwroot/data/synthetic-filler.json) is versioned in source control
/// and regenerated only when the aesthetic needs to change.
/// </summary>
public class SyntheticFillerGenerator
{
    private readonly string _outputPath;

    public SyntheticFillerGenerator(IWebHostEnvironment env)
    {
        var dataDir = Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "data");
        _outputPath = Path.Combine(dataDir, "synthetic-filler.json");
    }

    public void Generate()
    {
        var rng = new Random(12345); // deterministic seed
        var nodes = new List<object>();
        var edges = new List<object>();
        int nextId = 1;

        // Color groups matching the real node color scheme
        var colorGroups = new[]
        {
            new { Background = "#059669", Border = "#34d399", Group = "Settled" },
            new { Background = "#d97706", Border = "#f59e0b", Group = "Contested" },
            new { Background = "#0f766e", Border = "#14b8a6", Group = "Unevaluated" },
            new { Background = "#667487", Border = "#8b95a5", Group = "Unknown" }
        };

        // ── Cluster 1: Dense tight cluster (12 nodes) ──
        var cluster1Center = (X: -400.0, Y: -200.0);
        var cluster1Ids = new List<string>();
        for (int i = 0; i < 12; i++)
        {
            var id = $"synthetic-{nextId++}";
            cluster1Ids.Add(id);
            var color = colorGroups[rng.Next(colorGroups.Length)];
            var label = SyntheticLabels.GetLabel(rng);
            nodes.Add(new
            {
                id,
                label,
                socialArgumentId = SyntheticLabels.GetSocialArgumentId(label),
                confidence = Math.Round(0.5 + rng.NextDouble() * 0.4, 3),
                degreeCentrality = Math.Round(0.1 + rng.NextDouble() * 0.3, 3),
                betweennessCentrality = Math.Round(rng.NextDouble() * 0.2, 3),
                clusteringCoefficient = Math.Round(0.3 + rng.NextDouble() * 0.5, 3),
                dialecticalTemperature = Math.Round(rng.NextDouble() * 0.4, 3),
                controversyScore = Math.Round(rng.NextDouble() * 0.3, 3),
                schemaEntropy = Math.Round(rng.NextDouble() * 0.3, 3),
                status = color.Group,
                evidenceCount = rng.Next(1, 8),
                synthetic = true,
                x = cluster1Center.X + (rng.NextDouble() - 0.5) * 120,
                y = cluster1Center.Y + (rng.NextDouble() - 0.5) * 120,
                @fixed = true
            });
        }
        // Fully connect cluster 1 internally
        for (int i = 0; i < cluster1Ids.Count; i++)
        {
            for (int j = i + 1; j < cluster1Ids.Count; j++)
            {
                if (rng.NextDouble() < 0.7)
                {
                    edges.Add(new
                    {
                        id = $"synthetic-e-{nextId++}",
                        sourceId = cluster1Ids[i],
                        targetId = cluster1Ids[j],
                        edgeType = "supports",
                        weight = Math.Round(0.4 + rng.NextDouble() * 0.5, 3),
                        synthetic = true
                    });
                }
            }
        }

        // ── Cluster 2: Medium cluster (8 nodes) ──
        var cluster2Center = (X: 300.0, Y: 250.0);
        var cluster2Ids = new List<string>();
        for (int i = 0; i < 8; i++)
        {
            var id = $"synthetic-{nextId++}";
            cluster2Ids.Add(id);
            var color = colorGroups[rng.Next(colorGroups.Length)];
            var label = SyntheticLabels.GetLabel(rng);
            nodes.Add(new
            {
                id,
                label,
                socialArgumentId = SyntheticLabels.GetSocialArgumentId(label),
                confidence = Math.Round(0.5 + rng.NextDouble() * 0.4, 3),
                degreeCentrality = Math.Round(0.1 + rng.NextDouble() * 0.3, 3),
                betweennessCentrality = Math.Round(rng.NextDouble() * 0.2, 3),
                clusteringCoefficient = Math.Round(0.3 + rng.NextDouble() * 0.5, 3),
                dialecticalTemperature = Math.Round(rng.NextDouble() * 0.4, 3),
                controversyScore = Math.Round(rng.NextDouble() * 0.3, 3),
                schemaEntropy = Math.Round(rng.NextDouble() * 0.3, 3),
                status = color.Group,
                evidenceCount = rng.Next(1, 8),
                synthetic = true,
                x = cluster2Center.X + (rng.NextDouble() - 0.5) * 100,
                y = cluster2Center.Y + (rng.NextDouble() - 0.5) * 100,
                @fixed = true
            });
        }
        for (int i = 0; i < cluster2Ids.Count; i++)
        {
            for (int j = i + 1; j < cluster2Ids.Count; j++)
            {
                if (rng.NextDouble() < 0.65)
                {
                    edges.Add(new
                    {
                        id = $"synthetic-e-{nextId++}",
                        sourceId = cluster2Ids[i],
                        targetId = cluster2Ids[j],
                        edgeType = "supports",
                        weight = Math.Round(0.4 + rng.NextDouble() * 0.5, 3),
                        synthetic = true
                    });
                }
            }
        }

        // ── Sparse triangles (3 groups of 3 nodes) ──
        for (int t = 0; t < 3; t++)
        {
            var cx = -200 + t * 250;
            var cy = 400 + (t % 2) * 100;
            var triIds = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                var id = $"synthetic-{nextId++}";
                triIds.Add(id);
                var color = colorGroups[rng.Next(colorGroups.Length)];
                var label = SyntheticLabels.GetLabel(rng);
                nodes.Add(new
                {
                    id,
                    label,
                    socialArgumentId = SyntheticLabels.GetSocialArgumentId(label),
                    confidence = Math.Round(0.5 + rng.NextDouble() * 0.4, 3),
                    degreeCentrality = Math.Round(0.05 + rng.NextDouble() * 0.15, 3),
                    betweennessCentrality = Math.Round(rng.NextDouble() * 0.1, 3),
                    clusteringCoefficient = Math.Round(0.2 + rng.NextDouble() * 0.3, 3),
                    dialecticalTemperature = Math.Round(rng.NextDouble() * 0.4, 3),
                    controversyScore = Math.Round(rng.NextDouble() * 0.3, 3),
                    schemaEntropy = Math.Round(rng.NextDouble() * 0.3, 3),
                    status = color.Group,
                    evidenceCount = rng.Next(1, 5),
                    synthetic = true,
                    x = cx + (rng.NextDouble() - 0.5) * 80,
                    y = cy + (rng.NextDouble() - 0.5) * 80,
                    @fixed = true
                });
            }
            // Connect triangle
            edges.Add(new { id = $"synthetic-e-{nextId++}", sourceId = triIds[0], targetId = triIds[1], edgeType = "supports", weight = Math.Round(0.3 + rng.NextDouble() * 0.4, 3), synthetic = true });
            edges.Add(new { id = $"synthetic-e-{nextId++}", sourceId = triIds[1], targetId = triIds[2], edgeType = "qualifies", weight = Math.Round(0.3 + rng.NextDouble() * 0.4, 3), synthetic = true });
            edges.Add(new { id = $"synthetic-e-{nextId++}", sourceId = triIds[2], targetId = triIds[0], edgeType = "extends", weight = Math.Round(0.3 + rng.NextDouble() * 0.4, 3), synthetic = true });
        }

        // ── Isolated nodes (scattered) ──
        for (int i = 0; i < 8; i++)
        {
            var id = $"synthetic-{nextId++}";
            var color = colorGroups[rng.Next(colorGroups.Length)];
            var label = SyntheticLabels.GetLabel(rng);
            nodes.Add(new
            {
                id,
                label,
                socialArgumentId = SyntheticLabels.GetSocialArgumentId(label),
                confidence = Math.Round(0.3 + rng.NextDouble() * 0.3, 3),
                degreeCentrality = Math.Round(rng.NextDouble() * 0.05, 3),
                betweennessCentrality = 0.0,
                clusteringCoefficient = 0.0,
                dialecticalTemperature = Math.Round(rng.NextDouble() * 0.3, 3),
                controversyScore = Math.Round(rng.NextDouble() * 0.2, 3),
                schemaEntropy = Math.Round(rng.NextDouble() * 0.2, 3),
                status = color.Group,
                evidenceCount = rng.Next(0, 3),
                synthetic = true,
                x = (rng.NextDouble() - 0.5) * 800,
                y = (rng.NextDouble() - 0.5) * 600,
                @fixed = true
            });
        }

        // ── Cross-cluster bridge edges ──
        if (cluster1Ids.Count > 0 && cluster2Ids.Count > 0)
        {
            for (int i = 0; i < 3; i++)
            {
                edges.Add(new
                {
                    id = $"synthetic-e-{nextId++}",
                    sourceId = cluster1Ids[rng.Next(cluster1Ids.Count)],
                    targetId = cluster2Ids[rng.Next(cluster2Ids.Count)],
                    edgeType = "contradicts",
                    weight = Math.Round(0.2 + rng.NextDouble() * 0.3, 3),
                    synthetic = true
                });
            }
        }

        var output = new { nodes, edges, generatedAt = DateTime.UtcNow.ToString("o"), synthetic = true };
        var json = JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true });

        Directory.CreateDirectory(Path.GetDirectoryName(_outputPath)!);
        File.WriteAllText(_outputPath, json);

        Console.WriteLine($"Synthetic filler generated: {nodes.Count} nodes, {edges.Count} edges -> {_outputPath}");
    }
}

/// <summary>
/// Pool of plausible-sounding labels for synthetic nodes.
/// These are intentionally generic so they blend in with real data.
/// </summary>
internal static class SyntheticLabels
{
    public static readonly string[] All = new[]
    {
        "Individual autonomy should be balanced with collective responsibility",
        "Free markets drive innovation more effectively than regulation",
        "Education is the primary driver of social mobility",
        "Technology should serve human flourishing, not replace it",
        "Cultural diversity strengthens democratic institutions",
        "Economic growth must be decoupled from environmental degradation",
        "Healthcare is a fundamental human right",
        "Privacy is a prerequisite for intellectual freedom",
        "Scientific consensus should guide public policy",
        "Historical context is essential for understanding current events",
        "Community bonds are eroding in the digital age",
        "Meritocracy is an ideal, not a reality",
        "Tradition provides stability but can hinder progress",
        "Global cooperation is necessary for existential risks",
        "Language shapes thought more than we realize",
        "Power structures perpetuate inequality",
        "Art is essential for a healthy society",
        "Moral progress is possible but not inevitable",
        "Information quality determines decision quality",
        "Trust in institutions requires transparency",
        "Local governance is more responsive than centralized authority",
        "Intergenerational equity is a moral imperative",
        "Complex systems require humility in intervention",
        "Narrative shapes identity more than facts",
        "Competition and cooperation are both essential",
        "Rights come with corresponding responsibilities",
        "Uncertainty should be embraced, not eliminated",
        "Diversity of thought prevents groupthink",
        "Infrastructure investment yields long-term prosperity",
        "Mental health is as important as physical health"
    };

    public static string GetLabel(Random rng) => All[rng.Next(All.Length)];

    public static Guid GetSocialArgumentId(string label) => CreateStableId("argument", label);

    public static Guid GetClaimPropositionId(string label) => CreateStableId("claim", label);

    private static Guid CreateStableId(string kind, string label)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"synthetic-{kind}:{label}"));
        return new Guid(hash.AsSpan(0, 16));
    }
}