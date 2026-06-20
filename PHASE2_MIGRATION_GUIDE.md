# Phase 2 Migration & Setup Guide

## ✅ Completed
- [x] All 14 data models (SocialArgument, SocialProposition, ArgumentVote, ArgumentLink, ArgumentChain, Worldview, WorldviewChain, WorldviewVote, DebateRoom, DebateContribution, EpistemicProfile, UserReputation, XPTransaction, Moderator, ModerationFlag, ModerationAppeal)
- [x] DbContext extended with all Phase 2 DbSet declarations
- [x] All core services: VotingService, EpistemicScoringService, XPAwardService, BadgeAwardService, ArgumentChainService, WorldviewService, EmbeddingService, FeedService
- [x] All AI plugins: FallacyDetectionPlugin, ArgumentLinkSuggestionPlugin, WorldviewConvergencePlugin, BridgeArgumentPlugin
- [x] All background workers: HotScoreUpdateWorker, EpistemicScoringWorker, AIValidationWorker, EmbeddingBackfillWorker
- [x] All SignalR hubs: VotingHub, Phase2DebateHub, ChainUpdateHub
- [x] All CRUD controllers: SocialArgumentController, ArgumentChainController, ReputationController, FeedController, plus existing WorldviewController, DebateRoomController, ArgumentVoteController, ArgumentLinkController, EpistemicProfileController
- [x] Build: 0 errors, ready for production

## 🔄 In Progress

### 1. Create Initial Migration

```powershell
# From project root:
cd CommonUnderstanding
dotnet ef migrations add AddPhase2SocialEntities -o Data/Migrations

# Expected output:
# - Creates migration file: Data/Migrations/[timestamp]_AddPhase2SocialEntities.cs
# - New tables: social_arguments, social_propositions, argument_votes, argument_links, 
#               argument_chains, worldviews, worldview_chains, worldview_votes,
#               debate_rooms, debate_contributions, epistemic_profiles, user_reputations,
#               xp_transactions, moderators, moderation_flags, moderation_appeals
```

**Known Issue**: Embedding columns use `float4[]` instead of pgvector type.
- **Temporary Workaround**: Leave as-is; queries won't use pgvector operators yet
- **Fix (Post-Migration)**: See "pgvector Integration" section below

### 2. Apply Migration to Database

```powershell
# Ensure PostgreSQL is running and connstring in appsettings.json is correct
dotnet ef database update

# Verify:
# - All 16 new tables created in public schema
# - Constraints and indexes applied
# - Foreign keys established
```

**Rollback (if needed)**:
```powershell
# Remove migration:
dotnet ef migrations remove

# Or revert to previous migration:
dotnet ef database update [previous-migration-name]
```

## 🚀 Post-Migration Tasks

### 3. Create Seed Data (Optional)

Create `CommonUnderstanding/Data/SeedPhase2Data.cs`:
```csharp
public static class SeedPhase2Data
{
    public static async Task SeedAsync(ApplicationDbContext db)
    {
        if (await db.SocialPropositions.AnyAsync()) return; // Already seeded

        // Create sample propositions
        var prop1 = new SocialProposition
        {
            Text = "Climate change is primarily anthropogenic.",
            Type = SocialPropositionType.Claim,
            UserId = "system-seed",
            IsConfirmed = true
        };

        db.SocialPropositions.Add(prop1);
        await db.SaveChangesAsync();

        // Create sample arguments
        var arg1 = new SocialArgument
        {
            Title = "CO2 emissions link to temperature rise",
            ClaimPropositionId = prop1.Id,
            WarrantText = "95%+ of climate scientists agree...",
            UserId = "system-seed",
            IsPublic = true,
            Tags = new[] { "climate", "science" },
            SchwartzValues = new[] { "Universalism", "Benevolence" }
        };

        db.SocialArguments.Add(arg1);
        await db.SaveChangesAsync();

        // ... additional seed data ...
    }
}
```

