---
name: setup
description: Dev environment setup and commands. Load when setting up the project for the first time or when environment issues arise.
triggers:
  - "setup"
  - "install"
  - "environment"
  - "getting started"
  - "how do I run"
  - "local development"
edges:
  - target: context/stack.md
    condition: when specific technology versions or library details are needed
  - target: context/architecture.md
    condition: when understanding how components connect during setup
  - target: patterns/deploy-azure.md
    condition: when preparing or troubleshooting the hosted deployment
  - target: patterns/change-database-schema.md
    condition: when configuring a provider or applying migrations
# Ground only setup behavior implemented by specific code symbols.
# Entry shape: { node: "function:<tier-1-id>", fingerprint: "mh:64:<hex>" }
grounds_to: []
last_updated: 2026-08-02
---

# Setup

<!-- Commands and environment facts need no code grounding. For a concrete symbol:
```markdown
[`someFunction()`](mex://function:<tier-1-id>)
```
-->

## Prerequisites
- .NET 9 SDK.
- SQL Server or PostgreSQL, matching `DatabaseProvider` and `ConnectionStrings:DefaultConnection`.
- Ollama with a compatible model for local AI, or configured Azure Foundry credentials/models for hosted AI.
- Azure CLI and PowerShell only when deploying to Azure App Service.

## First-time Setup
1. `dotnet restore CommonUnderstanding/CommonUnderstanding.csproj`.
2. For the default Windows development setup, ensure SQL Server LocalDB is installed. `appsettings.Development.json` selects LocalDB with Windows authentication.
3. For another database server, override `DatabaseProvider` and `ConnectionStrings:DefaultConnection` with user secrets or environment variables; do not place credentials in committed appsettings files.
4. Start Ollama and run `ollama pull llama3.2:3b`, or configure `AzureFoundry__Endpoint`, `AzureFoundry__ApiKey`, and model IDs.
5. Run `dotnet run --project CommonUnderstanding/CommonUnderstanding.csproj --launch-profile http` and open `http://localhost:5220`. Startup creates the database when absent, applies pending migrations, and idempotently seeds sample data.

## Environment Variables
- `DatabaseProvider` (required) — `SqlServer` or `PostgreSQL`.
- `ConnectionStrings__DefaultConnection` (required) — connection string for the selected provider.
- `AzureFoundry__Endpoint` and `AzureFoundry__ApiKey` (required for hosted AI) — hosted model endpoint and credential.
- `AzureFoundry__ModelId`, `AzureFoundry__SecondaryModelId`, `AzureFoundry__ProModelId` (conditional) — deployment/model identifiers used by AI tiers and fallback.
- `Ollama__Endpoint` and `Ollama__Model` (optional/configurable) — local fallback host and model.
- `ApplicationInsights__ConnectionString` (optional) — Azure telemetry.
- `ASPNETCORE_ENVIRONMENT` (optional) — selects Development or Production configuration.

## Common Commands
- `dotnet run --project CommonUnderstanding/CommonUnderstanding.csproj --launch-profile http` — starts on `http://localhost:5220`.
- `dotnet watch run --project CommonUnderstanding/CommonUnderstanding.csproj` — development server with file watching.
- `dotnet build CommonUnderstanding/CommonUnderstanding.csproj --no-restore` — focused compile verification.
- `dotnet publish CommonUnderstanding/CommonUnderstanding.csproj -c Release -o publish` — creates deployment output.
- `dotnet ef migrations add <Name> --project CommonUnderstanding/CommonUnderstanding.csproj` — creates an EF migration for the selected provider.
- `dotnet ef database update --project CommonUnderstanding/CommonUnderstanding.csproj` — applies pending migrations.
- `mex check` — validates scaffold drift after project work.

## Common Issues
**Startup exits while build succeeds:** verify the selected database is reachable, the connection string is set, and the provider matches the database before changing application code.

**LocalDB is unavailable:** install the SQL Server Express LocalDB feature, or override the development connection string with a reachable SQL Server instance.

**AI status is unavailable:** verify either Ollama is running with the configured model or all required Azure Foundry settings are present; inspect AI status and trace logging for provider/fallback failures.

**Azure deployment starts with the wrong runtime/config:** verify the Web App uses Linux .NET 9, `ASPNETCORE_ENVIRONMENT=Production`, SQL Server settings, and hosted AI credentials.
