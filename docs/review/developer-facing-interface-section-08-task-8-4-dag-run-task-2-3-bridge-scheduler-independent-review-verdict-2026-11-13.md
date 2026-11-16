# DAG run Task 2.3 bridge and scheduler independent review verdict

**Date:** 2026-11-13
**Reviewer:** independent review
**Scope reviewed:** the 55-entry freeze in the main checkout `X:/Projects/GitHub/Workflow-orca`
(branch `feature/dag-run-task-2-3`) on base `048c3a6ecd3541f30c57d9f600190e63f6a16d84`
(tree `cc97c9e0…`), named by
`developer-facing-interface-section-08-task-8-4-dag-run-task-2-3-bridge-scheduler-dirty-manifest-2026-11-13.txt`.
It contains the Durable.Hosting run bridge (creation and reopen, run-input catalog, mapping reader,
admission, turns, cancellation, notifications, integrity reconciliation, child bridge), the
Dag.Hosting plan registry and scheduler, the child-engine role-key and schema changes, the round-104
F1 fix with provider certification, the design §15 clarification, tests, guards and metadata.

**Authorization requested:** checkpoint of this exact target and its direct-child evidence.
There is no activation.

This verdict authorizes that checkpoint and the evidence child. It authorizes no task checkbox, no
reshape 8.4/8.5 activation, and no public Task 8.6 registration, start or handle API.

## Summary

The bridge and scheduler implement the approved RC-3 contract, and round-104 F1 is fixed with a
regression test on all three providers. No defect was found. Three P3 items are recorded: two
scheduler/child checks lack regression tests (the code is correct), and deferral now waits the
full claim lease.

- **Task 2.2 chain.** Checkpoint `2fd7a7d` (tree `42770df9…`, parent `6028445`) and its only direct
  child `3dae4af`, which adds my round-104 verdict byte-exact (`0107b814…`), are ancestors of the
  base; the base merges `origin/master`.
- **F1.** A not-yet child result and an owning fault that could not commit now keep their claim
  until the lease expires instead of returning to the head of the lane. With `N` integrity-faulted
  runs filling batch `N` (1 and 3), a healthy run still dispatches within two cycles, retained
  records stay `Claimed`, and they return at attempt 2 after expiry. This passes on InMemory,
  PostgreSQL and SQL Server; I confirmed the real-storage cases ran by name. Reverting to release
  turns it red (Q1).
- **Bridge.** Run-input bindings are rebuilt per boot from typed plans, keyed by definition and
  version, and checked against fingerprint and the computed `dag:…:run-input:` shape key. A
  conflicting install is rejected before any change, and an uninstalled plan writes nothing.
  - Child values use the RC-3 §3 keys `workflow:{id}:{version}:input|output`. The child checkpoint
    schema identity is checked separately against the installed output type.
  - Reopen compares start key, graph, input fingerprint and bytes. Concurrent creation re-reads the
    winner.
  - Decode and normalization run outside every turn. Protocol and binding faults throw before
    materialization, so they are never recorded as user mapping failures.
- **Turns.** Admission, join, cancellation and reconciliation share one boot-local FIFO. Only the
  head reaches the provider. Each turn renews every second within the 5 s lease and 15 s cap,
  performs one append, and is ended or abandoned in `finally`. Cancellation and late admission are
  ordered by the fence: admission rechecks `Running`/`Ready`, version, receipt provenance and digest
  under the fence. Reconciliation rebuilds start, join and cancel signals from authoritative child
  facts, keeps old poison rows and their diagnostics, and does not release uncertain slots.
