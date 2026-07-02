# 🏅 Common Understanding — Badge & Achievement System Plan

> **Target Audience:** Junior developer implementing this feature  
> **Status:** Design complete — ready for implementation  
> **Date:** 2026-07-01

---

## Table of Contents

1. [Philosophy & Design Goals](#1-philosophy--design-goals)
2. [Terminology](#2-terminology)
3. [Badge Categories & Full Catalog](#3-badge-categories--full-catalog)
4. [Point System (XP)](#4-point-system-xp)
5. [AI-Assisted Scoring](#5-ai-assisted-scoring)
6. [Leaderboard System](#6-leaderboard-system)
7. [Database Schema Changes](#7-database-schema-changes)
8. [API Endpoints](#8-api-endpoints)
9. [Frontend Pages](#9-frontend-pages)
10. [Implementation Order](#10-implementation-order)
11. [Testing Checklist](#11-testing-checklist)

---

## 1. Philosophy & Design Goals

### What We're Incentivizing

The badge system exists to reward **peacemakers** — users who bridge divides, find common ground, and elevate the quality of discourse. The core behaviors we want to encourage are:

| Behavior | Why It Matters |
|----------|---------------|
| **Synthesis** — finding resolutions that satisfy both sides | This is the app's raison d'être |
| **Bridge-building** — connecting opposing viewpoints | Creates the graph edges that make the system valuable |
| **Evidence quality** — citing high-tier sources | Raises the epistemic floor of the entire platform |
| **Civil engagement** — respectful rebuttals, acknowledging good points | Keeps the community healthy |
| **Consistency** — regular participation over time | Sustains the community |
| **Generosity** — upvoting others' good arguments, not just one's own | Counters polarization dynamics |

### What We're NOT Incentivizing

- **Volume for volume's sake** — no points just for posting many arguments
- **Partisan cheerleading** — no points for agreeing with your own side
- **Trolling / bad-faith posting** — shadow-ban system already handles this

### Design Principles

1. **Transparency** — every point award has a public reason and traceable reference
2. **AI + Human hybrid** — AI suggests, humans confirm; neither alone is sufficient
3. **Progressive difficulty** — early badges are easy; later ones require genuine skill
4. **Anti-gaming** — rate limits, cooldowns, and diminishing returns prevent exploitation
5. **Thematic naming** — badge names and ranks use the language of deliberation, wisdom, and bridge-building

---

## 2. Terminology

### Ranks (replaces existing Novice → Reasoner → Scholar → Sage)

| Rank | XP Threshold | Icon | Meaning |
|------|-------------|------|---------|
| **Strategist-in-Training** | 0 | 👁️ | New user, learning the platform |
| **Contender** | 200 | ✍️ | Has begun posting arguments |
| **Logician** | 1,000 | ⚖️ | Regularly engages with opposing views |
| **Architect of Logic** | 3,000 | 🔗 | Has found common ground between opposing positions |
| **Master Dialectician** | 8,000 | 🌉 | Consistently connects divided perspectives |
| **Sovereign Reasoner** | 20,000 | 🕊️ | Master of finding shared understanding |
| **Grandmaster** | 50,000 | 💡 | Platform elder — wisdom recognized by all |

> **Note:** The existing `UserReputation.Rank` field stores a string. Update `XPAwardService.ComputeRank()` to use these new thresholds and names. The old ranks (Novice, Reasoner, Scholar, Sage) are deprecated.

### Badge Tiers

| Tier | Name | Visual | Rarity |
|------|------|--------|--------|
| **Bronze** | Common | 🥉 | Awarded for first-time actions and basic milestones |
| **Silver** | Uncommon | 🥈 | Requires sustained effort or moderate skill |
| **Gold** | Rare | 🥇 | Demonstrates genuine expertise |
| **Platinum** | Epic | 💎 | Exceptional achievement, platform-wide recognition |

### Key Concepts

- **Resolution:** A user-proposed resolution that reconciles two contradictory positions. Stored as a `StructuralResolution` in the graph, but also tracked socially.
- **Logical Nexus:** An edge between two nodes that were previously only connected by contradiction. Creating a `supports` or `qualifies` edge between opposing camps.
- **Alignment Matrix:** The existing 2-user analysis showing shared and disputed propositions.
- **Objective Premise Lock:** An AI-detected pattern where opposing stakeholders unknowingly share premises (existing `PremiseLockDetector` output).

---

## 3. Badge Categories & Full Catalog

### 3.1 Onboarding Badges (Bronze — automatic)

These get users familiar with the platform's core actions.

| Badge ID | Name | Description | Trigger |
|----------|------|-------------|---------|
| `first_argument` | **First Voice** | Published your first public argument | First `SocialArgument` with `IsPublic=true` |
| `first_upvote` | **Epistemic Validation** | Upvoted someone else's argument | First `ArgumentVote` with `Vote=Up` on another user's post |
| `first_bridge` | **Nexus Point** | Connected two opposing arguments with a qualifying link | First `ArgumentLink` with `LinkType=Qualifies` between args with opposing stances |
| `profile_complete` | **Full Picture** | Completed your belief profile | `PersistedUserProfile` has ≥10 `BeliefDimension` entries |
| `first_chain` | **First Chain** | Built your first reasoning chain | First `ArgumentChain` with ≥2 links |

### 3.2 Engagement Badges (Bronze → Silver)

Reward consistent, quality participation.

| Badge ID | Name | Description | Trigger |
|----------|------|-------------|---------|
| `streak_3` | **Warm Streak** | 3 consecutive days of activity | `CurrentStreak >= 3` |
| `streak_7` | **Weekly Regular** | 7 consecutive days | `CurrentStreak >= 7` (already exists) |
| `streak_30` | **Monthly Devotion** | 30 consecutive days | `CurrentStreak >= 30` (already exists) |
| `streak_100` | **Century Mark** | 100 consecutive days | `CurrentStreak >= 100` |
| `voter_50` | **Active Citizen** | Cast 50 votes | 50 `ArgumentVote` records |
| `voter_500` | **Voice of the People** | Cast 500 votes | 500 `ArgumentVote` records |
| `commenter_10` | **Conversationalist** | Posted 10 follow-up replies | 10 `SocialArgument` records where `ParentArgumentId != null` |
| `commenter_50` | **Dialogue Master** | Posted 50 follow-up replies | 50 such records |

### 3.3 Quality Badges (Silver → Gold)

Reward argument quality as judged by the community.

| Badge ID | Name | Description | Trigger |
|----------|------|-------------|---------|
| `top_argument_25` | **Respected Voice** | An argument reached 25 upvotes | Any `SocialArgument.UpvoteCount >= 25` |
| `top_argument_100` | **Influential** | An argument reached 100 upvotes | `UpvoteCount >= 100` (already exists as `top_argument`) |
| `top_argument_500` | **Thought Leader** | An argument reached 500 upvotes | `UpvoteCount >= 500` |
| `wilson_champion` | **Quality Champion** | 3 arguments with Wilson score ≥ 0.85 | Count of user's args where `WilsonScore >= 0.85` |
| `fallacy_free_5` | **Clear Thinker** | 5 arguments validated with no fallacies | Count of user's args where `AIValidityScore >= 0.9` and `AIFallacyFlags` is empty |
| `fallacy_free_25` | **Rigorous Reasoner** | 25 fallacy-free arguments | Same, count ≥ 25 |

### 3.4 Bridge-Building Badges (Silver → Platinum) ⭐ CORE

These are the heart of the system — rewarding peacemaking behavior.

| Badge ID | Name | Description | Trigger |
|----------|------|-------------|---------|
| `bridge_1` | **Paradigm Shift** | Created 1 resolution that resolves a contradiction | 1 `StructuralResolution` authored by user (already exists as `bridge_builder`) |
| `bridge_5` | **Systemic Resolver** | Created 5 resolutions | 5 resolutions |
| `bridge_25` | **Architect of Truth** | Created 25 resolutions | 25 resolutions |
| `bridge_100` | **Graph Sovereign** | Created 100 resolutions | 100 resolutions |
| `convergence_catalyst` | **Matrix Catalyst** | Helped two users discover shared ground | `AlignmentMatrix` where user initiated the analysis and overlap ≥ 30% (already exists) |
| `convergence_10` | **Matchmaker** | Catalyzed 10 convergence discoveries | 10 such matrices |
| `harmony_spotter` | **Premise Lock** | First time an AI-detected premise lock involves your argument | User's argument appears in a `PremiseLockDetector` output |
| `cross_aisle_voter` | **Intellectual Omnivore** | Upvoted arguments from 10 different worldview clusters | Upvotes on args by users with ≥10 distinct `Worldview` clusters |
| `cross_aisle_50` | **Bipartisan Spirit** | Upvoted arguments from 50 different worldview clusters | Same, ≥50 |
| `changed_mind` | **Conqueror of Conviction** | 5 users marked "Changed My View" on your arguments | `VoteRationale.ChangedMyView` count ≥ 5 (already exists) |
| `changed_mind_25` | **Unassailable Reality** | 25 "Changed My View" rationales received | Count ≥ 25 |

### 3.5 Evidence & Epistemic Badges (Silver → Gold)

Reward rigorous use of evidence.

| Badge ID | Name | Description | Trigger |
|----------|------|-------------|---------|
| `evidence_t1` | **Gold Standard** | Cited a T1 (meta-analysis) evidence source | `EvidenceItem.Tier = T1` linked to user's argument |
| `evidence_t1_5` | **Evidence Scholar** | Cited 5 T1 sources | 5 such evidence items |
| `evidence_diverse` | **Well-Rounded** | Cited evidence from 3+ different tiers | Distinct `EvidenceItem.Tier` values ≥ 3 across user's args |
| `epistemic_expert` | **Domain Expert** | Reached epistemic score 4.0 in any domain | `EpistemicProfile.EpistemicScore >= 4.0` (already exists) |
| `epistemic_master` | **Master Reasoner** | Reached epistemic score 4.5 in 3+ domains | 3 `EpistemicProfile` records with score ≥ 4.5 |

### 3.6 Community Recognition Badges (Gold → Platinum)

Special badges that require both AI and human validation.

| Badge ID | Name | Description | Trigger |
|----------|------|-------------|---------|
| `community_pick` | **Community Pick** | An argument was featured by moderators | Manual award via admin endpoint |
| `debate_champion` | **Debate Champion** | Won a structured debate (judged by community vote) | `DebateRoom` concluded with user's side having majority vote |
| `consensus_builder` | **Consensus Builder** | Authored a resolution that 10+ users endorsed | `StructuralResolution` with ≥10 unique user endorsements |
| `rising_star` | **Rising Star** | Gained 500 XP within first 30 days | XP earned in first 30 days ≥ 500 |
| `elder` | **Platform Elder** | Active for 365+ days with ≥10,000 XP | `CreatedAt` ≥ 1 year ago AND `XP >= 10000` |

### 3.7 Hidden / Easter Egg Badges

Surprise rewards for delightful behavior.

| Badge ID | Name | Description | Trigger |
|----------|------|-------------|---------|
| `night_owl` | **Night Owl** | Posted 5 arguments between midnight-4am local time | 5 args with `CreatedAt.Hour` in [0,4) |
| `early_bird` | **Early Bird** | Posted 5 arguments between 5am-8am local time | 5 args with `CreatedAt.Hour` in [5,8) |
| `globetrotter` | **Globetrotter** | Engaged with arguments from 5+ different languages | (Future: when i18n is implemented) |
| `century_club` | **Century Club** | 100 arguments posted | 100 `SocialArgument` records |
| `millennium_club` | **Millennium Club** | 1,000 arguments posted | 1,000 `SocialArgument` records |

---

## 4. Point System (XP)

### 4.1 XP Award Table

| Action | XP | Cooldown / Limit | Reason Code |
|--------|-----|------------------|-------------|
| Post a public argument | +10 | 5/day (diminishing: +5 after 5th) | `arg_posted` |
| Argument receives an upvote | +2 | Per unique voter, no cap | `arg_upvoted` |
| Argument receives a downvote | −1 | Per unique voter (can't go below 0 per arg) | `arg_downvoted` |
| Cast an upvote on another's argument | +1 | 50/day | `vote_cast` |
| Receive "Changed My View" rationale | +25 | Per occurrence | `changed_mind` |
| Create a resolution (nexus) | +50 | 10/day | `resolution_created` |
| Resolution endorsed by another user | +10 | Per unique endorser | `resolution_endorsed` |
| Complete an alignment matrix | +30 | 5/day | `matrix_mapped` |
| Win a structured debate | +100 | Per debate | `debate_won` |
| Daily streak (day 3+) | +5 | Per day | `daily_streak` |
| Daily streak (day 7+) | +10 | Per day | `daily_streak_bonus` |
| Daily streak (day 30+) | +25 | Per day | `daily_streak_master` |
| First argument of the day | +3 | 1/day | `daily_first_arg` |
| Argument validated fallacy-free by AI | +5 | Per argument | `ai_validated` |
| Reach a new rank | +50/100/200/500 | One-time per rank tier | `rank_up` |

### 4.2 Anti-Gaming Measures

- **Vote ring detection:** If user A and user B exclusively upvote each other, reduce XP to +0 after 10 mutual votes in 24 hours.
- **Diminishing returns:** After 5 arguments/day, XP per argument drops from +10 to +5.
- **Cooldown on resolution:** Max 10 resolution XP awards per day.
- **No self-voting:** Already enforced by `VotingService`.
- **Shadow-ban integration:** Shadow-banned users earn 0 XP from all actions.

### 4.3 Rank Thresholds (Updated)

Update `XPAwardService.ComputeRank()`:

```csharp
public static string ComputeRank(long xp) => xp switch
{
    >= 50000 => "Grandmaster",
    >= 20000 => "Sovereign Reasoner",
    >= 8000  => "Master Dialectician",
    >= 3000  => "Architect of Logic",
    >= 1000  => "Logician",
    >= 200   => "Contender",
    _        => "Strategist-in-Training"
};
```

---

## 5. AI-Assisted Scoring

### 5.1 Resolution Quality Assessment

When a user proposes a resolution (nexus between contradictory positions), the AI evaluates:

1. **Novelty** (0–1): Does this resolution offer a genuinely new framing, or is it a trivial restatement?
2. **Coverage** (0–1): Does it address the core concerns of both sides?
3. **Feasibility** (0–1): Is the proposed resolution practically implementable?
4. **Coherence** (0–1): Is the reasoning internally consistent?

**Composite Score** = (Novelty × 0.25) + (Coverage × 0.35) + (Feasibility × 0.25) + (Coherence × 0.15)

- Score ≥ 0.7 → Full XP award (+50)
- Score 0.4–0.7 → Reduced XP (+25)
- Score < 0.4 → No XP (but resolution is still saved; user can improve it)

### 5.2 Nexus Quality Detection

The AI (`PremiseLockDetector` already exists) scans for:

- **Convergent Ground:** Opposing stakeholders unknowingly share premises → flag for badge eligibility
- **Complementary Chains:** Arguments that reinforce each other despite different conclusions → suggest resolution opportunity

### 5.3 Automated Badge Nomination

For Platinum-tier badges (`consensus_builder`, `elder`), the AI nominates candidates. A moderator (or community vote threshold) confirms. This prevents gaming of high-value badges.

---

## 6. Leaderboard System

### 6.1 Leaderboard Types

| Leaderboard | Sort Key | Refresh | Endpoint |
|-------------|----------|---------|----------|
| **XP Overall** | `XP DESC` | Real-time (SignalR) | `/api/reputation/xpleaderboard` (exists) |
| **Weekly XP** | XP earned in last 7 days | Hourly | `/api/reputation/weekly-leaderboard` (new) |
| **Nexus Builders** | Count of resolutions created | Hourly
| **Streak Champions** | `LongestStreak DESC` | Daily | `/api/reputation/streakleaderboard` (exists) |
| **Dialectical Mastery Index** | Weighted composite (resolutions × 2 + alignment matrices × 1.5 + changed-minds × 3) | Hourly | `/api/reputation/mastery-leaderboard` (new) |

### 6.2 Dialectical Mastery Index (DMI) Formula

This is the flagship leaderboard. It combines multiple peacemaking signals into one number:

```
DmiScore = (ResolutionCount × 2.0) 
         + (AlignmentMatricesCreated × 1.5) 
         + (ChangedMindCount × 3.0) 
         + (CrossAisleUpvotes × 0.5)
         + (ResolutionsEndorsedByOthers × 1.0)
```

- **ResolutionCount:** Number of `StructuralResolution` records authored by user
- **AlignmentMatricesCreated:** Number of `AlignmentMatrix` records initiated by user
- **ChangedMindCount:** Number of `VoteRationale.ChangedMyView` received
- **CrossAisleUpvotes:** Number of upvotes user cast on arguments from different worldview clusters
- **ResolutionsEndorsedByOthers:** Number of endorsements user's resolutions received

### 6.3 Leaderboard UI Behavior

- Top 100 displayed by default, with pagination
- Current user's position highlighted even if outside top 100
- "Friends" filter: show only users in the user's connection graph
- Weekly reset for weekly leaderboard (Sunday midnight UTC)
- SignalR hub `/hubs/reputation` pushes real-time position changes for top 10

---

## 7. Database Schema Changes

### 7.1 New Table: `ResolutionEndorsements`

Tracks user endorsements of resolutions (needed for `consensus_builder` badge and DMI Score).

```sql
CREATE TABLE "ResolutionEndorsements" (
    "Id" uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    "ResolutionId" uuid NOT NULL REFERENCES "StructuralResolutions"("Id") ON DELETE CASCADE,
    "UserId" text NOT NULL REFERENCES "UserAccounts"("Id") ON DELETE CASCADE,
    "CreatedAt" timestamptz NOT NULL DEFAULT now(),
    UNIQUE("ResolutionId", "UserId")
);
```

### 7.2 New Table: `BadgeAwardLog`

Audit trail for every badge awarded (for transparency and dispute resolution).

```sql
CREATE TABLE "BadgeAwardLog" (
    "Id" uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    "UserId" text NOT NULL REFERENCES "UserAccounts"("Id") ON DELETE CASCADE,
    "BadgeId" text NOT NULL,
    "AwardedAt" timestamptz NOT NULL DEFAULT now(),
    "TriggerSummary" text  -- e.g., "5th synthesis created (ID: abc-123)"
);
```

### 7.3 New Columns on Existing Tables

**`StructuralResolutions`** — add:
- `AuthorId` text NULL REFERENCES `UserAccounts`(`Id`) — who created this resolution
- `EndorsementCount` int NOT NULL DEFAULT 0 — denormalized counter

**`UserReputations`** — add:
- `DmiScore` double precision NOT NULL DEFAULT 0 — computed score for leaderboard

### 7.4 EF Core Entities to Create

File: `CommonUnderstanding/Models/Social/ResolutionEndorsement.cs`

```csharp
public class ResolutionEndorsement : BaseEntity
{
    public Guid ResolutionId { get; set; }
    public string UserId { get; set; } = null!;
}
```

File: `CommonUnderstanding/Models/Social/BadgeAwardLog.cs`

```csharp
public class BadgeAwardLog : BaseEntity
{
    public string UserId { get; set; } = null!;
    public string BadgeId { get; set; } = null!;
    public DateTime AwardedAt { get; set; } = DateTime.UtcNow;
    public string? TriggerSummary { get; set; }
}
```

### 7.5 Migration

Run after creating entities and updating `ApplicationDbContext`:

```bash
dotnet ef migrations add AddBadgeSystemEntities
dotnet ef database update
```

---

## 8. API Endpoints

### 8.1 New Endpoints (add to `ReputationController`)

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/api/reputation/badges` | List all badge definitions with names, descriptions, tiers |
| `GET` | `/api/reputation/badges/{badgeId}/holders` | Users who hold a specific badge |
| `GET` | `/api/reputation/weekly-leaderboard` | Top 100 by XP earned in last 7 days |
| `GET` | `/api/reputation/nexus-leaderboard` | Top 100 by resolution count |
| `GET` | `/api/reputation/mastery-leaderboard` | Top 100 by DmiScore |
| `POST` | `/api/reputation/resolutions/{resolutionId}/endorse` | Endorse a resolution (auth required) |
| `DELETE` | `/api/reputation/resolutions/{resolutionId}/endorse` | Remove endorsement |
| `GET` | `/api/reputation/resolutions/{resolutionId}/endorsements` | List users who endorsed a resolution |
| `POST` | `/api/reputation/admin/award-badge` | Manually award a badge (admin only) |

### 8.2 Updated Endpoints

- `GET /api/reputation/me` — add `dmiScore` to response
- `GET /api/reputation/users/{userId}` — add `dmiScore` to response
- `GET /api/reputation/badges/{userId}` — include badge tier and awarded-at date

### 8.3 Badge Definition Registry

Create `CommonUnderstanding/Services/Social/BadgeRegistry.cs`:

```csharp
public static class BadgeRegistry
{
    public record BadgeDefinition(string Id, string Name, string Description, string Tier);

    public static readonly Dictionary<string, BadgeDefinition> All = new()
    {
        ["first_argument"] = new("first_argument", "First Voice", "Published your first public argument", "Bronze"),
        ["first_upvote"] = new("first_upvote", "Epistemic Validation", "Upvoted someone else's argument", "Bronze"),
        // ... all badges from Section 3
        ["bridge_100"] = new("bridge_100", "Graph Sovereign", "Created 100 resolutions", "Platinum"),
    };

    public static BadgeDefinition? Get(string id) => All.TryGetValue(id, out var def) ? def : null;
}
```

This single source of truth is used by:
- `BadgeAwardService` (to know what to award)
- `ReputationController` (to return badge details)
- Frontend (to render badge names, descriptions, tiers)

---

## 9. Frontend Pages

### 9.1 Leaderboard Page

**Route:** `/reputation/leaderboard`  
**Controller:** New `ReputationViewController` (MVC, not API)  
**Template:** `Views/Reputation/Leaderboard.cshtml`

**Layout:**
```
┌──────────────────────────────────────────────────┐
│  🏆 Leaderboards                                  │
│  [XP Overall] [Weekly] [Nexus Builders] [Mastery] │
├──────────────────────────────────────────────────┤
│  #1  👤 UserName    12,500 XP    🕊️ Sovereign Reasoner │
│  #2  👤 UserName    10,200 XP    🌉 Master Dialectician│
│  #3  👤 UserName     8,900 XP    🔗 Architect of Logic │
│  ...                                              │
│  ─────────────────────────────────────────────── │
│  #42 👤 YOU          3,100 XP    🔗 Architect of Logic │  ← highlighted
└──────────────────────────────────────────────────┘
```

**Features:**
- Tabbed interface for different leaderboard types
- Current user's row highlighted with accent color
- Rank change indicators (▲1, ▼2, ●new)
- Badge icons next to usernames
- Auto-refresh via SignalR for top 10

### 9.2 Badge Gallery Page

**Route:** `/reputation/badges`  
**Template:** `Views/Reputation/BadgeGallery.cshtml`

**Layout:**
```
┌──────────────────────────────────────────────────┐
│  🏅 Badge Gallery                                 │
│  Learn how to earn each badge                     │
├──────────────────────────────────────────────────┤
│  ┌──────────┐  ┌──────────┐  ┌──────────┐       │
│  │ 🥉       │  │ 🥈       │  │ 🥇       │       │
│  │ First    │  │ Paradigm │  │ Architect│       │
│  │ Voice    │  │ Shift    │  │ of Truth │       │
│  │ ✓ earned │  │ ✓ earned │  │ 🔒 locked│       │
│  └──────────┘  └──────────┘  └──────────┘       │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐       │
│  │ 💎       │  │ 🥉       │  │ 🥈       │       │
│  │ Graph    │  │ Epistemic│  │ Active   │       │
│  │ Sovereign│  │ Validatn │  │ Citizen  │       │
│  │ 🔒 locked│  │ ✓ earned │  │ 🔒 locked│       │
│  └──────────┘  └──────────┘  └──────────┘       │
├──────────────────────────────────────────────────┤
│  Filter: [All] [Earned] [Locked] [Bronze] [Silver] │
│  [Gold] [Platinum]                                │
└──────────────────────────────────────────────────┘
```

**Features:**
- Card grid showing all badges
- Earned badges: full color, checkmark
- Locked badges: greyed out, lock icon, shows requirement on hover
- Filter by tier and earned/locked status
- Progress bar for in-progress badges (e.g., "4/5 syntheses created")

### 9.3 User Profile Badge Section

Add to existing Dashboard or a new Profile page:

```
┌─────────────────────────────────────┐
│  👤 JaneSmith                        │
│  🕊️ Sovereign Reasoner · 22,450 XP   │
│                                     │
│  🏅 Badges (12)                     │
│  [🥉 First Voice] [🥈 Paradigm Shift]│
│  [🥇 Architect of Truth] [🥈 Active │
│  Citizen] [🥉 Epistemic Validation] │
│                                     │
│  📊 Stats                           │
│  Resolutions: 27 · Matrices: 15     │
│  Changed Minds: 8 · Streak: 42 days │
└─────────────────────────────────────┘
```

### 9.4 How It Works Page

**Route:** `/reputation/how-it-works`  
**Template:** `Views/Reputation/HowItWorks.cshtml`

A static informational page explaining:
1. What the badge system is for (peacemaking incentives)
2. How XP is earned (the XP table from Section 4)
3. How ranks work (the rank table from Section 2)
4. How AI evaluates resolutions (Section 5, in plain language)
5. Anti-gaming measures (Section 4.2, in plain language)
6. FAQ (e.g., "Can I lose XP?", "How often are leaderboards updated?")

### 9.5 Navigation Updates

Add to `_Layout.cshtml` navigation (in the "People" section of the "My Work" dropdown):

```html
<li>
    <a class="dropdown-item" asp-controller="Reputation" asp-action="Leaderboard">
        <i class="bi bi-trophy me-2"></i>Leaderboards
    </a>
</li>
<li>
    <a class="dropdown-item" asp-controller="Reputation" asp-action="BadgeGallery">
        <i class="bi bi-award me-2"></i>Badge Gallery
    </a>
</li>
<li>
    <a class="dropdown-item" asp-controller="Reputation" asp-action="HowItWorks">
        <i class="bi bi-question-circle me-2"></i>How It Works
    </a>
</li>
```

---

## 10. Implementation Order

### Sprint 1: Foundation (Days 1–3)

1. **Create entities:** `ResolutionEndorsement`, `BadgeAwardLog`
2. **Update entities:** Add `AuthorId` + `EndorsementCount` to `StructuralResolution`, add `DmiScore` to `UserReputation`
3. **Create migration** and apply it
4. **Create `BadgeRegistry.cs`** with all badge definitions
5. **Update `XPAwardService.ComputeRank()`** with new rank thresholds
6. **Update `BadgeAwardService`** — add all new badge checks from Section 3
7. **Add `BadgeAwardLog`** recording to `BadgeAwardService`

### Sprint 2: Scoring & Leaderboards (Days 4–6)

8. **Create `DmiScoreService`** — computes and caches DmiScore
9. **Create `ResolutionEndorsementService`** — CRUD for endorsements
10. **Add new API endpoints** to `ReputationController` (Section 8.1)
11. **Update existing endpoints** (Section 8.2)
12. **Create `DmiScoreWorker`** — background service recalculating scores hourly
13. **Wire up XP awards** for resolution creation, endorsements, alignment matrices

### Sprint 3: Frontend (Days 7–10)

14. **Create `ReputationViewController`** (MVC controller for pages)
15. **Build `Leaderboard.cshtml`** with tabbed interface
16. **Build `BadgeGallery.cshtml`** with card grid and filters
17. **Build `HowItWorks.cshtml`** informational page
18. **Add badge display** to Dashboard / user profile area
19. **Update navigation** in `_Layout.cshtml`
20. **Add SignalR hub** `/hubs/reputation` for real-time leaderboard updates

### Sprint 4: AI Integration & Polish (Days 11–13)

21. **Create `ResolutionQualityService`** — AI evaluation of resolution quality
22. **Integrate with `StructuralResolutionService`** — call quality eval on creation
23. **Add anti-gaming measures** (vote ring detection, diminishing returns)
24. **End-to-end testing** of all badge triggers
25. **Performance testing** — ensure leaderboard queries are fast with 100k+ users

---

## 11. Testing Checklist

### Badge Triggers

- [ ] `first_argument` — awarded on first public SocialArgument
- [ ] `first_upvote` — awarded on first upvote of another user's post
- [ ] `first_bridge` — awarded on first qualifying link between opposing args
- [ ] `profile_complete` — awarded when belief profile reaches 10 dimensions
- [ ] `streak_3`, `streak_7`, `streak_30`, `streak_100` — awarded at correct thresholds
- [ ] `voter_50`, `voter_500` — awarded at correct vote counts
- [ ] `top_argument_25/100/500` — awarded at correct upvote thresholds
- [ ] `bridge_1/5/25/100` — awarded at correct synthesis counts
- [ ] `convergence_catalyst`, `convergence_10` — awarded on convergence map creation
- [ ] `cross_aisle_voter`, `cross_aisle_50` — awarded on cross-worldview voting
- [ ] `changed_mind`, `changed_mind_25` — awarded on ChangedMyView rationales
- [ ] `evidence_t1`, `evidence_t1_5`, `evidence_diverse` — awarded on evidence citation
- [ ] `epistemic_expert`, `epistemic_master` — awarded on epistemic score thresholds
- [ ] `consensus_builder` — awarded when resolution gets 10 endorsements
- [ ] `rising_star` — awarded when 500 XP earned in first 30 days
- [ ] `elder` — awarded at 365 days + 10,000 XP
- [ ] `night_owl`, `early_bird` — awarded on time-based posting patterns
- [ ] `century_club`, `millennium_club` — awarded on argument count

### XP & Rank

- [ ] XP awarded correctly for all actions in Section 4.1
- [ ] Diminishing returns kick in after 5 arguments/day
- [ ] Vote ring detection reduces XP to 0
- [ ] Shadow-banned users earn 0 XP
- [ ] Rank updates correctly at each threshold
- [ ] Rank-up bonus XP awarded once per tier

### Leaderboards

- [ ] XP leaderboard returns correct ordering
- [ ] Weekly leaderboard only counts last 7 days
- [ ] Bridge leaderboard sorts by resolution count
- [ ] Mastery leaderboard uses correct formula
- [ ] Current user highlighted in all leaderboards
- [ ] Pagination works correctly

### AI Scoring

- [ ] Resolution quality assessment returns scores in [0,1]
- [ ] Full XP awarded for score ≥ 0.7
- [ ] Reduced XP awarded for score 0.4–0.7
- [ ] No XP awarded for score < 0.4
- [ ] AI nomination triggers for Platinum badges

### Frontend

- [ ] Leaderboard page renders all tabs correctly
- [ ] Badge gallery shows earned/locked states correctly
- [ ] How It Works page is accessible and accurate
- [ ] Navigation links appear in the correct dropdown section
- [ ] Badge display on profile/dashboard shows correct badges
- [ ] SignalR updates leaderboard top 10 in real time

### Integration

- [ ] Endorsing a resolution creates `ResolutionEndorsement` record
- [ ] Endorsement increments `StructuralResolution.EndorsementCount`
- [ ] `BadgeAwardLog` records every badge award
- [ ] `XPTransaction` records every XP award with correct reason code
- [ ] `DmiScoreWorker` updates scores on schedule
- [ ] All new endpoints return correct HTTP status codes
- [ ] Auth checks work on protected endpoints

---

## Appendix A: Files to Create/Modify Summary

### New Files
| File | Purpose |
|------|---------|
| `Models/Social/ResolutionEndorsement.cs` | Entity for resolution endorsements |
| `Models/Social/BadgeAwardLog.cs` | Entity for badge award audit trail |
| `Services/Social/BadgeRegistry.cs` | Central badge definition registry |
| `Services/Social/DmiScoreService.cs` | Computes DmiScore |
| `Services/Social/ResolutionEndorsementService.cs` | CRUD for endorsements |
| `Services/Social/ResolutionQualityService.cs` | AI evaluation of resolution quality |
| `Services/Social/Workers/DmiScoreWorker.cs` | Background score recalculation |
| `Controllers/Social/ReputationViewController.cs` | MVC controller for pages |
| `Views/Reputation/Leaderboard.cshtml` | Leaderboard page |
| `Views/Reputation/BadgeGallery.cshtml` | Badge gallery page |
| `Views/Reputation/HowItWorks.cshtml` | How It Works page |
| `Hubs/ReputationHub.cs` | SignalR hub for real-time updates |

### Modified Files
| File | Change |
|------|--------|
| `Models/Social/UserReputation.cs` | Add `DmiScore` column |
| `Models/Graph/StructuralResolution.cs` | Add `AuthorId`, `EndorsementCount` |
| `Data/ApplicationDbContext.cs` | Add new DbSets |
| `Services/Social/XPAwardService.cs` | Update `ComputeRank()`, add new XP award methods |
| `Services/Social/BadgeAwardService.cs` | Add all new badge checks |
| `Controllers/Social/ReputationController.cs` | Add new endpoints |
| `Views/Shared/_Layout.cshtml` | Add nav links |
| `Program.cs` | Register new services, workers, hubs |

---

## Appendix B: Quick Reference — XP Constants

```csharp
public static class XPConstants
{
    public const int ArgPosted        = 10;
    public const int ArgPostedDimin   = 5;   // after 5/day
    public const int ArgUpvoted       = 2;
    public const int ArgDownvoted     = -1;
    public const int VoteCast         = 1;
    public const int ChangedMind      = 25;
    public const int ResolutionCreated = 50;
    public const int ResolutionEndorsed = 10;
    public const int MatrixMapped = 30;
    public const int DebateWon        = 100;
    public const int DailyStreak3     = 5;
    public const int DailyStreak7     = 10;
    public const int DailyStreak30    = 25;
    public const int DailyFirstArg    = 3;
    public const int AiValidated      = 5;

    public const int MaxArgsPerDay    = 5;   // before diminishing
    public const int MaxVotesPerDay   = 50;
    public const int MaxResolutionsPerDay = 10;
    public const int MaxMatricesPerDay    = 5;
}
```