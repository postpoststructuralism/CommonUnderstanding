# Live Debate Monitor - Quick Start Guide

## 🚀 Get Started in 3 Minutes

### Step 1: Start the Application
```bash
cd CommonUnderstanding
dotnet run
```

### Step 2: Navigate to Live Debate
1. Open your browser to `https://localhost:5001` (or the port shown in the terminal)
2. Click **"Live Debate"** in the navigation menu

### Step 3: Create Your First Session
1. Click the **"New Session"** button
2. Enter a session title (e.g., "Testing the AI Debate Monitor")
3. Enter your name
4. Click **"Create & Join"**

### Step 4: Start Debating!
Type a message and watch the AI analyze it in real-time:

**Try These Examples:**

**Example 1: Fact Checking**
```
Climate change is causing sea levels to rise at approximately 3.3 millimeters per year.
```
→ Watch the AI fact-check this claim and provide evidence

**Example 2: Intent Analysis**
```
I think we should focus on renewable energy, but I'm curious what you think about nuclear power?
```
→ See the AI identify this as question-seeking with collaborative tone

**Example 3: Misunderstanding Detection**
```
Person 1: "We need to reduce emissions."
Person 2: "That would hurt the economy."
```
→ AI detects assumption gaps about the relationship between emissions and economy

## 📊 Understanding the Interface

### Left Panel - Messages
- Your messages appear instantly
- Each message gets AI analysis within 2-5 seconds
- Scroll to see conversation history

### Right Panel - Real-Time Analysis

**🚨 Active Alerts (Top)**
- Red badges = High severity misunderstandings
- Yellow badges = Medium severity
- Blue badges = Low severity
- Click the X to dismiss

**✅ Fact Checks (Middle)**
- Green = TRUE
- Red = FALSE
- Yellow = PARTIALLY_TRUE
- Gray = UNVERIFIABLE
- Shows confidence percentage

**🎯 Intent Insights (Bottom)**
- Shows what the speaker is trying to do
- Emotional tone indicators
- Communication style flags

## 💡 Pro Tips

1. **Keep it Natural**: Write naturally - the AI works best with normal conversation
2. **Watch Alerts**: Pay attention to high-severity alerts (red badges)
3. **Verify Facts**: AI fact-checks are helpful but not perfect - verify important claims
4. **Use Suggestions**: When alerts appear, they include suggestions for clarity
5. **Multiple Participants**: Share the session URL to invite others

## 🎯 Common Use Cases

### Productive Debates
Use when discussing controversial topics to:
- Catch misunderstandings early
- Verify factual claims
- Understand others' intentions
- Keep conversations constructive

### Team Discussions
Great for remote teams to:
- Ensure everyone is on the same page
- Identify communication gaps
- Track sentiment and tone
- Document key points

### Educational Settings
Perfect for:
- Teaching critical thinking
- Practicing debate skills
- Learning to identify bias
- Understanding argumentation

## ⚙️ System Requirements

- **AI Provider**: Ollama running locally OR OpenRouter/other API configured
- **Browser**: Modern browser (Chrome, Firefox, Edge, Safari)
- **Connection**: Stable internet/network connection for real-time updates

## 🔧 Troubleshooting

**"No analysis appearing"**
- Check AI provider is running (Ollama: `ollama serve`)
- Verify model is downloaded (`ollama pull llama3.2`)
- Check AI Control Panel in top-right shows "Active"

**"Connection lost"**
- Refresh the page
- Check network connection
- Look for the status badge (should be green "Connected")

**"Slow responses"**
- Complex messages take longer to analyze
- Check your AI model speed
- Consider using a faster model for real-time use

## 🎉 Next Steps

Once you're comfortable with the basics:
1. Try creating multiple sessions for different topics
2. Experiment with different types of statements and questions
3. Invite team members to join a session
4. Review the session analytics
5. Export findings for documentation

## 📖 Full Documentation

For complete details, see [LIVE_DEBATE_MONITOR.md](LIVE_DEBATE_MONITOR.md)

---

**Need Help?** Check the full documentation or review the session analytics to understand patterns in your debates.
