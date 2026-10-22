# Issue #3 green contract validation — 2026-10-20

Base `7895262a21f19741c0b5b7fbad30907bccff2cbb`; branch `codex/issue-3-ephemeral-aot`.
Validated in the native disposable short-path checkout `E:/oc3c`, holding exact
author bytes. This is ordinary contract/process validation, not trimming, AOT,
consumer execution or a compatibility result. No implementation has begun.

| Lane | Result |
|---|---|
| OpenSpec all / strict / noninteractive | 24/24 |
| Full solution Release build, warnings as errors | 0 warnings / 0 errors |
| Exact current-source package feed | 12/12 |
| Full DeveloperSurface Infrastructure, dotnet test | 256/256 |
| Manifest-staged whitespace and candidate-tree check | clean; final receipt in the review request |

No runtime test, provider database, integration, stress or coverage run is claimed
for this contract-only change. Existing required CI checks remain unchanged.
Compact build/package/test/TRX and strict-validation receipts are retained in
the ignored `.artifacts/publishing-validation/issue-3-contract/` directory.
The disposable worktree and its build/package/cache output are removed after
receipt verification; review material stays in the author worktree.

## Provenance and coverage

- 199 rows / 52,401 bytes / `dd0b1db45dfa689b3df6440bfccee5c4c87802fe4f82b780b52c3616a7c7dc51`.
- Four PendingAddition operations; semantic approval eligible remains false.
- 195 base rows and all 19 predecessor artifact files remain byte-exact.
- Active capability directories: 27; canonical directories: 14, unchanged.
- Current human artifact SHA-256: `492fb060d5046e7cd8b3cc088248fbef0eb631ed1110cd7ff6556f6383bd490a`.
- Static risk sweep: 89 lines / 15 files, not observed warnings.
- Normative/guide routing: 125 exact line hashes / 40 owners, including zero-hit owners.
- Proposed executable coverage: six cells / sixteen named public scenarios.
  Actual native results, diagnostic counts and measurements remain unknown.

Only the new contract directory and the provenance fixture/independent guard pins
change. Product source, consumer/test/workflow implementation, both normative
trees, declaration companion, API/compile/package baselines, dependencies, prior
registry records and immutable review evidence are unchanged. Task 1.4 and all
later tasks stay open. Independent approval is required before checkpointing or
preparing the canonical/consumer target; no commit, push, PR or issue edit occurs.
