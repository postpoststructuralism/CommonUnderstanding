---
name: change-database-schema
description: Change EF Core entities and migrations while preserving SQL Server and PostgreSQL behavior.
triggers:
  - "database migration"
  - "change entity"
  - "add column"
  - "ApplicationDbContext"
edges:
  - target: context/stack.md
    condition: when provider packages or version behavior matters
  - target: context/decisions.md
    condition: when deciding whether a change may be provider-specific
  - target: context/setup.md
    condition: when applying or troubleshooting migrations
grounds_to: []
last_updated: 2026-08-02
---

# Change Database Schema

## Context
The application selects SQL Server or PostgreSQL through `DatabaseProvider`; production currently uses SQL Server. Inspect the entity, `ApplicationDbContext`, the latest migration, and all relevant call sites. Run `mex impact <symbol|file>` before editing, but do not use irrelevant JavaScript/generated results as grounding.

## Steps
1. Change the entity and EF mapping with provider-neutral types and expressions where possible.
2. Update service/query code affected by nullability, relationships, indexes, or defaults.
3. Generate the migration for the intended provider with `dotnet ef migrations add <Name> --project CommonUnderstanding/CommonUnderstanding.csproj`.
4. Review generated `Up`, `Down`, and snapshot changes; isolate provider SQL and document why when neutrality is impossible.
5. Apply the migration to a disposable or backed-up database before production.

## Gotchas
- Local PostgreSQL success does not validate the production SQL Server migration.
- Raw SQL, vector types, filtered indexes, computed defaults, and timestamp behavior are provider-sensitive.
- Do not hand-edit the model snapshot independently of a migration.
- Root SQL files are diagnostics/maintenance scripts, not the application persistence API.

## Verify
- [ ] The project builds.
- [ ] `Up` and `Down` express the intended reversible change.
- [ ] The snapshot matches the entity model.
- [ ] The migration applies on the target provider and critical queries still work.
- [ ] Destructive production changes have a backup/roll-forward plan.

## Debug
Confirm `DatabaseProvider`, connection string, selected migration assembly, and applied migration list. Then compare generated SQL for the failing provider before changing domain code.

## Update Scaffold
- [ ] Record a changed provider decision in `context/decisions.md`
- [ ] Update setup when migration commands or prerequisites change
- [ ] Update router state after a production migration