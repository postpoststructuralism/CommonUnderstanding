# Multi-User Convergence Feature Plan

## Vision

While working alone, a user broadens their understanding by synthesizing other views as they come to understand those perspectives. However, to build a truly *common* understanding, personal understanding must be mapped to the personal understanding of others. This feature finds areas of convergence and overlap between users and helps them expand those areas through guided exploration.

---

## What Already Exists to Build On

| Existing Piece | Role in Multi-User Feature |
|---|---|
| `BeliefComparison` model | Already models CommonGround, Divergence, NonZeroSumOpportunity between two belief systems |
| `BeliefSnapshot` (in UserProfile) | Per-user point-in-time mental model with dimensional positions, moral foundations, values |
| `CommonUnderstandingNode` + edges | Canonical proposition graph already merges identical propositions across arguments via `ArgumentIdsJson` |
| `StakeholderPosition` | Already tracks which premises each actor accepts/rejects |
| `HarmonyDetector` | Already traverses the graph to find convergent ground between opposing stakeholders |
| `ComparativeAnalysisService` | Already does head-to-head argument comparison, finding shared/unique/conflicting premises |
| `EmergentConclusionsEngine` | Already produces harmony/blindspot reports across an argument corpus |

---

## The Core Architecture

The multi-user feature operates at three levels simultaneously:

```
Level 1: Profile Layer
  User A BeliefSnapshot ←→ User B BeliefSnapshot
      (dimensions, moral foundations, inferred values)
              ↓ BeliefComparison (already exists)

Level 2: Argument Layer
  User A's Arguments ←→ User B's Arguments
      (claims, premises, evidence)
              ↓ ComparativeAnalysisService + ArgumentComparison

Level 3: Proposition Graph Layer
  Shared CommonUnderstandingNodes
      (canonical propositions both users reference or dispute)
              ↓ HarmonyDetector finds convergent ground

              ↓ All three levels feed into:

Level 4: ConvergenceMap
  (Where do they actually agree? What's the real disagreement?
   What questions would expand the overlap?)
```

---

## New Data Models

### `UserConnection` — social graph edge
```csharp
string UserId
string ConnectedUserId
ConnectionStatus Status  // Pending | Active | Declined
DateTime InitiatedAt
DateTime? AcceptedAt
string InitiatorMessage
```

### `SharedItem` — content routing between users
```csharp
string ItemId
SharedItemType ItemType  // Argument | Analysis | EmergentReport | ConvergenceMap
string ItemReferenceId
string SharedByUserId
List<string> SharedWithUserIds
ItemVisibility Visibility  // Private | Connections | Public
string Message
DateTime SharedAt
List<SharedItemReaction> Reactions
```

### `ConvergenceMap` — central artifact of the feature
```csharp
string MapId
string User1Id
string User2Id
DateTime GeneratedAt
BeliefComparison ProfileOverlap          // reuses existing model
List<string> SharedPropositionIds        // graph nodes both users reference/accept
List<string> DisputedPropositionIds      // nodes they evaluate differently
List<DivergenceDimension> DivergencePoints
List<ExpansionPathway> ExpansionPathways
double OverallConvergenceScore           // 0-100
List<ConvergenceSnapshot> EvolutionHistory
```

### `DivergenceDimension`
```csharp
string DimensionName
double User1Position   // -1 to 1
double User2Position   // -1 to 1
double Gap             // absolute difference
bool IsValueLevel      // fundamental (true) vs. factual (false)
List<string> BridgingFrames
```

### `ExpansionPathway`
```csharp
string Title
string DivergenceDescription
List<string> SuggestedQuestions    // targeted Socratic questions
string PotentialCommonFraming
string SharedValueAnchor           // which shared value this pathway can leverage
PathwayPriority Priority           // High | Medium | Low
```

### `ConvergenceSnapshot` — historical record
```csharp
DateTime RecordedAt
double ConvergenceScore
int SharedPropositionCount
int DisputedPropositionCount
```

### `CollaborativeSession`
```csharp
string SessionId
List<string> ParticipantIds
DateTime CreatedAt
SessionStatus Status   // Active | Concluded
Dictionary<string, List<string>> ContributedArgumentIds   // UserId → ArgumentIds
List<string> MergedNodeIds
EmergentConclusionsReport ConsolidatedReport
ConvergenceMap JointConvergenceMap
```

---

## New Services

### `UserConnectionService`
Manages the social graph. Create/accept/decline connections, enumerate connections per user, enforce privacy (profiles not exposed to non-connections).

**Key methods:**
- `InitiateConnectionAsync(fromUserId, toUserId, message)`
- `AcceptConnectionAsync(connectionId)`
- `DeclineConnectionAsync(connectionId)`
- `GetConnectionsForUserAsync(userId)`
- `AreConnectedAsync(userId1, userId2)`
- `GetPendingInvitesForUserAsync(userId)`

### `ConvergenceMapService` ← core new engine

Operates at three layers:

1. **Profile layer**: Compare both users' `CurrentBeliefSnapshot` → populate `ProfileOverlap` using existing `BeliefComparison` model via `BeliefAnalysisService`
2. **Proposition layer**: Query `CommonUnderstandingNode`s where `ArgumentIdsJson` references arguments from *both* users → `SharedPropositionIds`; query `StakeholderPosition`s where the two users have opposing accepted/rejected premises → `DisputedPropositionIds`
3. **Argument layer**: Enumerate `ArgumentComparison` records for cross-user pairs → extract complementary vs. conflicting premises
4. **Synthesis**: Merge profile + proposition + argument signals into `DivergencePoints`, scored by gap magnitude × value-level weight
5. **Pathway generation**: LLM call with divergence context + shared values → generate `ExpansionPathway` list, each anchored on a shared value or moral foundation from `BeliefComparison`

