---
name: pairwise-vote-agreement
description: Rank users and explain pairwise agreement from shared decisive votes on public contributions.
last_updated: 2026-09-22
---

# Pairwise Vote Agreement

Use `UserAgreementService` as the owner of Convergence ranking and evidence queries.

- Compare only votes on public, non-deleted social arguments.
- Include only decisive `Up` and `Down` votes; exclude abstentions.
- Count matching vote values as agreement and opposing values as disagreement.
- Rank by `ScoringAlgorithms.WilsonScoreLowerBound`, then expose the raw agreement percentage and shared-vote count so the confidence adjustment remains understandable.
- Keep pairwise detail grounded in the exact contributions and both recorded votes.
- Build to an isolated output directory when the running development process locks the normal application DLL.