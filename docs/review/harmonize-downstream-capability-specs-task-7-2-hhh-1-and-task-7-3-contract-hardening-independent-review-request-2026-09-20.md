# Harmonization Tasks 7.2 HHH-1 and 7.3 contract-hardening independent review request

**Date:** 2026-09-20
**Requested verdict:** `APPROVE` or `REJECT`
**Authorization requested:** create one combined checkpoint for Task 7.2 approval-evidence
lineage hardening and the remediated Task 7.3 active-documentation contract

Review this frozen target on immutable base
`89e3ed55357e849852c1a0f6fefa2124433d7e30`. Do not edit, stage, or commit it.
The raw-order manifest is
`harmonize-downstream-capability-specs-task-7-2-hhh-1-and-task-7-3-contract-hardening-dirty-manifest-2026-09-20.txt`.
The BBB-1/Task 7.3 predecessor remains byte-exact under its 2026-09-20 request, manifest, and
`REJECT` verdict; the provenance fixture records it as
`7.2-bbb-1-and-7.3-remediation-rejected`.
This request does not authorize Task 7.4, Task 7.5, final archival, or Section 8.

## Part A — HHH-1 approval-evidence lineage

An independently approved registry entry now accepts an `approvalEvidenceCommit` only when all of
these conditions hold:

1. the exact full object ID resolves and remains in the current lineage;
2. it is a single-parent, non-merge commit whose parent is exactly the entry's
   `reviewedTargetCommit`;
3. it adds the registered governing verdict path; and
4. the committed verdict reproduces the registered normalized bytes.

This closes the prior containment-only gap: a later activation or unrelated evidence commit may
contain a verdict, but it cannot stand in for the distinct commit that introduced approval evidence
directly after the reviewed target. Every currently independently approved entry satisfies the
stronger rule without changing its recorded objects.

Task 5.3 is the one explicit owner-authorization exception. Its immutable verdict says that the
authorization was recorded retroactively and that a later mechanical transition may first preserve
the disclosure. That evidence commit must still be a single-parent descendant of the reviewed
target and must add the verdict; the registry does not falsify its parent as the older target.

Required Task 7.2 reviewer controls:

1. Point Task 7.2 at activation commit `89e3ed55357e849852c1a0f6fefa2124433d7e30`;
   the guard must fail on parentage and/or verdict introduction.
2. Point Task 7.1 at that activation commit; the guard must fail.
3. Point Task 6.6 at Task 7.2's evidence commit; the guard must fail.
4. Use a merge commit, a commit whose parent is not the reviewed target, or a commit that merely
   contains rather than adds the verdict; each must fail with the task/evidence path named.
5. Drop or edit either combined Task 7.2/7.3 rejection, its rejected-freeze record, its Task 7.2
   verdict row, or its append-only catalog row; the provenance lane must fail.

## Part B — III-1 and JJJ-1 contract corrections

The same 22 Task 3.1-owned sources remain governed, and the Orleans note remains exclusively owned
by open Task 7.4. The corrected numbered contract now:

- lists all five closed ingress rejection variants in AC-116, including
  `DirectInstanceTerminal`;
- states in AC-114 that a pre-commit crash leaves the wait `Active` and the accepted record
  re-matchable;
- describes only the groups and pressure values actually returned by
  `IWorkflowOperationalStore`, explicitly making no age-bucket promise;
- restores PR-040's exact engine/hosting/provider/DAG extension ownership and no-split rule; and
- retains the application-registered dispatcher and every previously restored Section 7B
  obligation.

The Task 7.3 guard cannot carry an `AC` trait itself. Acceptance evidence must stay on the real
product, provider, hosting, and engine tests, so moving a criterion back to the Markdown corpus
guard is red even if the general acceptance catalog would otherwise see the trait.

The design, task completion, and dated Task 7.3 artifact now assign whole-file-pin refresh
authority. Only the owner of a reviewed change intentionally editing a governed source may refresh
its row. Task 7.4 or Section 8 may do so only in the same frozen target that edits the source and
updates the artifact row, guard-source digest, and review evidence; a later mechanical refresh may
not legitimize an earlier unreviewed edit.

Required Task 7.3 reviewer controls:

1. Remove `DirectInstanceTerminal` from AC-116 or restore the old four-variant wording; the
   semantic guard and whole-file pin must fail.
2. Remove the `Active` wait state from AC-114, restore `age groups` in MG-030, or delete one
   PR-040 ownership sentence; each mutation must fail with the owning document named.
3. Add `[Trait("AC", "AC-116")]` to the Markdown corpus guard while removing it from real test
   evidence; the corpus guard must fail independently of the general catalog.
4. Remove or weaken the pin-refresh authority in the design, ledger, or artifact; the focused guard
   must fail even if document hashes are coherently refreshed.
5. Recompute the 22 artifact rows independently and confirm only
   `docs/specs/09-requirements-management-operations.md`,
   `docs/specs/10-provider-model-and-extensibility.md`, and
   `docs/specs/12-acceptance-criteria.md` changed from the rejected target for these contract
   corrections.

## Provenance and validation

The prior BBB request, manifest, and verdict are immutable and registered in all three required
places: Task 7.2 verdict evidence, the append-only historical catalog, and a rejected-freeze record.
The new request and raw manifest are also append-only records and the active-freeze descriptor
binds their exact target.

Reproduce:

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

Also run the current-match refresh in `-Check` mode, reproduce the raw manifest and scoped
content-record anchors reported with the handoff, and simulate the checkpoint using an isolated
index. Confirm there are no `src/**`, canonical `openspec/specs/**`, or Orleans-path changes.
