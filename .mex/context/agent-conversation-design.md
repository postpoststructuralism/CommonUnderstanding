---
name: agent-conversation-design
description: Architecture and design for the conversation-first agent rewrite. The agent drives interaction; the conversation is the primary interface.
last_updated: 2026-10-21
status: implemented (phases 1-4 complete, phase 5 pending)
triggers:
  - "agent"
  - "conversation"
  - "rewrite"
  - "vnext"
  - "chat"
edges:
  - target: context/architecture.md
    condition: when understanding how the agent connects to existing services
  - target: context/stack.md
    condition: when making technology choices for the agent layer
  - target: context/conventions.md
    condition: when implementing agent or conversation code
  - target: context/decisions.md
    condition: when making architectural choices about the agent
last_updated: 2026-11-16
---

# Agent Conversation Design

## Ethos

> **"The last message the agent has communicated."**

The application becomes an **agent that drives the conversation**. The agent is the persistent, evolving presence — it remembers, learns, and works with human interlocutors to build common understanding between them. The agent mediates, facilitates, and synthesizes. It is not a tool the user operates; it is a collaborator the user converses with.

**Soothing.** The interface is calm, focused, and minimal. Not a firehose of data. Not a dashboard of metrics. A conversation that surfaces what matters when it matters.

## Core Principles

1. **Conversation-first.** The chat is the shell. Every interaction flows through dialogue. Views (graph, profile, comparisons) are toggles within the conversation, not separate navigation destinations.

2. **Agent-driven, human-steered.** The agent proactively surfaces tensions, gaps, and convergences. It proposes directions. The human decides where to go.

3. **Feature flags, not deletions.** Existing features are gated behind flags. Nothing is removed. The new experience starts minimal and proven features re-enter when they earn their place.

4. **The graph survives.** The understanding graph is a toggleable spatial view — a way to *see* the understanding being built, not a separate tool.

5. **Domain preservation.** The existing domain model (arguments, propositions, beliefs, graph nodes, evidence, provenance) remains intact. The rewrite changes presentation and orchestration, not the domain.

## Architecture

### Conversation Shell (Presentation Layer)

```
┌──────────────────────────────────────────────────┐
│  HEADER: Logo + "A Common Understanding"          │
│         [Graph Toggle] [Profile] [Sign Out]       │
├──────────────────────────────────────────────────┤
│                                                    │
│  ┌──────────────────────────────────────────────┐ │
│  │                                              │ │
│  │           CONVERSATION AREA                   │ │
│  │                                              │ │
│  │  [Agent] Welcome back. Since your last        │ │
│  │  conversation, 3 new perspectives have been   │ │
│  │  added to the understanding graph in areas    │ │
│  │  you've explored. Would you like to see       │ │
│  │  what's changed?                              │ │
│  │                                              │ │
│  │  [User] Yes, show me.                         │ │
│  │                                              │ │
│  │  [Agent] Here's what's new...                 │ │
│  │  ┌──────────────────────────────────────┐     │ │
│  │  │  [Inline card: New argument summary]  │     │ │
│  │  │  [Inline card: Convergence detected]  │     │ │
│  │  │  [Inline card: Evidence gap flagged]  │     │ │
│  │  └──────────────────────────────────────┘     │ │
│  │                                              │ │
│  └──────────────────────────────────────────────┘ │
│                                                    │
│  ┌──────────────────────────────────────────────┐ │
│  │  [Message input area]              [Send]     │ │
│  └──────────────────────────────────────────────┘ │
│                                                    │
└──────────────────────────────────────────────────┘
```

The conversation area replaces the current multi-page navigation. The header is minimal: logo, graph toggle, profile, sign out. No dropdown menus. No feature labyrinth.

### Toggleable Views

When the user (or agent) invokes a view, it overlays or replaces the conversation area temporarily. The user can always return to conversation.

