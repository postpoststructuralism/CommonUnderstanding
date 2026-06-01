# CommonUnderstanding - Azure Foundry + Deployment Migration Plan

## 1) Goal

Migrate CommonUnderstanding from the current `OpenRouter -> Gemini -> Ollama` provider chain to an Azure-first inference stack on a new Azure instance, while preserving stability and enabling launch economics for:

- 5-10 unlimited core users (beta)
- paywalled usage for subsequent users after a free-tier allowance

This plan combines LLM provider migration and infrastructure deployment migration into one execution path.

---

## 2) Current State (Verified)

### AI provider chain in code

- `CommonUnderstanding/appsettings.json`
  - `OpenRouter`
  - `Gemini`
  - `Ollama`
- `CommonUnderstanding/Services/SemanticKernelService.cs`
  - `BuildProviders()` currently wires OpenRouter, Gemini, then Ollama
  - Includes OpenRouter-specific routing/retry logic via `RateLimitRetryHandler`
- `CommonUnderstanding/Services/FallbackChatCompletionService.cs`
  - Round-robin across configured providers

### Existing deployment assets

- Existing scripts/docs target App Service deployment
- Prior deployment history shows quota constraints on App Service plan creation in prior subscription

---

## 3) Target Architecture

## 3.1 Inference stack

Production inference order:

1. **Azure Foundry primary model** (DeepSeek-V3-0324 by default)
2. **Azure secondary model** (cost/perf fallback, e.g. GPT-4o-mini or GPT-5-nano)
3. **Ollama fallback** (optional, non-critical emergency fallback)

Remove OpenRouter and Gemini from production execution path.

## 3.2 Deployment stack (new Azure instance)

- Azure Resource Group (new)
- Azure App Service Plan + Linux Web App (or Container App if quota constraints persist)
- Azure Foundry resource/project + model deployment(s)
- Application Insights + Log Analytics
- Cost guardrails (budget + alerts)

## 3.3 Product monetization controls

- Core user allowlist: unlimited
- New users: free session allowance, then paywall
- LLM-consuming endpoints enforce quota checks before model invocation

---

## 4) Model Availability + Recommended Lineup

Use **Foundry Models sold by Azure** to keep billing within Azure subscription/credits.

Recommended lineup:

- Primary: `DeepSeek-V3-0324` (Global Standard)
- Secondary low-cost: `gpt-4o-mini` or `gpt-5-nano`
- Premium/paywalled workflows: `o4-mini` or `gpt-5-mini`

Notes:

- DeepSeek models are listed under models sold by Azure (credit-friendly path).
- Models from partners/community may require marketplace/SaaS billing behavior; avoid as default until confirmed for your subscription.

---

## 5) Migration Workstreams

## Workstream A - Azure environment readiness (new instance)

1. Select/confirm target Azure subscription for startup credits.
2. Create new resource group (example: `freedom-ledger-v2`, region: `eastus` or preferred).
3. Verify quotas before deployment:
   - App Service / VM family quota
   - Foundry model quota (TPM/RPM/concurrency)
4. Provision baseline observability:
   - Application Insights
   - Log Analytics workspace
5. Add budget + alerts:
   - Monthly budget cap (suggested: $150-$200)
   - Alert thresholds at 50/75/90/100%

Deliverable: New Azure foundation ready for app + model deployment.

## Workstream B - Azure Foundry model deployment

1. Create Foundry project/resource in target subscription.
2. Deploy models:
   - `DeepSeek-V3-0324` (primary)
   - one backup Azure model (secondary)
3. Capture endpoint URLs, API keys, deployment/model IDs.
4. Validate quotas and request limits against expected launch traffic.

Deliverable: Live model endpoints with tested auth and quotas.

## Workstream C - Application provider migration (code/config)

1. Introduce new config section(s), example:

```json
"AzureFoundry": {
  "Endpoint": "",
  "ApiKey": "",
  "ModelId": "DeepSeek-V3-0324",
  "SecondaryModelId": "gpt-4o-mini",
  "UseSecondaryFallback": true
}
```