- **Scheduler.** There is one reusable worker per run per boot host, with active and retired gauges.
  - The wake channel is coalesced and bounded.
  - Only the lowest Ready node is evaluated while capacity is free (same predicate as admission).
  - No mapper runs under a lease or claim; a ready hint is acknowledged before mapping.
  - A newly Ready lower node invalidates a higher receipt.
  - Subscription-addressed hints go to the notification hub; unaddressed ones go to the scheduler.
  - Discovery wraps every 4 s, skips only unchanged versions with nothing to do, and rechecks lanes
    of runs that left the index.
  - A blocked mapper holds only its own lane: another run completes and the blocked run can be
    cancelled (hosting and real-storage tests).
- **Boundaries.** The exact compiled Durable.Hosting inventory (26 types, 43 members) is hash-pinned.
  Dag.Hosting references no provider port, and reflection, `UnsafeAccessor` and expression-tree
  bridges are detected. The Dag runtime view stays at 3 types and 15 members.

## Freeze anchors

| Anchor | Claimed | Reproduced |
|---|---|---|
| Raw porcelain / manifest | 55 lines, 3,795 B, `35a03869…` | identical; 36 modified plus 19 untracked are exactly the 55; nothing staged |
| Semantic record | 51 rows, 6,808 B, `2ccb2103…` | identical |
| Active record | fixture-bound | 54 rows, 7,519 B, `80923a6d…`, equal to the fixture's `activeFreeze` |
| Status all-file record | not claimed | 55 rows, 7,674 B, `c68b34c7…` |
| Simulated checkpoint | promised in a handoff, none found | tree `463b7e62b7d5bf8c74b80c5a85a02cc36cf93ab2` from a copied index and from a committed disposable copy; parent `048c3a6`; 19 A / 36 M; every blob equals the raw bytes |

The request and manifest catalog rows (6,691 B `731e8f6c…`, 3,795 B `35a03869…`) match the files.
All anchors were rechecked unchanged after validation.

## Validation

SDK 10.0.403, plain `-warnaserror`, disposable detached copy on `E:`.

| Lane | Result |
|---|---|
| Debug and Release non-incremental `-warnaserror` builds | 0 warnings, 0 errors |
| Exact package feed / package fixtures | 12 packages / green fixtures pass, exactly `dag-hosting` red |
| Compile fixtures (`Green`) | green |
| Core / Ephemeral / Durable / Acceptance / Hosting / ProviderCertification | 390 / 405 / 113 / 37 / 50 / 116 |
| PostgreSQL / SQL Server (real containers) / Integration | 118 / 89 / 11 |
| Full guard project, frozen state | 319 passed plus exactly 14 expected-red (333) |
| `Disposition=Infrastructure`, Release | 319/319 |
| `Disposition=ExpectedRed` | exactly 14, all scenario guards |
| Strict OpenSpec / `git diff --check` | 26/26 / clean |
| Guards on the committed simulation | 319 passed plus the same 14 expected-red |
| Evidence state: committed simulation with the freeze cleared | Infrastructure 319/319 |

## Independent probes

One-line mutations in a second disposable copy, each running the Hosting DAG tests, two
reviewer-only scheduler tests and the in-memory DAG certification.

| # | Probe | Result |
|---|---|---|
| Q0 | unmodified (including the reviewer tests) | green (28 / 14) |
| Q1 | not-yet child released immediately (F1 regression) | **red**: `DagRetainedChild_DoesNotStarveHealthyRun` (both batch sizes) |
| Q2 | uncommitted owning fault released | **red**: `ChildLane_DefersWithoutPoisonWhenOwningFaultCouldNotCommit` |
| Q3 | scheduler keeps a higher receipt after a lower node becomes Ready | red **only** in reviewer test R2; the target's tests stay green |
| Q4 | scheduler evaluates the highest Ready node | red **only** in reviewer tests R1 and R2; the target's tests stay green |
| Q5 | admission drops its lower-Ready check | **red**: `Admission_RejectsCopiedReceiptAndRetainsTheOriginalAcrossOrdinalDeferral` |
| Q6 | catalog accepts a conflicting binding | **red**: `Restart_ResolvesOnlyExactLocallyInstalledRunInput` |
| Q7 | child output schema identity not checked | **green**: no test covers it |
| Q8 | value-role clause removed at child start | green; the graph-key check still rejects the fixture and providers reject value keys that differ from the graph, so the clause is redundant |
| Q9 | reopen ignores different input bytes | **red**: `Create_ReopensExactGraphAndRejectsConflictingInputOrIdentity` |
| Q10 | reconciliation never re-emits Start | **red**: `IntegrityReconciliation_RebuildsOnlyOwningChildWorkAndReplaysOperation` |

