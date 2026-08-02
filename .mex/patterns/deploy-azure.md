---
name: deploy-azure
description: Publish and deploy the .NET 9 application to Azure App Service with database and AI configuration checks.
triggers:
  - "deploy Azure"
  - "App Service"
  - "production publish"
  - "deployment failure"
edges:
  - target: context/setup.md
    condition: when resolving prerequisites, commands, or configuration keys
  - target: context/decisions.md
    condition: when changing the production database or AI topology
  - target: patterns/change-database-schema.md
    condition: when the release contains an EF Core migration
  - target: patterns/change-ai-integration.md
    condition: when the release changes hosted model configuration
grounds_to: []
last_updated: 2026-08-02
---

# Deploy to Azure App Service

## Context
The current target is Linux Azure App Service running .NET 9, with SQL Server/Azure SQL and a reachable hosted AI provider or external Ollama service. Existing PowerShell deployment scripts are repository-specific operational surfaces; inspect the selected script before running it. Prefer an IaC/`azd` path for new infrastructure and preview infrastructure changes before applying them.

## Steps
1. Confirm subscription/resource group/app target, deployment script parameters, and current Azure authentication context.
2. Build and publish Release output; do not treat an old `publish/` directory as source truth.
3. Review schema changes and apply the production-provider migration with backup and roll-forward planning.
4. Set secrets in App Service/Key Vault-backed configuration, not files: connection string, hosted AI endpoint/key/model IDs, Application Insights, and `ASPNETCORE_ENVIRONMENT=Production`.
5. Confirm Linux .NET 9 runtime, health/startup behavior, and that Ollama is not assumed to run inside App Service.
6. Deploy using the established script or approved IaC workflow.
7. Validate the returned application URL, AI status, database-backed route, logs, and one representative interactive workflow.

## Gotchas
- `appsettings.Production.json` defaults are not proof that App Service settings are complete.
- A successful package upload can still fail during startup because of runtime, database, migration, or AI configuration.
- In-process queues lose work on restarts and may duplicate independently when the App Service scales out.
- Never expose secrets in shell history, deployment output, committed scripts, or logs.

## Verify
- [ ] Release build/publish succeeds from current source.
- [ ] Production uses SQL Server and the expected applied migrations.
- [ ] App settings contain no placeholder credentials and use least privilege.
- [ ] The deployed URL and representative database, AI, SignalR, and background paths work.
- [ ] App Service logs and Application Insights show no startup loop or recurring worker failure.

## Debug
Check deployment logs, startup/runtime stack, App Service settings, database reachability/migrations, and hosted AI reachability in that order. Roll forward or restore the prior known-good package/configuration when user impact is active.

## Update Scaffold
- [ ] Record the deployed behavior and known issues in `.mex/ROUTER.md`
- [ ] Update setup when deployment commands/settings change
- [ ] Log topology or provider decisions in `context/decisions.md`