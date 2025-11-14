using Microsoft.SemanticKernel;
using CommonUnderstanding.Models;
using System.Text.Json;

namespace CommonUnderstanding.Services;

/// <summary>
/// Analyzes user responses using AI and extracts structured insights
/// </summary>
public class ResponseAnalysisEngine
{
    private readonly SemanticKernelService _kernelService;
    private readonly ILogger<ResponseAnalysisEngine> _logger;

    public ResponseAnalysisEngine(
        SemanticKernelService kernelService,
        ILogger<ResponseAnalysisEngine> logger)
    {
        _kernelService = kernelService;
        _logger = logger;
    }

    /// <summary>
    /// Analyze a user's response to extract belief signals
    /// </summary>
    public async Task<ResponseAnalysis> AnalyzeResponseAsync(
        UserInteraction interaction,
        UserProfile profile)
    {
        var kernel = _kernelService.GetKernel();
        var prompt = BuildAnalysisPrompt(interaction, profile);

        _logger.LogInformation("Analyzing response for user {UserId}", profile.Id);
        var startTime = DateTime.UtcNow;
        
        var result = await kernel.InvokePromptAsync(prompt);
        var analysisText = result.ToString();
        
        var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
        _logger.LogInformation("AI analysis completed in {Duration}ms for user {UserId}", 
            duration, profile.Id);

        var analysis = ParseAnalysisResponse(analysisText);
        return analysis;
    }

