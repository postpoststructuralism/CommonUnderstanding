# Comments Thread Embed & Live Feed Widget — Feature Plan

> **Status:** Planning  
> **Date:** 2026-07-02  
> **Revenue Model:** Core SaaS revenue stream — tiered subscription for embeddable comment threads powered by Common Understanding's live feed, AI analysis, and dialectical graph.

---

## 1. Executive Summary

Common Understanding already has a powerful live feed with real-time voting, structured replies, AI fallacy detection, epistemic scoring, and a dialectical understanding graph. This feature packages that capability as an **embeddable JavaScript widget** that publishers can drop onto any page to replace or augment their native comment sections.

The widget provides:
- A real-time, AI-moderated comment thread
- Live vote tallies with epistemic weighting
- AI fallacy flagging and validity scoring
- Dialectical contradiction detection across the thread
- Optional "Understanding Graph" sidebar showing how the conversation maps to broader knowledge

**Revenue model:** Freemium SaaS with tiered pricing based on page views, AI analysis depth, and moderation features.

---

## 2. Market Positioning

### 2.1 Target Customers
- **News/media sites** — replace toxic comment sections with structured, AI-moderated discourse
- **Blogs & Substacks** — add high-quality discussion without building infrastructure
- **Academic/policy platforms** — structured debate with epistemic scoring
- **Enterprise internal comms** — embed on intranet for team deliberation
- **Government consultation platforms** — public comment periods with AI synthesis

### 2.2 Competitive Landscape
| Competitor | Weakness | Our Advantage |
|---|---|---|
| Disqus | No AI analysis, toxic, ad-heavy | AI moderation, epistemic scoring, dialectical mapping |
| Facebook Comments | Walled garden, no structured reasoning | Open, structured argumentation, no social network required |
| Coral by Vox Media | Basic moderation only | Full AI fallacy detection, contradiction mapping, synthesis |
| Discourse | Forum, not embeddable thread | Lightweight embed, real-time SignalR, AI-powered |
| Commento | Privacy-focused but barebones | Rich AI features, understanding graph integration |

### 2.3 Unique Selling Points
1. **AI-moderated civility** — fallacy detection, shadow-banning, epistemic weight
2. **Structured reasoning** — not just "comments" but claims with warrants and resolutions
3. **Live dialectical map** — see how comments relate (support/contradict/refine) in real-time
4. **Cross-thread intelligence** — contradictions detected across different articles on the same site
5. **Zero infrastructure** — a single `<script>` tag, fully hosted

---

## 3. Architecture Overview

```
┌──────────────────────────────────────────────────────────────────┐
│                     PUBLISHER'S WEBSITE                          │
│  ┌────────────────────────────────────────────────────────────┐  │
│  │  <div id="cu-comments" data-site="pub123" data-page="/xyz">│  │
│  │  <script src="https://cdn.commonunderstanding.com/widget.js">│  │
│  └────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌──────────────────────────────────────────────────────────────────┐
│                   COMMON UNDERSTANDING CLOUD                      │
│                                                                   │
│  ┌─────────────┐  ┌──────────────┐  ┌────────────────────────┐  │
│  │ Widget API   │  │ SignalR Hub   │  │ AI Analysis Pipeline   │  │
│  │ (REST)       │  │ (WebSocket)   │  │ (Background Workers)   │  │
│  │              │  │               │  │                        │  │
│  │ POST /thread │  │ /hubs/widget  │  │ • Fallacy detection    │  │
│  │ GET  /thread │  │               │  │ • Contradiction map    │  │
│  │ POST /reply  │  │ Groups:       │  │ • Sentiment analysis   │  │
│  │ POST /vote   │  │ thread-{id}   │  │ • Epistemic scoring    │  │
│  └─────────────┘  └──────────────┘  └────────────────────────┘  │
│                                                                   │
│  ┌─────────────────────────────────────────────────────────────┐ │
│  │                 Existing Social Infrastructure               │ │
│  │  SocialArguments • ArgumentVotes • ArgumentLinks             │ │
│  │  VotingService • FeedService • FollowUpArgumentService      │ │
│  │  EpistemicScoringService • SocialArgumentAnalysisService    │ │
│  └─────────────────────────────────────────────────────────────┘ │
│                                                                   │
│  ┌─────────────────────────────────────────────────────────────┐ │
│  │                 Understanding Graph (Phase 3)                 │ │
│  │  UnderstandingNode • UnderstandingEdge • DialecticalPairs    │ │
│  │  SchemaDiscovery • BlindspotDetection • Syntheses            │ │
│  └─────────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────────┘
```

