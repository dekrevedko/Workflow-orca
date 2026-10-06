# DAG run contract independent review verdict

**Date:** 2026-10-04
**Reviewer:** independent review
**Scope reviewed:** the thirty-five-entry freeze on base `dab498ff54321d802b36ce052100954bbe00487d`,
named by
`developer-facing-interface-section-08-task-8-4-dag-run-contract-dirty-manifest-2026-10-04.txt`.
This is `admit-durable-dag-run-progression` task 1.1. It contains:
- the run contract (`task-1-1-run-contract-2026-10-04.md`, 43,056 B);
- the exact API catalog, the standalone signature proof and the four revised deltas;
- the revised proposal, design and tasks;
- proposed disposition blocks in 16 numbered, binding and guide documents;
- the reshape 8.4/8.5 and Task 8.0 map rows;
- guard pins and provenance.

**Authorization requested:** checkpoint of this contract target, then the 1.2-only activation.

This verdict authorizes no checkpoint, no canonical sync (1.3/1.4), no source and no archival.

## Summary

Most of the contract is careful, faithful to both trees, and executable as evidence.

- **Process chain.** The process chain is exact: `b55056c` → `e99f338` → `dab498f`.
- **Round-94 notes, all resolved:**
  - new SHALL text sits above the first scenario, and the OpenSpec parse confirms it;
  - ordinary decode failure is `DAG_INPUT_MAPPING_INVALID`;
  - child identity hashes only the run GUID and ordinal, with the binding checked separately;
  - `IMessageDispatcher` is the PR-015 internal port;
  - six planning files and 16 document blocks are pinned in guard source.
- **API compatibility.** The catalog is real. Its approved-baseline hashes equal the committed
  `OrcaCore`, `Runtime.Protocol` and `Provider.Abstractions` baselines, no existing key is
  removed, and the standalone proof compiles independently with only `OrcaCore`. Specifically:
  - `WorkflowStreamId` keeps every workflow signature and rendering;
  - the `ProviderCommitBatch` DAG branch is explicit;
  - the real `IWorkflowEventStore.AppendAsync` is used;
  - the 21 `WorkflowEventCodec` entries are unchanged, with a separate fixed record codec.
- **Persistence, routing and admission rules:**
  - five complete first-create tables with no `ALTER`;
  - exact claim selectors per host role;
  - wrong-lane release;
  - `CapacityDeferred` distinct from `AdmissionStopped`;
  - the current owner's host limit after restart;
  - the present-null bytes;
  - a separate DAG caller-key namespace.
- **Documents.** The status sentences are honestly "proposed / pending".

One blocking defect remains. Design Decision 5 required Task 1.1 to "define the fence and takeover
boundary precisely", and the design now records no open questions. But the contract never says
who holds a run's fence, how that holder obtains it through the bridge, or how long it may hold
it. Every run mutation depends on that lease, including joins and cancellation. As written, the
fenced-coordinator protocol is either unimplementable within the reviewed bridge allowlist or
unable to guarantee CP-024/CP-028 progress.

## Method

Validation ran in one disposable detached worktree at `dab498f`, and probes plus an independent
standalone compile in a second. Both held the same thirty-five entries.

- Every script asserted its disposable location and that the main `HEAD` was still `dab498f`.
- Main was never modified, and the review created no ref in the reviewed repository.
- Provenance was recomputed with my own script.
- Delta structure was checked against canonical block-by-block, and with
  `openspec show --json --deltas-only`.
- The contract was read in full against both trees and the current ports, outbox and
  start-idempotency code.

## 1. Freeze anchors and provenance

| Anchor | Claimed | Reproduced |
|---|---|---|
| Raw porcelain, `--untracked-files=all --no-renames` | 35 lines, 2,471 B, `fa360497…8a02` | identical; the NUL form has 35 entries; the union of diff and untracked paths is exactly 35; nothing staged |
| Semantic record: 31 rows | 4,179 B, `26b22536…e2da` | identical with the stated recipe |
| Active record: 34 rows | pinned in the fixture | 4,797 B, `38c40cc6de495e559445d9e96cf6056646a7707fe1d807b72fc629a1d07e79ad`, equal to the fixture |
| All-file content record, 35 rows (reviewer) | — | 4,952 B, `07ae56eb39ad07074538185239e87a6237afdd253c765dfd76d2e32559affa86` |
| Simulated checkpoint | tree `cde03fd24984418ccc40886c28f848e97ff795c4` (handoff) | identical from a copied index and from a committed disposable copy; parent `dab498f`; 6 A / 29 M; all blobs equal the raw bytes |
| Provenance (reviewer script) | 184 / 48,464 B / `bb7a3394…6470` | identical: 173 synchronized, 4 superseded, 4 `PendingModification`, 3 bootstrap-only; the multi-owner pairs are exactly the 2 runtime-view chains and the 4 registered pairs; capability directories 22 / 1,808 B / `2869d4ad…` |

