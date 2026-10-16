# Harmonization task 5.1 provenance-remediation independent review verdict

**Date:** 2026-08-22

**Reviewer:** independent review of the frozen target; no authorship of the reviewed commits

**Review request:** `docs/review/harmonize-downstream-capability-specs-task-5-1-provenance-remediation-independent-review-request-2026-08-22.md`

**Task 5.1 checkpoint:** `ff11ead781f8fef343fafc6e6bc8307d746e4a05`

**Remediation checkpoint:** `d0e7c4821199b8b1ee13d5f6fd22f79133abc576`

**Remediation tree:** `ef8f948b4e50d7b784c16e148f9ea1ec9c669bfc`

**Remediation parent:** `ff11ead781f8fef343fafc6e6bc8307d746e4a05`

## Scope

This verdict supplies the missing independent approval evidence for the Task 5.1 checkpoint
`ff11ead781f8fef343fafc6e6bc8307d746e4a05` as remediated by
`d0e7c4821199b8b1ee13d5f6fd22f79133abc576`. It does not approve Task 5.2, does not authorize a Task
5.2 checkpoint or refreeze, does not start Task 5.3, does not close the harmonization gate, does not
archive either change, and does not authorize reshape Task 8.0. The four prior immutable rejection
records for Tasks 5.1 and 5.2 remain unchanged and remain part of the provenance history.

## Method

All verification ran against the committed objects and against a disposable `git worktree` checkout
of `d0e7c4821199b8b1ee13d5f6fd22f79133abc576`. Mutation testing ran only inside that disposable
checkout, which was removed and pruned afterwards. The reviewed working copy was not modified during
verification: `HEAD` remained `d0e7c4821199b8b1ee13d5f6fd22f79133abc576` with zero staged paths and a
single untracked file (the review request) throughout.

## 1. Immutable objects — MATCH

- `d0e7c4821199b8b1ee13d5f6fd22f79133abc576^` resolves to `ff11ead781f8fef343fafc6e6bc8307d746e4a05`.
- `d0e7c4821199b8b1ee13d5f6fd22f79133abc576^{tree}` resolves to `ef8f948b4e50d7b784c16e148f9ea1ec9c669bfc`.
- `git diff-tree --no-commit-id --name-status -r` yields exactly **25 paths** (15 modified, 10 added).
- The four canonical specs that raw status intermittently reported as modified —
  `openspec/specs/event-driven-prototype/spec.md`, `openspec/specs/runtime-resource-governance/spec.md`,
  `openspec/specs/saga-orchestration/spec.md`, `openspec/specs/structured-fiber-execution/spec.md` —
  are byte-identical to `ff11ead781f8fef343fafc6e6bc8307d746e4a05` and are correctly absent from the
  commit. The prior review's phantom-entry finding is closed.
- `ff11ead781f8fef343fafc6e6bc8307d746e4a05` itself verifies at 17 changed paths, parent
  `179421029f62bc0cd4d5968d465cf045f420ba34`, tree `3baddd97a6919bf5f674daed33bc236496a234c2`.

## 2. Relationship to the previously approved target — DISCLOSED DRIFT, ACCOUNTED

Reconstructing the previously frozen 29-row content record from the committed tree yields 4,382 bytes
/ `9061452ec41e2d01ce0035c6215ccbf6e9fc73d4b9989a4da70b1639554746b7` against the frozen
`932bf906b6f5396fcb14a857f90c0c86d058bfce150667c9aa859229d36bbae0`. Exactly six of twenty-nine rows
differ, and all six are the files that carry the two post-freeze fixes:

| Path | Frozen bytes | Committed bytes |
| --- | --- | --- |
| `openspec/changes/harmonize-downstream-capability-specs/design.md` | 15,784 | 16,774 |
| `openspec/changes/harmonize-downstream-capability-specs/tasks.md` | 20,610 | 21,138 |
| `tests/OrcaCore.DeveloperSurface.Guards/OpenSpecCorpusGuards.cs` | 88,434 | 91,894 |
| `.gitattributes` | 190 | 237 |
| `openspec/changes/harmonize-downstream-capability-specs/artifacts/task-5-2-review-remediation-2026-08-21.md` | 7,295 | 8,424 |
| `tests/OrcaCore.DeveloperSurface.Guards/Fixtures/review-manifest-provenance.json` | 11,264 | 11,772 |

