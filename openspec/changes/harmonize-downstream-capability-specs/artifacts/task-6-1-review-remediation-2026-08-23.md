# Task 6.1 review remediation

**Date:** 2026-08-23
**Approved checkpoint:** `603dffc02b13f0a3515ffb007af247f378f7b790`
**Checkpoint tree:** `f6c7821a3299367be65566ac6b041bda6845a779`
**Source verdict:** `docs/review/harmonize-downstream-capability-specs-task-6-1-independent-review-verdict-2026-08-22.md`

The approved eleven-path Task 6.1 target was committed before its verdict entered the worktree. This
follow-up preserves that verdict and closes every review finding without changing product source or
starting Task 6.2.

## Finding dispositions

- **Q-1:** `MissingApproval` now requires both checkpoint commit and tree to be absent. Task 5.3's
  explicit repository-owner authorization is preserved by a dated disclosure and a separately named
  owner-authorization transition; it is not presented as independent review. Task 6.1 is recorded as
  independently approved and awaiting this distinct evidence checkpoint.
- **Q-2:** canonical and active reshape `workflow-authoring` requirements now name callback-local
  scope handles alongside nested, branch, item, and leased handles. The requirement identity and
  preamble are unchanged, and the provenance record was mechanically refreshed.
- **Q-3:** the Task 6.1 corpus guard rejects a restored lifecycle `[Trait("AC", "AC-021")]` while
  positively proving AC-021 remains on its lambda-mode evidence.
- **Q-4:** representative lifecycle members must carry an executable xUnit `Fact` or `Theory` with
  no `Skip`, and CI runs the exact `Requirement=CR-009a` lane. The lane selects 20 tests.
- **Q-5:** active-freeze schema 8 stores a scoped content record over every manifest path except the
  provenance fixture that stores the record. The singleton self-reference exclusion is enforced;
  worktree and committed-blob projections must reproduce the same bytes and SHA-256.

## Validation-detected infrastructure correction

A Release infrastructure run reproduced the pre-existing `3.11c/causal-release-gap-recovery`
race: the lease retry harness awaited the start operation and then sampled
`SecondStarted.IsCompleted`. That sample is not a notification boundary and can run before the retry
continuation under scheduler pressure. Both leased retry fixtures now await `SecondStarted.Task`
before awaiting start completion. The recursive lease-infrastructure guard requires both awaits and
forbids the old post-completion snapshot. Four concurrent focused lanes pass.

## Validation packet

- Debug and Release full solution builds: 0 warnings, 0 errors.
- `OrcaCore.Core.Tests`: 350/350; `Requirement=CR-009a`: 20/20.
- OpenSpec corpus guards: 5/5.
- infrastructure and expected-red guard lanes are run separately; the expected-red set remains the
  same 14 executable-behavior scenarios.
- OpenSpec strict: 18/18.
- harmonization ledger: 19 complete / 15 open / 34 total.
- `git diff --check`: clean.

## Freeze rule

The self-inclusive remediation request and raw-order manifest are part of the target. The
machine-readable active freeze carries the scoped content-record values; this artifact deliberately
does not duplicate those mutable final-freeze numbers.
