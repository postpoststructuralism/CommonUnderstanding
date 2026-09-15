# Structural Layers Gap Analysis
### Common Understanding — What's Missing to Realize the Mission

**Context:** This analysis was produced by reviewing the live codebase (`postpoststructuralism/CommonUnderstanding`), including the Understanding Graph architecture, Emergent Conclusions Engine, Multi-User Convergence plan, Argument Engine plan, and Product Specification. It confirms what is already implemented vs. what structural components are still missing to fully realize the mission of "building a common understanding for humanity" when shared facts have broken down.

*Revision note: Layer 1 was originally scoped as "Government of Canada policy fit" — that specific tie no longer applies and has been generalized to a pluggable reference-framework layer usable by any institution or community. Layer 2 has been extended with an automated evidence-corpus/citation-matching sub-component (2b) following further discussion.*

---

## Part 1 — What's Already Structurally Strong (for reference, not action)

| Layer | Status |
|---|---|
| Proposition/Argument core with evidence tiers, confidence, controversy scores | ✅ Built |
| Worldviews tagged with Schwartz values (10-dim vector) | ✅ Built |
| Moral Foundations vector (6-dim), parallel to Schwartz | ✅ Built (schema exists) |
| Understanding Graph (supports/contradicts/qualifies/assumes edges) | ✅ Built |
| Schema Discovery + Topological Data Analysis (bridge nodes, epistemic fault lines, conceptual voids) | ✅ Built |
| Emergent Conclusions Engine (blindspot + harmony detection) | ✅ Built |
| Multi-user convergence (stakeholder positions, comparative analysis, bridging arguments) | ✅ Built |
| Structured debate rooms, fallacy detection, Wilson-score voting, badges | ✅ Built |
| RAG-based argument-link suggestion (embed → pgvector similarity → LLM classifies Supports/Contradicts/Refines/Extends) | ✅ Specified (Phase 2 spec) — generalizable pattern |
| External literature API integration (Semantic Scholar, CrossRef, PubMed) | ⚠️ Planned, not yet wired to decomposition |

---

## Part 2 — Structural Layers Still Missing

### 1. Pluggable Reference-Framework Layer
**Problem:** There is currently no mechanism to score arguments against an external normative framework — a government's policy corpus, an organization's charter, a professional code of ethics, a body of scientific consensus, a municipal bylaw set, etc. This was previously scoped narrowly as "Government of Canada policy fit," but that specific tie no longer applies. The generalized version is more valuable and more consistent with a mission aimed at "humanity" rather than one jurisdiction.

**Structural shape:**
- A `ReferenceFramework` entity (name, description, source type — legal/policy/organizational/scientific/ethical, version, jurisdiction/scope, owning org)
- A `ReferenceProposition` entity — same shape as existing `Proposition`, but sourced from an imported reference document set instead of user argument submission
- Reuse the existing contradiction/support edge logic from the Understanding Graph to compute fit/tension scores between user propositions and a selected `ReferenceFramework`'s propositions
- A framework-selection UI so a community can attach zero, one, or many reference frameworks to a given debate or worldview comparison
- Import pipeline: ingest a document set (PDF/HTML/text), decompose into atomic propositions using the same argument-decomposition pipeline already used for user arguments, embed and store

**Why it matters:** Turns "policy fit" from a one-off integration into a reusable capability — any institution or community can define its own reference corpus and see where public argument diverges from or aligns with it.

---

### 2. Provenance and Source-Trust Layer
**Problem:** Evidence entries have a `Tier` (T1–T5/T6) and an optional DOI field, but there is no independent scoring of source reliability, publisher track record, retraction status, or outlet bias lean. Two pieces of "T2" evidence — one from a peer-reviewed journal, one from a partisan blog — currently look structurally identical unless a human manually re-tiers them.

**Structural shape:**
- A `Source` registry entity: publisher/outlet name, reliability score, bias-lean estimate, retraction/correction history, domain of expertise
- Link `Evidence` records to a `Source` via foreign key rather than a free-text citation
- A background verification job that periodically checks retraction databases (e.g., Retraction Watch-style feeds) and flags affected evidence
- Surface source-trust metadata directly in the evidence tier display so users see *why* something is or isn't credible, not just a tier number

