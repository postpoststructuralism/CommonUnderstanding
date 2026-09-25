# Emergent Conclusions UX Remediation Backlog

## Objective

Rework `/EmergentConclusions` from a passive report into an **action queue for improving the shared knowledge graph**. The existing feature plan defines an emergent conclusion as actionable and explicitly calls for blindspot actions that can create an argument, add evidence, or request stakeholder input; the current view reduces every blindspot to a generic **Investigate** link that opens the unscoped argument-submission page.

The redesign should preserve the engine’s read-only analytical role while letting user-initiated workflows modify the underlying arguments, propositions, evidence, comparisons, and stakeholder positions. Findings already carry involved argument, proposition, node, and stakeholder IDs, so the principal gap is workflow routing and durable action state—not detection provenance.

## Current-State Findings

- The page mixes the site’s shared design system with raw Bootstrap semantics: `btn-outline-danger`, `bg-danger`, `alert-warning`, `bg-light`, and generic progress bars dominate the page instead of the established navy, teal, amber, glass-surface, pill-button, workspace-hero, metric-card, and info-band patterns.
- The two equal-width columns give empty Harmonies half the desktop canvas while ten Blindspots become a long, repetitive card stack. That layout prioritizes type symmetry over the user’s current workload.
- The page explains how to classify findings only after the findings. It does not explain near the top that the user’s job is to improve the graph by supplying evidence, testing an assumption, answering a rebuttal, resolving a contradiction, adding stakeholder positions, or using shared ground.
- Evidence-desert cards know the affected proposition IDs and texts, but the UI links only to affected arguments and then offers generic argument submission. The repository already has proposition-scoped evidence submission in `ArgumentController.AddEvidence` and the argument view’s evidence form.
- The current **Investigate** button does not preserve finding context, proposition ID, source report, return URL, or suggested action. It therefore cannot support attribution, measure resolution, or return users to the item they acted on.
- Harmonies have a more relevant action—the dialogue-guide modal—but it passes only title and opportunity text, uses client-generated HTML without shown output encoding, and does not offer a durable synthesis, collaborative session, stakeholder comparison, or saved follow-up.
- Findings have no durable identity or lifecycle fields, despite the original plan’s model including an `Id` and success criteria based on the percentage of findings acted upon and evidence submitted within seven days.
- The page displays **100% confidence** beside findings explicitly saying there is no evidence. The model defines this as detection confidence, but the UI labels it only “Confidence,” making it easy to read as confidence in the proposition.
- Export regenerates the report rather than exporting the report currently on screen, so the downloaded artifact can differ from a historical or deep-analysis snapshot being viewed.
- The page says Deep Analysis requires a configured Gemini key, but the controller always exposes the entry point and does not show capability state before navigation.

## Priority Backlog

### P0 — Make Findings Actionable

#### EC-01 — Add stable finding identity and lifecycle

**Change**

- Add a deterministic `FindingKey` to `EmergentConclusion`, derived from category plus sorted involved entity IDs and a normalized claim fingerprint.
- Persist actionable instances separately from report JSON, for example `EmergentFindingState` with `FindingKey`, `FirstDetectedAt`, `LastDetectedAt`, `Status`, `ResolutionType`, `AssignedUserId`, `ResolutionNote`, `ResolvedAt`, `SourceReportId`, and `LastActionAt`.
- Use statuses: `Open`, `InProgress`, `Resolved`, `Dismissed`, and `Superseded`.
- Reconcile each generated report with prior finding state so regeneration does not erase ownership or resolution history.

**Acceptance criteria**

- The same logical finding retains the same key across report refreshes.
- A resolved finding does not silently reappear as a new open item unless its underlying fingerprint materially changes.
- The UI can filter findings by status and show who is addressing each item.

**Likely files**

- `Models/EmergentConclusionModels.cs`
- New persisted entity/configuration and EF migration
- `Services/EmergentConclusionsEngine.cs`
- `Controllers/EmergentConclusionsController.cs`

This is required for the feature’s own action-rate success metric; the current transient DTO lacks an `Id` even though the original plan anticipated one.

#### EC-02 — Replace generic actions with category-specific CTAs

**Change**

Map each category to a primary verb, helper copy, icon, destination, and contextual payload:

