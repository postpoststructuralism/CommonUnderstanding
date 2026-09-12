---
name: local-operations-dashboard
description: Extend the local-only production dashboard without creating new production load or exposing credentials.
last_updated: 2026-08-30
---

# Local Operations Dashboard

Use this pattern for changes under `CommonUnderstanding.Admin`.

## Boundaries

- Keep the application local-only and bound to `localhost`.
- Authenticate Azure Monitor calls with `DefaultAzureCredential`; never commit tokens or credentials.
- Store the production SQL connection string in user secrets and use a principal restricted to read access.
- When sourcing the local secret from App Service, check `ConnectionStrings__DefaultConnection` in application settings as well as the dedicated connection-strings collection; deployments in this repository use the application setting.
- The workstation's current public IP must be allowed by the Azure SQL firewall before local historical queries can connect. Keep that rule scoped to a single IP and remove or update it when no longer needed.
- Keep SQL queries bounded, timeout-limited, and cached. Do not add automatic polling.
- Measure hosted AI compute from the Foundry account's Azure Monitor `ModelRequests`, `InputTokens`, `OutputTokens`, and `TotalTokens` metrics. Query the whole configured account unless deployment names are validated against the live metric dimension; deployment configuration can change independently of dashboard source.
- Keep the aggregate App Service `Requests` metric as the full traffic total. Endpoint drill-down comes from cached, read-only `AppServiceHTTPLogs` queries in the existing Log Analytics workspace and requires the App Service diagnostic setting `LocalOpsHttpTraffic` with only that category enabled.
- Exclude query strings and normalize identifier-shaped URI segments before grouping endpoint logs. State that detailed history begins when diagnostic logging is enabled and can lag aggregate metrics.
- Label inferred values such as active users and process-lifetime uptime as approximations.
- Degrade unavailable data sources into concise warnings while keeping the rest of the dashboard usable.

## Verify

1. Run `dotnet build .\CommonUnderstanding.sln`.
2. Run `dotnet test .\CommonUnderstanding.Tests\CommonUnderstanding.Tests.csproj --no-build`.
3. Run the admin project in Development and inspect desktop and mobile layouts.
4. Test with missing Azure authentication and missing database secrets; both must render warnings rather than fail the page.