2. Update `SemanticKernelService`:
   - Add Azure Foundry/OpenAI-compatible provider builder
   - Remove OpenRouter/Gemini provider construction in production path
   - Keep Ollama optional fallback behind config flag
3. Remove OpenRouter-specific retry/model-cycling behavior that no longer applies.
4. Update `FallbackChatCompletionService` comments and behavior to reflect Azure-first chain.
5. Update `appsettings.Production.json` and deployment-time env var mapping.

Deliverable: App uses Azure Foundry by default with controlled fallback.

## Workstream D - Usage controls and paywall enforcement

1. Add user tier model:
   - `CoreUnlimited`
   - `FreeTier`
   - `Paid`
2. Add per-user counters for LLM sessions/calls.
3. Enforce free-tier cap for non-core users before invoking AI endpoints.
4. Return structured paywall responses for UI.
5. Add admin controls for:
   - editing core-user allowlist
   - adjusting free-tier cap

Deliverable: Launch monetization controls tied directly to LLM spend.

## Workstream E - Deployment migration and cutover

1. Build and deploy app to new Azure Web App (or Container App fallback).
2. Configure app settings/secrets in target environment:
   - AzureFoundry endpoint/key/model IDs
   - DB connection string
   - ASP.NET production settings
3. Run smoke tests:
   - auth/session flows
   - argument analysis
   - discovery question flow
   - fallback behavior on forced primary failure
4. Configure custom domain + TLS (if needed).
5. Cut over traffic using one of:
   - DNS switch
   - staged slot swap
   - temporary maintenance redirect

Deliverable: Production traffic served from new Azure instance with Azure-first AI path.

---

## 6) Rollout Strategy

## Phase 0 - Dry run (1-2 days)

- Deploy to staging in new Azure instance
- Validate end-to-end AI path + logging + cost telemetry

## Phase 1 - Core users only (3-7 days)

- Route only allowlisted 5-10 core users
- Monitor:
  - latency
  - model error rates
  - spend/day

## Phase 2 - Controlled public onboarding

- Enable free-tier cap for new users
- Monitor conversion to paid + spend trajectory

## Phase 3 - Optimization

- Route lightweight prompts to cheaper model
- keep premium workflows on stronger model
- tune token limits and prompt verbosity

---

## 7) Cost + Risk Guardrails

1. Hard monthly budget in Azure Cost Management.
2. In-app hard stop guardrail (disable expensive workflows when budget threshold crossed).
3. Per-user and per-IP request throttling.
4. Retry with jitter and max attempt limits.
5. Kill switch config:
   - `AI__Mode=Off | AzureOnly | AzurePlusOllama`

---

## 8) Testing and Exit Criteria

## Functional

- All primary AI workflows succeed using Azure provider.
- Fallback works when primary model intentionally fails.
- Paywall blocks capped users as expected.

## Operational

- p95 latency within acceptable bounds for key workflows.
- No sustained 429/5xx burst under expected concurrency.
- App logs include provider/model/cost attribution fields.

## Financial

- Daily spend remains within planned envelope.
- Budget alerts tested and verified.

Exit criteria:

- OpenRouter and Gemini disabled in production config
- New Azure instance serves 100% traffic
- Core-user unlimited and non-core paywall policies active

---

## 9) Implementation Checklist

- [ ] Create new Azure resource group + deployment targets
- [ ] Provision Foundry project and deploy primary/secondary models
- [ ] Add AzureFoundry config to appsettings/environment
- [ ] Refactor `SemanticKernelService` to Azure-first provider wiring
- [ ] Remove OpenRouter/Gemini production dependencies
- [ ] Add paywall and core-user allowance enforcement
- [ ] Add spend guardrails and operational alerts
- [ ] Deploy to staging and execute smoke tests
- [ ] Deploy to production in new Azure instance
- [ ] Cut over traffic and monitor first 72 hours

---

## 10) Immediate Next Actions (Suggested Order)

1. Confirm target subscription and region for the new Azure instance.
2. Provision Foundry deployments for `DeepSeek-V3-0324` + one backup model.
3. Implement code migration in `SemanticKernelService` and production settings.
4. Add free-tier/paywall enforcement path.
5. Deploy staging and begin Phase 0 validation.