| Category | Primary CTA | Secondary CTA | Context to carry |
|---|---|---|---|
| Evidence Desert | **Add evidence** | Request evidence | Proposition ID, argument ID, finding key, return URL |
| Confidence Illusion | **Review evidence quality** | Add stronger evidence | Proposition ID, existing evidence list, finding key |
| Assumption Cascade | **Test assumption** | Ask stakeholders | Proposition/node IDs, affected arguments, finding key |
| Unaddressed Rebuttal | **Respond to rebuttal** | Add counter-evidence | Rebuttal/proposition ID, parent argument, finding key |
| Silent Contradiction | **Compare claims** | Open resolution discussion | Both proposition/node IDs, finding key |
| Convergent Ground | **Start from common ground** | Open collaborative session | Stakeholder and argument IDs, finding key |
| Complementary Chains | **Build a synthesis** | View graph path | Node/argument chain, finding key |
| Emergent Consensus | **Review consensus** | Invite remaining stakeholders | Proposition and stakeholder IDs, finding key |
| Shared Value Core | **Draft bridge argument** | Start facilitated dialogue | Value profile, stakeholder IDs, finding key |
| Cross-Domain Reinforcement | **Connect arguments** | View graph relationship | Argument/node IDs, finding key |

**Acceptance criteria**

- No finding uses a generic **Investigate** or **Leverage** label without explaining the concrete result.
- Every primary CTA lands on a workflow pre-populated with the entities that caused the finding.
- Cancel and success paths return to the original finding anchor.
- If required provenance is missing, disable the action and show a precise reason rather than falling back to generic submission.

The category mapping follows the feature plan’s intended interactions and uses provenance that already exists on each finding.

#### EC-03 — Add a proposition-scoped evidence drawer

**Change**

- Extract the existing evidence form from `Views/Argument/View.cshtml` into a reusable partial or view component.
- Open it from Evidence Desert and Confidence Illusion cards in a modal/off-canvas drawer without losing page context.
- Preselect `ArgumentId` and `PropositionId`; display the full proposition text, current status, confidence, existing evidence count, and source-trust guidance.
- POST through the existing `ArgumentController.AddEvidence` path or a dedicated endpoint that delegates to the same service.
- On success, mark the finding `InProgress`, show confirmation, and offer **Refresh analysis**.

**Acceptance criteria**

- A user can begin evidence submission with one click from the finding.
- The proposition and affected argument are visible before submission.
- Anti-forgery validation, server-side validation, and authorization match the existing evidence workflow.
- A successful submission is attributable to the finding key and report snapshot.

The codebase already supports proposition-specific evidence submission, so this should reuse rather than duplicate validation and persistence logic.

#### EC-04 — Add an action router

**Change**

- Create an `EmergentFindingActionService` that returns a typed action descriptor from category and provenance rather than embedding routing rules throughout Razor.
- Suggested descriptor fields: `Kind`, `Label`, `Description`, `Icon`, `Controller`, `Action`, `RouteValues`, `Enabled`, and `DisabledReason`.
- Render finding CTAs through a partial/view component shared by blindspots and harmonies.

**Acceptance criteria**

- Category-action mapping has unit tests covering all ten categories.
- Razor contains no category-specific routing branches beyond presentation.
- Missing or malformed provenance produces a disabled action with telemetry, not an exception.

#### EC-05 — Support ownership, resolution, and dismissal

**Change**

- Add **I’ll address this**, **Mark resolved**, and **Dismiss** controls subject to authorization.
- Require a resolution note and type, such as `EvidenceAdded`, `ArgumentAdded`, `ComparisonCompleted`, `StakeholderInputAdded`, `NoLongerApplicable`, or `FalsePositive`.
- Display compact state and assignee chips on cards.

**Acceptance criteria**

- Anonymous users see sign-in prompts instead of mutation controls.
- Only the assignee, moderator, or authorized owner can resolve/dismiss according to the chosen policy.
- Resolution leaves an audit record and does not directly rewrite the analytical conclusion.

### P0 — Clarify the Page’s Job

#### EC-06 — Replace the header with an action-oriented workspace hero

**Change**

Use the existing `.cu-workspace-hero`, `.cu-workspace-tag`, `.cu-workspace-lead`, and `.cu-workspace-actions` patterns. Proposed copy:

> **Improve the shared understanding**  
> These findings show where the community’s reasoning needs evidence, testing, response, or collaboration. Choose a finding below and take the suggested next step; the next analysis will show whether the gap narrowed.

Add a compact three-step strip:

1. **Choose a finding** — start with high-impact open items.
2. **Take the linked action** — add evidence, test a claim, respond, compare, or collaborate.
3. **Refresh the analysis** — verify whether the finding changed or was resolved.