---

## 4. Database Schema Additions

### 4.1 New Tables

```sql
-- Publisher/site registration
CREATE TABLE "CommentSites" (
    "Id"              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "OwnerUserId"     TEXT NOT NULL REFERENCES "UserAccounts"("Id"),
    "Domain"          TEXT NOT NULL,
    "SiteName"        TEXT NOT NULL,
    "PlanTier"        TEXT NOT NULL DEFAULT 'free',
    "ApiKey"          TEXT NOT NULL UNIQUE,
    "AllowedOrigins"  TEXT[] NOT NULL DEFAULT '{}',
    "ModerationMode"  TEXT NOT NULL DEFAULT 'ai',
    "CustomCssUrl"    TEXT,
    "LogoUrl"         TEXT,
    "IsActive"        BOOLEAN NOT NULL DEFAULT true,
    "CreatedAt"       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt"       TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Thread = a comment section on a specific page
CREATE TABLE "CommentThreads" (
    "Id"              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "SiteId"          UUID NOT NULL REFERENCES "CommentSites"("Id"),
    "PageUrl"         TEXT NOT NULL,
    "PageTitle"       TEXT,
    "ThreadSlug"      TEXT NOT NULL,
    "IsLocked"        BOOLEAN NOT NULL DEFAULT false,
    "IsModerated"     BOOLEAN NOT NULL DEFAULT false,
    "SortOrder"       TEXT NOT NULL DEFAULT 'hot',
    "TotalComments"   INT NOT NULL DEFAULT 0,
    "CreatedAt"       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt"       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE("SiteId", "ThreadSlug")
);

-- Maps a thread's SocialArguments to the thread
CREATE TABLE "ThreadArguments" (
    "ThreadId"        UUID NOT NULL REFERENCES "CommentThreads"("Id"),
    "ArgumentId"      UUID NOT NULL REFERENCES "SocialArguments"("Id"),
    "IsTopLevel"      BOOLEAN NOT NULL DEFAULT true,
    "SortOrder"       INT NOT NULL DEFAULT 0,
    "CreatedAt"       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY ("ThreadId", "ArgumentId")
);

-- Cross-thread contradiction detection results
CREATE TABLE "ThreadContradictions" (
    "Id"              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "SiteId"          UUID NOT NULL REFERENCES "CommentSites"("Id"),
    "ThreadIdA"       UUID NOT NULL REFERENCES "CommentThreads"("Id"),
    "ThreadIdB"       UUID NOT NULL REFERENCES "CommentThreads"("Id"),
    "ArgumentIdA"     UUID NOT NULL REFERENCES "SocialArguments"("Id"),
    "ArgumentIdB"     UUID NOT NULL REFERENCES "SocialArguments"("Id"),
    "ContradictionType" TEXT NOT NULL,
    "Confidence"      DOUBLE PRECISION NOT NULL DEFAULT 0.5,
    "Explanation"     TEXT,
    "IsResolved"      BOOLEAN NOT NULL DEFAULT false,
    "DetectedAt"      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "ResolvedAt"      TIMESTAMPTZ
);

-- Usage tracking for billing
CREATE TABLE "WidgetUsage" (
    "Id"              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "SiteId"          UUID NOT NULL REFERENCES "CommentSites"("Id"),
    "Date"            DATE NOT NULL,
    "PageViews"       BIGINT NOT NULL DEFAULT 0,
    "CommentsPosted"  INT NOT NULL DEFAULT 0,
    "VotesCast"       INT NOT NULL DEFAULT 0,
    "AiAnalysesRun"   INT NOT NULL DEFAULT 0,
    "BandwidthBytes"  BIGINT NOT NULL DEFAULT 0,
    UNIQUE("SiteId", "Date")
);

-- Moderation queue for manual/hybrid mode
CREATE TABLE "CommentModerationQueue" (
    "Id"              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "SiteId"          UUID NOT NULL REFERENCES "CommentSites"("Id"),
    "ArgumentId"      UUID NOT NULL REFERENCES "SocialArguments"("Id"),
    "Status"          TEXT NOT NULL DEFAULT 'pending',
    "FlagReason"      TEXT,
    "AiConfidence"    DOUBLE PRECISION,
    "ReviewedByUserId" TEXT REFERENCES "UserAccounts"("Id"),
    "ReviewedAt"      TIMESTAMPTZ,
    "CreatedAt"       TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX "IX_CommentThreads_SiteId" ON "CommentThreads"("SiteId");
CREATE INDEX "IX_CommentThreads_ThreadSlug" ON "CommentThreads"("ThreadSlug");
CREATE INDEX "IX_ThreadArguments_ThreadId" ON "ThreadArguments"("ThreadId");
CREATE INDEX "IX_ThreadArguments_ArgumentId" ON "ThreadArguments"("ArgumentId");
CREATE INDEX "IX_WidgetUsage_SiteId_Date" ON "WidgetUsage"("SiteId", "Date");
CREATE INDEX "IX_CommentModerationQueue_SiteId_Status" ON "CommentModerationQueue"("SiteId", "Status");
```

