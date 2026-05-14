using Microsoft.SemanticKernel;
using CommonUnderstanding.Models;
using System.Text.Json;

namespace CommonUnderstanding.Services;

/// <summary>
/// Psychometrician Agent - Expert in adaptive survey design and psychometric measurement
/// Generates optimal question batches using latest research in:
/// - Computerized Adaptive Testing (CAT)
/// - Item Response Theory (IRT)
/// - Bayesian active learning
/// - Multi-dimensional adaptive testing
/// </summary>
public class PsychometricianAgent
{
    private readonly SemanticKernelService _kernelService;
    private readonly ILogger<PsychometricianAgent> _logger;

    public PsychometricianAgent(
        SemanticKernelService kernelService,
        ILogger<PsychometricianAgent> logger)
    {
        _kernelService = kernelService;
        _logger = logger;
    }

    /// <summary>
    /// Generate optimal batch of questions using psychometric principles
    /// </summary>
    public async Task<List<UserInteraction>> GenerateAdaptiveQuestionBatchAsync(
        UserProfile profile,
        int batchSize = 5)
    {
        var kernel = _kernelService.GetKernel();
        
        var psychometricContext = BuildPsychometricContext(profile);
        
        var prompt = $$$"""
        Always respond in English.
        You are an expert psychometrician specializing in adaptive belief assessment. You have deep knowledge of:
        
        **Core Psychometric Principles:**
        - Item Response Theory (IRT) - 1PL, 2PL, 3PL models
        - Computerized Adaptive Testing (CAT)
        - Multi-Dimensional Adaptive Testing (MDAT)
        - Bayesian Active Learning for optimal information gain
        - Fisher Information and Maximum Information item selection
        - Content balancing across belief dimensions
        
        **Current Assessment State:**
        {{{psychometricContext}}}
        
        **Task:** Design {{{batchSize}}} optimal questions for this user's next assessment batch.
        
        **Optimization Criteria:**
        1. **Maximize Information Gain** - Target dimensions with highest uncertainty
        2. **Content Balance** - Cover diverse belief domains (moral, political, epistemic, spiritual)
        3. **Difficulty Calibration** - Match question complexity to current understanding level
        4. **Cognitive Load** - Vary question types to prevent fatigue
        5. **Engagement** - Include compelling scenarios that motivate honest reflection
        6. **Discrimination** - High-discrimination items that separate belief positions clearly
        7. **Avoid Floor/Ceiling Effects** - Questions should be informative at user's current belief position
        
        **Question Design Standards:**
        - Use forced-choice formats (multiple choice, scale, ranking) for psychometric rigor
        - Each option should map to a distinct position on the belief dimension
        - Include context/scenarios for ecological validity
        - Ensure options are mutually exclusive and exhaustive
        - Avoid leading questions or socially desirable bias
        - Balance positive/negative framing across batch
        
        **Question Types to Consider:**
        1. **Polytomous Scale Items** (1-5 or 1-10 scales with clear anchor points)
        2. **Multiple Choice Dilemmas** (4-5 options representing different belief positions)
        3. **Forced Rankings** (prioritize values/principles)
        4. **Binary Forced Choice** (when measuring extreme dimensions)
        5. **Scenario-Based Situational Judgment Tests** (realistic vignettes)
        
        **CRITICAL: Output ONLY a valid JSON array with NO preamble, explanation, or markdown formatting.**
        **Start your response with [ and end with ]**
        
        **Required JSON Structure:**
        [
          {
            "question_type": "multiple_choice|scale|ranking|binary",
            "target_dimensions": ["dimension1", "dimension2"],
            "information_potential": 0.85,
            "difficulty_level": "low|medium|high",
            "context": "Brief scenario if needed",
            "question": "Clear, unbiased question text",
            "options": ["Option 1", "Option 2", "Option 3", "Option 4", "Option 5"],
            "dimension_mapping": {
              "Option 1": {"dimension1": 0.8, "dimension2": -0.3},
              "Option 2": {"dimension1": -0.5, "dimension2": 0.7}
            },
            "psychometric_rationale": "Why this question optimally targets uncertainty"
          }
        ]
        
        Generate EXACTLY {{{batchSize}}} questions as a valid JSON array. Do not include any text before or after the JSON array.
        """;

        _logger.LogInformation("Psychometrician Agent generating {BatchSize} optimal questions for user {UserId}", 
            batchSize, profile.Id);

        var result = await kernel.InvokePromptAsync(prompt);
        var questionBatchJson = result.ToString();

        _logger.LogInformation("Psychometrician Agent completed question generation for user {UserId}", profile.Id);

        return ParsePsychometricQuestionBatch(questionBatchJson, profile);
    }