**Acceptance criteria**

- Purpose and next step are understandable without scrolling.
- Refresh, Deep Analysis, History, and Export are visually secondary to working the findings.
- The generated timestamp and analysis mode appear as metadata, not toolbar clutter.

The project already defines workspace hero and action patterns that this page does not use.

#### EC-07 — Rename ambiguous metrics and confidence labels

**Change**

- Rename finding `Confidence` in the UI to **Detection confidence** and add help text: “How confident the system is that this pattern exists; not the truth of the proposition.”
- Rename `Significance` to **Estimated impact** or define significance inline.
- Replace “100%” precision with qualitative bands plus optional exact value in a tooltip unless the underlying method is calibrated.
- Rename **Evidence Coverage** to **Propositions with evidence** and clarify whether any evidence tier counts.

**Acceptance criteria**

- No card can reasonably be interpreted as asserting 100% truth for an unsupported claim.
- Every metric has accessible explanatory text or a popover.
- Screen-reader labels distinguish proposition confidence, detection confidence, and impact.

The model explicitly defines confidence as confidence that the pattern is real, but the current view drops that distinction.

#### EC-08 — Move explanations into context

**Change**

- Replace the large bottom “How to read this page” card with a short **About these findings** disclosure near the hero.
- Add one-sentence category definitions in each card or tooltip.
- Keep a link to full methodology explaining detectors, thresholds, standard versus deep analysis, and limitations.

**Acceptance criteria**

- Users do not need to scroll past all findings to understand terminology.
- Category names remain discoverable but do not dominate each card.
- Deep-only categories are marked by a small analysis-mode indicator, not parenthetical footer text.

### P1 — Redesign Information Architecture

#### EC-09 — Replace fixed two-column layout with an adaptive work queue

**Change**

- Default to a single prioritized list when only one finding type has results.
- When both types exist, use tabs or segmented controls: **Needs attention**, **Opportunities**, **All**.
- Add count chips and preserve the selected view in the query string.
- Keep desktop cards to a readable width; do not reserve 50% of the viewport for an empty state.

**Acceptance criteria**

- With ten blindspots and zero harmonies, blindspots use the full content width.
- With both types present, users can switch without losing filters or scroll target.
- On mobile, the layout is a single column and action buttons are full-width or wrap cleanly.

The current `col-lg-6` split always reserves equal width for each type, regardless of workload.

#### EC-10 — Add triage controls

**Change**

- Add filters for type, category, status, impact, confidence, affected argument, and “actionable by me.”
- Add sort options: impact, newest, oldest unresolved, most affected arguments, and lowest evidence coverage.
- Add a text search over proposition text, title, description, and affected argument titles.
- Keep filter state in the URL.

**Acceptance criteria**

- The result count updates as filters change.
- Clear-all is always available.
- Filtering occurs server-side once report size exceeds an agreed threshold; otherwise client-side filtering must remain accessible without JavaScript.

#### EC-11 — Introduce compact and expanded card states

**Change**

- Compact state: category, status, full untruncated claim title, impact band, detection confidence, affected-entity count, and primary CTA.
- Expanded state: explanation, why it was flagged, all affected propositions/arguments, evidence snapshot, suggested action, secondary actions, history, and resolution state.
- Link proposition labels directly to the proposition/premise section, not only to the parent argument.

**Acceptance criteria**

- Ten findings can be scanned without ten fully expanded repetitive descriptions.
- Truncated text always has an accessible way to reveal the complete claim.
- Expansion state survives returning from an action workflow.

#### EC-12 — Demote aggregate health into a collapsible summary

**Change**

- Show three or four decision-relevant metrics near the top: open findings, high-impact findings, propositions lacking evidence, and available opportunities.
- Move argument/proposition/evidence totals and the full status distribution into **Graph health details**.
- Make status bars accessible with text equivalents; do not render labels inside segments too narrow to contain them.

**Acceptance criteria**

- The first viewport prioritizes actionable findings rather than six inventory counters.
- All chart/status information remains understandable in text and at 200% zoom.

### P1 — Complete Harmony Workflows

#### EC-13 — Replace “Leverage” with outcome-specific harmony actions

**Change**

- Rename the existing button to **Create dialogue guide** if that is the operation.
- Add **Start collaborative session**, **Draft synthesis**, **Compare stakeholder positions**, or **View shared premises** according to category.
- Save generated dialogue guides or offer copy/download; do not leave the result only in a disposable modal.
- Pass entity IDs to guide generation so output is grounded in actual shared premises and stakeholders rather than only title text.

