---
name: debug-background-processing
description: Diagnose response queues, question prefetch, social analysis, scoring, and other hosted workers.
triggers:
  - "queue stuck"
  - "background worker"
  - "prefetch failed"
  - "deferred analysis"
edges:
  - target: context/architecture.md
    condition: when tracing work from the request boundary through queues and persistence
  - target: context/conventions.md
    condition: when repairing hosted-service lifetimes, cancellation, or error handling
  - target: patterns/change-ai-integration.md
    condition: when a worker failure occurs inside an AI call
grounds_to: []
last_updated: 2026-08-02
---

# Debug Background Processing

## Context
Interactive discovery enqueues response analysis and consumes prefetched questions. Other hosted workers perform social analysis and scoring in the web process. A successful enqueue is not proof that a worker completed or persisted its result.

## Steps
1. Correlate one user/item identifier from enqueue through dequeue, processing, persistence, and notification logs.
2. Confirm the hosted service started, remains alive, and observes application cancellation.
3. Inspect queue capacity/backpressure and whether batch behavior is delaying the item.
4. Verify each work item creates a DI scope before resolving EF Core or other scoped services.
5. Separate provider failures, database failures, malformed payloads, and SignalR notification failures.
6. Confirm retries are bounded and failed items remain diagnosable rather than silently disappearing.

## Gotchas
- Unhandled exceptions can terminate a `BackgroundService` and make later queue items appear lost.
- A scoped `DbContext` reused across items can cause concurrency and stale-tracking failures.
- In-memory queues lose pending work on process restart and do not coordinate across scaled-out instances.
- Prefetch cache misses should fall back without blocking the entire discovery loop.
- Baseline generation is disabled by default. Enable `BaselineContent:Enabled`, keep batches small, and use `GenerationSourceKey` plus `SourceArgumentId` to distinguish missing generation from incomplete analysis.

## Verify
- [ ] Enqueue returns promptly and one item reaches a terminal logged state.
- [ ] Worker scopes, cancellation, and exception boundaries are correct.
- [ ] Persisted state and any SignalR update agree.
- [ ] Restart behavior and queue-loss expectations are explicit.
- [ ] The project builds after the repair.

## Debug
If logs are insufficient, add temporary structured lifecycle logs around enqueue/dequeue/complete/fail using identifiers, queue depth, duration, and exception type; do not log response content or secrets.

## Update Scaffold
- [ ] Record newly discovered queue limits or failure modes
- [ ] Update architecture if processing ownership changes
- [ ] Update router state when a known worker issue is resolved