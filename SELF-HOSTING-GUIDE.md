# Self-Hosting Guide for CommonUnderstanding

## ?? Complete Guide to Running Your Belief Discovery System on Your Own Hardware

This guide will help you host CommonUnderstanding on your own hardware with minimal command-line work using GUI tools wherever possible.

---

## Table of Contents

1. [Quick Start (Windows - Easiest)](#quick-start-windows)
2. [Hardware Options](#hardware-options)
3. [GUI-Friendly Linux Setup](#gui-friendly-linux-setup)
4. [Making It Accessible from Internet](#internet-access)
5. [Monitoring & Management](#monitoring--management)
6. [Backup & Maintenance](#backup--maintenance)
7. [Troubleshooting](#troubleshooting)

---

## Quick Start (Windows - Easiest) {#quick-start-windows}

**If you just want to get started NOW on your current Windows PC:**

> **📘 For detailed Ollama installation help**, see [OLLAMA_SETUP.md](OLLAMA_SETUP.md)

### Prerequisites

1. **Install Ollama** (if not already installed):
   - Download from [https://ollama.com/download/windows](https://ollama.com/download/windows)
   - Run the installer
   - Ollama will start automatically in the background
   
2. **Download a model**:
   ```powershell
   ollama pull llama3.2:3b
   ```

3. **Verify Ollama is running**:
   ```powershell
   Invoke-WebRequest http://localhost:11434
   # Should return: "Ollama is running"
   ```

### Option A: Run Manually (Test First)

1. **Open PowerShell as Administrator**
2. **Navigate to your project:**
   ```powershell
   cd C:\Code\CommonUnderstanding\CommonUnderstanding
   ```

3. **Set production mode:**
   ```powershell
   $env:ASPNETCORE_ENVIRONMENT="Production"
   ```

4. **Run the app:**
   ```powershell
   dotnet run --urls "http://0.0.0.0:5220;https://0.0.0.0:7187"
   ```

5. **Access from any device on your network:**
   - Find your PC's IP: `ipconfig` (look for IPv4, e.g., 192.168.1.100)
   - From phone/tablet: `http://192.168.1.100:5220`

### Option B: Run as Windows Service (Always On)

**Using NSSM (GUI Tool for Windows Services)**

1. **Download NSSM (Non-Sucking Service Manager)**
   - Visit: https://nssm.cc/download
   - Download the latest version
   - Extract to `C:\nssm`

2. **Publish your app:**
   ```powershell
   cd C:\Code\CommonUnderstanding\CommonUnderstanding
   dotnet publish -c Release -o C:\Services\CommonUnderstanding
   ```

3. **Install as service using NSSM GUI:**
   ```powershell
   C:\nssm\win64\nssm.exe install CommonUnderstanding
   ```

4. **In the NSSM GUI window that opens:**
   - **Application** tab:
     - Path: `C:\Program Files\dotnet\dotnet.exe`
     - Startup directory: `C:\Services\CommonUnderstanding`
     - Arguments: `CommonUnderstanding.dll`
   - **Details** tab:
     - Display name: `CommonUnderstanding Belief Discovery`
     - Description: `Belief discovery system with AI-powered questions`
   - **Environment** tab:
     - Add: `ASPNETCORE_ENVIRONMENT=Production`
     - Add: `ASPNETCORE_URLS=http://0.0.0.0:5220`
   - Click **Install service**

5. **Start the service:**
   - Open Windows Services (`services.msc`)
   - Find "CommonUnderstanding Belief Discovery"
   - Right-click ? Start
   - Set Startup type to "Automatic" for auto-start on boot

**Your app now runs 24/7 in the background!** ?

---

## Hardware Options {#hardware-options}

### Option 1: Your Current Windows PC (Free)

**Pros:**
- ? No new hardware needed
- ? Familiar Windows environment
- ? Easy to manage with GUI tools
- ? Ollama already running

**Cons:**
- ?? Higher power consumption (~50-150W = $5-15/month)
- ?? Need to leave PC running 24/7

**Best for:** Testing, personal use, learning

---

### Option 2: Raspberry Pi 5 (8GB) - $80

**Pros:**
- ? Very low power (~5W = $0.50/month)
- ? Silent operation
- ? Small footprint
- ? Can run Ubuntu Desktop (has GUI!)

**Cons:**
- ?? ARM processor (slower for AI)
- ?? 8GB RAM limit
- ?? Requires SD card/SSD

**Best for:** Always-on hosting, learning Linux

**What you need:**
- Raspberry Pi 5 (8GB): $80
- Official power supply: $12
- MicroSD card (64GB+): $15
- Case with fan: $10
- **Total: ~$120**

---

### Option 3: Used Mini PC - $100-200

**Examples:**
- HP EliteDesk 800 G3 Mini
- Dell OptiPlex 3050 Micro
- Lenovo ThinkCentre M710q Tiny

**Pros:**
- ? x64 processor (faster for AI)
- ? 8-32GB RAM upgradeable
- ? Low power (~15-25W = $2-3/month)
- ? Built-in SSD
- ? Can run Windows or Linux

**Cons:**
- ?? Slightly larger than Pi
- ?? Need to buy used

**Best for:** Best bang for buck, serious self-hosting

**Where to buy:**
- eBay: Search "mini pc i5" (~$100-150)
- Amazon Renewed
- Local computer shops

---

### Option 4: NAS (If You Already Have One)

**Compatible with:**
- Synology (Docker support)
- QNAP (Container Station)
- TrueNAS Scale
- Unraid

**Pros:**
- ? Already running 24/7
- ? Built-in backup features
- ? Web GUI for everything

**Cons:**
- ?? May need to buy if you don't have one ($200-500+)

---

## GUI-Friendly Linux Setup {#gui-friendly-linux-setup}

**For those who want Linux but with a GUI (recommended for Pi or Mini PC)**

### Step 1: Install Ubuntu Desktop

**Why Ubuntu Desktop instead of Server?**
- ? Full desktop environment with GUI
- ? Easier for beginners
- ? Can still do everything Server does
- ? Visual tools for file management, monitoring

**Installation:**

1. **Download Ubuntu Desktop 24.04 LTS:**
   - Visit: https://ubuntu.com/download/desktop
   - Download 64-bit version
   - For Raspberry Pi: Use official Raspberry Pi Imager

2. **Create bootable USB:**
 - **Windows:** Use Rufus (https://rufus.ie/)
   - **For Pi:** Use Raspberry Pi Imager (https://www.raspberrypi.com/software/)

3. **Install Ubuntu:**
   - Boot from USB
   - Follow graphical installer
   - Create user account
   - Connect to WiFi/Ethernet

### Step 2: Install Required Software (GUI Method)

> **📘 For detailed Ollama installation**, see [OLLAMA_SETUP.md](OLLAMA_SETUP.md)

**Using Ubuntu Software Center (GUI):**

1. **Open Ubuntu Software** (click grid icon, search "Software")

2. **Install Terminal** (if not already there - you'll need it minimally)

3. **Install .NET 9:**
   - Open Terminal (Ctrl+Alt+T)
   - Copy/paste this (only needed once):
   ```bash
   wget https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
   sudo dpkg -i packages-microsoft-prod.deb
   sudo apt update
   sudo apt install -y dotnet-sdk-9.0
   ```

4. **Install Ollama** (runs as local service):
   ```bash
   curl -fsSL https://ollama.com/install.sh | sh
   ```
   
   Ollama automatically installs as a systemd service and starts running. Verify:
   ```bash
   sudo systemctl status ollama
   curl http://localhost:11434  # Should return: "Ollama is running"
   ```

5. **Download an AI model**:
   ```bash
   ollama pull llama3.2:3b
   ```

6. **Install Git**:
   ```bash
   sudo apt install -y git
   ```

**Important**: The application will connect to Ollama at `http://localhost:11434`. No additional configuration needed!

### Step 3: Deploy Your App (GUI + Minimal Terminal)

**Using File Manager + Text Editor:**

1. **Clone your repository:**
   ```bash
   cd ~
 git clone https://github.com/postpoststructuralism/CommonUnderstanding
   ```

2. **Build and publish:**
   ```bash
   cd ~/CommonUnderstanding/CommonUnderstanding
   dotnet publish -c Release -o ~/commonunderstanding-app
   ```

3. **Create a startup script (GUI):**
 - Open **Text Editor** (gedit)
   - Paste this:
   ```bash
   #!/bin/bash
   cd /home/YOUR_USERNAME/commonunderstanding-app
   export ASPNETCORE_ENVIRONMENT=Production
   export ASPNETCORE_URLS=http://0.0.0.0:5220
   dotnet CommonUnderstanding.dll
   ```
   - Replace `YOUR_USERNAME` with your actual username
   - Save as: `/home/YOUR_USERNAME/start-commonunderstanding.sh`
   - Make executable:
   ```bash
   chmod +x ~/start-commonunderstanding.sh
   ```

4. **Test it:**
   ```bash
   ~/start-commonunderstanding.sh
   ```
   - Should see: `Now listening on: http://0.0.0.0:5220`
   - Access from browser: `http://localhost:5220`

### Step 4: Auto-Start on Boot (GUI Method)

**Using Startup Applications:**

1. **Open Startup Applications:**
   - Press Super key (Windows key)
- Search "Startup Applications"
   - Click "Add"

2. **Fill in:**
   - Name: `CommonUnderstanding`
   - Command: `/home/YOUR_USERNAME/start-commonunderstanding.sh`
   - Comment: `Belief Discovery System`
   - Click "Add"

**Or use systemd (more reliable for servers):**

1. **Create service file:**
   ```bash
   sudo nano /etc/systemd/system/commonunderstanding.service
   ```

2. **Paste this:**
   ```ini
   [Unit]
   Description=CommonUnderstanding Belief Discovery
   After=network.target ollama.service

   [Service]
   Type=notify
   WorkingDirectory=/home/YOUR_USERNAME/commonunderstanding-app
   ExecStart=/usr/bin/dotnet /home/YOUR_USERNAME/commonunderstanding-app/CommonUnderstanding.dll
   Restart=always
   RestartSec=10
 User=YOUR_USERNAME
   Environment=ASPNETCORE_ENVIRONMENT=Production
   Environment=ASPNETCORE_URLS=http://0.0.0.0:5220

   [Install]
   WantedBy=multi-user.target
   ```
   - Press Ctrl+X, then Y, then Enter to save

3. **Enable and start:**
   ```bash
   sudo systemctl enable commonunderstanding
   sudo systemctl start commonunderstanding
   ```

4. **Check status:**
   ```bash
   sudo systemctl status commonunderstanding
   ```

---

## Making It Accessible from Internet {#internet-access}

### Option 1: Cloudflare Tunnel (Recommended - Free & Easy)

**Why Cloudflare Tunnel?**
- ? No port forwarding needed
- ? Free SSL certificate
- ? Hides your home IP address
- ? DDoS protection
- ? Works behind any router/firewall
- ? No static IP needed

**GUI-Friendly Setup:**

1. **Sign up for Cloudflare:**
   - Visit: https://dash.cloudflare.com/sign-up
   - Create free account

2. **Add a domain (optional but recommended):**
   - If you own a domain: Add it to Cloudflare
   - If not: Use Cloudflare's free `.trycloudflare.com` subdomain

3. **Install Cloudflare Tunnel:**

   **Windows:**
   - Download: https://github.com/cloudflare/cloudflared/releases/latest
   - Look for: `cloudflared-windows-amd64.exe`
   - Rename to `cloudflared.exe`
   - Move to: `C:\cloudflared\`

   **Linux:**
   ```bash
   curl -L https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-linux-amd64 -o cloudflared
   sudo mv cloudflared /usr/local/bin/
   sudo chmod +x /usr/local/bin/cloudflared
   ```

4. **Login and setup (one-time):**
   
   **Windows:**
   ```powershell
   cd C:\cloudflared
   .\cloudflared.exe tunnel login
   ```

   **Linux:**
   ```bash
   cloudflared tunnel login
 ```
   - Browser will open - login to Cloudflare
   - Select your domain (or skip for free subdomain)

5. **Create tunnel:**

   **Windows:**
   ```powershell
   .\cloudflared.exe tunnel create commonunderstanding
   .\cloudflared.exe tunnel route dns commonunderstanding beliefs.yourdomain.com
   ```

   **Linux:**
   ```bash
   cloudflared tunnel create commonunderstanding
   cloudflared tunnel route dns commonunderstanding beliefs.yourdomain.com
   ```

6. **Create config file:**

   **Windows:** Create `C:\cloudflared\config.yml`:
   ```yaml
   tunnel: commonunderstanding
   credentials-file: C:\Users\YOUR_USERNAME\.cloudflared\YOUR_TUNNEL_ID.json

   ingress:
     - hostname: beliefs.yourdomain.com
 service: http://localhost:5220
     - service: http_status:404
   ```

   **Linux:** Create `/home/YOUR_USERNAME/.cloudflared/config.yml`:
   ```yaml
   tunnel: commonunderstanding
   credentials-file: /home/YOUR_USERNAME/.cloudflared/YOUR_TUNNEL_ID.json

   ingress:
  - hostname: beliefs.yourdomain.com
       service: http://localhost:5220
     - service: http_status:404
   ```

7. **Run tunnel:**

   **Windows (as service using NSSM):**
   ```powershell
C:\nssm\win64\nssm.exe install CloudflareTunnel C:\cloudflared\cloudflared.exe "tunnel run"
   net start CloudflareTunnel
   ```

   **Linux (as systemd service):**
   ```bash
   sudo cloudflared service install
   sudo systemctl start cloudflared
   sudo systemctl enable cloudflared
   ```

8. **Access your app:**
   - Visit: `https://beliefs.yourdomain.com`
   - Works from anywhere in the world! ??

**Using Free Subdomain (No Domain Needed):**

If you don't have a domain, just run:

**Windows:**
```powershell
.\cloudflared.exe tunnel --url http://localhost:5220
```

**Linux:**
```bash
cloudflared tunnel --url http://localhost:5220
```

You'll get a URL like: `https://random-words-1234.trycloudflare.com`

---

### Option 2: Tailscale (Private Access Only)

**Perfect for:**
- ? Accessing from your own devices only
- ? No public exposure
- ? Super easy setup
- ? Free for personal use (up to 100 devices)

**Setup:**

1. **Sign up:** https://login.tailscale.com/start
2. **Install Tailscale:**
   - **Windows:** Download from https://tailscale.com/download/windows
   - **Linux:** 
   ```bash
   curl -fsSL https://tailscale.com/install.sh | sh
   sudo tailscale up
   ```

3. **Install on all your devices:**
   - Phone (iOS/Android): Install Tailscale app
   - Laptop: Install Tailscale
   - Tablet: Install Tailscale

4. **Access your app:**
   - Each device gets an IP like `100.x.x.x`
   - From any device: `http://100.x.x.x:5220`
   - Works even when you're away from home!

---

### Option 3: Traditional Port Forwarding + Dynamic DNS

**More complex, but gives you full control:**

**Prerequisites:**
- Router admin access
- Ability to port forward

**Steps:**

1. **Get a free domain name:**
   - **No-IP:** https://www.noip.com (free)
   - **DuckDNS:** https://www.duckdns.org (free, easier)
   - **Dynu:** https://www.dynu.com (free)

2. **Set up Dynamic DNS:**
   
   **Using DuckDNS (easiest):**
   - Go to https://www.duckdns.org
   - Login with GitHub/Google
   - Create subdomain: `commonunderstanding.duckdns.org`
   - Copy your token

   **Install DuckDNS updater:**

   **Windows (Task Scheduler):**
   - Create `C:\duckdns\duck.bat`:
   ```batch
 echo url="https://www.duckdns.org/update?domains=commonunderstanding&token=YOUR_TOKEN&ip=" | curl -k -o C:\duckdns\duck.log -K -
   ```
   - Open Task Scheduler
   - Create Basic Task ? Run every 5 minutes
   - Action: Run `C:\duckdns\duck.bat`

   **Linux (cron job):**
   ```bash
   mkdir ~/duckdns
   cd ~/duckdns
   echo "echo url=\"https://www.duckdns.org/update?domains=commonunderstanding&token=YOUR_TOKEN&ip=\" | curl -k -o ~/duckdns/duck.log -K -" > duck.sh
   chmod +x duck.sh
   crontab -e
   # Add this line:
   */5 * * * * ~/duckdns/duck.sh >/dev/null 2>&1
   ```

3. **Port Forwarding on your router:**
   
   **Find your router IP:**
   - **Windows:** `ipconfig` (look for Default Gateway)
   - **Linux:** `ip route | grep default`
   - Usually: `192.168.1.1` or `192.168.0.1`

   **Access router web interface:**
   - Open browser: `http://192.168.1.1`
   - Login (check router label or manual)

   **Forward ports:**
 - Find "Port Forwarding" or "Virtual Server" section
   - Add new rule:
     - External Port: `443` (HTTPS)
     - Internal Port: `5220`
     - Internal IP: Your server's local IP (e.g., `192.168.1.100`)
     - Protocol: TCP
     - Save

4. **Install Nginx for SSL:**

   **Linux:**
   ```bash
   sudo apt install -y nginx certbot python3-certbot-nginx
   ```

 **Create Nginx config:**
   ```bash
   sudo nano /etc/nginx/sites-available/commonunderstanding
   ```

   **Paste:**
   ```nginx
   server {
       listen 80;
       server_name commonunderstanding.duckdns.org;

       location / {
       proxy_pass http://localhost:5220;
           proxy_http_version 1.1;
           proxy_set_header Upgrade $http_upgrade;
           proxy_set_header Connection "upgrade";
       proxy_set_header Host $host;
           proxy_cache_bypass $http_upgrade;
      proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
proxy_set_header X-Forwarded-Proto $scheme;
       }
   }
   ```

   **Enable site:**
   ```bash
   sudo ln -s /etc/nginx/sites-available/commonunderstanding /etc/nginx/sites-enabled/
   sudo nginx -t
   sudo systemctl restart nginx
   ```

   **Get free SSL certificate:**
 ```bash
   sudo certbot --nginx -d commonunderstanding.duckdns.org
   ```
   - Follow prompts
   - Certificate auto-renews!

5. **Access your app:**
   - Visit: `https://commonunderstanding.duckdns.org`

---

## Monitoring & Management {#monitoring--management}

### GUI Tools for Server Management

#### 1. **Cockpit** (Linux - Web-Based GUI)

**Best for:** Managing Linux servers through a web browser

**Install:**
```bash
sudo apt install -y cockpit
sudo systemctl enable --now cockpit.socket
```

**Access:**
- From any browser: `https://your-server-ip:9090`
- Login with your Linux username/password

**Features:**
- ? View system resources (CPU, RAM, disk)
- ? Manage services (start/stop your app)
- ? View logs
- ? Terminal access in browser
- ? Update system
- ? Manage firewall

**Screenshot tour:**
- Dashboard shows CPU/RAM usage graphs
- Services tab: Start/stop CommonUnderstanding service
- Logs tab: View app logs in real-time
- Terminal tab: Run commands without SSH

---

#### 2. **Portainer** (Docker Management - If Using Docker)

**Install Docker first:**
```bash
sudo apt install -y docker.io docker-compose
sudo systemctl enable --now docker
sudo usermod -aG docker $USER
```

**Install Portainer:**
```bash
docker volume create portainer_data
docker run -d -p 9000:9000 --name=portainer --restart=always \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -v portainer_data:/data \
  portainer/portainer-ce
```

**Access:**
- Browser: `http://your-server-ip:9000`
- Create admin account on first visit

**Features:**
- ? Visual container management
- ? One-click start/stop
- ? View logs with GUI
- ? Resource usage graphs
- ? Easy updates

---

#### 3. **Netdata** (Real-Time Monitoring)

**Beautiful, real-time system monitoring**

**Install:**
```bash
bash <(curl -Ss https://my-netdata.io/kickstart.sh)
```

**Access:**
- Browser: `http://your-server-ip:19999`

**Features:**
- ? Gorgeous real-time graphs
- ? CPU, RAM, disk, network monitoring
- ? Per-process monitoring
- ? Alerts
- ? No configuration needed

---

#### 4. **Webmin** (Advanced Server Management)

**Most comprehensive web-based Linux admin tool**

**Install:**
```bash
curl -o setup-repos.sh https://raw.githubusercontent.com/webmin/webmin/master/setup-repos.sh
sudo sh setup-repos.sh
sudo apt install -y webmin
```

**Access:**
- Browser: `https://your-server-ip:10000`

**Features:**
- ? Full server configuration
- ? File manager
- ? User management
- ? Service management
- ? Backup configuration
- ? System updates

---

### Windows GUI Tools

#### 1. **Windows Admin Center**

**Free Microsoft tool for Windows Server management**

**Download:**
- https://www.microsoft.com/en-us/windows-server/windows-admin-center

**Features:**
- ? Manage services
- ? View performance
- ? Event logs
- ? Remote desktop
- ? PowerShell console

---

#### 2. **Process Explorer** (Sysinternals)

**Advanced task manager**

**Download:**
- https://learn.microsoft.com/en-us/sysinternals/downloads/process-explorer

**Features:**
- ? See exactly what your app is doing
- ? Monitor resource usage
- ? View open files/connections

---

### Log Viewing (GUI)

#### Linux: **GNOME Logs** (Pre-installed on Ubuntu Desktop)

1. Press Super key
2. Search "Logs"
3. Filter by "CommonUnderstanding"
4. View real-time logs with GUI

#### Windows: **Event Viewer**

1. Press Win+R
2. Type: `eventvwr.msc`
3. Navigate to: Windows Logs ? Application
4. Filter by "CommonUnderstanding"

---

### Monitoring Your App Health

**Create a simple health check page:**

Add to your `Program.cs`:

```csharp
app.MapGet("/health", () => new
{
    Status = "Healthy",
    Timestamp = DateTime.UtcNow,
    Environment = app.Environment.EnvironmentName
});
```

Then use **UptimeRobot** (free) to monitor it:
- Sign up: https://uptimerobot.com
- Add HTTP(s) monitor
- URL: `https://your-domain.com/health`
- Get email/SMS alerts if down

---

## Backup & Maintenance {#backup--maintenance}

### Automated Backups

#### Windows: Use built-in File History

1. **Settings** ? **Update & Security** ? **Backup**
2. **Add a drive** (external drive or network location)
3. **More options**
4. **Add folder**: `C:\Services\CommonUnderstanding`

#### Linux: Simple backup script

**Create backup script:**
```bash
nano ~/backup-commonunderstanding.sh
```

**Paste:**
```bash
#!/bin/bash
BACKUP_DIR="/home/YOUR_USERNAME/backups"
DATE=$(date +%Y%m%d_%H%M%S)
APP_DIR="/home/YOUR_USERNAME/commonunderstanding-app"

mkdir -p $BACKUP_DIR
tar -czf $BACKUP_DIR/commonunderstanding_$DATE.tar.gz $APP_DIR

# Keep only last 7 backups
ls -t $BACKUP_DIR/commonunderstanding_*.tar.gz | tail -n +8 | xargs -r rm

echo "Backup completed: $BACKUP_DIR/commonunderstanding_$DATE.tar.gz"
```

**Make executable:**
```bash
chmod +x ~/backup-commonunderstanding.sh
```

**Schedule daily backups:**
```bash
crontab -e
# Add this line:
0 2 * * * /home/YOUR_USERNAME/backup-commonunderstanding.sh
```

---

### Updates

#### Update Your App

**Pull latest code and redeploy:**

**Windows:**
```powershell
cd C:\Code\CommonUnderstanding
git pull
cd CommonUnderstanding
dotnet publish -c Release -o C:\Services\CommonUnderstanding
# Restart service in services.msc
```

**Linux:**
```bash
cd ~/CommonUnderstanding
git pull
cd CommonUnderstanding
dotnet publish -c Release -o ~/commonunderstanding-app
sudo systemctl restart commonunderstanding
```

#### Update System

**Windows:**
- Windows Update runs automatically

**Linux (GUI):**
- Software Updater runs automatically
- Or manually: Open "Software Updater"

**Linux (Terminal):**
```bash
sudo apt update && sudo apt upgrade -y
sudo reboot  # if kernel updated
```

---

### Security Checklist

#### Firewall Setup

**Windows:**
```powershell
# Allow your app through firewall
New-NetFirewallRule -DisplayName "CommonUnderstanding" -Direction Inbound -LocalPort 5220 -Protocol TCP -Action Allow
```

**Linux (UFW - Uncomplicated Firewall):**
```bash
sudo ufw allow 22       # SSH
sudo ufw allow 80   # HTTP
sudo ufw allow 443      # HTTPS
sudo ufw allow 5220     # Your app (if accessing directly)
sudo ufw enable
```

#### Keep Software Updated

**Enable automatic security updates:**

**Linux:**
```bash
sudo apt install unattended-upgrades
sudo dpkg-reconfigure --priority=low unattended-upgrades
# Select "Yes"
```

#### SSH Security (Linux)

**Disable password login (use SSH keys):**
```bash
sudo nano /etc/ssh/sshd_config
```

Find and change:
```
PasswordAuthentication no
PermitRootLogin no
```

**Restart SSH:**
```bash
sudo systemctl restart sshd
```

#### Regular Security Tasks

**Monthly checklist:**
- [ ] Check system updates
- [ ] Review firewall logs
- [ ] Test backups (restore to temp location)
- [ ] Check SSL certificate expiry (auto-renews, but verify)
- [ ] Review access logs for unusual activity

---

## Troubleshooting {#troubleshooting}

### App Won't Start

**Check logs:**

**Windows:**
```powershell
# If running as service
Get-EventLog -LogName Application -Source CommonUnderstanding -Newest 20

# If running manually, check console output
```

**Linux:**
```bash
# If running as systemd service
sudo journalctl -u commonunderstanding -f

# Or check app logs directly
tail -f ~/commonunderstanding-app/logs/*.log
```

**Common issues:**

1. **Port already in use:**
   ```bash
   # Find what's using port 5220
   # Linux:
   sudo lsof -i :5220
   # Windows:
   netstat -ano | findstr :5220
 ```

2. **Ollama not running:**
   ```bash
   # Linux:
   sudo systemctl status ollama
   sudo systemctl start ollama
   
   # Windows:
   # Check Task Manager for Ollama process
   ```

3. **Permissions error:**
   ```bash
   # Linux:
 sudo chown -R YOUR_USERNAME:YOUR_USERNAME ~/commonunderstanding-app
   chmod +x ~/commonunderstanding-app/CommonUnderstanding.dll
   ```

---

### Can't Access from Internet

**Cloudflare Tunnel:**
```bash
# Check tunnel status
# Linux:
sudo systemctl status cloudflared

# Windows:
# Check Services for CloudflareTunnel status
```

**Port Forwarding:**
1. Verify router port forwarding rule is active
2. Check your public IP: https://whatismyipaddress.com
3. Test port is open: https://www.yougetsignal.com/tools/open-ports/
4. Verify Dynamic DNS is updating:
   ```bash
   nslookup commonunderstanding.duckdns.org
   ```

---

### Performance Issues

**Check resource usage:**

**Windows:**
- Open Task Manager (Ctrl+Shift+Esc)
- Find `dotnet.exe` process
- Check CPU/RAM usage

**Linux (GUI):**
- Open System Monitor
- Find `CommonUnderstanding` process

**Linux (Terminal):**
```bash
htop# Install: sudo apt install htop
# Press F4, search "CommonUnderstanding"
```

**If using too much RAM/CPU:**
1. Check if Ollama model is too large
2. Consider switching to lighter model: `ollama pull llama3.2:1b`
3. Limit concurrent users in configuration

---

### Database/Profile Store Issues

**Reset user profiles (if corrupted):**

Your app stores profiles in memory (not persistent by default).

**To add persistent storage, update `Program.cs`:**
```csharp
// Add this before builder.Build()
builder.Services.AddSingleton<IUserProfileRepository, JsonFileProfileRepository>();
```

Then create backup script for JSON files.

---

## Quick Reference Commands

### Service Management

**Linux (systemd):**
```bash
# Start
sudo systemctl start commonunderstanding

# Stop
sudo systemctl stop commonunderstanding

# Restart
sudo systemctl restart commonunderstanding

# View logs
sudo journalctl -u commonunderstanding -f

# Enable auto-start
sudo systemctl enable commonunderstanding
```

**Windows (NSSM):**
```powershell
# Start
net start CommonUnderstanding

# Stop
net stop CommonUnderstanding

# Restart
net stop CommonUnderstanding && net start CommonUnderstanding

# View logs
Get-EventLog -LogName Application -Source CommonUnderstanding -Newest 20
```

---

### Network Testing

**Check if app is listening:**
```bash
# Linux:
sudo ss -tulpn | grep 5220

# Windows:
netstat -ano | findstr 5220
```

**Test from another device:**
```bash
curl http://192.168.1.100:5220
```

---

## Recommended Setup for Beginners

**My recommended path if you're new to self-hosting:**

### Phase 1: Test on Current PC (Week 1)
1. ? Run manually on Windows (see [Quick Start](#quick-start-windows))
2. ? Access from phone on same network
3. ? Test all features work

### Phase 2: Make It Persistent (Week 2)
1. ? Set up as Windows service (NSSM method above)
2. ? Configure auto-start
3. ? Set up basic backup

### Phase 3: Internet Access (Week 3-4)
1. ? Sign up for Cloudflare
2. ? Install Cloudflare Tunnel
3. ? Test access from phone on cellular data

### Phase 4: Dedicated Hardware (Optional - Month 2+)
1. ? Buy used Mini PC or Raspberry Pi
2. ? Install Ubuntu Desktop
3. ? Migrate app to new hardware
4. ? Install Cockpit for GUI management

**Total time commitment:** ~2-4 hours spread over a month

**Total cost:** 
- Phase 1-3: $0 (use existing PC)
- Phase 4: $80-150 for dedicated hardware (optional)

---

## Help & Resources

### Official Documentation
- ASP.NET Core Hosting: https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/
- .NET on Linux: https://learn.microsoft.com/en-us/dotnet/core/install/linux
- Kestrel Web Server: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel

### Community Help
- Reddit: r/selfhosted
- Discord: https://discord.gg/selfhosted
- Stack Overflow: Tag `asp.net-core` + `self-hosting`

### Tools Documentation
- NSSM: https://nssm.cc/usage
- Cloudflare Tunnel: https://developers.cloudflare.com/cloudflare-one/connections/connect-apps/
- Cockpit: https://cockpit-project.org/guide/latest/
- Ubuntu Desktop: https://help.ubuntu.com/

---

## Your Project-Specific Notes

### Current Configuration
- **Development Port:** HTTP: 5220, HTTPS: 7187
- **Ollama Endpoint:** http://localhost:11434
- **Model:** llama3.2:1b
- **Session Timeout:** 30 days
- **GitHub Repository:** https://github.com/postpoststructuralism/CommonUnderstanding

### Future Enhancements to Consider

1. **Persistent Storage:**
   - Add SQLite or PostgreSQL for user profiles
   - Survives restarts/crashes

2. **Multi-User Support:**
   - Add authentication
   - User accounts

3. **Analytics Dashboard:**
   - Track usage patterns
   - Visualize belief distributions

4. **Mobile App:**
   - Native iOS/Android clients
   - Push notifications for new questions

---

## Final Tips

### Best Practices
- ? Start simple (Windows + NSSM)
- ? Test locally before internet exposure
- ? Use Cloudflare Tunnel (safest internet option)
- ? Enable automatic backups early
- ? Monitor disk space (logs can grow)
- ? Keep .NET and Ollama updated

### What to Avoid
- ? Exposing port 5220 directly to internet (use reverse proxy)
- ? Using weak passwords
- ? Forgetting backups before updates
- ? Running as root/Administrator (use service accounts)

---

**Questions?** Open an issue on GitHub or check the resources above!

**Happy Self-Hosting! ????**
