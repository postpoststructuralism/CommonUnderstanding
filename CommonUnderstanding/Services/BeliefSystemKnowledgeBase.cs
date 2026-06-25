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

    /// <summary>
    /// Compare a user's discovered belief profile with all canonical belief systems
    /// Returns ranked list of matching systems with detailed comparison
    /// </summary>
    public List<UserBeliefSystemMatch> CompareUserToCanonicalSystems(BeliefSnapshot userProfile, int topN = 10)
    {
        var matches = new List<UserBeliefSystemMatch>();

        foreach (var system in _allSystems)
        {
            var match = CompareUserToSystem(userProfile, system);
            matches.Add(match);
        }

        // Return top N matches sorted by overall similarity
        return matches
            .OrderByDescending(m => m.OverallMatchPercentage)
            .Take(topN)
            .ToList();
    }

    /// <summary>
    /// Compare a user's profile with a specific canonical belief system
    /// </summary>
    private UserBeliefSystemMatch CompareUserToSystem(BeliefSnapshot userProfile, CanonicalBeliefSystem system)
    {
        var match = new UserBeliefSystemMatch
        {
            SystemId = system.Id,
            SystemName = system.Name,
            SystemSlug = system.Slug,
            SystemCategory = system.Category,
            SystemCulture = system.Culture,
            SystemEra = system.Era
        };

        // Compare values
        var userValues = userProfile.Values.Select(v => v.Name.ToLowerInvariant()).ToHashSet();
        var systemValueKeywords = ExtractValueKeywords(system);
        
        var sharedValues = userValues.Intersect(systemValueKeywords, StringComparer.OrdinalIgnoreCase).ToList();
        match.SharedValues = sharedValues;

        // Compare moral foundations
        var moralFoundationAlignment = CompareMoralFoundations(userProfile.MoralFoundations, system);
        match.MoralFoundationAlignment = moralFoundationAlignment;

        // Calculate dimensional similarities (if system has dimensional profile)
        if (system.Profile?.Dimensions != null && system.Profile.Dimensions.Any())
        {
            match.DimensionalAlignment = CompareDimensions(userProfile.Dimensions, system.Profile.Dimensions);
        }

        // Calculate overall match percentage
        match.OverallMatchPercentage = CalculateOverallMatch(
            sharedValues.Count,
            moralFoundationAlignment,
            match.DimensionalAlignment
        );

        // Identify key differences
        match.KeyDifferences = IdentifyKeyDifferences(userProfile, system, moralFoundationAlignment);

        // Generate explanation
        match.MatchExplanation = GenerateMatchExplanation(match, userProfile, system);

        return match;
    }

    private HashSet<string> ExtractValueKeywords(CanonicalBeliefSystem system)
    {
        var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        // Extract from core principles
        foreach (var principle in system.CorePrinciples)
        {
            var words = principle.ToLowerInvariant()
                .Split(new[] { ' ', ',', '.', ':', ';' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var word in words.Where(w => w.Length > 4)) // Filter short words
            {
                keywords.Add(word);
            }
        }

        // Common value mappings
        var valueMappings = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["compassion"] = new() { "kindness", "empathy", "caring", "mercy" },
            ["justice"] = new() { "fairness", "equality", "rights", "equity" },
            ["freedom"] = new() { "liberty", "autonomy", "independence" },
            ["wisdom"] = new() { "knowledge", "understanding", "insight" },
            ["community"] = new() { "solidarity", "togetherness", "collective" },
            ["duty"] = new() { "responsibility", "obligation", "honor" },
            ["harmony"] = new() { "balance", "peace", "equilibrium" }
        };

        foreach (var (value, synonyms) in valueMappings)
        {
            if (system.CorePrinciples.Any(p => synonyms.Any(s => p.Contains(s, StringComparison.OrdinalIgnoreCase))))
            {
                keywords.Add(value);
            }
        }

        return keywords;
    }

    private Dictionary<string, double> CompareMoralFoundations(
        MoralFoundationsProfile userMF,
        CanonicalBeliefSystem system)
    {
        var alignment = new Dictionary<string, double>();

        // If system doesn't have moral foundations profile, use heuristics
        var systemMF = system.Profile?.MoralFoundations ?? InferMoralFoundations(system);

        alignment["Care"] = 1.0 - Math.Abs(userMF.Care.Score - systemMF.Care.Score) / 10.0;
        alignment["Fairness"] = 1.0 - Math.Abs(userMF.Fairness.Score - systemMF.Fairness.Score) / 10.0;
        alignment["Loyalty"] = 1.0 - Math.Abs(userMF.Loyalty.Score - systemMF.Loyalty.Score) / 10.0;
        alignment["Authority"] = 1.0 - Math.Abs(userMF.Authority.Score - systemMF.Authority.Score) / 10.0;
        alignment["Sanctity"] = 1.0 - Math.Abs(userMF.Sanctity.Score - systemMF.Sanctity.Score) / 10.0;
        alignment["Liberty"] = 1.0 - Math.Abs(userMF.Liberty.Score - systemMF.Liberty.Score) / 10.0;

        return alignment;
    }

    private MoralFoundationsProfile InferMoralFoundations(CanonicalBeliefSystem system)
    {
        // Simple heuristic-based inference
        var profile = new MoralFoundationsProfile();

        var text = (system.Description + " " + string.Join(" ", system.CorePrinciples)).ToLowerInvariant();

        // Care/Harm
        profile.Care = new Foundation
        {
            Score = CountKeywords(text, new[] { "compassion", "kindness", "caring", "love", "mercy", "empathy" }) * 2.0
        };

        // Fairness/Cheating
        profile.Fairness = new Foundation
        {
            Score = CountKeywords(text, new[] { "justice", "fairness", "equality", "rights", "equity" }) * 2.0
        };

        // Loyalty/Betrayal
        profile.Loyalty = new Foundation
        {
            Score = CountKeywords(text, new[] { "loyalty", "community", "solidarity", "collective", "tradition" }) * 2.0
        };

        // Authority/Subversion
        profile.Authority = new Foundation
        {
            Score = CountKeywords(text, new[] { "authority", "hierarchy", "order", "discipline", "obedience" }) * 2.0
        };

        // Sanctity/Degradation
        profile.Sanctity = new Foundation
        {
            Score = CountKeywords(text, new[] { "sacred", "holy", "pure", "divine", "spiritual", "sanctity" }) * 2.0
        };

        // Liberty/Oppression
        profile.Liberty = new Foundation
        {
            Score = CountKeywords(text, new[] { "freedom", "liberty", "autonomy", "independence", "choice" }) * 2.0
        };

        return profile;
    }

    private double CountKeywords(string text, string[] keywords)
    {
        return Math.Min(keywords.Count(kw => text.Contains(kw)), 5); // Cap at 5
    }

    private Dictionary<string, double> CompareDimensions(
        List<BeliefDimension> userDimensions,
        List<BeliefDimension> systemDimensions)
    {
        var alignment = new Dictionary<string, double>();

        foreach (var userDim in userDimensions.Where(d => d.Position.HasValue && d.Confidence > 0.3))
        {
            var systemDim = systemDimensions.FirstOrDefault(d => 
                d.Name.Equals(userDim.Name, StringComparison.OrdinalIgnoreCase));

            if (systemDim?.Position.HasValue == true)
            {
                // Calculate alignment (1.0 = perfect match, 0.0 = opposite)
                var distance = Math.Abs(userDim.Position.Value - systemDim.Position.Value);
                var rawSimilarity = 1.0 - (distance / 2.0); // Normalize to 0-1
                
                // Weight by user's confidence in this dimension.
                // High-confidence dimensions contribute more to the match score.
                var confidenceWeight = 0.5 + (userDim.Confidence * 0.5); // Range: 0.5-1.0
                var weightedSimilarity = rawSimilarity * confidenceWeight;
                
                alignment[userDim.Name] = weightedSimilarity;
            }
        }

        return alignment;
    }

    private double CalculateOverallMatch(
        int sharedValuesCount,
        Dictionary<string, double> moralFoundationAlignment,
        Dictionary<string, double> dimensionalAlignment)
    {
        var scores = new List<double>();

        // Values match (0-30 points)
        scores.Add(Math.Min(sharedValuesCount * 6, 30));

        // Moral foundations match (0-40 points)
        if (moralFoundationAlignment.Any())
        {
            var avgMFAlignment = moralFoundationAlignment.Values.Average();
            scores.Add(avgMFAlignment * 40);
        }

        // Dimensional match (0-30 points) — already confidence-weighted
        if (dimensionalAlignment.Any())
        {
            var avgDimAlignment = dimensionalAlignment.Values.Average();
            scores.Add(avgDimAlignment * 30);
        }

        return scores.Sum();
    }

    private List<string> IdentifyKeyDifferences(
        BeliefSnapshot userProfile,
        CanonicalBeliefSystem system,
        Dictionary<string, double> moralFoundationAlignment)
    {
        var differences = new List<string>();

        // Find moral foundations with low alignment
        foreach (var (foundation, alignment) in moralFoundationAlignment.OrderBy(kvp => kvp.Value).Take(2))
        {
            if (alignment < 0.6)
            {
                differences.Add($"Different emphasis on {foundation}");
            }
        }

        // Check for conflicting core values
        var userTopValues = userProfile.Values
            .OrderByDescending(v => v.ImportanceScore)
            .Take(3)
            .Select(v => v.Name)
            .ToList();

        var systemKeywords = ExtractValueKeywords(system);
        var nonOverlapping = userTopValues.Where(v => !systemKeywords.Contains(v)).ToList();

        if (nonOverlapping.Any())
        {
            differences.Add($"Your emphasis on {string.Join(", ", nonOverlapping)} differs from {system.Name}'s focus");
        }

        return differences;
    }

    private string GenerateMatchExplanation(
        UserBeliefSystemMatch match,
        BeliefSnapshot userProfile,
        CanonicalBeliefSystem system)
    {
        var explanation = new List<string>();

        if (match.OverallMatchPercentage >= 70)
        {
            explanation.Add($"Strong alignment with {system.Name}.");
        }
        else if (match.OverallMatchPercentage >= 50)
        {
            explanation.Add($"Moderate alignment with {system.Name}.");
        }
        else
        {
            explanation.Add($"Some alignment with {system.Name}, but significant differences exist.");
        }

        if (match.SharedValues.Any())
        {
            explanation.Add($"Shared values include: {string.Join(", ", match.SharedValues.Take(3))}.");
        }

        var strongMF = match.MoralFoundationAlignment
            .Where(kvp => kvp.Value >= 0.8)
            .Select(kvp => kvp.Key)
            .ToList();

        if (strongMF.Any())
        {
            explanation.Add($"Strong alignment on {string.Join(" and ", strongMF)} foundations.");
        }

        return string.Join(" ", explanation);
    }
}

/// <summary>
/// Represents a match between a user's discovered beliefs and a canonical belief system
/// </summary>
public class UserBeliefSystemMatch
{
    public string SystemId { get; set; } = string.Empty;
    public string SystemName { get; set; } = string.Empty;
    public string SystemSlug { get; set; } = string.Empty;
    public string SystemCategory { get; set; } = string.Empty;
    public string SystemCulture { get; set; } = string.Empty;
    public string SystemEra { get; set; } = string.Empty;
    
    public double OverallMatchPercentage { get; set; } // 0-100
    
    public List<string> SharedValues { get; set; } = new();
    public List<string> KeyDifferences { get; set; } = new();
    
    public Dictionary<string, double> MoralFoundationAlignment { get; set; } = new(); // Foundation name -> alignment (0-1)
    public Dictionary<string, double> DimensionalAlignment { get; set; } = new(); // Dimension name -> alignment (0-1)
    
    public string MatchExplanation { get; set; } = string.Empty;
}