- **Process chain:**
  - `b55056c` has tree `5880c859` and parent `095bbfd`;
  - `e99f338` is its only child; it adds my process APPROVE byte-exact (12,342 B, `f54a6ba5…`),
    catalogs it, clears the freeze and keeps the pointer;
  - `dab498f` checks only 0.2.
- **Deltas:** each preserves the canonical statement and scenarios verbatim, inserts its SHALL text
  before the first scenario, and appends one scenario. The four proposed hashes are re-pinned
  coherently in source and the registry.
- **Superseded catalog:** the 0.1 provenance artifact is unedited, and the catalog now has 9
  entries.
- **Reshape:** only the open 8.4/8.5 text, their two map rows and dated notes change.
- **Guard change:** the disclosed `RequireApprovalEvidence` change is a sound narrow fix.
  `task 0.1.` now matches, while `0.11` and `0.1.1` are still rejected, with in-process
  self-checks. No Fact or Theory is added.
- **Current-match refresh:** emulated, because Windows PowerShell 5.1 lacks `SHA256.HashData`.
  16 entries, 102 matches, 0 stale. That equals the refreshed fixture, so the projections are maximal.

## 2. Validation

| Lane | Result |
|---|---|
| Debug and Release non-incremental `-warnaserror` builds | 0 warnings, 0 errors |
| Exact package feed | 12 packages |
| Compile fixtures (`Green`) / public API baseline | green / 14/14 |
| Core / Ephemeral / Durable / Acceptance / Hosting / ProviderCertification | 350 / 79 / 99 / 37 / 24 / 96 |
| PostgreSQL / SQL Server / Integration | 101 / 72 / 11 |
| Package and compile fixtures, rerun | the first run failed only on a transient "not enough space on the disk" during NuGet restore; the rerun on the identical committed tree `cde03fd2` gave 8 green, exactly `dag-hosting` red, and compile fixtures green |
| `Disposition=Infrastructure`, Release | 243/243 |
| `Disposition=ExpectedRed` | exactly 14, all `ExecutableBehaviorExpectedRedGuards` scenarios |
| `OpenSpecCorpusGuards`, Release / strict OpenSpec | 15/15 / 21/21 |
| `git diff --check`, worktree and committed simulation | clean |
| Guards on the committed simulation | 243 passed plus the same 14 expected-red |
| Evidence state: committed simulation with the freeze cleared / 1.2 checked | Infrastructure 243/243 / 243/243 |
| Standalone signature proof (`OrcaCore` reference only, Release `-warnaserror`), independent compile | 0 warnings, 0 errors |

## 3. Blocking finding

### RC-1 (P2): fence ownership and lease scope are undefined, so the run lifecycle is not operable as specified

The contract makes the run lease the single mutex for all progression:
- §3: "For non-creation append, provider checks … matching live owner/run/token and unexpired
  lease";
- the durable-runtime delta: "serialize all progression under a live fence/expected version".

Three gaps follow.

1. **No bridge path obtains a fence.**
   - `IDurableDagRunBridgeV1.AdmitMappedInputAsync`, `CommitMappingFailureAsync`,
     `RecordIntegrityFaultAsync` and `ReconcilePoisonAsync` each require a `DagRunFenceV1`.
   - No member of the internal run bridge, or of the six-call child bridge, acquires, renews or
     releases one. Lease operations exist only on the public provider port
     (`IWorkflowEventStore.Acquire/Renew/ReleaseDagRunLeaseAsync`).
   - §4 requires that "the current leased progression owner alone reads mapping
     inputs/evaluates", and only Dag.Hosting can evaluate mappers. Dag.Hosting would therefore
     have to call the provider port directly, a second DAG-to-storage path outside the reviewed
     bridge and its promised "exact type/member metadata allowlist", which names no provider port.
2. **The coordinator's location is contradictory.**
   - §4 says the child bridge's `CommitMappedInputAsync`, which Durable.Hosting implements,
     "dispatches to the current owning run coordinator", and that "only that coordinator obtains
     the live fence, version and trusted host limit".
   - The trusted limit is `DagHostOptions` in Dag.Hosting, and Durable.Hosting cannot call into
     Dag.Hosting except through `IDurableDagReadySinkV1.HandleAsync(notice)`, which carries no
     admission request.
   - The contract never says which package hosts that coordinator.
3. **No lease-scope or hand-off rule exists.**
   - `ApplyChildTerminalAsync` runs from the every-host child pump and "acquires the current run
     owner".
   - `RequestRunCancellationAsync` "acquires/retries the same fenced mutation lane".
   - Acquire returns `HeldByOther` while another host's lease is live, and §6 describes owner
     heartbeat and renewal.
   - Nothing bounds how long an owner may hold the lease, releases it when idle, or hands a
     non-owner's join or cancellation to the owner.
   - A long-lived, renewing owner therefore starves joins claimed on other hosts, which can only
     release and retry, and a cancellation requested elsewhere can wait indefinitely. That
     contradicts CP-024 progression and CP-028 cancellation.

   The alternative, short per-transition leases, is workable. But it must be stated, because it
   changes Decision 5's guarantee that "competing hosts with different options cannot
   independently admit under their own ceilings".

