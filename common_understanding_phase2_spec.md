# A Common Understanding — Phase 2 Specification

**Document Version:** 1.0  
**Date:** June 19, 2026  
**Status:** Draft — For Developer Review  
**Audience:** Senior .NET/C#/Blazor/ASP.NET Core developer  

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Core Concepts & Terminology](#2-core-concepts--terminology)
3. [Data Model (EF Core / PostgreSQL)](#3-data-model-ef-core--postgresql)
4. [API Design](#4-api-design)
5. [UI / UX Specification](#5-ui--ux-specification)
6. [AI / Semantic Kernel Integration](#6-ai--semantic-kernel-integration)
7. [PostgreSQL / pgvector Schema](#7-postgresql--pgvector-schema)
8. [SignalR Hubs (Phase 2)](#8-signalr-hubs-phase-2)
9. [Voting Algorithm](#9-voting-algorithm)
10. [Gamification System](#10-gamification-system)
11. [Moderation & Trust](#11-moderation--trust)
12. [Phase 2 Implementation Roadmap](#12-phase-2-implementation-roadmap)
13. [Non-Functional Requirements](#13-non-functional-requirements)
14. [Open Questions / Decision Points](#14-open-questions--decision-points)

---

## 1. Executive Summary

### 1.1 Phase 2 Goals

Phase 2 transforms *A Common Understanding* from a single-user belief-mapping tool into a multi-user social reasoning platform. The core objective is to let users build, share, and compare structured worldviews — and to surface genuine epistemic convergence between people who disagree.

**Success criteria:**

| Criterion | Target |
|---|---|
| Feed renders with vote tallies in < 300ms p95 | Performance gate for Sprint 1 release |
| Users can publish a named Worldview composed of ≥ 2 Argument Chains | Feature completeness |
| Convergence score is computed between any two public Worldviews | Feature completeness |
| At least one Debate Room completes a full structured session end-to-end | Integration smoke test |
| Epistemic scores update within 60 seconds of a vote being cast | Real-time correctness |
| Zero raw-SQL injection surfaces (all DB access through EF Core parameterized queries) | Security gate |

### 1.2 Key Differentiator from Reddit

Reddit's ranking system optimizes for emotional engagement and novelty. *A Common Understanding* Phase 2 ranks by **epistemic quality**: argument structure, logical validity, source quality, and the credibility of the voter — not just raw vote counts.

Specific structural differences:

- **Every upvote is attached to a reasoning trace.** A user must pick one of four vote rationales (Well-Sourced, Logically Valid, Changed My View, Fallacious) before the vote is recorded. Anonymous drive-by voting is not supported.
- **Vote weight is non-uniform.** A user with high Epistemic Standing in the relevant topic domain casts a heavier vote than a newcomer (configurable multiplier, default 2×).
- **Arguments are structured, not free-form.** Every top-level post is decomposed into Propositions with typed roles (Claim, Evidence, Warrant, Rebuttal). The AI validates this decomposition before the argument reaches the feed.
- **The feed surfaces convergence, not conflict.** The default sort ("Common Ground") shows arguments where divergent users have upvoted the same proposition. Controversy is a secondary sort, not the default.

### 1.3 Relationship to Phase 1

Phase 2 is an **extension, not a replacement**. All Phase 1 data entities (`Argument`, `BeliefSystem`, `CollaborativeSession`, `AITraceEntry`) are preserved and migrated. The Phase 1 `Argument` entity becomes the primary content unit that feeds into Phase 2's voting, chaining, and worldview composition features.

The existing design system (cream `#f5f2eb`, navy `#162131`, teal `#0f766e`, amber `#c07a3f`, glassmorphism cards) is carried forward without modification. All new components follow the same visual grammar.

Phase 1 users retain their data. The `BeliefSystem` entity is superseded by `Worldview` (richer, public-shareable), but existing `BeliefSystem` records are migrated into private `Worldview` records during Sprint 1.

---

## 2. Core Concepts & Terminology

### 2.1 Proposition

**Definition:** An atomic, single truth-claim unit that cannot be meaningfully divided further without losing its logical function.

A Proposition has exactly one `PropositionType`:

| Type | Description | Example |
|---|---|---|
| `Claim` | The central assertion being argued | "Universal Basic Income reduces poverty." |
| `Evidence` | An empirical or factual statement supporting or attacking a claim | "Stockton SEED trial showed median income rose 28%." |
| `Warrant` | The logical principle connecting evidence to the claim | "If income supplementation is shown to reduce poverty in controlled trials, then it reduces poverty." |
| `Rebuttal` | A counter-claim attacking a specific Proposition in the same argument | "The Stockton trial had selection bias; participants were not randomly sampled." |

A Proposition is the **atomic unit** of the platform. Arguments are composed from Propositions. Propositions can be reused across multiple Arguments (many-to-many). AI-generated Propositions are flagged with `IsAIGenerated = true` and are held for user confirmation before being made public.

### 2.2 Argument

**Definition:** A structured set of Propositions forming a complete reasoning unit: a Claim supported by Evidence, connected by a Warrant, with optional Rebuttals and a Resolution.

An Argument maps directly to the Phase 1 `Argument` entity with schema extensions. It is the primary social object in the feed: votable, linkable, chainable, and taggable.

An Argument is **not** a free-form comment or opinion. It must have at minimum:
- One `Claim` Proposition
- One `Evidence` Proposition
- A `WarrantText` explaining the inferential link

An Argument without these three components is rejected by the API with a `400 Bad Request` and a structured validation error.

### 2.3 Argument Chain

**Definition:** A directed acyclic graph (DAG) of linked Arguments where at least one Argument's conclusion (its `ResolutionText`) feeds into another Argument's premise (one of its `EvidencePropositions`).

An Argument Chain represents a multi-step reasoning process: premise → intermediate conclusions → final conclusion. Chains may branch (multiple arguments supporting the same conclusion) and merge (one argument serving as evidence in multiple downstream arguments), but must remain acyclic. Cycles are rejected at the `ArgumentLink` creation step.

An Argument Chain has an explicit `RootArgumentId` — the terminal conclusion that the entire chain supports.

### 2.4 Worldview

**Definition:** A named, curated, user-authored collection of Argument Chains that together represent a coherent belief system. A Worldview is the unit of comparison in convergence analysis.

A Worldview is analogous to a "subreddit" in structure but not in function: it is owned by one user, optionally published publicly, and is compared against other users' Worldviews to compute Convergence Scores.

A Worldview carries an aggregate `SchwartzValues[]` array derived from the union of Schwartz values across all its constituent Arguments. This array feeds the radar chart in the Worldview Composer UI.

A Worldview also carries an embedding vector (`vector(1536)`) computed as the centroid of all its constituent Argument embeddings. This vector is used for semantic convergence scoring.

### 2.5 Convergence Score

**Definition:** A scalar value in [0, 1] representing the quantified overlap between two Worldviews, computed as a combination of:

1. **Semantic similarity** (cosine similarity between Worldview embedding vectors, weight: 0.4)
2. **Shared Argument overlap** (Jaccard index of ArgumentId sets across both Worldviews, weight: 0.3)
3. **Schwartz value alignment** (cosine similarity between 10-dimensional Schwartz value vectors, weight: 0.3)

The final score is:

```
ConvergenceScore = 0.4 * semantic_cosine + 0.3 * argument_jaccard + 0.3 * schwartz_cosine
```

Scores ≥ 0.7 are classified as "Strong Convergence." Scores 0.4–0.69 are "Partial Convergence." Scores < 0.4 are "Divergent."

### 2.6 Epistemic Standing

**Definition:** A per-user, per-topic-domain reputation score derived from the historical accuracy of the user's votes (relative to eventual community consensus) and the quality of their submitted Arguments.

Epistemic Standing is computed on a rolling 90-day window. It ranges from 0.0 to 5.0 and is stored in the `EpistemicProfile` entity. It governs:
- The weight multiplier applied to that user's votes in the domain
- Access to advanced Debate Room roles (Judge requires Epistemic Standing ≥ 3.0 in the debate topic)
- Visual credibility badges on the user's profile card

### 2.7 Debate Room

**Definition:** A bounded, structured, real-time debate session between a Proponent and an Opponent on a specific Motion, moderated by one or more Judges and an AI referee.

A Debate Room is not a chat thread. It follows a structured format (configurable: Oxford, Lincoln-Douglas, or Custom) with ordered speaking turns, time limits per contribution, and a scoring rubric. All contributions must be submitted as references to existing Arguments in the system — no ad-hoc text-only posts.

---

## 3. Data Model (EF Core / PostgreSQL)

All entities inherit from a common `BaseEntity` abstract class:

```csharp
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

### 3.1 Proposition

```csharp
public enum PropositionType
{
    Claim,
    Evidence,
    Warrant,
    Rebuttal
}

public class Proposition : BaseEntity
{
    public string Text { get; set; } = null!;
    public PropositionType Type { get; set; }
    public string? SourceUrl { get; set; }
    public string UserId { get; set; } = null!;
    public bool IsAIGenerated { get; set; } = false;
    public bool IsConfirmed { get; set; } = false;   // AI-generated props require user confirmation

    // pgvector embedding — stored separately in EF via HasColumnType("vector(1536)")
    public float[]? Embedding { get; set; }

    // Navigation
    public ApplicationUser User { get; set; } = null!;
    public ICollection<ArgumentProposition> ArgumentPropositions { get; set; } = new List<ArgumentProposition>();
}
```

### 3.2 Argument (Extended from Phase 1)

```csharp
public class Argument : BaseEntity
{
    public string Title { get; set; } = null!;
    public Guid ClaimPropositionId { get; set; }
    public string WarrantText { get; set; } = null!;
    public string? ResolutionText { get; set; }

    public double Weight { get; set; } = 1.0;
    public bool IsPublic { get; set; } = false;

    public string UserId { get; set; } = null!;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Denormalized vote tallies — updated asynchronously by VotingHub consumer
    public int UpvoteCount { get; set; } = 0;
    public int DownvoteCount { get; set; } = 0;
    public double HotScore { get; set; } = 0.0;
    public double WilsonScore { get; set; } = 0.0;

    // Stored as jsonb in PostgreSQL
    public string[] Tags { get; set; } = Array.Empty<string>();
    public string[] SchwartzValues { get; set; } = Array.Empty<string>();

    // pgvector embedding
    public float[]? Embedding { get; set; }

    // Navigation
    public Proposition ClaimProposition { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
    public ICollection<ArgumentProposition> ArgumentPropositions { get; set; } = new List<ArgumentProposition>();
    public ICollection<ArgumentVote> Votes { get; set; } = new List<ArgumentVote>();
    public ICollection<ArgumentLink> OutboundLinks { get; set; } = new List<ArgumentLink>();
    public ICollection<ArgumentLink> InboundLinks { get; set; } = new List<ArgumentLink>();
}

// Join table for Argument ↔ Proposition (many-to-many)
public class ArgumentProposition
{
    public Guid ArgumentId { get; set; }
    public Guid PropositionId { get; set; }
    public PropositionType Role { get; set; }
    public int OrderIndex { get; set; }

    public Argument Argument { get; set; } = null!;
    public Proposition Proposition { get; set; } = null!;
}
```

### 3.3 ArgumentLink

```csharp
public enum LinkType
{
    Supports,
    Contradicts,
    Refines,
    Extends
}

public class ArgumentLink : BaseEntity
{
    public Guid SourceArgumentId { get; set; }
    public Guid TargetArgumentId { get; set; }
    public LinkType LinkType { get; set; }
    public string? Annotation { get; set; }
    public string UserId { get; set; } = null!;

    // Navigation
    public Argument SourceArgument { get; set; } = null!;
    public Argument TargetArgument { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}
```

**Constraint:** Source ≠ Target (enforced at DB level with a check constraint). Cycle detection is performed at the API layer before insert.

### 3.4 ArgumentVote

```csharp
public enum VoteValue
{
    Up,
    Down,
    Abstain
}

public enum VoteRationale
{
    WellSourced,
    LogicallyValid,
    ChangedMyView,
    Fallacious,
    OffTopic,
    Abstained
}

public class ArgumentVote : BaseEntity
{
    public Guid ArgumentId { get; set; }
    public string UserId { get; set; } = null!;
    public VoteValue Vote { get; set; }
    public VoteRationale Rationale { get; set; }
    public string? Comment { get; set; }

    // Computed at insert time from the user's EpistemicProfile in the argument's primary topic
    public double EpistemicWeight { get; set; } = 1.0;

    // Navigation
    public Argument Argument { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}
```

**Unique constraint:** One vote per `(ArgumentId, UserId)`. Voting again updates the existing record (upsert).

### 3.5 ArgumentChain

```csharp
public class ArgumentChain : BaseEntity
{
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public Guid RootArgumentId { get; set; }
    public bool IsPublic { get; set; } = false;
    public string UserId { get; set; } = null!;

    // Stored as jsonb
    public string[] Tags { get; set; } = Array.Empty<string>();

    // Ordered list of ArgumentIds in the chain — stored as uuid[] in PostgreSQL
    public Guid[] ArgumentIds { get; set; } = Array.Empty<Guid>();

    // pgvector embedding (centroid of argument embeddings)
    public float[]? Embedding { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Argument RootArgument { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
    public ICollection<WorldviewChain> WorldviewChains { get; set; } = new List<WorldviewChain>();
}
```

### 3.6 Worldview

```csharp
public class Worldview : BaseEntity
{
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string UserId { get; set; } = null!;
    public bool IsPublic { get; set; } = false;

    // Stored as jsonb
    public string[] Tags { get; set; } = Array.Empty<string>();
    public string[] SchwartzValues { get; set; } = Array.Empty<string>();

    // Denormalized Schwartz value dimension scores for radar chart (10-dim vector)
    public double[] SchwartzVector { get; set; } = new double[10];

    // pgvector embedding (centroid of all constituent argument embeddings)
    public float[]? Embedding { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ApplicationUser User { get; set; } = null!;
    public ICollection<WorldviewChain> WorldviewChains { get; set; } = new List<WorldviewChain>();
    public ICollection<WorldviewVote> Votes { get; set; } = new List<WorldviewVote>();
}

// Join table for Worldview ↔ ArgumentChain (many-to-many, ordered)
public class WorldviewChain
{
    public Guid WorldviewId { get; set; }
    public Guid ArgumentChainId { get; set; }
    public int OrderIndex { get; set; }

    public Worldview Worldview { get; set; } = null!;
    public ArgumentChain ArgumentChain { get; set; } = null!;
}
```

### 3.7 WorldviewVote

```csharp
public class WorldviewVote : BaseEntity
{
    public Guid WorldviewId { get; set; }
    public string UserId { get; set; } = null!;
    public VoteValue Vote { get; set; }

    // Navigation
    public Worldview Worldview { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}
```

### 3.8 DebateRoom

```csharp
public enum DebateStatus
{
    Open,
    Active,
    Concluded,
    Cancelled
}

public enum DebateFormat
{
    Oxford,
    LincolnDouglas,
    Custom
}

public class DebateRoom : BaseEntity
{
    public string Title { get; set; } = null!;
    public string Topic { get; set; } = null!;
    public string MotionText { get; set; } = null!;
    public Guid? MotionPropositionId { get; set; }

    public string ProponentUserId { get; set; } = null!;
    public string? OpponentUserId { get; set; }

    // Stored as text[] in PostgreSQL
    public string[] JudgeUserIds { get; set; } = Array.Empty<string>();

    public DebateStatus Status { get; set; } = DebateStatus.Open;
    public DebateFormat Format { get; set; } = DebateFormat.Oxford;

    // Per-contribution time limit in seconds
    public int TimeLimitSeconds { get; set; } = 300;
    public int MaxContributionsPerSide { get; set; } = 5;

    public DateTime? ConcludedAt { get; set; }

    // Scoring
    public double? ProponentScore { get; set; }
    public double? OpponentScore { get; set; }

    // AI referee enabled flag
    public bool AIRefereeEnabled { get; set; } = true;

    // Navigation
    public ApplicationUser Proponent { get; set; } = null!;
    public ApplicationUser? Opponent { get; set; }
    public Proposition? MotionProposition { get; set; }
    public ICollection<DebateContribution> Contributions { get; set; } = new List<DebateContribution>();
}
```

### 3.9 DebateContribution

```csharp
public enum DebateRole
{
    Proponent,
    Opponent,
    Rebuttal,
    JudgeComment
}

public class DebateContribution : BaseEntity
{
    public Guid DebateRoomId { get; set; }
    public string UserId { get; set; } = null!;
    public Guid ArgumentId { get; set; }
    public DebateRole Role { get; set; }
    public int OrderIndex { get; set; }

    // AI referee outputs — stored as jsonb
    public string? FallacyFlags { get; set; }
    public double? ValidityScore { get; set; }
    public string? AIRefereeComment { get; set; }

    // Navigation
    public DebateRoom DebateRoom { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
    public Argument Argument { get; set; } = null!;
}
```

### 3.10 EpistemicProfile

```csharp
public class EpistemicProfile : BaseEntity
{
    public string UserId { get; set; } = null!;
    public string TopicDomain { get; set; } = null!;
    public double EpistemicScore { get; set; } = 1.0;   // Range: 0.0–5.0

    // Rolling 90-day vote accuracy: fraction of user's votes that aligned with community consensus
    public double VoteAccuracy { get; set; } = 0.5;
    public int ContributionCount { get; set; } = 0;
    public int VoteCount { get; set; } = 0;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ApplicationUser User { get; set; } = null!;
}
```

**Unique constraint:** `(UserId, TopicDomain)`.

### 3.11 UserReputation

```csharp
public class UserReputation : BaseEntity
{
    public string UserId { get; set; } = null!;
    public long XP { get; set; } = 0;
    public string Rank { get; set; } = "Novice";

    // Stored as jsonb: array of badge identifiers
    public string[] Badges { get; set; } = Array.Empty<string>();

    public int CurrentStreak { get; set; } = 0;
    public int LongestStreak { get; set; } = 0;
    public DateTime? LastStreakDate { get; set; }
    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ApplicationUser User { get; set; } = null!;
}
```

### 3.12 Moderator

```csharp
public class Moderator : BaseEntity
{
    public string UserId { get; set; } = null!;
    public string? TopicDomain { get; set; }   // null = global moderator
    public string GrantedByUserId { get; set; } = null!;
    public bool IsActive { get; set; } = true;

    // Navigation
    public ApplicationUser User { get; set; } = null!;
    public ApplicationUser GrantedBy { get; set; } = null!;
}
```

### 3.13 ModerationFlag

```csharp
public enum FlagReason
{
    Fallacious,
    Toxic,
    Spam,
    OffTopic,
    Misinformation,
    Other
}

public enum FlagStatus
{
    Pending,
    UnderReview,
    Dismissed,
    ActionTaken
}

public class ModerationFlag : BaseEntity
{
    public string EntityType { get; set; } = null!;   // "Argument" | "Proposition" | "DebateContribution"
    public Guid EntityId { get; set; }
    public string FlaggingUserId { get; set; } = null!;
    public FlagReason Reason { get; set; }
    public string? Notes { get; set; }
    public FlagStatus Status { get; set; } = FlagStatus.Pending;
    public string? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }

    // Navigation
    public ApplicationUser FlaggingUser { get; set; } = null!;
}
```

### 3.14 EF Core DbContext Additions

```csharp
// Add to ApplicationDbContext
public DbSet<Proposition> Propositions => Set<Proposition>();
public DbSet<ArgumentProposition> ArgumentPropositions => Set<ArgumentProposition>();
public DbSet<ArgumentLink> ArgumentLinks => Set<ArgumentLink>();
public DbSet<ArgumentVote> ArgumentVotes => Set<ArgumentVote>();
public DbSet<ArgumentChain> ArgumentChains => Set<ArgumentChain>();
public DbSet<Worldview> Worldviews => Set<Worldview>();
public DbSet<WorldviewChain> WorldviewChains => Set<WorldviewChain>();
public DbSet<WorldviewVote> WorldviewVotes => Set<WorldviewVote>();
public DbSet<DebateRoom> DebateRooms => Set<DebateRoom>();
public DbSet<DebateContribution> DebateContributions => Set<DebateContribution>();
public DbSet<EpistemicProfile> EpistemicProfiles => Set<EpistemicProfile>();
public DbSet<UserReputation> UserReputations => Set<UserReputation>();
public DbSet<Moderator> Moderators => Set<Moderator>();
public DbSet<ModerationFlag> ModerationFlags => Set<ModerationFlag>();

protected override void OnModelCreating(ModelBuilder builder)
{
    base.OnModelCreating(builder);

    // Proposition embedding column
    builder.Entity<Proposition>()
        .Property(p => p.Embedding)
        .HasColumnType("vector(1536)");

    // Argument embedding column
    builder.Entity<Argument>()
        .Property(a => a.Embedding)
        .HasColumnType("vector(1536)");
    
    // Argument jsonb columns
    builder.Entity<Argument>()
        .Property(a => a.Tags)
        .HasColumnType("text[]");
    builder.Entity<Argument>()
        .Property(a => a.SchwartzValues)
        .HasColumnType("text[]");

    // ArgumentProposition composite key
    builder.Entity<ArgumentProposition>()
        .HasKey(ap => new { ap.ArgumentId, ap.PropositionId });

    // ArgumentVote unique constraint
    builder.Entity<ArgumentVote>()
        .HasIndex(av => new { av.ArgumentId, av.UserId })
        .IsUnique();

    // WorldviewChain composite key
    builder.Entity<WorldviewChain>()
        .HasKey(wc => new { wc.WorldviewId, wc.ArgumentChainId });

    // WorldviewVote unique constraint
    builder.Entity<WorldviewVote>()
        .HasIndex(wv => new { wv.WorldviewId, wv.UserId })
        .IsUnique();

    // EpistemicProfile unique constraint
    builder.Entity<EpistemicProfile>()
        .HasIndex(ep => new { ep.UserId, ep.TopicDomain })
        .IsUnique();

    // ArgumentLink self-referencing
    builder.Entity<ArgumentLink>()
        .HasOne(al => al.SourceArgument)
        .WithMany(a => a.OutboundLinks)
        .HasForeignKey(al => al.SourceArgumentId)
        .OnDelete(DeleteBehavior.Cascade);

    builder.Entity<ArgumentLink>()
        .HasOne(al => al.TargetArgument)
        .WithMany(a => a.InboundLinks)
        .HasForeignKey(al => al.TargetArgumentId)
        .OnDelete(DeleteBehavior.Restrict);

    // Check constraint: ArgumentLink source ≠ target
    builder.Entity<ArgumentLink>()
        .ToTable(t => t.HasCheckConstraint(
            "CK_ArgumentLink_NoSelfLoop",
            "\"SourceArgumentId\" <> \"TargetArgumentId\""));
}
```

---

## 4. API Design

All controllers return `ProblemDetails` on error (RFC 7807). All mutation endpoints require `[Authorize]` unless noted. Responses use `application/json`. Pagination uses cursor-based paging (`?cursor=<opaque>&limit=<int>`).

### 4.1 PropositionController

Base route: `/api/propositions`

| Verb | Route | Description | Auth |
|---|---|---|---|
| `GET` | `/api/propositions` | List public propositions. Query: `type`, `search`, `cursor`, `limit` | Optional |
| `GET` | `/api/propositions/{id}` | Get a single proposition by ID | Optional |
| `POST` | `/api/propositions` | Create a new proposition | Required |
| `PUT` | `/api/propositions/{id}` | Update own proposition (before any argument uses it) | Required (owner) |
| `DELETE` | `/api/propositions/{id}` | Delete own proposition (if unused) | Required (owner) |
| `POST` | `/api/propositions/{id}/confirm` | Confirm an AI-generated proposition | Required (owner) |
| `GET` | `/api/propositions/{id}/similar` | Find semantically similar propositions via pgvector. Query: `limit`, `threshold` | Optional |

**POST /api/propositions — Request body:**
```json
{
  "text": "string (required, 10–2000 chars)",
  "type": "Claim | Evidence | Warrant | Rebuttal",
  "sourceUrl": "string? (must be valid URL if provided)"
}
```

**GET /api/propositions/{id}/similar — Response:**
```json
{
  "items": [
    {
      "id": "uuid",
      "text": "string",
      "type": "string",
      "similarityScore": 0.0
    }
  ]
}
```

### 4.2 ArgumentVoteController

Base route: `/api/arguments/{argumentId}/votes`

| Verb | Route | Description | Auth |
|---|---|---|---|
| `GET` | `/api/arguments/{argumentId}/votes/tally` | Get vote tally: upvotes, downvotes, wilsonScore, hotScore | Optional |
| `POST` | `/api/arguments/{argumentId}/votes` | Cast or update vote | Required |
| `DELETE` | `/api/arguments/{argumentId}/votes` | Retract vote (sets to Abstain) | Required |
| `GET` | `/api/arguments/{argumentId}/votes/mine` | Get caller's current vote on this argument | Required |

**POST /api/arguments/{argumentId}/votes — Request body:**
```json
{
  "vote": "Up | Down | Abstain",
  "rationale": "WellSourced | LogicallyValid | ChangedMyView | Fallacious | OffTopic | Abstained",
  "comment": "string? (max 500 chars)"
}
```

**GET /api/arguments/{argumentId}/votes/tally — Response:**
```json
{
  "argumentId": "uuid",
  "upvotes": 0,
  "downvotes": 0,
  "epistemicWeightedUpvotes": 0.0,
  "wilsonScore": 0.0,
  "hotScore": 0.0,
  "totalVotes": 0
}
```

**Rate limiting:** Maximum 30 votes per user per hour across all arguments (enforced via Redis sliding window). Exceeds → `429 Too Many Requests`.

### 4.3 ArgumentLinkController

Base route: `/api/argumentlinks`

| Verb | Route | Description | Auth |
|---|---|---|---|
| `GET` | `/api/argumentlinks` | Query links. Params: `sourceId`, `targetId`, `linkType`, `cursor`, `limit` | Optional |
| `POST` | `/api/argumentlinks` | Create a new link between two arguments | Required |
| `DELETE` | `/api/argumentlinks/{id}` | Delete own link | Required (owner) |
| `GET` | `/api/arguments/{id}/graph` | Get full argument graph (N hops). Params: `depth` (default 2, max 5) | Optional |
| `POST` | `/api/argumentlinks/suggest` | Ask AI to suggest links for a given argument | Required |

**POST /api/argumentlinks — Request body:**
```json
{
  "sourceArgumentId": "uuid",
  "targetArgumentId": "uuid",
  "linkType": "Supports | Contradicts | Refines | Extends",
  "annotation": "string? (max 500 chars)"
}
```

The API rejects the request with `409 Conflict` if the link would create a cycle in the graph. Cycle detection uses a BFS from `targetArgumentId` checking if `sourceArgumentId` is reachable before inserting.

**GET /api/arguments/{id}/graph — Response:**
```json
{
  "nodes": [
    { "id": "uuid", "title": "string", "score": 0.0, "userId": "string" }
  ],
  "edges": [
    { "id": "uuid", "source": "uuid", "target": "uuid", "linkType": "string" }
  ]
}
```

### 4.4 ArgumentChainController

Base route: `/api/argumentchains`

| Verb | Route | Description | Auth |
|---|---|---|---|
| `GET` | `/api/argumentchains` | List public chains. Params: `tags`, `search`, `userId`, `cursor`, `limit` | Optional |
| `GET` | `/api/argumentchains/{id}` | Get chain with full argument graph | Optional (private chains: owner only) |
| `POST` | `/api/argumentchains` | Create chain | Required |
| `PUT` | `/api/argumentchains/{id}` | Update chain metadata and argument list | Required (owner) |
| `DELETE` | `/api/argumentchains/{id}` | Delete chain | Required (owner) |
| `POST` | `/api/argumentchains/{id}/arguments` | Add an argument to the chain | Required (owner) |
| `DELETE` | `/api/argumentchains/{id}/arguments/{argumentId}` | Remove an argument from the chain | Required (owner) |
| `POST` | `/api/argumentchains/{id}/publish` | Make chain public | Required (owner) |
| `GET` | `/api/argumentchains/{id}/export` | Export chain as JSON | Optional |

**POST /api/argumentchains — Request body:**
```json
{
  "title": "string (required, max 200 chars)",
  "description": "string? (max 2000 chars)",
  "rootArgumentId": "uuid (required)",
  "argumentIds": ["uuid"],
  "tags": ["string"],
  "isPublic": false
}
```

### 4.5 WorldviewController

Base route: `/api/worldviews`

| Verb | Route | Description | Auth |
|---|---|---|---|
| `GET` | `/api/worldviews` | List public worldviews. Params: `tags`, `schwartzValues`, `userId`, `cursor`, `limit` | Optional |
| `GET` | `/api/worldviews/{id}` | Get worldview detail | Optional (private: owner only) |
| `POST` | `/api/worldviews` | Create worldview | Required |
| `PUT` | `/api/worldviews/{id}` | Update worldview | Required (owner) |
| `DELETE` | `/api/worldviews/{id}` | Delete worldview | Required (owner) |
| `POST` | `/api/worldviews/{id}/chains` | Add chain to worldview | Required (owner) |
| `DELETE` | `/api/worldviews/{id}/chains/{chainId}` | Remove chain | Required (owner) |
| `PUT` | `/api/worldviews/{id}/chains/order` | Reorder chains | Required (owner) |
| `GET` | `/api/worldviews/{id}/convergence/{otherId}` | Compute convergence score between two worldviews | Optional |
| `POST` | `/api/worldviews/{id}/votes` | Vote on a worldview | Required |
| `GET` | `/api/worldviews/discover` | Discover worldviews by semantic similarity to caller's own. Params: `limit` | Required |

**GET /api/worldviews/{id}/convergence/{otherId} — Response:**
```json
{
  "worldviewAId": "uuid",
  "worldviewBId": "uuid",
  "convergenceScore": 0.0,
  "classification": "StrongConvergence | PartialConvergence | Divergent",
  "semanticSimilarity": 0.0,
  "argumentJaccard": 0.0,
  "schwartzAlignment": 0.0,
  "sharedArgumentIds": ["uuid"],
  "schwartzBreakdown": {
    "SelfDirection": 0.0,
    "Stimulation": 0.0,
    "Hedonism": 0.0,
    "Achievement": 0.0,
    "Power": 0.0,
    "Security": 0.0,
    "Conformity": 0.0,
    "Tradition": 0.0,
    "Benevolence": 0.0,
    "Universalism": 0.0
  }
}
```

### 4.6 DebateRoomController

Base route: `/api/debaterooms`

| Verb | Route | Description | Auth |
|---|---|---|---|
| `GET` | `/api/debaterooms` | List rooms. Params: `status`, `topic`, `cursor`, `limit` | Optional |
| `GET` | `/api/debaterooms/{id}` | Get room with contributions | Optional |
| `POST` | `/api/debaterooms` | Create room (caller becomes Proponent) | Required |
| `POST` | `/api/debaterooms/{id}/join` | Join as Opponent (if slot open) | Required |
| `POST` | `/api/debaterooms/{id}/contribute` | Submit a contribution (links to an Argument) | Required (Proponent/Opponent) |
| `POST` | `/api/debaterooms/{id}/judge` | Submit judge score | Required (Judge role) |
| `POST` | `/api/debaterooms/{id}/conclude` | Conclude debate (moderator or all judges agree) | Required (Judge/Admin) |
| `GET` | `/api/debaterooms/{id}/aiflags` | Get AI referee flags for this room | Optional |

**POST /api/debaterooms — Request body:**
```json
{
  "title": "string",
  "topic": "string",
  "motionText": "string",
  "motionPropositionId": "uuid?",
  "format": "Oxford | LincolnDouglas | Custom",
  "timeLimitSeconds": 300,
  "maxContributionsPerSide": 5,
  "aiRefereeEnabled": true
}
```

**POST /api/debaterooms/{id}/contribute — Request body:**
```json
{
  "argumentId": "uuid",
  "role": "Proponent | Opponent | Rebuttal"
}
```

### 4.7 EpistemicProfileController

Base route: `/api/epistemic`

| Verb | Route | Description | Auth |
|---|---|---|---|
| `GET` | `/api/epistemic/me` | Get caller's full epistemic profile (all domains) | Required |
| `GET` | `/api/epistemic/users/{userId}` | Get a user's public epistemic profile | Optional |
| `GET` | `/api/epistemic/leaderboard` | Leaderboard by domain. Params: `domain`, `limit` | Optional |
| `GET` | `/api/epistemic/domains` | List all topic domains with participant counts | Optional |

**GET /api/epistemic/me — Response:**
```json
{
  "userId": "string",
  "profiles": [
    {
      "topicDomain": "string",
      "epistemicScore": 0.0,
      "voteAccuracy": 0.0,
      "contributionCount": 0,
      "rank": 0
    }
  ]
}
```

### 4.8 ReputationController

Base route: `/api/reputation`

| Verb | Route | Description | Auth |
|---|---|---|---|
| `GET` | `/api/reputation/me` | Get caller's reputation: XP, rank, badges, streaks | Required |
| `GET` | `/api/reputation/users/{userId}` | Get a user's public reputation card | Optional |
| `GET` | `/api/reputation/leaderboard` | Global XP leaderboard. Params: `limit`, `cursor` | Optional |
| `GET` | `/api/reputation/badges` | List all badge definitions | Optional |

### 4.9 SignalR Hubs

#### DebateHub (`/hubs/debate`)

```csharp
public class DebateHub : Hub
{
    // Client → Server
    Task JoinDebate(Guid debateRoomId);
    Task LeaveDebate(Guid debateRoomId);
    Task SubmitArgument(Guid debateRoomId, Guid argumentId, DebateRole role);
    Task JudgeScore(Guid debateRoomId, string userId, double score, string? comment);
    Task RequestAIReferee(Guid debateRoomId, Guid contributionId);

    // Server → Client (broadcast to room group)
    // "ContributionAdded"  — payload: DebateContributionDto
    // "ScoreUpdated"       — payload: { userId, score }
    // "AIRefereeFlag"      — payload: { contributionId, fallacies[], validityScore, comment }
    // "DebateConcluded"    — payload: { proponentScore, opponentScore, winner }
    // "SpectatorCount"     — payload: { count }
}
```

#### VotingHub (`/hubs/voting`)

```csharp
public class VotingHub : Hub
{
    // Client → Server
    Task SubscribeToArgument(Guid argumentId);
    Task UnsubscribeFromArgument(Guid argumentId);
    Task CastVote(Guid argumentId, VoteValue vote, VoteRationale rationale);
    Task RevokeVote(Guid argumentId);

    // Server → Client
    // "VoteScoreUpdated"   — payload: VoteTallyDto (broadcast to argument group)
    // "VoteCastConfirmed"  — payload: { argumentId, newTally } (caller only)
    // "VoteRejected"       — payload: { reason } (caller only, e.g. rate limit hit)
}
```

#### ChainUpdateHub (`/hubs/chains`)

```csharp
public class ChainUpdateHub : Hub
{
    // Client → Server
    Task JoinChainSession(Guid chainId);
    Task LeaveChainSession(Guid chainId);
    Task NotifyArgumentAdded(Guid chainId, Guid argumentId);
    Task NotifyArgumentRemoved(Guid chainId, Guid argumentId);
    Task NotifyLinkCreated(Guid chainId, ArgumentLinkDto link);

    // Server → Client
    // "ChainArgumentAdded"   — payload: { chainId, argument: ArgumentDto }
    // "ChainArgumentRemoved" — payload: { chainId, argumentId }
    // "ChainLinkCreated"     — payload: { chainId, link: ArgumentLinkDto }
    // "ChainUpdated"         — payload: ArgumentChainDto (full refresh signal)
}
```

---

## 5. UI / UX Specification

All new components extend the existing design system: cream background `#f5f2eb`, dark navy `#162131`, teal `#0f766e`, amber `#c07a3f`. Glassmorphism card pattern (existing `.cu-card` class), rounded pill tags (existing `.cu-tag`), hover lift effect (existing `.lift-on-hover`).

Bootstrap 5.3.3 grid is the layout foundation. No new CSS frameworks are introduced.

### 5a. Argument Feed

**Route:** `/feed` (new page, added to top nav between Dashboard and Arguments)

**Card Layout:**

The feed card extends the existing `.cu-argument-card`:

```html
<div class="cu-argument-card cu-feed-card">
  <!-- Vote column (left, 48px wide) -->
  <div class="cu-vote-column">
    <button class="cu-vote-btn cu-vote-up" aria-label="Upvote">&#9650;</button>
    <span class="cu-vote-score">0</span>
    <button class="cu-vote-btn cu-vote-down" aria-label="Downvote">&#9660;</button>
  </div>

  <!-- Content column -->
  <div class="cu-feed-content">
    <!-- Header: title + meta -->
    <div class="cu-feed-header">
      <h3 class="cu-feed-title">Argument Title</h3>
      <span class="cu-feed-meta">by <a href="/profile/user">username</a> · 2h ago · <span class="cu-epistemic-badge">Epistemic: 3.2</span></span>
    </div>

    <!-- Claim preview (collapsed by default) -->
    <p class="cu-feed-claim">Claim text truncated to 200 chars…</p>

    <!-- Tag row -->
    <div class="cu-tag-row">
      <span class="cu-tag">Climate</span>
      <span class="cu-tag cu-schwartz-badge cu-schwartz-universalism">Universalism</span>
    </div>

    <!-- Action bar -->
    <div class="cu-feed-actions">
      <button class="cu-action-btn cu-chain-btn">Chain</button>
      <button class="cu-action-btn cu-expand-btn">Propositions ▼</button>
      <button class="cu-action-btn cu-share-btn">Share</button>
      <button class="cu-action-btn cu-flag-btn">Flag</button>
    </div>

    <!-- Expandable proposition panel (hidden by default, toggled by Propositions button) -->
    <div class="cu-proposition-panel" hidden>
      <div class="cu-proposition cu-claim">Claim: …</div>
      <div class="cu-proposition cu-evidence">Evidence: …</div>
      <div class="cu-proposition cu-warrant">Warrant: …</div>
    </div>
  </div>
</div>
```

**Vote button behavior:**
- Clicking Up/Down triggers a modal overlay asking for the `VoteRationale` (radio buttons, one required). The modal dismisses on selection and the vote is cast via VotingHub `CastVote`.
- The vote score updates in real-time via VotingHub `VoteScoreUpdated` subscription — no page reload.
- Already-voted state: the active vote button gains class `cu-vote-active` (teal fill).

**Sort controls:**

```html
<div class="cu-sort-bar">
  <button class="cu-sort-btn active" data-sort="hot">Hot</button>
  <button class="cu-sort-btn" data-sort="new">New</button>
  <button class="cu-sort-btn" data-sort="top">Top</button>
  <button class="cu-sort-btn" data-sort="controversial">Controversial</button>
  <button class="cu-sort-btn" data-sort="common-ground">Common Ground</button>
</div>
```

**Filter panel (collapsible sidebar, 280px):**

| Filter | Control |
|---|---|
| Topic Domain | Multi-select dropdown |
| Schwartz Value | Checkboxes (10 values) |
| Minimum Epistemic Standing | Range slider (0–5) |
| Date range | Date picker pair |
| AI validated only | Toggle |
| My votes only | Toggle (requires auth) |

**Infinite scroll:** Feed uses IntersectionObserver to trigger cursor-based pagination requests as user approaches the bottom.

**Epistemic Standing badge color:**
- 0–1.9: neutral gray
- 2–2.9: amber `#c07a3f`
- 3–3.9: teal `#0f766e`
- 4–5: navy `#162131` with gold border

### 5b. Argument Chain Builder

**Route:** `/chains/builder` and `/chains/{id}/edit`

**Canvas approach:** Use `vis-network` (already jQuery-compatible, 450KB gzipped, no framework conflicts). The canvas renders inside a `<div id="cu-chain-canvas">` with explicit height (min 500px, responsive).

**Node visual spec:**

| Node Type | Background | Border | Label color |
|---|---|---|---|
| Claim (root) | `#162131` (navy) | `#0f766e` (teal), 3px | `#f5f2eb` (cream) |
| Evidence | `#0f766e` (teal) | `#c07a3f` (amber), 2px | `#f5f2eb` |
| Warrant | `#c07a3f` (amber) | `#162131`, 2px | `#162131` |
| Rebuttal | `#8b2020` (dark red) | `#c07a3f`, 2px | `#f5f2eb` |

**Edge visual spec:**

| Edge Type | Style | Color |
|---|---|---|
| Supports | Solid arrow | `#0f766e` (teal) |
| Contradicts | Dashed | `#8b2020` (red) |
| Refines | Dotted | `#0f766e` (teal) |
| Extends | Solid, double arrow | `#0f766e` (teal) |

**Toolbar:**

```
[ Add Argument ] [ Add Link ] [ Auto-layout ] [ AI Suggest ] [ Export JSON ] [ Export URL ] [ Save ]
```

- **Add Argument:** Opens a search modal to find an existing Argument or create a new one inline.
- **Add Link:** Click-to-select source node, then click target node, then pick LinkType from a dropdown.
- **Auto-layout:** Triggers vis-network's `hierarchicalRepulsion` layout algorithm.
- **AI Suggest:** Calls `POST /api/argumentlinks/suggest` with the currently selected node, displays returned suggestions in a slide-over panel. User can click "Add to Chain" on any suggestion.
- **Export JSON:** Downloads the chain as a JSON file conforming to the `ArgumentChain` schema.
- **Export URL:** Copies a shareable URL to the clipboard: `/chains/{id}` for saved chains, or a base64-encoded chain snapshot for unsaved work.

**Keyboard shortcuts:**

| Key | Action |
|---|---|
| `Del` / `Backspace` | Remove selected node or edge |
| `Ctrl+Z` | Undo last action (client-side undo stack, max 20 steps) |
| `Ctrl+S` | Save chain |
| `Esc` | Cancel current link creation |

**Validation rules enforced in the UI (before API call):**
- Chain must have at least one root Claim node.
- No cycles (BFS check in the client before submitting any link).
- Chain must have at least 2 Arguments before it can be saved.

### 5c. Worldview Composer

**Route:** `/worldviews/composer` and `/worldviews/{id}/edit`

**Layout:** Two-column. Left column (70%): draggable grid of `ArgumentChain` cards. Right column (30%): Schwartz value radar chart + metadata form.

**Chain card in composer:**

```html
<div class="cu-composer-chain-card" draggable="true" data-chain-id="uuid">
  <div class="cu-chain-drag-handle">&#9776;</div>
  <div class="cu-chain-info">
    <h4>Chain Title</h4>
    <p class="cu-chain-meta">5 arguments · Universalism, Benevolence</p>
  </div>
  <button class="cu-remove-chain" aria-label="Remove chain">&#x2715;</button>
</div>
```

Drag-and-drop is implemented with the HTML5 Drag-and-Drop API (no additional library). `dragstart`, `dragover`, `drop` events on the grid update the `OrderIndex` values and POST to `/api/worldviews/{id}/chains/order`.

**Radar chart:** Chart.js radar chart (already a common Bootstrap+jQuery stack companion) with 10 axes for the Schwartz value dimensions. The chart updates live as chains are added or removed, computing the aggregate `SchwartzVector` client-side from the chain metadata loaded in the page.

**Add chain panel:** A search overlay (`/api/argumentchains?search=...` with debounced input) shows matching chains. Results displayed as cards with a "+" button.

**Compare button:** Opens a modal or navigates to `/convergence/compare?a={id}&b={otherId}`. User selects the second Worldview from a searchable dropdown.

**Visibility toggle:**

```html
<div class="cu-visibility-toggle">
  <label class="cu-toggle-label">
    <input type="checkbox" id="worldview-public" />
    <span class="cu-toggle-track"></span>
    <span class="cu-toggle-label-text">Make Public</span>
  </label>
</div>
```

Toggling calls `PUT /api/worldviews/{id}` with the updated `isPublic` field.

### 5d. Convergence Dashboard (Enhanced)

**Route:** `/convergence` (existing page, enhanced)

**New layout additions:**

- **Side-by-side comparison panel:** Two scrollable columns of `ArgumentChain` summaries. Shared arguments (present in both) are highlighted with a teal left-border. Unique arguments are shown at reduced opacity (0.6).
- **Overlap score header:** A large numeric display at the top of the comparison (`76% Convergence`) with three sub-scores (Semantic, Argument, Schwartz).
- **Schwartz breakdown table:**

| Dimension | Worldview A | Worldview B | Alignment |
|---|---|---|---|
| Universalism | 0.8 | 0.7 | 0.94 |
| Benevolence | 0.6 | 0.5 | 0.91 |
| … | … | … | … |

- **Bridge Arguments panel:** A collapsible panel on the right side showing AI-generated bridge arguments. Each bridge argument card has a "Add to My Worldview" button and a "Debate This" button that creates a new DebateRoom with the bridge argument as the motion.

### 5e. Debate Room UI

**Route:** `/debates/{id}`

**Layout:** Full-width, split 50/50 horizontally.

```
+---------------------------+---------------------------+
|  PROPONENT                |  OPPONENT                 |
|  [username]               |  [username]               |
|  Score: 7.2               |  Score: 6.8               |
|  +-----------------------+ | +-----------------------+ |
|  | Contribution 1        | | | Contribution 1        | |
|  | [Argument title]      | | | [Argument title]      | |
|  | Wilson: 0.84          | | | AI: Valid ✓           | |
|  +-----------------------+ | +-----------------------+ |
|  [ Submit Argument ]       | [ Submit Argument ]       |
+---------------------------+---------------------------+
|  JUDGE PANEL (collapsible bottom strip)               |
|  Judge A: 7/10  Judge B: 8/10  Judge C: 6/10          |
|  AI Referee: [flag list]                              |
+---------------------------+---------------------------+
|  SPECTATOR FEED (right sidebar, 300px)               |
|  Live join count: 14                                   |
|  [read-only chronological contribution list]           |
+---------------------------+---------------------------+
```

**Contribution submission:** A modal form. The user searches for an existing Argument (autocomplete against `/api/arguments?search=...&isPublic=true`) and selects it. A preview of the Argument's claim and evidence is shown before confirmation.

**AI referee indicator:** Each contribution card shows a badge after AI processing:
- Green badge: "Valid" (no fallacies detected, validity score ≥ 0.7)
- Amber badge: "Weak" (validity score 0.4–0.69)
- Red badge: fallacy names listed (e.g., "Ad Hominem", "Straw Man")

AI flags arrive via DebateHub `AIRefereeFlag` event within 5 seconds of contribution submission.

**Spectator mode:** If the current user is neither Proponent, Opponent, nor a Judge, the Submit buttons are hidden and the layout is read-only. The spectator count is updated via DebateHub `SpectatorCount` event.

**Concluded state:** When DebateHub broadcasts `DebateConcluded`, the UI renders a full-width winner banner and a summary scorecard. Both columns are frozen (no further contributions).

### 5f. Reputation & Gamification Panel

**Route:** `/profile/{username}` (extended) and `/leaderboard`

**Profile card additions:**

```html
<div class="cu-reputation-card cu-card">
  <div class="cu-xp-section">
    <span class="cu-rank-badge cu-rank-scholar">Scholar</span>
    <div class="cu-xp-bar">
      <div class="cu-xp-fill" style="width: 73%"></div>
    </div>
    <span class="cu-xp-label">1460 / 2000 XP → Sage</span>
  </div>

  <div class="cu-streak-section">
    <span class="cu-streak-icon">&#128293;</span>
    <span class="cu-streak-count">7-day streak</span>
  </div>

  <div class="cu-badge-showcase">
    <div class="cu-badge cu-badge-earned" title="First Blood: First argument upvoted">&#9733;</div>
    <!-- ... -->
  </div>
</div>
```

**Epistemic radar chart:** Same Chart.js radar component reused from Worldview Composer, showing the user's `EpistemicScore` across all domains they've participated in.

**Leaderboard:** `/leaderboard` page with tabs for Global XP, By Domain (dropdown), and Debate Winners. Each row shows rank, avatar placeholder, username, XP, rank badge, and top domain.

---

## 6. AI / Semantic Kernel Integration

All plugins are implemented as `IKernelPlugin` implementations registered in `Program.cs` via `kernel.ImportPluginFromObject(...)`. The existing Semantic Kernel setup (DeepSeek-V3-0324 as primary, gpt-4o-mini as fallback) is preserved.

### 6.1 ArgumentDecompositionPlugin (Extended)

**Existing functionality:** Decomposes raw text into a structured `Argument`.

**Phase 2 extensions:**

```csharp
[KernelFunction("DecomposeToPropositions")]
[Description("Decomposes a raw argument text into typed Propositions: Claim, Evidence, Warrant, Rebuttal.")]
public async Task<PropositionDecompositionResult> DecomposeToPropositions(
    Kernel kernel,
    [Description("The raw argument text to decompose")] string rawText,
    [Description("Optional hint about the topic domain")] string? topicDomain = null)
```

**Prompt strategy:** Chain-of-thought. The system prompt instructs the model to:
1. Identify the central claim.
2. Enumerate evidence statements with source URLs if mentioned.
3. Identify any explicit warrant or write a minimal implicit one.
4. Identify any rebuttals present in the text.
5. Output structured JSON conforming to `PropositionDecompositionResult`.

**Does not use RAG/embeddings.** Purely generative.

**Output type:**
```csharp
public record PropositionDecompositionResult(
    string ClaimText,
    List<string> EvidenceTexts,
    string WarrantText,
    List<string> RebuttalTexts,
    string? TopicDomain,
    List<string> SchwartzValues
);
```

### 6.2 ArgumentLinkSuggestionPlugin

```csharp
[KernelFunction("SuggestLinks")]
[Description("Given an argument, retrieves semantically similar arguments from the database and suggests how they are linked.")]
public async Task<List<ArgumentLinkSuggestion>> SuggestLinks(
    Kernel kernel,
    [Description("The ID of the source argument")] Guid sourceArgumentId,
    [Description("Maximum number of suggestions to return")] int maxSuggestions = 5)
```

**Prompt strategy:** Two-phase.
1. **RAG retrieval:** Embed the source argument's `ClaimProposition.Text + WarrantText` using the text-embedding-ada-002 model. Perform pgvector cosine similarity search against `Argument.Embedding` to retrieve top 20 candidates.
2. **LLM classification:** Pass the source argument and the 20 candidates to the LLM. The system prompt asks the model to identify which candidates have a `Supports`, `Contradicts`, `Refines`, or `Extends` relationship with the source, and to explain why. The model outputs a ranked list of up to `maxSuggestions` suggestions with `LinkType` and a one-sentence explanation.

**Uses RAG/embeddings:** Yes. Requires `Argument.Embedding` to be populated.

**Output type:**
```csharp
public record ArgumentLinkSuggestion(
    Guid TargetArgumentId,
    string TargetTitle,
    LinkType SuggestedLinkType,
    string Explanation,
    double SimilarityScore
);
```

### 6.3 WorldviewConvergencePlugin

```csharp
[KernelFunction("ComputeConvergence")]
[Description("Computes the semantic convergence score between two Worldviews using embedding similarity and Schwartz value alignment.")]
public async Task<ConvergenceResult> ComputeConvergence(
    Kernel kernel,
    [Description("ID of the first Worldview")] Guid worldviewAId,
    [Description("ID of the second Worldview")] Guid worldviewBId)
```

**Prompt strategy:** Mostly computational, minimal LLM involvement.
1. Load `Worldview.Embedding` for both worldviews from the database.
2. Compute cosine similarity between the two embedding vectors (pure math, no LLM).
3. Compute Argument Jaccard index from `ArgumentIds` sets.
4. Compute Schwartz cosine from `SchwartzVector` arrays.
5. Compute weighted sum.
6. Optionally call LLM with the top 5 shared arguments and top 5 divergent arguments to generate a 2-sentence "convergence narrative" for display in the UI.

**Uses embeddings:** Yes. LLM used only for narrative generation, not for the score itself.

### 6.4 FallacyDetectionPlugin

```csharp
[KernelFunction("DetectFallacies")]
[Description("Detects logical fallacies in a debate contribution in real-time.")]
public async Task<FallacyDetectionResult> DetectFallacies(
    Kernel kernel,
    [Description("The argument text (claim + evidence + warrant)")] string argumentText,
    [Description("The prior contributions in the debate for context")] string priorContext,
    [Description("The motion being debated")] string motionText)
```

**Prompt strategy:** Zero-shot classification with a predefined fallacy taxonomy. The system prompt includes definitions of 20 common informal fallacies (Ad Hominem, Straw Man, False Dichotomy, Appeal to Authority, Slippery Slope, Hasty Generalization, etc.). The model outputs:
- `IsValid` (bool): whether the argument is logically coherent
- `ValidityScore` (0.0–1.0): confidence in logical validity
- `Fallacies` (list of `{ Name, Description, QuotedText }`)
- `SuggestedImprovement` (string, 1 sentence)

**Does not use RAG.** Context window is sufficient for real-time debate contributions.

**Latency target:** < 3 seconds end-to-end. Use gpt-4o-mini as primary (faster) for this function, with DeepSeek as fallback.

**Output type:**
```csharp
public record FallacyDetectionResult(
    bool IsValid,
    double ValidityScore,
    List<FallacyFlag> Fallacies,
    string? SuggestedImprovement
);

public record FallacyFlag(string Name, string Description, string QuotedText);
```

### 6.5 BridgeArgumentPlugin

```csharp
[KernelFunction("GenerateBridgeArguments")]
[Description("Generates synthetic bridge arguments that could reconcile two diverging worldviews.")]
public async Task<List<BridgeArgumentSuggestion>> GenerateBridgeArguments(
    Kernel kernel,
    [Description("ID of the first Worldview")] Guid worldviewAId,
    [Description("ID of the second Worldview")] Guid worldviewBId,
    [Description("Maximum number of bridge arguments to generate")] int count = 3)
```

**Prompt strategy:** Three-phase.
1. **Identify divergence:** Load the top 5 most-upvoted Arguments unique to each Worldview.
2. **RAG search:** For each pair of divergent arguments, search for existing Arguments in the database that cite propositions from both sides using pgvector similarity. If found, prefer real Arguments over AI-generated ones.
3. **Generate:** If no bridge found via RAG, prompt the LLM with the two divergent arguments and ask it to compose a new bridging Argument that acknowledges both claims, identifies a shared underlying value (using the Schwartz framework), and proposes a resolution. The generated argument is marked `IsAIGenerated = true` and held for user confirmation.

**Uses RAG/embeddings:** Yes.

**Output type:**
```csharp
public record BridgeArgumentSuggestion(
    bool IsExisting,             // true if from DB, false if AI-generated
    Guid? ExistingArgumentId,    // set if IsExisting = true
    string? GeneratedClaim,      // set if IsExisting = false
    string? GeneratedWarrant,
    string SharedSchwartzValue,
    string BridgeRationale       // one paragraph
);
```

### 6.6 EpistemicScoringPlugin

```csharp
[KernelFunction("RecalculateEpistemicScore")]
[Description("Recalculates a user's epistemic score for a topic domain based on recent vote history and argument quality.")]
public async Task<double> RecalculateEpistemicScore(
    Kernel kernel,
    [Description("The user ID to recalculate for")] string userId,
    [Description("The topic domain")] string topicDomain)
```

**Prompt strategy:** Purely computational — no LLM required.

**Algorithm:**
1. Fetch all `ArgumentVote` records for the user in the domain within the last 90 days.
2. For each vote, determine the community consensus direction (the vote direction with > 60% of weighted votes).
3. `VoteAccuracy = (votes that matched consensus) / (total votes cast)`.
4. `ContributionQuality = avg(WilsonScore of arguments submitted by user in domain)`.
5. `EpistemicScore = clamp(VoteAccuracy * 2.5 + ContributionQuality * 2.5, 0.0, 5.0)`.

**Does not use LLM or RAG.** Registered as a `KernelFunction` for pipeline composability but executes pure C# math.

**Invocation:** Triggered by a background service (`EpistemicScoringWorker : BackgroundService`) that runs every 15 minutes and updates `EpistemicProfile` records where `UpdatedAt < UtcNow - 15 minutes` and the user has had activity.

---

## 7. PostgreSQL / pgvector Schema

### 7.1 Extension Installation

```sql
-- Run once on the database
CREATE EXTENSION IF NOT EXISTS vector;
```

Verify: `SELECT * FROM pg_extension WHERE extname = 'vector';`

### 7.2 Embedding Columns

```sql
-- Add to existing arguments table
ALTER TABLE "Arguments"
    ADD COLUMN "Embedding" vector(1536);

-- New propositions table
ALTER TABLE "Propositions"
    ADD COLUMN "Embedding" vector(1536);

-- New argument_chains table
ALTER TABLE "ArgumentChains"
    ADD COLUMN "Embedding" vector(1536);

-- New worldviews table
ALTER TABLE "Worldviews"
    ADD COLUMN "Embedding" vector(1536);
    ADD COLUMN "SchwartzVector" double precision[10];
```

### 7.3 Vector Indexes

```sql
-- IVFFlat index for approximate nearest-neighbor search on Arguments
-- lists = sqrt(row_count), typically 100 for < 1M rows
CREATE INDEX idx_arguments_embedding_cosine
    ON "Arguments"
    USING ivfflat ("Embedding" vector_cosine_ops)
    WITH (lists = 100);

-- IVFFlat index for Propositions
CREATE INDEX idx_propositions_embedding_cosine
    ON "Propositions"
    USING ivfflat ("Embedding" vector_cosine_ops)
    WITH (lists = 100);

-- IVFFlat index for Worldviews (smaller table, fewer lists)
CREATE INDEX idx_worldviews_embedding_cosine
    ON "Worldviews"
    USING ivfflat ("Embedding" vector_cosine_ops)
    WITH (lists = 20);
```

**Note on IVFFlat:** The index must be built after data is loaded (`ANALYZE "Arguments";` after bulk insert). For empty tables at migration time, the index will be created but only becomes effective after enough rows are inserted and `ANALYZE` is run.

**Alternative:** For higher recall requirements (> 95%), use `hnsw` index type (pgvector ≥ 0.5.0):

```sql
CREATE INDEX idx_arguments_embedding_hnsw
    ON "Arguments"
    USING hnsw ("Embedding" vector_cosine_ops)
    WITH (m = 16, ef_construction = 64);
```

HNSW has higher memory usage but better query-time recall and no need for `ANALYZE`.

### 7.4 Similarity Search Query Pattern (C#)

```csharp
// In a repository or DbContext extension method
public async Task<List<ArgumentSimilarityResult>> FindSimilarArguments(
    float[] queryEmbedding,
    int limit = 10,
    double threshold = 0.7,
    CancellationToken ct = default)
{
    // EF Core does not natively support pgvector operators; use raw SQL
    var results = await _context.Database
        .SqlQueryRaw<ArgumentSimilarityResult>(
            """
            SELECT
                a."Id",
                a."Title",
                a."UserId",
                1 - (a."Embedding" <=> CAST(@embedding AS vector)) AS "SimilarityScore"
            FROM "Arguments" a
            WHERE a."IsPublic" = true
              AND a."Embedding" IS NOT NULL
              AND 1 - (a."Embedding" <=> CAST(@embedding AS vector)) >= @threshold
            ORDER BY a."Embedding" <=> CAST(@embedding AS vector)
            LIMIT @limit
            """,
            new NpgsqlParameter("embedding", NpgsqlDbType.Unknown)
            {
                Value = queryEmbedding,
                DataTypeName = "vector"
            },
            new NpgsqlParameter("threshold", threshold),
            new NpgsqlParameter("limit", limit)
        )
        .ToListAsync(ct);

    return results;
}
```

**`<=>` operator:** cosine distance (0 = identical, 2 = opposite). Similarity = `1 - distance`.

**Embedding generation:** Call the Azure AI Foundry text-embedding-ada-002 endpoint via `ITextEmbeddingGenerationService` from Semantic Kernel:

```csharp
public class EmbeddingService(ITextEmbeddingGenerationService embeddingService)
{
    public async Task<float[]> GenerateEmbedding(string text, CancellationToken ct = default)
    {
        var embeddings = await embeddingService.GenerateEmbeddingsAsync(
            new List<string> { text },
            cancellationToken: ct);

        return embeddings[0].ToArray();
    }
}
```

### 7.5 Migration Strategy

Phase 2 introduces significant schema additions. Use EF Core code-first migrations.

**Migration order:**

```
Sprint 1:
  Add-Migration Phase2_Propositions
  Add-Migration Phase2_ArgumentExtensions  (add Embedding, HotScore, WilsonScore columns to existing Arguments)
  Add-Migration Phase2_ArgumentLinks
  Add-Migration Phase2_ArgumentVotes
  Add-Migration Phase2_ArgumentChains
  
Sprint 1 (data):
  -- Custom data migration: copy existing BeliefSystem records to Worldview
  -- Mark all existing Arguments as IsPublic = false pending user action

Sprint 2:
  Add-Migration Phase2_Worldviews
  Add-Migration Phase2_WorldviewChains
  Add-Migration Phase2_WorldviewVotes

Sprint 3:
  Add-Migration Phase2_DebateRooms
  Add-Migration Phase2_DebateContributions
  Add-Migration Phase2_EpistemicProfiles
  Add-Migration Phase2_UserReputation

Sprint 4:
  Add-Migration Phase2_Moderators
  Add-Migration Phase2_ModerationFlags
  Add-Migration Phase2_VectorIndexes  (custom SQL via MigrationBuilder.Sql())
```

**pgvector migration via EF Core:**

EF Core does not have native `vector` column support. Register a custom column type:

```csharp
// In OnModelCreating
builder.Entity<Argument>()
    .Property(a => a.Embedding)
    .HasColumnType("vector(1536)");

// The migration will generate:
// migrationBuilder.AddColumn<float[]>(
//     name: "Embedding",
//     table: "Arguments",
//     type: "vector(1536)",
//     nullable: true);
```

Use `Pgvector.EntityFrameworkCore` NuGet package (by the pgvector project) for cleaner integration, including the `<=>` operator support in LINQ if desired.

**BeliefSystem → Worldview migration script:**

```sql
-- Run after Phase2_Worldviews migration
INSERT INTO "Worldviews" ("Id", "Title", "Description", "UserId", "IsPublic", "Tags", "SchwartzValues", "SchwartzVector", "CreatedAt", "UpdatedAt")
SELECT
    gen_random_uuid(),
    bs."Name",
    bs."Description",
    bs."UserId",
    false,
    '{}',
    '{}',
    ARRAY[0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0],
    bs."CreatedAt",
    NOW()
FROM "BeliefSystems" bs;
```

---

## 8. SignalR Hubs (Phase 2)

### 8.1 Hub Registration

```csharp
// Program.cs additions
app.MapHub<DebateHub>("/hubs/debate");
app.MapHub<VotingHub>("/hubs/voting");
app.MapHub<ChainUpdateHub>("/hubs/chains");
```

```csharp
// SignalR backplane for multi-server deployments (Sprint 4)
builder.Services.AddSignalR()
    .AddAzureSignalR(builder.Configuration["AzureSignalR:ConnectionString"]);
```

### 8.2 DebateHub Full Implementation Signature

```csharp
[Authorize]
public class DebateHub : Hub
{
    private readonly IDebateService _debateService;
    private readonly IFallacyDetectionPlugin _fallacyPlugin;

    // Client → Server
    public async Task JoinDebate(Guid debateRoomId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"debate-{debateRoomId}");
        await Clients.Group($"debate-{debateRoomId}")
            .SendAsync("SpectatorCount", await _debateService.GetSpectatorCount(debateRoomId));
    }

    public async Task LeaveDebate(Guid debateRoomId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"debate-{debateRoomId}");
    }

    public async Task SubmitArgument(Guid debateRoomId, Guid argumentId, DebateRole role)
    {
        var userId = Context.UserIdentifier!;
        var contribution = await _debateService.AddContribution(debateRoomId, userId, argumentId, role);

        // Broadcast to room
        await Clients.Group($"debate-{debateRoomId}")
            .SendAsync("ContributionAdded", contribution);

        // Kick off AI referee in background
        _ = Task.Run(async () =>
        {
            var flags = await _fallacyPlugin.DetectFallaciesAsync(contribution);
            await Clients.Group($"debate-{debateRoomId}")
                .SendAsync("AIRefereeFlag", new { contributionId = contribution.Id, flags });
        });
    }

    public async Task JudgeScore(Guid debateRoomId, string scoredUserId, double score, string? comment)
    {
        var judgeId = Context.UserIdentifier!;
        await _debateService.SubmitJudgeScore(debateRoomId, judgeId, scoredUserId, score, comment);

        await Clients.Group($"debate-{debateRoomId}")
            .SendAsync("ScoreUpdated", new { userId = scoredUserId, score });
    }
}
```

### 8.3 VotingHub Full Implementation Signature

```csharp
[Authorize]
public class VotingHub : Hub
{
    private readonly IVotingService _votingService;

    public async Task SubscribeToArgument(Guid argumentId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"arg-votes-{argumentId}");
    }

    public async Task UnsubscribeFromArgument(Guid argumentId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"arg-votes-{argumentId}");
    }

    public async Task CastVote(Guid argumentId, VoteValue vote, VoteRationale rationale)
    {
        var userId = Context.UserIdentifier!;

        var result = await _votingService.CastVoteAsync(userId, argumentId, vote, rationale);

        if (!result.Success)
        {
            await Clients.Caller.SendAsync("VoteRejected", new { reason = result.ErrorMessage });
            return;
        }

        // Confirm to caller
        await Clients.Caller.SendAsync("VoteCastConfirmed", new { argumentId, newTally = result.Tally });

        // Broadcast updated tally to all subscribers
        await Clients.Group($"arg-votes-{argumentId}")
            .SendAsync("VoteScoreUpdated", result.Tally);
    }

    public async Task RevokeVote(Guid argumentId)
    {
        var userId = Context.UserIdentifier!;
        var tally = await _votingService.RevokeVoteAsync(userId, argumentId);

        await Clients.Group($"arg-votes-{argumentId}")
            .SendAsync("VoteScoreUpdated", tally);
    }
}
```

### 8.4 ChainUpdateHub Full Implementation Signature

```csharp
[Authorize]
public class ChainUpdateHub : Hub
{
    private readonly IChainService _chainService;

    public async Task JoinChainSession(Guid chainId)
    {
        // Validate that the user has access to this chain
        var userId = Context.UserIdentifier!;
        if (!await _chainService.UserCanViewChain(userId, chainId))
        {
            Context.Abort();
            return;
        }
        await Groups.AddToGroupAsync(Context.ConnectionId, $"chain-{chainId}");
    }

    public async Task LeaveChainSession(Guid chainId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"chain-{chainId}");
    }

    public async Task NotifyArgumentAdded(Guid chainId, Guid argumentId)
    {
        var argument = await _chainService.GetArgumentDto(argumentId);
        await Clients.OthersInGroup($"chain-{chainId}")
            .SendAsync("ChainArgumentAdded", new { chainId, argument });
    }

    public async Task NotifyLinkCreated(Guid chainId, ArgumentLinkDto link)
    {
        await Clients.OthersInGroup($"chain-{chainId}")
            .SendAsync("ChainLinkCreated", new { chainId, link });
    }
}
```

---

## 9. Voting Algorithm

### 9.1 Wilson Score Lower Bound

The Wilson score is used for the "Top" sort and for the `WilsonScore` column stored on `Argument`.

```csharp
/// <summary>
/// Computes the Wilson score lower bound for a 95% confidence interval.
/// </summary>
/// <param name="upvotes">Number of positive votes.</param>
/// <param name="total">Total number of votes cast (up + down, excluding Abstain).</param>
/// <returns>Wilson score lower bound in [0, 1].</returns>
public static double WilsonScoreLowerBound(int upvotes, int total)
{
    if (total == 0) return 0.0;

    const double z = 1.96; // 95% confidence
    double p = (double)upvotes / total;
    double denominator = 1.0 + z * z / total;
    double centre = p + z * z / (2 * total);
    double margin = z * Math.Sqrt(p * (1 - p) / total + z * z / (4 * total * total));

    return (centre - margin) / denominator;
}
```

### 9.2 Epistemic-Weighted Voting

The raw vote count used in all score calculations is the sum of `EpistemicWeight` values, not the raw count.

```csharp
public static double EpistemicWeightedVoteCount(
    IEnumerable<ArgumentVote> votes,
    VoteValue direction,
    double maxMultiplier = 2.0)
{
    return votes
        .Where(v => v.Vote == direction)
        .Sum(v => 1.0 + (v.EpistemicWeight - 1.0) * (maxMultiplier - 1.0));
}
```

`EpistemicWeight` is set at vote-cast time from the voter's `EpistemicProfile.EpistemicScore` in the argument's topic domain, normalized to [1.0, `maxMultiplier`]:

```csharp
// EpistemicScore range: 0–5, maxMultiplier = 2.0
double weight = 1.0 + (epistemicScore / 5.0) * (maxMultiplier - 1.0);
// epistemicScore = 0   → weight = 1.0 (neutral)
// epistemicScore = 2.5 → weight = 1.5
// epistemicScore = 5.0 → weight = 2.0 (full multiplier)
```

`maxMultiplier` is configurable via `IConfiguration["Voting:EpistemicMaxMultiplier"]` (default: `2.0`).

### 9.3 AI Validation Bonus

If `FallacyDetectionPlugin` returns `IsValid = true` and `ValidityScore >= 0.8` for an Argument, an AI validation bonus is added to the argument's effective score:

```csharp
double effectiveScore = wilsonScore + (isAIValidated ? aiBonus : 0.0);
```

`aiBonus` is configurable via `IConfiguration["Voting:AIValidationBonus"]` (default: `0.05`).

The AI validation flag is set by a background job (`AIValidationWorker`) that processes newly submitted public Arguments within 60 seconds of publication.

### 9.4 Hot Score (Time Decay)

```csharp
/// <summary>
/// Reddit-style hot score with configurable gravity.
/// </summary>
public static double HotScore(
    double weightedUpvotes,
    double weightedDownvotes,
    DateTime createdAt,
    double gravity = 1.8)
{
    double netVotes = weightedUpvotes - weightedDownvotes;
    double ageHours = (DateTime.UtcNow - createdAt).TotalHours;
    return netVotes / Math.Pow(ageHours + 2.0, gravity);
}
```

`gravity` is configurable via `IConfiguration["Voting:HotScoreGravity"]` (default: `1.8`).

Hot scores are recomputed by `HotScoreUpdateWorker : BackgroundService` every 5 minutes for Arguments modified in the last 24 hours, and every 60 minutes for older Arguments.

### 9.5 Controversy Score

Used for the "Controversial" sort:

```csharp
public static double ControversyScore(double weightedUpvotes, double weightedDownvotes)
{
    if (weightedUpvotes == 0 || weightedDownvotes == 0) return 0.0;

    double magnitude = weightedUpvotes + weightedDownvotes;
    double balance = Math.Min(weightedUpvotes, weightedDownvotes) / Math.Max(weightedUpvotes, weightedDownvotes);

    return magnitude * balance;
}
```

Higher when both up and down votes are high and roughly equal — the classic controversy signal.

### 9.6 Feed Ranking Queries

```sql
-- Hot sort
SELECT * FROM "Arguments"
WHERE "IsPublic" = true
ORDER BY "HotScore" DESC
LIMIT 20 OFFSET 0;

-- Top sort (Wilson score)
SELECT * FROM "Arguments"
WHERE "IsPublic" = true
ORDER BY "WilsonScore" DESC
LIMIT 20 OFFSET 0;

-- New sort
SELECT * FROM "Arguments"
WHERE "IsPublic" = true
ORDER BY "CreatedAt" DESC
LIMIT 20 OFFSET 0;
```

All sort columns are indexed:

```sql
CREATE INDEX idx_arguments_hot_score ON "Arguments" ("HotScore" DESC) WHERE "IsPublic" = true;
CREATE INDEX idx_arguments_wilson_score ON "Arguments" ("WilsonScore" DESC) WHERE "IsPublic" = true;
CREATE INDEX idx_arguments_created_at ON "Arguments" ("CreatedAt" DESC) WHERE "IsPublic" = true;
```

---

## 10. Gamification System

### 10.1 XP Awards Table

| Action | XP | Notes |
|---|---|---|
| Submit an Argument (accepted, not AI-generated) | +10 | One-time per Argument |
| Argument receives an Upvote | +5 | Per upvote received |
| Argument receives a Downvote | -2 | Per downvote received |
| Argument AI-validated as logically valid | +15 | One-time per Argument |
| Create an ArgumentChain (≥ 3 arguments) | +20 | One-time per Chain |
| Create a public Worldview (≥ 2 chains) | +30 | One-time per Worldview |
| Win a Debate Room (judge consensus) | +50 | Per debate win |
| Lose a Debate Room | +10 | Participation reward |
| Daily streak maintained | +5/day | Starting day 3 |
| Vote that aligns with eventual consensus | +2 | Per accurate vote |
| Receive "Changed My View" rationale | +25 | High-value signal |
| First public Argument | +25 | One-time milestone |
| First Worldview published | +50 | One-time milestone |
| Argument reaches 10 upvotes | +20 | One-time per Argument |
| Argument reaches 50 upvotes | +50 | One-time per Argument |
| Submit a bridge Argument that gets adopted | +40 | Per adoption |

XP awards are processed by `XPAwardService` called from domain event handlers. All XP events are logged to an `XPTransaction` table (UserId, Amount, Reason, CreatedAt) for auditability.

### 10.2 Rank Thresholds

| Rank | XP Required | Perks |
|---|---|---|
| Novice | 0 | Basic feed access |
| Thinker | 100 | Can create public Argument Chains |
| Reasoner | 500 | Can create public Worldviews; Debate Room access |
| Scholar | 2,000 | Can serve as Debate Judge (with Epistemic Standing ≥ 3.0) |
| Sage | 10,000 | Can apply to be a topic Moderator; featured on global leaderboard |
| Luminary | 50,000 | Permanent featured badge; input into platform governance decisions |

Rank is recomputed synchronously on every XP award. The `UserReputation.Rank` column is updated immediately; no background job required.

### 10.3 Badge Definitions

| Badge ID | Name | Trigger Condition |
|---|---|---|
| `first_argument` | First Principles | Submit your first public Argument |
| `first_upvote` | Signal Received | Receive your first Upvote |
| `chain_builder` | Chain Reaction | Create an Argument Chain with ≥ 5 Arguments |
| `worldview_author` | Worldview Author | Publish your first public Worldview |
| `debate_winner` | Victor | Win a Debate Room |
| `bridge_builder` | Bridge Builder | Have a bridge Argument adopted into 3 different Worldviews |
| `changed_mind` | Open Mind | Receive "Changed My View" rationale on 5 separate Arguments |
| `epistemic_expert` | Domain Expert | Reach Epistemic Standing ≥ 4.0 in any topic domain |
| `streak_7` | Week of Wisdom | Maintain a 7-day consecutive contribution streak |
| `streak_30` | Month of Reason | Maintain a 30-day consecutive contribution streak |
| `top_argument` | Viral Reasoning | Have an Argument reach 100 upvotes |
| `convergence_catalyst` | Convergence Catalyst | Be the author of Arguments that appear in both sides of a Strong Convergence pair |
| `fallacy_free` | Clean Logic | Submit 20 consecutive Arguments with no fallacy flags from AI referee |
| `judge` | Fair Judge | Complete 10 Debate Room judge sessions |

Badges are awarded by `BadgeAwardService` which checks trigger conditions after each relevant domain event. Badges are stored as string IDs in `UserReputation.Badges` (jsonb `text[]`). Each badge ID is unique per user — duplicate awards are silently skipped.

### 10.4 Streak Definition

A streak is incremented when the user submits at least one accepted (not rejected, not shadowbanned) public Argument on a given UTC calendar day.

```csharp
public void UpdateStreak(UserReputation reputation, DateTime today)
{
    var todayDate = today.Date;
    var lastDate = reputation.LastStreakDate?.Date;

    if (lastDate == null)
    {
        // First contribution
        reputation.CurrentStreak = 1;
        reputation.LastStreakDate = today;
    }
    else if (lastDate == todayDate)
    {
        // Already counted today — no change
    }
    else if (lastDate == todayDate.AddDays(-1))
    {
        // Consecutive day
        reputation.CurrentStreak++;
        reputation.LastStreakDate = today;
        if (reputation.CurrentStreak > reputation.LongestStreak)
            reputation.LongestStreak = reputation.CurrentStreak;
    }
    else
    {
        // Streak broken
        reputation.CurrentStreak = 1;
        reputation.LastStreakDate = today;
    }
}
```

**Streak grace period:** A user may miss one day per 7-day window without breaking their streak if they have previously used a "streak freeze" (consumable item, awarded at rank Reasoner: 1 freeze; Scholar: 3 freezes). Streak freezes are stored as a count in `UserReputation` (add `StreakFreezes` int column in Sprint 4).

### 10.5 Topic Credibility Score

The Topic Credibility Score is identical to `EpistemicProfile.EpistemicScore` for public display purposes. It is the single number shown on user profile cards next to a topic domain.

The rolling 90-day window means that old votes (> 90 days) are excluded from accuracy calculations. A user's score can decrease over time if their recent vote accuracy drops.

---

## 11. Moderation & Trust

### 11.1 AI-Assisted Flagging Pipeline

```
[Argument Submitted]
       |
       v
[ArgumentDecompositionPlugin validates structure]
       |
       +-- Invalid structure → 400 Bad Request (never reaches feed)
       |
       v
[AIValidationWorker runs FallacyDetectionPlugin within 60s]
       |
       +-- ValidityScore < 0.3 → Argument shadowbanned (IsVisible = false, not deleted)
       +-- Fallacies detected → FallacyFlags stored; Argument labeled in UI; score penalized -0.1 WilsonScore
       +-- Toxic content detected (second LLM call with toxicity prompt) → Automatic flag created, sent to review queue
       |
       v
[Argument visible in feed with AI quality labels]
```

**Toxicity check:** A separate LLM call using a concise system prompt: "Does the following text contain hate speech, personal attacks, or abusive language? Respond with JSON: { isToxic: bool, severity: low|medium|high, reason: string }." Uses gpt-4o-mini exclusively (cost-optimized for volume).

### 11.2 Community Flagging

Users can flag any Argument, Proposition, or DebateContribution via the "Flag" action button. Each flag requires a `FlagReason` selection.

**Threshold rules:**
- 3 unique-user flags on the same entity within 24 hours → entity enters `ModerationFlag.Status = UnderReview`; a `Moderator` is notified via an in-app notification and email.
- Moderator can: `Dismiss` (all flags cleared, entity restored), `ActionTaken` (entity permanently removed or edited, flagging users get +1 XP for valid flag).
- Users cannot flag their own content. The flag system is rate-limited: 10 flags per user per 24 hours.

### 11.3 Moderator Role

```csharp
// Check in policy
services.AddAuthorization(options =>
{
    options.AddPolicy("ModeratorPolicy", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.IsInRole("Admin") ||
            ctx.User.HasClaim(c => c.Type == "ModeratorDomain")));
});
```

Moderators are scoped to a topic domain (`Moderator.TopicDomain`). A global moderator has `TopicDomain = null`.

Moderators access a dedicated `/moderation` dashboard (requires `ModeratorPolicy`) showing:
- Review queue with pending flags
- Shadow-banned Arguments awaiting permanent action
- Appeal queue

### 11.4 Shadow Scoring

Arguments with `ValidityScore < 0.3` (from AI) or ≥ 5 community flags are shadow-scored: their feed ranking score is multiplied by 0.1 (effectively hiding them), but they remain accessible via direct URL. The `IsShadowBanned` bool column is added to `Argument`.

Shadow-banned arguments:
- Do not appear in feed sorts.
- Do not contribute to the author's XP.
- Are visible to the author with a "Under Review" label.
- Are visible to Moderators in the moderation queue.

Shadow banning is reversible by a Moderator.

### 11.5 Appeals Process

A user whose Argument has been shadow-banned or removed can submit an appeal via `POST /api/moderation/appeals` with a free-text `Justification`. Appeals are reviewed by a Moderator within 7 days. If the appeal is upheld, the Argument is restored, and the Moderator who took the original action is notified.

Appeals are stored in a new `ModerationAppeal` entity (add in Sprint 4):

```csharp
public class ModerationAppeal : BaseEntity
{
    public string EntityType { get; set; } = null!;
    public Guid EntityId { get; set; }
    public string AppellantUserId { get; set; } = null!;
    public string Justification { get; set; } = null!;
    public string Status { get; set; } = "Pending";   // Pending | Upheld | Denied
    public string? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
}
```

---

## 12. Phase 2 Implementation Roadmap

### Sprint 1 (Weeks 1–2): Data Model + Voting API + Feed UI

**Objective:** Users can see a vote-ranked feed of existing Phase 1 Arguments and cast weighted votes.

**Tasks:**

| # | Task | Owner | Est. Days |
|---|---|---|---|
| 1.1 | Install pgvector extension in dev + staging + prod databases | DevOps | 0.5 |
| 1.2 | EF Core migrations: `Proposition`, `ArgumentExtensions`, `ArgumentVote`, `ArgumentLink` | Backend | 2 |
| 1.3 | `VotingService` with Wilson score, hot score, and XP award integration | Backend | 2 |
| 1.4 | `ArgumentVoteController` and `VotingHub` | Backend | 1.5 |
| 1.5 | Rate limiting middleware for vote endpoints (Redis sliding window) | Backend | 1 |
| 1.6 | Feed page (`/feed`) with sort controls and infinite scroll | Frontend | 3 |
| 1.7 | Feed card component with vote buttons and real-time VotingHub subscription | Frontend | 2 |
| 1.8 | `UserReputation` entity + `XPAwardService` + rank computation | Backend | 1.5 |
| 1.9 | BeliefSystem → Worldview data migration script | Backend | 1 |
| 1.10 | Embedding backfill job for existing Arguments (runs once, async) | Backend | 1 |
| 1.11 | Integration tests: vote CRUD, rate limiting, score computation | Backend | 1.5 |

**Acceptance criteria:**
- Feed page loads at p95 < 300ms with 1,000 seeded arguments.
- A vote cast on the feed updates the displayed score within 2 seconds without page reload.
- Duplicate votes (same user, same argument) upsert correctly.
- Rate limit (30 votes/hour) returns 429 with a `Retry-After` header.
- All existing Phase 1 Arguments are accessible in the new feed.

**Dependencies:** Redis for rate limiting must be provisioned before 1.5. pgvector must be installed before 1.2.

---

### Sprint 2 (Weeks 3–4): Argument Chain Builder + Worldview Composer

**Objective:** Users can build Argument Chains and compose Worldviews from them.

**Tasks:**

| # | Task | Owner | Est. Days |
|---|---|---|---|
| 2.1 | EF Core migrations: `ArgumentChain`, `Worldview`, `WorldviewChain`, `WorldviewVote` | Backend | 1.5 |
| 2.2 | `ArgumentChainController` and `WorldviewController` | Backend | 2 |
| 2.3 | Cycle detection for `ArgumentLink` creation | Backend | 1 |
| 2.4 | `ArgumentLinkSuggestionPlugin` with pgvector RAG | Backend/AI | 2 |
| 2.5 | Chain Builder UI (`/chains/builder`) with vis-network canvas | Frontend | 4 |
| 2.6 | AI Suggest panel in Chain Builder (calls `ArgumentLinkSuggestionPlugin`) | Frontend | 1.5 |
| 2.7 | Chain export (JSON + shareable URL) | Frontend | 1 |
| 2.8 | Worldview Composer UI (`/worldviews/composer`) | Frontend | 3 |
| 2.9 | Schwartz radar chart component (Chart.js, reusable) | Frontend | 1.5 |
| 2.10 | `ChainUpdateHub` for collaborative chain editing | Backend | 1.5 |
| 2.11 | Chain Builder real-time collaboration via `ChainUpdateHub` | Frontend | 1 |
| 2.12 | `WorldviewConvergencePlugin` (embedding + Jaccard + Schwartz) | Backend/AI | 2 |
| 2.13 | Convergence comparison view (`/convergence/compare`) enhancements | Frontend | 2 |

**Acceptance criteria:**
- A Chain with 5 Arguments and 4 links can be created, saved, and reloaded with full fidelity.
- Submitting a circular link returns 409 with a descriptive error message.
- AI link suggestions return within 5 seconds for a chain with up to 20 arguments.
- Worldview Composer loads and reorders chains without page reload.
- Convergence score between two public Worldviews is computed and displayed correctly.

**Dependencies:** Sprint 1 embeddings backfill must be substantially complete for RAG to work. Chart.js must be added to the bundle (add to `_Layout.cshtml` or lazy-loaded on relevant pages only).

---

### Sprint 3 (Weeks 5–6): Debate Room + AI Referee + SignalR Hubs

**Objective:** Two users can conduct a full structured debate in real-time with AI moderation.

**Tasks:**

| # | Task | Owner | Est. Days |
|---|---|---|---|
| 3.1 | EF Core migrations: `DebateRoom`, `DebateContribution` | Backend | 1 |
| 3.2 | `DebateRoomController` | Backend | 2 |
| 3.3 | `DebateHub` full implementation | Backend | 2 |
| 3.4 | `FallacyDetectionPlugin` | Backend/AI | 2 |
| 3.5 | AI referee integration in `DebateHub.SubmitArgument` | Backend | 1.5 |
| 3.6 | Debate Room UI (`/debates/{id}`) — split layout | Frontend | 3 |
| 3.7 | Contribution submission modal (Argument search + preview) | Frontend | 1.5 |
| 3.8 | AI referee badge rendering in UI | Frontend | 1 |
| 3.9 | Judge scoring panel | Frontend | 1.5 |
| 3.10 | Spectator mode and live join count | Frontend | 1 |
| 3.11 | Debate concluded state and winner banner | Frontend | 1 |
| 3.12 | `EpistemicProfile` migrations + `EpistemicProfileController` | Backend | 1.5 |
| 3.13 | `EpistemicScoringWorker` background service | Backend | 1.5 |
| 3.14 | E2E test: full debate session from creation to conclusion | QA | 2 |

**Acceptance criteria:**
- A complete debate session (creation → join → 3 contributions per side → judge scoring → conclusion) completes without error.
- AI referee flags appear within 5 seconds of contribution submission.
- Spectators receive live updates via SignalR without polling.
- Debate conclusions correctly award XP to both Proponent and Opponent.
- Judge role is blocked if user's Epistemic Standing < 3.0 in the debate topic.

**Dependencies:** `FallacyDetectionPlugin` requires Azure AI Foundry gpt-4o-mini endpoint configured. SignalR backplane (Azure Service Bus) should be tested in staging during this sprint.

---

### Sprint 4 (Weeks 7–8): Reputation + Gamification + pgvector RAG + Convergence Enhancements

**Objective:** Full gamification system live; Bridge Arguments generated; platform is production-ready.

**Tasks:**

| # | Task | Owner | Est. Days |
|---|---|---|---|
| 4.1 | `BadgeAwardService` with all 14 badge triggers | Backend | 2 |
| 4.2 | `ReputationController` and leaderboard endpoint | Backend | 1.5 |
| 4.3 | Profile reputation card UI (XP bar, rank, badges, radar) | Frontend | 2 |
| 4.4 | Leaderboard page (`/leaderboard`) | Frontend | 1.5 |
| 4.5 | `BridgeArgumentPlugin` with RAG + generation | Backend/AI | 3 |
| 4.6 | Bridge Arguments panel in Convergence Dashboard | Frontend | 1.5 |
| 4.7 | `Moderator`, `ModerationFlag`, `ModerationAppeal` migrations | Backend | 1 |
| 4.8 | Moderation dashboard (`/moderation`) | Backend + Frontend | 2.5 |
| 4.9 | Community flagging UI (flag button + modal) | Frontend | 1 |
| 4.10 | Shadow scoring pipeline and `AIValidationWorker` | Backend | 1.5 |
| 4.11 | Azure SignalR backplane production configuration | DevOps | 1 |
| 4.12 | WCAG 2.1 AA accessibility audit and fixes | Frontend | 2 |
| 4.13 | i18n EN/FR extension (ICU message format) | Frontend | 2 |
| 4.14 | Performance profiling — feed p95 < 300ms under load | Backend | 1.5 |
| 4.15 | Security audit: rate limiting, CSRF, ownership checks | Backend | 1.5 |
| 4.16 | Production deployment + smoke tests | DevOps | 1 |

**Acceptance criteria:**
- All 14 badges can be awarded and displayed on the profile card.
- Bridge Arguments appear in the Convergence Dashboard for any two Divergent Worldviews.
- Moderation queue correctly surfaces flagged content after 3 flags.
- Shadow-banned Arguments do not appear in the feed.
- Feed p95 latency ≤ 300ms under simulated 100 concurrent users.
- WCAG 2.1 AA audit passes with 0 critical violations.
- FR locale toggles correctly for all new UI strings.

---

## 13. Non-Functional Requirements

### 13.1 Performance

| Metric | Target | Measurement Method |
|---|---|---|
| Feed page initial load (TTFB + render) | p95 < 300ms | k6 load test, 100 VUs |
| Vote cast (HTTP + SignalR broadcast) | p95 < 200ms | k6 + custom SignalR probe |
| Convergence score computation | p95 < 2s (cached 5 min) | k6 |
| AI referee flag (FallacyDetection) | p95 < 5s | Manual timing |
| Vector similarity search (top 20) | p95 < 100ms | pgvector EXPLAIN ANALYZE |
| Chain Builder canvas render (50 nodes) | < 500ms | Lighthouse performance |

**Caching strategy:** Convergence scores are cached in Redis (key: `convergence:{idA}:{idB}`, TTL: 5 minutes). Vote tallies are cached in Redis (key: `votes:{argumentId}`, TTL: 30 seconds, invalidated on VotingHub write).

### 13.2 Scalability

- **Stateless app tier:** All session state is stored in ASP.NET Core Identity (cookie-based), or JWTs for API clients. No in-process session.
- **SignalR backplane:** Azure SignalR Service in production (configured in Sprint 4). This allows horizontal scaling of the web tier without sticky sessions.
- **Background workers:** `EpistemicScoringWorker`, `HotScoreUpdateWorker`, `AIValidationWorker` are implemented as `BackgroundService` instances. In production, these can be extracted to a separate Azure Container App worker instance to avoid CPU competition with the web tier.
- **Database connections:** Use Npgsql connection pooling (default pool size 20, configurable). Enable `Pooling=true` and `Max Pool Size=100` in the connection string for production.

### 13.3 Accessibility (WCAG 2.1 AA)

- All interactive elements reachable and activatable via keyboard (`Tab`, `Enter`, `Space`, `Escape`).
- Feed vote buttons have `aria-label` and `aria-pressed` attributes.
- vis-network canvas for Chain Builder has a keyboard-navigable fallback (table representation of nodes and edges, hidden visually, available to screen readers).
- Color is never the sole conveyor of information — all status indicators have text labels alongside color coding.
- All dialogs/modals trap focus and restore focus on close.
- ARIA live regions (`aria-live="polite"`) on vote score displays and SignalR-driven updates.
- Minimum contrast ratios: 4.5:1 for body text, 3:1 for large text and UI components — validated against the existing design system (cream `#f5f2eb` on navy `#162131` passes at approximately 14:1).

### 13.4 Internationalization

Existing pattern: localStorage key `lang` with values `en` and `fr`, toggled via a navbar button.

**Phase 2 extension:**
- All new UI strings registered in `/wwwroot/i18n/en.json` and `/wwwroot/i18n/fr.json`.
- Switch from simple key-value JSON to ICU Message Format to support pluralization (e.g., "1 upvote" vs. "5 upvotes") and gender-neutral forms.
- Use `messageformat` (npm) or `FormatJS` for client-side ICU parsing.
- Server-side: `IStringLocalizer<T>` with `.resx` files for any server-rendered strings (error messages, email templates).
- All new Razor views use `@inject IStringLocalizer<SharedResource> L` with `@L["key"]` pattern — consistent with any existing Phase 1 pattern.

### 13.5 Security

| Concern | Mitigation |
|---|---|
| Vote brigading | Redis sliding window rate limit: 30 votes/hour/user. IP-based fallback: 60 votes/hour/IP (unauthenticated). |
| CSRF on mutations | ASP.NET Core `AntiForgeryToken` on all form submissions. `[ValidateAntiForgeryToken]` attribute on all POST/PUT/DELETE controller actions. SignalR connections authenticated via the existing Identity cookie. |
| Argument ownership | `[Authorize]` + explicit ownership check in all PUT/DELETE handlers: `if (argument.UserId != currentUserId && !IsAdmin) return Forbid();` |
| SQL injection | All database access via EF Core parameterized queries or `NpgsqlParameter` objects in raw SQL. No string concatenation in queries. |
| Embedding API key | Azure AI Foundry key stored in Azure Key Vault, accessed via `DefaultAzureCredential`. Never in source code or `appsettings.json`. |
| XSS | All user-generated content rendered with Razor's default HTML-encoding. `@Html.Raw()` is not used on user content. Argument text stored as plain text, not HTML. |
| Insecure Direct Object Reference | All entity access validates ownership or public status before returning data. GUIDs are non-sequential (UUID v4) to prevent enumeration. |
| SignalR group injection | Group names are always derived from server-side validated entity IDs, never from client-provided strings. |

---

## 14. Open Questions / Decision Points

### 14.1 Embedding Model: ada-002 vs. text-embedding-3-small

**Context:** The spec assumes `text-embedding-ada-002` (1536 dimensions). Azure AI Foundry also supports `text-embedding-3-small` (1536 dims, cheaper, similar quality) and `text-embedding-3-large` (3072 dims, higher quality, more expensive).

**Trade-offs:**
- `ada-002`: Battle-tested, well-documented, existing pgvector tooling widely calibrated for 1536-dim vectors.
- `text-embedding-3-small`: ~5× cheaper per token, comparable accuracy on most benchmarks, same dimension count — minimal schema change.
- `text-embedding-3-large`: Higher quality for nuanced philosophical text (which is this platform's primary content type), but requires schema change to `vector(3072)` and doubles storage/index size.

**Decision required:** Choose before Sprint 1 embeddings backfill (1.10). Changing after backfill requires re-embedding all Arguments.

### 14.2 Graph Storage: PostgreSQL vs. Dedicated Graph Database

**Context:** `ArgumentLink` creates a graph of Arguments. The current design stores graph edges in a PostgreSQL relational table. Graph traversal (finding all Arguments reachable from a root, cycle detection) is implemented via BFS in application code or recursive CTEs in SQL.

**Trade-offs:**
- **PostgreSQL recursive CTE:** No new infrastructure. Sufficient for graphs up to ~10,000 nodes. Cycle detection works. Performance degrades above ~50 hops.
- **Neo4j / Apache AGE (PostgreSQL extension):** Native graph traversal, Cypher query language, much faster for deep traversal. Adds operational complexity (new service to manage, separate connection).
- **Apache AGE:** Runs inside PostgreSQL as an extension, no separate service. But AGE is less mature and has fewer hosted deployment options.

**Decision required:** For Phase 2 scale (estimated < 100K Arguments), PostgreSQL recursive CTEs are likely sufficient. Revisit if graph depth queries exceed 500ms at p95.

### 14.3 Real-Time Vote Updates: SignalR vs. Server-Sent Events

**Context:** `VotingHub` broadcasts vote tally updates to all subscribers of an Argument. This is a many-publisher, many-subscriber fan-out pattern.

**Trade-offs:**
- **SignalR (current choice):** Full duplex, integrates with existing SignalR infrastructure (DebateHub, ChainUpdateHub), Azure SignalR Service handles fan-out scaling. Overhead: WebSocket connection per hub.
- **Server-Sent Events (SSE):** One-way push from server to client, lower overhead than WebSocket for read-only updates (vote score display). Cannot reuse for DebateHub which needs bidirectional. Would require maintaining two real-time transport mechanisms.

**Decision required:** If vote score display is the primary real-time need and DebateHub handles the rest, SSE for votes reduces connection count. If simplicity of a single transport is preferred, stay with SignalR.

### 14.4 Argument Chain Acyclicity: Client-Side vs. Server-Side Enforcement

**Context:** Cycle detection for `ArgumentLink` must prevent circular reasoning chains. The spec proposes BFS in both the client (Sprint 2 Chain Builder UI) and the API layer.

**Trade-offs:**
- **Client + Server:** Best UX (user gets immediate feedback) + correctness guarantee. Duplicates logic.
- **Server only:** Single source of truth. Client must wait for API response to know if a link is valid. Acceptable latency (~100ms) for most use cases.
- **Database constraint:** A PostgreSQL `BEFORE INSERT` trigger could enforce acyclicity, but recursive CTE in triggers is complex and affects insert performance.

**Decision required:** Implement server-side BFS as the authoritative check. Client-side check is a UX optimization only — implement if time allows in Sprint 2.

### 14.5 Debate Room Format: Prescribed Turn Order vs. Free-Form Submission

**Context:** The spec defines Oxford and Lincoln-Douglas formats with `MaxContributionsPerSide` and `TimeLimitSeconds`. Enforcing strict turn order (Proponent speaks, then Opponent, alternating) requires server-side state management.

**Trade-offs:**
- **Strict turn order:** More faithful to real debate formats, fairer for competitive use. Requires `DebateRoom.CurrentTurnUserId` state and server-side validation in `DebateHub.SubmitArgument`. Complexity: handling user disconnects during their turn, timeout handling.
- **Free-form with MaxContributions cap:** Simpler to implement. Both sides can submit contributions in any order up to the cap. Less structured but fewer edge cases. Sufficient for a v1 Debate Room.

**Decision required:** Sprint 3 should implement free-form with cap for v1. Strict turn order is a Sprint 5+ enhancement.

### 14.6 Epistemic Score: Global vs. Per-Domain Only

**Context:** `EpistemicProfile` is scoped to `TopicDomain`. A user's vote weight in a debate about "Climate Policy" depends on their Climate Policy Epistemic Standing, not their overall reputation.

**Trade-offs:**
- **Per-domain only (current design):** Most accurate representation of domain expertise. Problem: a new user has no domain-specific history and gets weight 1.0 everywhere until they accumulate votes. A user who participates in only one domain has no weight in others.
- **Global fallback:** If `EpistemicScore` for a domain is not set (no history), fall back to the user's global Epistemic Standing (average across all domains). Prevents permanent weight=1.0 for low-activity users.
- **Rank-weighted:** Use `UserReputation.Rank` as a global multiplier (0.9 for Novice, 1.0 for Thinker, 1.1 for Reasoner, etc.) applied as a floor/ceiling modifier to domain-specific weights.

**Decision required:** Implement global fallback in Sprint 1 (`EpistemicWeight = domainScore ?? globalAverageScore ?? 1.0`).

### 14.7 AI Bridge Arguments: Display-Only vs. Editable by Users

**Context:** `BridgeArgumentPlugin` generates synthetic arguments. These are marked `IsAIGenerated = true`. The spec states they require user confirmation before becoming public.

**Trade-offs:**
- **Display-only (reference, not persistent):** Bridge arguments shown in Convergence Dashboard are ephemeral — generated on demand, not stored. Simple. No risk of AI-generated content polluting the database. Con: cannot be upvoted or chained until confirmed.
- **Stored with confirmation workflow:** Bridge arguments are stored as `Argument` entities with `IsAIGenerated = true`, `IsConfirmed = false`. Users can "claim" a bridge argument (becoming its author), edit it, and confirm it. The confirmed version enters the normal voting/chaining pipeline. Con: adds workflow complexity.
- **Auto-publish with low weight:** Bridge arguments are published directly but start with a very low `Weight` (e.g., 0.1) and are labeled "AI-Suggested." Community votes adjust the weight upward. Con: AI content in the feed may dilute quality signals.

**Decision required:** Implement the "stored with confirmation workflow" approach (Option 2) as it preserves human oversight while enabling reuse of valuable AI-generated reasoning.

### 14.8 Worldview Embedding Computation: Real-Time vs. Scheduled

**Context:** `Worldview.Embedding` is the centroid of all constituent Argument embeddings. It must be recomputed every time a Chain is added to or removed from the Worldview.

**Trade-offs:**
- **Real-time (on mutation):** Embedding is always fresh. Each `PUT /api/worldviews/{id}/chains` request triggers embedding recomputation. Adds ~100ms latency to chain add/remove operations (one API call to Azure for centroid — can be computed locally as average of existing vectors without an API call).
- **Scheduled (background job every 5 minutes):** Worldview embedding may be up to 5 minutes stale. Convergence scores computed against a stale embedding are slightly inaccurate. Simpler implementation.
- **Lazy (recompute on convergence query):** Only recompute when a convergence query is made. Cheapest in background compute. May add 200–500ms to convergence query response time.

**Decision required:** Centroid computation does not require an API call (it is a simple vector average of already-computed argument embeddings). Compute it synchronously in-process on chain add/remove at negligible cost. This is the recommended approach.

---

*End of Phase 2 Specification — Version 1.0*

*Prepared for A Common Understanding development team, June 2026.*