The remaining twenty-three rows, including every `docs/review/` record, are byte-identical to the
previously reviewed bytes. The drift is therefore fully explained by findings 5 and 6, which this
request submits for review, plus one undisclosed rider recorded as new finding N-1 below.

## 3. Requested findings — dispositions

**Finding 1 — clean-checkout infrastructure: CLOSED.** A genuine clean checkout of
`d0e7c4821199b8b1ee13d5f6fd22f79133abc576` (feed seeded with the 12 exact packages) runs the
`Disposition=Infrastructure` lane at **214/214**. Retired-project discovery tolerates an absent
retired root, with the discriminating regression in `ProductionDeletionLedgerGuards.cs`, and
historical crosswalk discovery drains `git archive` before parsing the TAR payload. This is the
dispositive objection of both prior independent rejections (their findings F1 and R2) and it is
closed.

**Finding 2 — line-ending reproducibility: CLOSED.** `.gitattributes` pins the public-authoring
companion, `openspec/specs/**/*.md`, and the Section 7 declaration crosswalk to LF, and the guard
additionally sweeps every hash-accounted canonical spec and the crosswalk fixture for raw CR bytes.
Both layers are independently effective (mutation M-F below, and CR injection in the prior round).

**Finding 3 — historical content records: CLOSED.** Schema 4 was recomputed independently:

- Task 5.1: 17 rows aggregate to **2,428 bytes** and SHA-256
  `741cfd6bbdd46cb4390c2f40c0d21d81d35b3e3749438b38efda44f26da1ff72`.
- Task 5.2: 12 rows aggregate to **1,793 bytes** and SHA-256
  `0068973b0dbd4c1f79086a0262cefe62b728911362b5cd43fe472e0c7daebc8a`.
- All 17 Task 5.1 rows are byte-identical to `ff11ead781f8fef343fafc6e6bc8307d746e4a05`'s blobs, and
  the row path set equals that commit's exact 17-path changed set.
- Rows bind one-to-one to their frozen manifests in the disclosed order; the manifests verify at
  1,228 bytes / `271309bd1da188c023610ffdde4065729fbc17f16f3ae786fc17561d1842f898` and
  947 bytes / `9a1cda496e71eb2f40ec02cb834478c3906df9b89d18b7461816a70845781b0f`.
- The unstable checkout-filtered projection invariant is gone.

**Finding 4 — review-state integrity: CLOSED FOR TASK 5.1, GAP FOR TASK 5.2.** Task 5.1 is recorded
`Rejected` with zero `APPROVE` verdicts, `approvalEvidenceCommit` null, and both rejections retained.
All four registered verdicts (two for Task 5.1, two for Task 5.2) verify byte-exact against their
recorded SHA-256 values. No commit in the repository matches the Task 5.2 checkpoint subject pattern.
The prohibition itself, however, is enforced only by commit subject; see new finding N-2.

**Finding 5 — commit-real freezes: CLOSED.** `design.md` section 6 now defines the freeze entry set
as the union of `git diff --name-only --no-renames HEAD` and
`git ls-files --others --exclude-standard`, with the manifest retaining Git-emitted order over that
set only. The guard carries a synthetic self-test that is mutation-sensitive (M-A) plus a live
consistency assertion.

**Finding 6 — opportunistic Task 5.2 evidence: CLOSED.** The four still-reproducible rows are pinned
and verified independently against current bytes: the Task 4.3 artifact (5,312 B), the post-gate
fixture (5,302 B), the Task 5.2 dirty manifest (947 B), and the Task 5.2 request (8,647 B). I
confirmed the pin set is currently maximal — of the remaining eight Task 5.2 rows, none still matches
current bytes. Maximality is not enforced; see new finding N-4.

## 4. Mutation results

