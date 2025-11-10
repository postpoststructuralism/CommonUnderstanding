# Quick Start Guide

## Get Up and Running in 5 Minutes

### Step 1: Install Ollama

1. Download Ollama from [https://ollama.ai/](https://ollama.ai/)
2. Install it on your system
3. Open a terminal/PowerShell and run:
   ```bash
   ollama pull llama3.2
   ```

### Step 2: Start Ollama

Keep Ollama running in a terminal:
```bash
ollama serve
```

You should see output indicating it's listening on `http://localhost:11434`

### Step 3: Run the Application

Open a **new** terminal/PowerShell window, navigate to the project directory, and run:
```bash
cd c:\Code\CommonUnderstanding\CommonUnderstanding
dotnet run
```

### Step 4: Open Your Browser

Navigate to:
```
https://localhost:5001
```

Or whatever URL is shown in the terminal (usually `http://localhost:5000` or `https://localhost:5001`)

## First Steps in the Application

### 1. Add Your First Belief System

Click "Add New Belief System" and try something like:

**Name:** Buddhism  
**Description:**
```
Buddhism teaches the Four Noble Truths: suffering exists, suffering has a cause, 
suffering can end, and there is a path to end suffering (the Eightfold Path). 
Core values include compassion, mindfulness, non-attachment, and the middle way. 
Buddhism emphasizes personal spiritual development and the attainment of insight 
into the true nature of reality. Key principles include karma, rebirth, and nirvana.
```

### 2. Add a Second Belief System

**Name:** Stoicism  
**Description:**
```
Stoicism teaches that virtue is the highest good and that we should focus only on 
what we can control while accepting what we cannot. Core values include wisdom, 
courage, justice, and temperance. Stoics believe in living according to nature 
and reason, practicing negative visualization, and maintaining equanimity in the 
face of external events. Key principles include the dichotomy of control, amor fati 
(love of fate), and the importance of ethical living.
```

### 3. Compare Them

1. Click "Compare Belief Systems"
2. Select "Buddhism" and "Stoicism"
3. Click "Analyze & Compare"
4. Wait a few seconds for the AI to generate the analysis
5. Explore the results!

### 4. Get Dialogue Suggestions

On the comparison results page, click "Generate Dialogue Suggestions" to get practical tips for constructive conversations.

## Troubleshooting

### "Error analyzing belief system. Make sure Ollama is running."

**Solution:** 
- Make sure Ollama is running (`ollama serve` in a terminal)
- Verify it's accessible at `http://localhost:11434`
- Try: `curl http://localhost:11434` (should return "Ollama is running")

### "Connection refused" or similar errors

**Solution:**
- Check `appsettings.json` to ensure the endpoint matches your Ollama installation
- Default is `http://localhost:11434`
- Make sure no firewall is blocking the connection

### Application is slow

**Solutions:**
- Try a smaller/faster model: `ollama pull phi3`
- Update `appsettings.json` to use `"ModelName": "phi3"`
- Ensure Ollama has adequate system resources (RAM/CPU)

### Model not found

**Solution:**
- Check which models you have: `ollama list`
- Pull the model specified in `appsettings.json`: `ollama pull llama3.2`
- Or change the model in `appsettings.json` to one you have

## Tips for Best Results

1. **Be Detailed**: The more comprehensive your belief system descriptions, the better the analysis
2. **Include Multiple Aspects**: Cover ethics, metaphysics, epistemology, values, and practices
3. **Stay Objective**: Describe belief systems accurately, even ones you disagree with
4. **Compare Thoughtfully**: Choose belief systems with interesting similarities and differences
5. **Be Patient**: AI analysis can take 10-30 seconds depending on your model and hardware

## Example Belief Systems to Try

**Easy Comparisons** (lots of overlap):
- Buddhism vs. Stoicism
- Humanism vs. Enlightenment Rationalism
- Taoism vs. Zen Buddhism

**Challenging Comparisons** (interesting differences):
- Scientific Materialism vs. Religious Mysticism
- Utilitarianism vs. Virtue Ethics
- Individualism vs. Collectivism

**Surprising Comparisons** (unexpected common ground):
- Christianity vs. Buddhism
- Stoicism vs. Cognitive Behavioral Therapy
- Marxism vs. Catholic Social Teaching

## Next Steps

- Explore different belief systems and worldviews
- Experiment with various Ollama models
- Consider adding database persistence (see main README)
- Share your findings with others to build understanding

---

**Remember:** The goal is understanding, not agreement. Focus on finding common humanity while respecting honest differences.
