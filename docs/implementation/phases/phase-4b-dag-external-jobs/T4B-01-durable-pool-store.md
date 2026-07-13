# T4B-01: Add durable pool store port

**Difficulty**: Sonnet        **Depends on**: T4B-00
**Spec**: MG-062, MG-063, MG-064, JS-007        **AC**: AC-518, JS-AC-007

## Goal
Introduce the durable resource-pool persistence port and implement it for InMemory and
PostgreSQL providers. The store owns pool definitions, held tickets, FIFO waiters, and
atomic all-or-nothing grants.

## Read first
- `src/OrcaCore.Abstractions/Providers/ProviderPorts.cs`
- `src/OrcaCore.Providers.InMemory/InMemoryWorkflowProvider.cs`
- `src/OrcaCore.Providers.PostgreSql/PostgreSqlWorkflowStore.cs`
- `tests/OrcaCore.ProviderCertification/EventStoreCertificationTests.cs`
- `tests/OrcaCore.Providers.PostgreSql.Tests/PostgreSqlProviderCertificationTests.cs`
- Spec: `docs/specs/09-requirements-management-operations.md` section 9.7
- Spec: `docs/specs/14-driving-scenario-eks-job-scheduler.md` sections 14.3 and 14.4

## Deliverables
- `src/OrcaCore.Abstractions/Providers/IResourcePoolStore.cs`
- `src/OrcaCore.Abstractions/Providers/ResourcePoolModels.cs`
- `src/OrcaCore.Providers.InMemory/InMemoryResourcePoolStore.cs`
- `src/OrcaCore.Providers.PostgreSql/PostgreSqlResourcePoolStore.cs`
- `tests/OrcaCore.ProviderCertification/ResourcePoolStoreCertificationTests.cs`
- Provider wiring updates as needed in `src/OrcaCore.Providers.InMemory/` and
  `src/OrcaCore.Providers.PostgreSql/`

## Tests to write FIRST
In `tests/OrcaCore.ProviderCertification/ResourcePoolStoreCertificationTests.cs`:
1. `AcquireAsync_WhenPoolHasCapacity_GrantsTicketAndReducesAvailableCapacity` - given a
   pool with capacity, acquiring one requirement creates one held ticket.
2. `AcquireAsync_WhenPoolIsExhausted_QueuesWaiterWithoutGrantingTicket` - given no
   capacity, acquisition records a FIFO waiter and no ticket.
3. `AcquireAsync_WhenMultiplePoolsRequested_GrantsAllOrNone` - given one exhausted pool,
   no partial ticket is held for any pool. Trait AC-522.
4. `ReleaseAsync_WhenTicketReleased_GrantsNextWaiterInFifoOrder` - given queued waiters,
   releasing capacity grants the oldest compatible request. Trait AC-518.

## Implementation notes
Keep the port in Abstractions and keep both providers independent of Core. Model grant
results explicitly: granted tickets, queued request, rejected/missing pool. PostgreSQL may
create provider-owned tables in code; do not add EF or Dapper.

## Out of scope
Workflow acquisition semantics, lifecycle events, management APIs, and external job
dispatch.

## Definition of done
- [ ] New certification tests are red before implementation and green after
- [ ] `dotnet test OrcaCore.slnx --filter ResourcePool` passes
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] Providers.InMemory references only Abstractions
- [ ] PROGRESS.md updated; committed as "T4B-01: durable pool store port (MG-062)"
