# Harmonization Tasks 7.2 BBB-1 and 7.3 remediation independent review request

**Date:** 2026-09-20
**Requested verdict:** `APPROVE` or `REJECT`
**Authorization requested:** create one combined checkpoint for Task 7.2 provenance hardening and
the remediated Task 7.3 active-documentation reconciliation

Review this frozen target on immutable base
`89e3ed55357e849852c1a0f6fefa2124433d7e30`. Do not edit, stage, or commit it.
The raw-order manifest is
`harmonize-downstream-capability-specs-task-7-2-bbb-1-and-task-7-3-remediation-dirty-manifest-2026-09-20.txt`.
The rejected predecessor remains byte-exact under its 2026-09-18 request/manifest and 2026-09-20
verdict; the provenance fixture records it as `7.2-aaa-1-and-7.3-rejected`.
This request does not authorize Task 7.4, Task 7.5, final archival, or Section 8.

## Part A — Task 7.2 approval-evidence hardening

The Task 7.2 `approvalEvidenceCommit` is corrected to the real evidence commit
`087817776fe5747ed2d1b153f2dd52d26386b164`. Approved and owner-authorized registry entries now
fail closed unless that field resolves to a real commit, is an ancestor of `HEAD`, contains the
registered state-evidence path, and carries the exact normalized verdict bytes currently registered.
Null, all-zero, missing, non-ancestor, wrong-path, and content-mismatched evidence therefore cannot
survive merely because the verdict file exists in the worktree.

The prior combined review's REJECT verdict is registered byte-exact in Task 7.2's verdict evidence,
in the append-only document registry, and in a rejected-freeze record with its original 36-line
manifest. The previous request, manifest, and verdict remain immutable.

Required Task 7.2 reviewer controls:

1. Replace Task 7.2's approval commit with `null`, all zeroes, or an unresolved SHA; the provenance
   guard must fail before active-freeze validation.
2. Point the field at a real commit that does not contain the state verdict, or mutate the committed
   verdict bytes; the guard must fail with the evidence path named.
3. Revert the corrected SHA to `087817731b94...`; the guard must fail because that object does not
   resolve.
4. Drop or edit the 2026-09-20 REJECT verdict, its Task 7.2 evidence row, its append-only row, or the
   `7.2-aaa-1-and-7.3-rejected` freeze; the provenance/immutable-history lane must fail.

## Part B — Task 7.3 contract and coverage remediation

The same 22 Task 3.1-owned active documents remain in scope, while
`docs/orleans-engine/README.md` remains untouched for Task 7.4. The dated disposition artifact now
contains the exact ordered path list plus an LF-normalized whole-file SHA-256 for every source, and
guard source owns the artifact digest. Reverting or merely rewording any governed document without
updating the reviewed artifact and guard terminus is red.

The remediated contract records the shipped semantics rather than the rejected paraphrase:

- durable inbound `EventId` is caller-created, `CorrelationId` is required, and internal
  provider/wait/fiber/scope identities remain runtime-owned;
- `OrcaCore.Durable.Hosting` owns the `IWorkflowEventDispatcher` port type, the application
  registers its implementation, and the durable engine consumes it for authored `Publish`;
- AC-113 retains persisted per-attempt timeout/deadline/number behavior and the distinct
  `StepAttemptTimeoutException` winner;
- AC-117 retains rejection of leading/trailing whitespace, exact scalar round-trip, and identical
  ordinal/case-sensitive behavior regardless of provider collation;
- EV-045 remains bound through AC-116 to both structural `Wait` and dynamic
  `StepResult.WaitForEvent` ownership;
- MG-030 again requires retained operational statistics through `IWorkflowOperationalStore`; and
- DU-032/PR-015 retain observability and retry-delay strategy hooks on both dispatch families.

AC-108 and AC-116 no longer rely on stale waivers. AC-108 is attached to the three real fanout
provider certifications; AC-116 covers the closed route/result surface and both execution modes.
AC-118/119/120 traits moved off the Markdown guard and onto the real reflection, durable publish,
adapter-isolation, retry-identity, broker-acknowledgement, and poison-path tests. The independent
acceptance-catalog guard now corroborates every criterion.

The Task 7.3 completion record also fixes the Task 7.20 prose grammar and discloses this remediation.
New prose is wrapped; the disposition artifact contains no trailing whitespace.

Required Task 7.3 reviewer controls:

1. Restore runtime ownership of inbound `EventId`, optional correlation, or engine registration of
   the application dispatcher in any governed source; the Task 7.3 guard must fail.
2. Delete or weaken one restored AC-113, AC-117, EV-045/AC-116, MG-030, DU-032, or PR-015 clause;
   the corresponding whole-file hash must fail.
3. Remove each AC-108/116/118/119/120 trait from its real test evidence. The pre-existing acceptance
   catalog must fail, and no removed waiver or Markdown-only replacement may satisfy it.
4. Revert `CLAUDE.md`, `docs/normative-source-map.md`, or `docs/specs/01-concept-and-goals.md`
   wholesale, or rephrase one stale claim without using a known forbidden token. The per-document
   SHA must fail and name the source.
5. Drop, reorder, duplicate, or coherently alter one artifact row without updating guard source;
   exact path ordering, row numbering, source hash, or the artifact digest must fail.
6. Confirm Task 7.4 remains open and the Orleans guide is absent from this target.

## Validation to reproduce

- Debug solution build: 0 warnings, 0 errors.
- Release non-incremental `-warnaserror` solution build: 0 warnings, 0 errors.
- Focused Task 7.2/7.3, acceptance-catalog, and accounting guards: green.
- Infrastructure disposition: 224/224; ExpectedRed disposition: exactly 14 intentional failures.
- Core / Ephemeral / Durable / Acceptance / Hosting / ProviderCertification: 350 / 79 / 99 / 37 /
  24 / 96.
- PostgreSQL / SQL Server / Integration: 101 / 72 / 11.
- Strict OpenSpec: 18/18.
- Harmonization ledger: 27 complete / 7 open / 34 total.
- `git diff --check`: clean.

Also run the current-match refresh in `-Check` mode and reproduce the raw manifest and scoped
content-record anchors reported with the handoff. Confirm there are no `src/**`, canonical
`openspec/specs/**`, or Orleans-path changes.