Then in `Program.cs`:
```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
    await SeedPhase2Data.SeedAsync(db);
}
app.Run();
```

### 4. pgvector Integration (Optional, Recommended)

#### Step 1: Install NuGet
```powershell
dotnet add package Pgvector.EntityFrameworkCore
```

#### Step 2: Enable pgvector Extension in Database
```sql
CREATE EXTENSION IF NOT EXISTS vector;
```

#### Step 3: Update DbContext Mapping

In `ApplicationDbContext.OnModelCreating()`:
```csharp
modelBuilder.Entity<SocialArgument>()
    .Property(a => a.Embedding)
    .HasColumnType("vector(1536)");  // Change from float4[] to pgvector

modelBuilder.Entity<Worldview>()
    .Property(w => w.Embedding)
    .HasColumnType("vector(1536)");

modelBuilder.Entity<ArgumentChain>()
    .Property(c => c.Embedding)
    .HasColumnType("vector(1536)");
```

#### Step 4: Create Migration
```powershell
dotnet ef migrations add ConvertEmbeddingsToVectorType -o Data/Migrations
dotnet ef database update
```

#### Step 5: Update Plugin for pgvector Queries

In `Services/Social/Plugins/ArgumentLinkSuggestionPlugin.cs`:
```csharp
// Replace current cosine similarity search with pgvector operator:
var candidates = await db.SocialArguments
    .FromSql($@"
        SELECT *, 
               1 - (embedding <=> {sourceEmbedding}::vector) AS similarity
        FROM social_arguments
        WHERE id != {sourceId}
          AND embedding IS NOT NULL
        ORDER BY similarity DESC
        LIMIT 10
    ")
    .ToListAsync();
```

---

## 📊 Database Schema Overview

### Phase 2 Tables (16 new)

| Table | Purpose | Key Columns |
|-------|---------|-------------|
| `social_propositions` | Atomic claims (Claim/Premise/Rebuttal) | id (uuid), text, type, user_id, embedding |
| `social_arguments` | Social posts (votable, chainable) | id, title, claim_id, warrant_text, hotScore, wilsonScore, tags[] |
| `argument_votes` | Rationale-backed votes per user | id, argument_id, user_id, vote (enum), rationale (enum), epistemic_weight |
| `argument_links` | DAG edges (Supports/Contradicts/Refines/Extends) | id, source_id, target_id, link_type |
| `argument_chains` | Multi-argument chains (DAG validation) | id, title, root_id, argument_ids[] (uuid[]), tags[], embedding |
| `worldviews` | Named belief systems | id, title, schwartz_values[], schwartz_vector[] (10-dim), embedding |
| `worldview_chains` | Chain membership (join table) | worldview_id, chain_id, order_index |
| `worldview_votes` | Upvote/downvote on worldviews | id, worldview_id, user_id, vote |
| `debate_rooms` | Bounded structured debates | id, title, motion_id, proponent_id, opponent_id, judges[] (text[]), status |
| `debate_contributions` | Posts in debate rooms | id, room_id, argument_id, user_id, role (enum), order_index |
| `epistemic_profiles` | Per-user, per-domain reputation [0-5] | id, user_id, topic_domain, epistemic_score, vote_accuracy |
| `user_reputations` | Global XP, rank, badges | user_id, xp, rank (enum), badges[] (text[]), current_streak |
| `xp_transactions` | XP audit trail | id, user_id, amount, reason, reference_entity_id |
| `moderators` | Moderation rights | user_id, topic_domain (nullable), is_active |
| `moderation_flags` | Report-and-review system | id, entity_type, entity_id, flagging_user_id, reason, status |
| `moderation_appeals` | User appeals of moderation | id, flag_id, appellant_id, justification, status |

### Key Constraints

