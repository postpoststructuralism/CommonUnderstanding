# Azure Deployment Script for CommonUnderstanding
# This script deploys the application to Azure App Service in the freedom-ledger resource group

param(
    [string]$ResourceGroup = "freedom-ledger",
    [string]$AppName = "common-understanding",
    [string]$Location = "eastus",
    [string]$PlanName = "freedom-ledger-plan",
    [string]$Runtime = "DOTNETCORE:9.0"
)

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

# Configure App Settings
Write-Host "Configuring App Settings..." -ForegroundColor Yellow
az webapp config appsettings set --name $AppName --resource-group $ResourceGroup --settings ASPNETCORE_ENVIRONMENT="Production" Ollama__Endpoint="https://ollama-service.azurewebsites.net" Ollama__ModelName="llama3.2:3b"

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
dotnet publish -c Release -o ..\$publishPath
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
Write-Host "1. The Ollama endpoint is configured to: https://ollama-service.azurewebsites.net" -ForegroundColor White
Write-Host "   You'll need to either:" -ForegroundColor White
Write-Host "   - Deploy Ollama to Azure (separate container/VM)" -ForegroundColor White
Write-Host "   - Use a cloud-based LLM service (OpenAI, Azure OpenAI, etc.)" -ForegroundColor White
Write-Host "   - Configure the app to use an external Ollama instance" -ForegroundColor White
Write-Host ""
Write-Host "2. To view logs:" -ForegroundColor White
Write-Host "   az webapp log tail --name $AppName --resource-group $ResourceGroup" -ForegroundColor Gray
Write-Host ""
Write-Host "3. To configure settings:" -ForegroundColor White
Write-Host "   Visit: https://portal.azure.com and navigate to your Web App" -ForegroundColor Gray
Write-Host ""
Write-Host "Clean up deployment files..." -ForegroundColor Yellow
Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
Remove-Item $publishPath -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "✓ Cleanup complete" -ForegroundColor Green
