using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using CommonUnderstanding.Services;
using CommonUnderstanding.Models;

namespace CommonUnderstanding.Controllers;

/// <summary>
/// MVC controller for the Understanding Graph — Phase 3 feature.
/// Provides schema exploration, dialectical navigation, node detail,
/// evolution timeline, and JSON API endpoints for the frontend.
/// </summary>
public class UnderstandingGraphController : Controller
{
    private readonly UnderstandingGraphService _graphService;
    private readonly UnderstandingQueryService _queryService;
    private readonly GraphSnapshotService _snapshotService;
    private readonly SchemaDiscoveryService _schemaService;
    private readonly DialecticalSynthesisService _synthesisService;
    private readonly SkeletonGeneratorService _skeletonGenerator;
    private readonly ILogger<UnderstandingGraphController> _logger;

    public UnderstandingGraphController(
        UnderstandingGraphService graphService,
        UnderstandingQueryService queryService,
        GraphSnapshotService snapshotService,
        SchemaDiscoveryService schemaService,
        DialecticalSynthesisService synthesisService,
        SkeletonGeneratorService skeletonGenerator,
        ILogger<UnderstandingGraphController> logger)
    {
        _graphService = graphService;
        _queryService = queryService;
        _snapshotService = snapshotService;
        _schemaService = schemaService;
        _synthesisService = synthesisService;
        _skeletonGenerator = skeletonGenerator;
        _logger = logger;
    }

    // ── Schema Explorer ───────────────────────────────────────────────────

    /// <summary>
    /// Main schema explorer page — shows all schemas with member counts,
    /// coherence scores, and the full graph map for visualization.
    /// </summary>
    public async Task<IActionResult> Index()
    {
        // Load only lightweight stats on initial page render.
        // Sidebar tab data (schemas, syntheses, bridges, blindspots) loads via AJAX.
        // Use the fast count-only query to avoid loading full entities.
        var stats = await _queryService.GetQuickStatsAsync();
        ViewBag.Statistics = stats;
        return View();
    }

    /// <summary>
    /// Schema detail page — shows all nodes in a schema with membership
    /// strengths and topology metrics.
    /// </summary>
    public async Task<IActionResult> Schema(int id)
    {
        var schemas = await _queryService.GetAllSchemasAsync();
        var schema = schemas.FirstOrDefault(s => s.Id == id);
        if (schema == null) return NotFound();

        var nodes = await _queryService.GetSchemaNodesAsync(id);
        var dialecticalPairs = await _queryService.GetDialecticalPairsAsync(0.3, 20);

        ViewBag.Schema = schema;
        ViewBag.Nodes = nodes;
        ViewBag.DialecticalPairs = dialecticalPairs;

        return View();
    }

    /// <summary>
    /// Node detail page — shows a single node with its schema memberships,
    /// connected edges, and dialectical relationships.
    /// </summary>
    public async Task<IActionResult> Node(int id)
    {
        var node = await _graphService.GetNodeWithEdgesAsync(id);
        if (node == null) return NotFound();

        var memberships = await _queryService.GetSchemaForNodeAsync(id);

        ViewBag.Node = node;
        ViewBag.Memberships = memberships;

        return View();
    }

    // ── Dialectical Navigator ─────────────────────────────────────────────

    /// <summary>
    /// Dialectical navigator page — shows thesis-antithesis-synthesis triads,
    /// dialectical hierarchy, and unresolved contradictions.
    /// </summary>
    public async Task<IActionResult> Navigator()
    {
        var dialecticalPairs = await _queryService.GetDialecticalPairsAsync(0.4, 100);
        var syntheses = await _queryService.GetAllSynthesesAsync();
        var blindspots = await _queryService.GetBlindspotsAsync(20);

        ViewBag.DialecticalPairs = dialecticalPairs;
        ViewBag.Syntheses = syntheses;
        ViewBag.Blindspots = blindspots;

        return View();
    }

    // ── Evolution Timeline ────────────────────────────────────────────────

