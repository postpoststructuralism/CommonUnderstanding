---
name: architecture
description: How the major pieces of this project connect and flow. Load when working on system design, integrations, or understanding how components interact.
triggers:
  - "architecture"
  - "system design"
  - "how does X connect to Y"
  - "integration"
  - "flow"
edges:
  - target: context/stack.md
    condition: when specific technology details are needed
  - target: context/decisions.md
    condition: when understanding why the architecture is structured this way
  - target: context/setup.md
    condition: when running the application or diagnosing an environment boundary
  - target: patterns/add-mvc-feature.md
    condition: when extending a request, service, persistence, or real-time flow
  - target: patterns/change-ai-integration.md
    condition: when changing the AI orchestration boundary
  - target: patterns/debug-background-processing.md
    condition: when tracing deferred work through queues, services, persistence, and notifications
# Broad overview: keep this empty unless a claim depends on a few specific symbols.
# Entry shape: { node: "function:<tier-1-id>", fingerprint: "mh:64:<hex>" }
grounds_to: []
last_updated: 2026-08-02
---

# Architecture

<!-- Read broad, ground tight. Architecture usually grounds sparsely. When a
     specific symbol is worth navigating to, use this inline form:
```markdown
[`someFunction()`](mex://function:<tier-1-id>)
```
-->

## System Overview
Browser and embedded widget requests enter the ASP.NET Core MVC application through controllers and SignalR hubs.
Controllers coordinate discovery, debate, social argument, account, and understanding-graph workflows through injected services.
Services apply domain logic, persist state through EF Core, and invoke AI orchestration through Microsoft Semantic Kernel.
Runtime configuration selects SQL Server or PostgreSQL for persistence and selects a configured AI provider/model.
Background workers handle deferred social analysis and scoring so expensive AI work does not block interactive requests.
Razor views and static JavaScript render the result; SignalR pushes voting, debate, discovery, chain, and reputation updates to connected clients.

## Key Components
- **Discovery and belief modelling** — adaptive conversations extract belief signals and update confidence-bearing worldview models; depends on AI orchestration and statistical services.
- **Social argument platform** — feed, structured arguments, votes, replies, chains, debate rooms, reputation, and badges; depends on EF Core, background workers, and SignalR hubs.
- **Baseline content generation** — a bounded hosted worker selects canonical belief systems, generates common arguments through the shared Semantic Kernel fallback chain, publishes them under an explicitly marked AI service account, and invokes the same decomposition and adjudication service used by human social posts. Stable source keys make publication resumable and idempotent.
- **Understanding graph** — connects propositions, arguments, evidence, contradictions, syntheses, and snapshots for exploration; depends on persisted graph entities and visualization endpoints.
- **Semantic Kernel integration** — central AI boundary for local or hosted model providers; provider behavior is controlled by runtime configuration rather than direct calls from views.
- **ApplicationDbContext** — shared EF Core persistence boundary with SQL Server and PostgreSQL providers; migrations and provider-specific behavior must remain compatible with the selected deployment.

## External Dependencies
- **SQL Server / Azure SQL** — current hosted relational store and supported local provider through EF Core.
- **PostgreSQL via Npgsql** — alternate relational provider; pgvector-era data and diagnostic SQL scripts remain in the repository.
- **Ollama** — local AI runtime used for privacy-oriented development and self-hosting; normally reachable at `localhost:11434`.
- **Hosted OpenAI-compatible and Google model connectors** — optional Semantic Kernel connectors selected through configuration for hosted AI deployments.
- **Redis** — optional distributed cache integration through `Microsoft.Extensions.Caching.StackExchangeRedis`.

## What Does NOT Exist Here
- There is no separate SPA repository: the user interface is Razor views and static assets served by the ASP.NET Core application.
- There is no separately deployed worker project: hosted background services run in the web application process.
- There is no dedicated automated test project in the solution; verification currently relies on build checks and focused runtime/database diagnostics.
- The generated `publish/`, `bin/`, deployment archives, and downloaded App Service logs are artifacts, not implementation sources.
