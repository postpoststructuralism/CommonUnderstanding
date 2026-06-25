using Microsoft.SemanticKernel;
using CommonUnderstanding.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace CommonUnderstanding.Services;

/// <summary>
/// Analyzes user responses using AI and extracts structured insights.
/// Uses structured JSON prompts for accurate dimension/value extraction
/// with keyword-based fallback when JSON parsing fails.
/// </summary>
public class ResponseAnalysisEngine
{
    private readonly SemanticKernelService _kernelService;
    private readonly ILogger<ResponseAnalysisEngine> _logger;

    // JSON serializer options for parsing AI output
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public ResponseAnalysisEngine(
        SemanticKernelService kernelService,
        ILogger<ResponseAnalysisEngine> logger)
    {
        _kernelService = kernelService;
        _logger = logger;
    }

    /// <summary>
    /// Analyze a user's response to extract belief signals using structured AI output.
    /// Falls back to keyword-based analysis if JSON parsing fails.
    /// </summary>
    public async Task<ResponseAnalysis> AnalyzeResponseAsync(
        UserInteraction interaction,
        UserProfile profile)
    {
        var kernel = _kernelService.GetKernel();
        var prompt = BuildStructuredAnalysisPrompt(interaction, profile);

        _logger.LogInformation("Analyzing response for user {UserId} with structured prompt", profile.Id);
        var startTime = DateTime.UtcNow;

        string analysisText;
        try
        {
            var result = await kernel.InvokePromptAsync(prompt);
            analysisText = result.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI analysis failed for user {UserId}, using keyword fallback", profile.Id);
            return BuildKeywordFallbackAnalysis(interaction);
        }

        var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
        _logger.LogInformation("AI analysis completed in {Duration}ms for user {UserId}",
            duration, profile.Id);

        // Try structured JSON parsing first
        var analysis = TryParseStructuredAnalysis(analysisText, interaction);
        if (analysis != null)
        {
            _logger.LogInformation("Structured analysis succeeded for user {UserId}: {ValueCount} values, {DimCount} dimensions",
                profile.Id, analysis.ImpliedValues.Count, analysis.DimensionUpdates.Count);
            return analysis;
        }

        // Fall back to keyword-based analysis
        _logger.LogWarning("Structured parsing failed for user {UserId}, using keyword fallback", profile.Id);
        return BuildKeywordFallbackAnalysis(interaction);
    }

