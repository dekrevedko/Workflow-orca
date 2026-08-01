# T4B-03: Add ticket expiry and pool operations

> **Superseded historical task (2026-07-18):** do not execute this task or implement its
> signatures/deliverables. Current v1 authority is
> [document 17](../../../specs/17-selected-mode-capability-matrix.md) and the active
> `reshape-developer-facing-interfaces` change. Review time marks ownership but never releases
> capacity; v1 has no force-release operation or time-only reclaim.

> **2026-07-16 follow-up contract:** expiry is an audible owner-review mark, not reclaim or a
> holder TTL. The final implementation needs exact owner-state reconciliation and fenced
> recovery; live/ambiguous ownership stays capacity-held without baseline renewal.

**Difficulty**: Haiku        **Depends on**: T4B-02
**Spec**: MG-064        **AC**: AC-521

## Goal
Make expired durable-pool tickets observable and operable. Operators can inspect pool state,
resize capacity, and force-release tickets through an audited management surface.

## Read first
- `src/OrcaCore.Abstractions/Providers/IResourcePoolStore.cs`
- `src/OrcaCore.Engine.Durable/Management/DurableManagement.cs`
- `src/OrcaCore.Engine.Durable/Management/DurableManagementQuery.cs`
- `tests/OrcaCore.Engine.Durable.Tests/Management/DurableManagementTests.cs`
- `tests/OrcaCore.ProviderCertification/ResourcePoolStoreCertificationTests.cs`
- Spec: `docs/specs/09-requirements-management-operations.md` section 9.7
- Spec: `docs/specs/12-acceptance-criteria.md` AC-521

## Deliverables
- Pool inspection snapshots in Abstractions
- Durable management methods for pool inspect, resize, expiry-mark/reconciliation, and audited
  exact-obligation force release
- Provider certification additions for mark-retains-capacity, live/ambiguous/recoverable
  owner outcomes, `LeaseLost`, compare-and-act fencing, races, and resize semantics
- `tests/OrcaCore.Engine.Durable.Tests/ResourcePools/PoolOperationsTests.cs`

## Tests to write FIRST
In `PoolOperationsTests.cs`:
1. `ExpireTicketsAsync_WhenTicketExpired_RecordsAudibleExpiryState` - expired tickets are
   queryable, remain capacity-held, and are not silently reclaimed. Trait AC-521.
2. `ResizePool_WhenShrinkingBelowReservedUnits_RecordsDebtAndBlocksNewGrants` - shrinking
   revokes nothing, exposes `max(0, reserved - configured)`, and grants nothing while debt
   exists.
3. `ResizePool_WhenGrowingCapacity_RecomputesDebtBeforeAdmission` - upward resize may clear
   debt; the next whole request is eligible only when it fits.
4. `ForceReleaseTicket_WithoutStopProof_RemainsQuarantined` - force release is audited but
   cannot wake the FIFO queue until owner resume is fenced and protected work is confirmed
   stopped or end-to-end fenced.
5. `ReconcileTicket_WhenOwnerIsLive_KeepsCapacityWithoutRenewal` - active/reconstructable
   owner remains held after review.
6. `ReconcileTicket_WhenOwnerReleaseIsProven_ReleasesExactFenceOnce` - recovery uses exact
   obligation/ticket/provider generation; ambiguous state remains held.
7. `MixedPoolRequest_UsesEachPoolsReviewPolicy` - one atomic request produces per-ticket
   pool-owned review deadlines and no request-wide author `ExpiresAt`.

## Implementation notes
Expiry policy may start with explicit scan/command semantics; the hosted reconciler can be a
later slice. Keep marks, reconciliation results, and operator actions durable and visible.
Elapsed time alone never advances a waiter.

## Out of scope
Rate-based pools, priority queues, external arbiters, and Kubernetes adapters.

## Definition of done
- [ ] New tests are red before implementation and green after
- [ ] `dotnet test OrcaCore.slnx --filter "AC=AC-521|ResourcePool"` passes
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] PROGRESS.md updated; committed as "T4B-03: ticket expiry and pool operations (MG-064)"