    /// <summary>
    /// Analyze emotional content of a response
    /// </summary>
    public async Task<EmotionalMarkers> AnalyzeEmotionalContentAsync(string responseText)
    {
        var kernel = _kernelService.GetKernel();

        var prompt = $"What emotions are present in this text? '{responseText}' List them.";

        _logger.LogInformation("Analyzing emotional content");
        
        var result = await kernel.InvokePromptAsync(prompt);
        return ParseEmotionalAnalysis(result.ToString());
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
        Analyze this response through the lens of Moral Foundations Theory (Haidt).
        
        Question: {{{questionText}}}
        Response: {{{responseText}}}
        
        Rate how much each moral foundation is activated (0-10 scale):
        
        1. CARE/HARM - Compassion for suffering, kindness, nurturing
        2. FAIRNESS/CHEATING - Justice, rights, equality, reciprocity  
        3. LOYALTY/BETRAYAL - Group solidarity, patriotism, team loyalty
        4. AUTHORITY/SUBVERSION - Respect for tradition, hierarchy, leadership
        5. SANCTITY/DEGRADATION - Purity, disgust, elevation, sacredness
        6. LIBERTY/OPPRESSION - Freedom from domination, resistance to tyranny
        
        For each foundation, provide a score (0-10) and brief evidence.
        Format: FOUNDATION: score (evidence)
        """;

        _logger.LogInformation("Analyzing moral foundations");
        
        var result = await kernel.InvokePromptAsync(prompt);
        return ParseMoralFoundations(result.ToString());
    }

    #region Private Helpers

    private string BuildAnalysisPrompt(UserInteraction interaction, UserProfile profile)
    {
        // SIMPLIFIED for smaller models
        return $$$"""
        Question: {{{interaction.Content.Question}}}
        Response: {{{interaction.Response.RawText}}}
        
        Briefly describe what values and beliefs this response reveals.
        """;
    }

    private ResponseAnalysis ParseAnalysisResponse(string analysisText)
    {
        // Simplified parsing - in production, use structured JSON output
        var analysis = new ResponseAnalysis
        {
            NarrativeAnalysis = analysisText,
            AnalysisConfidence = ExtractConfidence(analysisText),
            DimensionUpdates = ExtractDimensionUpdates(analysisText),
            ImpliedValues = ExtractImpliedValues(analysisText),
            ReasoningPatterns = ExtractReasoningPatterns(analysisText),
            MoralFoundationScores = ExtractMoralFoundationScores(analysisText),
            SuggestedFollowUps = ExtractFollowUps(analysisText)
        };

        return analysis;
    }

    private double ExtractConfidence(string text)
    {
        // Look for confidence mentions - simplified
        if (text.Contains("very confident", StringComparison.OrdinalIgnoreCase))
            return 0.9;
        if (text.Contains("confident", StringComparison.OrdinalIgnoreCase))
            return 0.75;
        if (text.Contains("somewhat confident", StringComparison.OrdinalIgnoreCase))
            return 0.6;
        if (text.Contains("uncertain", StringComparison.OrdinalIgnoreCase))
            return 0.4;
        
        return 0.7; // Default
    }

    private List<DimensionUpdate> ExtractDimensionUpdates(string text)
    {
        // Simplified extraction - in production, use regex or structured parsing
        var updates = new List<DimensionUpdate>();
        
        // This is a placeholder - you'd implement proper parsing here
        // For now, return empty list and let the Bayesian engine handle it
        
        return updates;
    }

    private List<string> ExtractImpliedValues(string text)
    {
        var values = new List<string>();
        var valueKeywords = new[] { 
            "freedom", "equality", "justice", "compassion", "loyalty", 
            "tradition", "progress", "security", "authenticity", "community" 
        };

        foreach (var keyword in valueKeywords)
        {
            if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                values.Add(keyword);
        }

        return values;
    }

    private List<string> ExtractReasoningPatterns(string text)
    {
        var patterns = new List<string>();
        
        if (text.Contains("consequence", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("outcome", StringComparison.OrdinalIgnoreCase))
            patterns.Add("Consequentialist");
        
        if (text.Contains("duty", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("principle", StringComparison.OrdinalIgnoreCase))
            patterns.Add("Deontological");
        
        if (text.Contains("virtue", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("character", StringComparison.OrdinalIgnoreCase))
            patterns.Add("Virtue Ethics");
        
        if (text.Contains("feel", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("emotion", StringComparison.OrdinalIgnoreCase))
            patterns.Add("Emotional/Intuitive");

        return patterns;
    }

    private Dictionary<string, double> ExtractMoralFoundationScores(string text)
    {
        var scores = new Dictionary<string, double>();
        var foundations = new[] { "Care", "Fairness", "Loyalty", "Authority", "Sanctity", "Liberty" };

        foreach (var foundation in foundations)
        {
            // Simplified scoring based on mentions
            var score = text.Contains(foundation, StringComparison.OrdinalIgnoreCase) ? 6.0 : 3.0;
            scores[foundation] = score;
        }

        return scores;
    }

    private List<string> ExtractFollowUps(string text)
    {
        // Simplified - look for suggestions section
        var followUps = new List<string>();
        
        if (text.Contains("explore", StringComparison.OrdinalIgnoreCase))
            followUps.Add("Explore mentioned themes in more depth");
        
        if (text.Contains("clarify", StringComparison.OrdinalIgnoreCase))
            followUps.Add("Clarify ambiguous statements");
        
        if (text.Contains("test", StringComparison.OrdinalIgnoreCase))
            followUps.Add("Test with edge cases");

        return followUps;
    }

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
        // Simplified extraction - look for patterns like "intensity: 0.7"
        var pattern = $"{label}";
        var index = text.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            var substring = text.Substring(index + pattern.Length).Trim();
            var match = System.Text.RegularExpressions.Regex.Match(substring, @"[\d.]+");
            if (match.Success && double.TryParse(match.Value, out var value))
            {
                return value > 1 ? value / 10.0 : value; // Normalize to 0-1
            }
        }
        return 0.5; // Default
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
        // Look for pattern like "CARE: 7.5"
        var index = text.IndexOf(foundation, StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            var substring = text.Substring(index + foundation.Length).Trim();
            var match = System.Text.RegularExpressions.Regex.Match(substring, @"[\d.]+");
            if (match.Success && double.TryParse(match.Value, out var value))
            {
                return value;
            }
        }
        return 5.0; // Default neutral score
    }

    #endregion
}
