# Provision a new Azure instance for CommonUnderstanding.
# Creates resource group, App Service plan, Web App, Application Insights,
# and Log Analytics workspace for the migration target environment.

param(
    [string]$SubscriptionId = "3425f381-90e9-49d2-8a85-4c3cab0599ab",
    [string]$ResourceGroup = "CommonUnderstanding",
    [string]$Location = "eastus",
    [string]$PlanName = "common-understanding-v2-plan",
    [string]$AppName = "common-understanding-v2",
    [string]$WorkspaceName = "common-understanding-v2-logs",
    [string]$AppInsightsName = "common-understanding-v2-ai",
    [string]$Runtime = "DOTNETCORE:9.0",
    [string]$Sku = "B1"
)

$ErrorActionPreference = "Stop"

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

Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "CommonUnderstanding - New Azure Instance Setup" -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Checking Azure CLI..." -ForegroundColor Yellow
az --version > $null
Write-Host "[OK] Azure CLI available" -ForegroundColor Green

Write-Host "Checking Azure login..." -ForegroundColor Yellow
az account show > $null 2>&1
if ($LASTEXITCODE -ne 0) {
    az login > $null
}
Write-Host "[OK] Azure login active" -ForegroundColor Green

if (-not [string]::IsNullOrWhiteSpace($SubscriptionId)) {
    Write-Host "Selecting subscription: $SubscriptionId" -ForegroundColor Yellow
    az account set --subscription $SubscriptionId
}

$activeSub = az account show --query id --output tsv
Write-Host "Active subscription: $activeSub" -ForegroundColor Cyan

Write-Host "Ensuring resource group exists..." -ForegroundColor Yellow
az group create --name $ResourceGroup --location $Location --output none
Write-Host "[OK] Resource group ready: $ResourceGroup" -ForegroundColor Green

Write-Host "Ensuring Log Analytics workspace exists..." -ForegroundColor Yellow
$workspaceExists = az monitor log-analytics workspace list --resource-group $ResourceGroup --query "[?name=='$WorkspaceName'].name" --output tsv 2>$null
if ([string]::IsNullOrWhiteSpace($workspaceExists)) {
    az monitor log-analytics workspace create --resource-group $ResourceGroup --workspace-name $WorkspaceName --location $Location --output none
}
Write-Host "[OK] Log Analytics workspace ready: $WorkspaceName" -ForegroundColor Green

#Write-Host "Ensuring Application Insights exists..." -ForegroundColor Yellow
#$appiExists = az monitor app-insights component show --app $AppInsightsName --resource-group $ResourceGroup --query name --output tsv 2>$null
#if ([string]::IsNullOrWhiteSpace($appiExists)) {
#    az monitor app-insights component create --app $AppInsightsName --location $Location --resource-group $ResourceGroup --workspace $WorkspaceName --kind web --application-type web --output none
#}
#$appInsightsConnectionString = az monitor app-insights component show --app $AppInsightsName --resource-group $ResourceGroup --query connectionString --output tsv
#Write-Host "[OK] Application Insights ready: $AppInsightsName" -ForegroundColor Green

Write-Host "Ensuring App Service plan exists..." -ForegroundColor Yellow
$planExists = az appservice plan list --resource-group $ResourceGroup --query "[?name=='$PlanName'].name" --output tsv 2>$null
if ([string]::IsNullOrWhiteSpace($planExists)) {
    az appservice plan create --name $PlanName --resource-group $ResourceGroup --location $Location --sku $Sku --is-linux --output none
}
Write-Host "[OK] App Service plan ready: $PlanName" -ForegroundColor Green

Write-Host "Ensuring Web App exists..." -ForegroundColor Yellow
$appExists = az webapp list --resource-group $ResourceGroup --query "[?name=='$AppName'].name" --output tsv 2>$null
if ([string]::IsNullOrWhiteSpace($appExists)) {
    az webapp create --name $AppName --resource-group $ResourceGroup --plan $PlanName --runtime $Runtime --output none
}
az webapp config set --name $AppName --resource-group $ResourceGroup --linux-fx-version "DOTNETCORE|9.0" --output none
Write-Host "[OK] Web App ready: $AppName" -ForegroundColor Green

Write-Host "Applying baseline app settings..." -ForegroundColor Yellow
az webapp config appsettings set --name $AppName --resource-group $ResourceGroup --settings `
    ASPNETCORE_ENVIRONMENT="Production" `
    ApplicationInsights__ConnectionString="$appInsightsConnectionString" `
    DatabaseProvider="SqlServer" `
    ConnectionStrings__DefaultConnection="$databaseConnectionString" `
    AzureFoundry__Endpoint="$azureFoundryEndpoint" `
    AzureFoundry__ApiKey="$azureFoundryApiKey" `
    AzureFoundry__ModelId="DeepSeek-V3-0324" `
    AzureFoundry__SecondaryModelId="gpt-4o-mini" `
    AzureFoundry__UseSecondaryFallback="true" `
    AiAccessPolicy__Enabled="true" `
    AiAccessPolicy__FreeAiRequestLimit="120" `
    AiAccessPolicy__CountAnonymous="true" `
    Ollama__EnableFallback="true" `
    Ollama__Endpoint="http://localhost:11434" `
    Ollama__Model="llama3.2:3b" `
    --output none
Write-Host "[OK] Baseline settings applied" -ForegroundColor Green

Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "New Azure instance is ready" -ForegroundColor Green
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "Resource Group: $ResourceGroup"
Write-Host "Web App:        $AppName"
Write-Host "Plan:           $PlanName"
Write-Host "AI Insights:    $AppInsightsName"
Write-Host "Log Workspace:  $WorkspaceName"
Write-Host "URL:            https://$AppName.azurewebsites.net"
Write-Host ""
Write-Host "Next: deploy app using deploy-to-azure.ps1 with AzureFoundry endpoint/key values." -ForegroundColor Yellow
