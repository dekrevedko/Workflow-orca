# T4B-02: Add acquisition-as-wait

> **Superseded historical task (2026-07-18):** do not execute this task or implement its
> signatures/deliverables. Current v1 authority is
> [document 17](../../../specs/17-selected-mode-capability-matrix.md) and the active
> `reshape-developer-facing-interfaces` change. Durable acquisition is lexical scoped-only
> `AcquireResources(request, body)` with quarantine and trusted stop/fence confirmation.

> **2026-07-16 follow-up contract:** structural no-author-TTL `AcquireResources` supersedes
> decorator-only acquisition. One exact owned lease record spans pending and held phases;
> selector commitment, inclusive fiber-ancestry safety, release-before-parent-resume, and
> granted-lease continue-as-new rejection are required by reshape task 5.2/DR-038.

**Difficulty**: Sonnet        **Depends on**: T4B-01
**Spec**: MG-062, MG-063        **AC**: AC-519, AC-520, AC-522, JS-AC-013

## Goal
Bind structural durable pool requests to exact workflow fiber occurrences so exhausted pools
suspend only the requesting fiber cold instead of starting guarded work. Granted tickets are
held across the wait and released on exact normal/failure/cancellation cleanup; forced
termination retains capacity in quarantine until protected work is confirmed stopped or
end-to-end fenced.

## Read first
- `src/OrcaCore.Abstractions/Providers/IResourcePoolStore.cs`
- `src/OrcaCore.Abstractions/Durable/WorkflowCommand.cs`
- `src/OrcaCore.Abstractions/Durable/WorkflowEvent.cs`
- `src/OrcaCore.Engine.Durable/Aggregates/DurableWorkflowAggregate.cs`
- `src/OrcaCore.Engine.Durable/Execution/DurableCommandProcessor.cs`
- Spec: `docs/specs/09-requirements-management-operations.md` section 9.7
- Spec: `docs/specs/14-driving-scenario-eks-job-scheduler.md` section 14.3

## Deliverables
- Durable command/event contracts for pool acquisition, grant, and release
- Aggregate state for held tickets and queued acquisition waits
- Command processor integration with `IResourcePoolStore`
- `tests/OrcaCore.Engine.Durable.Tests/ResourcePools/PoolAcquisitionTests.cs`
- Acceptance coverage in `tests/OrcaCore.Acceptance.Tests/ResourcePoolAcceptanceTests.cs`

## Tests to write FIRST
In `PoolAcquisitionTests.cs`:
1. `AcquirePool_WhenCapacityAvailable_RecordsHeldTicketsBeforeGuardedWorkStarts` - grant
   state is durable before continuation.
2. `AcquirePool_WhenExhausted_RecordsColdWaitWithoutOutboxWork` - queued acquisition leaves
   the instance waiting and cold-capable. Traits AC-519 and JS-AC-013.
3. `CompleteOwningFiber_ReleasesTicketsExactlyOnce` - normal success releases the grant.
4. `TerminateOwner_WithoutStopProof_QuarantinesReservedTickets` - forced termination fences
   owner resume but does not release capacity until protected work is proven stopped or
   end-to-end fenced. Trait AC-520.

## Implementation notes
Use `AcquireResources` as an explicit durable workflow graph node. Persist one normalized
request and exact owned occurrence across pending and held phases. No author duration or
renewal exists. Runtime checks pending plus held inclusive ancestry before pool mutation.
Do not treat an expiry timestamp as validity or automatic release.

## Out of scope
Pool management operations, expiry reconciliation, external job dispatch, and DAG builder
syntax.

## Definition of done
- [ ] New durable and acceptance tests are red before implementation and green after
- [ ] `dotnet test OrcaCore.slnx --filter "AC=AC-519|AC=AC-520|AC=AC-522|AC=JS-AC-013"` passes
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] Core and Providers.InMemory still reference only Abstractions
- [ ] PROGRESS.md updated; committed as "T4B-02: acquisition as wait (MG-062)"
