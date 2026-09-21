# Harmonization Tasks 7.2 AAA-1 and 7.3 combined independent review request

**Date:** 2026-09-18
**Requested verdict:** `APPROVE` or `REJECT`
**Authorization requested:** create one combined checkpoint for Task 7.2 AAA-1 hardening and completed Task 7.3 documentation reconciliation

Review this frozen target on immutable base
`89e3ed55357e849852c1a0f6fefa2124433d7e30`. Do not edit, stage, or commit it.
The raw-order manifest is
`harmonize-downstream-capability-specs-task-7-2-aaa-1-and-task-7-3-dirty-manifest-2026-09-18.txt`.
The approved Task 7.2 YY-1/ZZ-1 checkpoint is
`421c39b1dd39a3742907a2f7b7bbeb963b14f6ba`, its evidence commit is
`087817776fe5747ed2d1b153f2dd52d26386b164`, and its activation commit is the base above.
This request does not authorize Task 7.4, Task 7.5, final archival, or Section 8.

## Part A — Task 7.2 AAA-1 hardening

`ValidateAppendOnlyHistoricalRecords` no longer requires exactly one first-addition commit.
Every addition of a catalogued path discovered through the existing `--full-history` query must
reproduce the catalogued LF-normalized byte count and SHA-256. Re-adding identical normalized
content is valid; any differing addition fails and names the offending commit.

The executable semantics regression accepts two synthetic identical additions, including CRLF/LF
normalization, and rejects a divergent second addition. The Task 7.2 ledger, design decision, and
active archive rule state the same contract and remain exact-text guarded.

Required AAA-1 reviewer controls:

1. Merge an identical independent addition and confirm Task 7.2 remains green.
2. Change either addition to different normalized content and confirm the guard is red.
3. Restore the former single-addition assertion and confirm the identical merge is red.
4. Remove or weaken the ledger, design, or archive wording and confirm the focused guard is red.

## Part B — Task 7.3 active documentation reconciliation

Task 3.1 recorded 23 stale active documents. This target reconciles the exact 22 assigned to Task
7.3; `docs/orleans-engine/README.md` remains untouched and owned by Task 7.4. The dated disposition
is `openspec/changes/harmonize-downstream-capability-specs/artifacts/task-7-3-active-documentation-reconciliation-2026-09-18.md`.

The reconciliation makes these current contracts explicit across architecture, implementation,
production-readiness, developer, and numbered requirement/acceptance sources:

- durable `IWorkflowEventIngress` over fixed-codec `WorkflowInboundEvent`;
- exactly direct, correlation, definition-fanout, and start-or-deliver routes;
- global `EventId` plus full normalized-envelope fingerprint before route state;
- accepted pre-wait ownership without broker redelivery, hot-instance dependence, or automatic TTL;
- `Accepted`/`Duplicate` as the only source-acknowledgement outcomes and observable poison for unresolved ownership;
- stable fanout membership with independent per-target deduplication;
- durable authored `Publish` committed through the workflow-event outbox and dispatched only through `IWorkflowEventDispatcher`;
- exact ephemeral, durable, callback, in-memory, PostgreSQL, SQL Server, and DAG hosting roles;
- nonreplaceable `orcacore-json-v1`; and
- no broad application enumeration/statistics or public archive/purge, with provider/operator ownership through `IWorkflowOperationalStore` and `IWorkflowProviderMaintenanceStore`.

`docs/specs/05-requirements-events-waits-timers.md` owns EV-010/012/030/031/032/042/060.
`docs/specs/12-acceptance-criteria.md` owns AC-104 through AC-120 for the reconciled event contract.
The new infrastructure guard binds the exact 22-path list, the dated artifact hash, the Task 3.1
provenance, the numbered owners, and a closed set of stale claims. One added `[Fact]` is reflected
in the declaration crosswalk and the maintained Task 7.20 total.

Required Task 7.3 reviewer controls:

1. Reintroduce `IWorkflowEventClient`, `NoActiveWait`, deferred fanout/Publish, a two-route claim, or `EventDeliveryStatus` in any of the 22 sources; the Task 7.3 guard must fail and name the source.
2. Drop or reorder one artifact path; the exact 22-path assertion must fail.
3. Remove EV-010/030/031/060, AC-104/108/116/118/119/120, or one required ownership/hosting marker; the focused guard must fail.
4. Reopen Task 7.3, remove its completion evidence, or alter the artifact coherently without changing the guard-source digest; the guard must fail.
5. Confirm Task 7.4 remains open and the Orleans guide is absent from this target.

## Validation to reproduce

- Debug solution build: 0 warnings, 0 errors.
- Release non-incremental `-warnaserror` solution build: 0 warnings, 0 errors.
- Focused Task 7.2/7.3 and accounting guards: green.
- Infrastructure disposition: 224/224; ExpectedRed disposition: exactly 14 intentional failures.
- Core / Ephemeral / Durable / Acceptance / Hosting / ProviderCertification: 350 / 79 / 99 / 37 / 24 / 96.
- PostgreSQL / SQL Server / Integration: 101 / 72 / 11.
- Strict OpenSpec: 18/18.
- Harmonization ledger: 27 complete / 7 open / 34 total.
- `git diff --check`: clean.

Also run the current-match refresh in `-Check` mode and reproduce the raw manifest and scoped
content-record anchors reported with the handoff. Confirm no `src/**`, canonical `openspec/specs/**`,
or Orleans path changed.