    /// <summary>
    /// Analyze emotional content of a response
    /// </summary>
    public async Task<EmotionalMarkers> AnalyzeEmotionalContentAsync(string responseText)
    {
        var kernel = _kernelService.GetKernel();

var prompt = $$"""
        Always respond in English.
        Analyze the emotional content of this text. Return ONLY a JSON object:
        {
            "intensity": <0.0-1.0>,
            "certainty": <0.0-1.0>,
            "detectedEmotions": ["emotion1", "emotion2"],
            "conflictIndicator": <0.0-1.0>
        }
        
        Text: '{{responseText}}'
        """;

        _logger.LogInformation("Analyzing emotional content");

        try
        {
            var result = await kernel.InvokePromptAsync(prompt);
            var json = ExtractJsonFromResponse(result.ToString());
            var parsed = JsonSerializer.Deserialize<EmotionalAnalysisResult>(json, JsonOptions);
            if (parsed != null)
            {
                return new EmotionalMarkers
                {
                    Intensity = parsed.Intensity,
                    Certainty = parsed.Certainty,
                    DetectedEmotions = parsed.DetectedEmotions ?? new List<string>(),
                    ConflictIndicator = parsed.ConflictIndicator
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Emotional analysis JSON parsing failed, using keyword fallback");
        }

        return ParseEmotionalAnalysis(responseText);
    }

    /// <summary>
    /// Extract moral foundation activations from a response
    /// </summary>
    public async Task<Dictionary<string, double>> AnalyzeMoralFoundationsAsync(
        string questionText,
        string responseText)
    {
        var kernel = _kernelService.GetKernel();

        var prompt = $$$"""
        Always respond in English.
        Analyze this response through Moral Foundations Theory (Haidt).
        
        Question: {{{questionText}}}
        Response: {{{responseText}}}
        
        Return ONLY a JSON object with scores 0-10 for each foundation:
        {
            "care": <0-10>,
            "fairness": <0-10>,
            "loyalty": <0-10>,
            "authority": <0-10>,
            "sanctity": <0-10>,
            "liberty": <0-10>
        }
        """;

        _logger.LogInformation("Analyzing moral foundations");

        try
        {
            var result = await kernel.InvokePromptAsync(prompt);
            var json = ExtractJsonFromResponse(result.ToString());
            var parsed = JsonSerializer.Deserialize<Dictionary<string, double>>(json, JsonOptions);
            if (parsed != null)
            {
                // Map lowercase keys to PascalCase for the model
                return new Dictionary<string, double>
                {
                    ["Care"] = parsed.GetValueOrDefault("care", 5.0),
                    ["Fairness"] = parsed.GetValueOrDefault("fairness", 5.0),
                    ["Loyalty"] = parsed.GetValueOrDefault("loyalty", 5.0),
                    ["Authority"] = parsed.GetValueOrDefault("authority", 5.0),
                    ["Sanctity"] = parsed.GetValueOrDefault("sanctity", 5.0),
                    ["Liberty"] = parsed.GetValueOrDefault("liberty", 5.0)
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Moral foundations JSON parsing failed, using keyword fallback");
        }

        return ParseMoralFoundations(responseText);
    }

    #region Structured Analysis

    /// <summary>
    /// Build a prompt that requests structured JSON output from the AI.
    /// </summary>
    private string BuildStructuredAnalysisPrompt(UserInteraction interaction, UserProfile profile)
    {
        var previousInsights = profile.CurrentBeliefSnapshot?.NarrativeSummary ?? "No prior insights yet.";
        var currentDimensions = profile.CurrentBeliefSnapshot?.Dimensions
            .Select(d => $"{d.Name}: position={d.Position?.ToString("F2") ?? "unknown"}, confidence={d.Confidence:F2}")
            .ToList() ?? new List<string>();

        return $$$"""
        Always respond in English.
        You are an expert psychometric analyst mapping a person's belief system. Analyze this response carefully.

        QUESTION ASKED: {{{interaction.Content.Question}}}
        USER'S RESPONSE: {{{interaction.Response.RawText}}}
        
        PREVIOUS INSIGHTS: {{{previousInsights}}}
        KNOWN DIMENSIONS: {{{string.Join("; ", currentDimensions)}}}

        Return ONLY a valid JSON object (no markdown, no explanation outside the JSON) with this exact structure:
        {
            "analysisConfidence": <0.0-1.0, how confident you are in this analysis>,
            "responseQuality": <0.0-1.0, how thoughtful/substantive the response is>,
            "narrativeInsight": "<1-2 sentence insight about what this reveals>",
            "dimensionUpdates": [
                {
                    "dimensionName": "<dimension name, e.g. authority, compassion, individualism-collectivism, political-economic, spirituality, tradition, fairness, etc.>",
                    "category": "<Political|Religious|Ethical|Metaphysical|Social|Economic>",
                    "position": <-1.0 to 1.0, where -1 is one extreme and +1 is the opposite>,
                    "evidenceWeight": <0.0-1.0, how strongly this response supports the position>,
                    "evidence": "<brief quote or reasoning from the response>"
                }
            ],
            "impliedValues": ["<value1>", "<value2>"],
            "moralFoundationScores": {
                "care": <0-10>,
                "fairness": <0-10>,
                "loyalty": <0-10>,
                "authority": <0-10>,
                "sanctity": <0-10>,
                "liberty": <0-10>
            },
            "reasoningPatterns": ["<Consequentialist|Deontological|VirtueEthics|EmotionalIntuitive|Utilitarian|RightsBased|Relational>"],
            "suggestedFollowUps": ["<brief follow-up question idea>"]
        }

        IMPORTANT DIMENSION GUIDELINES:
        - "political-economic": -1 = free market/libertarian, +1 = socialist/redistributionist
        - "individualism-collectivism": -1 = radical individualist, +1 = strong collectivist
        - "authority": -1 = anti-authority/anarchist, +1 = strong respect for hierarchy
        - "tradition": -1 = radical progressive, +1 = strong traditionalist
        - "spirituality": -1 = strict materialist/atheist, +1 = deeply spiritual/religious
        - "compassion": -1 = low empathy/self-interested, +1 = high empathy/altruistic
        - "fairness": -1 = accepts inequality as natural, +1 = demands strict equality
        - "security": -1 = values freedom over safety, +1 = values safety over freedom
        - "change-orientation": -1 = prefers stability, +1 = embraces radical change
        - "human-nature": -1 = people are fundamentally bad, +1 = people are fundamentally good
        - "science-trust": -1 = skeptical of science, +1 = strong trust in science
        - "nationalism": -1 = cosmopolitan/globalist, +1 = strong nationalist
        - "environment": -1 = dismissive of environmental concerns, +1 = strong environmentalist
        - "consequentialism-deontology": -1 = pure consequentialist, +1 = pure deontologist
        - "systemic-thinking": -1 = individual responsibility focus, +1 = systemic/structural focus

        Only include dimensions where the response provides clear evidence. For moral foundations, use 5.0 as neutral/default when no evidence exists.
        """;
    }

    /// <summary>
    /// Try to parse structured JSON from the AI response.
    /// Returns null if parsing fails, so caller can fall back to keyword analysis.
    /// </summary>
    private ResponseAnalysis? TryParseStructuredAnalysis(string analysisText, UserInteraction interaction)
    {
        try
        {
            var json = ExtractJsonFromResponse(analysisText);
            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogWarning("No JSON found in AI response");
                return null;
            }

            var parsed = JsonSerializer.Deserialize<StructuredAnalysisResult>(json, JsonOptions);
            if (parsed == null)
            {
                _logger.LogWarning("JSON deserialization returned null");
                return null;
            }

            // Validate we got meaningful data
            if (parsed.DimensionUpdates == null && parsed.ImpliedValues == null)
            {
                _logger.LogWarning("Parsed JSON has no dimension updates or implied values");
                return null;
            }

            var analysis = new ResponseAnalysis
            {
                AnalyzedAt = DateTime.UtcNow,
                NarrativeAnalysis = parsed.NarrativeInsight ?? analysisText,
                AnalysisConfidence = Clamp(parsed.AnalysisConfidence, 0.1, 1.0),
                ResponseQuality = Clamp(parsed.ResponseQuality, 0.1, 1.0),
                DimensionUpdates = parsed.DimensionUpdates?.Select(d => new DimensionUpdate
                {
                    DimensionName = d.DimensionName ?? "unknown",
                    Category = d.Category ?? "General",
                    Position = Clamp(d.Position, -1.0, 1.0),
                    EvidenceWeight = Clamp(d.EvidenceWeight, 0.1, 1.0),
                    Evidence = d.Evidence ?? ""
                }).ToList() ?? new List<DimensionUpdate>(),
                ImpliedValues = parsed.ImpliedValues ?? new List<string>(),
                ReasoningPatterns = parsed.ReasoningPatterns ?? new List<string>(),
                MoralFoundationScores = MapMoralFoundations(parsed.MoralFoundationScores),
                SuggestedFollowUps = parsed.SuggestedFollowUps ?? new List<string>()
            };

            return analysis;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "JSON parsing error in structured analysis");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error in structured analysis parsing");
            return null;
        }
    }

    /// <summary>
    /// Extract JSON object from AI response that may contain markdown or extra text.
    /// </summary>
    private static string ExtractJsonFromResponse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        // Try to find JSON between ```json and ``` markers
        var jsonBlockMatch = Regex.Match(text, @"```(?:json)?\s*\n?(\{[\s\S]*?\})\s*\n?```", RegexOptions.IgnoreCase);
        if (jsonBlockMatch.Success)
            return jsonBlockMatch.Groups[1].Value;

        // Try to find the first { and last } for raw JSON
        var firstBrace = text.IndexOf('{');
        var lastBrace = text.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
            return text.Substring(firstBrace, lastBrace - firstBrace + 1);

        return string.Empty;
    }

    private static double Clamp(double value, double min, double max)
        => Math.Max(min, Math.Min(max, value));

    private static Dictionary<string, double> MapMoralFoundations(Dictionary<string, double>? scores)
    {
        var result = new Dictionary<string, double>();
        var defaults = new Dictionary<string, double>
        {
            ["Care"] = 5.0, ["Fairness"] = 5.0, ["Loyalty"] = 5.0,
            ["Authority"] = 5.0, ["Sanctity"] = 5.0, ["Liberty"] = 5.0
        };

        if (scores == null) return defaults;

        foreach (var (key, defaultValue) in defaults)
        {
            // Try case-insensitive match
            var match = scores.FirstOrDefault(kvp =>
                kvp.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            result[key] = match.Value > 0 ? Clamp(match.Value, 0, 10) : defaultValue;
        }

        return result;
    }

    #endregion

    #region Keyword Fallback (Robust)

    /// <summary>
    /// Build analysis using keyword matching when AI structured output fails.
    /// This is improved over the original with better keyword coverage and scoring.
    /// </summary>
    private ResponseAnalysis BuildKeywordFallbackAnalysis(UserInteraction interaction)
    {
        var text = interaction.Response.RawText.ToLowerInvariant();
        var question = interaction.Content.Question.ToLowerInvariant();

        var analysis = new ResponseAnalysis
        {
            AnalyzedAt = DateTime.UtcNow,
            NarrativeAnalysis = "Analysis based on keyword detection (AI structured output unavailable).",
            AnalysisConfidence = 0.5, // Lower confidence for keyword-based
            ResponseQuality = EvaluateResponseQuality(text),
            DimensionUpdates = ExtractDimensionUpdatesFromKeywords(text, question),
            ImpliedValues = ExtractImpliedValuesFromKeywords(text),
            ReasoningPatterns = ExtractReasoningPatternsFromKeywords(text),
            MoralFoundationScores = ExtractMoralFoundationScoresFromKeywords(text),
            SuggestedFollowUps = new List<string>()
        };

        return analysis;
    }

    private double EvaluateResponseQuality(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0.3;
        if (text.Length < 20) return 0.4;
        if (text.Length < 50) return 0.6;
        if (text.Length < 100) return 0.75;
        if (text.Contains("because") || text.Contains("therefore") || text.Contains("however"))
            return 0.9;
        return 0.8;
    }

    private List<DimensionUpdate> ExtractDimensionUpdatesFromKeywords(string text, string question)
    {
        var updates = new List<DimensionUpdate>();

        // Dimension keyword maps with position hints
        var dimensionMap = new Dictionary<string, (string[] positiveKeywords, string[] negativeKeywords, string category)>
        {
            ["authority"] = (
                new[] { "respect authority", "follow rules", "law and order", "hierarchy", "obey", "tradition matters" },
                new[] { "question authority", "no hierarchy", "defy", "resist", "anarch", "freedom from" },
                "Social"
            ),
            ["compassion"] = (
                new[] { "help others", "care for", "empathy", "compassion", "kindness", "altruis" },
                new[] { "self-interest", "not my problem", "their own fault", "deserve" },
                "Ethical"
            ),
            ["individualism-collectivism"] = (
                new[] { "community", "collective", "together", "common good", "society", "group" },
                new[] { "individual", "personal freedom", "my choice", "self-reliance", "independence" },
                "Social"
            ),
            ["political-economic"] = (
                new[] { "redistribut", "welfare", "public service", "tax the rich", "universal", "social program" },
                new[] { "free market", "capitalism", "private sector", "deregulat", "low tax", "libert" },
                "Economic"
            ),
            ["tradition"] = (
                new[] { "tradition", "preserve", "heritage", "time-tested", "wisdom of the past" },
                new[] { "progress", "change", "reform", "outdated", "move forward", "evolve" },
                "Social"
            ),
            ["spirituality"] = (
                new[] { "god", "spiritual", "soul", "faith", "divine", "sacred", "religion", "pray" },
                new[] { "atheist", "material", "no god", "science explains", "secular" },
                "Metaphysical"
            ),
            ["fairness"] = (
                new[] { "fair", "equal", "justice", "equity", "level playing field", "deserve equal" },
                new[] { "life isn't fair", "earn", "merit", "natural inequality" },
                "Ethical"
            ),
            ["security"] = (
                new[] { "safety", "security", "protect", "stable", "order", "safe" },
                new[] { "freedom", "liberty", "risk", "autonomy", "rights" },
                "Political"
            ),
            ["human-nature"] = (
                new[] { "people are good", "trust", "inherently good", "cooperat" },
                new[] { "people are selfish", "greed", "cannot trust", "corrupt" },
                "Metaphysical"
            ),
            ["science-trust"] = (
                new[] { "science", "evidence", "data", "research", "empirical", "proven" },
                new[] { "science doesn't", "not everything is", "beyond science", "other ways of knowing" },
                "Metaphysical"
            ),
            ["nationalism"] = (
                new[] { "my country", "nation first", "patriot", "border", "sovereign" },
                new[] { "global", "humanity", "cosmopolitan", "world citizen", "no borders" },
                "Political"
            ),
            ["environment"] = (
                new[] { "environment", "climate", "planet", "sustain", "green", "nature", "earth" },
                new[] { "overblown", "not a priority", "economy first", "natural cycles" },
                "Political"
            ),
            ["systemic-thinking"] = (
                new[] { "system", "structural", "institution", "society causes", "not their fault", "circumstance" },
                new[] { "personal responsibility", "choices", "work hard", "pull yourself up" },
                "Social"
            ),
            ["consequentialism-deontology"] = (
                new[] { "outcome", "consequence", "result", "greater good", "ends justify", "harm" },
                new[] { "principle", "duty", "rule", "regardless of outcome", "intrinsically wrong" },
                "Ethical"
            ),
            ["change-orientation"] = (
                new[] { "change", "progress", "reform", "innovate", "disrupt", "transform" },
                new[] { "stable", "preserve", "maintain", "gradual", "cautious" },
                "Social"
            )
        };

        foreach (var (dimName, (posKw, negKw, category)) in dimensionMap)
        {
            double? position = null;
            double evidenceWeight = 0.0;

            foreach (var kw in posKw)
            {
                if (text.Contains(kw))
                {
                    position = (position ?? 0) + 0.3;
                    evidenceWeight = Math.Max(evidenceWeight, 0.7);
                }
            }
            foreach (var kw in negKw)
            {
                if (text.Contains(kw))
                {
                    position = (position ?? 0) - 0.3;
                    evidenceWeight = Math.Max(evidenceWeight, 0.7);
                }
            }

            if (position.HasValue && evidenceWeight > 0)
            {
                updates.Add(new DimensionUpdate
                {
                    DimensionName = dimName,
                    Category = category,
                    Position = Clamp(position.Value, -1.0, 1.0),
                    EvidenceWeight = Clamp(evidenceWeight, 0.1, 0.8),
                    Evidence = $"Keyword detection in response"
                });
            }
        }

        return updates;
    }

    private List<string> ExtractImpliedValuesFromKeywords(string text)
    {
        var values = new List<string>();
        var valueKeywords = new Dictionary<string, string[]>
        {
            ["freedom"] = new[] { "freedom", "liberty", "autonomy", "free will", "independence" },
            ["equality"] = new[] { "equality", "equal", "equity", "fairness", "level playing field" },
            ["justice"] = new[] { "justice", "fair", "just", "righting wrongs" },
            ["compassion"] = new[] { "compassion", "empathy", "kindness", "care", "helping" },
            ["loyalty"] = new[] { "loyalty", "faithful", "allegiance", "devotion" },
            ["tradition"] = new[] { "tradition", "heritage", "preserve", "ancestral" },
            ["progress"] = new[] { "progress", "innovation", "forward", "improve", "advance" },
            ["security"] = new[] { "security", "safety", "stability", "protection" },
            ["authenticity"] = new[] { "authentic", "genuine", "true to myself", "real" },
            ["community"] = new[] { "community", "together", "belonging", "common good" },
            ["wisdom"] = new[] { "wisdom", "knowledge", "understanding", "insight" },
            ["responsibility"] = new[] { "responsibility", "duty", "obligation", "accountable" },
            ["peace"] = new[] { "peace", "harmony", "nonviolence", "reconciliation" },
            ["truth"] = new[] { "truth", "honesty", "integrity", "transparency" },
            ["courage"] = new[] { "courage", "bravery", "bold", "stand up" }
        };

        foreach (var (value, keywords) in valueKeywords)
        {
            if (keywords.Any(kw => text.Contains(kw)))
                values.Add(value);
        }

        return values;
    }

    private List<string> ExtractReasoningPatternsFromKeywords(string text)
    {
        var patterns = new List<string>();

        if (text.Contains("consequence") || text.Contains("outcome") || text.Contains("result") ||
            text.Contains("greater good") || text.Contains("harm") || text.Contains("benefit"))
            patterns.Add("Consequentialist");

        if (text.Contains("duty") || text.Contains("principle") || text.Contains("regardless") ||
            text.Contains("intrinsically") || text.Contains("categorical"))
            patterns.Add("Deontological");

        if (text.Contains("virtue") || text.Contains("character") || text.Contains("integrity") ||
            text.Contains("honor") || text.Contains("wisdom"))
            patterns.Add("VirtueEthics");

        if (text.Contains("feel") || text.Contains("emotion") || text.Contains("gut") ||
            text.Contains("heart") || text.Contains("intuition"))
            patterns.Add("EmotionalIntuitive");

        if (text.Contains("rights") || text.Contains("entitled") || text.Contains("autonomy") ||
            text.Contains("consent"))
            patterns.Add("RightsBased");

        if (text.Contains("relationship") || text.Contains("community") || text.Contains("care") ||
            text.Contains("connection"))
            patterns.Add("Relational");

        return patterns;
    }

    private Dictionary<string, double> ExtractMoralFoundationScoresFromKeywords(string text)
    {
        var scores = new Dictionary<string, double>
        {
            ["Care"] = 5.0, ["Fairness"] = 5.0, ["Loyalty"] = 5.0,
            ["Authority"] = 5.0, ["Sanctity"] = 5.0, ["Liberty"] = 5.0
        };

        // Care/Harm
        if (text.Contains("compassion") || text.Contains("empathy") || text.Contains("care for") ||
            text.Contains("suffer") || text.Contains("hurt") || text.Contains("kindness"))
            scores["Care"] = 7.5;
        if (text.Contains("don't care") || text.Contains("not my problem"))
            scores["Care"] = 2.5;

        // Fairness/Cheating
        if (text.Contains("fair") || text.Contains("justice") || text.Contains("equal") ||
            text.Contains("deserve") || text.Contains("rights"))
            scores["Fairness"] = 7.5;
        if (text.Contains("life isn't fair") || text.Contains("some people deserve less"))
            scores["Fairness"] = 2.5;

        // Loyalty/Betrayal
        if (text.Contains("loyal") || text.Contains("community") || text.Contains("my country") ||
            text.Contains("family first") || text.Contains("allegiance"))
            scores["Loyalty"] = 7.5;
        if (text.Contains("no allegiance") || text.Contains("self-interest"))
            scores["Loyalty"] = 2.5;

        // Authority/Subversion
        if (text.Contains("respect") || text.Contains("authority") || text.Contains("law") ||
            text.Contains("order") || text.Contains("tradition"))
            scores["Authority"] = 7.5;
        if (text.Contains("question authority") || text.Contains("defy") || text.Contains("anarch"))
            scores["Authority"] = 2.5;

        // Sanctity/Degradation
        if (text.Contains("sacred") || text.Contains("pure") || text.Contains("holy") ||
            text.Contains("disgust") || text.Contains("degrad"))
            scores["Sanctity"] = 7.5;
        if (text.Contains("nothing sacred") || text.Contains("material"))
            scores["Sanctity"] = 2.5;

        // Liberty/Oppression
        if (text.Contains("freedom") || text.Contains("liberty") || text.Contains("autonomy") ||
            text.Contains("rights") || text.Contains("oppress"))
            scores["Liberty"] = 7.5;
        if (text.Contains("security over freedom") || text.Contains("restrict"))
            scores["Liberty"] = 2.5;

        return scores;
    }

    #endregion

    #region Legacy Methods (kept for compatibility)

    private EmotionalMarkers ParseEmotionalAnalysis(string analysisText)
    {
        return new EmotionalMarkers
        {
            Intensity = ExtractNumericValue(analysisText, "intensity"),
            Certainty = ExtractNumericValue(analysisText, "certainty"),
            DetectedEmotions = ExtractEmotions(analysisText),
            ConflictIndicator = ExtractNumericValue(analysisText, "conflict")
        };
    }

    private double ExtractNumericValue(string text, string label)
    {
        var pattern = $"{label}";
        var index = text.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            var substring = text.Substring(index + pattern.Length).Trim();
            var match = Regex.Match(substring, @"[\d.]+");
            if (match.Success && double.TryParse(match.Value, out var value))
            {
                return value > 1 ? value / 10.0 : value;
            }
        }
        return 0.5;
    }

    private List<string> ExtractEmotions(string text)
    {
        var emotions = new List<string>();
        var emotionKeywords = new[] {
            "anger", "joy", "fear", "disgust", "sadness", "compassion",
            "pride", "shame", "guilt", "gratitude", "hope", "anxiety"
        };

        foreach (var emotion in emotionKeywords)
        {
            if (text.Contains(emotion, StringComparison.OrdinalIgnoreCase))
                emotions.Add(emotion);
        }

        return emotions;
    }

    private Dictionary<string, double> ParseMoralFoundations(string text)
    {
        var foundations = new Dictionary<string, double>();
        var foundationNames = new[] { "Care", "Fairness", "Loyalty", "Authority", "Sanctity", "Liberty" };

        foreach (var name in foundationNames)
        {
            var score = ExtractFoundationScore(text, name);
            foundations[name] = score;
        }

        return foundations;
    }

    private double ExtractFoundationScore(string text, string foundation)
    {
        var index = text.IndexOf(foundation, StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            var substring = text.Substring(index + foundation.Length).Trim();
            var match = Regex.Match(substring, @"[\d.]+");
            if (match.Success && double.TryParse(match.Value, out var value))
            {
                return value;
            }
        }
        return 5.0;
    }

    #endregion
}

#region JSON Deserialization Types

/// <summary>
/// Expected JSON structure from the AI analysis prompt.
/// </summary>
internal class StructuredAnalysisResult
{
    [JsonPropertyName("analysisConfidence")]
    public double AnalysisConfidence { get; set; } = 0.7;