**Why it matters:** Without this, the truth-assessment layer cannot differentiate between well-sourced and poorly-sourced claims that happen to share a tier label — a core requirement for the "assess truth claims" part of the mission.

---

### 2b. Automated Evidence Corpus & Citation-Matching Sub-Component
*(An extension of Layer 2 — the automated ingestion arm of the Provenance/Source-Trust Layer)*

**Problem:** Users frequently submit arguments without any supporting citation attached. Requiring users to supply their own documents undercounts how much real evidence exists to support or contradict a claim, and leaves well-evidenced positions looking "unsupported" purely because the submitter didn't cite anything. The Argument Engine plan already lists Semantic Scholar/CrossRef/PubMed integration as a future task, and the Phase 2 spec already defines the exact RAG pattern needed (embed → pgvector similarity search → LLM classification) for argument-to-argument link suggestions. Neither has been wired into automatic citation-matching during decomposition.

**Structural shape:**
- `EvidenceCorpusEntry`: a continuously-maintained, embedded store of external literature (abstracts, key findings) ingested from Semantic Scholar/CrossRef/PubMed adapters (and later, broader sources), each linked to a `Source` record from Layer 2 for reliability/retraction metadata
- Background ingestion service: periodically pulls new/updated literature for tracked domains, embeds it, and upserts into `EvidenceCorpusEntry`, re-flagging any entries whose source is later retracted or corrected
- Auto-match at decomposition time: when a `Proposition` is created, embed it and run the same RAG pattern already specified for argument-linking — retrieve top-K candidate `EvidenceCorpusEntry` records via pgvector cosine similarity, then have the LLM classify each as `Supports` / `Contradicts` / `Irrelevant`, assign a tier, and produce a one-sentence rationale
- **Grounding constraint (critical):** the LLM step may only select and characterize retrieved corpus entries — it must never be allowed to generate or paraphrase a citation from parametric knowledge. Retrieval grounds the claim; generation only explains the relationship.
- **Confirmation step (critical):** auto-matched citations are surfaced as pending suggestions, not permanently attached evidence, until a user or moderator confirms them. Silently auto-attaching machine-suggested evidence is a bigger trust failure here than in argument-to-argument linking, because it directly affects the "assess truth claims" function of the platform.
- **Bounded discovery (critical):** surface no more than three pending source suggestions per evidential claim. After the user confirms or rejects suggestions, a subsequent Find Sources action retrieves the next best previously unseen corpus entries instead of repeating or overwhelming the user with the full candidate set.
- The same retrieval mechanism generalizes to a third corpus later: argument-to-`ReferenceFramework` proposition matching (Layer 1), meaning one retrieval service ultimately serves three matching use cases (argument↔argument, argument↔evidence, argument↔policy).

**Why it matters:** Removes the dependency on users manually supplying citations, surfaces existing evidence the user may not know about, and turns evidence-tiering from a manual, inconsistent process into a continuously-updated, source-graded one — while the confirmation step prevents the system from asserting false confidence in an auto-generated match.

---

### 3. Adversarial Integrity Layer
**Problem:** No bot/sockpuppet/coordinated-inauthentic-behavior detection exists, and no formal moderation or dispute-resolution workflow exists for contested node classifications or disputed worldview assignments.