### 4.2 Modifications to Existing Tables

```sql
ALTER TABLE "SocialArguments" 
ADD COLUMN "ThreadId" UUID REFERENCES "CommentThreads"("Id");

ALTER TABLE "SocialArguments"
ADD COLUMN "SiteId" UUID REFERENCES "CommentSites"("Id");

CREATE INDEX "IX_SocialArguments_ThreadId" ON "SocialArguments"("ThreadId");
CREATE INDEX "IX_SocialArguments_SiteId" ON "SocialArguments"("SiteId");
```

---

## 5. API Design

### 5.1 Widget REST API

All endpoints are CORS-enabled for registered origins. Authentication: API key in `X-CU-API-Key` header or Bearer token for logged-in users.

```
Base URL: https://api.commonunderstanding.com/v1/widget
```

#### Thread Management

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/thread?site={key}&page={url}` | API Key | Get or create thread for a page |
| `GET` | `/thread/{id}/comments` | Optional | Get comments with sort/filter |
| `POST` | `/thread/{id}/comments` | User* | Post a new top-level comment |
| `POST` | `/thread/{id}/comments/{commentId}/replies` | User* | Reply to a comment |
| `GET` | `/thread/{id}/contradictions` | Optional | Get detected contradictions in thread |
| `GET` | `/thread/{id}/summary` | Optional | AI-generated thread summary |
| `POST` | `/thread/{id}/subscribe` | User | Subscribe to thread notifications |

#### Voting

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `POST` | `/comments/{id}/vote` | User* | Cast vote (up/down + rationale) |
| `DELETE` | `/comments/{id}/vote` | User* | Revoke vote |

#### Moderation (Pro/Enterprise)

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/site/{id}/queue` | Site Admin | Get moderation queue |
| `POST` | `/site/{id}/queue/{itemId}/approve` | Site Admin | Approve flagged comment |
| `POST` | `/site/{id}/queue/{itemId}/reject` | Site Admin | Reject flagged comment |
| `POST` | `/thread/{id}/lock` | Site Admin | Lock thread |
| `POST` | `/thread/{id}/unlock` | Site Admin | Unlock thread |

#### Analytics (Pro/Enterprise)

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/site/{id}/analytics` | Site Admin | Usage stats, engagement metrics |
| `GET` | `/site/{id}/insights` | Site Admin | AI insights: top contradictions, sentiment trends |

*\* User auth: anonymous commenting supported on Free tier; Pro/Enterprise can require auth.*

### 5.2 SignalR Hub — Real-Time Updates

```
Endpoint: /hubs/widget
```

**Groups:** `thread-{threadId}`

**Server → Client Messages:**

| Message | Payload | Description |
|---------|---------|-------------|
| `CommentAdded` | `{ threadId, comment }` | New comment posted |
| `ReplyAdded` | `{ threadId, parentId, reply }` | New reply to a comment |
| `VoteScoreUpdated` | `{ commentId, tally }` | Vote tally changed |
| `CommentFlagged` | `{ commentId, flags }` | AI flagged a comment |
| `CommentRemoved` | `{ commentId, reason }` | Comment moderated/removed |
| `ContradictionDetected` | `{ pair }` | New contradiction found in thread |
| `ThreadLocked` | `{ threadId }` | Thread was locked |
| `UserCountUpdated` | `{ threadId, count }` | Active viewer count |

**Client → Server Methods:**

| Method | Auth | Description |
|--------|------|-------------|
| `SubscribeToThread(threadId)` | None | Join thread group |
| `UnsubscribeFromThread(threadId)` | None | Leave thread group |
| `CastVote(commentId, vote, rationale)` | User | Cast a vote |
| `RevokeVote(commentId)` | User | Revoke a vote |

---

## 6. Widget Frontend (JavaScript SDK)

### 6.1 Embed Snippet

```html
<!-- Minimal embed -->
<div id="cu-comments" 
     data-site="YOUR_SITE_KEY"
     data-page="https://example.com/article/123">