| View | Trigger | Behavior |
|------|---------|----------|
| Understanding Graph | Header toggle or agent suggestion | Full-screen spatial graph overlay with camera controls. "Back to conversation" button. |
| Belief Profile | Agent surfaces it ("Here's how your worldview has evolved") | Inline card or expandable panel within conversation. |
| Comparison | Agent detects convergence/divergence | Side-by-side inline card. |
| Evidence Deep-Dive | Agent flags thin evidence | Expandable detail within conversation. |
| Emergent Conclusions | Agent surfaces findings | Scrollable inline list. |

### Agent Architecture

```
┌─────────────────────────────────────────────────┐
│                 Agent Orchestrator                │
│  (ConversationAgentService)                       │
│                                                   │
│  ┌─────────────┐  ┌──────────────┐               │
│  │ Conversation │  │  Proactive   │               │
│  │    State     │  │  Monitor     │               │
│  │  (per-user)  │  │  (background)│               │
│  └──────┬───────┘  └──────┬───────┘               │
│         │                  │                       │
│         ▼                  ▼                       │
│  ┌─────────────────────────────────────┐          │
│  │         Semantic Kernel              │          │
│  │  (existing provider chain + fallback)│          │
│  └─────────────────────────────────────┘          │
│         │                                          │
│         ▼                                          │
│  ┌─────────────────────────────────────┐          │
│  │         Tool Registry                │          │
│  │  ┌──────────┐ ┌──────────┐         │          │
│  │  │ Graph    │ │ Belief   │  ...    │          │
│  │  │ Tools    │ │ Tools    │         │          │
│  │  └──────────┘ └──────────┘         │          │
│  └─────────────────────────────────────┘          │
│         │                                          │
│         ▼                                          │
│  ┌─────────────────────────────────────┐          │
│  │      Existing Domain Services        │          │
│  │  (UnderstandingGraphService,         │          │
│  │   BeliefAnalysisService,             │          │
│  │   EmergentConclusionsEngine, etc.)   │          │
│  └─────────────────────────────────────┘          │
└─────────────────────────────────────────────────┘
```

### Conversation State Model

```csharp
public class ConversationState
{
    public string ConversationId { get; set; }
    public string UserId { get; set; }
    public List<ConversationMessage> Messages { get; set; }
    
    // Agent's working model of this user
    public UserMentalModel MentalModel { get; set; }
    
    // What the agent is currently tracking
    public List<string> ActiveTopics { get; set; }
    public List<string> PendingSurfacings { get; set; }  // things to bring up
    
    // Session context
    public DateTime LastActivity { get; set; }
    public ConversationPhase Phase { get; set; }
}

public class UserMentalModel
{
    public List<string> KnownBeliefs { get; set; }
    public List<int> ExploredArgumentIds { get; set; }
    public List<string> ValuePriorities { get; set; }
    public Dictionary<string, double> TopicConfidence { get; set; }
}

public enum ConversationPhase
{
    Onboarding,      // First conversation — agent learns about the user
    Exploring,       // User is exploring the understanding space
    DeepDive,        // Focused on a specific topic or comparison
    Reflecting       // Agent is surfacing patterns and synthesis
}
```

### Agent Loop

```
1. User sends message
2. Agent orchestrator:
   a. Appends message to conversation state
   b. Updates mental model from message content
   c. Determines intent (question, exploration, comparison, etc.)
   d. Selects relevant tools from registry
   e. Invokes Semantic Kernel with conversation context + tools
   f. Receives response (text + optional inline cards)
   g. Updates pending surfacings
3. Agent responds with:
   - Text response
   - Optional inline cards (argument summaries, comparisons, findings)
   - Optional view suggestions ("Would you like to see this on the graph?")
4. Proactive monitor (background):
   a. Periodically checks for new content in user's interest areas
   b. Detects convergences, tensions, evidence gaps
   c. Queues surfacings for next conversation turn
```

### Tool Registry

The agent has access to existing domain services through a tool interface. Each tool is a focused capability the agent can invoke:

| Tool | Existing Service | Purpose |
|------|-----------------|---------|
| `search_arguments` | UnderstandingQueryService | Find arguments by topic, value, or keyword |
| `get_argument_detail` | ArgumentDecompositionService | Get full analysis of an argument |
| `compare_positions` | ComparativeAnalysisService | Compare two arguments or worldviews |
| `get_belief_profile` | BeliefAnalysisService | Get user's current belief snapshot |
| `get_graph_region` | UnderstandingGraphService | Get nodes/edges for a region of the graph |
| `get_emergent_findings` | EmergentConclusionsEngine | Get current emergent conclusions |
| `detect_convergence` | ConvergenceMapService | Find areas of agreement between users |
| `get_evidence_status` | EvidenceClassificationService | Check evidence strength for a proposition |
| `suggest_exploration` | BlindspotDetector | Find unexplored areas relevant to user |

### Feature Flag Strategy

Use `Microsoft.FeatureManagement` (lightweight, built for ASP.NET Core) with the existing `appsettings.json` configuration pattern:

```json
{
  "FeatureFlags": {
    "ConversationShell": true,
    "LegacyNavigation": false,
    "UnderstandingGraph": true,
    "BeliefProfile": true,
    "SocialFeed": false,
    "DebateRooms": false,
    "EmergentConclusions": true,
    "ReferenceLibrary": false,
    "Widgets": false,
    "Convergence": true,
    "DiscoveryQuestionnaire": false,
    "Projects": false,
    "BaselineContent": false,
    "Provenance": false,
    "AdversarialIntegrity": false,
    "Calibration": false,
    "Steelmanning": false
  }
}
```

Flags are checked at three levels:
1. **Route registration** — disabled features don't register their routes
2. **Navigation** — disabled features don't appear in any UI
3. **Service registration** — disabled features skip DI registration (optional, for heavy services)

### Migration Path

**Phase A: Foundation (this phase)**
- Add `Microsoft.FeatureManagement` package
- Create feature flag configuration
- Gate all existing controllers behind flags
- Build the conversation shell (new layout, new CSS)
- Implement `ConversationAgentService` with basic loop
- Wire the graph as a toggleable view
- Verify: build succeeds, all existing tests pass with flags enabled

**Phase B: Agent Intelligence**
- Implement tool registry with all domain tools
- Build proactive monitor
- Implement mental model tracking
- Add inline card rendering
- Verify: agent can answer questions, surface findings, suggest exploration

**Phase C: Migration**
- Move surviving features into conversation context
- Remove legacy navigation (flag off)
- Polish soothing design system
- Verify: full test suite, browser smoke test

**Phase D: Cleanup**
- Remove feature flags for permanently disabled features
- Archive or remove dead code
- Update all documentation

## Design Decisions

### Why FeatureManagement over custom flags?
`Microsoft.FeatureManagement` is the standard ASP.NET Core approach. It supports percentage rollouts, filters, and integrates with `IConfiguration`. We only need on/off flags, but using the standard library avoids reinvention and supports future needs.

### Why keep the existing domain model?
The domain model (arguments, propositions, beliefs, graph, evidence) is the valuable part. It's been built over months with real data. The rewrite is about how users interact with it — not what it is.

### Why Semantic Kernel for the agent loop?
It's already integrated, already handles provider selection/fallback, and supports tool-calling natively. Building a separate agent framework would duplicate the AI boundary.

### Why not a SPA?
The project convention is Razor views + static JS. A SPA rewrite would be a second project, not a migration. The conversation shell can be built with Razor + HTMX or minimal JS while preserving the existing stack.

## Open Questions

1. **HTMX vs. SignalR for conversation updates?** SignalR is already in the stack and supports real-time streaming (important for AI response streaming). HTMX would be simpler but would require polling or SSE for streaming. Recommendation: use SignalR for the conversation hub (streaming responses), with HTMX for inline card loading.

2. **Conversation persistence?** Should conversation history survive server restarts? Current session is cookie-based (30 days). Recommendation: persist conversation state in the database (new `Conversations` and `ConversationMessages` tables) so the agent's mental model survives across sessions.

3. **Multi-user conversations?** The current design is single-user agent conversation. Multi-user (the agent mediating between multiple humans) is a future phase. The architecture should not preclude it.