---
name: add-mvc-feature
description: Extend an MVC, Razor, service, or SignalR feature without moving domain logic into transport code.
triggers:
  - "add endpoint"
  - "add controller"
  - "add view"
  - "add hub method"
edges:
  - target: context/conventions.md
    condition: when choosing names, folders, dependency boundaries, or verification
  - target: context/architecture.md
    condition: when the feature crosses controllers, services, persistence, workers, or SignalR
grounds_to: []
last_updated: 2026-08-02
---

# Add an MVC Feature

## Context
Load architecture and conventions. Identify the nearest existing feature in `Controllers`, `Services`, `Models`, `Views`, or `Hubs`; use its folder/namespace and DI shape. Run `mex graph scope "<feature task>" --fingerprint`, but reject generated `publish/` matches and use focused source reads when C# is absent.

## Steps
1. Define or update the model/DTO at the domain boundary; do not bind EF entities directly when an input has validation or authorization concerns.
2. Put domain behavior in an existing service or add an interface and implementation beside related services.
3. Register new dependencies in `Program.cs` with a lifetime compatible with their dependencies.
4. Add the thin controller action or hub method, including authorization, validation, cancellation, and explicit failure behavior.
5. Add/update the Razor view and source JavaScript/CSS under `Views` and `wwwroot`; never edit `publish/` copies.
6. If the feature pushes updates, map or reuse the relevant SignalR hub and keep persisted state authoritative.

## Gotchas
- Scoped EF services cannot be captured by singleton workers; create a scope per unit of work.
- MVC success does not prove the SignalR or browser path works; validate both when applicable.
- Large controllers are a signal that domain logic belongs in a service.

## Verify
- [ ] `dotnet build CommonUnderstanding/CommonUnderstanding.csproj --no-restore` succeeds.
- [ ] New services are registered and lifetimes are compatible.
- [ ] Authorization, validation, cancellation, and failure paths are explicit.
- [ ] Source views/assets render at desktop and mobile widths when UI changed.
- [ ] No generated artifact was edited.

## Debug
Check endpoint/hub mapping and DI startup failures first, then action logs, model-state errors, service exceptions, EF queries, and browser/SignalR network errors in that order.

## Update Scaffold
- [ ] Update `.mex/ROUTER.md` if project state changed
- [ ] Update affected `.mex/context/` facts
- [ ] Add a narrower pattern when a new recurring boundary appears