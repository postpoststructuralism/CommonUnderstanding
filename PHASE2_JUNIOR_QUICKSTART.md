# Quick Start: Phase 2 for Junior Developers

Welcome! Phase 2 (social platform) is code-complete and ready for database setup. Here's what you need to know.

## Current Status
- ✅ All backend code written (models, services, controllers)
- ✅ Build passes: 0 errors
- ⏳ **YOUR TASK**: Create database migration, verify tables, build frontend UI

## 30-Second Overview

Phase 2 adds:
1. **Social Arguments**: Posts that people vote on with reasons
2. **Chains**: Multi-step arguments linked together (no circular reasoning allowed)
3. **Worldviews**: Collections of chains representing belief systems
4. **Debates**: Live structured conversations between two people with judges
5. **Reputation**: XP system, ranks (Novice → Luminary), badges

All running on 3 real-time hubs (VotingHub, DebateHub, ChainHub) + 4 background workers.

## Your First Steps (30 minutes)

### Step 1: Create Database Migration
```powershell
cd c:\Code\CommonUnderstanding\CommonUnderstanding
dotnet ef migrations add AddPhase2SocialEntities -o Data/Migrations
```

**Expected**: Creates file like `Data/Migrations/20250101120000_AddPhase2SocialEntities.cs`

### Step 2: Apply to Database
```powershell
dotnet ef database update
```

**Expected**: ~16 new tables created in PostgreSQL

### Step 3: Verify
```powershell
# List Phase 2 tables (using psql)
psql -U postgres -d [your-database-name] -c "\dt public.social_*"
psql -U postgres -d [your-database-name] -c "\dt public.argument_*"
```

**Expected Output**:
```
              List of relations
 Schema |        Name         | Type  | Owner
--------+---------------------+-------+-------
 public | argument_chains     | table | postgres
 public | argument_links      | table | postgres
 public | argument_votes      | table | postgres
 public | social_arguments    | table | postgres
 public | social_propositions | table | postgres
 public | worldviews          | table | postgres
 ... (15 total)
```

### Step 4: Test One Endpoint
```bash
# Get public feed (should return empty list, that's OK)
curl http://localhost:5000/api/feed?sort=hot

# If you get JSON back: SUCCESS! ✅
```

## Architecture Cheat Sheet

### Key Tables
- `social_arguments` — The main social object (like tweets but with reasons)
- `argument_votes` — How people vote (Up/Down) with a rationale (WellSourced, Fallacious, etc.)
- `argument_chains` — DAGs (directed acyclic graphs) linking arguments
- `worldviews` — Collections of chains; represent belief systems
- `epistemic_profiles` — User reputation scores per topic (0-5 scale)
- `user_reputations` — Global XP, rank, badges

### Key APIs to Know
| Endpoint | Purpose |
|----------|---------|
| `POST /api/arguments` | Create a social argument |
| `POST /api/arguments/{id}/votes` | Vote with a reason |
| `GET /api/feed` | Public feed (sorted by hot/wilson/recent) |
| `POST /api/argumentchains` | Create a chain |
| `GET /api/reputation/me` | See your XP, rank, badges |
| `POST /api/debaterooms` | Start a structured debate |

### Guid IDs vs Int IDs
- Phase 1 (old): Arguments use `int` IDs
- Phase 2 (new): Social arguments use `Guid` IDs
- **Why?**: Prevents conflicts, cleaner distributed systems

## Common Questions

### Q: Where's the frontend?
**A**: Not built yet. That's next. For now, test with Postman or curl.

### Q: How do voting scores work?
**A**: 
- **Wilson Score**: Conservative estimate of % upvotes (used for "Top" sort)
- **Hot Score**: Reddit-style decay: `(up - down) / (hours + 2)^1.8` (used for "Hot" sort)
- **Epistemic Weight**: Vote weight multiplier [1.0-2.0] based on user's reputation in topic

### Q: What's this "cycle detection" thing?
**A**: When someone creates an argument link (e.g., "Argument A supports Argument B"), we check if B already points back to A (directly or indirectly). If yes, reject it. Prevents circular logic.

### Q: Real-time? Like WebSocket?
**A**: Yes! SignalR hubs handle live voting, debates, and chain editing. `VotingHub` broadcasts score updates; `Phase2DebateHub` shows new contributions in real-time.

### Q: What if embedding fails?
**A**: Falls back gracefully. Embedding queries use Wilson score instead of semantic similarity.

## Frontend To-Do List

Pick one and build:

1. **Feed View** (`/Views/Social/Feed.cshtml`)
   - [ ] Razor page showing paginated public arguments
   - [ ] Real-time vote count via VotingHub
   - [ ] Sort dropdown (Hot/Wilson/Recent/Controversial)
   - [ ] Vote UI (upvote/downvote + comment box)

2. **Chain Builder** (`/Views/Social/ChainBuilder.cshtml`)
   - [ ] vis-network DAG visualization
   - [ ] Drag-and-drop to add arguments
   - [ ] Create links with type selector
   - [ ] Real-time collab via ChainUpdateHub

3. **Worldview Composer** (`/Views/Social/WorldviewComposer.cshtml`)
   - [ ] Multiselect chains
   - [ ] Schwartz radar chart (10 dimensions)
   - [ ] Convergence score display
   - [ ] Bridge argument suggestions

4. **Debate Room** (`/Views/Social/DebateRoom.cshtml`)
   - [ ] Real-time contribution list
   - [ ] AI referee flags (visual fallacy highlights)
   - [ ] Judge scoring UI
   - [ ] Conclude debate button

## If Build Fails

**Error: CS1061 - Something doesn't contain definition for 'X'**
→ Check if property exists in model class. Likely typo. Fix and rebuild.

**Error: CS0266 - Cannot implicitly convert Guid? to Guid**
→ Use `value ?? Guid.Empty` to provide default.

**Error: No assembly**
→ Ensure NuGet packages installed: `dotnet restore`

**Command**: `dotnet build 2>&1 | Select-String error` — shows all errors

## If Migration Fails

**Error: Table already exists**
→ Run `dotnet ef migrations remove`, then add again (migration should be idempotent)

**Error: PostgreSQL connection refused**
→ Check `appsettings.json` connection string. Ensure PostgreSQL is running.

**Rollback**: `dotnet ef database update [previous-migration-name]`

## Debugging Tips

### See Pending Changes in DbContext
```csharp
var entry = db.ChangeTracker.Entries().FirstOrDefault();
Console.WriteLine($"State: {entry?.State}"); // Added, Modified, etc.
```

### Check Raw SQL
```csharp
var sql = db.SocialArguments.ToQueryString();
Console.WriteLine(sql); // Shows generated SQL
```

### Log Database Activity
In `appsettings.Development.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Microsoft.EntityFrameworkCore": "Debug"
    }
  }
}
```

## File Organization

```
CommonUnderstanding/
├── Controllers/Social/          ← 5 new CRUD controllers
│   ├── SocialArgumentController.cs
│   ├── SocialPropositionController.cs
│   ├── ArgumentChainController.cs
│   ├── ReputationController.cs
│   └── FeedController.cs
├── Models/Social/               ← 16 data models
│   ├── SocialArgument.cs
│   ├── SocialProposition.cs
│   ├── ArgumentVote.cs
│   └── ... (13 more)
├── Services/Social/
│   ├── VotingService.cs
│   ├── EpistemicScoringService.cs
│   ├── XPAwardService.cs
│   ├── ... (4 more core)
│   ├── Plugins/                 ← AI integrations
│   │   ├── FallacyDetectionPlugin.cs
│   │   ├── ArgumentLinkSuggestionPlugin.cs
│   │   ├── WorldviewConvergencePlugin.cs
│   │   └── BridgeArgumentPlugin.cs
│   └── Workers/                 ← Background jobs
│       ├── HotScoreUpdateWorker.cs
│       ├── EpistemicScoringWorker.cs
│       ├── AIValidationWorker.cs
│       └── EmbeddingBackfillWorker.cs
├── Hubs/
│   ├── VotingHub.cs
│   ├── Phase2DebateHub.cs
│   └── ChainUpdateHub.cs
├── Data/
│   ├── Migrations/              ← Your migration will go here
│   └── ApplicationDbContext.cs
├── Views/Social/                ← Future: frontend
├── Program.cs                   ← All services registered
└── appsettings.json
```

## Next Steps After Migration

1. Create seed data (optional but recommended)
2. Build Feed Razor view
3. Connect Feed to VotingHub for real-time updates
4. Build Chain Builder UI
5. Test end-to-end: Create argument → Vote → Create chain → Create worldview

## Need Help?

1. Read `PHASE2_MIGRATION_GUIDE.md` (detailed migration + troubleshooting)
2. Check `PHASE2_IMPLEMENTATION_COMPLETE.md` (full technical overview)
3. Look at `Program.cs` for service registration patterns
4. Check existing Phase 1 controllers for patterns (e.g., `ArgumentController`)

---

**You've got this!** 🚀 Start with the migration, verify the tables, then pick a frontend task.

Good luck!
