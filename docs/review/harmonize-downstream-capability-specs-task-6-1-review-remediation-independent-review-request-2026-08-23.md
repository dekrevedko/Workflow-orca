# Harmonization Task 6.1 review-remediation independent review request

**Date:** 2026-08-23
**Requested verdict:** `APPROVE` or `REJECT`
**Authorization requested:** create only the Task 6.1 remediation checkpoint

## Checkpoint chain

Verify that `603dffc02b13f0a3515ffb007af247f378f7b790` is the approved eleven-path Task
6.1 checkpoint, with parent `87f0b0dd1d0815e9334eb686eb52bb9c30d02513` and tree
`f6c7821a3299367be65566ac6b041bda6845a779`. Its immutable external verdict is
`harmonize-downstream-capability-specs-task-6-1-independent-review-verdict-2026-08-22.md`.

## Review scope

Review the exact target named by
`harmonize-downstream-capability-specs-task-6-1-review-remediation-dirty-manifest-2026-08-23.txt`
on base `603dffc02b13f0a3515ffb007af247f378f7b790`.

Confirm:

1. `MissingApproval` cannot carry a checkpoint, and Task 5.3's repository-owner authorization is
   explicitly recorded without being mislabeled as independent review.
2. Task 6.1's approved verdict, checkpoint, tree, raw manifest, and committed content record are
   immutable historical evidence in the provenance fixture.
3. Both canonical and active reshape lifecycle requirements include callback-local scope handles
   while retaining the same requirement identity and matching normative body.
4. The lifecycle corpus guard rejects an `AC-021` retag, rejects skipped representative evidence,
   preserves the real AC-021 owner, and CI executes `Requirement=CR-009a` (20 tests).
5. Active-freeze schema 8 recomputes one scoped content record over every target path except its
   sole self-referential provenance fixture, both before and after checkpoint commit.
6. The leased retry harness awaits its second-attempt notification rather than sampling task state
   after start completion; four concurrent `causal-release-gap-recovery` runs are green and the old
   shape is source-guarded.

No `src/**` file is in scope, Task 6.2 remains open, and the harmonization ledger stays 19/15/34.

## Validation to reproduce

- Debug and Release solution builds with `--no-incremental -m:1 -warnaserror`: 0/0.
- Core: 350/350; `Requirement=CR-009a`: 20/20.
- OpenSpec corpus guards: 5/5.
- Infrastructure: all must-green; ExpectedRed: exactly the established 14 failures.
- OpenSpec strict: 18/18; `git diff --check`: clean.

Recompute the raw manifest and the scoped content record from the live target before and after
validation. Do not edit, stage, or commit the target. Return one dated immutable `APPROVE` or
`REJECT` verdict.
