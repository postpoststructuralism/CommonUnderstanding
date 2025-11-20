using CommonUnderstanding.Models;
using System.Text.Json;

namespace CommonUnderstanding.Services;

/// <summary>
/// Loads and manages canonical belief systems from JSON files
/// </summary>
public class BeliefSystemKnowledgeBase
{
    private readonly List<CanonicalBeliefSystem> _allSystems = new();
    private readonly ILogger<BeliefSystemKnowledgeBase> _logger;

    public BeliefSystemKnowledgeBase(ILogger<BeliefSystemKnowledgeBase> logger)
    {
        _logger = logger;
        LoadAllBeliefSystems();
    }

    public IReadOnlyList<CanonicalBeliefSystem> AllSystems => _allSystems.AsReadOnly();

    private void LoadAllBeliefSystems()
    {
        var dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "BeliefSystems");
        
        if (!Directory.Exists(dataPath))
        {
            _logger.LogWarning("Belief systems data directory not found: {Path}", dataPath);
            return;
        }

        var jsonFiles = Directory.GetFiles(dataPath, "*.json");
        
        foreach (var file in jsonFiles)
        {
            try
            {
                var json = File.ReadAllText(file);
                var systems = JsonSerializer.Deserialize<List<CanonicalBeliefSystem>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (systems != null)
                {
                    // Compute slug for each system so we can route using a stable URL-friendly value
                    foreach (var s in systems)
                    {
                        s.Slug = GenerateSlug(s.Name);
                    }

                    _allSystems.AddRange(systems);
                    _logger.LogInformation("Loaded {Count} belief systems from {File}", systems.Count, Path.GetFileName(file));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading belief systems from {File}", file);
            }
        }

        _logger.LogInformation("Total belief systems loaded: {Count}", _allSystems.Count);
    }

    private static string GenerateSlug(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        // normalize and remove diacritics
        var normalized = value.Normalize(System.Text.NormalizationForm.FormD);
        var chars = normalized.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray();
        var cleaned = new string(chars).Normalize(System.Text.NormalizationForm.FormC);
        // remove invalid chars, replace spaces with hyphens, lowercase
        var slug = System.Text.RegularExpressions.Regex.Replace(cleaned, "[^a-zA-Z0-9\\s-]", "").Trim();
        slug = System.Text.RegularExpressions.Regex.Replace(slug, "\\s+", "-").ToLowerInvariant();
        return slug;
    }

    public CanonicalBeliefSystem? GetByName(string name)
    {
        return _allSystems.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public CanonicalBeliefSystem? GetBySlug(string slug)
    {
        return _allSystems.FirstOrDefault(s => s.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));
    }

    public List<CanonicalBeliefSystem> GetByCategory(string category)
    {
        return _allSystems.Where(s => s.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public List<CanonicalBeliefSystem> GetByCulture(string culture)
    {
        return _allSystems.Where(s => s.Culture.Contains(culture, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    /// <summary>
    /// Calculate user's position in the multidimensional belief universe
    /// </summary>
    public BeliefUniversePosition CalculateUniversePosition(BeliefSnapshot userProfile)
    {
        // Create a simplified match list based on available belief systems
        var simpleMatches = new List<BeliefSystemMatch>();
        
        foreach (var system in _allSystems.Take(5)) // Top 5 for now
        {
            simpleMatches.Add(new BeliefSystemMatch
            {
                SystemId = system.Id,
                SystemName = system.Name,
                MatchPercentage = 50.0, // Placeholder - will implement proper scoring later
                DimensionalAlignment = new Dictionary<string, double>(),
                SharedValues = new List<string> { "Pending detailed analysis" },
                KeyDifferences = new List<string>()
            });
        }
        
        // Build coordinates from user's dimensions
        var coordinates = new Dictionary<string, double>();
        foreach (var dim in userProfile.Dimensions.Where(d => d.Position.HasValue))
        {
            coordinates[dim.Name] = dim.Position!.Value;
        }

        // Determine region based on dominant dimensions
        var region = DetermineBeliefRegion(userProfile);

        var position = new BeliefUniversePosition
        {
            UserId = userProfile.UserId,
            NearestSystems = simpleMatches,
            UniverseCoordinates = coordinates,
            PositionNarrative = GeneratePositionDescription(userProfile, simpleMatches, region)
        };

        return position;
    }

    private string DetermineBeliefRegion(BeliefSnapshot profile)
    {
        // Simple heuristic classification based on dominant dimensions
        var strongDimensions = profile.Dimensions
            .Where(d => d.Position.HasValue && d.Confidence > 0.6)
            .OrderByDescending(d => Math.Abs(d.Position!.Value) * d.Confidence)
            .Take(2)
            .Select(d => d.Name)
            .ToList();
        
        return strongDimensions.Any() ? string.Join("-", strongDimensions) : "Exploratory";
    }

    private string GeneratePositionDescription(BeliefSnapshot profile, List<BeliefSystemMatch> nearest, string region)
    {
        var description = $"Your worldview is currently being mapped in the {region} region of the belief universe. ";
        description += $"With {profile.InteractionCount} interactions collected, we're building understanding of your perspective. ";
        
        if (profile.Values.Any())
        {
            var topValues = string.Join(", ", profile.Values.Take(3).Select(v => v.Name.ToLower()));
            description += $"Your responses suggest strong alignment with values like {topValues}. ";
        }

        description += $"Continue answering questions to refine your position and discover which belief systems resonate most closely with your worldview.";

        return description;
    }

    /// <summary>
    /// Compare two canonical belief systems
    /// </summary>
    public BeliefSystemComparison? CompareBeliefSystems(string system1Name, string system2Name)
    {
        var system1 = GetByName(system1Name);
        var system2 = GetByName(system2Name);

        if (system1 == null || system2 == null)
        {
            return null;
        }

        // Simple text-based comparison for now
        var sharedValues = new List<string>();
        var differingValues = new List<string>();
        var synergies = new List<string>();

        // Compare core principles
        var common = system1.CorePrinciples.Intersect(system2.CorePrinciples, StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var principle in common)
        {
            sharedValues.Add($"Both emphasize: {principle}");
        }

        // Find complementary principles
        if (system1.CorePrinciples.Any() && system2.CorePrinciples.Any())
        {
            synergies.Add($"{system1.Name}'s focus on '{system1.CorePrinciples.First()}' could complement {system2.Name}'s emphasis on '{system2.CorePrinciples.First()}'");
        }

        // Simple similarity based on category and culture
        double similarity = 0.5; // Base similarity
        if (system1.Category == system2.Category) similarity += 0.2;
        if (system1.Culture.Split('/').Any(c => system2.Culture.Contains(c))) similarity += 0.1;
        if (common.Any()) similarity += 0.2;

        if (system1.Category != system2.Category)
        {
            differingValues.Add($"Different categories: {system1.Category} vs {system2.Category}");
        }

        if (!system1.Culture.Split('/').Any(c => system2.Culture.Contains(c)))
        {
            differingValues.Add($"Different cultural origins: {system1.Culture} vs {system2.Culture}");
        }

        return new BeliefSystemComparison
        {
            System1 = system1.Name,
            System2 = system2.Name,
            OverallSimilarity = Math.Min(similarity, 1.0),
            SharedValues = sharedValues,
            DifferingValues = differingValues,
            PotentialSynergies = synergies,
            HistoricalInteractions = new List<string>()
        };
    }
}
