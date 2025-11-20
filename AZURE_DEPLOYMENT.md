# Azure Deployment Guide

This guide walks you through deploying the Common Understanding application to Microsoft Azure in the `freedom-ledger` resource group.

## ⚠️ Important: Ollama Requirements

**This application requires Ollama for AI functionality.** Azure App Service **cannot** run Ollama directly. You have two options:

### Option A: Local Ollama (Client-Side AI)
- Users run Ollama on their own machines
- Application connects to `localhost:11434` from user's browser
- **Limitation**: Server-side AI features won't work
- **Best for**: Development/testing

### Option B: Ollama on Azure VM (Recommended for Production)
- Deploy a separate Azure VM to run Ollama
- Configure the App Service to connect to the VM
- **Recommended**: See [Setting Up Ollama on Azure VM](#ollama-on-azure-vm) section below
- **Best for**: Production deployments

> **📘 For detailed Ollama setup**, see [OLLAMA_SETUP.md](OLLAMA_SETUP.md)

## Prerequisites

1. **Azure Account**: You need an active Azure subscription
   - Sign up at: https://azure.microsoft.com/free/

2. **Azure CLI**: Install the Azure Command Line Interface
   - Download from: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli
   - Windows: Download the MSI installer
   - Verify installation: `az --version`

3. **.NET 9.0 SDK**: Required to build the application
   - Already installed if you've been running locally
   - Verify: `dotnet --version`

---

## Deployment Options

### Option 1: Automated Deployment (Recommended)

Use the provided PowerShell script for one-command deployment.

**Step 1: Open PowerShell as Administrator**

**Step 2: Navigate to project directory**
```powershell
cd c:\Code\CommonUnderstanding
```

**Step 3: Run the deployment script**
```powershell
.\deploy-to-azure.ps1
```

The script will:
- ✅ Check for Azure CLI installation
- ✅ Login to Azure (if needed)
- ✅ Create/verify the `freedom-ledger` resource group
- ✅ Create an App Service Plan (Free tier by default)
- ✅ Create the Web App
- ✅ Build and publish the application
- ✅ Deploy to Azure

**Custom Parameters:**
```powershell
.\deploy-to-azure.ps1 -ResourceGroup "freedom-ledger" -AppName "my-custom-name" -Location "eastus"
```

---

### Option 2: Manual Deployment

For more control over the deployment process.

#### Step 1: Login to Azure
```powershell
az login
```

Your browser will open for authentication.

#### Step 2: Create Resource Group (if it doesn't exist)
```powershell
az group create --name freedom-ledger --location eastus
```

#### Step 3: Create App Service Plan
```powershell
az appservice plan create `
    --name freedom-ledger-plan `
    --resource-group freedom-ledger `
    --location eastus `
    --sku F1 `
    --is-linux
```

**Pricing Tiers:**
- `F1` - Free tier (limited resources, good for testing)
- `B1` - Basic tier (~$13/month, better performance)
- `S1` - Standard tier (~$70/month, production-ready)

#### Step 4: Create Web App
```powershell
az webapp create `
    --name common-understanding `
    --resource-group freedom-ledger `
    --plan freedom-ledger-plan `
    --runtime "DOTNETCORE:9.0"
```

**Note**: The app name must be globally unique. Try `common-understanding-[yourname]` if taken.

#### Step 5: Configure App Settings
```powershell
az webapp config appsettings set `
    --name common-understanding `
    --resource-group freedom-ledger `
    --settings `
        ASPNETCORE_ENVIRONMENT="Production" `
        Ollama__Endpoint="http://your-ollama-service" `
        Ollama__ModelName="llama3.2:3b"
```

#### Step 6: Build and Publish
```powershell
cd CommonUnderstanding
dotnet publish -c Release -o ..\publish
cd ..
```

#### Step 7: Create Deployment ZIP
```powershell
Compress-Archive -Path publish\* -DestinationPath deploy.zip -Force
```

#### Step 8: Deploy to Azure
```powershell
az webapp deployment source config-zip `
    --name common-understanding `
    --resource-group freedom-ledger `
    --src deploy.zip
```

#### Step 9: Open Your App
```powershell
az webapp browse --name common-understanding --resource-group freedom-ledger
```

Or visit: `https://common-understanding.azurewebsites.net`

---

## Setting Up Ollama on Azure VM {#ollama-on-azure-vm}

**This is the recommended approach for production deployments.** Run Ollama on a dedicated Azure VM for reliable, low-latency AI processing.

> **📘 For detailed Ollama installation**, see [OLLAMA_SETUP.md](OLLAMA_SETUP.md)

### Step 1: Create an Azure VM

**Option A: Using Azure Portal (GUI)**

1. Go to [Azure Portal](https://portal.azure.com)
2. Click **"Create a resource"** → **"Virtual Machine"**
3. **Basics Tab**:
   - Resource group: `freedom-ledger`
   - VM name: `ollama-vm`
   - Region: Same as your App Service (e.g., East US)
   - Image: **Ubuntu Server 24.04 LTS**
   - Size: **Standard_D4s_v3** (4 vCPUs, 16 GB RAM) - minimum recommended
   - Authentication: SSH public key (recommended) or Password
4. **Networking Tab**:
   - Virtual network: Create new or use existing
   - Public IP: Yes (for initial setup)
   - NIC network security group: **Advanced**
   - Configure inbound rules:
     - Allow SSH (port 22)
     - Allow Ollama (port 11434) - **only from App Service subnet for security**
5. **Review + Create** → **Create**

**Option B: Using Azure CLI**

```powershell
# Create the VM
az vm create `
    --name ollama-vm `
    --resource-group freedom-ledger `
    --image Ubuntu2404 `
    --size Standard_D4s_v3 `
    --admin-username azureuser `
    --generate-ssh-keys `
    --public-ip-sku Standard

# Open port 11434 for Ollama
az vm open-port `
    --name ollama-vm `
    --resource-group freedom-ledger `
    --port 11434 `
    --priority 1001
```

**Recommended VM Sizes:**

| Size | vCPUs | RAM | Cost/Month | Best For |
|------|-------|-----|------------|----------|
| Standard_D2s_v3 | 2 | 8 GB | ~$70 | Testing, light models (1b-3b) |
| Standard_D4s_v3 | 4 | 16 GB | ~$140 | **Recommended** for 3b-7b models |
| Standard_D8s_v3 | 8 | 32 GB | ~$280 | Production, 8b+ models |

### Step 2: Connect to the VM

```powershell
# Get the public IP
az vm show `
    --name ollama-vm `
    --resource-group freedom-ledger `
    --show-details `
    --query publicIps `
    --output tsv

# SSH into the VM
ssh azureuser@<PUBLIC_IP>
```

### Step 3: Install Ollama on the VM

Once connected via SSH:

```bash
# Update system
sudo apt update && sudo apt upgrade -y

# Install Ollama
curl -fsSL https://ollama.com/install.sh | sh

# Verify installation
ollama --version

# Configure Ollama to listen on all interfaces (not just localhost)
sudo systemctl stop ollama

# Edit systemd service file
sudo nano /etc/systemd/system/ollama.service
```

Add the following environment variable in the `[Service]` section:
```ini
[Service]
Environment="OLLAMA_HOST=0.0.0.0:11434"
```

Save (Ctrl+X, Y, Enter) and reload:

```bash
sudo systemctl daemon-reload
sudo systemctl start ollama
sudo systemctl enable ollama

# Verify Ollama is running and listening
sudo systemctl status ollama
curl http://localhost:11434
# Should return: "Ollama is running"
```

### Step 4: Download AI Models

```bash
# Download recommended model
ollama pull llama3.2:3b

# Or for better quality (requires more RAM)
ollama pull llama3.1:8b

# Verify
ollama list
```

### Step 5: Configure Firewall (Important Security Step)

**Restrict Ollama access to only your App Service:**

```bash
# Get your App Service outbound IPs
az webapp show `
    --name common-understanding `
    --resource-group freedom-ledger `
    --query outboundIpAddresses `
    --output tsv
```

**Configure NSG (Network Security Group) in Azure Portal:**
1. Go to your VM → **Networking** → **Network settings**
2. Edit the inbound rule for port 11434
3. Change **Source** from "Any" to **IP Addresses**
4. Add your App Service outbound IPs (comma-separated)
5. **Save**

**Or use Azure CLI:**
```powershell
# Update NSG rule to restrict to App Service IPs
az network nsg rule update `
    --name allow-ollama `
    --nsg-name ollama-vmNSG `
    --resource-group freedom-ledger `
    --source-address-prefixes <APP_SERVICE_IP_1> <APP_SERVICE_IP_2>
```

### Step 6: Get VM Private IP

For best security, use VNet integration:

```bash
# Get the private IP
az vm show `
    --name ollama-vm `
    --resource-group freedom-ledger `
    --show-details `
    --query privateIps `
    --output tsv
```

### Step 7: Configure App Service to Connect to Ollama VM

**Option A: Using Public IP (Simpler, Less Secure)**

```powershell
# Get public IP
$OLLAMA_IP = az vm show `
    --name ollama-vm `
    --resource-group freedom-ledger `
    --show-details `
    --query publicIps `
    --output tsv

# Update App Service settings
az webapp config appsettings set `
    --name common-understanding `
    --resource-group freedom-ledger `
    --settings Ollama__Endpoint="http://${OLLAMA_IP}:11434"
```

**Option B: Using VNet Integration (Recommended, More Secure)**

1. **Create VNet Integration** (if not already done):
```powershell
# Create VNet subnet for App Service
az network vnet subnet create `
    --name app-service-subnet `
    --resource-group freedom-ledger `
    --vnet-name <VNET_NAME> `
    --address-prefixes 10.0.1.0/24

# Enable VNet integration for App Service
az webapp vnet-integration add `
    --name common-understanding `
    --resource-group freedom-ledger `
    --vnet <VNET_NAME> `
    --subnet app-service-subnet
```

2. **Use Private IP in App Settings**:
```powershell
# Get private IP
$OLLAMA_PRIVATE_IP = az vm show `
    --name ollama-vm `
    --resource-group freedom-ledger `
    --show-details `
    --query privateIps `
    --output tsv

# Update App Service settings to use private IP
az webapp config appsettings set `
    --name common-understanding `
    --resource-group freedom-ledger `
    --settings Ollama__Endpoint="http://${OLLAMA_PRIVATE_IP}:11434"
```

### Step 8: Test the Connection

```powershell
# Restart the App Service to pick up new settings
az webapp restart `
    --name common-understanding `
    --resource-group freedom-ledger

# Browse to your app
az webapp browse `
    --name common-understanding `
    --resource-group freedom-ledger
```

Visit the **AI Status** page in your app (`/api/AIStatus/status`) to verify Ollama connectivity.

### Step 9: (Optional) Auto-Shutdown to Save Costs

Configure the VM to auto-shutdown during non-business hours:

```powershell
# Set auto-shutdown at 11 PM EST
az vm auto-shutdown `
    --name ollama-vm `
    --resource-group freedom-ledger `
    --time 2300
```

### Monitoring & Maintenance

**Check Ollama status:**
```bash
# SSH into VM
ssh azureuser@<IP>

# Check service status
sudo systemctl status ollama

# View logs
sudo journalctl -u ollama -f

# Check running models
ollama ps

# Monitor resources
htop
```

**Update Ollama:**
```bash
# SSH into VM
curl -fsSL https://ollama.com/install.sh | sh
sudo systemctl restart ollama
```

### Cost Optimization Tips

1. **Use Reserved Instances**: Save 30-40% with 1-year commitment
2. **Auto-Shutdown**: Configure shutdown during non-peak hours
3. **Right-Size VM**: Start with D4s_v3, monitor usage, downgrade if possible
4. **Use Spot Instances**: Save up to 90% (but VM can be evicted)

**Enable Spot Instance:**
```powershell
az vm create `
    --name ollama-vm `
    --resource-group freedom-ledger `
    --image Ubuntu2404 `
    --size Standard_D4s_v3 `
    --priority Spot `
    --max-price -1 `
    --eviction-policy Deallocate
```

---

## Alternative: Ollama in Azure

### Option A: Azure Container Instance with Ollama

1. **Deploy Ollama in a Container**:
```powershell
# Create a container instance with GPU support
az container create `
    --name ollama-instance `
    --resource-group freedom-ledger `
    --image ollama/ollama:latest `
    --cpu 4 `
    --memory 8 `
    --ports 11434 `
    --dns-name-label ollama-freedom-ledger
```

2. **Update Web App Settings**:
```powershell
az webapp config appsettings set `
    --name common-understanding `
    --resource-group freedom-ledger `
    --settings Ollama__Endpoint="http://ollama-freedom-ledger.eastus.azurecontainer.io:11434"
```

**Cost Estimate**: ~$100-150/month for 4 CPU, 8GB RAM container

---

#### Option B: Azure OpenAI Service (Cloud-Based Alternative)

Instead of Ollama, use Azure OpenAI for a fully managed solution.

1. **Create Azure OpenAI Resource**:
```powershell
az cognitiveservices account create `
    --name common-understanding-openai `
    --resource-group freedom-ledger `
    --kind OpenAI `
    --sku S0 `
    --location eastus
```

2. **Deploy a Model**:
```powershell
az cognitiveservices account deployment create `
    --name common-understanding-openai `
    --resource-group freedom-ledger `
    --deployment-name gpt-4 `
    --model-name gpt-4 `
    --model-version "0613" `
    --model-format OpenAI `
    --scale-settings-scale-type "Standard"
```

3. **Update Application Code**: Modify `SemanticKernelService.cs` to use Azure OpenAI instead of Ollama.

**Cost Estimate**: Pay-per-use, typically $0.03-0.06 per 1K tokens

---

#### Option C: Hybrid Approach (Development)

Keep Ollama running locally and expose it via a tunnel for development/testing.

1. **Install Cloudflare Tunnel** (on your local machine):
```powershell
# Download cloudflared
Invoke-WebRequest -Uri "https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-windows-amd64.exe" -OutFile "cloudflared.exe"

# Create tunnel to Ollama
.\cloudflared.exe tunnel --url http://localhost:11434
```

2. **Use the provided URL** in your Azure Web App settings:
```powershell
az webapp config appsettings set `
    --name common-understanding `
    --resource-group freedom-ledger `
    --settings Ollama__Endpoint="https://your-tunnel-id.trycloudflare.com"
```

**Note**: This is for development only. The tunnel will close when you stop cloudflared.

---

#### Option D: Azure Virtual Machine with Ollama

Deploy a dedicated VM running Ollama.

1. **Create Ubuntu VM**:
```powershell
az vm create `
    --name ollama-vm `
    --resource-group freedom-ledger `
    --image Ubuntu2204 `
    --size Standard_D4s_v3 `
    --admin-username azureuser `
    --generate-ssh-keys `
    --public-ip-sku Standard
```

2. **SSH into VM and install Ollama**:
```bash
ssh azureuser@<VM-PUBLIC-IP>
curl -fsSL https://ollama.com/install.sh | sh
ollama pull llama3.2:3b
ollama serve
```

3. **Open port 11434**:
```powershell
az vm open-port `
    --port 11434 `
    --resource-group freedom-ledger `
    --name ollama-vm
```

4. **Update Web App settings** with VM's public IP.

**Cost Estimate**: ~$120-180/month for Standard_D4s_v3 (4 vCPUs, 16GB RAM)

---

## Recommended Configuration for freedom-ledger

Based on typical use cases, here's the recommended setup:

### For Testing/Development:
- **Web App**: Free tier (F1)
- **Ollama**: Local machine with Cloudflare Tunnel
- **Cost**: $0/month

### For Small Production (1-50 users):
- **Web App**: Basic tier (B1) - $13/month
- **Ollama**: Azure Container Instance (4 CPU, 8GB) - $120/month
- **Total**: ~$135/month

### For Production (50+ users):
- **Web App**: Standard tier (S1) - $70/month
- **Azure OpenAI**: Pay-per-use - ~$50-200/month depending on usage
- **Total**: ~$120-270/month

---

## Post-Deployment Configuration

### 1. Enable HTTPS (Free SSL Certificate)

Azure App Service provides free SSL certificates automatically.

```powershell
az webapp update `
    --name common-understanding `
    --resource-group freedom-ledger `
    --https-only true
```

### 2. Configure Custom Domain (Optional)

If you own a domain like `beliefs.yourdomain.com`:

```powershell
# Add custom domain
az webapp config hostname add `
    --webapp-name common-understanding `
    --resource-group freedom-ledger `
    --hostname beliefs.yourdomain.com

# Bind SSL certificate
az webapp config ssl bind `
    --name common-understanding `
    --resource-group freedom-ledger `
    --certificate-thumbprint <thumbprint> `
    --ssl-type SNI
```

### 3. Enable Application Insights (Monitoring)

```powershell
# Create Application Insights
az monitor app-insights component create `
    --app common-understanding-insights `
    --location eastus `
    --resource-group freedom-ledger `
    --application-type web

# Get instrumentation key
$instrumentationKey = az monitor app-insights component show `
    --app common-understanding-insights `
    --resource-group freedom-ledger `
    --query instrumentationKey -o tsv

# Configure Web App
az webapp config appsettings set `
    --name common-understanding `
    --resource-group freedom-ledger `
    --settings APPLICATIONINSIGHTS_CONNECTION_STRING="InstrumentationKey=$instrumentationKey"
```

### 4. Scale Settings

**Manual Scaling**:
```powershell
az appservice plan update `
    --name freedom-ledger-plan `
    --resource-group freedom-ledger `
    --number-of-workers 2
```

**Auto-Scaling** (Standard tier or higher):
```powershell
az monitor autoscale create `
    --name common-understanding-autoscale `
    --resource-group freedom-ledger `
    --resource common-understanding `
    --resource-type Microsoft.Web/serverfarms `
    --min-count 1 `
    --max-count 5 `
    --count 1
```

---

## Monitoring and Troubleshooting

### View Live Logs
```powershell
az webapp log tail `
    --name common-understanding `
    --resource-group freedom-ledger
```

### View Application Logs
```powershell
az webapp log download `
    --name common-understanding `
    --resource-group freedom-ledger `
    --log-file logs.zip
```

### Restart the App
```powershell
az webapp restart `
    --name common-understanding `
    --resource-group freedom-ledger
```

### Check App Health
```powershell
az webapp show `
    --name common-understanding `
    --resource-group freedom-ledger `
    --query state
```

### Common Issues

#### 1. "Application Error" on website

**Check logs**:
```powershell
az webapp log tail --name common-understanding --resource-group freedom-ledger
```

**Common causes**:
- Missing app settings (Ollama endpoint)
- Ollama service not reachable
- .NET runtime mismatch

#### 2. "Cannot connect to Ollama"

**Verify endpoint**:
```powershell
# Get current settings
az webapp config appsettings list `
    --name common-understanding `
    --resource-group freedom-ledger
```

**Test connectivity** from Azure:
- Use Kudu console: `https://common-understanding.scm.azurewebsites.net`
- Navigate to Debug Console → CMD
- Try: `curl http://your-ollama-endpoint:11434`

#### 3. Slow Performance

**Upgrade tier**:
```powershell
az appservice plan update `
    --name freedom-ledger-plan `
    --resource-group freedom-ledger `
    --sku B2
```

---

## Updating the Application

### Update Deployment

```powershell
# Pull latest code
cd c:\Code\CommonUnderstanding
git pull

# Run deployment script again
.\deploy-to-azure.ps1
```

Or manually:
```powershell
cd CommonUnderstanding
dotnet publish -c Release -o ..\publish
cd ..
Compress-Archive -Path publish\* -DestinationPath deploy.zip -Force

az webapp deployment source config-zip `
    --name common-understanding `
    --resource-group freedom-ledger `
    --src deploy.zip
```

---

## Cost Management

### View Current Costs
```powershell
az consumption usage list `
    --start-date 2025-11-01 `
    --end-date 2025-11-30
```

### Set Budget Alerts

1. Go to **Azure Portal**: https://portal.azure.com
2. Navigate to **Cost Management + Billing**
3. Create a **Budget**
4. Set alert at 80% and 100% of budget

### Stop Resources to Save Costs

**Stop Web App** (while keeping configuration):
```powershell
az webapp stop `
    --name common-understanding `
    --resource-group freedom-ledger
```

**Delete Everything** (removes all resources):
```powershell
az group delete --name freedom-ledger --yes
```

---

## Security Best Practices

### 1. Enable Managed Identity

```powershell
az webapp identity assign `
    --name common-understanding `
    --resource-group freedom-ledger
```

### 2. Restrict Access (IP Whitelisting)

```powershell
az webapp config access-restriction add `
    --name common-understanding `
    --resource-group freedom-ledger `
    --rule-name office `
    --action Allow `
    --ip-address 203.0.113.0/24 `
    --priority 100
```

### 3. Enable Authentication (Azure AD)

```powershell
az webapp auth update `
    --name common-understanding `
    --resource-group freedom-ledger `
    --enabled true `
    --action LoginWithAzureActiveDirectory
```

---

## Next Steps

1. **Deploy Ollama Service** (choose an option from above)
2. **Test the Application** (visit your Azure URL)
3. **Configure Monitoring** (Application Insights)
4. **Set Up Backups** (if using database in future)
5. **Configure Custom Domain** (optional)

---

## Useful Links

- **Azure Portal**: https://portal.azure.com
- **Azure CLI Reference**: https://docs.microsoft.com/en-us/cli/azure/
- **App Service Documentation**: https://docs.microsoft.com/en-us/azure/app-service/
- **Azure Pricing Calculator**: https://azure.microsoft.com/en-us/pricing/calculator/

---

## Support

For issues specific to Azure deployment:
- Azure Support: https://azure.microsoft.com/en-us/support/
- Stack Overflow: Tag `azure-web-app-service`

For application issues:
- GitHub Issues: https://github.com/postpoststructuralism/CommonUnderstanding/issues
