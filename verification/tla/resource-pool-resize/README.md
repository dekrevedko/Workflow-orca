# FIFO promotion on a durable resource pool increase

An idle pool X has capacity 1. The first committed waiter needs 2 units of X. The second needs 1 unit of idle pool Y. The single partition-wide FIFO keeps both queued. Increasing X to 2 lets the resize commit both grants in order. With the old capacity-only write, both waiters can stay queued forever. Neither pool has a holder to release a ticket. See [AC-518 and AC-519](../../../docs/specs/12-acceptance-criteria.md) and the [serialized governance aggregate](../../../src/OrcaCore.Durable.Hosting/ResourceLeases/SerializedResourceGovernanceAggregate.cs).

## What the model checks

[ResourcePoolResize.tla](ResourcePoolResize.tla) starts with two requests already queued in committed order. In a second layout, an existing holder occupies both units of X and both waiters request X. Increasing X to 3 leaves the two-unit head blocked despite room for the one-unit follower. Increasing X to 4 pays for the head alone, and increasing X to 5 pays for both.

Resize reads a proposal, then tries to publish capacity and all grants at one governance version. An exact holder release or cancellation cleanup can commit between the read and append. The first waiter has a scoped workflow owner with a separate checkpoint. Closing the owner invalidates a proposal that would promote it. A fresh scan skips the closed waiter, which can remain queued until cleanup.

| Profiles | Checked outcome |
|---|---|
| `SafetyAndProgress` | 427 states generated, 300 distinct, none left on the queue. TLC checked grant uniqueness, FIFO, closure, reservation bounds, and resize progress. |
| `OldNoDrain`, `OldNeedsRelease` | In the idle two-pool case, the old resize returns with both waiters eligible in sequence and no tickets. The first control fails the drain invariant. The second produces indefinite stuttering after that commit. |
| `DisjointFifo`, `BlockedFirst` | The corrected resize commits both disjoint grants in order. A separate trace retains both loaded-pool waiters when the live head still cannot fit. |
| `ReleaseWinsAppend` | The holder releases and promotes the head between the resize read and append. Resize retries and promotes the follower once. |
| `OwnerClosesBeforeAppend` | A checkpoint change rejects the stale promotion. Resize retries and grants the follower while the closed head awaits cleanup. |
| `BypassHead`, `MissingOwnerFence`, `MissingVersion` | Each mutation violates its named safety property by bypassing a live head, granting a closed owner, or restoring a released reservation. |

Progress assumes that a continuously enabled resize step and cleanup of an already-closed owner eventually run. Release of the unrelated holder and closure of a live owner are optional. The immediate drain property covers settled owner state. The temporal property also covers an eligible head exposed by an outstanding cancellation.

Tickets stand for exact reservations by one occurrence in one pool. The model bounds the schedule to one resize, one holder release and one scoped closure. Time, GUID generation, operation-ID replay, database transactions, retry exhaustion and workflow wakeup delivery are outside this check.

## Provider checks

The [idle-pool regression](../../../tests/OrcaCore.ProviderCertification/DurableResourceGrantRaceCertification.Resize.cs) failed on PostgreSQL and SQL Server at the Phase E baseline `9465475d`. The capacity increase succeeded with zero units reserved. Both tests now check the tickets issued by the resize, their identities on a replacement host, and adoption by the parked workflow on its next driver turn. The [competing writes](../../../tests/OrcaCore.ProviderCertification/DurableResourceGrantRaceCertification.ResizeRaces.cs) cover a partially fitting head, cancellation between checkpoint read and append, and a release that wins the partition version. Neither granting tickets nor recovery of those tickets requires another holder to release.

New resize records retain the winning management snapshot for replay after intervening changes. An older operation retains its identity and uses current accounting when its v1 record lacks that snapshot. Replaying an old operation issues no new tickets. Drain older writers before enabling the new version on a shared partition because an older writer can omit the snapshot when it writes its next full state. Workflow adoption requires a subsequent driver turn.

## Reproduce

Run from the repository root with PowerShell 7. The runner uses the pinned private Java runtime and TLC. It records configurations, source hashes, classifications, state counts and counterexample traces under `.codex-run/ResourcePoolResize/`.

```powershell
./scripts/verification/Invoke-TlaModelCheck.ps1 -ModelDirectory verification/tla/resource-pool-resize -ModuleName ResourcePoolResize
```
