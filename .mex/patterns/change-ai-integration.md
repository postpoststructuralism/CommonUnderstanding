---
name: change-ai-integration
description: Modify Semantic Kernel, Azure Foundry, Ollama, model fallback, or AI access policy safely.
triggers:
  - "Semantic Kernel"
  - "Azure Foundry model"
  - "Ollama"
  - "AI fallback"
  - "AI policy"
edges:
  - target: context/architecture.md
    condition: when tracing an AI call from transport through orchestration and persistence
  - target: context/stack.md
    condition: when connector versions or supported APIs matter
  - target: context/decisions.md
    condition: when changing provider-selection or fallback policy
  - target: patterns/debug-background-processing.md
    condition: when AI execution fails inside deferred work
grounds_to: []
last_updated: 2026-08-02
---

# Change AI Integration

## Context
Treat `SemanticKernelService` and runtime AI configuration as the orchestration boundary. Hosted configuration lives under `AzureFoundry`; local fallback lives under `Ollama`; access limits are configured under AI policy. Connector package versions differ, so verify APIs against the installed versions.

## Steps
1. Trace the caller into the shared AI service and identify the required response contract, model tier, and persistence side effects.
2. Extend runtime configuration rather than hard-coding an endpoint, key, model, or provider.
3. Preserve primary/secondary/local fallback ordering, timeouts, cancellation, structured parsing, and useful provider/model logging without logging prompts containing sensitive data.
4. Apply access policy and request accounting at the existing shared boundary.
5. Keep expensive work off interactive request paths; queue it when the result is not required synchronously.

## Gotchas
- Azure App Service cannot run local Ollama in-process.
- An empty key or endpoint must degrade predictably instead of failing startup unexpectedly.
- Model output is untrusted input: validate JSON/structure and tolerate malformed responses.
- Alpha Google and newer OpenAI connector APIs may differ from the base Semantic Kernel version.

## Verify
- [ ] The project builds with the installed connector versions.
- [ ] Primary, secondary, and Ollama paths have explicit success/failure behavior.
- [ ] Cancellation, timeout, policy limits, and request accounting still work.
- [ ] Logs identify provider/model/failure without exposing secrets or sensitive content.
- [ ] A focused runtime call validates response parsing.

## Debug
Use the AI status endpoint/logs to inspect resolved configuration, then test provider reachability, deployment/model ID, credentials, policy limits, fallback flags, and parser failures in order.

## Update Scaffold
- [ ] Update stack when package/provider versions change
- [ ] Log provider-policy decisions in `context/decisions.md`
- [ ] Update router state when a model path becomes operational or unavailable