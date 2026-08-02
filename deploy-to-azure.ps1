# Azure Deployment Script for CommonUnderstanding
# This script deploys the application to Azure App Service in the freedom-ledger resource group

param(
    [string]$ResourceGroup = "CommonUnderstanding",
    [string]$AppName = "common-understanding",
    [string]$Location = "eastus",
    [string]$PlanName = "common-understanding-v2-plan",
    [string]$Runtime = "DOTNETCORE:9.0",
    [string]$AzureFoundryModelId = "DeepSeek-V3-0324",
    [string]$AzureFoundrySecondaryModelId = "gpt-4o-mini",
    [int]$FreeAiRequestLimit = 120
)

$ProjectPath = Join-Path $PSScriptRoot "CommonUnderstanding\CommonUnderstanding.csproj"
$secrets = @{}
dotnet user-secrets list --project $ProjectPath 2>$null | ForEach-Object {
    if ($_ -match '^(.+?)\s*=\s*(.*)$') {
        $secrets[$matches[1].Trim()] = $matches[2]
    }
}

$requiredSecretKeys = @(
    "ConnectionStrings:DefaultConnection",
    "AzureFoundry:Endpoint",
    "AzureFoundry:ApiKey"
)
$missingSecretKeys = $requiredSecretKeys | Where-Object {
    $secretValue = $secrets.Item($_)
    [string]::IsNullOrWhiteSpace($secretValue)
}
if ($missingSecretKeys.Count -gt 0) {
    throw "Missing required .NET user secrets: $($missingSecretKeys -join ', '). Configure them with 'dotnet user-secrets set --project $ProjectPath <key> <value>'."
}

$databaseConnectionString = $secrets.Item("ConnectionStrings:DefaultConnection")
$azureFoundryEndpoint = $secrets.Item("AzureFoundry:Endpoint")
$azureFoundryApiKey = $secrets.Item("AzureFoundry:ApiKey")

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Common Understanding - Azure Deployment" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if Azure CLI is installed
Write-Host "Checking Azure CLI installation..." -ForegroundColor Yellow
$azVersion = az --version 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Azure CLI is not installed." -ForegroundColor Red
    Write-Host "Please install from: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli" -ForegroundColor Yellow
    exit 1
}
Write-Host "✓ Azure CLI is installed" -ForegroundColor Green
Write-Host ""

# Login to Azure
Write-Host "Checking Azure login status..." -ForegroundColor Yellow
$accountInfo = az account show 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "Please login to Azure..." -ForegroundColor Yellow
    az login
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Azure login failed" -ForegroundColor Red
        exit 1
    }
}
Write-Host "✓ Logged in to Azure" -ForegroundColor Green
Write-Host ""

# Check if resource group exists
Write-Host "Checking resource group: $ResourceGroup..." -ForegroundColor Yellow
$rgExists = az group exists --name $ResourceGroup
if ($rgExists -eq "false") {
    Write-Host "Creating resource group: $ResourceGroup in $Location..." -ForegroundColor Yellow
    az group create --name $ResourceGroup --location $Location
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Failed to create resource group" -ForegroundColor Red
        exit 1
    }
    Write-Host "✓ Resource group created" -ForegroundColor Green
} else {
    Write-Host "✓ Resource group exists" -ForegroundColor Green
}
Write-Host ""

# Check if App Service Plan exists
Write-Host "Checking App Service Plan: $PlanName..." -ForegroundColor Yellow
$planExists = az appservice plan show --name $PlanName --resource-group $ResourceGroup 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "Creating App Service Plan: $PlanName (Basic tier)..." -ForegroundColor Yellow
    az appservice plan create --name $PlanName --resource-group $ResourceGroup --location $Location --sku B1 --is-linux
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Failed to create App Service Plan" -ForegroundColor Red
        Write-Host "You may need to check your Azure subscription limits or try a different region." -ForegroundColor Yellow
        exit 1
    }
    Write-Host "✓ App Service Plan created" -ForegroundColor Green
} else {
    Write-Host "✓ App Service Plan exists" -ForegroundColor Green
}
Write-Host ""

# Check if Web App exists
Write-Host "Checking Web App: $AppName..." -ForegroundColor Yellow
$appExists = az webapp show --name $AppName --resource-group $ResourceGroup 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "Creating Web App: $AppName..." -ForegroundColor Yellow
    az webapp create --name $AppName --resource-group $ResourceGroup --plan $PlanName --runtime $Runtime
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Failed to create Web App" -ForegroundColor Red
        exit 1
    }
    Write-Host "✓ Web App created" -ForegroundColor Green
} else {
    Write-Host "✓ Web App exists" -ForegroundColor Green
}
Write-Host ""

