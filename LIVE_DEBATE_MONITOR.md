# Live Debate Monitor - AI-Powered Real-Time Analysis

## Overview

The Live Debate Monitor is an AI agent that provides real-time analysis of debates and conversations, helping participants communicate more effectively by identifying potential misunderstandings, fact-checking claims, and analyzing intent.

## Features

### 🎯 **Real-Time AI Analysis**
- **Instant Processing**: Messages are analyzed in real-time as they're sent
- **Parallel Analysis**: Fact checking, intent analysis, and misunderstanding detection run simultaneously for fast results
- **Live Updates**: All participants see analysis results instantly via SignalR

### ✅ **Automated Fact Checking**
- **Claim Extraction**: AI automatically identifies factual claims in messages
- **Verdict Classification**: Claims are rated as TRUE, FALSE, PARTIALLY_TRUE, or UNVERIFIABLE
- **Evidence & Context**: Each fact check includes supporting evidence and contextual information
- **Confidence Scores**: Transparency through 0-100% confidence ratings

### 🔍 **Intent Discovery**
- **Primary Intent Detection**: What is the speaker trying to accomplish?
- **Secondary Intents**: Multiple layers of communication intent
- **Emotional Tone Analysis**: NEUTRAL, POSITIVE, NEGATIVE, DEFENSIVE, COLLABORATIVE
- **Communication Style Indicators**:
  - Question-seeking
  - Statement-asserting
  - Persuasion attempts

### ⚠️ **Misunderstanding Detection**
- **Four Alert Types**:
  - **AMBIGUITY**: Unclear or vague statements
  - **CONTRADICTION**: Conflicts with previous statements
  - **ASSUMPTION_GAP**: Unstated assumptions that might differ
  - **DEFINITION_MISMATCH**: Same words, different meanings
- **Severity Levels**: 0-1 scale for prioritizing alerts
- **Resolution Suggestions**: AI provides actionable suggestions to clarify

### 📊 **Session Analytics**
- Real-time statistics dashboard
- Message count tracking
- Fact check summaries
- Misunderstanding trends
- Intent distribution analysis

## How to Use

### Starting a Debate Session

1. **Navigate to Live Debate**: Click "Live Debate" in the navigation menu
2. **Create New Session**: Click the "New Session" button
3. **Enter Details**:
   - Session Title (e.g., "Climate Policy Discussion")
   - Your Name
4. **Click "Create & Join"**

### Participating in a Debate

1. **Type your message** in the input box at the bottom
2. **Press Enter or click Send**
3. **Watch the AI analysis** appear in real-time:
   - Your message appears immediately
   - Analysis indicators show processing
   - Fact checks appear in the right sidebar
   - Intent insights update
   - Misunderstanding alerts pop up if detected

### Understanding the Interface

**Main Chat Area (Left)**
- Messages from all participants
- Chronological conversation flow
- Real-time updates

**Active Alerts Panel (Top Right)**
- High-priority misunderstanding alerts
- Color-coded by severity (red = high, yellow = medium)
- Dismissible alerts
- Includes suggestions for resolution

**Recent Fact Checks (Middle Right)**
- Last 5 fact checks
- Color-coded verdicts (green = true, red = false, yellow = partial)
- Confidence percentages
- Brief evidence

**Intent Insights (Bottom Right)**
- Current speaker's intent
- Emotional tone indicator
- Communication style flags

### Best Practices

✅ **Do:**
- Review fact checks to verify your claims
- Pay attention to high-severity misunderstanding alerts
- Consider the AI's suggestions for clarity
- Use intent insights to understand others' perspectives
- Keep sessions focused on one topic

