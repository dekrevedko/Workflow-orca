# Harmonization Tasks 7.4 and 7.5 combined independent review request

**Date:** 2026-09-21
**Requested verdict:** `APPROVE` or `REJECT`
**Authorization requested:** checkpoint the combined Task 7.4 remediation and Task 7.5 recurring
active-tree documentation guard

Review this frozen target on immutable base
`7b1bf74ee83c97a19f2c8b08ddcd30d54d443658`. Do not edit, stage, or commit it.
The raw-order manifest is
`harmonize-downstream-capability-specs-task-7-4-and-7-5-combined-dirty-manifest-2026-09-21.txt`.
Perform the review in the dedicated review worktree. This request does not authorize Task 7.6,
final archival, reshape Task 8.0, or Section 8 implementation.

## Part A — Task 7.4 Orleans boundary

The target closes the final Task 3.1 Orleans finding without reactivating the superseded plan:

- `docs/orleans-engine/README.md` is the only active file below `docs/orleans-engine/`;
- it is a non-authorizing future boundary requiring a new independently approved OpenSpec change;
- it uses one ordinary cold-capable `Wait`, caller-created inbound identity, all four durable routes,
  retained pre-wait ownership, and the transactional workflow-event outbox;
- the application owns the dispatcher implementation while durable hosting owns the ingress and
  dispatcher port, the durable engine owns execution, and providers own storage registration;
- the fixed `orcacore-json-v1` codec remains mandatory; and
- all 25 files below `docs/archive/plans/orleans-engine-pre-v1/` remain byte-unchanged.

The dated Task 7.4 artifact records the one active-note digest plus an ordinal, LF-normalized record
for all 25 archived files. The must-green guard independently derives both inventories and hashes.

Required Task 7.4 controls:

1. Add a second active file below `docs/orleans-engine/`, remove the active note, or restore the
   stale two-route/`NoActiveWait` claim; each mutation must fail.
2. Change, remove, or add an archived Orleans-plan file; the archive count, row set, or digest must
   fail.
3. Remove the new-change prerequisite, ordinary cold-capable `Wait`, four-route retained ingress,
   application-owned dispatch, fixed codec, or package-tier ownership; each must fail.
4. Add Orleans source, package, migration, or implementation-task material without a new approved
   change; the repository-boundary checks must remain red.

## Part B — Task 7.5 recurring active-tree documentation guard

Task 7.5 turns Task 3.1's classifications into a recurring must-green guard:

- it enumerates the same evolving root-guidance, active-docs, canonical-spec, and active-change
  corpus as Task 7.1;
- it excludes immutable `docs/archive/` and `docs/review/` evidence;
- it reuses the Task 7.1 positive removed/deferred call expressions;
- it pins the complete Task 3.1 artifact by guard-source SHA-256 and extracts its exact 23 stale
  paths;
- it replays each path at pre-reconciliation commit
  `89e3ed55357e849852c1a0f6fefa2124433d7e30` and requires at least one classified stale claim,
  proving the fixture remains complete rather than merely passing on corrected current text; and
- it requires zero positive removed/deferred calls and zero stale Section 7B claims in the current
  evolving active corpus.

The classifier covers the superseded `IWorkflowEventClient`/`EventDeliveryStatus` surface,
non-buffering pre-wait delivery, two-route ingress, deferred definition fanout, deferred durable
`Publish`, and unapproved Section 7B assertions. Explicitly marked replacement/supersession
sentences remain searchable planning history.

The first recurring run found one real stale source outside Task 3.1's 23-path snapshot:
`docs/implementation/README.md` still called Section 7B pending and non-authoritative. This target
corrects it to the approved Section 7A/7B checkpoint while preserving the harmonization gate before
Section 8. The dated Task 7.5 artifact records 86 evolving sources, 23 historical fixture paths,
and zero current findings.

Required Task 7.5 controls:

1. Add `WaitLong(` or another positive removed/deferred call to any active source; the guard must
   fail with the path, line, and classification.
2. Add a stale “Pending Section 7B”, non-buffering `NoActiveWait`, deferred fanout/publish, legacy
   client/status, or two-route assertion; the guard must fail with the path and line.
3. Narrow or delete a stale classifier; at least one of the 23 historical fixture replays must fail.
4. Put the same text only under `docs/archive/` or `docs/review/`; the recurring active-tree guard
   must ignore immutable evidence while the separate history guard continues to byte-pin it.
5. Revert `docs/implementation/README.md` to its pending-Section-7B paragraph; the guard must fail.

## Part C — approved-review carry-forward

This combined target also retains all six KKK-1 fixes from the approved Task 7.2/7.3 checkpoint:

- the Markdown corpus guard rejects combined and qualified AC-trait spellings;
- PR-040's durable builder/ingress/dispatcher list and provider/DAG no-split rule are semantic
  assertions that survive a coherent row and digest refresh;
- approval-evidence lineage diagnostics name their task;
- only Task 5.3 may use the retroactive owner-authorization exception;
- EV-032 says a pre-commit crash leaves the wait `Active`; and
- the approval-history design sentence is complete and pinned.

The intentional EV-032 correction refreshes exactly one Task 7.3 source row and its guard-source
artifact digest in this reviewed target.

Required carry-forward controls:

1. Put either alternate AC-trait spelling on `OpenSpecCorpusGuards`; Task 7.3 must fail.
2. Delete either explicit PR-040 clause, coherently refresh document 10's row and artifact digest,
   and rebuild; Task 7.3 must still fail on the missing semantic clause.
3. Relax the retroactive owner rule to another task; the synthetic provenance control must fail.
4. Remove EV-032's `Active` wording or the completed approval-history decision; the focused guard
   must fail.

## Provenance and validation

The approved Task 7.2/7.3 checkpoint is `9e921753eeadf71fcb641b6c91002fc89541d6df`.
Evidence commit `0b979942ebfe9a32c38485a7da0c00c596226caa` adds the governing verdict,
and activation commit `7b1bf74ee83c97a19f2c8b08ddcd30d54d443658` changes only the two transition
values. The governing verdict is byte-exact and every archived freeze remains executable.

Reproduce:

- Debug and Release non-incremental `-warnaserror` solution builds: 0 warnings, 0 errors.
- Core / Ephemeral / Durable / Acceptance / Hosting / ProviderCertification:
  350 / 79 / 99 / 37 / 24 / 96.
- PostgreSQL / SQL Server / Integration: 101 / 72 / 11.
- Infrastructure disposition: 226/226; ExpectedRed disposition: exactly 14 documented failures.
- Focused Task 7.1, 7.3, 7.4, 7.5, review-provenance, crosswalk, and accounting guards: green.
- Strict OpenSpec: 18/18.
- Harmonization ledger: 29 complete / 5 open / 34 total.
- Current-match refresh `-Check`: clean.
- `git diff --check`: clean.

Also reproduce the raw manifest and scoped content-record anchors supplied with the handoff, then
simulate the checkpoint with an isolated index. Confirm the target changes no `src/**`, canonical
`openspec/specs/**`, or archived Orleans-plan file.