    /// <summary>
    /// Build comprehensive psychometric context for the agent
    /// </summary>
    private string BuildPsychometricContext(UserProfile profile)
    {
        var snapshot = profile.CurrentBeliefSnapshot;
        
        if (snapshot == null)
        {
            return $"""
            **Assessment Stage:** Initial (no prior data)
            **Recommended Strategy:** Foundation-building questions across core belief domains
            **Priority:** Establish baseline on major dimensions (moral foundations, political orientation, epistemic stance, spiritual beliefs)
            """;
        }

        var uncertainDimensions = snapshot.Dimensions
            .Where(d => d.Confidence < 0.7)
            .OrderBy(d => d.Confidence)
            .Take(10)
            .ToList();

        var wellEstimatedDimensions = snapshot.Dimensions
            .Where(d => d.Confidence >= 0.7)
            .OrderByDescending(d => d.Confidence)
            .Take(5)
            .ToList();

        var contradictions = snapshot.Statistics.DetectedContradictions;
        var entropy = snapshot.Statistics.Entropy;
        var consistency = snapshot.Statistics.Consistency;

        return $"""
        **Assessment Stage:** {profile.Stage} ({profile.InteractionCount} interactions completed)
        **Overall Confidence:** {snapshot.OverallConfidence:F3}
        **Model Entropy:** {entropy:F3} (lower = more certain)
        **Response Consistency:** {consistency:F3} (higher = more reliable)
        
        **High-Uncertainty Dimensions (PRIORITY TARGETS):**
        {string.Join("\n", uncertainDimensions.Select(d => 
            $"- {d.Name}: Confidence={d.Confidence:F3}, Uncertainty={d.Uncertainty:F3}, Position={d.Position?.ToString("F2") ?? "unknown"}, Samples={d.SampleSize}"))}
        
        **Well-Estimated Dimensions (for validation/refinement):**
        {string.Join("\n", wellEstimatedDimensions.Select(d => 
            $"- {d.Name}: Confidence={d.Confidence:F3}, Position={d.Position?.ToString("F2") ?? "unknown"}"))}
        
        **Detected Contradictions:**
        {(contradictions.Any() ? string.Join("\n", contradictions.Select(c => $"- {c}")) : "None detected")}
        
        **Moral Foundations Profile:**
        - Care: {snapshot.MoralFoundations.Care.Score:F1} (SE={snapshot.MoralFoundations.Care.StandardError:F2})
        - Fairness: {snapshot.MoralFoundations.Fairness.Score:F1} (SE={snapshot.MoralFoundations.Fairness.StandardError:F2})
        - Loyalty: {snapshot.MoralFoundations.Loyalty.Score:F1} (SE={snapshot.MoralFoundations.Loyalty.StandardError:F2})
        - Authority: {snapshot.MoralFoundations.Authority.Score:F1} (SE={snapshot.MoralFoundations.Authority.StandardError:F2})
        - Sanctity: {snapshot.MoralFoundations.Sanctity.Score:F1} (SE={snapshot.MoralFoundations.Sanctity.StandardError:F2})
        - Liberty: {snapshot.MoralFoundations.Liberty.Score:F1} (SE={snapshot.MoralFoundations.Liberty.StandardError:F2})
        
        **Top Inferred Values:**
        {string.Join("\n", snapshot.Values.OrderByDescending(v => v.ImportanceScore).Take(5).Select(v => 
            $"- {v.Name}: Importance={v.ImportanceScore:F1}, Confidence={v.Confidence:F3}"))}
        
        **Previous Question History (avoid duplication):**
        {string.Join("\n", profile.Interactions.TakeLast(5).Select(i => $"- {i.Content.Question}"))}
        
        **Psychometric Recommendations:**
        {GeneratePsychometricRecommendations(profile, snapshot)}
        """;
    }

