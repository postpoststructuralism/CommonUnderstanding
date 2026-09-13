# Pluggable Reference-Framework Layer Completion

Completed: 2026-09-12

## Scope

Layer 1 only is implemented. No structural, synthesis, deliberation, recommendation, or other later-layer behavior was added.

## Delivered

- Versioned `ReferenceFramework` records with legal, policy, organizational, scientific, and ethical source types.
- Shared frameworks and private frameworks with multiple `ReferenceFrameworkOwner` records.
- PDF, HTML, and plain-text extraction with a 20 MB upload limit and metadata-only source retention.
- Approximately 3,200-character chunking before the existing argument decomposition pipeline to avoid its input truncation boundary.
- Deduplicated atomic `ReferenceProposition` records from central claims and premises, including provisional assessment, confidence, order, and persisted embeddings.
- Fit and tension scoring against analytical `Proposition` records and social `SocialArgument` records.
- A shared `ReferenceRelationshipClassifier` used by both reference scoring and the understanding graph, preserving the graph's established thresholds.
- Persisted `ReferenceFrameworkRelationship` rows with a database constraint requiring exactly one analytical target.
- Authenticated Reference Library list, import, details, and scoring UI.
- Importers are assigned as owners automatically; optional additional owners are selected through bounded active-user search rather than entered as profile IDs.
- SQL Server migration `20260912055140_AddReferenceFrameworks`, with PostgreSQL-aware model configuration retained.

## Verification

- `dotnet build CommonUnderstanding/CommonUnderstanding.csproj`: passed with 30 pre-existing warnings and no errors.
- xUnit suite: 35 passed, 0 failed.
- Added focused tests for relationship threshold boundaries, status-driven contradictions, cosine similarity, zero/invalid vectors, and lossless decomposition chunking.
- Confirmed anonymous access to the import route redirects to login; authenticated owner-picker interaction still requires a configured test account.
- Reviewed migration `Up`, `Down`, indexes, foreign keys, SQL Server types, target constraint, and model snapshot.

## Operational Follow-up

- Apply and smoke-test the migration against a disposable or backed-up SQL Server database before production rollout.
- Verify the authenticated upload and scoring workflow in a configured environment with database and AI providers available.