    /// <summary>
    /// Evolution timeline page — presents snapshots as the map's version history.
    /// </summary>
    public async Task<IActionResult> Evolution(int? nodeId = null, int? schemaId = null)
    {
        var model = await _snapshotService.GetEvolutionHistoryAsync(50, nodeId, schemaId);
        ViewBag.NodeId = nodeId;
        ViewBag.SchemaId = schemaId;
        return View(model);
    }

    // ── JSON API Endpoints ────────────────────────────────────────────────

    /// <summary>
    /// Returns the full graph map as JSON for frontend visualization.
    /// </summary>
    [HttpGet("api/understanding-graph/map")]
    [OutputCache(Duration = 30)]
    public async Task<IActionResult> GetMap()
    {
        var map = await _queryService.GetMapAsync();
        return Json(map);
    }

    /// <summary>
    /// Returns only root nodes (high-confidence, high-centrality) for fast initial render.
    /// The frontend loads these first, then streams in leaf nodes.
    /// </summary>
    [HttpGet("api/understanding-graph/roots")]
    [OutputCache(Duration = 60)]
    public async Task<IActionResult> GetRootNodes(int count = 150)
    {
        var map = await _queryService.GetRootNodesAsync(count);
        return Json(map);
    }

    /// <summary>
    /// Returns leaf nodes (everything except the given root IDs) for progressive loading.
    /// If ViewportNodeIds is provided, only returns nodes in that set (viewport-scoped fetch).
    /// </summary>
    [HttpPost("api/understanding-graph/leaves")]
    [OutputCache(Duration = 30)]
    public async Task<IActionResult> GetLeafNodes([FromBody] LeafNodesRequest request)
    {
        if (request.RootNodeIds == null || request.RootNodeIds.Count == 0)
            return Json(new GraphMap()); // nothing to add

        var rootIdSet = new HashSet<int>(request.RootNodeIds);
        GraphMap map;

        if (request.ViewportNodeIds != null && request.ViewportNodeIds.Count > 0)
        {
            // Viewport-scoped: only fetch the specific nodes the client asked for
            var viewportIdSet = new HashSet<int>(request.ViewportNodeIds);
            map = await _queryService.GetLeafNodesByViewportAsync(rootIdSet, viewportIdSet, request.MaxNodes > 0 ? request.MaxNodes : 2000);
        }
        else
        {
            map = await _queryService.GetLeafNodesAsync(rootIdSet, request.MaxNodes > 0 ? request.MaxNodes : 2000);
        }

        return Json(map);
    }

    /// <summary>
    /// Returns schema details as JSON.
    /// </summary>
    [HttpGet("api/understanding-graph/schemas")]
    [OutputCache(Duration = 30)]
    public async Task<IActionResult> GetSchemas()
    {
        var schemas = await _queryService.GetAllSchemasAsync();
        return Json(schemas);
    }

    /// <summary>
    /// Returns schema nodes as JSON.
    /// </summary>
    [HttpGet("api/understanding-graph/schema/{id}/nodes")]
    [OutputCache(Duration = 30, VaryByRouteValueNames = new[] { "id" })]
    public async Task<IActionResult> GetSchemaNodes(int id)
    {
        var nodes = await _queryService.GetSchemaNodesAsync(id);
        return Json(nodes);
    }

    /// <summary>
    /// Returns syntheses as JSON.
    /// </summary>
    [HttpGet("api/understanding-graph/syntheses")]
    [OutputCache(Duration = 30)]
    public async Task<IActionResult> GetSyntheses()
    {
        var syntheses = await _queryService.GetAllSynthesesAsync();
        return Json(syntheses);
    }

    /// <summary>
    /// Returns dialectical pairs as JSON.
    /// </summary>
    [HttpGet("api/understanding-graph/dialectical-pairs")]
    [OutputCache(Duration = 30)]
    public async Task<IActionResult> GetDialecticalPairs(double minWeight = 0.3, int count = 50)
    {
        var pairs = await _queryService.GetDialecticalPairsAsync(minWeight, count);
        return Json(pairs);
    }

