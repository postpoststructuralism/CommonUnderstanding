# UX Redesign Plan — Evidence-Based Decision Platform

## The Problem

The current UX was designed for a **belief-discovery / worldview exploration tool**. The app's primary purpose has shifted to **organizational evidence-based decision-making**. The result is a fragmented experience where the new core workflow (submit argument → decompose → gather evidence → adjudicate → decide) is buried behind nav items designed for a different app. A user arriving today sees "Discover Your Worldview" and "Explore Beliefs" — not "Make Better Decisions."

### Current Navigation (7 items, no hierarchy)
```
Home → Explore Canon → Find Your Place → Arguments → Common Understanding → Compare → Ollama Admin
```

### Current UX Pain Points

| Problem | Severity | Detail |
|---------|----------|--------|
| **Identity crisis** | Critical | The landing page, branding, and 4 of 7 nav items are about belief systems, not decision-making |
| **No task-oriented flow** | Critical | Users must discover the workflow themselves across disconnected pages |
| **Arguments buried in nav** | High | The primary function appears as just another nav item alongside legacy features |
| **No dashboard** | High | No at-a-glance view of active decisions, pending evidence, stakeholder status |
| **Common Understanding disconnected** | High | The knowledge graph is a separate page with no visual connection to arguments |
| **Decision Support hidden** | High | Only reachable deep inside an individual argument view — no aggregate view |
| **Stakeholder experience absent** | Medium | Stakeholders must navigate to a specific argument to register positions |
| **Admin panel always visible** | Medium | Ollama Admin is an infrastructure concern shown to all users |
| **No onboarding** | Medium | New users have no guided introduction to the argument workflow |
| **Mobile-hostile argument views** | Medium | Dense evidence tables, nested collapsibles don't work well on mobile |

---

## Design Principles for the Redesign

1. **Decision-first**: Every screen serves the goal of making better organizational decisions
2. **Progressive disclosure**: Show summary → allow drill-down, never overwhelm
3. **Task-oriented**: Organize around what users *do*, not what data *exists*
4. **Transparent reasoning**: Every recommendation must show its work (evidence chain visible)
5. **Collaborative by default**: Stakeholder input is a first-class citizen, not an add-on
6. **Preserve the canon**: Belief system exploration becomes a reference tool, not the front door

---

## Proposed Information Architecture

### Primary Navigation (5 items)

```
Dashboard    Arguments    Common Understanding    Reference Library    ⚙ Settings
```

| Nav Item | Maps To | Purpose |
|----------|---------|---------|
| **Dashboard** | New page | At-a-glance status of all active decisions, pending work, org health |
| **Arguments** | Existing (enhanced) | Submit, decompose, adjudicate, decide — the core workflow |
| **Common Understanding** | Existing (enhanced) | The org's evolving knowledge graph — settled facts, contested claims |
| **Reference Library** | Existing Explore/Discovery (reframed) | Belief systems, worldviews, frameworks — reference material for context |
| **⚙ Settings** | New page | Ollama config, model switching, AI status — admin-only concerns |

### Secondary Navigation (contextual)

Within **Arguments**, a left sidebar or tab bar provides sub-navigation:
```
All Arguments → [Argument Detail] → Evidence → Stakeholders → Decision Report
```

Within **Common Understanding**, tabs or filters:
```
All Propositions | Settled | Contested | Unknown | Evidence Gaps
```

---

## Page-by-Page Redesign

### 1. Dashboard (New — replaces Home/Index)

**URL**: `/` (default route)

**Purpose**: Command center. Answer "What needs my attention?" in 5 seconds.

