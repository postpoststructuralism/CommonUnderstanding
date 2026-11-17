# Common Understanding - Feature Specification

Updated: 2026-10-20

## Product

A private, source-linked workspace for reviewing claims and evidence about a bounded policy question. Users contribute material, correct extracted claims, inspect sources and objections, record decisions, and export a readable review history. Public discussion remains separate from private review.

This replaces the previous execution plan. Work-item IDs identify features, not approval dependencies. Agent roles, contract approvals, independent audits, founder sign-offs, commercial validation, execution logs, and release ceremonies are removed from code-completion requirements.

## Scope

Phase 1 is locally complete. Phase 2 is the authorized implementation scope; Phases 3-5 remain backlog. Retain and check existing implementation rather than restarting it.

Use local tests, synthetic/public-source fixtures, and fake model services. This specification does not authorize deployments, paid calls, shared/production database operations, existing-data cleanup, infrastructure changes, or commits. Implementing migration code does not authorize executing it against operational data.

Do not expand personality inference, feeds, widgets, automated truth ranking, global consensus/graphs, reference frameworks, steelmanning, calibration, adversarial analysis, or governance. Existing surfaces receive only the listed repairs.

## Product Rules

- AI output is a draft, not truth, human assent, human review, consensus, or a decision.
- Extraction confidence, model assessment, evidence assessment, participant position, review disposition, and decision outcome are separate dimensions.
- Preserve type, scope, attribution, exact source spans, and transformation lineage. Unknown provenance stays unknown.
- Similarity is not support. Different scores are not contradiction. No response is not agreement.
- Human review belongs to exact revisions and inputs. Changes require revalidation, not silent transfer of review. Preserve historical decisions.
- Corrections remain editable until explicit submission. Background completion cannot overwrite corrections or publish automatically.
- Private material stays private unless an authorized user deliberately publishes it.
- Explain assessments through their inputs and policy versions. Suppress unsafe outputs; disclaimers alone are insufficient.

## Phase 1 - Containment and Immediate Repairs

Local implementation complete: 83 focused tests pass and the application/Razor build succeeds (2026-10-19). Synthetic browser checks cover deliberate scale/ranking answers and correction selection. No deployment, paid calls, shared database work, cleanup, or commits were performed. Production and exhaustive visual verification are not claimed.

### W-002 - Conservative Displays

Analytical containment is enabled by default. Label AI-only and legacy claims without applicable human review as Unreviewed, not Settled. What Changed must not infer contributor agreement or attribute unrelated statements. Comparisons suppress unsupported conflict, unique-claim, and favored-winner judgments. Harmonies and thematic matches do not imply evidential reinforcement. Participation displays literal counts or insufficient participation. Sparse Mosaic/profile data shows insufficient information, not invented conviction, tentativeness, or predictability.

Cover Argument detail, What Changed, comparisons, Decision Support, Emergent Conclusions/Key Insights, Live Map labels/legends, and Worldview Composer/Mosaic. Identify generated assessments as drafts for human review, not evidence or consensus determinations.

Check representative rendering with containment on/off, including zero evidence, unrelated Settled claims, unsupported harmonies, an Undecided participant, and sparse profiles. Authorization, correction protection, and explicit publication apply with either setting.

### W-003 - Weightless Proposed Relations

AI-proposed relations must not affect evidence strength, review state, participation summaries, or recommendations. Cover persisted-relation consumers as well as generation. Similarity and analogy carry no evidence weight. Disable affected calculations when proposed/reviewed records cannot safely be distinguished; do not delete existing records.

Check unchanged outputs before/after adding proposed relations. Disabled generation returns before writes, embeddings, or model calls.

### W-004 - Content-Free Workflow Logging

Record analysis start/completion/failure/cancellation, status changes, correction saves, and publication with structured event names, argument ID, authenticated actor where applicable, request correlation, transitions, actions, and created public-record ID. Include real job/project/source/model/prompt/policy identifiers where available; missing values remain unavailable. Request correlation is not a persistent job ID. Do not build the later ledger merely to fill fields.

Check captured fields and absence of submitted content using fake success/failure paths.

### W-005 - Mechanical Repairs