    [JsonPropertyName("responseQuality")]
    public double ResponseQuality { get; set; } = 0.7;

    [JsonPropertyName("narrativeInsight")]
    public string? NarrativeInsight { get; set; }

    [JsonPropertyName("dimensionUpdates")]
    public List<StructuredDimensionUpdate>? DimensionUpdates { get; set; }

    [JsonPropertyName("impliedValues")]
    public List<string>? ImpliedValues { get; set; }

    [JsonPropertyName("moralFoundationScores")]
    public Dictionary<string, double>? MoralFoundationScores { get; set; }

    [JsonPropertyName("reasoningPatterns")]
    public List<string>? ReasoningPatterns { get; set; }

    [JsonPropertyName("suggestedFollowUps")]
    public List<string>? SuggestedFollowUps { get; set; }
}

internal class StructuredDimensionUpdate
{
    [JsonPropertyName("dimensionName")]
    public string? DimensionName { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("position")]
    public double Position { get; set; }

    [JsonPropertyName("evidenceWeight")]
    public double EvidenceWeight { get; set; } = 0.5;

    [JsonPropertyName("evidence")]
    public string? Evidence { get; set; }
}

internal class EmotionalAnalysisResult
{
    [JsonPropertyName("intensity")]
    public double Intensity { get; set; } = 0.5;

    [JsonPropertyName("certainty")]
    public double Certainty { get; set; } = 0.5;

    [JsonPropertyName("detectedEmotions")]
    public List<string>? DetectedEmotions { get; set; }

    [JsonPropertyName("conflictIndicator")]
    public double ConflictIndicator { get; set; }
}

#endregion