**Layout**:
```
┌──────────────────────────────────────────────────────────────────┐
│  Header: "A Common Understanding — Decision Support Platform"    │
├──────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌─── Active Decisions ───────────────────────────────────┐      │
│  │                                                         │      │
│  │  [Card] 4-Day Work Week         Confidence: 63%         │      │
│  │         Status: Investigating   3 stakeholders           │      │
│  │         ⚠ 2 evidence gaps       → View                  │      │
│  │                                                         │      │
│  │  [Card] WFH Policy              Confidence: 82%         │      │
│  │         Status: Ready to Decide  5 stakeholders          │      │
│  │         ✓ All premises settled   → Decision Report       │      │
│  │                                                         │      │
│  └─────────────────────────────────────────────────────────┘      │
│                                                                  │
│  ┌─── Quick Stats ─────────┐  ┌─── Needs Attention ──────┐      │
│  │  12 Arguments            │  │  3 arguments need evidence│      │
│  │  47 Propositions         │  │  2 contested premises     │      │
│  │  23 Settled / 8 Contested│  │  1 stakeholder deadlock   │      │
│  │  89 Evidence Items       │  │  → View all               │      │
│  └──────────────────────────┘  └──────────────────────────┘      │
│                                                                  │
│  ┌─── Recent Activity ─────────────────────────────────────┐      │
│  │  • Jane added T2 evidence to "WFH productivity"  2h ago │      │
│  │  • Bob registered Support on "4-Day Work Week"    4h ago │      │
│  │  • AI adjudication completed for "Cloud Migration" 1d ago│      │
│  └─────────────────────────────────────────────────────────┘      │
│                                                                  │
│  [ + Submit New Argument ]  (prominent floating action button)    │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘
```

**Key data sources**:
- `ApplicationDbContext` → all Arguments with status + AdjudicationSummary
- `CommonUnderstandingService.GetStatisticsAsync()` → graph stats
- `StakeholderService` → position counts per argument

**New controller**: `DashboardController` (replaces `HomeController.Index` as default route)

### 2. Arguments List (Enhanced)

**URL**: `/Arguments`

**Changes from current**:
- Add **filter bar**: Status (Draft/Analyzing/Complete), Recommendation (Proceed/Investigate/Defer/Reject), Date range
- Add **sort options**: By confidence, by date, by evidence count, by stakeholder count
- Each card shows a **mini confidence gauge** + recommendation badge + stakeholder consensus indicator
- Add **bulk actions**: Export, compare two arguments
- Prominent **"Submit New Argument" CTA** at top

**Argument card redesign**:
```
┌────────────────────────────────────────────────────────┐
│  4-Day Work Week                              [Complete]│
│                                                        │
│  ┌──────┐  Overall Confidence: ████████░░ 63%          │
│  │INVEST│  Premises: 3 settled, 2 contested, 1 unknown │
│  │IGATE │  Evidence: 12 items (2 T1, 4 T2, 3 T3, 3 T5)│
│  └──────┘  Stakeholders: 3 support, 1 oppose, 1 undec  │
│                                                        │
│  Submitted by Jane Smith · Mar 15, 2026                │
│  [View Analysis]  [Decision Report]  [Add Evidence]    │
└────────────────────────────────────────────────────────┘
```

### 3. Argument Detail (Redesigned — replaces current View)

**URL**: `/Arguments/{id}`

**Current problem**: Single long scrolling page with collapsible sections. Evidence forms are inline. Stakeholder panel is at the bottom.

**Redesign**: **Tabbed interface** with persistent header showing key metrics.

```
┌──────────────────────────────────────────────────────────────┐
│  ← Back to Arguments                                         │
│                                                              │
│  4-Day Work Week                         Confidence: 63%     │
│  Recommendation: INVESTIGATE             5 Stakeholders      │
│                                                              │
│  ┌────────┬───────────┬──────────────┬───────────┬─────────┐ │
│  │Overview│ Evidence  │ Stakeholders │ Decision  │ History │ │
│  └────────┴───────────┴──────────────┴───────────┴─────────┘ │
│                                                              │
│  [Tab content area]                                          │
│                                                              │
└──────────────────────────────────────────────────────────────┘
```

**Tab: Overview** (default)
- Central claim (hero card)
- Premises list with inline confidence bars (current design, kept)
- Syllogisms (current design, kept)
- Assumptions, Qualifiers, Rebuttals (current design, kept)
- Original text (collapsible, kept)

**Tab: Evidence**
- Per-premise evidence table (sortable by tier, direction, date)
- "Add Evidence" form (full-width, not a collapsible inside each premise)
- Evidence gap alerts prominently shown
- Conflicting evidence highlighted
- Auto-classify toggle

**Tab: Stakeholders**
- Current consensus bar (promoted to top)
- Position list with reasoning
- "Register Your Position" form
- Per-premise accept/reject matrix (new: checkboxes for accepted/rejected premises)
- Anonymous toggle