- Convert affinity from its 0-100 producer scale to the DTO/display's 0-1 fraction at the mapping boundary. Reject invalid values. Check actual mapping, aggregation, serialization, and formatting at minimum/midpoint/maximum.
- Untouched discovery scales remain unanswered. Reject missing, nonfinite, out-of-range, undefined-bound, and reversed-bound answers before profile changes or queued work. Preserve unanswered interactions.
- Rankings require explicit selection or confirmation, not automatic acceptance of initial order. Provide keyboard interaction and accessible error feedback.
- Avoid repeated discovery questions while suitable unused questions remain. Match endpoint labels to questions and use unique accessible control IDs.
- Replace exposed enum/version/template artifacts with readable labels and repair encoding in affected discovery/profile/affinity views only.
- Remove dead Terms/Contact links or use working destinations. Full policy/contact features belong to Phase 5.
- Distinguish empty data from empty search/filter results. Do not claim updates, agreement, or completed analysis when none occurred.

Check answer preservation, questions/endpoints, rankings, affinity, representative rendering, and keyboard submission/error handling. No repository-wide cleanup is required.

### W-006 - Correction Before Publication

Selecting "Let Me Fix This" must not submit or navigate to What Changed. Keep claims and premises editable. Correction saves return to Review without public records. Explicit Publish uses submitted corrections, including premises. Completion during editing cannot overwrite saved corrections, navigate away, or publish. Preventing reanalysis of completed corrected arguments is acceptable here; revision-aware reruns belong to Phase 3.

Check browser selection, save/publish behavior, and deterministic completion-during-editing scenarios with fake analysis. Correction saves and background completion create no public records.

### W-009 - Secure Existing Operations

Authenticate before expensive work or private-result access. Enforce ownership for reads, review, analysis, comparison inputs, dependent evidence operations, mutations, and deletion. Discovery hub operations use the authenticated caller's profile and reject another profile before dependencies. Unknown private ownership fails closed.

State changes and work generation use non-GET operations. Cookie-authenticated mutations enforce antiforgery, with tokens supplied by callers. GET result/detail pages read existing data rather than generate it. Public reads include only published records.

Check anonymous/owner/other-user/nonexistent-record/GET/missing-antiforgery cases and hub ownership. Denied operations perform no writes or model calls. Test hosts cannot reach production or real workers.

### Phase 1 Completion

Finish these six feature items, pass their focused checks, and pass the local application/Razor build. Report actual results and unavailable checks honestly. A failing required check means that behavior remains unfinished. Do not broaden scope to finish this phase.

W-001 mapping is already done. W-007 audit-data cleanup and W-008 contract drafting are removed. No snapshots, independent acceptance, founder approval, production verification, or deployment are required. This definition does not assert that Phase 1 is already complete.

## Phase 2 - Projects and Source-Linked Claims

Local implementation: authenticated Projects pages/API, private owner/editor/reviewer/reader permissions, membership and policy management, exact source-version passages, immutable claim revisions, separate review dimensions, explicit human review, owner-only JSON history export, and project deletion. Legacy import is deliberate and limited to the caller's own propositions; former status is retained without inventing source provenance or human review. Existing legacy rows are not rewritten.

Versioned regression cases are documented in `CommonUnderstanding.Tests/Fixtures/project-review-v1.md`. Storage, permissions, stale review/correction, source retraction/replacement, policy changes, export reconstruction, and authenticated Razor rendering have focused checks. The combined project-service and existing HTTP/Razor regression run passed 77 tests, with the opt-in LocalDB test skipped. The application/Razor build succeeded using isolated output because Visual Studio/IIS Express held the normal output open. Disposable LocalDB previously verified SQL Server schema creation, project operations, migration rollback/reapply, and cleanup; opt in with `CU_PROJECT_LOCAL_SQL_TESTS=1`. The generated migration has not been applied to any existing database. PostgreSQL migration execution remains unverified. Project graphs, evidence/relation engines, decisions, publication, and background-job races remain in their specified later phases.

Project forms now offer permission-filtered person searches with names, handles, and existing roles; owned-contribution search/import; named source versions; and exact passage selection instead of storage IDs and numeric offsets. Corrections preserve provenance and use Save Privately, without adding publication. Synthetic compiled-Razor browser checks at 1280px and 390px verified correction prefill, named search results, mixed-newline passage coordinates and submitted text, and no horizontal overflow. Browser API responses were mocked; these checks do not establish a live deployment or complete production visual/asset verification.