**Acceptance criteria**

- Each button states the artifact or workflow it creates.
- The generated guide cites the shared premises and relevant participants available in the graph.
- Generated content is safely encoded/sanitized before insertion into the DOM.

The current endpoint produces a generic four-step guide from title and opportunity strings alone, and the client concatenates returned strings into `innerHTML`.

#### EC-14 — Make the empty Harmony state actionable

**Change**

- Replace “Add stakeholder positions and run comparisons” with direct buttons: **Add stakeholder position**, **Compare arguments**, and **Learn what creates harmonies**.
- If either prerequisite is absent, explain it with counts: for example, “No comparisons exist yet” or “Only one stakeholder position is recorded.”

**Acceptance criteria**

- Every prerequisite named in the empty state has a direct destination.
- The system identifies the actual missing prerequisite rather than always showing generic advice.

### P1 — Visual Alignment

#### EC-15 — Create page-specific semantic styles using design tokens

**Change**

- Add `.cu-emergent-*` classes in `portal.css`; do not place new style blocks in the Razor view.
- Base surfaces, radii, shadows, typography, and spacing on existing CSS variables and workspace components.
- Use teal for constructive actions, amber for attention, and restrained red only for destructive or genuinely critical states.
- Use shared `.cu-btn-primary` and `.cu-btn-secondary` patterns, adding semantic variants only where needed.
- Disable hover lift for static metric and explanation cards; reserve lift for clickable cards.

**Acceptance criteria**

- The page contains no ad hoc hex values or inline visual styles except computed widths that cannot be represented otherwise.
- The page visually matches the workspace and exploration pages at common breakpoints.
- Color is never the sole indicator of blindspot/harmony, severity, or status.

The shared design system supplies the intended navy/teal/amber palette and reusable hero, metric, button, card, and information-band components; the current view mainly uses raw Bootstrap state colors.

#### EC-16 — Extract reusable partials

**Change**

Create:

- `_EmergentFindingCard.cshtml`
- `_FindingActions.cshtml`
- `_GraphHealthSummary.cshtml`
- `_FindingFilters.cshtml`
- `_EvidenceDrawer.cshtml` or a view component
- `_EmptyState.cshtml`

Use a page view model rather than binding the raw report directly. Include report metadata, filters, action descriptors, lifecycle state, authorization flags, and capability flags.

**Acceptance criteria**

- Blindspot and harmony markup share one card renderer with semantic variants.
- The page view has no large helper switch blocks for labels/icons/actions.
- Partials have focused rendering tests where supported.

### P1 — Correct Data and Capability Behaviour

#### EC-17 — Export the viewed snapshot

**Change**

- Pass `reportId`/snapshot ID to Export and load that persisted report.
- For the latest generated report, persist first and export the resulting snapshot.
- Display the snapshot ID/time in exported metadata.

**Acceptance criteria**

- Exporting a historical or deep report produces exactly the findings visible on screen.
- Export does not silently regenerate analysis.

The current Export action always calls `GenerateReportAsync`, even when the user is viewing a saved report.

#### EC-18 — Expose analysis capabilities before action

**Change**

- Add a capability model such as `CanRunDeepAnalysis`, provider label, missing-configuration reason, and expected scope.
- Disable or hide Deep Analysis when unavailable; show an administrator-oriented setup link only to authorized users.
- Explain the difference between standard and deep analysis before the user starts it.

**Acceptance criteria**

- Users cannot enter a workflow guaranteed to fail because no provider is configured.
- The page clearly marks which findings require deep analysis.

#### EC-19 — Add anti-forgery and output-safety hardening

**Change**

- Ensure an anti-forgery token is actually rendered on the page before `GenerateDialogue` reads it, or use a standard form/post helper.
- Add `[ValidateAntiForgeryToken]` to all mutation endpoints.
- Replace string-concatenated `innerHTML` with DOM node creation or sanitize encoded output.
- Add authorization to ownership, resolution, dismissal, and evidence-request actions.

**Acceptance criteria**

- CSRF tests fail without a valid token.
- A guide containing markup-like text is displayed as text, not executable HTML.
- Unauthorized state changes return the correct challenge/forbid response.

The current JavaScript looks for a token that is not rendered in the shown view, and the endpoint lacks an explicit anti-forgery attribute.

### P2 — Measure Whether Actions Work

#### EC-20 — Add finding-to-action telemetry

**Change**