| ID | Mutation | Result |
| --- | --- | --- |
| M-A | Neutralise the commit-real filter in `ProjectCommitRealFreezeManifest` | **RED** — synthetic phantom re-admitted, exact diagnostic |
| M-B | Coherently rehash a pinned Task 5.2 row and its aggregate | **RED** — aggregate reconstruction passes, current-byte check fails |
| M-C | Empty `currentWorktreeMatchPaths` for Task 5.2 | **GREEN** — residual, recorded as N-4 |
| M-E | Coherently fabricate a Task 5.1 row and rehash all four aggregates | **RED** — fails against the commit-blob projection |
| M-F | Remove `openspec/specs/**/*.md text eol=lf` from `.gitattributes` | **RED** |

Control run before every mutation: 3/3 green. Every mutation was reverted and the disposable checkout
verified clean before the next.

## 5. Validation — REPRODUCED

Run from a clean checkout of `d0e7c4821199b8b1ee13d5f6fd22f79133abc576`:

- Release build, `--no-incremental -m:1 -warnaserror`: **0 warnings / 0 errors**, 29 outputs.
- Debug build, `--no-incremental -m:1 -warnaserror`: **0 warnings / 0 errors**, 29 outputs.
- `Disposition=Infrastructure`: **214 passed / 0 failed / 0 skipped**.
- `Disposition=ExpectedRed`: **exactly 14 failures**, all `ExecutableBehaviorExpectedRedGuards`.
- Complete guard assembly: **228 total — 214 passed, 14 intentional red**.
- `OrcaCore.ProviderCertification`: **96 passed / 0 failed**.
- `openspec validate --all --strict`: **18 passed / 0 failed**.
- Harmonization ledger: **16 complete / 18 open / 34 total**.
- `git diff --check` between the two commits: exit 0 (see N-1 for why).

Independently re-derived from the committed tree:

- Provenance record: **176 rows / 46,211 bytes /
  `e1420f367a491e62e896f041f288b3235eeaf7647d721069cb71dda509b4021b`**, composed of 173
  `Synchronized` and 3 `NewCapabilityOutsideCanonical`, zero pending operations, zero duplicate
  owners, 11 synchronized removals.
- Canonical directory inventory: 14 / 547 bytes /
  `7165dac4e1a57022a7890b421f522bf4152f6d5ddc159a41539ce2ef0d18ec9f`.
- Active-delta inventory: 16 / 1,321 bytes /
  `5e8a9725979d702ff2f639fef587828958c92533139602ab13f1401d6df7ebd5`.
- Canonical preamble record: 14 / 1,233 bytes /
  `595528c6a7ba56dd5648e7fc12ac6bc2af9e9bf6fa3fbf853b86fffdd7cecd3c`.

## 6. New findings

### N-1 (P2) — undisclosed `.gitattributes` rider changes the meaning of `git diff --check`

`d0e7c4821199b8b1ee13d5f6fd22f79133abc576` adds a fourth attribute line,
`docs/review/**/*.md whitespace=-trailing-space`, which appears in no design, task, artifact, or
request text and belongs to none of the six declared findings. It is load-bearing: the commit
introduces **six added lines with trailing whitespace**, four in
`harmonize-downstream-capability-specs-task-5-1-checkpoint-provenance-independent-review-request-2026-08-21.md`
and two in `harmonize-downstream-capability-specs-task-5-2-independent-review-request-2026-08-20.md`.
Without the exemption, `git diff --check ff11ead d0e7c482` reports those six lines and exits
non-zero, so the exemption is precisely what makes one of the request's own listed validations pass.

The substance is defensible — those are frozen immutable records that must not be edited to satisfy a
whitespace check — but it must be disclosed rather than silently added, and the guard permits it
without constraint: `CompanionBaseline_HasExactReviewedNamespaceArityAndSignatures` asserts only that
three specific lines are present, so an arbitrarily broader exemption (for example `* -whitespace`)
would pass unchanged. Recommended: state the exemption and its rationale in `design.md`, and pin the
exact `.gitattributes` line set rather than three membership probes.

### N-2 (P2) — Task 5.2's canonical output is committed; the prohibition is subject-line only

`d0e7c4821199b8b1ee13d5f6fd22f79133abc576` modifies exactly the three canonical specs Task 5.2 owns —
`openspec/specs/management-and-querying/spec.md` (+5/-1),
`openspec/specs/quality-and-verification/spec.md` (+37/-6), and
`openspec/specs/repository-foundation/spec.md` (+9/-5) — and marks task 5.2 complete in the ledger.
Task 5.2's eight canonical operations are therefore in immutable history while Task 5.2 stands
rejected.

