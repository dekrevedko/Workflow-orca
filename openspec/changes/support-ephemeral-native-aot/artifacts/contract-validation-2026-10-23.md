# Issue #13 contract validation — 2026-10-23

Base 3b7f98aa0748a4d9c3f54d4604e566ed61f21bad; branch
codex/issue-13-native-aot-trimming. Independent review pending; source and
canonical implementation have not started. This document will record ordinary
contract/process lanes, not a new Native AOT support result.

Validated in disposable short-path checkout E:/oc13c. The reviewed manifest has
20 paths: 18 in the new change directory and 2 mechanical provenance files.

| Lane | Result |
|---|---|
| OpenSpec all / strict / noninteractive | 25/25 |
| Full solution Release build, warnings as errors | 0 warnings / 0 errors |
| Exact current-source package feed | 12/12 |
| Full DeveloperSurface Infrastructure, dotnet test with CI engine-only coverage filter | 259/259, no failures or skips |
| Manifest-staged whitespace/tree rehearsal | clean |
| Existing review-manifest current-match check | all existing entries current; no fixture edit needed |

Compact build/package/test/TRX/coverage receipts remain under ignored
`.artifacts/publishing-validation/native-support-contract/`. This contract adds
no runtime tests or new native support evidence. No additional runtime/unit,
database/provider integration, stress or full product coverage result is claimed.
The Infrastructure coverage collector uses CI's exact engine-only include; it
is not reported as product-wide acceptance coverage.

## Provenance and planned coverage

- Complete record: 206 rows / 54,112 UTF-8 LF bytes /
  `bb742c12cf2deee0a47c92bea180aad9ff18d2df302c52740917601219c7f9c0`.
- Seven PendingAddition blocks across four existing capabilities; semantic
  approval eligibility remains false and source approval remains unset.
- All 199 master rows and 21 predecessor files remain unchanged; 31 active
  capability directories and 14 unchanged canonical capabilities are pinned.
- Seven new requirements map to six native cells, original AOT-01–16 and new
  NS-01–12. These are future coverage obligations, not implemented test passes.
- Scope inspection confirms no src/, docs/, canonical spec, public/executable
  baseline, dependency, workflow, runtime test or old approval-record change.
  The guard diff only refreshes pins and appends the previous current artifact.

Tasks 1.3/1.4 and all later tasks remain open pending independent approval.
No checkpoint, source implementation, canonical sync, remote branch, PR or
further issue edit is part of the frozen target. The new issue creation itself
was explicitly requested and is complete.

## Cleanup ownership

E:/oc13c and all its generated bin/obj/package/test-consumer outputs belong to
this validation only. After the final exact-target lane and receipt capture,
remove that checkout through git worktree remove. The ignored cleanup receipt
records the observed outcome and reclaimed output size without changing this
frozen contract. Keep only compact review inputs and receipts in the author
checkout; preserve unrelated worktrees, shared caches and prior deliverables.
