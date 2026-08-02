---
name: conventions
description: How code is written in this project — naming, structure, patterns, and style. Load when writing new code or reviewing existing code.
triggers:
  - "convention"
  - "pattern"
  - "naming"
  - "style"
  - "how should I"
  - "what's the right way"
edges:
  - target: context/architecture.md
    condition: when a convention depends on understanding the system structure
  - target: context/stack.md
    condition: when framework or provider details affect implementation style
  - target: patterns/add-mvc-feature.md
    condition: when adding a controller, view, service, or model
  - target: patterns/debug-background-processing.md
    condition: when repairing hosted-service scope, cancellation, or failure behavior
# Add only nodes that embody the documented convention; do not ground examples broadly.
# grounds_to:
#   - node: "function:<tier-1-id>"
#     fingerprint: "mh:64:<hex>"
grounds_to: []
last_updated: 2026-08-02
---

# Conventions

<!-- Read broad, ground tight. Anchor concrete symbols while keeping prose readable:
```markdown
[`someFunction()`](mex://function:<tier-1-id>)
```
-->

## Naming
- C# types and files use PascalCase and normally match (`FeedService.cs`, `UnderstandingGraphController.cs`).
- Service interfaces use the `INameService` form; concrete implementations omit the `I` prefix.
- Async methods use the `Async` suffix; private fields use `_camelCase`; public members use PascalCase.
- Feature namespaces are reflected in folders such as `Controllers/Social`, `Services/Social`, `Models/Social`, and `Services/Widget`.
- Configuration uses hierarchical PascalCase keys and double-underscore environment overrides, for example `AzureFoundry__Endpoint`.

## Structure
- Controllers and hubs are transport boundaries; domain computation belongs in injected services.
- EF Core entities and DTOs live under `Models`; persistence configuration and migrations live under `Data` and `Migrations`.
- Razor pages are grouped under `Views`; shared browser assets live under `wwwroot`; never edit generated copies under `publish` or `bin`.
- Long-running or periodic work is implemented as hosted services under `Services` or feature-specific `Workers` folders.
- Cross-cutting registrations, middleware, provider selection, and endpoint maps are centralized in `Program.cs`.

## Patterns
- **Dependency injection over construction:** register interfaces and implementations at startup, then inject them into controllers, hubs, workers, or services.
- **Provider-neutral persistence:** use EF Core APIs for application behavior and isolate SQL Server/PostgreSQL differences to provider setup or migrations.
- **Asynchronous I/O:** use `Task`-returning methods and cancellation tokens in controllers, AI calls, EF queries, and hosted workers; avoid `.Result` and `.Wait()`.
- **Configuration over secrets in source:** commit empty placeholders in appsettings and supply connection strings/API keys through user secrets or deployment settings.

## Verify Checklist
Before presenting any code:
- [ ] `dotnet build CommonUnderstanding/CommonUnderstanding.csproj --no-restore` succeeds.
- [ ] Controllers/hubs delegate domain work to services and new dependencies are registered in `Program.cs`.
- [ ] Database changes account for the configured provider and include an EF migration when schema changes.
- [ ] AI changes preserve configured provider/model selection, access policy, fallback, logging, and cancellation behavior.
- [ ] Hosted services create scopes for scoped dependencies and handle cancellation and failures without killing the process.
- [ ] Razor and JavaScript changes update source assets only, not `publish/` or `bin/` artifacts.
- [ ] No secrets, connection strings, generated archives, or downloaded logs are introduced into source changes.
