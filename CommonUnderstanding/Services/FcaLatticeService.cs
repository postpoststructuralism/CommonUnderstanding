using System.Text.Json;
using CommonUnderstanding.Data;
using CommonUnderstanding.Models;
using Microsoft.EntityFrameworkCore;

namespace CommonUnderstanding.Services;

/// <summary>
/// Formal Concept Analysis (FCA) — discovers concept lattices from the
/// Arguments × Propositions incidence matrix.
///
/// A formal concept is a maximal set of arguments (extent) sharing a maximal
/// set of propositions (intent). The lattice reveals the hierarchical structure
/// of understanding: broad, abstract concepts at the top (shared by many arguments)
/// and narrow, specific concepts at the bottom (unique to few arguments).
///
/// Algorithm: NextClosure (Ganter, 1984) for computing all formal concepts.
/// </summary>
public class FcaLatticeService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ILogger<FcaLatticeService> _logger;

    public FcaLatticeService(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        ILogger<FcaLatticeService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    // ── Formal Concept Discovery ──────────────────────────────────────────

    /// <summary>
    /// Builds the formal context (Arguments × Propositions incidence matrix)
    /// and computes all formal concepts using the NextClosure algorithm.
    /// </summary>
    public async Task<FcaLatticeResult> ComputeLatticeAsync()
    {
        await using var db = await _contextFactory.CreateDbContextAsync();

        var arguments = await db.Arguments.ToListAsync();
        var nodes = await db.UnderstandingNodes.ToListAsync();
        var edges = await db.UnderstandingEdges.ToListAsync();

        _logger.LogInformation("Building FCA lattice: {A} arguments, {P} propositions.",
            arguments.Count, nodes.Count);

        // Build incidence matrix: arguments × propositions
        // M[a, p] = 1 if argument a references proposition p
        var argIds = arguments.Select(a => a.Id).ToList();
        var nodeIds = nodes.Select(n => n.Id).ToList();

        var incidence = new HashSet<(int ArgId, int NodeId)>();
        foreach (var edge in edges)
        {
            // Check if edge provenance references an argument
            if (string.IsNullOrEmpty(edge.ProvenanceJson) || edge.ProvenanceJson == "{}") continue;

            try
            {
                var prov = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(edge.ProvenanceJson);
                if (prov == null) continue;

                // Extract argument IDs from provenance
                if (prov.TryGetValue("sharedArgumentIds", out var sharedIds))
                {
                    var ids = JsonSerializer.Deserialize<List<int>>(sharedIds.GetRawText());
                    if (ids != null)
                    {
                        foreach (var aid in ids)
                            if (argIds.Contains(aid))
                                incidence.Add((aid, edge.SourceNodeId));
                    }
                }

                if (prov.TryGetValue("sourceArgumentIds", out var srcIds))
                {
                    var ids = JsonSerializer.Deserialize<List<int>>(srcIds.GetRawText());
                    if (ids != null)
                    {
                        foreach (var aid in ids)
                            if (argIds.Contains(aid))
                                incidence.Add((aid, edge.SourceNodeId));
                    }
                }
            }
            catch { /* skip malformed provenance */ }
        }

        // Also check argument IDs stored on nodes
        foreach (var node in nodes)
        {
            try
            {
                var nodeArgIds = JsonSerializer.Deserialize<List<int>>(node.ArgumentIdsJson);
                if (nodeArgIds != null)
                {
                    foreach (var aid in nodeArgIds)
                        if (argIds.Contains(aid))
                            incidence.Add((aid, node.Id));
                }
            }
            catch { /* skip */ }
        }

        _logger.LogInformation("Incidence matrix: {Entries} non-zero entries.", incidence.Count);

        // Convert to bit arrays for efficient NextClosure
        int m = argIds.Count;
        int n = nodeIds.Count;
        var argIndex = argIds.Select((id, idx) => (id, idx)).ToDictionary(x => x.id, x => x.idx);
        var nodeIndex = nodeIds.Select((id, idx) => (id, idx)).ToDictionary(x => x.id, x => x.idx);

        // For each argument, store the set of propositions it references as a bitmask
        var argMasks = new long[m][];
        int words = (n + 63) / 64;
        for (int i = 0; i < m; i++)
        {
            argMasks[i] = new long[words];
            var argId = argIds[i];
            foreach (var (aid, nid) in incidence.Where(x => x.ArgId == argId))
            {
                if (nodeIndex.TryGetValue(nid, out int ni))
                    argMasks[i][ni / 64] |= 1L << (ni % 64);
            }
        }

        // Compute all formal concepts using NextClosure
        var concepts = new List<FormalConcept>();
        var currentIntent = new long[words]; // Start with empty intent (all attributes)

        while (true)
        {
            // Compute extent: all arguments that share this intent
            var extent = new List<int>();
            for (int i = 0; i < m; i++)
            {
                bool allMatch = true;
                for (int w = 0; w < words; w++)
                    if ((argMasks[i][w] & currentIntent[w]) != currentIntent[w])
                    { allMatch = false; break; }
                if (allMatch) extent.Add(i);
            }

            // Compute intent from extent (closure)
            var newIntent = new long[words];
            if (extent.Count > 0)
            {
                // Start with all attributes present in first extent argument
                Array.Copy(argMasks[extent[0]], newIntent, words);
                for (int i = 1; i < extent.Count; i++)
                    for (int w = 0; w < words; w++)
                        newIntent[w] &= argMasks[extent[i]][w];
            }

            // Check if this is a new concept (extent, intent pair)
            bool isNew = true;
            foreach (var c in concepts)
            {
                bool sameExtent = c.Extent.Count == extent.Count && !c.Extent.Except(extent).Any();
                if (sameExtent) { isNew = false; break; }
            }

            if (isNew && extent.Count > 0)
            {
                var propIds = new List<int>();
                for (int p = 0; p < n; p++)
                    if ((newIntent[p / 64] & (1L << (p % 64))) != 0)
                        propIds.Add(nodeIds[p]);

                concepts.Add(new FormalConcept
                {
                    Extent = extent.Select(i => argIds[i]).ToList(),
                    Intent = propIds,
                    Size = extent.Count + propIds.Count
                });
            }

            // NextClosure: find the next intent in lexicographic order
            bool found = false;
            for (int p = n - 1; p >= 0; p--)
            {
                if ((currentIntent[p / 64] & (1L << (p % 64))) != 0)
                {
                    // Clear this bit and all bits after it
                    currentIntent[p / 64] &= ~(1L << (p % 64));
                    for (int q = p + 1; q < n; q++)
                        currentIntent[q / 64] &= ~(1L << (q % 64));
                    continue;
                }

                // Try setting this bit
                currentIntent[p / 64] |= 1L << (p % 64);

                // Compute closure of this candidate intent
                var candidateExtent = new List<int>();
                for (int i = 0; i < m; i++)
                {
                    bool allMatch = true;
                    for (int w = 0; w < words; w++)
                        if ((argMasks[i][w] & currentIntent[w]) != currentIntent[w])
                        { allMatch = false; break; }
                    if (allMatch) candidateExtent.Add(i);
                }

                var closedIntent = new long[words];
                if (candidateExtent.Count > 0)
                {
                    Array.Copy(argMasks[candidateExtent[0]], closedIntent, words);
                    for (int i = 1; i < candidateExtent.Count; i++)
                        for (int w = 0; w < words; w++)
                            closedIntent[w] &= argMasks[candidateExtent[i]][w];
                }

                // Check if closure equals the candidate
                bool isClosed = true;
                for (int w = 0; w < words; w++)
                    if (closedIntent[w] != currentIntent[w])
                    { isClosed = false; break; }

                if (isClosed)
                {
                    currentIntent = closedIntent;
                    found = true;
                    break;
                }

                // Reset and continue
                currentIntent[p / 64] &= ~(1L << (p % 64));
            }

            if (!found) break; // No more concepts
        }

        // Build concept lattice hierarchy (parent-child relationships)
        BuildLatticeHierarchy(concepts);

        // Persist as ConceptualSchemas
        var schemas = await PersistConceptsAsync(db, concepts, nodeIds);

        _logger.LogInformation("FCA complete: {Concepts} formal concepts, {Schemas} schemas persisted.",
            concepts.Count, schemas.Count);

        return new FcaLatticeResult
        {
            Concepts = concepts,
            Schemas = schemas,
            ArgumentCount = m,
            PropositionCount = n
        };
    }

    // ── Lattice Hierarchy ─────────────────────────────────────────────────

    private static void BuildLatticeHierarchy(List<FormalConcept> concepts)
    {
        // Sort by extent size descending (most general first)
        concepts.Sort((a, b) => b.Extent.Count.CompareTo(a.Extent.Count));

        for (int i = 0; i < concepts.Count; i++)
        {
            for (int j = 0; j < concepts.Count; j++)
            {
                if (i == j) continue;

                // Check if concept i is a subconcept of concept j
                // (i's extent is subset of j's extent, i's intent is superset of j's intent)
                bool extentSubset = concepts[i].Extent.All(id => concepts[j].Extent.Contains(id));
                bool intentSuperset = concepts[j].Intent.All(id => concepts[i].Intent.Contains(id));

                if (extentSubset && intentSuperset)
                {
                    // Check if there's an intermediate concept
                    bool direct = true;
                    for (int k = 0; k < concepts.Count; k++)
                    {
                        if (k == i || k == j) continue;
                        bool kExtentSubset = concepts[i].Extent.All(id => concepts[k].Extent.Contains(id));
                        bool kIntentSuperset = concepts[j].Intent.All(id => concepts[k].Intent.Contains(id));
                        bool jExtentSubset = concepts[k].Extent.All(id => concepts[j].Extent.Contains(id));
                        bool jIntentSuperset = concepts[k].Intent.All(id => concepts[i].Intent.Contains(id));
                        if (kExtentSubset && kIntentSuperset && jExtentSubset && jIntentSuperset)
                        { direct = false; break; }
                    }

                    if (direct)
                    {
                        concepts[i].ParentIndices.Add(j);
                        concepts[j].ChildIndices.Add(i);
                    }
                }
            }
        }
    }

    // ── Persistence ───────────────────────────────────────────────────────

    private async Task<List<ConceptualSchema>> PersistConceptsAsync(
        ApplicationDbContext db, List<FormalConcept> concepts, List<int> nodeIds)
    {
        var schemas = new List<ConceptualSchema>();

        foreach (var concept in concepts)
        {
            if (concept.Intent.Count < 2) continue; // Skip trivial concepts

            var schema = new ConceptualSchema
            {
                Label = $"FCA Concept ({concept.Intent.Count} propositions, {concept.Extent.Count} arguments)",
                Description = $"Formal concept discovered via FCA. " +
                    $"Extent: {concept.Extent.Count} arguments, " +
                    $"Intent: {concept.Intent.Count} propositions.",
                DiscoveryMethod = "fca_lattice",
                Coherence = Math.Round((double)concept.Intent.Count / (concept.Intent.Count + concept.Extent.Count), 4),
                Stability = 0.0,
                DiscoveredAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow
            };

            db.ConceptualSchemas.Add(schema);
            await db.SaveChangesAsync();

            foreach (var nodeId in concept.Intent)
            {
                db.SchemaMemberships.Add(new SchemaMembership
                {
                    NodeId = nodeId,
                    SchemaId = schema.Id,
                    Weight = Math.Round(1.0 / concept.Intent.Count, 4)
                });
            }

            schemas.Add(schema);
        }

        await db.SaveChangesAsync();
        return schemas;
    }
}

// ── Supporting types ─────────────────────────────────────────────────────────

/// <summary>A formal concept: a maximal set of arguments sharing a maximal set of propositions.</summary>
public class FormalConcept
{
    public List<int> Extent { get; set; } = new(); // Argument IDs
    public List<int> Intent { get; set; } = new();  // Proposition (node) IDs
    public int Size { get; set; }
    public List<int> ParentIndices { get; set; } = new();
    public List<int> ChildIndices { get; set; } = new();
}

/// <summary>Result of FCA lattice computation.</summary>
public class FcaLatticeResult
{
    public List<FormalConcept> Concepts { get; set; } = new();
    public List<ConceptualSchema> Schemas { get; set; } = new();
    public int ArgumentCount { get; set; }
    public int PropositionCount { get; set; }
    public int LatticeDepth =>
        Concepts.Count > 0 ? Concepts.Max(c => c.ParentIndices.Count) : 0;
}