**Structural shape:**
- Behavioral fingerprinting service: posting cadence, account age, argument-similarity clustering across accounts, vote-pattern anomaly detection
- A `Dispute` entity: raised against a specific node/edge/classification, with a resolution workflow (evidence submission → community/moderator review → resolution status), modeled similarly to the existing `DebateRoom` structure
- Rate-limiting and reputation-weighting hooks that feed into the existing Wilson-score voting so flagged/suspicious accounts have reduced influence rather than being outright banned
- Audit trail for all classification changes (who/what changed a node's schema membership, worldview tag, or contradiction edge, and why)

**Why it matters:** A system meant to operate "at scale" for democratic deliberation is a direct target for coordinated manipulation. Without this layer, the existing voting and worldview-aggregation math is gameable.

---

### 4. Epistemic Status and Calibration Layer
**Problem:** Confidence and controversy scores exist, but there is no first-class status label (e.g., established consensus / actively contested / speculative / retracted) independent of the raw confidence math, and no calibration tracking (e.g., Brier scoring) for users or worldviews that make falsifiable predictions.

**Structural shape:**
- `EpistemicStatus` enum on `Proposition`: distinguishes *why* something is uncertain — lack of evidence vs. genuine expert disagreement vs. unresolved value conflict — rather than collapsing everything into one confidence number
- A `Prediction` entity for falsifiable claims tied to a resolution date and actual outcome, enabling Brier/calibration scoring per user and per worldview
- Calibration score surfaced on user profiles and worldview pages, distinct from participation-based badges

**Why it matters:** Readers need to know *why* something is uncertain, not just how uncertain it is. Calibration scoring also creates an incentive structure that rewards accuracy over volume — currently the badge system rewards participation, not being right.

---

### 5. Representativeness and Governance Layer
**Problem:** The graph reflects whoever chooses to argue on the platform. There is no tracking of how representative the contributor base is of the broader population (demographics, geography, self-placement on political/values spectrum), and no formal appeals process for disputed classifications — the kind of governance layer platforms like Wikipedia had to build reactively after facing legitimacy challenges.

**Structural shape:**
- Optional, privacy-preserving self-reported demographic/values metadata on user profiles, aggregated (never individually exposed) to produce a representativeness dashboard per debate topic
- Visible "representativeness confidence" indicator alongside any aggregate finding (e.g., "this harmony was detected across N worldviews representing an estimated M% demographic spread")
- A formal governance/appeals body structure: `GovernancePolicy` entity defining who can adjudicate contested classifications, escalation paths, and public changelog of adjudicated disputes
- Transparency reporting: periodic public report on contributor composition, dispute volume, and resolution outcomes

**Why it matters:** The Product Specification explicitly names earning trust from journalists, academics, and policy researchers as a gate to the next phase. That claim isn't credible without a legitimacy and governance structure that predates the first major controversy, not one built reactively after it.

---

### 6. Steelmanning / Adversarial Synthesis Agent
**Problem:** The existing `BridgeArgumentPlugin` generates bridges between worldviews using shared Schwartz values, but nothing explicitly generates the *strongest possible version* of an opposing argument before it is critiqued. This is distinct from bridging (finding common ground) — steelmanning strengthens the opposing case itself.

**Structural shape:**
- A `SteelmanAgent` service, structurally parallel to `BridgeArgumentPlugin`: takes an argument and its Schwartz/Moral Foundations profile, then constructs the most values-consistent, evidentially strongest version of the counter-position
- Surface the steelmanned counter-argument alongside the original in the argument detail view, with a clear label distinguishing it from the original submission
- Optional community rating of "did this steelman fairly represent the opposing view?" to keep the agent honest over time

**Why it matters:** This is a low-cost, high-leverage addition given the Schwartz classifier already built — the same values-extraction machinery that powers bridging can power steelmanning with comparatively little new infrastructure.

---

## Suggested Priority Order

1. **Pluggable reference-framework layer** — highest strategic value, reuses existing graph/embedding infrastructure most directly
2. **Provenance and source-trust layer (incl. automated evidence corpus, 2b)** — cheapest to bolt onto the existing evidence-tier model, and 2b directly reuses the RAG pattern already specified for argument-linking
3. **Steelmanning agent** — near-free extension of `BridgeArgumentPlugin`
4. **Epistemic status and calibration layer** — moderate effort, high trust payoff
5. **Adversarial integrity layer** — expensive but becomes non-negotiable once the platform gets real traction
6. **Representativeness and governance layer** — expensive, but should be in place *before* the first major controversy, not after

*Note: Layer 2b (automated evidence corpus) has a natural dependency on Layer 2's `Source` registry existing first, since every corpus entry needs a source-reliability record to be meaningful. Sequence 2 → 2b within that priority slot.*
