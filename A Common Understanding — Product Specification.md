# A Common Understanding — Product Specification

**Version:** 1.0
**Date:** July 15, 2026
**Status:** Handoff to development
**Owner:** Product / Engineering

---

## 0. Purpose of this document

This specification captures the product vision, the gaps between the current implementation and that vision, and a phased plan of work to close them. It is the source of truth for the next 6–12 months of development on [common-understanding-v2.azurewebsites.net](https://common-understanding-v2.azurewebsites.net/).

It is written to be actionable: every phase has explicit deliverables, acceptance criteria ("gates"), and a mapping back to the vision it serves. Where the current build diverges from the vision, the divergence is named and the remediation is specified.

Read Sections 1–3 for context. Sections 4–8 are the work plan. Section 9 is the immediate week-one hotfix list. Section 10 is cross-cutting concerns (security, legal, accessibility) inherited from the pre-production audit.

---

## 1. Vision

Common Understanding is a **social reasoning platform** where every discussion contributes to a growing map of collective understanding.

People interact naturally — posting and responding to arguments in plain language — while AI continuously analyzes claims, evidence, assumptions, and points of agreement or disagreement. Instead of conversations disappearing into endless comment threads, they produce **living knowledge**: shared understanding, unresolved questions, and emergent conclusions that evolve as new perspectives are added.

The project is a response to the observed dysfunctions of contemporary social media: echo chambers, mutual distrust, algorithmic manipulation, the futility of debate, and the amplification of users' baser instincts. The core bet is that people come to social platforms seeking an outlet for reasoning and connection, and that a platform which channels that impulse toward **insight, shared goals, and collective action** — rather than dunking, outrage, and performance — can win their sustained attention.

The reference model is closer to **Wikipedia** than to Twitter, Reddit, Kialo, or any B2B decision-intelligence tool. The output is a durable public good: an evolving map of what humans collectively understand, contest, and are still figuring out.

## 2. Product principles

These principles govern every design and engineering decision. When a proposed feature conflicts with a principle, the principle wins.

1. **The map is the product.** The feed is a view onto the map, not the primary artifact. Every interaction should visibly contribute to the map's growth or refinement.
2. **Natural input, structured output.** Users write in plain language. The AI does the ontological work of extracting claims, premises, assumptions, and connections. Users are never asked to fill out structured forms.
3. **Every contribution changes something visible.** After posting, users see exactly what shifted on the map. No contribution is silent.
4. **Reward mind-changing, not engagement.** Reputation, ranking, and visibility are anchored to behaviors that improve collective understanding — not to attention, virality, or activity volume.
5. **No engagement algorithms.** Sorting and surfacing logic is chronological, structural, or explicit-user-choice. Never optimized for time-on-site. Ranking logic is publicly documented.
6. **Every reply is an argument.** There is no plain-comment primitive that bypasses AI structural analysis. Shortcuts around the AI are shortcuts back to Twitter.
7. **Sovereign AI, transparent processing.** Inference runs on infrastructure the project controls. What the AI sees, stores, and produces is documented, reviewable, and correctable.
8. **The commons is open.** Code is open source. Map data is openly licensed. Governance is public. This is not negotiable — closed platforms lose the trust required for the vision to work.
9. **Bilingual by default.** English and French parity is a first-class requirement, not a v2 feature.

## 3. Current state assessment

### 3.1 What is working well

- **Submission UX is correct in shape.** A single free-text field with a natural-language prompt ("What do you believe, and why?") is architecturally aligned with the vision. This is the hardest thing to get right and it is right.
- **AI pipeline is genuinely novel.** The 5-step streaming decomposition (extract claim → decompose structure → assess premises → surface assumptions → identify rebuttals) is more sophisticated than anything shipping in comparable consumer platforms.
- **Auto-linking to existing arguments** works when the pipeline completes — the "References" panel shows the map connection.
- **Graph Evolution / snapshot system** is the correct underlying primitive for the "living knowledge" artifact. It is currently buried as a technical stats page but the data is there.
- **Local-LLM commitment (Ollama + Semantic Kernel)** is the correct trust story for the topics the platform must handle credibly.

### 3.2 Critical failures blocking the vision

1. **The AI backend is broken in production.** Submissions currently fail with `HTTP 404 DeploymentNotFound`. Arguments are saved as "Draft" and never analyzed. Every new user's first experience is the opposite of the promised product. **This is a P0 incident, not a feature request.**
2. **The homepage is a Reddit-style feed.** Hot/Top/New/Controversial sort tabs, algorithmically-shaped attention. This is the dysfunction the platform exists to escape, currently occupying its front door.
3. **The post-submission moment is a debug trace.** Users see "Step 2/5: Decomposing argument structure & assessing premises" instead of a narrative of what changed on the map. The dopamine moment the vision requires does not exist.
4. **The map is hidden.** `/UnderstandingGraph`, `/CommonUnderstanding`, and `/EmergentConclusions` — the actual product — are three clicks deep behind a feed.
5. **Reputation system mirrors social media.** XP, badges, leaderboards. Directly opposed to Principle 4.
6. **Reply comments bypass the AI.** The "Optional comment…" textarea on arguments is a plain comment field. This is where Twitter dynamics will re-emerge.
7. **Seed/test data is in production.** "West Coast party" arguments, a debate titled "a" with topic "a", worldview chains named "Random thoughts" — visible to anyone who lands on the site.
8. **AI confidence badge ("AI: 30%") is a scarlet letter.** Uninterpretable, visible on every card, actively undermines contributors.
9. **Privacy Notice contradicts hosting reality.** Claims local processing / no external transmission while running on Azure App Service.
10. **French toggle does nothing.** For a Canadian-context platform, this is a legitimacy failure.

### 3.3 Category context

The consumer deliberation platform graveyard is large: Kialo's public side is now dormant, Arguman/Socratrees/Debate Map/DebateGraph are stalled or abandoned. Every prior attempt required users to do the ontological work themselves, which capped growth at hobbyist-and-academic scale. **The generative-AI-does-the-structuring architecture is the reason this attempt can work where prior ones did not.** That architecture is only valuable if the AI actually runs, produces useful output, and is presented to users as magic rather than as a debug log.

---

## 4. Phase 1 — Close the post-submission loop

**Timeline:** Weeks 1–4
**Objective:** A single user, posting a single argument, has an experience that feels *better* than posting the same thought on Twitter — because they see their contribution visibly change a shared map.
**Gate to Phase 2:** 20 real users post arguments and, in unprompted feedback or interview, describe the post-submission "what changed" moment as the best part of the product.

### 4.1 Fix the AI backend (P0)

- Diagnose and resolve the `DeploymentNotFound` error currently returned by the analysis pipeline.
- Add a **fallback provider chain**: if the primary provider returns an error, the pipeline transparently retries against a secondary provider before user-visible failure. Ollama on project-controlled infrastructure is the required end-of-chain fallback (aligns with Principle 7).
- Add **health checks** for every provider in the chain, running every 60 seconds. Alert on failure. Public status page.
- Add a **submission queue**. If all providers fail, the argument is queued (not dropped, not stranded as "Draft"). User is notified: "We'll analyze this shortly and email you when it's ready." Retry with exponential backoff.
- Add **structured logging and error tracking** (App Insights or equivalent). Every pipeline step, every provider call, every failure. No silent failures.
- Add **rate limiting and abuse protection** on the AI pipeline. A single user cannot exhaust the compute budget.
- **Acceptance:** 99% of submissions complete analysis within 60 seconds. 0% of submissions are stranded in "Draft" state. Provider failures are invisible to users unless the entire chain is down, in which case the queue path activates cleanly.

### 4.2 Rewrite the streaming log as narrative

Replace the current debug-style step output with human-readable narration. Same underlying 5 steps, same streaming behavior, different words.

**Current output:**
```
[8:02:17] Connecting to streaming analysis endpoint…
[8:02:18] Step 0/5: Starting AI decomposition…
[8:02:20] Step 1/5: Extracting central claim…
[8:02:20] Step 2/5: Decomposing argument structure & assessing premises…
```

**Replacement output (indicative, subject to copy refinement):**
```
Reading what you wrote…
Your central claim looks like: "Remote work makes federal employees more productive."
Looking at your reasoning — I see three supporting points…
Checking whether anyone else on the map has argued this before…
Found 4 related arguments, including 2 that push back. Pulling them in…
```

- Copy is written from the AI's perspective in first person or neutral voice — never from a server log's perspective.
- No jargon: no "decomposition," "premises," "defeaters," "qualifiers" visible to end users at this stage. Those terms belong on the detail page where they can be explained on hover.
- Progress is communicated by content, not by "Step N/5" indicators.
- **Acceptance:** A non-technical user reads the streaming output and understands what the system is doing at each step.

### 4.3 Add the "what changed" screen

After analysis completes, users land on a new screen — not the argument detail page — that shows the map delta caused by their contribution.

**Required elements:**

- **Headline:** "Here's what your argument added to the map."
- **New propositions created:** Count and list of any new propositions the AI extracted that didn't previously exist on the map.
- **Existing propositions strengthened:** Which existing claims your argument now supports, with links.
- **Existing propositions contested:** Which existing claims your argument now pushes back on, with links.
- **Areas of agreement joined:** "You now agree with N other contributors on X."
- **New questions opened:** Any assumptions the AI surfaced that aren't yet supported by evidence on the map.
- **The strongest opposing view:** If the argument has strong existing counter-arguments, show the single strongest one prominently. Copy: "Worth reading — here's the strongest version of the opposing view." (Serves Principle 4 anti-echo-chamber mechanism.)
- **Primary CTA:** "See it on the map" (navigates to the map view centered on the user's contribution).
- **Secondary CTA:** "View the full analysis" (navigates to the argument detail page).

**Acceptance:** In user testing, the "what changed" screen is the most-commented-on aspect of the flow. Users describe feeling that their contribution mattered.

### 4.4 Add lightweight review-before-publish

The AI will get structural analysis wrong sometimes. Users need to correct it before publication, or they will stop trusting the platform when a wrong extraction goes live under their name.

- After the streaming analysis completes and *before* the "what changed" screen, show a brief confirmation: **"Does this look right?"**
- Display the extracted central claim and top-level premises inline-editable.
- Two buttons: **"Yes, publish"** (default, primary) and **"Let me fix this"** (opens the extracted structure for inline edit).
- If the user edits, the corrected structure is what publishes. The corrections are logged as training signal for future pipeline improvement.
- **Acceptance:** Users can correct the AI's extraction in under 30 seconds. Correction rate is tracked; a >20% correction rate indicates the AI pipeline needs improvement (Phase 3 signal).

### 4.5 Kill or reframe the "AI: 30%" confidence badge

The current badge is uninterpretable and appears as a scarlet letter on every argument card.

**Options, in order of preference:**

1. **Remove entirely.** Confidence in an argument's empirical strength is not a signal that belongs on a card in a browsing view. It belongs deep in the detail page's analysis panel, if anywhere.
2. **Replace with an interpretable label.** Categorical, not numeric: *"Well-supported by cited evidence"* / *"Based on personal experience"* / *"Needs evidence"* / *"Value-based claim"*. The AI already produces argument type ("empirical," "normative") — extend that classification into a support-quality dimension.
3. **Keep only on the detail page,** with a hover explanation of what the number means and how it was computed.

Default recommendation: **Option 1** for card views, **Option 3** for the detail page's analysis panel.

**Acceptance:** No numeric AI confidence percentage is visible on any card, feed item, or list view.

### 4.6 Purge test and seed data

- Delete or hide all seed arguments (the "West Coast party" content, sequential fake UUIDs `b0000000-0000-0000-0000-00000000000N`).
- Delete the debate titled "a" with topic "a."
- Delete or rename worldview chains "Random thoughts" and "Partying on the flat earth."
- Establish a process: no test data ships to the production database. Test/staging environments only.
- **Acceptance:** A first-time visitor sees no obviously-fake content anywhere.

---

## 5. Phase 2 — Make the map the product

**Timeline:** Weeks 4–10 (overlaps late Phase 1)
**Objective:** The map, not the feed, is the front door. Every user session starts and ends on the map. The feed is one view among many, not the default.
**Gate to Phase 3:** New users, in unprompted feedback, describe the product as "a map" or "a living document" — not as "a debate site" or "Reddit-but-better."

### 5.1 Move the map to `/`

- The homepage becomes a browsable, zoomable view of the current state of collective understanding.
- Design reference: Wikipedia's front page (curated + browsable) crossed with a semantic map view.
- **Required elements:**
  - **Featured questions:** 3–6 currently-active questions with high recent map movement, hand-curated or algorithmically surfaced by *structural* signals (recent updates, new evidence added, contested-to-settled transitions) — never by engagement.
  - **Map regions:** Zoomable clusters of related propositions. Color or visual weight indicates settled / contested / open.
  - **Recently updated:** Chronological list of propositions where the map state has recently shifted, with a one-line description of what changed.
  - **Contribute here:** Contextual entry points into submission from any region of the map.
- The current `/UnderstandingGraph` and `/CommonUnderstanding` views are the starting material. Rebuild the homepage from those primitives.

### 5.2 Add inline "what do you think?" input on propositions and map regions

- On every proposition detail page and every map region view, a single-textarea input labeled contextually (e.g., "What do you think about this?" or "Add your perspective on remote work productivity").
- Same submission pipeline as the current `/Argument/Submit` page — but contextually attached to *this thing the user is looking at*.
- The AI uses the contextual anchor as a hint for reference-linking (this argument is likely to relate to the proposition the user was viewing).
- **Acceptance:** A user who lands on a proposition page can contribute without navigating away.

### 5.3 Demote the feed

- Rename the current feed something like **"Latest Contributions"** or **"Recent Activity"**.
- Move it out of the homepage slot. Accessible from the nav but not the default landing.
- **Remove the Hot and Controversial sorts.** These are engagement primitives that violate Principle 5.
- Keep **New** (chronological, transparent).
- Add **Recently updated on the map** (structural signal — where has the map moved most recently).
- Add **Underrepresented views** (explicit surfacing logic — arguments with few contributors on their side of a contested proposition).
- Publish the ranking logic for each sort in a public document linked from the sort selector.

### 5.4 Rebuild the argument detail page as "the state of this claim"

The current detail page is a Reddit post with an analysis sidebar. Invert the hierarchy.

**Required layout, top to bottom:**

1. **The current best understanding of the claim.** What's settled, what's contested, what evidence exists on each side, what remains unknown. Auto-generated from the map state around this proposition, refreshed on each contribution.
2. **The strongest arguments on each side.** Two columns: strongest supporting, strongest opposing. Each is a link to the full contributing argument.
3. **Evidence linked to this claim.** Sources, citations, uploaded documents, links.
4. **Remaining questions.** Assumptions that have been surfaced but not yet supported or contested.
5. **Contribution history.** Chronological list of arguments that have shaped this proposition. This is the "revision history" analog to Wikipedia's View History — the provenance of the current understanding.
6. **Add your perspective.** Inline submission input (from 5.2).

- **Acceptance:** A reader who lands on a detail page can understand the current state of the debate in under 60 seconds without scrolling into individual comments.

### 5.5 Elevate Graph Evolution as the map's version history

- The `/UnderstandingGraph/Evolution` page is currently a technical stats display. Reframe it as **"How this understanding evolved"** — accessible from every proposition and map region, not just as a top-level admin view.
- Each snapshot becomes a **narrative moment**: "On July 8, evidence X was added, shifting Proposition Y from Contested to Leaning-Settled."
- Add a **"State of the Map" weekly digest**: automated newsletter or on-site publication drawn from the past week's snapshot deltas. Serves Principle 3 (every contribution changes something visible) at aggregate scale.

---

## 6. Phase 3 — Anti-dysfunction mechanisms

**Timeline:** Weeks 8–16 (overlaps late Phase 2)
**Objective:** Engineer explicitly against each dysfunction the platform exists to address. Each mechanism is a specific piece of software, not a policy or a vibe.
**Gate to Phase 4:** Red-team exercise. Three antagonistic users spend a week attempting to reproduce Twitter-style dynamics. They fail.

### 6.1 Echo chambers → structural counter-argument surfacing

- On every proposition view, the strongest opposing argument is rendered at the same visual weight as the strongest supporting argument. Not below the fold, not in a collapsed section, not behind a tab.
- On submission, if the AI detects strong existing counter-arguments the user has not viewed, surface them in the "what changed" screen (already specified in 4.3): *"Before you finalize, here's the strongest version of the opposing view — worth reading."*
- **Do not force the user to read them.** Surfacing is enough. Coercion breaks trust.

### 6.2 Mutual distrust → mind-change tracking

Replace the current XP / badges / leaderboard system with a mind-change tracking system.

**Data model additions:**

- **Position:** A user's current stance on a proposition (supports / opposes / uncertain / no position). Positions are created implicitly by the user's submitted arguments and can be updated explicitly.
- **Position update event:** A record of a user changing their position on a proposition, timestamped, with the argument(s) they read between old and new position.
- **Attribution:** When a position update occurs and a specific argument was read between old and new position, credit is attributed to that argument's author.

**User-visible surfaces:**

- On argument detail pages: *"12 contributors have updated their position after reading this argument."* Click-through to (anonymized or opt-in-named) list of update events.
- On user profiles: aggregate mind-change contributions. This is the reputation signal.
- On the map: propositions with high recent update activity are visually flagged as "shifting."

**Gaming resistance:**

- Position updates require a minimum time-between-changes (e.g., 24 hours) to prevent farming.
- Sock-puppet detection (rate-limit new accounts, IP correlation, behavioral signals).
- Attribution requires *reading* the argument, not merely being exposed to it (measured by dwell time, not just view event).
- Position updates on your own arguments do not count.
- **Do not display any user's "score" as a leaderboard.** Mind-change is a contribution signal, not a competition. No top-10 view.

**Deprecate:**

- XP system: remove.
- Badges: remove or repurpose as behavior recognition (e.g., "Regularly contributes to underrepresented views") — never as scoreboard.
- Leaderboards: remove.

### 6.3 Pointlessness of debate → living version history

- Every proposition and every map region has a visible history view (from 5.5).
- Weekly "State of the Map" digest published publicly.
- On contribution, the "what changed" screen (4.3) shows the immediate delta. On aggregate, users see their contributions accumulated on their profile: *"Your arguments have influenced N positions on M propositions."*
- Debate stops feeling pointless when contributors can see the map visibly move because of them.

### 6.4 Algorithmic manipulation → transparent, published sort logic

- Remove Hot and Controversial sorts (specified in 5.3).
- All remaining sorts are chronological, structural, or user-explicit.
- Publish the sort logic in a public document: what each sort surfaces, what data it uses, what it does not do.
- On the sort selector, a small info icon links to that document.
- **Positioning language for public communication:** *"We don't rank content to maximize your engagement. Here's exactly how we surface arguments."*

### 6.5 Base instincts → all replies route through AI decomposition

- **Remove the "Optional comment…" textarea** on argument replies. This is the current bypass around the AI structural analysis.
- Every response to an argument goes through the same submission pipeline as a top-level argument: natural-language input → AI decomposition → structured contribution to the map.
- The AI analysis for replies uses the parent argument as context, biasing reference-linking toward the parent's map region.
- If a user types content the AI cannot productively decompose (e.g., "you're an idiot"), the response is not silently accepted. Options:
  - The AI offers to help articulate the claim underneath: *"I couldn't find a clear argument in what you wrote. Are you trying to say [attempted extraction]?"*
  - If no productive extraction is possible after one retry, the input is not published. User is shown the input with an explanation.
- **This is deliberate friction.** Users who want to dunk go elsewhere; users who want to reason stay. This is the Wikipedia edit-interface pattern applied to social interaction.

**Acceptance for Phase 3:** Red-team exercise. Recruit three users (paid or volunteer) with an explicit incentive to make the platform feel like Twitter. If after one week they have not been able to reproduce dunking, pile-on, or engagement-farming dynamics, the mechanisms are working.

---

## 7. Phase 4 — Legitimacy and durability

**Timeline:** Months 4–12 (starts once Phase 3 mechanisms are live)
**Objective:** The platform becomes a thing that serious people (journalists, academics, policy researchers, civic organizations) trust enough to cite, contribute to, and defend. Wikipedia earned this over a decade; the goal here is enough of it in year one that the project survives its first controversy.
**Gate to Phase 5:** A journalist, academic, or policy researcher cites the map as a source in a published piece, unprompted.

### 7.1 Legal structure decision

Choose one, before public launch scales:

- **Canadian nonprofit** (federally or provincially incorporated).
- **US 501(c)(3)** (if targeting cross-border grant funding).
- **Public-benefit corporation** (Canadian BC/Nova Scotia PBC or US Delaware PBC — allows commercial activity with legally-binding mission).
- **Hosted under an existing foundation** (e.g., Code for Canada, an academic institution, a civic tech umbrella).

Each has different implications for funding pathways, IP ownership, and governance. Legal counsel required. **Deliverable:** a written recommendation memo with the chosen structure and rationale.

### 7.2 Governance policy v1

Draft and publish, before public launch:

- **Content policy.** What contributions are welcome, what are not, what the platform's stance is on controversial topics (analog to Wikipedia's NPOV).
- **Dispute resolution.** How disagreements about the map's state are resolved. Who has authority. Appeals process.
- **AI analysis review.** How users can appeal or correct the AI's structural extraction after publication. Who reviews. What the SLA is.
- **Sanctions and enforcement.** What behaviors result in restrictions, how they are applied, appeals.
- **Governance of governance.** How the policies themselves are updated. Community input mechanisms.

Draft publicly (on GitHub or an equivalent transparent forum). Solicit input from early contributors before finalizing.

### 7.3 AI transparency documentation

Publish:

- **What model runs.** Name, version, where it runs (Canadian-hosted infrastructure detail).
- **What it sees.** Inputs, context, prior map state.
- **What it stores.** Logs, retention windows, deletion mechanisms.
- **What it produces.** Structural extraction, confidence signals, reference links.
- **How to correct it.** User-facing appeals and edit paths.

This is a first-class asset, not a policy footnote. Linked from the homepage footer, referenced in the Privacy Notice.

### 7.4 Reconcile the Privacy Notice contradiction

The current Privacy Notice claims local processing while the app runs on Azure App Service with the AI backend currently pointing at a hosted OpenAI-compatible deployment.

Two acceptable resolutions, one unacceptable:

- **Acceptable:** Move AI inference to project-controlled infrastructure (Ollama on Canadian-region VMs, or a sovereign-hosted deployment). Update the Privacy Notice to accurately describe the new architecture. This is the aligned resolution and supports Principle 7.
- **Acceptable:** Update the Privacy Notice to accurately describe the current hosted-AI architecture and what data is sent where. Loses the sovereignty positioning but is honest.
- **Unacceptable:** Leave the current claim in place while running hosted AI.

**Recommendation:** the first option, because sovereign AI is a defensible differentiator for the target audience.

### 7.5 Open source and open data

- **Code:** open source under AGPL or a similar copyleft license (choice depends on the funding structure from 7.1).
- **Map data:** open license (CC-BY-SA is the Wikipedia analog and the default recommendation).
- **API:** public read API for the map. Researchers, journalists, and third-party tools can query the map.
- **Contribution:** external contributors can propose changes via the standard open-source workflow.

Kialo's failure to open its code and data is [explicitly cited](https://de.wikipedia.org/wiki/Kialo) as a reason it was not adopted at the science-policy interface. Do not repeat that mistake.

### 7.6 Funding path

- **Not acceptable:** advertising, engagement-optimized monetization, algorithmic feed monetization. These *are* the dysfunctions.
- **Acceptable sustaining revenue:**
  - **Grants** from mission-aligned foundations: Knight, Mozilla, Ford, MacArthur, Omidyar, Canadian civic-tech funders (Digital.gc.ca, Code for Canada, Inspirit).
  - **Hosted private instances** for organizations (federal departments, unions, municipalities, universities) that want a private map that can optionally merge findings back to the public commons. This is where the previous B2B analysis is still useful — as sustaining revenue, not as the product.
  - **Donations** from users who value the commons (Wikipedia model).
  - **Institutional partnerships** with academic institutions, libraries, public broadcasters.

**Deliverable:** a funding strategy memo covering the nonprofit/PBC decision, grant targets with application deadlines, and enterprise-hosting revenue model.

### 7.7 Bilingual parity

The French language toggle currently does nothing. This is a launch blocker for a Canadian-context platform.

- Full EN/FR parity across UI copy, submission prompts, AI narrative output, governance documents, Privacy Notice.
- AI pipeline must accept and analyze French input at parity with English input. This is a model choice constraint — verify the fallback chain (7.1 / 4.1) includes French-capable models.
- The map itself is inherently multilingual: propositions may have contributors in either language, and the AI must be able to link a French argument to an English proposition when they concern the same claim.
- **Acceptance:** A monolingual French speaker can complete every user flow without encountering English.

---

## 8. Phase 5 — Scale the commons

**Timeline:** Year 2+
**Objective:** Extend the map across topics, languages, and communities without losing coherence.
**Precondition:** Phase 4 gate met. Do not begin scaling before institutional legitimacy is established.

Rather than a detailed spec at this stage, the design principles for scaling:

- **Add topics one at a time**, each with a founding steward from the community.
- **Language expansion follows quality, not ambition.** French parity is a launch requirement (Phase 4.7); further languages are added only when a language-community founding cohort exists.
- **Federated / self-hosted instances** for organizations that want private maps with optional public-commons merge. Enables the sustaining revenue model from 7.6.
- **Cross-map synthesis:** when the same claim appears in multiple topic maps, the AI surfaces the connection. Emergent conclusions can span topics.
- **Community governance scaling:** as the contributor base grows, governance evolves from founder-driven to community-elected. This is the Wikipedia trajectory; plan for it explicitly.

Full Phase 5 specification to be authored at Phase 4 exit.

---

## 9. Week-one hotfix list

Independent of the phased plan, the following must be resolved immediately because they actively damage the product right now:

1. **Fix the AI pipeline `DeploymentNotFound` error.** Every submission currently fails. P0 incident.
2. **Add a queue path** so failed submissions are not stranded as "Draft" forever. Users are notified when analysis completes.
3. **Purge production seed/test data.** "West Coast party" arguments, sequential fake UUIDs, "a"/"a" debate, "Random thoughts" chains.
4. **Rewrite the streaming analysis log copy** from debug output to natural narrative (specified in 4.2). Even a first pass is a large improvement.
5. **Move the map view** (`/UnderstandingGraph` or `/CommonUnderstanding`) into the primary navigation as a prominent, first-class destination — even before the full homepage rebuild in 5.1.
6. **Reconcile or remove the Privacy Notice local-processing claim** (7.4). Ongoing misstatement is a legal exposure.
7. **Disable or fix the French toggle.** A visible non-functional button is worse than no button.

---

## 10. Cross-cutting concerns (inherited from pre-production audit)

These items are not phase-specific but must be resolved before public launch scales beyond the founding contributor cohort. They are grouped from the earlier pre-production audit and remain valid.

### 10.1 Security

- Add security headers: `Content-Security-Policy`, `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`.
- Extend HSTS `max-age` to at least 31536000 (1 year).
- Strip the `Server: Kestrel` header.
- Add CAPTCHA on registration.
- Add rate limiting on registration, login, and AI submission endpoints.
- Gate `/Dashboard/Index` and any other endpoint currently exposing system metrics behind authentication.
- Remove the duplicate SignalR dependency (both `@microsoft/signalr@latest` and the Cloudflare CDN version are currently loaded).

### 10.2 Legal and compliance

- Create Terms of Service page. Wire the footer link (currently `href="#"`).
- Create Contact page. Wire the footer link.
- Add cookie consent banner (PIPEDA and GDPR compliance).
- Implement the data-deletion workflow the Privacy Notice promises.
- Add password reset / forgot-password flow.
- Consider email verification on registration.

### 10.3 Accessibility

- Fix heading hierarchy (currently jumps H1 → H5/H6 with no intermediates).
- Add `aria-label`s to vote and action buttons.
- Add `<label>` elements to the tag filter and other unlabeled inputs.
- Add a skip-to-content link.
- Ensure color is not the only signal for argument type or confidence.
- Verify screen reader compatibility across all primary user flows.

### 10.4 SEO and discoverability

- Add favicon.
- Create `robots.txt` and `sitemap.xml` (both currently return 404).
- Add meta description, Open Graph, and Twitter Card tags to all pages.
- Add canonical URL tags.

### 10.5 Monitoring and operations

- Add analytics (privacy-respecting: Plausible, Fathom, or self-hosted alternative — not Google Analytics, which conflicts with Principle 8).
- Add error tracking (Sentry, Raygun, or Azure Application Insights).
- Add uptime monitoring with a public status page.
- Add structured logging on the AI pipeline (specified in 4.1).

### 10.6 Missing standard pages

- About page (mission, team, structure, funding — grows from Phase 4 deliverables).
- Help / FAQ / getting-started documentation.
- Landing / marketing page (arguably subsumed by the map-as-homepage in 5.1, but a distinct "learn about the project" page is still required).

---

## 11. What the current build should keep

Named explicitly, so that no one on the team deletes or de-prioritizes these while executing the plan:

- The single free-text submission textarea and its natural-language prompt.
- The 5-step AI decomposition pipeline architecture (fix the deployment error but keep the design).
- The auto-linking of new arguments to existing propositions.
- The premise-level analysis on the argument detail page (in the reworked layout from 5.4, this data becomes the raw material for "the current best understanding").
- Hidden-assumption surfacing.
- Rebuttal and defeater identification.
- The `/UnderstandingGraph/Evolution` snapshot system (elevate per 5.5).
- The `/CommonUnderstanding/Index` propositions view (becomes core homepage material per 5.1).
- The `/EmergentConclusions` engine.
- The Ollama + Semantic Kernel foundation (harden per 4.1 and 7.4).
- The Blazor / ASP.NET Core / SignalR technical stack — no rewrite required; all changes above are additive or subtractive within the existing stack.

---

## 12. What the current build should remove or radically rebuild

Named explicitly, to avoid ambiguity:

- **Homepage as social feed** — rebuilt as map view (5.1).
- **Hot and Controversial sort tabs** — deleted (5.3).
- **XP, badges, leaderboards** — deleted, replaced by mind-change tracking (6.2).
- **"Optional comment…" reply textarea** — deleted, replaced by AI-routed replies (6.5).
- **"AI: 30%" confidence badges on cards** — deleted or reframed (4.5).
- **Test / seed data** — purged (4.6).
- **Reference Library of religions and political systems** — descope for now; unfocused and not serving the vision. Revisit in Phase 5 if a use case emerges.
- **Argument submission as a separate destination page** — replaced by contextual inline inputs (5.2). The `/Argument/Submit` route can remain as a fallback entry point but is no longer the primary path.
- **The Privacy Notice's local-processing claim as currently worded** — reconciled per 7.4.
- **Streaming analysis debug log** — rewritten as narrative (4.2).
- **Argument detail page as Reddit-post-with-sidebar** — rebuilt as "state of the claim" (5.4).

---

## 13. Success metrics

Not vanity metrics (DAU, time-on-site, argument count). Mission-aligned metrics:

- **Mind-change events per week.** The primary product metric. Rising = product is working.
- **Propositions transitioning between states** (open → contested → leaning-settled → settled, or reverse). Map liveness signal.
- **New evidence links added per proposition per week.** Depth signal.
- **Return contribution rate.** Percentage of contributors who post again within 30 days. Retention that reflects genuine value, not engagement-farming.
- **AI extraction correction rate.** Percentage of submissions where the user edits the AI's extraction. Trust and pipeline-quality signal.
- **Cross-position reading rate.** Percentage of contributors who view arguments opposing their position. Anti-echo-chamber signal.
- **External citation count.** Number of times the map or a specific proposition is cited by an external publication (journalism, academic paper, policy document). Legitimacy signal — the Phase 4 gate metric.

Explicitly **not tracked as success metrics:** DAU, session duration, page views, argument volume, vote counts, comment counts. These are the metrics social media optimizes and they are not aligned with the vision.

---

## 14. Open questions for founder decision

These require founder / product-owner decisions before or during Phase 1:

1. **Wedge topic.** Is the platform launching with a curated seed topic (recommended for cold-start density), or general-purpose from day one? If curated, which topic?
2. **Founding contributor recruitment.** Who are the first 20–50 hand-invited contributors? Which communities do they come from?
3. **Legal structure preference.** Nonprofit, PBC, or hosted-under-foundation (7.1). Determines funding pathway and IP terms.
4. **AI infrastructure.** Sovereign Ollama-only, or hybrid with hosted fallback? Determines Privacy Notice reconciliation approach (7.4) and infrastructure cost profile.
5. **Public launch timing.** Phase 1 gate met + Phase 2 gate met is the recommended public launch threshold. Founder confirms or adjusts.

---

## Appendix A — References

- Prior audit: pre-production readiness review (informal, in-thread, July 15 2026).
- Prior audit: market viability analysis (informal, in-thread, July 15 2026).
- Prior audit: submission UX verification (informal, in-thread, July 15 2026).
- [Kialo (German Wikipedia)](https://de.wikipedia.org/wiki/Kialo) — dormancy analysis and closed-data critique.
- [Argumentree](https://argumentree.com/) — comparable AI-extraction architecture reference.
- [Digital.gc.ca Civic Tech Report 2025](https://digital.canada.ca/reports/civic-tech-report-2025.pdf) — Canadian civic-tech partnership framework.
- [Argument map (Wikipedia)](https://en.wikipedia.org/wiki/Argument_map) — historical context on prior deliberation platforms.

---

*End of specification.*
