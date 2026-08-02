---
name: stack
description: Technology stack, library choices, and the reasoning behind them. Load when working with specific technologies or making decisions about libraries and tools.
triggers:
  - "library"
  - "package"
  - "dependency"
  - "which tool"
  - "technology"
edges:
  - target: context/architecture.md
    condition: when locating a technology in the runtime flow
  - target: context/decisions.md
    condition: when the reasoning behind a tech choice is needed
  - target: context/conventions.md
    condition: when understanding how to use a technology in this codebase
  - target: context/setup.md
    condition: when installing or configuring a stack dependency
  - target: patterns/change-ai-integration.md
    condition: when using Semantic Kernel or model connector packages
  - target: patterns/change-database-schema.md
    condition: when using EF Core provider packages and migrations
# Broad inventory: ground only claims embodied by a small number of symbols.
# Entry shape: { node: "function:<tier-1-id>", fingerprint: "mh:64:<hex>" }
grounds_to: []
last_updated: 2026-08-02
---

# Stack

<!-- Keep grounding sparse here. For a concrete wrapper or adapter mention, use:
```markdown
[`someFunction()`](mex://function:<tier-1-id>)
```
-->

## Core Technologies
- **C# with .NET 9** — `net9.0`, nullable references and implicit usings enabled, language version set to latest.
- **ASP.NET Core MVC and Razor** — one web project serves controllers, views, APIs, static assets, and hosted services.
- **Entity Framework Core 9** — relational persistence with SQL Server 9.0.3 and Npgsql 9.0.4 providers.
- **SignalR 9** — real-time discovery, debate, voting, chain, reputation, and widget communication.
- **PowerShell and Azure CLI** — deployment automation for the Linux .NET 9 Azure App Service.

## Key Libraries
- **Microsoft.SemanticKernel 1.67.1** — AI orchestration boundary; connector versions differ, with OpenAI at 1.76.0 and Google at 1.67.1-alpha.
- **MathNet.Numerics 5.0.0** — numerical support for Bayesian and graph-analysis services.
- **EF Core providers** — use `Microsoft.EntityFrameworkCore.SqlServer` or `Npgsql.EntityFrameworkCore.PostgreSQL`, selected by `DatabaseProvider`.
- **StackExchange.Redis cache integration 9.0.0** — optional distributed caching support.
- **Bootstrap 5 and browser JavaScript** — server-rendered UI; no separate Node frontend build is present.

## What We Deliberately Do NOT Use
- No React/Vue/Angular SPA toolchain: UI changes belong in Razor views and `wwwroot` assets.
- No second application persistence abstraction: application data is modelled through EF Core, while SQL files are diagnostics and maintenance tools.
- No separate queue broker is documented: background queues and workers are in-process hosted services.

## Version Constraints
Build and Azure hosting require the .NET 9 SDK/runtime. Semantic Kernel connector versions are intentionally not uniform; verify APIs against each referenced package version before updating provider code.
