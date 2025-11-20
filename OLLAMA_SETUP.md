# Ollama Setup Guide for Common Understanding

## 🎯 Overview

Common Understanding uses **Ollama** for AI-powered belief analysis. Every instance of the application—whether running on your local machine, on Azure, or self-hosted—connects to a **local Ollama instance** at `http://localhost:11434`.

## 🔑 Key Architecture Principle

**Each machine runs its own Ollama instance.** This design ensures:

- ✅ **Privacy**: AI processing happens locally, no data sent to external services
- ✅ **Low Latency**: No network delays for AI inference
- ✅ **Reliability**: No dependency on external API availability
- ✅ **Cost Efficiency**: No per-request API charges
- ✅ **Offline Capability**: Works without internet connection (after model download)

### Deployment Scenarios

```
┌─────────────────────────────────────────────────────────────┐
│                    LOCAL DEVELOPMENT                         │
│                                                              │
│  ┌──────────────┐         ┌──────────────┐                 │
│  │   Browser    │────────▶│  ASP.NET App │                 │
│  └──────────────┘         └──────┬───────┘                 │
│                                   │                          │
│                                   ▼                          │
│                          ┌──────────────┐                   │
│                          │    Ollama    │                   │
│                          │ localhost:   │                   │
│                          │    11434     │                   │
│                          └──────────────┘                   │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│                    SELF-HOSTED SERVER                        │
│                                                              │
│  Internet ──────────▶ ┌──────────────┐                     │
│                       │  ASP.NET App │                     │
│                       └──────┬───────┘                     │
│                              │                              │
│                              ▼                              │
│                     ┌──────────────┐                       │
│                     │    Ollama    │                       │
│                     │ localhost:   │                       │
│                     │    11434     │                       │
│                     └──────────────┘                       │
│                     (Same machine)                         │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│                    AZURE DEPLOYMENT                          │
│                                                              │
│  Internet ──────────▶ ┌──────────────┐  Private Network   │
│                       │ App Service  │◀────────────────┐   │
│                       └──────────────┘                  │   │
│                                                          │   │
│                       ┌──────────────┐                  │   │
│                       │  Azure VM    │◀─────────────────┘   │
│                       │              │                      │
│                       │  ┌─────────┐ │                      │
│                       │  │ Ollama  │ │                      │
│                       │  │localhost│ │                      │
│                       │  │  :11434 │ │                      │
│                       │  └─────────┘ │                      │
│                       └──────────────┘                      │
│                       (Separate VM)                         │
└─────────────────────────────────────────────────────────────┘
```

**Important**: This application does NOT connect to remote Ollama servers. Each machine running the application needs its own local Ollama installation (or connects to a VM on the same private network in Azure scenarios).

---

## 📦 Installation by Platform

### Windows

