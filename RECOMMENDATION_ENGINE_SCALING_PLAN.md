# Recommendation Engine Intermediate Scaling Plan

## 1. Purpose

The current recommendation service validates the Interest, Growth, and Collective
ranking behavior, but computes too much from normalized transactional data during
each feed request. This plan moves expensive feature extraction and most candidate
discovery into SQL-backed projections maintained outside the request path.

This is an intermediate architecture. It deliberately avoids Kafka, a dedicated
feature store, and a separate vector database while preserving the current API,
user controls, explainability, and SQL Server/PostgreSQL portability.

## 2. Goals

1. Target a warm p95 below 300 ms for a 20-item recommended page.
2. Keep request work bounded as arguments, votes, links, and impressions grow.
3. Precompute argument, user, and community features at suitable frequencies.
4. Hydrate full argument cards only after selecting final argument IDs.
5. Preserve exploration, challenge selection, diversity, and explanations.
6. Fall back safely when projections are missing, stale, or unavailable.
7. Measure latency, freshness, candidate counts, and fallback frequency.

## 3. Non-Goals

- A real-time streaming platform.
- A machine-learned ranker or full Thompson sampling.
- A dedicated vector database before SQL projections are measured.
- Synchronous graph clustering after each interaction.
- Immediate consistency for every recommendation signal.

## 4. Target Architecture

```text
Argument/vote/link/evidence changes
        -> durable dirty-argument markers
        -> argument feature worker
        -> ArgumentRecommendationFeatures

Vote/impression/profile changes
        -> durable dirty-user markers
        -> user profile worker
        -> UserRecommendationProfiles + TopicAffinities

Periodic aggregate job
        -> CommunityRecommendationFeatures

Feed request
        -> load preferences and compact user profile
        -> retrieve bounded candidate IDs from indexed projections
        -> score, merge, explore, challenge, and diversity-rerank
        -> hydrate final 20 cards and current-user votes
        -> return response and enqueue impression telemetry
```

The relational database remains the source of truth. Projection rows are disposable
and rebuildable; domain correctness must never depend on them.

## 5. Persistence Model

### 5.1 `ArgumentRecommendationFeatures`

One row per social argument:

| Column | Purpose |
|---|---|
| `ArgumentId` | Primary key and FK |
| `CreatedAt` | Recency retrieval without joining the source table |
| `IsEligible` | Public, visible, and feed eligible |
| `IsAIGenerated` | Source filtering |
| `TagsJson` | Compact provider-neutral tags |
| `SchwartzValuesJson` | Challenge/value features |
| `HasCitedEvidence` | Evidence preference signal |
| `WilsonScore` / `HotScore` | Quality and velocity signals |
| `PositiveVoterBreadth` | Distinct positive voters |
| `LinkCount` / `ContradictionCount` | Graph signals |
| `StructuralScore` | Normalized graph contribution |
| `CollectiveScore` | Precomputed base Collective score |
| `FeatureVersion` | Controlled formula rebuilds |
| `ComputedAt` | Freshness and diagnostics |

Indexes:

- `(IsEligible, CreatedAt DESC)`
- `(IsEligible, CollectiveScore DESC)`
- `(IsEligible, HotScore DESC)`
- `(IsEligible, IsAIGenerated, CreatedAt DESC)`

Add `ArgumentRecommendationTags(ArgumentId, Tag)` for indexed topic retrieval.
This avoids provider-specific array operators in the serving projection.

### 5.2 `UserRecommendationProfiles`

One compact row per user:

| Column | Purpose |
|---|---|
| `UserId` | Primary key |
| `SchwartzValuesJson` | Challenge eligibility |
| `ExpertiseDomainsJson` | Inferred expertise |
| `HistoryWatermark` | Latest represented source event |
| `FeatureVersion` | Profile algorithm version |
| `ComputedAt` | Freshness |

### 5.3 `UserRecommendationTopicAffinities`

Use `(UserId, Topic)` as the primary key, with `Affinity`, `LastEngagedAt`, and
`ComputedAt`. Do not put an unbounded affinity dictionary in the profile row.

### 5.4 `CommunityRecommendationFeatures`

Start with global and topic-level rows containing:

- Trending score.
- Underexposed-quality score.
- Unresolved-contradiction score.
- Computation timestamp.

Do not add user clusters yet. Add cross-value or voting-cluster bridging later,
after enough interaction data exists to make those clusters meaningful.

