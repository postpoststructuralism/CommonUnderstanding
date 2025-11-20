# Quick Start Guide

## Get Up and Running in 10 Minutes

This guide will walk you through downloading Ollama, selecting the optimal LLM for your system, launching the application, and exploring the interactive belief discovery map.

> **📋 Quick Reference**: Use [OLLAMA_CHECKLIST.md](../OLLAMA_CHECKLIST.md) to verify your setup
> 
> **📘 Detailed Guide**: See [OLLAMA_SETUP.md](../OLLAMA_SETUP.md) for comprehensive installation and troubleshooting

---

## Step 1: Install Ollama

> **📘 Need detailed installation help?** See the comprehensive [OLLAMA_SETUP.md](../OLLAMA_SETUP.md) guide.

### Quick Install

**Windows:**
1. Visit [https://ollama.com/download/windows](https://ollama.com/download/windows)
2. Download and run the installer
3. Ollama will run automatically in the background

**macOS:**
1. Visit [https://ollama.com/download/mac](https://ollama.com/download/mac)
2. Download the DMG and drag to Applications
3. Launch Ollama from Applications

**Linux:**
```bash
curl -fsSL https://ollama.com/install.sh | sh
```

### Verify Installation

Open a terminal (PowerShell on Windows, Terminal on macOS/Linux) and verify Ollama is installed:

```bash
ollama --version
```

You should see version information displayed.

**Important**: Ollama runs **locally** on your machine at `http://localhost:11434`. The application connects to this local instance, not a remote server.

---

## Step 2: Choose and Download Your LLM

The quality of your belief discovery experience depends on the AI model you choose. Here are the recommended models based on your system:

### Recommended Models by System

| Model | RAM Required | Speed | Quality | Best For |
|-------|-------------|-------|---------|----------|
| **llama3.2:1b** ⚡ | 2-4 GB | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | **Laptops, Quick Testing** |
| **llama3.2:3b** ✨ | 4-6 GB | ⭐⭐⭐⭐ | ⭐⭐⭐⭐ | **Recommended Default** |
| **llama3.1:8b** 🚀 | 8-12 GB | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ | **Best Quality, Gaming PCs** |
| **qwen2.5:7b** 🎯 | 8-10 GB | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | **Excellent Reasoning** |
| **phi3:3.8b** 💡 | 4-6 GB | ⭐⭐⭐⭐ | ⭐⭐⭐⭐ | **Fast Alternative** |

### Download Your Chosen Model

In your terminal, run one of the following commands:

**For most users (recommended):**
```bash
ollama pull llama3.2:3b
```

**For lower-end systems (2-4GB RAM available):**
```bash
ollama pull llama3.2:1b
```

**For high-performance systems (8GB+ RAM available):**
```bash
ollama pull llama3.1:8b
```

**For best reasoning quality:**
```bash
ollama pull qwen2.5:7b
```

The download may take 5-15 minutes depending on your internet speed. Model sizes range from 1GB to 8GB.

### Verify Model Download

Check your installed models:
```bash
ollama list
```

You should see your downloaded model listed.

---

## Step 3: Ensure Ollama is Running

**Windows/macOS**: Ollama should already be running in the background after installation. Check your system tray (Windows) or menu bar (macOS) for the Ollama icon.

**Linux**: If you installed via the script, Ollama runs as a service:
```bash
sudo systemctl status ollama
# If not running:
sudo systemctl start ollama
```

**Manual startup** (if needed):
```bash
ollama serve
```

**Expected output:**
```
Listening on 127.0.0.1:11434 (version 0.x.x)
```

**Verify Ollama is accessible**:
```bash
# Linux/macOS:
curl http://localhost:11434

# Windows (PowerShell):
Invoke-WebRequest -Uri http://localhost:11434
```

Should return: `Ollama is running`

---

## Step 4: Configure the Application

Before running the app, update the configuration to use your chosen model.

1. **Open the project folder** in your file explorer:
   - Navigate to: `c:\Code\CommonUnderstanding\CommonUnderstanding`

2. **Edit `appsettings.json`**:
   - Open the file in any text editor (Notepad, VS Code, etc.)
   - Find the `"Ollama"` section
   - Update the `"ModelName"` to match your downloaded model:

```json
{
  "Ollama": {
    "Endpoint": "http://localhost:11434",
    "ModelName": "llama3.2:3b"
  }
}
```

Replace `"llama3.2:3b"` with your model:
- `"llama3.2:1b"` for the 1B model
- `"llama3.2:3b"` for the 3B model (recommended)
- `"llama3.1:8b"` for the 8B model
- `"qwen2.5:7b"` for Qwen model
- `"phi3:3.8b"` for Phi-3 model

3. **Save the file**

---

## Step 5: Run the Application

1. **Open a NEW terminal/PowerShell window** (keep the Ollama terminal open!)

2. **Navigate to the project directory**:
   ```powershell
   cd c:\Code\CommonUnderstanding\CommonUnderstanding
   ```

3. **Run the application**:
   ```powershell
   dotnet run
   ```

4. **Wait for the startup message**. You should see:
   ```
   info: Microsoft.Hosting.Lifetime[14]
         Now listening on: http://localhost:5220
   info: Microsoft.Hosting.Lifetime[14]
         Now listening on: https://localhost:7187
   ```

---

## Step 6: Open Your Browser

1. **Open your web browser** (Chrome, Firefox, Edge, Safari, etc.)

2. **Navigate to**:
   ```
   https://localhost:7187
   ```
   
   Or use HTTP:
   ```
   http://localhost:5220
   ```

3. **Accept the security warning** (for local development, the HTTPS certificate is self-signed)
   - **Chrome/Edge**: Click "Advanced" → "Proceed to localhost"
   - **Firefox**: Click "Advanced" → "Accept the Risk and Continue"

You should now see the Common Understanding home page!

---

## Exploring the Belief Discovery Map

Now that you're up and running, let's explore the core features!

### Option 1: Discover YOUR Belief System (Recommended) 🧠

This is the primary feature - an AI-powered conversation that infers your values and beliefs.

**Navigate to Discovery:**
1. Click **"Begin Discovery"** on the home page
2. Or go to: `https://localhost:7187/Discovery/Start`

**Start Your Journey:**
1. **Enter your name** (just a first name or nickname is fine)
2. Click **"Start My Discovery Journey"**

**Engage with the AI:**
The system will ask you thoughtful questions to understand your worldview:

- **Open-ended questions**: "What principles guide your decisions in difficult situations?"
- **Moral dilemmas**: "A close friend asks you to lie to protect them from consequences..."
- **Scale questions**: "Rate 1-10: Individual freedom vs. Collective good"
- **Value rankings**: "Order these by importance: Justice, Mercy, Truth, Freedom..."

**Answer authentically** - there are no right or wrong answers. The AI is learning about YOUR unique perspective.

**After 5-10 questions**, click **"View My Profile"** to see:
- **Your core values** (ranked by confidence and importance)
- **Moral Foundations scores** (Care, Fairness, Loyalty, Authority, Sanctity, Liberty)
- **Belief dimensions** with confidence intervals
- **Statistical metrics** showing how well the AI understands you

**Continue the conversation** to refine your profile:
- Answer 20-30 questions for a robust profile
- The AI adapts questions based on what it's learned
- Watch your confidence scores increase over time
- See contradictions identified for deeper reflection

---

### Option 2: Compare Established Belief Systems 🤝

This is the original comparison feature for exploring well-known philosophies.

**Navigate to Belief Systems:**
1. Click **"Belief Systems"** in the navigation menu
2. Or go to: `https://localhost:7187/BeliefSystems`

**Explore Pre-loaded Systems:**
The application comes with several belief systems already loaded:
- **World Religions**: Buddhism, Christianity, Islam, Judaism, Hinduism
- **Philosophies**: Stoicism, Existentialism, Utilitarianism
- **Political Systems**: Liberalism, Conservatism, Socialism
- **Modern Worldviews**: Humanism, Transhumanism, Environmentalism

**Add Your Own Belief System:**
1. Click **"Add New Belief System"**
2. **Name**: Buddhism (or any belief system you want to explore)
3. **Description**: Provide a comprehensive description
   ```
   Buddhism teaches the Four Noble Truths: suffering exists, suffering has a cause, 
   suffering can end, and there is a path to end suffering (the Eightfold Path). 
   Core values include compassion, mindfulness, non-attachment, and the middle way. 
   Buddhism emphasizes personal spiritual development and the attainment of insight 
   into the true nature of reality. Key principles include karma, rebirth, and nirvana.
   The practice includes meditation, ethical conduct, and wisdom development.
   ```
4. Click **"Analyze Belief System"**
5. Wait 10-30 seconds for AI analysis

**Compare Two Belief Systems:**
1. Click **"Compare Belief Systems"**
2. **Select two systems** from the dropdowns (e.g., "Buddhism" and "Stoicism")
3. Click **"Analyze & Compare"**
4. Wait for AI to generate comprehensive analysis

**Explore the Comparison Results:**
- **Overlapping values**: Where the belief systems agree
- **Complementary aspects**: How they support each other
- **Divergent points**: Key differences
- **Non-zero-sum opportunities**: Areas where both perspectives add value
- **Common ground**: The foundation for dialogue

**Get Dialogue Suggestions:**
Click **"Generate Dialogue Suggestions"** for practical tips on having productive conversations between people holding these different views.

---

### The Belief Map Visualization 🗺️

**Interactive Exploration:**
1. Navigate to **"Explore"** in the menu
2. Or go to: `https://localhost:7187/Explore`

**Explore Different Views:**

**Categories View** (`/Explore/Categories`)
- See all belief systems organized by category
- Quick overview of religious, philosophical, and political worldviews
- Click any system to see details

**Map View** (`/Explore/Map`) 🗺️
- **Visual representation** of belief systems in multidimensional space
- Systems closer together share more values
- Interactive chart showing relationships
- Hover to see connections and differences

**Timeline View** (`/Explore/Timeline`)
- Chronological view of belief systems
- See how ideas evolved through history
- Understand historical context and influences

**System Details** (`/Explore/System/{name}`)
- Deep dive into a specific belief system
- Core tenets and values
- AI-generated analysis
- Related belief systems
- Historical context

**Comparison View** (`/Explore/Compare`)
- Side-by-side comparison of any two systems
- Visual overlap representation
- Identified common ground
- Practical dialogue suggestions

---

## Tips for Best Results

### For Discovery Sessions

1. **Be Thoughtful**: Take your time with each question - authentic responses lead to better analysis
2. **Be Honest**: The AI doesn't judge - it's here to understand YOUR perspective
3. **Be Specific**: Detailed answers help the AI understand nuances
4. **Be Patient**: Some questions require 10-30 seconds for AI processing
5. **Build Gradually**: Start with 10 questions, then view profile, then continue
6. **Watch Confidence**: Higher confidence scores = more accurate understanding

### For Belief System Comparisons

1. **Be Detailed**: The more comprehensive your descriptions, the better the analysis
2. **Include Multiple Aspects**: Cover ethics, metaphysics, epistemology, values, and practices
3. **Stay Objective**: Describe belief systems accurately, even ones you disagree with
4. **Choose Thoughtfully**: Compare systems with interesting similarities AND differences
5. **Be Patient**: Complex comparisons can take 30-60 seconds

---

## Troubleshooting

### "Error analyzing belief system. Make sure Ollama is running."

**Solution:** 
- **Check if Ollama is running**: 
  - Windows/macOS: Look for Ollama icon in system tray/menu bar
  - Linux: `sudo systemctl status ollama`
- **Verify accessibility**:
  - Linux/macOS: `curl http://localhost:11434`
  - Windows: `Invoke-WebRequest http://localhost:11434`
  - Should return: "Ollama is running"
- **Start Ollama if needed**:
  - Windows/macOS: Launch Ollama from Start Menu/Applications
  - Linux: `sudo systemctl start ollama` or `ollama serve`

**📘 For detailed troubleshooting, see [OLLAMA_SETUP.md](../OLLAMA_SETUP.md#health-check--troubleshooting)**

### "Connection refused" or similar errors

**Solution:**
- Check `appsettings.json` to ensure the endpoint matches your Ollama installation
- Default is `http://localhost:11434`
- Make sure no firewall is blocking the connection
- Restart Ollama: Press Ctrl+C in the Ollama terminal, then run `ollama serve` again

### Application is slow or times out

**Solutions:**
- **Switch to a faster model**: 
  ```bash
  ollama pull llama3.2:1b
  ```
  Then update `appsettings.json` to use `"llama3.2:1b"`
- **Close other applications** to free up RAM and CPU
- **Ensure adequate system resources**: 
  - Minimum: 4GB RAM available
  - Recommended: 8GB+ RAM available
- **Check if your GPU is being used** (if you have one):
  ```bash
  ollama ps
  ```

### Model not found

**Solution:**
- Check which models you have installed:
  ```bash
  ollama list
  ```
- Pull the model specified in `appsettings.json`:
  ```bash
  ollama pull llama3.2:3b
  ```
- Or change the model in `appsettings.json` to one you have installed

### "Cannot connect to database" or persistence errors

**Note**: The application currently stores data in memory only. Data is lost when you restart the app.

**To preserve data across sessions**, see the full README.md for database setup instructions.

### Port already in use (5220 or 7187)

**Solution:**
- **Find what's using the port** (Windows PowerShell):
  ```powershell
  netstat -ano | findstr :5220
  ```
- **Kill the process** or change the port in `launchSettings.json`

---

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