**Required.** Freeze one concrete fence protocol, and reflect it in the internal signatures and the
promised metadata allowlist:
- name the coordinator's package;
- give it a bridge-level way to acquire, renew and release a run fence, or keep leases entirely
  inside a Durable.Hosting coordinator that drives Dag.Hosting only through the sink;
- define lease scope: per transition, or held with idle release;
- define how non-owner joins and cancellations make bounded progress: short-lease retry, an
  addressed hand-off to the owner, or committing those non-admission facts under version-CAS
  without the admission fence;
- reconcile that choice with Decision 5;
- add DG-P certification rows for non-owner join and cancellation liveness under a live owner.

## 4. Independent negative controls

All controls ran the focused `OpenSpecCorpusGuards` lane (15 tests). H1 to H14 ran with the active
freeze cleared, as the evidence commit will leave it.

| Control | Mutation | Result |
|---|---|---|
| H0 | unmodified, freeze active | green |
| H1 | evidence state: freeze cleared | green |
| H2 | planned activation: 1.2 checked only | green, as intended |
| H3 | task 1.3 checked | **red**: exact open task state |
| H4 | 1.2 task text edited, checkbox unchanged | **red**: whole `tasks.md` pin, where only the 1.2 checkbox is normalized |
| H5 | `CapacityDeferred` removed from the contract's mutation outcomes | **red**: whole contract pin |
| H6 | API catalog edited by one byte | **red**: catalog pin |
| H7 | fence parameter dropped from `AdmitMappedInputAsync` in the standalone proof | **red**: signature pin |
| H8 | doc 10 block relabeled "Approved" | **red**: document block pin and Task 7.3 row |
| H9 | proposed durable-runtime block hashes the workflow definition into child identity | **red**: proposed block hash |
| H10 / H11 | `design.md` / `proposal.md` edited by one byte | **red** / **red**: whole planning pins |
| H12 | reshape 8.4 checked | **red**: exact open task state |
| H13 | superseded 0.1 provenance artifact edited | **red**: permanent catalog |
| H14 | process APPROVE verdict edited by one byte | **red**: immutable history and approval evidence |
| H15 | freeze active, run contract edited by one byte | **red**: active content record and contract pin |
| R0 | restored | green; porcelain equals the manifest; the 35 target files and the tracked probe targets equal main byte-for-byte |

The pins themselves are sound. RC-1 concerns the decision they pin, not their enforcement.

## 5. Non-blocking observations (P3)

- **P3-1: behavioral scenarios were replaced by a process scenario.**
  - The process-gate deltas had behavior scenarios: stale-owner admission, `CapacityDeferred`,
    unused-output decode, wrong-lane release, role-free hosts and null bytes.
  - The revision replaces them with one identical scenario in all four blocks: "Full proposed DAG
    run successor is implemented after approval". That is process text, and 1.3 would sync it
    into canonical behavior specs verbatim.
  - Restore a behavioral scenario for each new obligation group.
- **P3-2: orphaned addressed notices need a disposition.** Observer notices addressed to a boot
  `HostId` become unclaimable when that host dies. The contract calls them "maintenance data" but
  defines no purge. Specify one, so they never count as pending messaging pressure or stuck work
  (canonical "Operational projections expose durable messaging pressure").
- **P3-3: storage grows with every transition.** Every DAG event carries a full run snapshot, and
  the checkpoint repeats it, so storage per run grows as node count times transitions. State the
  expected size bound, or allow delta events with checkpoint snapshots, before schema
  certification.
- **Recorded (expected):** the 16 "pending independent approval" status blocks become stale once
  1.2 lands. They are pinned, so refresh them in the 1.3/1.4 target, not in activation.

## 6. Reviewer hygiene and next step

`HEAD` is `dab498ff54321d802b36ce052100954bbe00487d`, with the thirty-five frozen entries and
nothing staged. When this verdict was written, the repository showed exactly the frozen entries
plus this new, untracked verdict. The disposable worktrees are removed.

Do not checkpoint this target. Keep this request and manifest byte-exact as a rejected freeze, and
catalog them as before. Revise the contract for RC-1, ideally also P3-1 to P3-3. Re-pin the planning
files, blocks and provenance coherently, then refreeze for a new independent review. Tasks 1.2–3.2
and reshape 8.4/8.5 stay open; no canonical sync and no source.

## Determination

The contract's identity, codec, persistence-shape, claim-partition, admission-outcome and
documentation decisions are sound and well pinned. It does not yet define the concurrency protocol
everything else depends on: which component holds the run fence, how it obtains it through the
bridge, and how joins and cancellation progress while another host owns the run.

**Verdict:** **REJECT**