### W-010 - Private Projects

Private-default projects enforce owner/editor/reviewer/reader permissions server-side. Owners manage membership, policy, decisions, publication, export, and deletion; editors contribute/correct; reviewers adjudicate; readers view permitted records. Deny unspecified operations. Unknown legacy ownership does not become public access. Check roles, direct-object access, isolation, and new relational schema behavior.

### W-011 - Regression Cases

Versioned synthetic/public-source fixtures retain explained expectations for type, scope, attribution, relation, evidence direction, state, and provenance. Include duplicates, paraphrases, negation, scope/time/population differences, hypotheticals, normative/predictive/mixed claims, quantifiers, dependent/circular evidence, sparse participation, revisions, retractions, and stale-job races. Never change expectations merely to pass. Deterministic checks need no live models.

### W-020 / W-024 - Review Dimensions

Store origin, review disposition, evidence coverage, objections, lifecycle, and revalidation separately. Corrections create unreviewed revisions. Review retains reviewer, rationale, conditions, timestamp, and exact inputs; acceptance with objections records their treatment. AI completion cannot accept claims.

Legacy Settled becomes unreviewed unless applicable human adjudication exists. Preserve former values and known origin. Check transitions, migration/rollback fixtures, and consistent list/detail/graph/reopened-record/export rendering.

### W-021 - Claim Revisions

Separate stable identity from immutable revisions. Retain source version, exact passage coordinates/text, type, scope, attribution, and transformation history. Matching text is separate from display text. Authored/unsupported claims need no fabricated sources; normative statements are not automatically empirical. Check provenance through ingestion, correction, and export.

## Phase 3 - Relations, Evidence, Positions, and Freshness

### W-022 / W-060 - Explicit Positions

Positions belong to authenticated participants and exact revisions/scopes. Track eligible population, invitations, responses, support, oppose, mixed, undecided, abstain, and non-response separately. Show counts and real denominators. Consensus requires configured quorum/threshold/abstention rules; otherwise show counts or insufficient participation. Never infer agreement from silence, affinity, unrelated votes, or old revisions, or imply wider representativeness. Check sparse/revised cases.

### W-023 / W-025 / W-027 - Relations and Objections

Check exact/canonical matches before semantic classification, then scope, attribution, polarity, and context. Genuine duplicates are not contradictions or two unique claims; wording alone does not transfer review. No favored winner without explicit decision policy.

Distinguish equivalence, support, contradiction, qualification, objection, dependency, attribution, similarity, and analogy. Retain direction, endpoint revisions, scope, passages, proposer/version, proposed/reviewed state, reviewer, and rationale. Only reviewed evidential support contributes weight; reviewing a relation does not prove its source. Preserve evidence origins. Explain contradiction, unresolved objection, and human disagreement separately. Check duplicates/different scores, scope, polarity, analogy, and objections.

### W-026 - Logic Assessments

Separate deductive validity, inductive strength, plausibility, and generated reconstruction. "Most..." premises do not establish deductive certainty. Empirical, normative, predictive, interpretive, philosophical, attributed, and mixed claims use appropriate dimensions, not a universal truth score. Check quantified/probabilistic and type-specific cases.

### W-028 - Revision-Safe Results

Pin claim/source revisions and model/prompt/policy versions. Atomically guard writes against changed inputs, cancellation, deletion, authorization revocation, and missing projects. Stale completion cannot overwrite corrections, publish, or repopulate deleted caches/feeds. Mark dependents for revalidation without rewriting history. Preserve failed/partial states. Check deterministic races, SQL Server concurrency, and historical reconstruction/deletion markers.

### W-030 - Evidence Assessment

Return typed explanations of inputs, factor contributions, before/after values, and policy version. Neutral stays neutral; coverage is separate. No hidden fallback score. Duplicate citations, copied reports, AI summaries, dependent/circular evidence do not add independent support. Unknown independence remains unknown. Preserve policy history. Check neutral/no-evidence, supporting/opposing/mixed, duplicate/dependent/circular, and retracted cases.

