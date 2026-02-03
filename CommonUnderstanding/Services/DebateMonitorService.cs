using CommonUnderstanding.Models;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel;

namespace CommonUnderstanding.Services;

public class DebateMonitorService
{
    private readonly SemanticKernelService _kernelService;
    private readonly ILogger<DebateMonitorService> _logger;
    private readonly ConcurrentDictionary<string, DebateSession> _sessions = new();
    private readonly ConcurrentDictionary<string, List<DebateMessage>> _conversationHistory = new();

    public DebateMonitorService(
        SemanticKernelService kernelService,
        ILogger<DebateMonitorService> logger)
    {
        _kernelService = kernelService;
        _logger = logger;
    }

    public DebateSession CreateSession(string title)
    {
        var session = new DebateSession { Title = title };
        _sessions.TryAdd(session.Id, session);
        _conversationHistory.TryAdd(session.Id, new List<DebateMessage>());
        _logger.LogInformation("Created debate session {SessionId}: {Title}", session.Id, title);
        return session;
    }

    public DebateSession? GetSession(string sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return session;
    }

    public async Task<DebateMessage> AnalyzeMessageAsync(string sessionId, string userId, string userName, string content)
    {
        var message = new DebateMessage
        {
            SessionId = sessionId,
            UserId = userId,
            UserName = userName,
            Content = content
        };

        // Get conversation history for context
        var history = _conversationHistory.GetOrAdd(sessionId, _ => new List<DebateMessage>());
        
        // Simple, fast analysis instead of complex parallel processing
        try
        {
            var analysis = await PerformQuickAnalysisAsync(content, history);
            
            // Parse the simple analysis into our structure
            message.FactChecks = analysis.FactChecks ?? new List<FactCheck>();
            message.IntentAnalysis = analysis.Intent ?? new IntentAnalysis { PrimaryIntent = "STATEMENT", EmotionalTone = "NEUTRAL", Confidence = 0.5 };
            message.MisunderstandingAlerts = analysis.Alerts ?? new List<MisunderstandingAlert>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing message");
            // Return message with empty analysis on error
            message.IntentAnalysis = new IntentAnalysis { PrimaryIntent = "UNKNOWN", EmotionalTone = "NEUTRAL", Confidence = 0.0 };
        }

        // Add to history
        history.Add(message);
        
        // Update session
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.Messages.Add(message);
            UpdateSessionAnalytics(session);
        }

