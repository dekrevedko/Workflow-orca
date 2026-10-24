# Issue #13 R1 contract validation — 2026-10-23

Base/HEAD 3b7f98aa0748a4d9c3f54d4604e566ed61f21bad; branch
codex/issue-13-native-aot-trimming. Independent re-review pending. Source
implementation has not begun. The exact manifest has 29 paths: 27 in the new
change directory and two mechanical provenance fixture/guard files.

| Lane | Result |
|---|---|
| OpenSpec all / strict / noninteractive | 25/25 |
| Full solution Release build, warnings as errors | 0 warnings / 0 errors |
| Exact current-source package feed | 12/12 |
| Full DeveloperSurface Infrastructure through dotnet test and CI engine-only coverage include | 259/259, zero failed or skipped |
| Manifest-staged whitespace/tree rehearsal | clean |
| Existing review-manifest current-match check | current, no fixture changes needed |

Receipts remain under ignored `.artifacts/publishing-validation/native-support-contract-r1/`.
No new runtime tests, product implementation, native support qualification,
database/provider/integration/stress or full product coverage result is claimed.
The Infrastructure collector uses CI's exact engine-only include; it is not a
new product-wide coverage claim. The final freeze receipt binds file/status/tree
hashes and the final lane receipt without altering frozen contract inputs.

## F1/P3 coverage and preservation

- Seven additions still map to six native cells, original AOT-01–16 and NS-01–12,
  now with mandatory NS-02a–c and NS-06a–f. These nine named subcases require all
  omitted/present, root/nested, positive/rejection/retry assertions in the R1
  protocol; they are future product tests, not implemented passes.
- The framework-only .NET 10 probe confirms metadata can identify the affected
  shape without constructing the model, preserves the original 42-versus-0
  counterexample, and distinguishes admitted positional/mutable/constructor-bound
  controls. The mutable omitted control is 42 through both paths; the genuine
  constructor-bound omitted control is 0 through both. Native preservation of
  classifier facts remains required later. Probe source/output is retained.
- All eleven dated rejected artifacts and verdict remain byte-exact. The rejected
  candidate is c0fe638a0143421f6470bd207e65966aa0d1698a; R1 changes only the F1/P3
  active planning text, R1 evidence and necessary provenance pins.
- The exact public type/member catalogue remains one WorkflowJsonMetadata type
  and one Register method. No API annotation, option/fingerprint, package,
  dependency or friend addition is proposed.
- Source, canonical/numbered docs, declaration companion, executable baselines,
  runtime tests, workflows and old approval registry records are unchanged.

## Provenance

- 206 rows / 54,112 UTF-8 LF bytes /
  `72138cf1e243aa337a81466cd47400cf2c5cd7efe32536d8c6e0d931b394ee5b`.
- Seven PendingAddition operations; semantic approval eligibility remains false.
- All 199 master rows remain exact. The permanent predecessor catalog has 22
  files: the 21 existing master predecessors/current record plus the rejected
  contract's preserved current provenance artifact. All remain byte-accountable.
- Active capability directories: 31; canonical directories: 14, unchanged.
- Guard edits only refresh pins and append immutable predecessors; no validation
  logic or source exception is relaxed. Source approval remains absent.

## Cleanup and authority

The small framework probe at E:/oc13f1 remains: 500,838 bytes (26 files).
Automatic approval review rejected its recursive deletion with only the reason
"blocked by policy". No retry or alternate deletion mechanism was used. The
ignored probe-cleanup.json records that limitation; this is not product evidence.

The separate validation checkout E:/oc13c1 and its build/feed/test-consumer output
are owned by this task. After exact-target verification, normal git worktree
cleanup and its observed result are recorded in cleanup.json outside the frozen
target. Preserve unrelated worktrees, shared caches, old receipts and review
deliverables. This report does not assert a deletion before it is observed.

Tasks 1.3/1.4 and every source/publication task remain open. No checkpoint, source
implementation, canonical transition, push, PR or GitHub issue edit is authorized
by these green lanes. The R1 target stays uncommitted for independent re-review.