The reviewer tests, run on the shipped in-memory host:
- **R1:** two independent roots with `MaxConcurrentNodes = 1` admit in authored order.
- **R2:** chain `0 → 1` plus independent node `2` with `MaxConcurrentNodes = 2`. Node 2's first
  mapping waits until node 1 is Ready, so its receipt is stale. The run must still finish with
  admission order `0, 1, 2`.

Both pass on the frozen code. Under Q3 and Q4 the run livelocks, because admission repeatedly
defers the out-of-order receipt; authored order itself is never violated.

## Findings

No blocking finding.

- **F1 (P3, test coverage): scheduler liveness has no regression test.** Lower-Ready receipt
  invalidation (Q3) and lowest-Ready selection (Q4) are implemented correctly, but breaking either
  livelocks a run while every target test stays green. Add R1/R2-style tests in the next target.
- **F2 (P3, test coverage): the separate child output schema check (design §15) is untested** (Q7).
  Add a mismatched `OutputSchemaIdentity` case.
- **F3 (P3, latency): deferral waits the full claim lease.** The certification uses a 30 s lease,
  but the default `OutboxClaimLeaseDuration` is 5 minutes.
  - A not-yet result now delays that record by up to the lease. For an integrity-faulted run that
    is intended. It also applies to a start or resultful join claimed by a host without the child
    definition, a local queue full at 1,024, a lost turn lease, and an owning fault that could not
    commit.
  - Fairness and eventual progress are preserved, and there is no attempt-count poisoning.
  - Consider a dedicated short back-off, such as a provider not-before on release, or document the
    bound before mixed-definition deployments are supported.
- **Recorded:**
  - An idle lane keeps its dedicated worker thread until the run is terminal, so thread count scales
    with active runs per boot host. Isolation would survive releasing an idle worker.
  - A record that keeps throwing an unexpected exception still aborts its batch. Because it is now
    retained, the same batch re-forms after each expiry, so co-claimed records wait as long as it
    keeps throwing. This is a delay, not loss.
  - Evaluation protocol faults are logged and retried on each discovery pass without faulting the
    run, matching RC-3's rule that storage and protocol errors throw their runtime classification.
  - The scheduler tests reach the internal pre-8.6 creation entry point through reflection; shipped
    Hosting has no reflection bridge.
  - ReSharper was not rerun.

## Hygiene and checkpoint sequence

The main checkout is at `048c3a6ecd3541f30c57d9f600190e63f6a16d84` with the 55 frozen entries and
nothing staged. When this verdict was written, it showed exactly those entries plus this new,
untracked verdict. My disposable worktrees are removed.

1. Confirm zero drift against the 55-entry manifest and the anchors above.
2. Stage exactly those 55 paths from the manifest (not `git add -A`), expecting tree `463b7e62…`
   on parent `048c3a6`, and commit.
3. In its only direct child, add and catalog this verdict and clear the active freeze, keeping the
   history pointer.

There is no activation. Fold F1 and F2 tests (and any F3 decision) into the next target.

## Determination

The bridge keeps all CLR typing boot-local and fail-closed and decodes outside every turn. The
scheduler isolates each run's mapper and admits in authored order under the fence; turns,
cancellation and reconciliation follow the contract. The F1 starvation is fixed and certified on real
storage. No test was weakened. The open items are test coverage and a latency bound, none of
which affects correctness of this checkpoint.

**Verdict:** **APPROVE**