**Tab: Decision**
- Full Decision Support Report (currently a separate page — embed it here)
- Recommendation + confidence gauge
- Reasoning trace
- Contested premises
- Discussion topics
- Action buttons: "Accept recommendation", "Override", "Request more evidence"

**Tab: History** (new, lightweight)
- Timeline of events: submission, decomposition, evidence additions, stakeholder positions, adjudication runs
- Source: timestamps already on all entities

### 4. Submit Argument (Simplified)

**URL**: `/Arguments/Submit`

**Changes**:
- Keep the current form (it's fine)
- Replace sidebar "What Happens Next" with a simpler **stepper preview** showing the 5-step pipeline as icons
- Add a **template dropdown** (optional future enhancement): "Policy proposal", "Resource allocation", "Strategy decision"
- After submission, show a **processing screen** with a progress stepper instead of redirecting immediately

### 5. Common Understanding (Enhanced)

**URL**: `/CommonUnderstanding`

**Changes**:
- Keep the current statistics bar and grouped list
- Add a **visual graph view** tab (future: D3.js or Cytoscape.js node-link diagram)
- Add **clickable status filter tabs**: All | Settled | Contested | Unknown | Unevaluated
- Each node card links to both the proposition detail AND the source argument(s)
- Add "Evidence Gaps" as a first-class filter (propositions with 0 or very low evidence)

### 6. Reference Library (Reframed Legacy Features)

**URL**: `/Reference`

**Purpose**: The existing belief system browser, discovery journey, and comparison tools — reframed as reference material that informs decision-making rather than being the app's primary purpose.

**Sub-routes**:
```
/Reference                    → Overview of available reference frameworks
/Reference/Explore            → Browse belief systems (current Explore/Index)
/Reference/Explore/{slug}     → System detail (current Explore/System)
/Reference/Compare            → Compare systems (current Explore/Compare)
/Reference/Timeline           → Historical timeline (current Explore/Timeline)
/Reference/Discovery          → Personal worldview discovery (current Discovery/*)
/Reference/Categories         → Browse by category (current Explore/Categories)
```

**Changes**:
- New landing page at `/Reference` explaining how these frameworks inform decision-making
- All existing views preserved, just re-routed
- Removed from primary nav prominence
- Add contextual links FROM argument views: "This disagreement may reflect different moral foundations → [Explore frameworks]"

### 7. Settings (New — replaces Ollama Admin panel)

**URL**: `/Settings`

**Purpose**: Admin-only page for infrastructure configuration.

**Contents**:
- AI Provider status (current Ollama panel content)
- Model switching
- Confidence threshold configuration (future)
- Organization settings (future)

**Change**: Remove the always-visible floating Ollama panel from the layout. Replace with a small status indicator dot in the nav (green/red) that links to Settings.

---

## Layout & Branding Changes

### Header
- **Current**: "A Common Understanding" with Government of Canada logo
- **Proposed**: Keep the branding but update the tagline:
  - Current: "A Common Understanding"
  - New: "A Common Understanding — Evidence-Based Decision Platform"
- Add a subtle **AI status dot** (green/yellow/red) next to the nav, replacing the floating admin panel

### Navigation Bar
- **Current**: 7 flat items with icons
- **Proposed**: 5 items, organized by task priority
  ```
  📊 Dashboard    📋 Arguments    🔗 Common Understanding    📚 Reference    ⚙ Settings
  ```
- Active state styling: keep the current underline + highlight (it works well)
- On mobile: hamburger menu with the same 5 items

### Footer
- Keep as-is (Government of Canada branding is appropriate)

### Floating Elements
- **Remove**: Ollama Admin panel (move to Settings)
- **Remove**: Activity Monitor (move activity feed to Dashboard)
- **Keep**: Back-to-top button
- **Add**: Floating Action Button "+ New Argument" (visible on Dashboard and Arguments list)

---

## Migration Strategy

### Phase A — Restructure Navigation & Dashboard (Implement First)

| Task | Effort | Description |
|------|--------|-------------|
| A1. Create `DashboardController` + view | Medium | New landing page with argument summaries, stats, attention items |
| A2. Update `_Layout.cshtml` navigation | Small | Replace 7-item nav with new 5-item structure |
| A3. Update default route | Small | Change default route from `Home/Index` to `Dashboard/Index` |
| A4. Create `/Settings` page | Small | Move Ollama panel content to a dedicated page |
| A5. Remove floating Ollama panel | Small | Delete panel markup from `_Layout.cshtml`, add status dot |
| A6. Reroute Home to Dashboard | Small | `HomeController.Index` → redirect to Dashboard, or remove |

### Phase B — Argument Detail Tabs

| Task | Effort | Description |
|------|--------|-------------|
| B1. Redesign `View.cshtml` with Bootstrap tabs | Medium | Split current monolith into Overview/Evidence/Stakeholders/Decision/History tabs |
| B2. Move Decision Support inline | Small | Embed `DecisionSupport` content as a tab instead of separate page |
| B3. Enhance Evidence tab | Medium | Full-width evidence form, sortable table, gap alerts |
| B4. Enhance Stakeholder tab | Medium | Premise accept/reject matrix, improved consensus viz |
| B5. Add History tab | Small | Timeline from entity timestamps |

### Phase C — Arguments List & Common Understanding Enhancements

| Task | Effort | Description |
|------|--------|-------------|
| C1. Add filter/sort to Arguments index | Medium | Status, recommendation, date, confidence filters |
| C2. Redesign argument cards | Small | Mini confidence gauge, stakeholder indicator, recommendation badge |
| C3. Add status filter tabs to Common Understanding | Small | All/Settled/Contested/Unknown/Unevaluated |
| C4. Add Evidence Gaps filter | Small | Filter for under-evidenced propositions |

### Phase D — Reference Library Reframing

| Task | Effort | Description |
|------|--------|-------------|
| D1. Create `ReferenceController` | Small | Wrapper that delegates to existing Explore/Discovery controllers |
| D2. Create Reference landing page | Small | Overview of available frameworks with contextual framing |
| D3. Update routes | Small | Redirect old Explore/Discovery URLs to /Reference/* |
| D4. Add contextual links from Arguments | Small | "Explore relevant frameworks" links in argument views |

---

## Component Design Tokens (New/Updated)

### New Status Colors
```css
/* Decision recommendation colors */
--cu-proceed:     #198754;   /* Bootstrap success green */
--cu-investigate:  #0dcaf0;   /* Bootstrap info cyan */
--cu-defer:        #ffc107;   /* Bootstrap warning amber */
--cu-reject:       #dc3545;   /* Bootstrap danger red */

/* Proposition status colors */
--cu-settled:      #198754;
--cu-contested:    #ffc107;
--cu-unknown:      #6c757d;
--cu-unevaluated:  #e9ecef;
```

### New Components Needed
| Component | Purpose |
|-----------|---------|
| **Confidence Gauge** | Small circular or linear gauge (0–100%) with color coding |
| **Recommendation Badge** | Pill badge with icon: ✓ Proceed, 🔍 Investigate, ⏸ Defer, ✗ Reject |
| **Consensus Bar** | Stacked horizontal bar showing Support/Oppose/Undecided proportions |
| **Evidence Tier Badge** | Small labeled badge: T1–T6 with graded colors (dark→light) |
| **Attention Card** | Dashboard card with warning styling for items needing action |
| **Tab Navigator** | Bootstrap nav-tabs styled with GC colors for argument detail |
| **Activity Timeline** | Vertical timeline with icons for different event types |
| **Status Dot** | Tiny colored circle (6px) for AI health indicator in nav |

---

## What We're NOT Doing

- **Not building a graph visualization** (D3/Cytoscape) — that's a Phase 5 enhancement
- **Not adding authentication** — the plan preserves the current session-based approach
- **Not changing the backend services** — all changes are UI/controller-level only
- **Not removing any existing features** — everything is preserved, just reorganized
- **Not changing the GC Design System** — we extend it, not replace it
- **Not touching the AI pipeline** — decomposition, adjudication, and classification stay as-is

---

## Success Metrics

After this redesign, a new user should be able to:

1. **In 5 seconds**: Understand this is a decision-support tool (from branding + dashboard)
2. **In 30 seconds**: See active decisions and their status (dashboard)
3. **In 2 minutes**: Submit their first argument (guided submission flow)
4. **In 5 minutes**: Register their position on an existing argument (stakeholder tab)
5. **In 10 minutes**: Understand an argument's full evidence base (tabbed detail view)

The primary workflow — **Submit → Decompose → Evidence → Stakeholders → Decide** — should be obvious from the navigation alone, without needing documentation.
