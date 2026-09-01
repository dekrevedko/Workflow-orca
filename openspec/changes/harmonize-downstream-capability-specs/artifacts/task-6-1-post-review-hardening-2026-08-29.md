# Task 6.1 post-review hardening

**Date:** 2026-08-29
**Approved remediation checkpoint:** `9f4fa0b5aba0a2d8ada4188c0a3d6753a63a608c`
**Checkpoint tree:** `aecaf5c686e0047d628705ce2e94d02db9449ea9`
**Source verdict:** `docs/review/harmonize-downstream-capability-specs-task-6-1-review-remediation-independent-review-verdict-2026-08-23.md`

The approved sixteen-path Task 6.1 remediation is preserved as an immutable checkpoint. This
separate target closes every S1-S7 observation from its independent review without changing
product source or starting Task 6.2.

## Observation dispositions

- **S-1:** both leased retry fixtures race the `SecondStarted` notification against workflow
  completion through `AwaitSignalBeforeWorkflowCompletionAsync`. A workflow that terminates without
  the expected retry now fails with a named diagnostic instead of hanging on a bare task await. The
  recursive lease guard requires both helper calls and forbids the superseded bare-await shape.
- **S-2:** the corpus guard reads the canonical and active reshape lifecycle requirement blocks,
  requires them to remain byte-equivalent, and pins the callback-local scope-handle clause in both.
- **S-3:** `ApprovalAwaitingEvidenceCommit` and `Approved` accept one or more immutable approval
  rounds without inventing rejection history. The design records that first-pass approval is valid
  and that `stateEvidencePath` identifies the verdict governing the current transition.
- **S-4:** CR-009a CI evidence is parsed as one exact named workflow step and is checked for the
  precise Core test project, Release/no-build/no-restore options, and requirement filter. It reuses
  the workflow-step parser that protects the Infrastructure lane.
- **S-5:** the active-freeze self-test invokes the real worktree and committed-blob content-record
  builders and compares each result with independently constructed bytes and SHA-256 values.
- **S-6:** the synchronized workflow-authoring requirement is rewrapped identically in canonical
  and active reshape owners so the normative sentence no longer strands `operator through a`.
- **S-7:** Task 5.3 now exercises the `OwnerAuthorized` terminal state with its existing committed
  owner-authorization evidence instead of leaving that transition unused.

## Provenance disposition

The new Task 6.1 remediation verdict is registered byte-for-byte under
`ApprovalAwaitingEvidenceCommit`. Its reviewed target is checkpoint `9f4fa0b5...` and its tree is
`aecaf5c6...`. This hardening target has its own self-inclusive request, raw-order manifest, and
scoped active-freeze content record; the provenance fixture is the sole self-reference exclusion.
Schema 9 also promotes the now-committed sixteen-path remediation freeze into an executable
`archivedFreezes` record. Its raw manifest, parent, checkpoint tree, exact changed paths, and
committed scoped content record are revalidated after the new active freeze replaces it.

## Validation packet

- Debug and Release full solution builds: 0 warnings, 0 errors.
- Core 350/350; Ephemeral 79/79; Durable 98/98; Acceptance 37/37; Hosting 24/24;
  ProviderCertification 96/96.
- `Requirement=CR-009a`: 20/20; OpenSpec corpus: 5/5; lease/governance infrastructure: 3/3.
- Infrastructure 216/216; ExpectedRed exactly 14/14 intentional failures; full guards 216 passed /
  14 failed / 230 total.
- OpenSpec strict validation: 18/18.
- harmonization ledger: 19 complete / 15 open / 34 total.
- `git diff --check`: clean.

## Scope

No `src/**` file is changed. Task 6.2 stays open. This target is review hardening for Task 6.1 only.
