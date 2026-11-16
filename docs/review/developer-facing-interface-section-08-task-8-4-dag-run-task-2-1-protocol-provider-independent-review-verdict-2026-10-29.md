# DAG run Task 2.1 protocol/provider data plane independent review verdict

**Date:** 2026-10-29
**Reviewer:** independent review
**Scope reviewed:** the 95-entry freeze in the review copy `E:/d21` on base
`0a8e375fbcf5005a2a2f491cf24dfbbd9d64c580` (tree `6e26e807…`), named by
`developer-facing-interface-section-08-task-8-4-dag-run-task-2-1-protocol-provider-dirty-manifest-2026-10-29.txt`.
It contains:
- the closed DAG protocol records, state reducer and `DagRunRecordCodecV1`;
- the eleven provider ports with InMemory, PostgreSQL and SQL Server implementations, shared
  provider-private rules and SQL store, and first-create DDL for six DAG tables;
- the `Released = 6` erratum, an API candidate, signatures and an implementation record;
- the fail-closed external-pump rider, seven DAG gauges and `DagRunCertification`;
- the round-100 carry-forwards and guard, registry, ledger and accounting refreshes.

**Authorization requested:** checkpoint of this exact source target and its direct-child evidence.
There is no activation.

This verdict authorizes that checkpoint and the evidence child. It authorizes no task checkbox, no
pump, no Hosting bridge, no mapper scheduling and no publication.

## Summary

The data plane is correct, carefully bounded and well guarded. Three contract rules are implemented
correctly but have no certification, which is the main thing to fix next.

