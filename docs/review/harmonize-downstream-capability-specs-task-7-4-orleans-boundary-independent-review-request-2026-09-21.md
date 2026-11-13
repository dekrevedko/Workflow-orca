# Harmonization Task 7.4 Orleans-boundary independent review request

**Date:** 2026-09-21
**Requested verdict:** `APPROVE` or `REJECT`
**Authorization requested:** checkpoint Task 7.4 and its disclosed Task 7.2/7.3 review carry-forward

Review this frozen target on immutable base
`7b1bf74ee83c97a19f2c8b08ddcd30d54d443658`. Do not edit, stage, or commit it.
The raw-order manifest is
`harmonize-downstream-capability-specs-task-7-4-orleans-boundary-dirty-manifest-2026-09-21.txt`.
Perform the review in the dedicated review worktree. This request does not authorize Task 7.5,
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

1. Add a second active file under `docs/orleans-engine/`, remove the active note, or restore the
   stale two-route/`NoActiveWait` claim; each mutation must fail.
2. Change, remove, or add an archived Orleans-plan file; the archive count, row set, or digest must
   fail.
3. Remove the new-change prerequisite, one ordinary cold-capable `Wait`, the four-route retained
   ingress contract, application-owned dispatch, fixed codec, or package-tier ownership; each must
   fail with the active note named.
4. Add Orleans source, package, migration, or implementation-task material without a new approved
   change; the existing repository-boundary checks must remain red.

## Part B — approved-review carry-forward

This same frozen target closes all six non-blocking KKK-1 findings from the approved Task 7.2/7.3
checkpoint:

- the Markdown corpus guard rejects every `Trait("AC", "…")` spelling recognized by the acceptance
  catalog, including combined `[Fact, Trait(...)]` and qualified `[Xunit.Trait(...)]` forms;
- PR-040's durable builder/ingress/dispatcher list and provider/DAG no-split rule are semantic
  assertions that survive a coherent source-row and artifact-digest refresh;
- approval-evidence parent-count and parent-mismatch diagnostics name their task;
- only historical Task 5.3 may use the retroactive owner-authorization lineage rule;
- EV-032 now states that a pre-commit crash leaves the wait `Active`; and
- the previously truncated approval-history decision is complete and executable.

The intentional EV-032 wording correction refreshes exactly one Task 7.3 source row and the
guard-source-owned Task 7.3 artifact digest in this same reviewed target.

Required carry-forward controls:

1. Put either alternate AC-trait spelling on `OpenSpecCorpusGuards`; Task 7.3 must fail.
2. Delete either newly explicit PR-040 clause, coherently refresh document 10's row and the artifact
   digest, and rebuild; Task 7.3 must still fail on the missing semantic clause.
3. Relax the retroactive owner rule to another task; the provenance guard's synthetic control must
   fail independently of current fixture data.
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
- Infrastructure disposition: 225/225; ExpectedRed disposition: exactly 14 documented failures.
- Focused Task 7.3, Task 7.4, review-provenance, crosswalk, and accounting guards: green.
- Strict OpenSpec: 18/18.
- Harmonization ledger: 28 complete / 6 open / 34 total.
- Current-match refresh `-Check`: clean.
- `git diff --check`: clean.

Also reproduce the raw manifest and scoped content-record anchors supplied with the handoff, then
simulate the checkpoint with an isolated index. Confirm the target changes no `src/**`, canonical
`openspec/specs/**`, or archived Orleans-plan file.