❌ **Don't:**
- Ignore repeated misunderstanding alerts
- Assume fact checks are 100% accurate (they're AI-assisted, not perfect)
- Use the system to "win" debates - use it to achieve understanding
- Overwhelm with rapid-fire messages (AI needs time to analyze)

## Technical Architecture

### Backend Components

1. **DebateMonitorService** (`Services/DebateMonitorService.cs`)
   - Core AI analysis engine
   - Manages debate sessions
   - Coordinates fact checking, intent analysis, and misunderstanding detection
   - Uses RuntimeAiConfigService for AI completions

2. **DebateHub** (`Hubs/DebateHub.cs`)
   - SignalR hub for real-time communication
   - Broadcasts messages and analysis to all participants
   - Manages user connections and sessions

3. **DebateController** (`Controllers/DebateController.cs`)
   - REST API for session management
   - CRUD operations for debate sessions
   - Analytics retrieval

4. **Models** (`Models/DebateMessage.cs`)
   - `DebateMessage`: Individual messages with analysis
   - `FactCheck`: Fact checking results
   - `IntentAnalysis`: Intent and tone analysis
   - `MisunderstandingAlert`: Potential confusion detection
   - `DebateSession`: Session container with analytics

### Frontend Components

**View**: `Views/Debate/Monitor.cshtml`
- Real-time UI with SignalR integration
- Responsive three-panel layout
- Live statistics and analytics
- Interactive alert management

**JavaScript**:
- SignalR client for WebSocket communication
- Event-driven UI updates
- Automatic reconnection handling
- Local storage for user preferences

## API Endpoints

### Create Session
```http
POST /api/debate/sessions
Content-Type: application/json

{
  "title": "Session Title"
}
```

### Get Active Sessions
```http
GET /api/debate/sessions
```

### Get Session Details
```http
GET /api/debate/sessions/{sessionId}
```

### End Session
```http
POST /api/debate/sessions/{sessionId}/end
```

### Get Session Analytics
```http
GET /api/debate/sessions/{sessionId}/analytics
```

## SignalR Events

### Client to Server
- `JoinSession(sessionId, userName)`: Join a debate session
- `LeaveSession(sessionId)`: Leave a session
- `SendMessage(sessionId, userName, content)`: Send a message
- `RequestSessionSummary(sessionId)`: Get session analytics

### Server to Client
- `ReceiveMessage(data)`: New message from participant
- `ReceiveAnalysis(message)`: Complete analysis results
- `ReceiveFactChecks(data)`: Fact check results
- `ReceiveMisunderstandingAlert(data)`: Misunderstanding alerts
- `ReceiveIntentAnalysis(data)`: Intent analysis
- `UserJoined(data)`: Participant joined
- `UserLeft(data)`: Participant left
- `Error(data)`: Error occurred

## Configuration

The system uses your existing AI configuration from `RuntimeAiConfigService`. Make sure you have:

1. **AI Provider Configured**: Ollama, OpenRouter, or other LLM provider
2. **Model Selected**: A capable model for analysis (GPT-4, Claude, Llama 3, etc.)
3. **API Keys Set**: If using external providers

## Privacy & Data

- **In-Memory Storage**: Sessions are stored in memory (not persisted)
- **Session Lifecycle**: Data is cleared when the application restarts
- **No External Logging**: Conversations are not logged externally
- **Real-Time Only**: Historical data is only available during active sessions

## Limitations

- **AI Accuracy**: Fact checks are AI-generated and may not be 100% accurate
- **Context Window**: Analysis considers last 3-5 messages for context
- **Processing Time**: Complex messages may take 2-5 seconds to analyze
- **Concurrent Sessions**: Multiple sessions supported but share AI resources
- **Language Support**: Optimized for English (depends on your AI model)

## Future Enhancements

Potential improvements:
- Persistent storage for session history
- Multi-language support
- Voice input/output
- Sentiment trend analysis
- Topic clustering and deviation detection
- Citation lookup and verification
- Export session transcripts with analysis
- Moderation tools for larger groups
- Integration with external fact-checking APIs

## Troubleshooting

**Messages not appearing?**
- Check the connection status badge (should be green "Connected")
- Refresh the page if status shows "Disconnected"

**Slow analysis?**
- Large conversations increase context processing time
- Check your AI provider's response time
- Consider using a faster model for real-time use

**Fact checks seem inaccurate?**
- AI fact-checking is probabilistic, not deterministic
- Use confidence scores as a guide
- Verify important claims manually
- Consider the context provided

**Alerts not helpful?**
- The AI learns from conversation patterns
- More context (longer conversations) = better analysis
- Dismiss low-severity alerts that aren't relevant

## Getting Started Checklist

- [ ] Navigate to "Live Debate" in the menu
- [ ] Create a new session with a descriptive title
- [ ] Enter your name
- [ ] Send a test message
- [ ] Observe the real-time analysis
- [ ] Invite others to join the same session
- [ ] Review fact checks and alerts
- [ ] Use suggestions to clarify misunderstandings
- [ ] Check session analytics

---

**Note**: This system is designed to enhance understanding, not replace human judgment. Always verify critical information and use the AI analysis as a tool for better communication, not as absolute truth.
