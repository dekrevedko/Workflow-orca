# DAG run Task 2.2 pumps and carried certification independent review verdict

**Date:** 2026-11-01
**Reviewer:** independent review
**Scope reviewed:** the 42-entry freeze in `E:/d21` (branch `codex/dag-run-task-2-2`) on base
`602844547539e0e5908d250c2a0d9560fc4b25dc`, named by
`developer-facing-interface-section-08-task-8-4-dag-run-task-2-2-pumps-and-certification-dirty-manifest-2026-11-01.txt`.
It contains the dedicated child pump, coordinator and child engine, the capable-sink-only ready
pump, the hosted pump service, the three carried provider certifications, the `CLAUDE.md`
correction, Hosting tests, guards and metadata.

**Authorization requested:** checkpoint of this exact target and its direct-child evidence.
There is no activation.

This verdict authorizes that checkpoint and the evidence child. It authorizes no task checkbox, no
Task 2.3 bridge or scheduler, and no public DAG execution. Finding F1 below must be fixed before
any target that makes DAG runs creatable is approved.

## Summary

The pump slice is carefully built and the carried items are all fixed. One liveness defect is
reproduced; it cannot affect anything until Task 2.3 makes runs creatable, so it is carried as a
blocker for that target rather than for this checkpoint.

- **Round-103 chain.** Checkpoint `f62b0aa` has parent `0a8e375` and tree `0e530063`, the tree I
  approved; its only direct child `6028445` adds my verdict byte-exact (`c971876e…`) and clears the
  freeze.
- **Child lane.** Claims only the three child kinds, releases any other kind before parsing,
  acknowledges only after the handler commits, and records an owning integrity fault before
  poisoning an unreadable signal (releasing instead if that fault cannot commit).
- **Joins.** Re-read authoritative state inside a ChildJoin turn; duplicate and lost-response
  deliveries are verified against the persisted outcome, so admission cannot be freed twice.
  Success readies only fully satisfied dependants, failure blocks reachable Pending/Ready
  descendants, and terminal state is derived in the same append.
- **Starts.** Check the deterministic identity recipe, reattach to an existing bound start, require
  the installed definition's fingerprint and contract keys, and start with no caller key, so no user
  step runs under a claim or turn.
- **Ready lane.** Makes no claim without a capable sink, releases foreign-boot and wrong-kind records
  before parsing, checks hints against the authoritative checkpoint, and acknowledges only after
  the sink accepts the wake.
- **Carried items.** All three round-103 certification gaps now have targeted tests: my original
  mutations (dead-waiter expiry, purpose restriction, same-append terminal closure) each fail their
  new case. `CLAUDE.md` names the Task 2.1 checkpoint.

## Freeze anchors

| Anchor | Claimed | Reproduced |
|---|---|---|
| Raw porcelain / manifest | 42 lines, 3,057 B, `d17c81ba…` | identical; diff (26) plus untracked (16) are exactly the 42; nothing staged |
| NUL porcelain | `2f14bcaf…` | identical |
| Semantic record | 38 rows, 5,177 B, `ffd72155…` | identical |
| Active record | 41 rows, 5,864 B, `c83ad635…` | identical, equal to the fixture |
| Status-less all-file record | 42 rows, 5,893 B, `3c4137c9…` | identical |
| Simulated checkpoint | tree `42770df9ad12a5cbeaeefb53ae13ef8e3794da7e` | identical from a copied index and a committed disposable copy; parent `6028445`; 16 A / 26 M; every blob equals the raw bytes |

## Validation

SDK 10.0.403, plain `-warnaserror`, disposable copy on `E:`.

| Lane | Result |
|---|---|
| Debug and Release non-incremental `-warnaserror` builds | 0 warnings, 0 errors |
| Exact package feed / package fixtures | 12 packages / green fixtures pass, exactly `dag-hosting` red |
| Compile fixtures (`Green`) | green |
| Core / Ephemeral / Durable / Acceptance / Hosting / ProviderCertification | 372 / 352 / 112 / 37 / 41 / 111 |
| PostgreSQL / SQL Server (real containers) / Integration | 114 / 85 / 11 |
| Full guard project, frozen state | 313 passed plus exactly 14 expected-red (327) |
| `Disposition=Infrastructure`, Release | 313/313 |
| `Disposition=ExpectedRed` | exactly 14, all scenario guards |
| Strict OpenSpec / `git diff --check` | 25/25 / clean |
| Guards on the committed simulation | 313 passed plus the same 14 expected-red |
| Evidence state: committed simulation with the freeze cleared | Infrastructure 313/313 |