    /// <summary>
    /// Generate specific psychometric recommendations
    /// </summary>
    private string GeneratePsychometricRecommendations(UserProfile profile, BeliefSnapshot snapshot)
    {
        var recommendations = new List<string>();

        // Stage-based recommendations
        switch (profile.Stage)
        {
            case DiscoveryStage.Initial:
                recommendations.Add("- Use broad, foundational questions to establish baseline across major belief domains");
                recommendations.Add("- Include both cognitive (what you think) and affective (how you feel) items");
                recommendations.Add("- Start with moderate difficulty to gauge response patterns");
                break;
            
            case DiscoveryStage.Foundation:
                recommendations.Add("- Narrow focus to dimensions showing highest variance");
                recommendations.Add("- Introduce moral dilemmas to reveal value hierarchies");
                recommendations.Add("- Use comparative forced-choice items to establish relative importance");
                break;
            
            case DiscoveryStage.Exploration:
                recommendations.Add("- Target specific uncertain dimensions with high-discrimination items");
                recommendations.Add("- Probe contradictions with carefully designed consistency checks");
                recommendations.Add("- Include situational judgment tests for ecological validity");
                break;
            
            case DiscoveryStage.Refinement:
                recommendations.Add("- Use highly specific items to reduce uncertainty in partially-known dimensions");
                recommendations.Add("- Introduce nuanced scenarios to test boundary conditions");
                recommendations.Add("- Validate well-estimated dimensions with alternate item formulations");
                break;
            
            case DiscoveryStage.Continuous:
                recommendations.Add("- Monitor for belief evolution with longitudinal consistency checks");
                recommendations.Add("- Periodically re-assess foundational dimensions");
                recommendations.Add("- Introduce novel dimensions not yet explored");
                break;
        }

        // Confidence-based recommendations
        if (snapshot.OverallConfidence < 0.5)
        {
            recommendations.Add("- PRIORITY: Use high-information items (broad, multi-dimensional questions)");
            recommendations.Add("- Maximize content coverage - avoid over-sampling any single dimension");
        }
        else if (snapshot.OverallConfidence > 0.8)
        {
            recommendations.Add("- Focus on precision - narrow targeting of remaining uncertain areas");
            recommendations.Add("- Include validation items to confirm high-confidence estimates");
        }

        // Entropy-based recommendations
        if (snapshot.Statistics.Entropy > 2.0)
        {
            recommendations.Add("- High entropy detected - increase structure (forced-choice over open-ended)");
            recommendations.Add("- Use anchoring vignettes to calibrate response scale usage");
        }

        // Consistency-based recommendations
        if (snapshot.Statistics.Consistency < 0.7)
        {
            recommendations.Add("- Low consistency detected - include reverse-coded items to check response validity");
            recommendations.Add("- Consider simpler question formats to reduce cognitive load");
        }

        // Contradiction-based recommendations
        if (snapshot.Statistics.DetectedContradictions.Any())
        {
            recommendations.Add($"- {snapshot.Statistics.DetectedContradictions.Count} contradictions detected - probe these with direct comparison items");
            recommendations.Add("- Use forced rankings to establish clear preference hierarchies");
        }

        return string.Join("\n", recommendations);
    }