</div>
<script async src="https://cdn.commonunderstanding.com/widget/v1/cu-comments.js"></script>
```

### 6.2 Configuration Options

```html
<div id="cu-comments"
     data-site="YOUR_SITE_KEY"
     data-page="https://example.com/article/123"
     data-theme="light"
     data-sort="hot"
     data-max-depth="5"
     data-require-auth="false"
     data-show-graph="true"
     data-locale="en"
     data-custom-css="https://example.com/cu-theme.css">
</div>
```

### 6.3 JavaScript API

```javascript
// Programmatic API (also available via window.CUComments)
const cu = window.CUComments;

// Initialize manually
cu.init({
  container: '#my-comments',
  site: 'YOUR_SITE_KEY',
  page: window.location.href,
  theme: 'light'
});

// Events
cu.on('commentPosted', (comment) => { /* analytics */ });
cu.on('voteCast', (vote) => { /* analytics */ });
cu.on('ready', () => { /* widget loaded */ });

// Methods
cu.getThread().then(thread => { /* ... */ });
cu.refresh();
cu.destroy();
```

### 6.4 Widget UI Components

The widget renders as a self-contained shadow DOM component with:

1. **Header bar** — comment count, sort selector (Hot/Top/New/Controversial), subscribe button
2. **Comment list** — threaded replies with:
   - User avatar, name, timestamp
   - Claim text with warrant/resolution
   - AI validity badge (Validated / Flagged)
   - Vote buttons with rationale selector
   - Reply count and expand/collapse
   - Wilson score confidence bar
3. **Comment composer** — text area with:
   - Claim + warrant fields (structured mode) or free text (simple mode)
   - Tag selector
   - Preview of AI analysis before posting
4. **Understanding Graph sidebar** (optional) — shows:
   - Dialectical pairs within the thread
   - Cross-thread contradictions
   - "Where this conversation fits" in the broader knowledge graph

---

## 7. Pricing Tiers

### 7.1 Free Tier
- Up to 1,000 page views/month
- Up to 3 active threads
- Basic AI moderation (fallacy detection only)
- "Powered by Common Understanding" branding
- Community support

### 7.2 Pro Tier — $49/month
- Up to 50,000 page views/month
- Unlimited threads
- Full AI analysis (fallacy + contradiction + sentiment)
- Manual moderation queue
- Custom CSS theming
- Remove branding
- Email support
- Basic analytics dashboard

### 7.3 Enterprise Tier — $299/month
- Up to 500,000 page views/month
- Everything in Pro
- Cross-thread contradiction intelligence
- Understanding Graph sidebar for readers
- SSO / custom auth integration
- API access for custom integrations
- Priority support + SLA
- White-label option
- Dedicated infrastructure

### 7.4 Custom / High Volume
- 1M+ page views/month
- Custom AI model fine-tuning on publisher's content
- On-premises deployment option
- Dedicated account manager

---

## 8. Implementation Phases

### Phase A: Core Widget (Weeks 1-3)
**Goal:** Embeddable comment thread with basic functionality

1. **Database migration** — Create CommentSites, CommentThreads, ThreadArguments tables; add ThreadId/SiteId to SocialArguments
2. **WidgetController** — REST API for thread CRUD, comment posting, reply threading
3. **WidgetHub** — SignalR hub for real-time comment/vote updates per thread
4. **JavaScript SDK v0.1** — Minimal embed script that renders a comment thread in an iframe
5. **Site registration dashboard** — Simple page for publishers to register, get API key, configure origins
6. **CORS middleware** — Validate API keys and allowed origins for cross-origin requests

### Phase B: AI Integration (Weeks 4-5)
**Goal:** AI-powered moderation and analysis in widget threads

1. **AI analysis pipeline for widget comments** — Run SocialArgumentAnalysisService on each new comment (fallacy detection, validity scoring)
2. **Auto-moderation worker** — Background service that flags low-validity comments, pushes to moderation queue
3. **Thread contradiction detection** — Use existing DialecticalSynthesisService to find contradictions within a thread
4. **AI summary generation** — Generate thread summaries using the LLM pipeline
5. **Real-time flagging** — Broadcast AI flags to connected clients via WidgetHub

### Phase C: Publisher Dashboard (Weeks 6-7)
**Goal:** Full self-service publisher experience

1. **Dashboard UI** — Site management, thread browser, analytics, moderation queue
2. **Analytics engine** — Track WidgetUsage, generate charts (comments over time, vote distribution, AI flag rate)
3. **Moderation UI** — Queue management with approve/reject, bulk actions, moderator role assignment
4. **Customization panel** — CSS editor, theme picker, logo upload, branding toggle
5. **Billing integration** — Stripe subscription management, usage-based overage tracking

### Phase D: Advanced Features (Weeks 8-10)
**Goal:** Differentiated enterprise features

1. **Cross-thread contradiction intelligence** — Background worker that compares embeddings across threads on the same site, surfaces contradictions
2. **Understanding Graph sidebar** — Embeddable graph visualization showing dialectical relationships
3. **SSO integration** — Support for publisher's existing auth (OAuth2, SAML)
4. **Webhook notifications** — Fire webhooks on new comments, flags, contradictions
5. **Export API** — Allow publishers to export their comment data

### Phase E: Scale & Polish (Weeks 11-12)
**Goal:** Production readiness

1. **CDN deployment** — Serve widget JS from Azure CDN with cache-busting
2. **Rate limiting** — Per-site and per-IP rate limiting on comment/vote endpoints
3. **Load testing** — Verify 10K+ concurrent WebSocket connections per node
4. **Monitoring & alerting** — Application Insights dashboards for widget API
5. **Documentation** — Publisher integration guide, API reference, SDK docs

---

## 9. New Code Files Required

### 9.1 Backend (C# / ASP.NET Core)

| File | Purpose |
|------|---------|
| `Controllers/WidgetController.cs` | REST API for widget threads, comments, moderation |
| `Hubs/WidgetHub.cs` | SignalR hub for real-time thread updates |
| `Services/Widget/ThreadService.cs` | Thread creation, comment management, slug generation |
| `Services/Widget/WidgetModerationService.cs` | AI flagging, moderation queue management |
| `Services/Widget/WidgetAnalyticsService.cs` | Usage tracking, billing metrics |
| `Services/Widget/CrossThreadContradictionWorker.cs` | Background service for cross-thread analysis |
| `Services/Widget/ApiKeyAuthenticationHandler.cs` | Custom auth handler for API key validation |
| `Models/Widget/CommentSite.cs` | EF Core entity for CommentSites table |
| `Models/Widget/CommentThread.cs` | EF Core entity for CommentThreads table |
| `Models/Widget/ThreadArgument.cs` | EF Core entity for ThreadArguments table |
| `Models/Widget/ThreadContradiction.cs` | EF Core entity for ThreadContradictions table |
| `Models/Widget/WidgetUsage.cs` | EF Core entity for WidgetUsage table |
| `Models/Widget/CommentModerationItem.cs` | EF Core entity for CommentModerationQueue table |
| `Models/Widget/DTOs/*.cs` | Request/response DTOs for widget API |
| `Migrations/*_WidgetEmbed.cs` | EF Core migration |

### 9.2 Frontend (JavaScript)

| File | Purpose |
|------|---------|
| `wwwroot/widget/v1/cu-comments.js` | Main widget loader script |
| `wwwroot/widget/v1/cu-comments.css` | Widget styles (light + dark themes) |
| `wwwroot/widget/v1/signalr-client.js` | SignalR client for real-time updates |
| `wwwroot/widget/v1/components/ThreadView.js` | Comment list with threading |
| `wwwroot/widget/v1/components/CommentComposer.js` | Comment input form |
| `wwwroot/widget/v1/components/VoteWidget.js` | Vote buttons with rationale modal |
| `wwwroot/widget/v1/components/GraphSidebar.js` | Understanding Graph mini-view |
| `wwwroot/widget/v1/components/AuthModal.js` | Login/signup modal |
| `wwwroot/widget/v1/i18n/en.js` | English locale strings |
| `wwwroot/widget/v1/i18n/fr.js` | French locale strings |

### 9.3 Views (Razor Pages for Dashboard)

| File | Purpose |
|------|---------|
| `Views/Widget/Dashboard.cshtml` | Publisher dashboard home |
| `Views/Widget/Sites.cshtml` | Site management list |
| `Views/Widget/SiteSettings.cshtml` | Per-site configuration |
| `Views/Widget/Moderation.cshtml` | Moderation queue |
| `Views/Widget/Analytics.cshtml` | Analytics dashboard |
| `Views/Widget/Billing.cshtml` | Subscription management |

---

## 10. Key Design Decisions

### 10.1 Reuse Existing SocialArgument Infrastructure
The widget does NOT create a separate "comments" table. Instead, widget comments are `SocialArgument` entities linked to a `CommentThread` via `ThreadArguments`. This means:
- All existing voting, scoring, and analysis infrastructure works immediately
- Widget comments appear in the main feed (if public) — driving network effects
- Reply threading uses the existing `ArgumentLink` with `LinkType.Reply`
- AI fallacy detection and epistemic scoring apply automatically

### 10.2 iframe vs Shadow DOM
**Decision: Shadow DOM** for the widget UI. Rationale:
- Inherits publisher page styles where desired, overrides where needed
- No cross-origin iframe communication complexity
- SignalR WebSocket works in the main page context
- Better SEO (comments are in the page DOM, not hidden in an iframe)
- Smaller bundle size (no iframe boilerplate)

### 10.3 API Key Authentication
Each `CommentSite` gets a unique API key. The widget includes this key in:
- `X-CU-API-Key` header for REST calls
- Query string parameter for SignalR connection (`?api_key=xxx`)
- The server validates the key against `AllowedOrigins` to prevent unauthorized embedding

### 10.4 Anonymous vs Authenticated Users
- **Free tier:** Anonymous commenting allowed (rate-limited by IP)
- **Pro tier:** Can require CU account login to comment
- **Enterprise tier:** Can integrate with publisher's existing auth via OAuth2/SAML
- Anonymous users get a session-scoped pseudonymous identity for vote tracking

### 10.5 AI Analysis Cost Management
- Free tier: Basic fallacy detection only (cheapest model)
- Pro tier: Full analysis with contradiction detection
- Enterprise: Cross-thread analysis, custom models
- AI calls are tracked in `WidgetUsage.AiAnalysesRun` for billing

---

## 11. Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| AI analysis latency slows comment posting | Poor UX | Post comment immediately, run AI async, update via SignalR when analysis completes |
| Abusive/spam comments on free tier | Brand damage | AI auto-flagging + rate limiting + IP-based shadow banning |
| High AI costs on free tier | Margin erosion | Strict free tier limits; fallback to rule-based checks if AI quota exceeded |
| Cross-origin SignalR issues | Broken real-time | Use WebSocket transport with CORS; fallback to long-polling |
| Publisher wants to migrate from Disqus | Churn risk | Build Disqus import tool as part of onboarding |
| GDPR/privacy compliance | Legal risk | Data processing agreement templates; EU data residency option for Enterprise |

---

## 12. Success Metrics

| Metric | Target (Month 3) | Target (Month 12) |
|--------|-------------------|---------------------|
| Registered publisher sites | 50 | 500 |
| Monthly active threads | 200 | 5,000 |
| Comments posted/month | 5,000 | 250,000 |
| Pro/Enterprise conversion rate | 5% | 8% |
| Monthly recurring revenue | $2,500 | $50,000 |
| Widget load time (p95) | < 500ms | < 300ms |
| AI flag accuracy (publisher feedback) | > 85% | > 95% |

---

## 13. Open Questions

1. **Should widget comments be indexed by search engines?** If yes, we need SSR or pre-rendering. If no, shadow DOM is sufficient.
2. **Should we support WordPress/Drupal plugins?** Native plugins would dramatically increase adoption but add maintenance burden.
3. **Should we offer a "community" tier for non-profits?** Aligns with mission but needs cost modeling.
4. **How do we handle comment migration from Disqus/Facebook?** Import tool needed for enterprise sales.
5. **Should the widget support media embeds (images, tweets)?** Rich media increases engagement but also moderation complexity.

---

*Plan prepared by GitHub Copilot based on analysis of the existing Common Understanding codebase, including the SocialArgument infrastructure, SignalR hubs, AI analysis pipeline, and Understanding Graph architecture.*