## Independent probes

| # | Probe | Result |
|---|---|---|
| Q0 / R0 | in-memory DAG certification, unmodified / restored | green (11/11) / green |
| Q1 | dead waiter never expires | **red**: `DagDeadWaiter_ExpiresWithoutBlockingTheLiveQueue` |
| Q2 | turn purpose no longer restricts record kinds | **red**: `DagTurnPurpose_RejectsForeignRecordsBeforeStorage` |
| Q4 | terminal closure deferrable to a later append | **red**: `DagLastNode_RequiresAtomicRunClosure` |
| S1 | child-lane starvation test (reviewer-only, disposable copy): N integrity-faulted runs created first, then one healthy run, 10 pump cycles | **fails** at batch = N (N = 1 and 3): the healthy start stays `Pending`; passes at batch = N + 1 |

## Findings

- **F1 (P2, blocker for Task 2.3): retained child signals starve the whole child lane.**
  - When the handler returns "not yet" (owning run is `IntegrityFault`, child definition not
    installed on this host, start conflict), the pump calls `ReleaseAsync`, which sets the record
    `Retryable` with no claim deadline. Claims are ordered oldest-first (`outbox_record_id`, a
    version-7 GUID; insertion order in InMemory).
  - So once the retained records reach the batch size, every cycle reclaims and releases the same
    records and no newer child signal for any run is ever claimed. Integrity-faulted runs retain
    their signals indefinitely in this slice, because reconciliation is not implemented yet.
    S1 reproduces it at batch 1 and 3; the default batch is 100. The ready lane would behave the
    same with a persistently failing sink.
  - This contradicts the contract's isolation principle that a faulted or stuck run affects only
    itself. It cannot occur yet, because no DAG run can be created until the Task 2.3 bridge.
  - Fix it in the Task 2.3 target, before or with whatever first creates runs. For example, defer a
    "not yet" record instead of releasing it to the head (keep it claimed until its lease expires,
    or set a back-off time), move a faulted run's signals out of the claimable set until
    reconciliation, or rotate claims so retained records cannot monopolize a batch. Add a regression
    like S1 on all three providers.
- **Recorded:**
  - An unexpected exception in the child or ready pump releases the current record but rethrows,
    so the rest of that claimed batch waits out its claim lease before retrying. That is a delay,
    not loss.
  - The refreshed `CLAUDE.md` line describes "the prepared Task 2.2 target", which becomes stale at
    checkpoint, as earlier status lines did.
  - The ready lane is exercised only through a test-owned sink, as the request discloses; no credit
    is given for the Task 2.3 scheduler.
  - ReSharper was not rerun; the author's scoped run reports zero errors.

## Hygiene and checkpoint sequence

`E:/d21` is at `602844547539e0e5908d250c2a0d9560fc4b25dc` with the 42 frozen entries and nothing
staged. When this verdict was written, the copy showed exactly those entries plus this new,
untracked verdict. My disposable worktrees are removed.

1. Confirm zero drift against the 42-entry manifest and the anchors above.
2. Stage exactly those 42 paths on `6028445`, expecting tree `42770df9…`, and commit; do not use
   `git add -A` in the main X: checkout.
3. In its only direct child, add and catalog this verdict and clear the active freeze, keeping the
   history pointer.

There is no activation. The next target is Task 2.3 and must resolve F1 with a regression test.

## Determination

The child and ready lanes order their acknowledgements correctly, recover deterministically,
fault before poisoning, and keep admission until an authoritative join; the carried certifications
and status fix are complete, and no test was weakened. The one defect, head-of-line starvation by
retained signals, is reproduced and must be fixed before DAG runs become creatable, but it does not
affect this checkpoint.

**Verdict:** **APPROVE**