    /// <summary>
    /// Returns bridge nodes as JSON.
    /// </summary>
    [HttpGet("api/understanding-graph/bridge-nodes")]
    [OutputCache(Duration = 30)]
    public async Task<IActionResult> GetBridgeNodes(int count = 20)
    {
        var bridges = await _queryService.GetBridgeNodesAsync(count);
        return Json(bridges);
    }

    /// <summary>
    /// Returns blindspots as JSON.
    /// </summary>
    [HttpGet("api/understanding-graph/blindspots")]
    [OutputCache(Duration = 30)]
    public async Task<IActionResult> GetBlindspots(int count = 20)
    {
        var blindspots = await _queryService.GetBlindspotsAsync(count);
        return Json(blindspots);
    }

    /// <summary>
    /// Returns snapshots as JSON.
    /// </summary>
    [HttpGet("api/understanding-graph/snapshots")]
    [OutputCache(Duration = 30)]
    public async Task<IActionResult> GetSnapshots(int count = 20)
    {
        var snapshots = await _snapshotService.GetSnapshotsAsync(count);
        return Json(snapshots);
    }

    /// <summary>
    /// Returns metric evolution as JSON.
    /// </summary>
    [HttpGet("api/understanding-graph/evolution/{metricName}")]
    [OutputCache(Duration = 30, VaryByRouteValueNames = new[] { "metricName" })]
    public async Task<IActionResult> GetMetricEvolution(string metricName)
    {
        var evolution = await _snapshotService.GetMetricEvolutionAsync(metricName);
        return Json(evolution);
    }

    /// <summary>
    /// Returns a single node's detail as JSON for the slide-in panel.
    /// Uses a lightweight projection — no heavy embedding vectors.
    /// </summary>
    [HttpGet("api/understanding-graph/node/{id}")]
    [OutputCache(Duration = 15, VaryByRouteValueNames = new[] { "id" })]
    public async Task<IActionResult> GetNodeDetail(int id)
    {
        var node = await _graphService.GetNodeWithEdgesAsync(id);
        if (node == null) return NotFound(new { error = "Node not found" });

        return Json(new
        {
            node.Id,
            node.CanonicalText,
            node.Status,
            node.Confidence,
            node.DegreeCentrality,
            node.BetweennessCentrality,
            node.ClusteringCoefficient,
            node.PageRank,
            node.EigenvectorCentrality,
            node.DialecticalTemperature,
            node.ControversyScore,
            node.SchemaEntropy,
            node.EvidenceCount,
            node.FirstSeenAt,
            SchemaMemberships = node.SchemaMemberships.Select(m => new
            {
                m.SchemaId,
                SchemaLabel = m.Schema?.Label ?? "Unknown",
                m.Weight
            }),
            OutboundEdges = node.OutboundEdges.Select(e => new
            {
                e.Id,
                e.TargetNodeId,
                TargetNode = e.TargetNode != null ? new { e.TargetNode.Id, e.TargetNode.CanonicalText } : null,
                e.Relationship,
                e.Weight
            }),
            InboundEdges = node.InboundEdges.Select(e => new
            {
                e.Id,
                e.SourceNodeId,
                SourceNode = e.SourceNode != null ? new { e.SourceNode.Id, e.SourceNode.CanonicalText } : null,
                e.Relationship,
                e.Weight
            })
        });
    }

    /// <summary>
    /// Returns a lightweight node preview for hover tooltips — only id, label, status, confidence.
    /// Clients cache results in a Map to avoid re-fetching on repeated hovers.
    /// </summary>
    [HttpGet("api/understanding-graph/node/{id}/preview")]
    [OutputCache(Duration = 60, VaryByRouteValueNames = new[] { "id" })]
    public async Task<IActionResult> GetNodePreview(int id)
    {
        var preview = await _queryService.GetNodePreviewAsync(id);
        if (preview == null) return NotFound(new { error = "Node not found" });
        return Json(preview);
    }