# Always enforce .NET 9 runtime stack (fixes 500.30 if runtime was ever reset)
Write-Host "Configuring runtime stack to .NET 9..." -ForegroundColor Yellow
az webapp config set --name $AppName --resource-group $ResourceGroup --linux-fx-version "DOTNETCORE|9.0"
if ($LASTEXITCODE -ne 0) {
    Write-Host "WARNING: Failed to set runtime stack. Check Azure portal manually." -ForegroundColor Yellow
} else {
    Write-Host "✓ Runtime stack set to DOTNETCORE|9.0" -ForegroundColor Green
}
Write-Host ""

# Configure App Settings
Write-Host "Configuring App Settings..." -ForegroundColor Yellow
az webapp config appsettings set --name $AppName --resource-group $ResourceGroup --settings `
    ASPNETCORE_ENVIRONMENT="Production" `
    DatabaseProvider="SqlServer" `
    ConnectionStrings__DefaultConnection="$databaseConnectionString" `
    AzureFoundry__Endpoint="$AzureFoundryEndpoint" `
    AzureFoundry__ApiKey="$AzureFoundryApiKey" `
    AzureFoundry__ModelId="$AzureFoundryModelId" `
    AzureFoundry__SecondaryModelId="$AzureFoundrySecondaryModelId" `
    AzureFoundry__UseSecondaryFallback="true" `
    AiAccessPolicy__Enabled="true" `
    AiAccessPolicy__FreeAiRequestLimit="$FreeAiRequestLimit" `
    Ollama__EnableFallback="true" `
    Ollama__Endpoint="https://ollama-service.azurewebsites.net" `
    Ollama__Model="llama3.2:3b"

if ($LASTEXITCODE -ne 0) {
    Write-Host "WARNING: Failed to set app settings. You may need to configure manually." -ForegroundColor Yellow
} else {
    Write-Host "✓ App settings configured" -ForegroundColor Green
}
Write-Host ""

# Build and publish the application
Write-Host "Building and publishing application..." -ForegroundColor Yellow
$publishPath = ".\publish"
if (Test-Path $publishPath) {
    Remove-Item $publishPath -Recurse -Force
}

Push-Location ".\CommonUnderstanding"
# Publish self-contained for linux-x64 so the app carries its own .NET runtime.
# This prevents HTTP 500.30 errors caused by a missing/mismatched runtime on the server.
dotnet publish -c Release -r linux-x64 --self-contained true -o ..\$publishPath
$buildResult = $LASTEXITCODE
Pop-Location

if ($buildResult -ne 0) {
    Write-Host "ERROR: Failed to build application" -ForegroundColor Red
    exit 1
}
Write-Host "✓ Application built successfully" -ForegroundColor Green
Write-Host ""

# Create deployment ZIP
Write-Host "Creating deployment package..." -ForegroundColor Yellow
$zipPath = ".\deploy.zip"
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Compress-Archive -Path "$publishPath\*" -DestinationPath $zipPath -Force
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Failed to create deployment package" -ForegroundColor Red
    exit 1
}
Write-Host "✓ Deployment package created" -ForegroundColor Green
Write-Host ""

# Deploy to Azure
Write-Host "Deploying to Azure App Service..." -ForegroundColor Yellow
Write-Host "This may take several minutes..." -ForegroundColor Yellow
az webapp deployment source config-zip --name $AppName --resource-group $ResourceGroup --src $zipPath

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Deployment failed" -ForegroundColor Red
    exit 1
}
Write-Host "✓ Deployment completed successfully!" -ForegroundColor Green
Write-Host ""

# Get the URL
$appUrl = "https://$AppName.azurewebsites.net"
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Deployment Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Your application is available at:" -ForegroundColor Yellow
Write-Host $appUrl -ForegroundColor Cyan
Write-Host ""
Write-Host "IMPORTANT NOTES:" -ForegroundColor Yellow
Write-Host "1. Azure Foundry endpoint/model are configured from script parameters." -ForegroundColor White
Write-Host "   If endpoint/key are empty, Azure AI calls will fail until configured." -ForegroundColor White
Write-Host "" 
Write-Host "2. The Ollama endpoint is configured to: https://ollama-service.azurewebsites.net" -ForegroundColor White
Write-Host "   You will need to either:" -ForegroundColor White
Write-Host "   - Deploy Ollama to Azure (separate container/VM)" -ForegroundColor White
Write-Host "   - Use a cloud-based LLM service such as Azure OpenAI" -ForegroundColor White
Write-Host "   - Configure the app to use an external Ollama instance" -ForegroundColor White
Write-Host ""
Write-Host "3. To view logs:" -ForegroundColor White
Write-Host "   az webapp log tail --name $AppName --resource-group $ResourceGroup" -ForegroundColor Gray
Write-Host ""
Write-Host "4. To configure settings:" -ForegroundColor White
Write-Host "   Visit: https://portal.azure.com and navigate to your Web App" -ForegroundColor Gray
Write-Host ""
Write-Host "Clean up deployment files..." -ForegroundColor Yellow
Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
Remove-Item $publishPath -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "✓ Cleanup complete" -ForegroundColor Green
