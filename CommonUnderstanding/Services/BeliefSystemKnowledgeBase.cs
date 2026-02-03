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
        // Calculate actual similarity scores with all belief systems
        var matches = new List<BeliefSystemMatch>();
        
        foreach (var system in _allSystems)
        {
            var similarity = CalculateSimilarity(userProfile, system);
            
            matches.Add(new BeliefSystemMatch
            {
                SystemId = system.Id,
                SystemName = system.Name,
                MatchPercentage = similarity.OverallMatch * 100,
                DimensionalAlignment = similarity.DimensionScores,
                SharedValues = similarity.SharedValues,
                KeyDifferences = similarity.KeyDifferences
            });
        }
        
        // Sort by match percentage and take top 5
        var topMatches = matches.OrderByDescending(m => m.MatchPercentage).Take(5).ToList();
        
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
            NearestSystems = topMatches,
            UniverseCoordinates = coordinates,
            PositionNarrative = GeneratePositionDescription(userProfile, topMatches, region)
        };

        return position;
    }

    /// <summary>
    /// Calculate similarity between user's beliefs and a canonical belief system
    /// </summary>
    private (double OverallMatch, Dictionary<string, double> DimensionScores, List<string> SharedValues, List<string> KeyDifferences) 
        CalculateSimilarity(BeliefSnapshot userProfile, CanonicalBeliefSystem system)
    {
        var dimensionScores = new Dictionary<string, double>();
        var sharedValues = new List<string>();
        var keyDifferences = new List<string>();
        
        // Calculate value alignment
        double valueAlignment = 0.0;
        int valueMatches = 0;
        
        foreach (var userValue in userProfile.Values.Take(5))
        {
            // Check against system's core principles
            var valueMatch = system.CorePrinciples.Any(p => 
                p.Contains(userValue.Name, StringComparison.OrdinalIgnoreCase) ||
                userValue.Name.Contains(p, StringComparison.OrdinalIgnoreCase));
            
            // Also check against system's profile values
            if (!valueMatch && system.Profile?.Values != null)
            {
                valueMatch = system.Profile.Values.Any(v => 
                    v.Name.Equals(userValue.Name, StringComparison.OrdinalIgnoreCase));
            }
            
            if (valueMatch)
            {
                valueAlignment += userValue.Confidence;
                valueMatches++;
                sharedValues.Add(userValue.Name);
            }
        }
        
        if (valueMatches > 0)
        {
            var maxValues = Math.Max(userProfile.Values.Count, 
                system.Profile?.Values?.Count ?? system.CorePrinciples.Count);
            valueAlignment /= maxValues;
        }
        
        // Calculate dimensional alignment using system's Profile
        double dimensionAlignment = 0.5; // Default moderate alignment
        if (system.Profile?.Dimensions != null && system.Profile.Dimensions.Any())
        {
            double alignedDimensions = 0.0;
            int totalDimensions = 0;
            
            foreach (var sysDim in system.Profile.Dimensions.Where(d => d.Position.HasValue))
            {
                var userDim = userProfile.Dimensions.FirstOrDefault(d => 
                    d.Name.Equals(sysDim.Name, StringComparison.OrdinalIgnoreCase));
                
                if (userDim?.Position.HasValue == true)
                {
                    totalDimensions++;
                    // Calculate how close the positions are (-1 to 1 scale)
                    var distance = Math.Abs(userDim.Position.Value - sysDim.Position!.Value);
                    var similarity = 1.0 - (distance / 2.0); // Normalize to 0-1
                    
                    dimensionScores[sysDim.Name] = similarity * 100;
                    alignedDimensions += similarity;
                }
            }
            
            if (totalDimensions > 0)
            {
                dimensionAlignment = alignedDimensions / totalDimensions;
            }
        }
        
        // Weighted overall match: values matter more than abstract dimensions
        var overallMatch = (valueAlignment * 0.6) + (dimensionAlignment * 0.4);
        
        // Identify key differences (areas where confidence is high but alignment is low)
        foreach (var userDim in userProfile.Dimensions.Where(d => d.Confidence > 0.7 && d.Position.HasValue))
        {
            var sysDim = system.Profile?.Dimensions?.FirstOrDefault(d => 
                d.Name.Equals(userDim.Name, StringComparison.OrdinalIgnoreCase));
            
            if (sysDim?.Position.HasValue == true)
            {
                var distance = Math.Abs(userDim.Position!.Value - sysDim.Position.Value);
                
                if (distance > 1.0) // Significant difference (more than halfway across spectrum)
                {
                    var userTendency = userDim.Position.Value > 0 ? "positive" : "negative";
                    var systemTendency = sysDim.Position.Value > 0 ? "positive" : "negative";
                    
                    keyDifferences.Add($"{userDim.Name}: Your position ({userDim.Position.Value:F2}) differs from " +
                        $"{system.Name}'s position ({sysDim.Position.Value:F2})");
                }
            }
        }
        
        return (overallMatch, dimensionScores, sharedValues, keyDifferences);
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
