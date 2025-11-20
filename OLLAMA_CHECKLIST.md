# Ollama Quick Checklist

Use this checklist to verify your Ollama setup before running Common Understanding.

## ✅ Pre-Flight Checklist

### 1. Ollama Installed
```bash
ollama --version
```
- [ ] Returns version number (e.g., `ollama version 0.x.x`)
- [ ] If not: See [OLLAMA_SETUP.md](OLLAMA_SETUP.md#installation-by-platform)

### 2. Ollama Running
**Linux/macOS:**
```bash
curl http://localhost:11434
```

**Windows:**
```powershell
Invoke-WebRequest http://localhost:11434
```

- [ ] Returns: `Ollama is running`
- [ ] If not running:
  - **Linux**: `sudo systemctl start ollama`
  - **macOS/Windows**: Launch Ollama from Applications/Start Menu
  - **Manual**: `ollama serve`

### 3. Model Downloaded
```bash
ollama list
```

- [ ] At least one model is listed (e.g., `llama3.2:3b`)
- [ ] If no models: `ollama pull llama3.2:3b`

### 4. Model Matches Configuration

Check `appsettings.json`:
```json
{
  "Ollama": {
    "Endpoint": "http://localhost:11434",
    "ModelName": "llama3.2:3b"
  }
}
```

- [ ] `ModelName` matches a model from `ollama list`
- [ ] `Endpoint` is `http://localhost:11434` (standard)

### 5. Test Model
```bash
ollama run llama3.2:3b "Say hello"
```

- [ ] Model responds with text
- [ ] If error: See [OLLAMA_SETUP.md#troubleshooting](OLLAMA_SETUP.md#health-check--troubleshooting)

---

## 🚨 Common Issues

### Issue: "Cannot connect to Ollama"

**Check:**
1. [ ] Ollama is running: `curl http://localhost:11434`
2. [ ] Port 11434 is not blocked by firewall
3. [ ] No other application using port 11434

**Fix:**
```bash
# Check what's using the port
# Linux/macOS:
lsof -i :11434

# Windows:
netstat -ano | findstr :11434

# Restart Ollama
# Linux:
sudo systemctl restart ollama

# macOS/Windows:
# Close and reopen Ollama app
```

---

### Issue: "Model not found"

**Check:**
1. [ ] Model is downloaded: `ollama list`
2. [ ] Model name in `appsettings.json` matches exactly

**Fix:**
```bash
# Pull the model specified in appsettings.json
ollama pull llama3.2:3b

# Or change appsettings.json to match an installed model
```

---

### Issue: App is slow or times out

**Check:**
1. [ ] Adequate system resources (4GB+ RAM available)
2. [ ] Model size is appropriate for your hardware

**Fix:**
```bash
# Switch to faster model
ollama pull llama3.2:1b

# Update appsettings.json:
"ModelName": "llama3.2:1b"

# Close other applications to free RAM
```

---

### Issue: "Connection refused" or "Port already in use"

**Check:**
1. [ ] Another Ollama instance is running
2. [ ] Different application using port 11434

**Fix:**
```bash
# Find what's using the port
# Linux/macOS:
sudo lsof -i :11434
sudo kill -9 <PID>

# Windows:
netstat -ano | findstr :11434
Stop-Process -Id <PID> -Force

# Start Ollama
ollama serve
```

---

## 🎯 Quick Start Commands

**Install Ollama:**
```bash
# Linux/macOS
curl -fsSL https://ollama.com/install.sh | sh

# Windows: Download from https://ollama.com/download/windows
```

**Download Model:**
```bash
ollama pull llama3.2:3b
```

**Start Ollama:**
```bash
# Linux (auto-starts as service)
sudo systemctl start ollama

# macOS/Windows (usually auto-starts)
ollama serve  # if not running
```

**Test Everything:**
```bash
# 1. Check Ollama
curl http://localhost:11434

# 2. List models
ollama list

# 3. Test model
ollama run llama3.2:3b "Hello"
```

---

## 📋 Model Recommendations

| System RAM | Recommended Model | Download Command |
|-----------|------------------|------------------|
| 2-4 GB | llama3.2:1b | `ollama pull llama3.2:1b` |
| 4-8 GB | llama3.2:3b ⭐ | `ollama pull llama3.2:3b` |
| 8-16 GB | llama3.1:8b | `ollama pull llama3.1:8b` |
| 16+ GB | qwen2.5:7b | `ollama pull qwen2.5:7b` |

⭐ = Recommended for most users

---

## 🔗 More Help

- **Detailed Setup**: [OLLAMA_SETUP.md](OLLAMA_SETUP.md)
- **Quick Start Guide**: [CommonUnderstanding/QUICKSTART.md](CommonUnderstanding/QUICKSTART.md)
- **Full README**: [CommonUnderstanding/README.md](CommonUnderstanding/README.md)
- **Azure Deployment**: [AZURE_DEPLOYMENT.md](AZURE_DEPLOYMENT.md)
- **Self-Hosting**: [SELF-HOSTING-GUIDE.md](SELF-HOSTING-GUIDE.md)

---

**All checks passed? You're ready to run Common Understanding! 🚀**

```bash
cd CommonUnderstanding
dotnet run
```

Then open: `https://localhost:7187` or `http://localhost:5220`