    /// <summary>
    /// Returns the current skeleton manifest — tells the client which versioned
    /// static JSON file to fetch. Enables cache-busting on rebuild without redeploy.
    /// </summary>
    [HttpGet("api/understanding-graph/skeleton-manifest")]
    [OutputCache(Duration = 30)]
    public IActionResult GetSkeletonManifest()
    {
        var dataDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "data");
        // Fallback: if not found at current directory, try the web root path
        if (!Directory.Exists(dataDir))
        {
            var webRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "data");
            if (Directory.Exists(webRoot))
                dataDir = webRoot;
        }
        if (!Directory.Exists(dataDir))
            return Json(new SkeletonManifest { Version = "none" });

        var manifestPath = Path.Combine(dataDir, "skeleton-manifest.json");
        if (!System.IO.File.Exists(manifestPath))
            return Json(new SkeletonManifest { Version = "none" });

        var json = System.IO.File.ReadAllText(manifestPath);
        var manifest = System.Text.Json.JsonSerializer.Deserialize<SkeletonManifest>(json,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return Json(manifest ?? new SkeletonManifest { Version = "none" });
    }

    /// <summary>
    /// Triggers a new snapshot capture.
    /// </summary>
    [HttpPost("api/understanding-graph/capture-snapshot")]
    public async Task<IActionResult> CaptureSnapshot(string? label = null)
    {
        var snapshot = await _snapshotService.CaptureSnapshotAsync(label);
        return Json(new { snapshot.Id, snapshot.Label, snapshot.CapturedAt });
    }

    /// <summary>
    /// Triggers a full capture pipeline (recompute + discover + snapshot).
    /// </summary>
    [HttpPost("api/understanding-graph/run-full-capture")]
    public async Task<IActionResult> RunFullCapture(string? label = null)
    {
        var snapshot = await _snapshotService.RunFullCaptureAsync(label);
        return Json(new { snapshot.Id, snapshot.Label, snapshot.CapturedAt });
    }

    /// <summary>
    /// Searches nodes by query text.
    /// </summary>
    [HttpGet("api/understanding-graph/search")]
    public async Task<IActionResult> Search(string q, int count = 20)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Json(Array.Empty<object>());

        var results = await _queryService.SearchAsync(q, count);
        return Json(results);
    }

    // ── Pipeline Actions ──────────────────────────────────────────────────

    /// <summary>
    /// Runs the full analysis pipeline: detect contradictions, recompute topology,
    /// discover schemas, run dialectical synthesis, and capture a snapshot.
    /// </summary>
    [HttpPost("api/understanding-graph/run-pipeline")]
    public async Task<IActionResult> RunPipeline()
    {
        try
        {
            _logger.LogInformation("Full analysis pipeline triggered via UI.");

            // Step 1: Detect contradictions
            var contradictionsFound = await _graphService.DetectContradictionsAsync();
            _logger.LogInformation("Pipeline: detected {Count} contradictions.", contradictionsFound);

            // Step 2: Recompute topology metrics
            await _graphService.RecomputeTopologyMetricsAsync();

            // Step 3: Discover schemas via k-means
            var schemas = await _schemaService.DiscoverSchemasKMeansAsync();
            _logger.LogInformation("Pipeline: discovered {Count} schemas.", schemas.Count);

            // Step 4: Run dialectical synthesis
            var synthesesGenerated = await _synthesisService.GenerateSynthesesAsync();
            _logger.LogInformation("Pipeline: generated {Count} syntheses.", synthesesGenerated);

            // Step 5: Capture snapshot
            var snapshot = await _snapshotService.CaptureSnapshotAsync($"Pipeline run {DateTime.UtcNow:yyyy-MM-dd HH:mm}");

            return Json(new
            {
                success = true,
                contradictionsDetected = contradictionsFound,
                synthesesGenerated = synthesesGenerated,
                schemasDiscovered = schemas.Count,
                snapshotId = snapshot.Id,
                snapshotLabel = snapshot.Label,
                message = $"Pipeline complete. Detected {contradictionsFound} contradictions, generated {synthesesGenerated} syntheses, discovered {schemas.Count} schemas."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pipeline run failed.");
            return Json(new { success = false, message = $"Pipeline failed: {ex.Message}" });
        }
    }

    /// <summary>
    /// Runs just the schema discovery step.
    /// </summary>
    [HttpPost("api/understanding-graph/run-discovery")]
    public async Task<IActionResult> RunDiscovery()
    {
        try
        {
            var schemas = await _schemaService.DiscoverSchemasKMeansAsync();
            return Json(new { success = true, schemasDiscovered = schemas.Count });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Rebuilds the entire Understanding Graph from existing data.
    /// Syncs all arguments and social arguments into nodes/edges,
    /// then runs the full pipeline (topology, contradiction detection,
    /// discovery, synthesis, snapshot).
    /// </summary>
    [HttpPost("api/understanding-graph/rebuild")]
    public async Task<IActionResult> RebuildGraph()
    {
        try
        {
            _logger.LogInformation("Rebuild Graph triggered via UI.");

            // Step 1: Bulk sync all existing data
            await _graphService.SyncAllAsync();

            // Step 2: Detect edges (including contradictions via enhanced DetermineRelationship)
            await _graphService.DetectEdgesAsync();

            // Step 3: Detect additional contradictions from evidence direction,
            //         social argument links, and rebuttal propositions
            var contradictionsFound = await _graphService.DetectContradictionsAsync();
            _logger.LogInformation("Rebuild: detected {Count} contradiction edges.", contradictionsFound);

            // Step 4: Recompute topology metrics
            await _graphService.RecomputeTopologyMetricsAsync();

            // Step 5: Discover schemas via k-means
            var schemas = await _schemaService.DiscoverSchemasKMeansAsync();
            _logger.LogInformation("Rebuild: discovered {Count} schemas.", schemas.Count);

            // Step 6: Run dialectical synthesis
            var synthesesGenerated = await _synthesisService.GenerateSynthesesAsync();
            _logger.LogInformation("Rebuild: generated {Count} syntheses.", synthesesGenerated);

            // Step 7: Capture snapshot
            var snapshot = await _snapshotService.CaptureSnapshotAsync($"Rebuild {DateTime.UtcNow:yyyy-MM-dd HH:mm}");

            // Step 8: Regenerate the static skeleton before reporting success so
            // request-scoped services remain available and the next reload is current.
            await _skeletonGenerator.GenerateAsync();

            return Json(new
            {
                success = true,
                nodesCreated = true,
                contradictionsDetected = contradictionsFound,
                synthesesGenerated = synthesesGenerated,
                schemasDiscovered = schemas.Count,
                snapshotId = snapshot.Id,
                snapshotLabel = snapshot.Label,
                message = $"Graph rebuilt. Detected {contradictionsFound} contradictions, generated {synthesesGenerated} syntheses, discovered {schemas.Count} schemas."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rebuild graph failed.");
            return Json(new { success = false, message = $"Rebuild failed: {ex.Message}" });
        }
    }

    /// <summary>
    /// Runs just the dialectical synthesis step.
    /// </summary>
    [HttpPost("api/understanding-graph/run-synthesis")]
    public async Task<IActionResult> RunSynthesis()
    {
        try
        {
            var count = await _synthesisService.GenerateSynthesesAsync();
            return Json(new { success = true, synthesesGenerated = count });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Runs just the contradiction detection step.
    /// Scans for contradictions via evidence direction, social argument links,
    /// and rebuttal propositions.
    /// </summary>
    [HttpPost("api/understanding-graph/detect-contradictions")]
    public async Task<IActionResult> DetectContradictions()
    {
        try
        {
            var count = await _graphService.DetectContradictionsAsync();
            return Json(new { success = true, contradictionsDetected = count });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
}

/// <summary>
/// Request DTO for progressive leaf-node loading.
/// </summary>
public class LeafNodesRequest
{
    public List<int> RootNodeIds { get; set; } = new();
    public int MaxNodes { get; set; } = 2000;
    /// <summary>
    /// Optional: if provided, only return nodes whose IDs are in this list.
    /// Used for viewport-scoped loading — the client sends the IDs of nodes
    /// currently visible in the viewport, and we return only those that aren't
    /// already in the client-side DataSet.
    /// </summary>
    public List<int>? ViewportNodeIds { get; set; }
}