# T4B-03: Add ticket expiry and pool operations

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
- Durable management methods for pool inspect, resize, expiry scan, and force release
- Provider certification additions for expiry and resize semantics
- `tests/OrcaCore.Engine.Durable.Tests/ResourcePools/PoolOperationsTests.cs`

## Tests to write FIRST
In `PoolOperationsTests.cs`:
1. `ExpireTicketsAsync_WhenTicketExpired_RecordsAudibleExpiryState` - expired tickets are
   queryable and not silently reclaimed. Trait AC-521.
2. `ResizePool_WhenShrinkingBelowHeldCount_DoesNotRevokeHeldTickets` - shrinking changes
   future capacity only.
3. `ForceReleaseTicket_WhenOperatorReleases_RecordsAuditAndGrantsNextWaiter` - force
   release is observable and wakes the FIFO queue.

## Implementation notes
Expiry policy may start with explicit scan/command semantics; do not add a background
scheduler unless existing timer infrastructure makes it trivial. Keep audit facts durable
and visible through snapshots.

## Out of scope
Rate-based pools, priority queues, external arbiters, and Kubernetes adapters.

## Definition of done
- [ ] New tests are red before implementation and green after
- [ ] `dotnet test OrcaCore.slnx --filter "AC=AC-521|ResourcePool"` passes
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] PROGRESS.md updated; committed as "T4B-03: ticket expiry and pool operations (MG-064)"