    /// <summary>
    /// Parse psychometric question batch from AI response
    /// </summary>
    private List<UserInteraction> ParsePsychometricQuestionBatch(string questionBatchJson, UserProfile profile)
    {
        var questions = new List<UserInteraction>();
        
        try
        {
            // Extract JSON array from response (AI may include preamble text)
            var jsonStart = questionBatchJson.IndexOf('[');
            var jsonEnd = questionBatchJson.LastIndexOf(']');
            
            if (jsonStart == -1 || jsonEnd == -1 || jsonEnd <= jsonStart)
            {
                _logger.LogWarning("No JSON array found in AI response for user {UserId}. Response: {Response}", 
                    profile.Id, questionBatchJson.Length > 200 ? questionBatchJson.Substring(0, 200) + "..." : questionBatchJson);
                return FallbackQuestionGeneration(profile);
            }
            
            var jsonText = questionBatchJson.Substring(jsonStart, jsonEnd - jsonStart + 1);
            
            _logger.LogDebug("Extracted JSON: {JsonLength} characters from {TotalLength} character response", 
                jsonText.Length, questionBatchJson.Length);
            
            // Attempt to parse as JSON
            var jsonDoc = JsonDocument.Parse(jsonText);
            var questionsArray = jsonDoc.RootElement;

            foreach (var questionElement in questionsArray.EnumerateArray())
            {
                var questionType = questionElement.GetProperty("question_type").GetString() ?? "multiple_choice";
                var questionText = questionElement.GetProperty("question").GetString() ?? "";
                var context = questionElement.TryGetProperty("context", out var ctx) ? ctx.GetString() : null;
                
                var targetDimensions = new List<string>();
                if (questionElement.TryGetProperty("target_dimensions", out var dims))
                {
                    foreach (var dim in dims.EnumerateArray())
                    {
                        targetDimensions.Add(dim.GetString() ?? "");
                    }
                }

                var options = new List<string>();
                if (questionElement.TryGetProperty("options", out var opts))
                {
                    foreach (var option in opts.EnumerateArray())
                    {
                        options.Add(option.GetString() ?? "");
                    }
                }

                var interaction = new UserInteraction
                {
                    UserId = profile.Id,
                    Type = MapQuestionType(questionType),
                    Content = new InteractionContent
                    {
                        Question = questionText,
                        Context = context,
                        Format = MapQuestionFormat(questionType),
                        Options = options.Any() ? options : null,
                        MinValue = questionType == "scale" ? 1 : null,
                        MaxValue = questionType == "scale" ? 10 : null,
                        MinLabel = questionType == "scale" ? "Strongly Disagree" : null,
                        MaxLabel = questionType == "scale" ? "Strongly Agree" : null
                    },
                    TargetedDimensions = targetDimensions
                };

                questions.Add(interaction);
                
                _logger.LogDebug("Parsed psychometric question: Type={Type}, Question={QuestionPreview}", 
                    questionType, questionText.Length > 50 ? questionText.Substring(0, 47) + "..." : questionText);
            }
            
            _logger.LogInformation("Successfully parsed {Count} psychometric questions for user {UserId}", 
                questions.Count, profile.Id);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse psychometric question batch as JSON for user {UserId}. Response preview: {Preview}", 
                profile.Id, questionBatchJson.Length > 300 ? questionBatchJson.Substring(0, 297) + "..." : questionBatchJson);
            
            return FallbackQuestionGeneration(profile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error parsing psychometric questions for user {UserId}", profile.Id);
            return FallbackQuestionGeneration(profile);
        }

        return questions;
    }

    /// <summary>
    /// Generate fallback questions when parsing fails
    /// </summary>
    private List<UserInteraction> FallbackQuestionGeneration(UserProfile profile)
    {
        _logger.LogWarning("Using fallback question generation for user {UserId}", profile.Id);
        
        // Return empty list - QuestionPrefetchService will handle fallback to DiscoveryQuestionEngine
        return new List<UserInteraction>();
    }

    private InteractionType MapQuestionType(string questionType)
    {
        return questionType.ToLowerInvariant() switch
        {
            "multiple_choice" => InteractionType.StatementAgreement,
            "scale" => InteractionType.ScaleQuestion,
            "ranking" => InteractionType.ValueRanking,
            "binary" => InteractionType.BinaryChoice,
            "dilemma" => InteractionType.MoralDilemma,
            "scenario" => InteractionType.ScenarioReaction,
            _ => InteractionType.StatementAgreement
        };
    }

    private InteractionFormat MapQuestionFormat(string questionType)
    {
        return questionType.ToLowerInvariant() switch
        {
            "multiple_choice" => InteractionFormat.MultipleChoice,
            "scale" => InteractionFormat.Scale,
            "ranking" => InteractionFormat.Ranking,
            "binary" => InteractionFormat.Binary,
            _ => InteractionFormat.MultipleChoice
        };
    }
}