        return message;
    }

    private async Task<SimpleAnalysis> PerformQuickAnalysisAsync(string content, List<DebateMessage> history)
    {
        var context = history.Count > 0 
            ? string.Join("\n", history.TakeLast(3).Select(m => $"{m.UserName}: {m.Content}"))
            : "No prior context.";

        var prompt = $@"Analyze this debate message quickly and concisely.

Context:
{context}

New message: ""{content}""

Provide a brief analysis covering:
1. Any factual claims that need checking (be selective - only obvious claims)
2. The speaker's primary intent and tone
3. Any potential misunderstandings or ambiguities

Keep it brief and actionable. Focus on what matters most.";

        var kernel = _kernelService.GetKernel();
        var result = await kernel.InvokePromptAsync(prompt);
        var response = result.ToString();

        return ParseSimpleAnalysis(response, content);
    }

    private SimpleAnalysis ParseSimpleAnalysis(string aiResponse, string originalContent)
    {
        var analysis = new SimpleAnalysis
        {
            FactChecks = new List<FactCheck>(),
            Intent = new IntentAnalysis 
            { 
                PrimaryIntent = "STATEMENT", 
                EmotionalTone = "NEUTRAL", 
                Confidence = 0.7,
                IsStatementAsserting = true
            },
            Alerts = new List<MisunderstandingAlert>()
        };

        // Simple keyword-based parsing (fast and reliable)
        var lowerResponse = aiResponse.ToLower();

        // Detect fact claims
        if (lowerResponse.Contains("claim") || lowerResponse.Contains("fact") || lowerResponse.Contains("statistic"))
        {
            analysis.FactChecks.Add(new FactCheck
            {
                Claim = ExtractFirstSentence(originalContent),
                Verdict = lowerResponse.Contains("true") ? "TRUE" : 
                         lowerResponse.Contains("false") ? "FALSE" :
                         lowerResponse.Contains("unverified") ? "UNVERIFIABLE" : "PARTIALLY_TRUE",
                Confidence = 0.6,
                Evidence = ExtractEvidence(aiResponse),
                Context = "AI-assisted fact check"
            });
        }

        // Detect intent
        if (lowerResponse.Contains("question"))
        {
            analysis.Intent.PrimaryIntent = "SEEKING_INFORMATION";
            analysis.Intent.IsQuestionSeeking = true;
            analysis.Intent.IsStatementAsserting = false;
        }
        else if (lowerResponse.Contains("persuad") || lowerResponse.Contains("convinc"))
        {
            analysis.Intent.PrimaryIntent = "PERSUADING";
            analysis.Intent.IsPersuasionAttempt = true;
        }
        else if (lowerResponse.Contains("explain") || lowerResponse.Contains("clarif"))
        {
            analysis.Intent.PrimaryIntent = "EXPLAINING";
        }

        // Detect tone
        if (lowerResponse.Contains("negative") || lowerResponse.Contains("defensive") || lowerResponse.Contains("hostile"))
        {
            analysis.Intent.EmotionalTone = lowerResponse.Contains("defensive") ? "DEFENSIVE" : "NEGATIVE";
        }
        else if (lowerResponse.Contains("positive") || lowerResponse.Contains("collaborative") || lowerResponse.Contains("cooperative"))
        {
            analysis.Intent.EmotionalTone = "COLLABORATIVE";
        }

        // Detect misunderstandings
        if (lowerResponse.Contains("ambig") || lowerResponse.Contains("unclear") || lowerResponse.Contains("vague"))
        {
            analysis.Alerts.Add(new MisunderstandingAlert
            {
                Type = "AMBIGUITY",
                Description = ExtractAlertDescription(aiResponse, "ambig", "unclear", "vague"),
                Suggestion = "Consider clarifying this point",
                Severity = 0.5
            });
        }

        if (lowerResponse.Contains("contradict") || lowerResponse.Contains("inconsist"))
        {
            analysis.Alerts.Add(new MisunderstandingAlert
            {
                Type = "CONTRADICTION",
                Description = ExtractAlertDescription(aiResponse, "contradict", "inconsist"),
                Suggestion = "Review previous statements for consistency",
                Severity = 0.7
            });
        }

        if (lowerResponse.Contains("assum") || lowerResponse.Contains("presum"))
        {
            analysis.Alerts.Add(new MisunderstandingAlert
            {
                Type = "ASSUMPTION_GAP",
                Description = ExtractAlertDescription(aiResponse, "assum", "presum"),
                Suggestion = "Make underlying assumptions explicit",
                Severity = 0.6
            });
        }

        return analysis;
    }

    private string ExtractFirstSentence(string text)
    {
        var match = Regex.Match(text, @"^[^.!?]*[.!?]");
        return match.Success ? match.Value.Trim() : text.Substring(0, Math.Min(100, text.Length));
    }

    private string ExtractEvidence(string aiResponse)
    {
        var sentences = aiResponse.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
        return sentences.FirstOrDefault(s => s.Length > 20 && s.Length < 200)?.Trim() ?? "See AI analysis";
    }

    private string ExtractAlertDescription(string aiResponse, params string[] keywords)
    {
        var sentences = aiResponse.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var sentence in sentences)
        {
            if (keywords.Any(k => sentence.ToLower().Contains(k)))
            {
                return sentence.Trim();
            }
        }
        return "Potential issue detected in message";
    }

    private class SimpleAnalysis
    {
        public List<FactCheck>? FactChecks { get; set; }
        public IntentAnalysis? Intent { get; set; }
        public List<MisunderstandingAlert>? Alerts { get; set; }
    }

    private string GetRecentContext(List<DebateMessage> history, int count)
    {
        var recent = history.TakeLast(count).ToList();
        return string.Join("\n", recent.Select(m => $"{m.UserName}: {m.Content}"));
    }

    private void UpdateSessionAnalytics(DebateSession session)
    {
        var analytics = session.Analytics;
        analytics.TotalMessages = session.Messages.Count;
        analytics.TotalFactChecks = session.Messages.SelectMany(m => m.FactChecks).Count();
        analytics.TotalMisunderstandings = session.Messages.SelectMany(m => m.MisunderstandingAlerts).Count();
        
        analytics.VerdictCounts = session.Messages
            .SelectMany(m => m.FactChecks)
            .GroupBy(fc => fc.Verdict)
            .ToDictionary(g => g.Key, g => g.Count());
            
        analytics.IntentCounts = session.Messages
            .Where(m => m.IntentAnalysis != null)
            .GroupBy(m => m.IntentAnalysis!.PrimaryIntent)
            .ToDictionary(g => g.Key, g => g.Count());
            
        analytics.MisunderstandingTypeCounts = session.Messages
            .SelectMany(m => m.MisunderstandingAlerts)
            .GroupBy(a => a.Type)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    public List<DebateSession> GetActiveSessions()
    {
        return _sessions.Values.Where(s => s.IsActive).ToList();
    }

    public void EndSession(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.IsActive = false;
            session.EndedAt = DateTime.UtcNow;
        }
    }
}