### 5.5 Durable Work Tables

Use SQL work markers rather than an in-memory-only feature queue:

- `RecommendationArgumentWork(ArgumentId, RequestedAt, AttemptCount, LastError)`
- `RecommendationUserWork(UserId, RequestedAt, AttemptCount, LastError)`

The entity ID is the primary key. Repeated changes update one marker, coalescing
work. Workers claim bounded batches, process idempotently, retry with a limit, and
leave failures diagnosable. Durable markers survive restarts and scaled-out hosts.

## 6. Projection Maintenance

### Argument Features

Mark an argument dirty when it is published, edited, hidden, voted on, linked,
contradicted, synthesized, or given new evidence. Also mark it after existing
scoring workers change its denormalized scores.

`ArgumentRecommendationFeatureWorker` recomputes bounded batches with aggregate
queries. It must not load full vote or proposition collections. A periodic sweep
should enqueue missing or stale eligible arguments.

### User Features

Mark a user dirty after votes, meaningful dwell/click/comment engagement, epistemic
profile changes, or relevant worldview changes. Coalesce activity and enforce an
initial five-minute minimum refresh interval per active user.

`UserRecommendationProfileWorker` rebuilds from a bounded history window and
upserts the profile and topic-affinity rows transactionally.

### Community Features

`CommunityRecommendationFeatureWorker` runs every 15 minutes while recent users
are active and hourly otherwise. It computes topic/global trends, underexposed
quality, and unresolved contradiction demand. Eventual consistency is acceptable.

### Initial Backfill

1. Enqueue eligible arguments in bounded pages.
2. Enqueue users with recent social activity.
3. Let normal workers build projections without one unbounded transaction.
4. Report remaining, succeeded, failed, and stale counts.

Deploy and backfill projections before enabling indexed serving.

## 7. Candidate Retrieval

Retrieve compact rows from bounded pools:

- Interest: 60 recent arguments sharing top affinity topics.
- Growth: 40 arguments with partial expertise overlap and novel topics.
- Collective: 40 arguments by precomputed Collective score.
- Trending/community: 20 arguments by Hot or community score.
- Explore: 10 deterministic candidates from a recent eligible window.

Union by `ArgumentId`, retain source-lane membership, and cap the merged pool at
100. Exclude recently seen IDs using the existing `(UserId, ServedAt)` impression
index. Candidate queries must project scalar feature columns only and use no
`Include` calls. Cold-start users receive Collective, trending, recent, and Explore
pools.

## 8. Online Ranking Path

`FeedRankingService.GetFeedAsync` should:

1. Load preferences and the compact user profile.
2. Load top affinities and recently seen IDs with bounded projections.
3. Retrieve compact candidates from indexed lane pools.
4. Compute Interest and Growth from projections only.
5. Blend with precomputed Collective scores.
6. Apply deterministic exploration and challenge boosts.
7. Apply MMR to select the requested page.
8. Hydrate `SocialArguments` only for selected IDs and include the claim.
9. Query only the current user's votes for selected IDs; use denormalized tallies.
10. Restore ranked order and map cards.
11. Enqueue impression events after response-critical ranking work.

Continue using the daily deterministic seed initially. If projection updates make
offset pagination unstable, introduce a short-lived ranking/session token.

## 9. Impression Write Path

Remove impression insertion from response latency:

- Add a bounded `Channel<FeedImpressionEvent>` owned by a singleton hosted service.
- Enqueue served events without waiting for database persistence.
- Batch writes through a fresh `DbContext` with structured failure logs.
- When full, increment a dropped-telemetry metric rather than blocking the feed.

Restart loss is acceptable for impression telemetry. It is not acceptable for
feature work, which therefore uses durable SQL markers.

## 10. Freshness, Caching, and Fallback

- Argument features older than 30 minutes remain usable and are queued for refresh.
- User profiles older than 30 minutes remain usable; missing profiles use cold start.
- Community features older than two hours are ignored.
- If projections yield less than a page, fill from a compact recent-argument query.
- If indexed serving fails, log it and return the chronological feed.

Do not add Redis first. After measuring, optionally cache anonymous/global candidate
IDs for 30-60 seconds, community rows for five minutes, and user projections for
30-60 seconds. Add Redis only when multiple instances make local caching inadequate.

## 11. Implementation Phases

### Phase A: Immediate Query Repair