**Option 1: Official Installer (Recommended)**
1. Visit [https://ollama.com/download/windows](https://ollama.com/download/windows)
2. Download the Windows installer (`.exe`)
3. Run the installer
4. Ollama will automatically start and run in the background

**Option 2: Manual Installation**
```powershell
# Download using PowerShell
Invoke-WebRequest -Uri https://ollama.com/download/OllamaSetup.exe -OutFile OllamaSetup.exe

# Run installer
.\OllamaSetup.exe
```

**Verify Installation**
```powershell
ollama --version
# Should display: ollama version 0.x.x
```

**Check Ollama is Running**
```powershell
# Test if Ollama API is accessible
Invoke-WebRequest -Uri http://localhost:11434
# Should return: Ollama is running
```

---

### macOS

**Option 1: Official Installer (Recommended)**
1. Visit [https://ollama.com/download/mac](https://ollama.com/download/mac)
2. Download the macOS installer (`.dmg`)
3. Open the DMG and drag Ollama to Applications
4. Launch Ollama from Applications

**Option 2: Homebrew**
```bash
brew install ollama
ollama serve
```

**Verify Installation**
```bash
ollama --version
curl http://localhost:11434
# Should return: Ollama is running
```

---

### Linux (Ubuntu/Debian)

**One-line Installation**
```bash
curl -fsSL https://ollama.com/install.sh | sh
```

**Start Ollama as a Service**
```bash
# Ollama automatically installs as a systemd service
sudo systemctl status ollama
sudo systemctl start ollama
sudo systemctl enable ollama  # Auto-start on boot
```

**Manual Installation (Alternative)**
```bash
# Download binary
curl -L https://ollama.com/download/ollama-linux-amd64 -o ollama
sudo mv ollama /usr/local/bin/
sudo chmod +x /usr/local/bin/ollama

# Run Ollama
ollama serve
```

**Verify Installation**
```bash
ollama --version
curl http://localhost:11434
# Should return: Ollama is running
```

---

### Linux (Other Distributions)

**Arch Linux**
```bash
yay -S ollama
sudo systemctl start ollama
sudo systemctl enable ollama
```

**Fedora/RHEL/CentOS**
```bash
curl -fsSL https://ollama.com/install.sh | sh
sudo systemctl start ollama
sudo systemctl enable ollama
```

---

## 🤖 Downloading Models

After installing Ollama, you need to download at least one AI model.

### Recommended Models

| Model | Size | RAM Required | Speed | Quality | Best For |
|-------|------|--------------|-------|---------|----------|
| **llama3.2:1b** | 1.3 GB | 2-4 GB | ⚡⚡⚡⚡⚡ | ⭐⭐⭐ | **Laptops, Testing** |
| **llama3.2:3b** ✨ | 2.0 GB | 4-6 GB | ⚡⚡⚡⚡ | ⭐⭐⭐⭐ | **Recommended Default** |
| **llama3.1:8b** | 4.7 GB | 8-12 GB | ⚡⚡⚡ | ⭐⭐⭐⭐⭐ | **Best Quality** |
| **qwen2.5:7b** | 4.4 GB | 8-10 GB | ⚡⚡⚡⭐ | ⭐⭐⭐⭐⭐ | **Excellent Reasoning** |
| **phi3:3.8b** | 2.3 GB | 4-6 GB | ⚡⚡⚡⚡ | ⭐⭐⭐⭐ | **Fast Alternative** |

### Download a Model

**For most users (recommended):**
```bash
ollama pull llama3.2:3b
```

**For low-end systems (2-4GB RAM):**
```bash
ollama pull llama3.2:1b
```

**For high-performance systems (8GB+ RAM):**
```bash
ollama pull llama3.1:8b
```

**For best reasoning quality:**
```bash
ollama pull qwen2.5:7b
```

### Verify Model Installation

```bash
ollama list
```

Expected output:
```
NAME              ID            SIZE     MODIFIED
llama3.2:3b       a80c4f17acd5  2.0 GB   2 minutes ago
```

### Test the Model

```bash
ollama run llama3.2:3b
```

Type a message to test:
```
>>> Hello!
Hello! How can I help you today?

>>> /bye
```

---

## ⚙️ Configuration

### Default Configuration

The application is pre-configured to connect to Ollama at `http://localhost:11434`. No changes needed for most users!

**File**: `CommonUnderstanding/appsettings.json`
```json
{
  "Ollama": {
    "Endpoint": "http://localhost:11434",
    "ModelName": "llama3.2:3b"
  }
}
```

### Changing the Model

Edit `appsettings.json` to use a different model:

```json
{
  "Ollama": {
    "Endpoint": "http://localhost:11434",
    "ModelName": "llama3.1:8b"
  }
}
```

**Important**: The model name must match exactly what's shown in `ollama list`.

### Environment-Specific Configuration

**Development** (`appsettings.Development.json`)
```json
{
  "Ollama": {
    "Endpoint": "http://localhost:11434",
    "ModelName": "llama3.2:1b"
  }
}
```

**Production** (`appsettings.Production.json`)
```json
{
  "Ollama": {
    "Endpoint": "http://localhost:11434",
    "ModelName": "llama3.2:3b"
  }
}
```

---

## 🚀 Running Ollama

### As a Background Service (Recommended)

**Windows**
- Ollama runs automatically in the background after installation
- Check system tray for Ollama icon
- Right-click icon for options

**Linux (systemd)**
```bash
sudo systemctl start ollama
sudo systemctl enable ollama  # Auto-start on boot
sudo systemctl status ollama  # Check status
```

**macOS**
- Ollama runs automatically when launched from Applications
- Check menu bar for Ollama icon

### Manual Startup (Development)

If Ollama isn't running as a service:

```bash
ollama serve
```

Keep this terminal window open. You should see:
```
Listening on 127.0.0.1:11434 (version 0.x.x)
```

---

## 🏥 Health Check & Troubleshooting

### Check if Ollama is Running

**Linux/macOS:**
```bash
curl http://localhost:11434
```

**Windows (PowerShell):**
```powershell
Invoke-WebRequest -Uri http://localhost:11434
```

**Expected Response:**
```
Ollama is running
```

### Common Issues

#### 1. "Cannot connect to Ollama"

**Symptoms:**
- Application shows "Ollama offline" status
- Error messages in browser console
- Discovery feature doesn't work

**Solutions:**

**Check if Ollama is running:**
```bash
# Linux/macOS
ps aux | grep ollama

# Windows
Get-Process ollama
```

**Start Ollama if not running:**
```bash
# Linux
sudo systemctl start ollama

# macOS/Windows
ollama serve
```

**Check port 11434 is accessible:**
```bash
# Linux/macOS
lsof -i :11434

# Windows
netstat -ano | findstr :11434
```

---

#### 2. "Model not found"

**Symptoms:**
- Application status shows "model-missing"
- Errors mentioning model name

**Solutions:**

**List installed models:**
```bash
ollama list
```

**Pull the missing model:**
```bash
ollama pull llama3.2:3b
```

**Update `appsettings.json` to match an installed model.**

---

#### 3. "Connection refused" or "Port already in use"

**Symptoms:**
- Ollama won't start
- Error: "address already in use"

**Solutions:**

**Find what's using port 11434:**
```bash
# Linux/macOS
sudo lsof -i :11434

# Windows
netstat -ano | findstr :11434
```

**Kill the conflicting process:**
```bash
# Linux/macOS
sudo kill -9 <PID>

# Windows
Stop-Process -Id <PID> -Force
```

**Or configure Ollama to use a different port:**
```bash
# Set environment variable
export OLLAMA_HOST=0.0.0.0:11435
ollama serve

# Update appsettings.json
"Endpoint": "http://localhost:11435"
```

---

#### 4. Application is slow or times out

**Symptoms:**
- Long wait times for AI responses
- Timeout errors

**Solutions:**

**Switch to a faster/smaller model:**
```bash
ollama pull llama3.2:1b
```

Update `appsettings.json`:
```json
"ModelName": "llama3.2:1b"
```

**Check system resources:**
```bash
# Linux
htop

# macOS
top

# Windows
Task Manager
```

**Close other applications to free RAM/CPU.**

**Check if GPU is being used (if available):**
```bash
ollama ps
```

---

#### 5. Firewall blocking connection

**Symptoms:**
- Works locally but not from other machines
- Azure/remote instances can't connect

**Important Note**: Common Understanding is designed to use LOCAL Ollama only. Each machine runs its own Ollama instance.

**If you need to connect remotely (not recommended):**

**Linux (UFW):**
```bash
sudo ufw allow 11434/tcp
```

**Windows Firewall:**
```powershell
New-NetFirewallRule -DisplayName "Ollama" -Direction Inbound -LocalPort 11434 -Protocol TCP -Action Allow
```

**Configure Ollama to listen on all interfaces:**
```bash
export OLLAMA_HOST=0.0.0.0:11434
ollama serve
```

---

## 🌐 Deployment Scenarios

### Local Development

**Setup:**
1. Install Ollama locally
2. Pull a model: `ollama pull llama3.2:3b`
3. Ollama runs in background
4. Application connects to `localhost:11434`

**Config:** Use defaults in `appsettings.json`

---

### Self-Hosted (Home Server, Raspberry Pi, etc.)

**Setup:**
1. Install Ollama on the server
2. Pull a model appropriate for hardware
3. Enable Ollama as a service
4. Application runs on same machine, connects to `localhost:11434`

**Example (Ubuntu Server):**
```bash
# Install Ollama
curl -fsSL https://ollama.com/install.sh | sh

# Pull model (choose based on RAM)
ollama pull llama3.2:3b

# Enable service
sudo systemctl enable ollama
sudo systemctl start ollama

# Verify
curl http://localhost:11434
```

**Config:** Use defaults in `appsettings.Production.json`

---

### Azure App Service

**Important**: Azure App Service instances run the application code, but Ollama must be installed locally on the machine accessing the app, OR you need a separate VM running Ollama.

**Option 1: Ollama on Local Machine (Recommended for Development)**
- User runs Ollama on their local machine
- Application connects to `localhost:11434` from user's browser perspective
- **Limitation**: Won't work for server-side AI analysis

**Option 2: Ollama on Azure VM (Recommended for Production)**
1. Create an Azure VM (Standard_D4s_v3 or larger)
2. Install Ollama on the VM
3. Configure the App Service to connect to the VM's private IP
4. Update `appsettings.Production.json`:

```json
{
  "Ollama": {
    "Endpoint": "http://10.0.1.4:11434",
    "ModelName": "llama3.2:3b"
  }
}
```

**Azure VM Setup (Ubuntu):**
```bash
# SSH into VM
ssh azureuser@<vm-ip>

# Install Ollama
curl -fsSL https://ollama.com/install.sh | sh

# Pull model
ollama pull llama3.2:3b

# Configure to listen on all interfaces
export OLLAMA_HOST=0.0.0.0:11434
echo 'export OLLAMA_HOST=0.0.0.0:11434' >> ~/.bashrc

# Start Ollama
ollama serve

# Or use systemd
sudo systemctl enable ollama
sudo systemctl start ollama
```

**Security Note**: Use Azure Virtual Network (VNet) to secure communication between App Service and VM. Don't expose Ollama to the public internet.

---

### Docker/Container Deployment

**Dockerfile with Ollama:**

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

# Install Ollama
RUN curl -fsSL https://ollama.com/install.sh | sh

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["CommonUnderstanding.csproj", "."]
RUN dotnet restore
COPY . .
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Start Ollama in background and run app
CMD ollama serve & dotnet CommonUnderstanding.dll
```

**Or use Docker Compose with separate services:**

```yaml
version: '3.8'

services:
  ollama:
    image: ollama/ollama:latest
    ports:
      - "11434:11434"
    volumes:
      - ollama_data:/root/.ollama
    command: serve

  app:
    build: .
    ports:
      - "8080:8080"
    environment:
      - Ollama__Endpoint=http://ollama:11434
      - Ollama__ModelName=llama3.2:3b
    depends_on:
      - ollama

volumes:
  ollama_data:
```

**Pull model into container:**
```bash
docker-compose exec ollama ollama pull llama3.2:3b
```

---

## 🔧 Advanced Configuration

### Environment Variables

You can override settings using environment variables:

**Linux/macOS:**
```bash
export Ollama__Endpoint="http://localhost:11434"
export Ollama__ModelName="llama3.1:8b"
dotnet run
```

**Windows (PowerShell):**
```powershell
$env:Ollama__Endpoint="http://localhost:11434"
$env:Ollama__ModelName="llama3.1:8b"
dotnet run
```

### Runtime Model Switching

The application supports switching models at runtime via the AI Status page:

1. Navigate to the AI Status page in the app
2. View available models
3. Select a different model from the dropdown
4. Click "Switch Model"

**API Endpoint:**
```http
POST /api/AIStatus/switch-model
Content-Type: application/json

{
  "modelName": "llama3.1:8b"
}
```

### Custom Ollama Host

If you need to run Ollama on a non-standard port or host:

**Configure Ollama:**
```bash
export OLLAMA_HOST=0.0.0.0:8080
ollama serve
```

**Update appsettings.json:**
```json
{
  "Ollama": {
    "Endpoint": "http://localhost:8080",
    "ModelName": "llama3.2:3b"
  }
}
```

---

## 📊 Monitoring Ollama

### Check Ollama Status

**Via API:**
```bash
curl http://localhost:11434/api/tags
```

**View Running Models:**
```bash
ollama ps
```

Expected output:
```
NAME              ID            SIZE     PROCESSOR    UNTIL
llama3.2:3b       a80c4f17acd5  2.0 GB   100% CPU     4 minutes from now
```

### View Logs

**Linux (systemd):**
```bash
sudo journalctl -u ollama -f
```

**Manual run:**
- Logs appear in the terminal where you ran `ollama serve`

**Windows:**
- Check Event Viewer → Application logs
- Or check Ollama log files in `%USERPROFILE%\.ollama\logs`

---

## 🎯 Best Practices

### ✅ Do's

- ✅ Keep Ollama updated: `ollama update`
- ✅ Use the recommended model for your hardware
- ✅ Run Ollama as a service for reliability
- ✅ Monitor system resources (RAM, CPU)
- ✅ Download multiple models for testing
- ✅ Use faster models (1b, 3b) for development
- ✅ Use quality models (7b, 8b) for production

### ❌ Don'ts

- ❌ Don't expose Ollama to public internet without security
- ❌ Don't run models larger than your RAM can handle
- ❌ Don't forget to pull models before configuring the app
- ❌ Don't use remote Ollama servers (high latency, security risk)
- ❌ Don't change Ollama endpoint unless you know what you're doing

---

## 🆘 Getting Help

### Resources

- **Ollama Documentation**: [https://github.com/ollama/ollama/tree/main/docs](https://github.com/ollama/ollama/tree/main/docs)
- **Ollama Discord**: [https://discord.gg/ollama](https://discord.gg/ollama)
- **Model Library**: [https://ollama.com/library](https://ollama.com/library)

### Common Understanding Specific

- Check the [QUICKSTART.md](CommonUnderstanding/QUICKSTART.md) guide
- Review [README.md](CommonUnderstanding/README.md) for application details
- Open an issue on GitHub

### Diagnostic Information

When asking for help, provide:

```bash
# Ollama version
ollama --version

# List of models
ollama list

# OS information
# Linux:
uname -a
cat /etc/os-release

# macOS:
sw_vers

# Windows:
systeminfo | findstr /B /C:"OS Name" /C:"OS Version"

# Check Ollama accessibility
curl -v http://localhost:11434
```

---

## 🎉 Quick Validation Checklist

Before running the application, verify:

- [ ] Ollama is installed: `ollama --version`
- [ ] Ollama is running: `curl http://localhost:11434`
- [ ] Model is downloaded: `ollama list`
- [ ] Model name in `appsettings.json` matches installed model
- [ ] Endpoint in `appsettings.json` is `http://localhost:11434`
- [ ] Application can reach Ollama (check AI Status page)

---

**You're now ready to run Common Understanding with Ollama! 🚀**

For next steps, see [QUICKSTART.md](CommonUnderstanding/QUICKSTART.md).
