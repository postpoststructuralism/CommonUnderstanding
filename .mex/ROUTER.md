---
name: router
description: Session bootstrap and navigation hub. Read at the start of every session before any task. Contains project state, routing table, and behavioural contract.
edges:
  - target: context/architecture.md
    condition: when working on system design, integrations, or understanding how components connect
  - target: context/stack.md
    condition: when working with specific technologies, libraries, or making tech decisions
  - target: context/conventions.md
    condition: when writing new code, reviewing code, or unsure about project patterns
  - target: context/decisions.md
    condition: when making architectural choices or understanding why something is built a certain way
  - target: context/setup.md
    condition: when setting up the dev environment or running the project for the first time
  - target: patterns/INDEX.md
    condition: when starting a task — check the pattern index for a matching pattern file
  - target: patterns/add-mvc-feature.md
    condition: when extending an MVC, Razor, service, or SignalR feature
  - target: patterns/change-database-schema.md
    condition: when changing EF Core entities, context configuration, or migrations
  - target: patterns/change-ai-integration.md
    condition: when changing Semantic Kernel, hosted models, Ollama, fallback, or AI policy
  - target: patterns/debug-background-processing.md
    condition: when diagnosing a queue, worker, prefetch, scoring, or deferred-analysis failure
  - target: patterns/deploy-azure.md
    condition: when publishing or deploying the application to Azure App Service
last_updated: 2026-08-02
---

# Session Bootstrap

If you haven't already read `AGENTS.md`, read it now — it contains the project identity, non-negotiables, and commands.

Then read this file fully before doing anything else in this session.

## Current Project State
**Working:**
- Single .NET 9 MVC/Razor application builds successfully and serves controllers, APIs, views, static assets, SignalR hubs, and hosted workers.
- Adaptive discovery, Bayesian belief modelling, social arguments/voting/replies/chains, debate rooms, reputation/badges, widgets, and understanding-graph features are represented in the implementation.
- EF Core supports runtime-selected SQL Server or PostgreSQL; production configuration selects SQL Server.
- Semantic Kernel supports configured hosted models with local Ollama fallback and AI access policy.
- Response analysis and question prefetch queues keep interactive discovery non-blocking.

**Not yet built or verified:**
- A dedicated automated test project is not present in the solution.
- Full production smoke coverage for every social, graph, AI, and background-worker path is not documented.
- Exact completion status of roadmap documents must be checked against current implementation before treating a planned feature as absent.

**Known issues:**
- Mex 0.7.0 indexes only a small JavaScript/Python/generated subset and does not currently resolve core C# symbols; C# scaffold grounding is therefore intentionally incomplete.
- Local and production database providers differ, so provider-specific behavior can escape a single-environment check.
- Azure App Service cannot host Ollama in-process; production needs hosted AI or a separately reachable Ollama service.
- Generated `publish/`, build outputs, deployment archives, and downloaded logs coexist with source and must be excluded from implementation searches and edits.

## Routing Table

Load the relevant file based on the current task. Always load `context/architecture.md` first if not already in context this session.

| Task type | Load |
|-----------|------|
| Understanding how the system works | `context/architecture.md` |
| Working with a specific technology | `context/stack.md` |
| Writing or reviewing code | `context/conventions.md` |
| Making a design decision | `context/decisions.md` |
| Setting up or running the project | `context/setup.md` |
| Any specific task | Check `patterns/INDEX.md` for a matching pattern |
| Adding an MVC/Razor/SignalR feature | `patterns/add-mvc-feature.md` |
| Changing entities or migrations | `patterns/change-database-schema.md` |
| Changing AI provider behavior | `patterns/change-ai-integration.md` |
| Debugging queues or workers | `patterns/debug-background-processing.md` |
| Deploying to Azure | `patterns/deploy-azure.md` |

## Behavioural Contract

For every task, follow this loop:

1. **CONTEXT** — Load the relevant context file(s) from the routing table above. Check `patterns/INDEX.md` for a matching pattern. If one exists, follow it. Narrate what you load: "Loading architecture context..."
2. **BUILD** — Do the work. If a pattern exists, follow its Steps. If you are about to deviate from an established pattern, say so before writing any code — state the deviation and why.
3. **VERIFY** — Load `context/conventions.md` and run the Verify Checklist item by item. State each item and whether the output passes. Do not summarise — enumerate explicitly.
4. **DEBUG** — If verification fails or something breaks, check `patterns/INDEX.md` for a debug pattern. Follow it. Fix the issue and re-run VERIFY.
5. **GROW** — After meaningful work, run this binary checklist:
   - **Ground:** What changed in reality? Name the changed behavior, system, command, dependency, or workflow.
   - **Record:** If project state changed, update the "Current Project State" section above. If documented facts changed, update the relevant `context/` file surgically.
   - **Orient:** If this task can recur and no pattern exists, create one in `patterns/` using `patterns/README.md`, then add it to `patterns/INDEX.md`. If a pattern exists but you learned a gotcha, update it.
   - **Write:** Bump `last_updated` in every scaffold file you changed. If the why matters, run `mex log --type decision "<what changed and why>"` or `mex log "<note>"`.
