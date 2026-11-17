# Project Review Fixtures v1

All passages are synthetic. No model or external source lookup is used. Expectations
describe the intended interpretation, not an implemented automatic classifier.
Changing wording or expectations requires a new fixture version and an explanation.

## Claims

`ProjectReviewTests.SyntheticClaimsV1` checks exact display text, supplied type,
scope, attribution, unknown coverage, absent invented source, and unreviewed state
through save, reopen, and export. Relation and evidence-direction expectations below
are reference cases for Phase 3; Phase 2 does not classify or weight these relations.

| Case | Expected Relation / Direction | Explanation |
| --- | --- | --- |
| Transit reduces emissions | None / unknown | An assertion is not its own evidence. |
| Identical transit wording | Duplicate candidate / neutral | Duplicate wording must not transfer review or count as independent evidence. |
| Public transport lowers emissions | Equivalence candidate / neutral | Paraphrase requires scope verification, not automatic support. |
| Transit does not reduce emissions | Contradiction candidate / opposing | Polarity matters only with matching scope and attribution. |
| Transit reduced emissions in 2010 | Historical qualification / unknown | A past observation does not establish a present claim. |
| Some residents prefer transit | Population qualification / unknown | Some surveyed residents does not mean all residents. |
| Clean-electricity city | Context qualification / unknown | Energy context changes applicability. |
| Free fares might raise ridership | Hypothetical dependency / unknown | A conditional prediction is not an observed result. |
| Transit should be affordable | Normative / not applicable | Empirical truth scoring cannot establish a value judgment. |
| Ridership will rise next year | Predictive / unknown | A forecast is not a resolved observation. |
| Transit reduces emissions and should be funded | Mixed / mixed or unknown | Factual and normative components must remain visible. |
| Most residents prefer transit | Quantified qualification / unknown | Most does not establish deductive certainty or unanimity. |
| Every resident prefers transit | Universal / unknown | A bounded sample does not establish a universal assertion. |
| Mayor says transit is affordable | Attribution / neutral | Attribution does not transfer the assertion to the importer. |
| Summary repeats one study | Dependency / no additional independent support | Copying a study cannot double its evidential weight. |
| Reports A and B cite each other | Circular dependency / no independent support | Circular citation cannot manufacture external grounding. |
| Zero participant responses | No agreement relation / neutral | Non-response is not support; retain the literal count. |

All these records initially have authored origin as explicitly supplied by the
fixture, no attached evidence, unknown coverage, active lifecycle, and unreviewed
disposition. Authored attribution text is not verified source provenance.

## Revision and Provenance Scenarios

| Executable Scenario | Expected State and Provenance |
| --- | --- |
| SourceCorrectionAndReviewRetainExactHistoryAndRejectStaleReview | Exact source span survives correction; stable claim ID, new revision ID; new revision unreviewed; old human review retains rationale, conditions, objection treatment, hash, policy, reviewer, and time. Old-revision review/correction attempts fail. |
| PolicyAndSourceChangesInvalidateReviewWithoutRewritingHistory | Policy changes and source replacement/retraction invalidate applicable acceptance, retain historical reviews and source versions, and require revalidation. |
| HistoricalRecordsRejectMutationAndObjectionsRequireTreatment | Stored revisions cannot be silently edited through tracked EF writes; acceptance cannot ignore recorded objections. |
| LegacyImportPreservesFormerStatusWithoutInventingReviewOrSource | Legacy Settled is preserved as former status but imported as Unreviewed with unknown origin/source; original legacy row remains unchanged. |
| RolesAndCrossProjectSourcesFailClosedWithoutWrites | Another project's exact source span cannot be attached even if its ID is known; denied reads/writes/export do not expose private records. |
| MissingOwnerDeniesExistingMemberAndHidesProject | Missing ownership fails closed, including existing members. |
| LocalSqlServerProjectSchemaSupportsIsolationDeletionAndRollback | Disposable SQL Server schema supports queries/export/deletion and migration Down/Up without affecting unrelated user rows. Opt in with CU_PROJECT_LOCAL_SQL_TESTS=1. |

The stale-review and stale-correction checks are deterministic Phase 2 races.
Background-job cancellation, concurrent source-set invalidation, and late worker
commits belong to W-028 in Phase 3; this fixture set does not claim those implemented.