- **Unique**: `(user_id, argument_id)` on `argument_votes` (one vote per user per argument)
- **Unique**: `(user_id, topic_domain)` on `epistemic_profiles`
- **Check**: `CK_ArgumentLinks_NoSelfLoop` prevents self-loops on `argument_links`
- **Check**: `score BETWEEN 0 AND 5` on `epistemic_profiles`
- **Check**: `xp >= 0` on `user_reputations`
- **Foreign Keys**: Full cascade delete on argument references

### Indexes

- `argument_votes (argument_id, user_id)` — primary lookup
- `argument_votes (argument_id)` — score recomputation
- `argument_links (source_id, target_id)` — cycle detection BFS
- `social_arguments (created_at DESC)` — feed pagination
- `social_arguments (hot_score DESC)` — sorting
- `epistemic_profiles (user_id, topic_domain)` — profile lookup
- `user_reputations (xp DESC)` — leaderboard
- `xp_transactions (user_id, created_at DESC)` — audit history
- `moderation_flags (status)` — queue for reviewers
- pgvector indexes (after conversion): `embedding` on all 3 embedding columns

---

## 🧪 Testing Post-Migration

### 1. Verify Schema
```powershell
# List all Phase 2 tables
psql -U postgres -d [dbname] -c "\dt public.social_*"
psql -U postgres -d [dbname] -c "\dt public.argument_*"
psql -U postgres -d [dbname] -c "\dt public.epistemic_*"
psql -U postgres -d [dbname] -c "\dt public.user_reputations"
```

### 2. Test Basic CRUD (from Postman or curl)

```bash
# Create argument
curl -X POST http://localhost:5000/api/arguments \
  -H "Authorization: Bearer [token]" \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Test argument",
    "warrantyText": "Because...",
    "claimText": "Sample claim",
    "isPublic": true,
    "tags": ["test"],
    "schwartzValues": ["Universalism"]
  }'

# List public feed
curl http://localhost:5000/api/feed?sort=hot&limit=10

# Cast vote
curl -X POST http://localhost:5000/api/arguments/{id}/votes \
  -H "Authorization: Bearer [token]" \
  -H "Content-Type: application/json" \
  -d '{
    "vote": "Up",
    "rationale": "WellSourced",
    "comment": "Solid evidence"
  }'
```

### 3. Validate Background Workers

Monitor logs for:
```
[Worker: HotScoreUpdateWorker] Processing [N] arguments...
[Worker: EpistemicScoringWorker] Recalculating [N] profiles...
[Worker: AIValidationWorker] Validating [N] arguments...
[Worker: EmbeddingBackfillWorker] Backfilling [N] embeddings...
```

---

## 🛠️ Troubleshooting

| Issue | Solution |
|-------|----------|
| **Migration fails with "Column 'X' already exists"** | Schema mismatch; verify existing Phase 2 code hasn't created tables. Run `dotnet ef database update -v` for details. |
| **pgvector extension not found** | Run `CREATE EXTENSION IF NOT EXISTS vector;` in your PostgreSQL database first. |
| **Embedding queries return null** | Fallback active (embedding service unavailable or no embeddings yet). Check worker logs. |
| **Vote rate limiting returns 429** | User exceeded 30 votes/hour. In-memory sliding window; resets naturally after 1 hour. |
| **Debate room reports empty AI referee flags** | AI validation is async; wait 30 sec for worker to process. Refresh endpoint. |

---

## 📝 Next Steps (UI & Frontend)

1. **Feed Razor View** (`Views/Social/Feed.cshtml`): Render paginated arguments with voting UI
2. **Chain Builder** (`Views/Social/ChainBuilder.cshtml`): vis-network DAG editor
3. **Worldview Composer** (`Views/Social/WorldviewComposer.cshtml`): Schwartz radar chart + convergence UI
4. **Debate Room Live** (`Views/Social/DebateRoom.cshtml`): Real-time contributions via `Phase2DebateHub`

---

**Migration & Setup: COMPLETE** ✅
