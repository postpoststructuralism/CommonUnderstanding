---
name: agents
description: Always-loaded project anchor. Read this first. Contains project identity, non-negotiables, commands, and pointer to ROUTER.md for full context.
last_updated: 2026-08-02
---

# Common Understanding

## What This Is
An ASP.NET Core application for adaptive belief discovery, structured social argument, collaborative debate, and understanding-graph analysis.

## Non-Negotiables
- Never commit database credentials, AI keys, deployment secrets, or downloaded production data/logs.
- Keep controllers and hubs thin; put domain behavior in injected services.
- Preserve SQL Server and PostgreSQL compatibility unless a provider-specific change is explicitly scoped.
- Preserve AI policy, runtime provider selection, fallback, cancellation, and failure handling.
- Never edit generated `bin/`, `publish/`, deployment archives, or downloaded App Service artifacts as source.

## Commands
- Dev: `dotnet watch run --project CommonUnderstanding/CommonUnderstanding.csproj`
- Run: `dotnet run --project CommonUnderstanding/CommonUnderstanding.csproj --launch-profile http`
- Build: `dotnet build CommonUnderstanding/CommonUnderstanding.csproj --no-restore`
- Test: no automated test project is currently present; run the focused build and relevant runtime/SQL diagnostic.
- Publish: `dotnet publish CommonUnderstanding/CommonUnderstanding.csproj -c Release -o publish`
- Scaffold: `mex check`

## Code Graph
The repo is indexed into `.mex/graph.db`. Prefer graph commands over grepping or reading files.
- Explore a task with `mex graph scope "<task>"` first — it returns a compact JSONL manifest (`meta`, `fact`s, `summary`). Treat any source the graph returns as ALREADY READ; do not re-open those files.
- Pick 1-3 relevant node ids from the manifest and expand only those with `mex graph get <id> --detail source`.
- If you already know the symbol, skip scope: use `mex graph query <who-calls|what-calls|where-defined> <symbol>`, or `mex graph get <id>`.
- Before editing a symbol, run `mex impact <symbol|file>` to see affected callers and scaffold memory.
- If a result is `truncated`, do NOT repeat the broad query — narrow the task or use the summary's `suggestedNextCommands`. Scale through a few focused calls, never one giant response.
- During `mex sync`, adjudicate any AMBIGUOUS grounding; after repairs, ensure the refreshed grounding is re-emitted.
- Current limitation: mex 0.7.0 does not resolve the core C# symbols in this repository and often returns JavaScript or generated `publish/` nodes. Never invent grounding; use focused file/symbol tools when graph results are irrelevant.

## Scaffold Growth
After meaningful work, run GROW:
- Ground: what changed in reality?
- Record: update `ROUTER.md` and relevant `context/` files
- Orient: create or update a `patterns/` runbook if this can recur
- Write: bump `last_updated` on changed scaffold files and run `mex log` when rationale matters

The scaffold grows from real work, not just setup. See the GROW step in `ROUTER.md` for details.

## Navigation
At the start of every session, read `ROUTER.md` before doing anything else.
For full project context, patterns, and task guidance — everything is there.