- **Concurrency.** Every change runs in one real database transaction under a per-aggregate lock
  (PostgreSQL advisory lock; SQL Server's existing workflow-document lock under `Serializable`),
  and reads database time only after the lock. Workflow appends take the same lock and reject a
  DAG-owned GUID, so the two creation directions cannot race.
- **Leases.** FIFO grants, one live request per owner per run (also enforced by a partial unique
  index), a 5-second renewable lease capped at a 15-second turn, stale-fence rejection, one
  mutation per turn, exact replay before fence and version checks, and an idempotent `Released`
  that never revokes a newer grant. Time is clamped so it never runs backwards per run.
- **Protocol.** The codec accepts exactly the expected fields, re-serializes every record and
  rejects anything not byte-identical, and validates coordinates and committed-byte hashes.
  Appends are re-reduced and the supplied checkpoint must equal the reduction.
- **Storage.** The DDL uses binary collation, hex-case checks, closed state enums, foreign keys and
  unique child identity, as complete first-create definitions.
- **Rider.** The external pump excludes all four DAG kinds by default and releases them unacknowledged
  even if a caller or provider selects them.
- **Carry-forwards.** All three round-100 notes are fixed: task 1.4 is locked complete, early
  rejection is tested through inbox, outbox, global event count and a cancelled token, and the
  stale transition sentence is corrected.
- **Guards.** Narrowing two native guards to the Abstractions/Core/Ephemeral boundary matches the
  qualified ephemeral closure, which does not include Runtime.Protocol. The registry projection
  used in two older guards hides nothing the DAG guard does not pin exactly.
- **Evidence.** Anchors and tree reproduce; every lane is green; no test assertion was weakened.

## Freeze anchors

| Anchor | Claimed | Reproduced |
|---|---|---|
| Raw porcelain / manifest | 95 lines, 7,059 B, `1934b160…` | identical; diff (55) plus untracked (40) paths are exactly the 95; nothing staged |
| NUL porcelain | 95, `22422543…` | identical |
| Semantic record | 91 rows, 12,747 B, `40042e49…` | identical |
| Active record | 94 rows, 13,581 B, `02167ed5…` | identical, equal to the fixture |
| Status-less all-file record | 95 rows, 13,451 B, `da04d7ab…` | identical |
| Simulated checkpoint | tree `0e5300632b9804f096ac45d119a602b5b9f5b9c7` | identical from a copied index and from a committed disposable copy; parent `0a8e375`; 40 A / 55 M; every blob equals the raw bytes |

## Validation

SDK 10.0.403, plain `-warnaserror`, disposable copy on `E:`.

| Lane | Result |
|---|---|
| Debug and Release non-incremental `-warnaserror` builds | 0 warnings, 0 errors |
| Exact package feed / package fixtures | 12 packages / green fixtures pass, exactly `dag-hosting` red |
| Compile fixtures (`Green`) | green |
| Core / Ephemeral / Durable / Acceptance / Hosting / ProviderCertification | 372 / 352 / 106 / 37 / 24 / 108 |
| PostgreSQL / SQL Server (real containers) / Integration | 111 / 82 / 11; all eight DAG certification steps run on each SQL provider |
| Full guard project, frozen state | 311 passed plus exactly 14 expected-red (325) |
| `Disposition=Infrastructure`, Release | 311/311 |
| `Disposition=ExpectedRed` | exactly 14, all `ExecutableBehaviorExpectedRedGuards` scenarios |
| Strict OpenSpec / `git diff --check` | 25/25 / clean |
| Guards on the committed simulation | 311 passed plus the same 14 expected-red |
| Evidence state: committed simulation with the freeze cleared | Infrastructure 311/311 |

No modified test lost an assertion: the two replaced assertions were strengthened (the pump's
default exclusion list and the early-rejection certification).

## Independent probes

Six source mutations against the shared rules, run on the in-memory DAG certification facts (8 tests,
the same rules source every provider links), plus baseline and restore:

| # | Mutation | Result |
|---|---|---|
| Q0 / R0 | none / restored and rebuilt | green / green; worktree equals the manifest |
| Q1 | dead waiter never expires (30 s -> 1 day) | **green**: see P3-1 |
| Q2 | turn purpose no longer restricts record kinds | **green**: see P3-1 |
| Q3 | releasing an old grant also revokes the newer one | **red**: FIFO/deadline fact |
| Q4 | terminal closure may be deferred to a later append | **green**: see P3-1 |
| Q5 | InMemory workflow append no longer rejects a DAG-owned GUID | **red**: identity-collision fact |
| Q6 | renewal no longer capped by the 15 s turn deadline | **red**: FIFO/deadline fact |

## Non-blocking observations (P3)

- **P3-1: three contract rules have no certification.** The code implements each correctly, but
  removing it leaves every certification test green:
  - the 30-second expiry of a dead head-of-queue waiter (the contract's "at most 30 seconds per
    dead waiter" bound); without it a dead waiter blocks the run's queue indefinitely;
  - per-purpose record restrictions (an Admission turn may append only admission/mapping records,
    a ChildJoin turn only joins, and so on);
  - committing terminal closure in the same append that leaves every node terminal.

  Add a negative certification step for each, run against all three providers, in the next target.
- **P3-2: one more stale status sentence.** `CLAUDE.md` line 315 still says "this first
  uncommitted source slice implements typed stream identity only, not run persistence…", but that
  slice is checkpointed (`c93411e`) and this target implements run persistence. Refresh it with the
  next document edit.
- **Recorded:**
  - On SQL Server every DAG operation and every workflow append serialize through the existing
    schema-wide workflow-document lock. The erratum discloses this as an inherited throughput
    limit, not a new design.
  - The exact `src/` path set pinned in the DAG guard is a standing tripwire: any later source
    change, DAG or not, must update that list in its own reviewed target.
  - The four DAG kind strings are repeated as literals in the pump and hosted service rather than
    shared constants.
  - ReSharper was not rerun; the author's scoped run reports zero errors.

## Hygiene and checkpoint sequence

`E:/d21` is at `0a8e375fbcf5005a2a2f491cf24dfbbd9d64c580` with the 95 frozen entries and nothing
staged. When this verdict was written, the copy showed exactly those entries plus this new, untracked
verdict. My disposable worktrees are removed.

1. Confirm zero drift against the 95-entry manifest and the anchors above.
2. Stage exactly those 95 paths on `0a8e375`, expecting tree `0e530063…`, and commit; do not use
   `git add -A` in the main checkout, which holds unrelated SQLite planning.
3. In its only direct child, add and catalog this verdict and clear the active freeze, keeping the
   history pointer.

There is no activation. Tasks 2.1–3.2 and reshape 8.4/8.5 stay open; the next target is the Task 2.2
pumps and should carry P3-1 and P3-2.

## Determination

The protocol and all three provider data planes implement the approved contract with real
transactions, exact fencing and fail-closed framing, and the carried findings are fixed without
weakening any test. The remaining gaps are missing negative certifications for three correctly
implemented rules, which are non-blocking.

**Verdict:** **APPROVE**