The guard does not observe this.
`ReviewCheckpointProvenance_BlocksTask52CheckpointUntilTask51ApprovalExists` detects a Task 5.2
checkpoint solely by grepping commit subjects for `^Checkpoint harmonization task 5.2`, and this
commit's subject is "Checkpoint harmonization review provenance remediation". The fixture
independently declares Task 5.2 `checkpointCommit: null`, and the schema only uses that null to
require the blob-projection fields to be absent — it never checks the claim against the tree. The
request's finding 4 ("no Task 5.2 checkpoint may exist") is thus satisfied by naming, not by content.

This is not a request to rewrite history, and the canonical content itself was previously reviewed.
It is a defect in the guard and an inaccuracy in the record. Before Task 5.2 is refrozen, either
record `d0e7c4821199b8b1ee13d5f6fd22f79133abc576` explicitly as the commit that landed Task 5.2's
canonical output under a named, non-approving state, or make the detector content-based — for
example, any commit whose tree touches a capability owned by the entry — instead of subject-based.

### N-3 (P2) — the `Approved` state is unreachable without one red intermediate commit

The `Approved` branch requires `approvalEvidenceCommit` to name an existing commit that already
contains the registered `APPROVE` verdict, with `merge-base --is-ancestor` from the Task 5.1
checkpoint. A commit cannot contain its own SHA, so the verdict must land in commit A and the fixture
must be updated in commit A+1. But `ValidateReviewVerdictEvidence` enumerates
`docs/review/harmonize-downstream-capability-specs-task-5-1*verdict-*.md` and requires the registered
set to **equal** the discovered set, so at commit A the two provenance guards are red; and the
verdict cannot instead be registered at A under state `Rejected`, because that branch asserts zero
`APPROVE` verdicts. The must-be-green `Disposition=Infrastructure` lane that CI enforces therefore
cannot stay green across this transition as written.

Recommended: admit a fourth state (for example `ApprovalPendingEvidenceCommit`) that accepts a
registered `APPROVE` with a null `approvalEvidenceCommit` while still prohibiting any Task 5.2
checkpoint, flipping to `Approved` in the following commit; or derive the evidence commit from
`git rev-list HEAD -- <verdict path>` instead of pinning it by hand.

**This applies immediately to this verdict.** Adding this file to `docs/review/` makes it discoverable
and unregistered, so both provenance guards go red until the fixture registers it with its byte hash.
That is expected, and is the owner's registration step rather than a defect in this review.

### N-4 (P3) — the opportunistic pin set is not maximality-enforced

`currentWorktreeMatchPaths` is validated as a subset of the historical rows, but nothing requires it
to include every row that still matches on disk. Emptying it for Task 5.2 leaves both provenance
guards green (M-C). The maximal set is exactly computable: every row whose file exists with matching
length and digest must be listed. Today the recorded set is already maximal, so this is a hardening
gap rather than a live inaccuracy.

## Determination

- Every immutable anchor for both commits reproduces exactly, and the remediation scope is exactly
  the 25 real paths with the four status-only phantoms correctly excluded.
- The dispositive objection of both prior independent rejections — the clean-checkout Infrastructure
  defect — is fixed and verified at 214/214 on a genuine clean checkout of the remediation commit.
- All six findings the request submits are closed on the evidence, with findings 5 and 6 each proven
  by a mutation that turns the guard red for the exact reason claimed.
- Every validation number in the request reproduces, and the provenance, inventory, and preamble
  records re-derive byte-identically from independent recomputation.
- The three new P2 findings concern disclosure (N-1), the representation and enforcement of Task
  5.2's status (N-2), and the reachability of the approved state (N-3). None of them impugns Task
  5.1's canonical synchronization or its checkpoint provenance, and none is repairable by withholding
  Task 5.1 approval — N-3 in particular is unblocked only by granting it. They are carried into task
  5.2a and must be closed before Task 5.2 is refrozen.

**Verdict:** **APPROVE**