Capture events with privacy-appropriate metadata:

- `FindingViewed`
- `FindingActionStarted`
- `FindingActionCompleted`
- `FindingAssigned`
- `FindingResolved`
- `FindingDismissed`
- `AnalysisRefreshedAfterAction`

Include finding key, category, report ID, action type, entity IDs, and elapsed time. Do not log submitted evidence text or private stakeholder reasoning.

**Acceptance criteria**

- The product can calculate the percentage of findings acted upon.
- The product can calculate whether evidence deserts receive evidence within seven days.
- Telemetry distinguishes action starts from completed actions.

These measurements directly implement the original success criteria.

#### EC-21 — Show before/after impact

**Change**

- In History, compare findings by stable key and label them `New`, `Persisting`, `Improved`, `Resolved`, or `Regressed`.
- On each finding, show “Open since,” previous impact/confidence, and actions taken.
- After refresh, show a feedback banner such as “2 findings resolved, 1 impact reduced, 1 new finding detected.”

**Acceptance criteria**

- A user can see whether their action changed the analysis.
- Changes are based on stable finding identity, not title-string matching.

#### EC-22 — Add false-positive feedback

**Change**

- Provide **This finding is inaccurate** with structured reasons: wrong entity match, not actually high-stakes, evidence exists but is unlinked, culturally inappropriate evidence expectation, detector misunderstanding, or other.
- Route feedback to moderator/review tooling and retain it for detector evaluation.

**Acceptance criteria**

- Dismissal does not erase the record.
- Detector precision can be reviewed by category and analysis mode.

This is especially important for the attached sample: historical, philosophical, legal, cultural, and counterfactual propositions should not all be told to obtain “peer-reviewed empirical evidence.” The action recommendation must follow claim type and epistemic status rather than applying one template universally.

## Recommendation Logic

Add an `ActionRecommendationFactory` that considers both category and claim type. A practical first pass:

| Claim type | Appropriate next step |
|---|---|
| Empirical/descriptive | Add primary study, systematic review, dataset, or other tier-appropriate evidence |
| Historical attribution | Add primary text, critical edition, archival source, or reputable scholarly reference |
| Legal/institutional | Add statute, regulation, judgment, official guidance, or authoritative commentary |
| Interpretive/cultural | Add relevant ethnographic scholarship and clearly identified community/knowledge-holder sources; avoid treating the claim as a laboratory hypothesis |
| Normative/value judgment | Add stakeholder rationale, ethical framework, or argument; do not demand empirical proof of the value judgment itself |
| Counterfactual/falsifier | Mark as a test condition or rebuttal criterion rather than an unsupported factual proposition |
| Definitional/analytic | Link an authoritative definition or formal argument; evaluate coherence rather than empirical support |

**Acceptance criteria**

- Suggested actions do not always request T1–T3 peer-reviewed evidence.
- The detector records why a particular evidence/action type is appropriate.
- Users can challenge the inferred claim type.

## Suggested Implementation Order

1. **EC-07 and EC-06:** fix misleading language and explain the page’s purpose.
2. **EC-02 and EC-03:** ship direct evidence actions for the most common current finding.
3. **EC-09 and EC-15:** align layout and visuals with the design system.
4. **EC-01 and EC-05:** add durable identity, ownership, and resolution.
5. **EC-04 and claim-type recommendation logic:** generalize action routing across categories.
6. **EC-13 and EC-14:** complete harmony workflows and prerequisites.
7. **EC-10 through EC-12:** add triage, compact cards, and progressive disclosure.
8. **EC-17 through EC-19:** correct snapshot export, capabilities, and security.
9. **EC-20 through EC-22:** measure outcomes and improve detector quality.

## Definition of Done

- A first-time user can explain in one sentence what the page is for and can begin the recommended action within two clicks.
- Every open finding has a category-appropriate primary action or an explicit disabled reason.
- Evidence gaps open a proposition-scoped submission workflow with context preserved.
- Finding confidence is unambiguously labeled as detection confidence.
- The page uses the shared design tokens and workspace component language, with accessible non-color indicators.
- Finding identity and lifecycle survive report regeneration.
- Export matches the exact viewed snapshot.
- Mutation endpoints have authorization, validation, anti-forgery protection, output encoding, and audit trails.
- History shows whether user actions improved, resolved, or failed to change each finding.
- Automated tests cover category routing, stable-key generation, lifecycle reconciliation, authorization, CSRF, mobile layout smoke checks, and accessibility-critical labels.