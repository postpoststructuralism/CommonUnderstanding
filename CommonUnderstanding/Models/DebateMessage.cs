namespace CommonUnderstanding.Models;

public class DebateMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SessionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public List<FactCheck> FactChecks { get; set; } = new();
    public IntentAnalysis? IntentAnalysis { get; set; }
    public List<MisunderstandingAlert> MisunderstandingAlerts { get; set; } = new();
}

public class FactCheck
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Claim { get; set; } = string.Empty;
    public string Verdict { get; set; } = string.Empty; // "TRUE", "FALSE", "PARTIALLY_TRUE", "UNVERIFIABLE"
    public double Confidence { get; set; }
    public string Evidence { get; set; } = string.Empty;
    public List<string> Sources { get; set; } = new();
    public string Context { get; set; } = string.Empty;
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}

public class IntentAnalysis
{
    public string PrimaryIntent { get; set; } = string.Empty;
    public List<string> SecondaryIntents { get; set; } = new();
    public double Confidence { get; set; }
    public string EmotionalTone { get; set; } = string.Empty; // "NEUTRAL", "POSITIVE", "NEGATIVE", "DEFENSIVE", "COLLABORATIVE"
    public bool IsQuestionSeeking { get; set; }
    public bool IsStatementAsserting { get; set; }
    public bool IsPersuasionAttempt { get; set; }
    public Dictionary<string, double> IntentScores { get; set; } = new();
}

public class MisunderstandingAlert
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Type { get; set; } = string.Empty; // "AMBIGUITY", "CONTRADICTION", "ASSUMPTION_GAP", "DEFINITION_MISMATCH"
    public string Description { get; set; } = string.Empty;
    public string Suggestion { get; set; } = string.Empty;
    public double Severity { get; set; } // 0-1 scale
    public List<string> RelatedMessageIds { get; set; } = new();
    public string ContextualExplanation { get; set; } = string.Empty;
}

public class DebateSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public List<string> ParticipantIds { get; set; } = new();
    public List<DebateMessage> Messages { get; set; } = new();
    public DebateAnalyticsSummary Analytics { get; set; } = new();
    public bool IsActive { get; set; } = true;
}

public class DebateAnalyticsSummary
{
    public int TotalMessages { get; set; }
    public int TotalFactChecks { get; set; }
    public int TotalMisunderstandings { get; set; }
    public Dictionary<string, int> VerdictCounts { get; set; } = new();
    public Dictionary<string, int> IntentCounts { get; set; } = new();
    public Dictionary<string, int> MisunderstandingTypeCounts { get; set; } = new();
    public double AverageResponseTime { get; set; }
    public List<string> KeyTopics { get; set; } = new();
    public List<string> UnresolvedMisunderstandings { get; set; } = new();
}