**Key methods:**
- `GenerateConvergenceMapAsync(userId1, userId2)` → `ConvergenceMap`
- `RefreshConvergenceMapAsync(mapId)` → updated `ConvergenceMap`
- `GetConvergenceHistoryAsync(userId1, userId2)` → `List<ConvergenceSnapshot>`

### `ConvergenceExpansionService`

Iterative session engine — takes a `ConvergenceMap`, selects the highest-priority `ExpansionPathway`, generates targeted questions for *both* users at that divergence point (distinct questions per user, calibrated to their respective positions). As answers come in:
- Flows through existing `ResponseAnalysisEngine` + `BayesianInferenceEngine` pipeline
- Updates `BeliefSnapshot` for both users
- Re-runs `ConvergenceMapService` → convergence score grows or divergence becomes more precise
- Persists `ConvergenceSnapshot` to `EvolutionHistory`

**Key methods:**
- `StartExpansionSessionAsync(mapId)` → first question pair
- `ProcessResponseAsync(mapId, userId, interactionId)` → updates map, returns next question pair or null if session complete
- `GetSessionSummaryAsync(mapId)` → final convergence delta report

### `CollaborativeSessionService`

Orchestrates a multi-user session:

1. Each participant submits selected arguments
2. Runs `ArgumentDecompositionService` on undecomposed submissions
3. Merges propositions into joint graph space
4. Runs `ComparativeAnalysisService` on all cross-user argument pairs
5. Runs `HarmonyDetector` on merged graph
6. Runs `EmergentConclusionsEngine` over combined corpus
7. Produces joint `EmergentConclusionsReport` + multi-party `ConvergenceMap`

Real-time updates via existing `DiscoveryHub` SignalR infrastructure.

**Key methods:**
- `CreateSessionAsync(initiatorUserId, invitedUserIds)` → `CollaborativeSession`
- `ContributeArgumentsAsync(sessionId, userId, argumentIds[])`
- `RunJointAnalysisAsync(sessionId)` → updates session with merged report
- `GetSessionAsync(sessionId)` → `CollaborativeSession`

---

## New Controllers

| Controller | Key Endpoints |
|---|---|
| `ConnectionsController` | `GET /Connections`, `POST /Connections/Invite`, `POST /Connections/Accept`, `POST /Connections/Decline` |
| `SharingController` | `POST /Share`, `GET /SharedWithMe`, `GET /SharedByMe` |
| `ConvergenceController` | `GET /Convergence/{userId}` (map), `POST /Convergence/{userId}/Expand` (starts expansion session) |
| `CollaborativeSessionController` | `POST /Sessions/Create`, `GET /Sessions/{id}`, `POST /Sessions/{id}/Contribute`, `POST /Sessions/{id}/Analyze` |

---

## Views

- **Connection Feed** — what connected users have shared + convergence scores
- **Convergence Map View** — visual overlap/divergence representation; Venn-like diagram with proposition graph at center
- **Expansion Session View** — side-by-side question interface for both users, live convergence score via SignalR
- **Collaborative Session Dashboard** — real-time argument contribution + joint EmergentConclusions

---

## Implementation Phases

### Phase 1 — Identity & Sharing (prerequisite)
- `UserConnection` model + `UserConnectionService`
- `SharedItem` model + `SharingController`
- Minimal UI: share an argument with a connection, view what was shared
- **Also requires**: Persisting `UserProfile` to a durable store — `UserProfileStore` is in-memory today; multi-user feature needs stable user identities across sessions

### Phase 2 — Convergence Mapping
- `ConvergenceMap` + `DivergenceDimension` + `ExpansionPathway` models
- `ConvergenceMapService` (profile layer + proposition layer)
- `ConvergenceController` GET endpoint
- Static convergence map view

### Phase 3 — Guided Expansion
- `ConvergenceExpansionService`
- Iterative session loop with per-user targeted questions
- Live convergence score via SignalR (`DiscoveryHub`)

### Phase 4 — Collaborative Sessions
- `CollaborativeSession` model + `CollaborativeSessionService`
- Joint argument contribution + merged graph
- Collaborative `EmergentConclusionsReport`

---

## Key Design Decision: Defining "Shared Understanding"

Three options — used in combination:

1. **Same proposition text** — `CommonUnderstandingNode.NormalizedKey` already deduplicates identical propositions. Two users "share" a node if their arguments both reference it. Precise but misses semantic equivalents in different words.
2. **Same belief dimension position** — compare `BeliefSnapshot.Dimensions` directly. Fast, coarser-grained.
3. **AI semantic equivalence** — LLM-judged overlap across propositions that aren't textually identical. Most powerful, most expensive.

**Recommendation**: Use (1) + (2) together by default. Add (3) as "deep analysis" mode triggered on demand — mirroring how `HarmonyDetector` already separates graph-only and LLM-powered analysis phases.

---

## Starting Point

The single highest-value starting point is `ConvergenceMapService` wired to two existing user profiles — even before social connections exist. Invoke it directly via `/Convergence/Compare?user1=...&user2=...` to validate signal quality before investing in the social delivery layer.