1. Stop eager-loading both votes and propositions for 200 candidates.
2. Use `AsSplitQuery` only as a temporary safety guard.
3. Project evidence presence with `Any`.
4. Hydrate current-user votes only after final selection.
5. Log elapsed time around every pipeline stage.

Exit: repeated local loads no longer exhibit collection-join expansion, and timing
logs identify the remaining dominant stage.

### Phase B: Argument Projection

1. Add argument features, tags, and argument-work entities and mappings.
2. Generate and review a provider-neutral migration.
3. Add idempotent recomputation, worker, stale sweep, and backfill.
4. Switch Collective and evidence scoring to projections.

Exit: ranking loads no vote, link, or proposition collections.

### Phase C: User and Community Projections

1. Add user profile, affinity, community, and user-work entities.
2. Mark dirty users at vote, engagement, and profile update boundaries.
3. Add user/community workers and resumable backfill.
4. Remove online vote-history and expertise reconstruction.

Exit: request rows and query count remain bounded regardless of lifetime history.

### Phase D: Bounded Serving and Deferred Impressions

1. Implement lane candidate queries and the merged 100-row cap.
2. Hydrate only final page IDs and current-user votes.
3. Add the impression channel and batch writer.
4. Add freshness and fallback behavior.

Exit: the endpoint performs no unbounded computation or synchronous telemetry write.

### Phase E: Measure and Roll Out

1. Shadow-compare current and indexed ranking without changing output.
2. Record overlap, lane distribution, candidate count, fallback rate, and latency.
3. Enable indexed ranking behind configuration for development, then a small cohort.
4. Tune pool sizes and thresholds from measurements, then complete rollout.
5. Remove the legacy ranker after indexed serving is stable.

## 12. Configuration

```json
{
  "Recommendation": {
    "UseIndexedServing": false,
    "ShadowCompare": false,
    "CandidatePoolLimit": 100,
    "ArgumentFeatureBatchSize": 100,
    "UserFeatureBatchSize": 50,
    "ArgumentMaxAgeMinutes": 30,
    "UserMaxAgeMinutes": 30,
    "CommunityMaxAgeMinutes": 120,
    "ImpressionQueueCapacity": 5000,
    "ImpressionBatchSize": 100
  }
}
```

## 13. Observability

Emit structured metrics or logs for:

- Total endpoint duration and each pipeline stage.
- Rows read per lane and candidates after deduplication.
- Projection age and stale/missing projection counts.
- Fallback count and reason.
- Work queue depth, oldest age, attempts, failures, and processing duration.
- Impression queue depth, dropped events, and write failures.
- Lane distribution and current/indexed rank overlap.

Do not log profile contents, argument text, or raw interaction histories.

## 14. Testing

### Unit

- Feature formulas and feature-version behavior.
- Candidate merge, lane attribution, cold start, exploration, and MMR.
- Freshness and fallback decisions.
- Dirty-marker coalescing and retry limits.

### Integration

- Projection recomputation from votes, links, evidence, and profile changes.
- Candidate queries use the intended indexes on SQL Server.
- Backfill resumes after cancellation and is idempotent.
- Impression batching does not delay the API.
- Missing/stale projections still produce a valid feed.

### Performance

- Seed realistic arguments, votes, links, impressions, and users.
- Measure cold and warm p50/p95/p99 latency and rows read.
- Verify bounded behavior as source-table volume increases by 10x.
- Compare authenticated, anonymous, filtered, and subsequent-page requests.

## 15. Rollout and Rollback

1. Ship schema and workers with `UseIndexedServing=false`.
2. Backfill and verify freshness/error metrics.
3. Enable `ShadowCompare` and inspect quality and latency differences.
4. Enable indexed serving locally and for a small production cohort.
5. Expand only when latency, fallback, and lane-distribution thresholds pass.
6. Roll back by disabling `UseIndexedServing`; projections may remain populated.

No rollout step should require dropping source data. New projection migrations must
have reversible `Down` methods, and production backfill must be bounded and resumable.

## 16. Definition of Done

- The feed request loads no broad vote, proposition, or link collections.
- Candidate retrieval and hydration are bounded by configuration/page size.
- Argument, user, and community projections are rebuildable and monitored.
- Projection work survives restart and handles retries without silent loss.
- Impression persistence is outside response latency.
- SQL Server and PostgreSQL builds/migrations remain supported.
- Focused unit/integration tests pass and a seeded performance test meets the target.
- Indexed serving can be enabled and rolled back through configuration.