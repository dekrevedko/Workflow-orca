# Harmonization Tasks 7.4 and 7.5 LLL-1/MMM-1 remediation independent review request

**Date:** 2026-09-22
**Requested verdict:** `APPROVE` or `REJECT`
**Authorization requested:** checkpoint the combined Task 7.4 and Task 7.5 target after review remediation

Review this frozen target on immutable base
`7b1bf74ee83c97a19f2c8b08ddcd30d54d443658`. Do not edit, stage, or commit it.
Use the dedicated `Workflow-orca-review-task-7-4-7-5` worktree. The raw-order manifest is
`harmonize-downstream-capability-specs-task-7-4-and-7-5-lll-1-mmm-1-remediation-dirty-manifest-2026-09-22.txt`.
This request does not authorize Task 7.6, final harmonization exit, reshape Task 8.0, or Section 8.

## Prior rejection and immutable evidence

The 2026-09-21 combined target was rejected by verdict
`harmonize-downstream-capability-specs-task-7-4-and-7-5-combined-independent-review-verdict-2026-09-21.md`
(13,548 bytes, SHA-256
`7af6d9be8651d2526fe8318a67c4dc8b5c9e09f542f88c7c215f5c05112ef6b7`).
The rejected request, manifest, and verdict remain byte-exact, append-only, and registered as a
rejected freeze. The dated remediation artifact
`task-7-4-and-7-5-review-remediation-2026-09-22.md` records every change below and explicitly
does not authorize a checkpoint.

## LLL-1 — exact historical classifier replay

Task 7.5 no longer accepts one unrelated finding per historical file. Its evidence artifact records
the complete ordered set of 56
`HISTORICAL<TAB>path<TAB>line<TAB>classifier` results from the 23 Task 3.1 sources at
`89e3ed55357e849852c1a0f6fefa2124433d7e30`. The guard recomputes that same ordered result and
requires every named classifier to occur.

Required controls:

1. Delete the definition-fanout classifier even though another finding remains in every historical
   file; Task 7.5 must fail.
2. Delete the complete `returns/yields ... NoActiveWait` classifier alternative; Task 7.5 must
   fail.
3. Change a historical result's path, line, or classifier in the artifact; Task 7.5 must fail.
4. Retain the implementation but narrow one of the historical results through a regex change; the
   complete result comparison must fail.

## MMM-1 — current event vocabulary

- DU-055 now names `WorkflowEventAcceptanceResult`, the exact EV-012 result.
- AC-005 now says direct durable terminal ingress returns
  `Rejected(DirectInstanceTerminal)`.
- The superseded `EventDeliveryResult`, `EventDeliveryStatus`,
  `IWorkflowEventClient`, and `DeliverToInstanceAsync` vocabulary is classified as stale.
- Task 7.3 artifact rows 16 and 19 alone are refreshed, followed by the artifact's independent
  guard-source digest.

Required controls:

1. Restore `EventDeliveryResult` in DU-055; Task 7.3 and Task 7.5 must fail.
2. Restore “event delivery returns `InstanceTerminal`” in AC-005; both guards must fail.
3. Coherently refresh the Task 7.3 row and mutable artifact digest after either regression; the
   guard-source pin and semantic assertions must still fail.

## NNN-1 — observations remediated

- Eight natural rewordings are classified, including `DeliverToInstanceAsync`,
  `yields NoActiveWait`, instance/correlation-only routing, and deferred durable `Publish`.
- The Task 7.5 artifact's 86-source count is explicitly a checkpoint snapshot. Runtime enumeration
  remains dynamic, and adding a benign active document does not require fixture churn.
- The Task 7.4 artifact has no trailing whitespace, including in the committed-diff simulation.
- Task 7.4 semantically pins the numbered independent-approval prerequisite and Orleans-only
  adapter boundary instead of relying only on the active-note hash.
- The archived Orleans inventory is derived from disk and compared with the immutable fixture.
- Every active Orleans task-ledger block is enumerated. An added implementation task fails even
  after the active freeze ends.
- The implementation README retains paragraph-continuation indentation.
- This request uses the singular `task-7-4-` discovery stem.

Required controls:

1. Insert all eight reviewed stale rewordings in an active document; Task 7.5 must fail with path
   and line evidence.
2. Add a benign active document; Task 7.5 must scan it without treating the recorded 86 as a live
   corpus-size invariant.
3. Remove either numbered Task 7.4 prerequisite clause or expose an internal engine/provider seam
   through the Orleans adapter paragraph; Task 7.4 must fail.
4. Add an Orleans implementation task to any active change ledger; Task 7.4 must fail.
5. Change an archived Orleans file on disk while coherently editing the mutable Task 7.4 artifact;
   the immutable archive fixture comparison must fail.

## Scope and validation

The target changes no `src/**`, canonical `openspec/specs/**`, or archived Orleans-plan file.
Tasks 7.4 and 7.5 remain the only newly completed tasks; Task 7.6 remains open.

Reproduce:

- Debug and Release non-incremental `-warnaserror` solution builds: 0 warnings, 0 errors.
- Core / Ephemeral / Durable / Acceptance / Hosting / ProviderCertification:
  350 / 79 / 99 / 37 / 24 / 96.
- PostgreSQL / SQL Server / Integration: 101 / 72 / 11.
- Infrastructure disposition: 226/226; ExpectedRed disposition: exactly 14 documented failures.
- Focused Task 7.3, Task 7.4, Task 7.5, provenance, crosswalk, and accounting guards: green.
- Strict OpenSpec: 18/18.
- Harmonization ledger: 29 complete / 5 open / 34 total.
- Current-match refresh `-Check`: clean.
- Worktree and simulated committed `git diff --check`: clean.

Reproduce the raw manifest and scoped content-record anchors supplied with the handoff. Simulate the
checkpoint from a copy of the live index and stage only the manifest paths; confirm that no hidden
line-ending-only paths enter the tree.
