using CommonUnderstanding.Models;

namespace CommonUnderstanding.Services;

/// <summary>
/// Statistical engine for Bayesian belief inference and model updating
/// </summary>
public class BayesianInferenceEngine
{
    private readonly ILogger<BayesianInferenceEngine> _logger;

    // Prior distribution parameters
    private const double DefaultPriorMean = 0.0;      // Neutral position
    private const double DefaultPriorVariance = 1.0;   // High initial uncertainty
    private const double MinVariance = 0.01;           // Minimum uncertainty (never 100% certain)
    private const double LearningRate = 0.35;          // How quickly we update beliefs (higher = faster convergence)

    public BayesianInferenceEngine(ILogger<BayesianInferenceEngine> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Update belief model based on new evidence.
    /// Uses response quality to weight evidence — low-effort responses
    /// have less impact on the model.
    /// </summary>
    public BeliefSnapshot UpdateModel(
        UserProfile profile,
        UserInteraction interaction,
        ResponseAnalysis analysis)
    {
        var previousSnapshot = profile.CurrentBeliefSnapshot ?? CreateInitialSnapshot(profile.Id);
        var newSnapshot = CloneSnapshot(previousSnapshot);
        
        newSnapshot.Id = Guid.NewGuid().ToString();
        newSnapshot.Timestamp = DateTime.UtcNow;
        newSnapshot.InteractionCount = profile.InteractionCount;

        // Use response quality to scale evidence weight
        var qualityMultiplier = analysis.ResponseQuality > 0 ? analysis.ResponseQuality : 0.7;

        // Update dimensions based on analysis
        foreach (var update in analysis.DimensionUpdates)
        {
            // Scale evidence weight by response quality
            var weightedUpdate = new DimensionUpdate
            {
                DimensionName = update.DimensionName,
                Category = update.Category,
                Position = update.Position,
                EvidenceWeight = update.EvidenceWeight * qualityMultiplier,
                Evidence = update.Evidence,
                Reasoning = update.Reasoning
            };
            UpdateDimension(newSnapshot, weightedUpdate, interaction.Id);
        }

        // Update moral foundations
        UpdateMoralFoundations(newSnapshot, analysis.MoralFoundationScores, interaction.Id);

        // Update values
        UpdateValues(newSnapshot, analysis.ImpliedValues, analysis.AnalysisConfidence, interaction.Id);

        // Calculate overall confidence
        newSnapshot.OverallConfidence = CalculateOverallConfidence(newSnapshot);

        // Update statistics
        newSnapshot.Statistics = CalculateStatistics(newSnapshot, profile);

        // Generate narrative summary (placeholder - would use AI in production)
        newSnapshot.NarrativeSummary = GenerateNarrativeSummary(newSnapshot);

        _logger.LogInformation("Updated belief model for user {UserId}. Confidence: {Confidence:F3}",
            profile.Id, newSnapshot.OverallConfidence);

        return newSnapshot;
    }

    /// <summary>
    /// Bayesian update for a single dimension
    /// </summary>
    private void UpdateDimension(
        BeliefSnapshot snapshot,
        DimensionUpdate update,
        string evidenceId)
    {
        var dimension = snapshot.Dimensions.FirstOrDefault(d => d.Name == update.DimensionName);
        
        if (dimension == null)
        {
            // New dimension - create with prior
            dimension = new BeliefDimension
            {
                Name = update.DimensionName,
                Category = update.Category,
                Position = update.Position,
                Confidence = 0.1, // Low initial confidence
                Uncertainty = DefaultPriorVariance,
                SampleSize = 1
            };
            snapshot.Dimensions.Add(dimension);
        }
        else if (update.Position.HasValue)
        {
            // Bayesian update: combine prior and new evidence
            var prior = new GaussianDistribution(
                dimension.Position ?? DefaultPriorMean,
                dimension.Uncertainty);
            
            var likelihood = new GaussianDistribution(
                update.Position.Value,
                1.0 - update.EvidenceWeight); // Higher weight = lower variance
            
            var posterior = BayesianUpdate(prior, likelihood);
            
            dimension.Position = posterior.Mean;
            dimension.Uncertainty = Math.Max(posterior.Variance, MinVariance);
            dimension.Confidence = CalculateDimensionConfidence(dimension.SampleSize + 1, posterior.Variance);
            dimension.SampleSize++;
        }

        dimension.EvidenceIds.Add(evidenceId);
    }

    /// <summary>
    /// Bayesian update formula for Gaussian distributions
    /// </summary>
    private GaussianDistribution BayesianUpdate(
        GaussianDistribution prior,
        GaussianDistribution likelihood)
    {
        // Posterior mean is precision-weighted average
        var priorPrecision = 1.0 / prior.Variance;
        var likelihoodPrecision = 1.0 / likelihood.Variance;
        var posteriorPrecision = priorPrecision + likelihoodPrecision * LearningRate;
        
        var posteriorMean = (priorPrecision * prior.Mean + likelihoodPrecision * LearningRate * likelihood.Mean) 
                          / posteriorPrecision;
        var posteriorVariance = 1.0 / posteriorPrecision;

        return new GaussianDistribution(posteriorMean, posteriorVariance);
    }

    /// <summary>
    /// Update moral foundations scores
    /// </summary>
    private void UpdateMoralFoundations(
        BeliefSnapshot snapshot,
        Dictionary<string, double> scores,
        string evidenceId)
    {
        foreach (var (foundation, score) in scores)
        {
            var foundationProp = typeof(MoralFoundationsProfile).GetProperty(foundation);
            if (foundationProp != null)
            {
                var current = (Foundation?)foundationProp.GetValue(snapshot.MoralFoundations);
                if (current != null)
                {
                    // Running average with exponential smoothing
                    var alpha = 0.2; // Smoothing factor
                    current.Score = current.Score * (1 - alpha) + score * alpha;
                    
                    // Increase confidence with more data
                    current.Confidence = Math.Min(current.Confidence + 0.05, 0.95);
                    
                    // Decrease standard error with more samples
                    current.StandardError = Math.Max(current.StandardError * 0.95, 0.5);
                }
            }
        }
    }

    /// <summary>
    /// Update inferred values
    /// </summary>
    private void UpdateValues(
        BeliefSnapshot snapshot,
        List<string> newValues,
        double confidence,
        string evidenceId)
    {
        foreach (var valueName in newValues)
        {
            var existing = snapshot.Values.FirstOrDefault(v => 
                v.Name.Equals(valueName, StringComparison.OrdinalIgnoreCase));
            
            if (existing != null)
            {
                // Reinforce existing value
                existing.ImportanceScore = Math.Min(existing.ImportanceScore + 0.5, 10.0);
                existing.Confidence = Math.Min(existing.Confidence + 0.05, 0.95);
                existing.EvidenceIds.Add(evidenceId);
            }
            else
            {
                // New value discovered
                snapshot.Values.Add(new InferredValue
                {
                    Name = valueName,
                    ImportanceScore = 5.0,
                    Confidence = confidence * 0.5, // Lower confidence for single mention
                    EvidenceIds = new List<string> { evidenceId }
                });
            }
        }

        // Sort by importance * confidence
        snapshot.Values = snapshot.Values
            .OrderByDescending(v => v.ImportanceScore * v.Confidence)
            .ToList();
    }

    /// <summary>
    /// Calculate confidence based on sample size and variance.
    /// Uses a faster-growing sigmoid curve so that 2-3 data points
    /// on a dimension already yield meaningful confidence (40-60%).
    /// </summary>
    private double CalculateDimensionConfidence(int sampleSize, double variance)
    {
        // Sigmoid-based sample factor: grows quickly for early samples, saturates at ~1.0
        // At N=1: 0.27, N=2: 0.50, N=3: 0.73, N=5: 0.92, N=10: 0.99
        var sampleFactor = 1.0 / (1.0 + Math.Exp(-0.8 * (sampleSize - 2.0)));
        
        // Variance factor: tighter distributions = higher confidence
        // At variance=1.0: 0.37, variance=0.5: 0.61, variance=0.1: 0.90
        var varianceFactor = Math.Exp(-variance * 1.5);
        
        return sampleFactor * varianceFactor;
    }

    /// <summary>
    /// Calculate overall model confidence.
    /// Uses a weighted average that emphasizes high-confidence dimensions
    /// and rewards having more dimensions covered.
    /// </summary>
    private double CalculateOverallConfidence(BeliefSnapshot snapshot)
    {
        if (!snapshot.Dimensions.Any())
            return 0.1;

        // Use a weighted average that gives more weight to higher-confidence dimensions
        // This prevents a few low-confidence dimensions from dragging the average down
        var dimensions = snapshot.Dimensions.Where(d => d.Confidence > 0).ToList();
        if (!dimensions.Any()) return 0.1;

        // Weight each dimension by its own confidence (self-reinforcing)
        var totalWeight = dimensions.Sum(d => d.Confidence);
        var weightedAvg = totalWeight > 0
            ? dimensions.Sum(d => d.Confidence * d.Confidence) / totalWeight
            : 0.1;

        // Coverage bonus: having more dimensions is good, but don't penalize early stages
        // At 5 dims: 0.25, at 10 dims: 0.50, at 15 dims: 0.75
        var coverageBonus = Math.Min(dimensions.Count / 20.0, 0.5);

        return Math.Min(weightedAvg + coverageBonus, 0.98);
    }

    /// <summary>
    /// Calculate statistical metadata
    /// </summary>
    private ModelStatistics CalculateStatistics(BeliefSnapshot snapshot, UserProfile profile)
    {
        var stats = new ModelStatistics
        {
            TotalEvidence = profile.Interactions.Count,
            LastUpdated = DateTime.UtcNow
        };

        // Calculate entropy (information content)
        stats.Entropy = CalculateEntropy(snapshot);

        // Calculate internal consistency
        stats.Consistency = CalculateConsistency(snapshot, profile);

        // Signal-to-noise ratio
        stats.SignalToNoise = CalculateSignalToNoise(snapshot);

        // Identify uncertain areas
        stats.UncertainAreas = snapshot.Dimensions
            .Where(d => d.Confidence < 0.5)
            .Select(d => d.Name)
            .ToList();

        // Detect contradictions (simplified)
        stats.DetectedContradictions = DetectContradictions(snapshot, profile);

        return stats;
    }

    /// <summary>
    /// Calculate Shannon entropy of the belief distribution
    /// </summary>
    private double CalculateEntropy(BeliefSnapshot snapshot)
    {
        if (!snapshot.Dimensions.Any())
            return 0;

        // Normalized entropy based on dimension uncertainties
        var totalEntropy = snapshot.Dimensions
            .Where(d => d.Uncertainty > 0)
            .Sum(d => 0.5 * Math.Log(2 * Math.PI * Math.E * d.Uncertainty));

        return totalEntropy / snapshot.Dimensions.Count;
    }

    /// <summary>
    /// Measure internal consistency of beliefs
    /// </summary>
    private double CalculateConsistency(BeliefSnapshot snapshot, UserProfile profile)
    {
        // Simplified consistency check
        // In production: check for logical contradictions, temporal consistency, etc.
        
        var recentInteractions = profile.Interactions.TakeLast(10).ToList();
        if (recentInteractions.Count < 3)
            return 0.7; // Default for insufficient data

        // High variance in confidence scores suggests inconsistency
        var confidenceVariance = snapshot.Dimensions
            .Select(d => d.Confidence)
            .DefaultIfEmpty(0.7)
            .Variance();

        return 1.0 - Math.Min(confidenceVariance, 0.5) * 2;
    }

    /// <summary>
    /// Calculate signal-to-noise ratio
    /// </summary>
    private double CalculateSignalToNoise(BeliefSnapshot snapshot)
    {
        if (!snapshot.Dimensions.Any())
            return 0;

        var avgConfidence = snapshot.Dimensions.Average(d => d.Confidence);
        var avgUncertainty = snapshot.Dimensions.Average(d => d.Uncertainty);

        return avgUncertainty > 0 ? avgConfidence / avgUncertainty : avgConfidence;
    }

    /// <summary>
    /// Detect potential contradictions
    /// </summary>
    private List<string> DetectContradictions(BeliefSnapshot snapshot, UserProfile profile)
    {
        var contradictions = new List<string>();

        // Look for opposing dimensions with high confidence
        var dimensions = snapshot.Dimensions.Where(d => d.Position.HasValue).ToList();
        
        for (int i = 0; i < dimensions.Count; i++)
        {
            for (int j = i + 1; j < dimensions.Count; j++)
            {
                if (AreContradictory(dimensions[i], dimensions[j]))
                {
                    contradictions.Add($"{dimensions[i].Name} vs {dimensions[j].Name}");
                }
            }
        }

        return contradictions;
    }

    /// <summary>
    /// Check if two dimensions are contradictory
    /// </summary>
    private bool AreContradictory(BeliefDimension d1, BeliefDimension d2)
    {
        // Simplified - in production: use domain knowledge about incompatible beliefs
        if (!d1.Position.HasValue || !d2.Position.HasValue)
            return false;

        // If both have opposite strong positions
        if (Math.Abs(d1.Position.Value) > 0.7 && Math.Abs(d2.Position.Value) > 0.7)
        {
            if (Math.Sign(d1.Position.Value) != Math.Sign(d2.Position.Value))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Generate narrative summary (placeholder)
    /// </summary>
    private string GenerateNarrativeSummary(BeliefSnapshot snapshot)
    {
        var topValues = snapshot.Values.Take(3).Select(v => v.Name);
        var topDimensions = snapshot.Dimensions
            .OrderByDescending(d => d.Confidence)
            .Take(3)
            .Select(d => d.Name);

        return $"Based on {snapshot.InteractionCount} interactions, this individual values {string.Join(", ", topValues)}. " +
               $"Key belief dimensions: {string.Join(", ", topDimensions)}. " +
               $"Overall model confidence: {snapshot.OverallConfidence:P0}.";
    }

    /// <summary>
    /// Check whether the belief model has converged enough to stop asking questions.
    /// Returns a ConvergenceResult indicating whether to stop and why.
    /// </summary>
    public ConvergenceResult CheckConvergence(BeliefSnapshot snapshot, UserProfile profile)
    {
        var result = new ConvergenceResult();

        // Minimum questions before we even consider convergence
        const int minQuestions = 8;
        if (profile.InteractionCount < minQuestions)
        {
            result.ShouldStop = false;
            result.Reason = $"Need at least {minQuestions} responses before convergence check.";
            return result;
        }

        // Criterion 1: High overall confidence
        const double highConfidenceThreshold = 0.75;
        if (snapshot.OverallConfidence >= highConfidenceThreshold)
        {
            result.ShouldStop = true;
            result.Reason = $"Model confidence ({snapshot.OverallConfidence:P0}) exceeds threshold ({highConfidenceThreshold:P0}).";
            result.Confidence = snapshot.OverallConfidence;
            return result;
        }

        // Criterion 2: Diminishing returns — confidence growth has plateaued
        var recentSnapshots = profile.HistoricalSnapshots.TakeLast(5).ToList();
        if (recentSnapshots.Count >= 5)
        {
            var confidenceGrowth = snapshot.OverallConfidence - recentSnapshots.First().OverallConfidence;
            const double minGrowthThreshold = 0.03;
            if (confidenceGrowth < minGrowthThreshold && snapshot.OverallConfidence >= 0.55)
            {
                result.ShouldStop = true;
                result.Reason = $"Confidence growth has plateaued (only {confidenceGrowth:P2} growth over last 5 questions).";
                result.Confidence = snapshot.OverallConfidence;
                return result;
            }
        }

        // Criterion 3: All key dimensions have sufficient confidence
        const double dimensionConfidenceThreshold = 0.6;
        const int minDimensionsForConvergence = 6;
        var highConfidenceDimensions = snapshot.Dimensions
            .Where(d => d.Confidence >= dimensionConfidenceThreshold)
            .ToList();
        if (highConfidenceDimensions.Count >= minDimensionsForConvergence && snapshot.OverallConfidence >= 0.5)
        {
            result.ShouldStop = true;
            result.Reason = $"{highConfidenceDimensions.Count} dimensions have high confidence (≥{dimensionConfidenceThreshold:P0}).";
            result.Confidence = snapshot.OverallConfidence;
            return result;
        }

        // Criterion 4: Entropy is low (beliefs are well-defined)
        if (snapshot.Statistics.Entropy < 0.3 && snapshot.OverallConfidence >= 0.5)
        {
            result.ShouldStop = true;
            result.Reason = $"Belief distribution is well-defined (entropy: {snapshot.Statistics.Entropy:F3}).";
            result.Confidence = snapshot.OverallConfidence;
            return result;
        }

        // Criterion 5: Hard cap — don't ask more than 30 questions
        const int maxQuestions = 30;
        if (profile.InteractionCount >= maxQuestions)
        {
            result.ShouldStop = true;
            result.Reason = $"Reached maximum question limit ({maxQuestions}).";
            result.Confidence = snapshot.OverallConfidence;
            return result;
        }

        result.ShouldStop = false;
        result.Reason = $"Continuing: confidence={snapshot.OverallConfidence:P2}, " +
                        $"high-confidence dims={highConfidenceDimensions.Count}/{minDimensionsForConvergence}, " +
                        $"entropy={snapshot.Statistics.Entropy:F3}";
        result.Confidence = snapshot.OverallConfidence;
        return result;
    }

    #region Helper Classes

    private BeliefSnapshot CreateInitialSnapshot(string userId)
    {
        return new BeliefSnapshot
        {
            UserId = userId,
            InteractionCount = 0,
            OverallConfidence = 0.1,
            MoralFoundations = new MoralFoundationsProfile
            {
                Care = new Foundation { Score = 5.0, Confidence = 0.1, StandardError = 2.0 },
                Fairness = new Foundation { Score = 5.0, Confidence = 0.1, StandardError = 2.0 },
                Loyalty = new Foundation { Score = 5.0, Confidence = 0.1, StandardError = 2.0 },
                Authority = new Foundation { Score = 5.0, Confidence = 0.1, StandardError = 2.0 },
                Sanctity = new Foundation { Score = 5.0, Confidence = 0.1, StandardError = 2.0 },
                Liberty = new Foundation { Score = 5.0, Confidence = 0.1, StandardError = 2.0 }
            }
        };
    }

    private BeliefSnapshot CloneSnapshot(BeliefSnapshot source)
    {
        // Deep clone (simplified - in production use proper serialization)
        return new BeliefSnapshot
        {
            UserId = source.UserId,
            Timestamp = DateTime.UtcNow,
            InteractionCount = source.InteractionCount,
            Dimensions = source.Dimensions.Select(d => new BeliefDimension
            {
                Name = d.Name,
                Category = d.Category,
                Position = d.Position,
                Confidence = d.Confidence,
                Uncertainty = d.Uncertainty,
                SampleSize = d.SampleSize,
                EvidenceIds = new List<string>(d.EvidenceIds)
            }).ToList(),
            Values = source.Values.Select(v => new InferredValue
            {
                Name = v.Name,
                Description = v.Description,
                ImportanceScore = v.ImportanceScore,
                Confidence = v.Confidence,
                EvidenceIds = new List<string>(v.EvidenceIds)
            }).ToList(),
            MoralFoundations = new MoralFoundationsProfile
            {
                Care = CloneFoundation(source.MoralFoundations.Care),
                Fairness = CloneFoundation(source.MoralFoundations.Fairness),
                Loyalty = CloneFoundation(source.MoralFoundations.Loyalty),
                Authority = CloneFoundation(source.MoralFoundations.Authority),
                Sanctity = CloneFoundation(source.MoralFoundations.Sanctity),
                Liberty = CloneFoundation(source.MoralFoundations.Liberty)
            },
            OverallConfidence = source.OverallConfidence,
            NarrativeSummary = source.NarrativeSummary,
            Statistics = source.Statistics
        };
    }

    private Foundation CloneFoundation(Foundation f) => new Foundation
    {
        Score = f.Score,
        Confidence = f.Confidence,
        StandardError = f.StandardError
    };

    #endregion
}

/// <summary>
/// Gaussian distribution helper
/// </summary>
internal record GaussianDistribution(double Mean, double Variance);

/// <summary>
/// Extension methods for statistical calculations
/// </summary>
internal static class StatisticalExtensions
{
    public static double Variance(this IEnumerable<double> values)
    {
        var list = values.ToList();
        if (list.Count < 2) return 0;
        
        var mean = list.Average();
        return list.Sum(v => Math.Pow(v - mean, 2)) / (list.Count - 1);
    }
}