## Phase 4 - Review Workflow and Decisions

### W-031 / W-040 - Queue and Evidence Cards

Provide sortable/filterable review by type, state, evidence gap, objection, impact, owner, and change. Detail exposes passages, attribution, scope, evidence, positions, objections, history, and affected decisions. Objections have assignment, due date, severity, evidence links, and resolution rationale. Cards show direction, quality with reason, relevance, independence, coverage, verification/retraction, and reviewer instead of misleading headline percentages. Check filters, workflows, and accessibility.

### W-032 - Decision History

One record holds alternatives, criteria, evidence, unresolved issues, owner, outcome, rationale, conditions, date, policy version, and exact snapshot. Replace overlapping decision representations. Later invalidation preserves history. Support readable/JSON exports; check reconstruction and invalidation.

### W-041 - Project Graph

Graph/list share dataset/detail. Provide legend, filters/search, provenance, revision/time context, distinct relation/review states, bounded neighborhoods, and equivalent keyboard/screen-reader access. Explain size/confidence/cluster encodings and provide text/icon redundancy for color. Check parity, bounds, freshness, and accessibility.

### W-042 / W-043 - Changes and Navigation

What Changed shows field differences, actor, reason, revision/source-set IDs, generation time, inclusions, and freshness without triggering analysis. Primary navigation: Projects, Review Queue, Claims and Evidence, Decision History. Map is a view toggle; public/experimental experiences stay separate. Check revised/retracted/stale displays and end-to-end navigation.

### W-100 - Deliberate Publication

Separate private save, graph inclusion, and public publication. Confirm audience and exact revision for destination changes; ordinary private saves need no extra prompt. Enforce permissions, label generated/imported/authored material, support withdrawal/correction, and prevent completion races. Explain ranking and provide chronological mode.

### W-122 - Review Export

Export authorized readable/JSON records with manifest/checksums, sources, revisions, evidence, positions, objections, decisions, and honest exclusions. Reconcile against the authorized dataset without exposing other projects or presenting drafts as facts.

## Phase 5 - Operational and Data-Handling Features

### W-101 - Profile Controls

Sparse profiles abstain from unsupported inference. Show source/response counts and correction/deletion controls. Experimental labels do not excuse false attribution/privacy leakage. Check sparse/prohibited inference and correction/deletion.

### W-120 - Derived-Store Isolation

Enforce project authorization across search, graph, embeddings, caches, exports, snapshots, and job commits. Check cross-account/two-project isolation at each boundary.

### W-121 - Job Controls

Provide idempotency, quotas, concurrency limits, bounded retries, cancellation, and cost limits. Ledger records owner/project/input versions, state, usage, and retries. Expose queue depth, latency, failures, and orphan/recovery states. Check duplicate, timeout, partial-write, recovery, cancellation/revocation, and budget behavior using fakes.

### W-122 - Data Deletion

Authorized deletion covers active/app-controlled derived/public stores, indexes, embeddings, caches, and temporary outputs. Preserve shared references and only necessary non-content metadata. Prevent late writes and replay deletion markers before serving restored data. Disclose backup expiry/external-copy limitations; do not promise impossible erasure. Check isolated deletion, retries, shared references, and restored-data markers. This specifies software behavior, not operational-data cleanup.

### W-124 / W-125 - Privacy and Contact

Provide working Terms, Privacy, Support, and Privacy Request destinations with request routing/tracking. Describe actual data categories, purposes, processors/model providers, regions/flows, retention/training settings, and deletion/backup limitations. Disclose unknown facts rather than certify them. Check links and local requests without external messages. Legal certification/founder approval is not a coding prerequisite.

### W-126 - Recovery Behavior

Implement recoverable job states and restored-data deletion-marker handling. Test interrupted work and recovery locally with simulations. Operational restore/incident rehearsals are not feature-completion requirements.

### W-123 / W-130 - End-to-End Checks

Check the delivered private workflow: import, correct, inspect sources, raise/resolve objections, record a decision, and export. Include permissions, stale results, cancellation, deletion, and consistent explanations where implemented. These test delivered behavior, not a separate independent audit, pilot approval, fresh-reviewer ceremony, or evidence-pack requirement.