# Harmonization Task 6.1 post-review-hardening independent review request

**Date:** 2026-08-29
**Requested verdict:** `APPROVE` or `REJECT`
**Authorization requested:** create only the Task 6.1 post-review-hardening checkpoint

## Checkpoint chain

Verify that `9f4fa0b5aba0a2d8ada4188c0a3d6753a63a608c` is the approved sixteen-path
Task 6.1 remediation checkpoint, with parent
`603dffc02b13f0a3515ffb007af247f378f7b790` and tree
`aecaf5c686e0047d628705ce2e94d02db9449ea9`. Its immutable external verdict is
`harmonize-downstream-capability-specs-task-6-1-review-remediation-independent-review-verdict-2026-08-23.md`.

## Review scope

Review the exact target named by
`harmonize-downstream-capability-specs-task-6-1-post-review-hardening-dirty-manifest-2026-08-29.txt`
on base `9f4fa0b5aba0a2d8ada4188c0a3d6753a63a608c`.

Confirm:

1. Both second-attempt waits race their signal against workflow completion and fail diagnostically
   if the workflow completes without retry; the source guard forbids the bare-await regression.
2. The canonical and active reshape lifecycle requirements remain byte-equivalent, and executable
   evidence pins the callback-local scope clause in both.
3. First-pass and later remediation approvals require no invented rejection; all immutable verdicts
   stay registered and the current state-evidence path selects the governing approval.
4. CR-009a's CI lane is one exactly parsed workflow step with the Core project, Release
   no-build/no-restore execution, and exact requirement filter.
5. Active-freeze tests invoke both real content-record builders rather than testing only the
   historical formatter.
6. The synchronized normative sentence is readably wrapped without semantic change.
7. The owner-authorized terminal transition is exercised by Task 5.3's committed evidence.
8. The prior 16-path remediation checkpoint and tree remain immutable, while this follow-up owns
9. Schema 9 retains the committed prior remediation freeze as executable archived provenance
   after this distinct active freeze replaces it.
   a separate self-inclusive freeze and scoped content record.

No `src/**` file is in scope, Task 6.2 remains open, and the harmonization ledger stays 19/15/34.

## Validation to reproduce

- Debug and Release solution builds with `--no-incremental -m:1 -warnaserror`: 0/0.
- Core: 350/350; `Requirement=CR-009a`: 20/20.
- Ephemeral 79/79; Durable 98/98; Acceptance 37/37; Hosting 24/24;
  ProviderCertification 96/96.
- OpenSpec corpus guards: 5/5; lease/governance infrastructure guards: 3/3.
- Infrastructure 216/216; ExpectedRed exactly 14/14; full guards 216 passed / 14 failed / 230 total.
- OpenSpec strict: 18/18; `git diff --check`: clean.

Mutation-check the bare second-attempt await, callback-local clause, CR-009a CI step, approval-state
rejection requirement, and active-freeze builder calls. Recompute the raw manifest and scoped
content record before and after validation. Do not edit, stage, or commit the target. Return one
dated immutable `APPROVE` or `REJECT` verdict.
