# T4B-02: Add acquisition-as-wait

**Difficulty**: Sonnet        **Depends on**: T4B-01
**Spec**: MG-062, MG-063        **AC**: AC-519, AC-520, AC-522, JS-AC-013

## Goal
Bind durable pool requirements to workflow execution so exhausted pools suspend instances
cold instead of starting guarded work. Granted tickets are held across the wait and released
symmetrically on every guarded terminal path.

## Read first
- `v3-gpt/src/OrcaCore.Abstractions/Providers/IResourcePoolStore.cs`
- `v3-gpt/src/OrcaCore.Abstractions/Durable/WorkflowCommand.cs`
- `v3-gpt/src/OrcaCore.Abstractions/Durable/WorkflowEvent.cs`
- `v3-gpt/src/OrcaCore.Engine.Durable/Aggregates/DurableWorkflowAggregate.cs`
- `v3-gpt/src/OrcaCore.Engine.Durable/Execution/DurableCommandProcessor.cs`
- Spec: `docs/specs/09-requirements-management-operations.md` section 9.7
- Spec: `docs/specs/14-driving-scenario-eks-job-scheduler.md` section 14.3

## Deliverables
- Durable command/event contracts for pool acquisition, grant, and release
- Aggregate state for held tickets and queued acquisition waits
- Command processor integration with `IResourcePoolStore`
- `v3-gpt/tests/OrcaCore.Engine.Durable.Tests/ResourcePools/PoolAcquisitionTests.cs`
- Acceptance coverage in `v3-gpt/tests/OrcaCore.Acceptance.Tests/ResourcePoolAcceptanceTests.cs`

## Tests to write FIRST
In `PoolAcquisitionTests.cs`:
1. `AcquirePool_WhenCapacityAvailable_RecordsHeldTicketsBeforeGuardedWorkStarts` - grant
   state is durable before continuation.
2. `AcquirePool_WhenExhausted_RecordsColdWaitWithoutOutboxWork` - queued acquisition leaves
   the instance waiting and cold-capable. Traits AC-519 and JS-AC-013.
3. `CompleteGuardedScope_ReleasesTicketsExactlyOnce` - normal success releases the grant.
4. `TerminateGuardedScope_ReleasesTicketsExactlyOnce` - forced termination releases held
   tickets. Trait AC-520.

## Implementation notes
Use acquisition as a durable wait concept, not an explicit workflow graph node. Persist
enough state to make release idempotent. Do not implement expiry handling here except for
carrying expiry timestamps from the pool store.

## Out of scope
Pool management operations, expiry reconciliation, external job dispatch, and DAG builder
syntax.

## Definition of done
- [ ] New durable and acceptance tests are red before implementation and green after
- [ ] `dotnet test v3-gpt/OrcaCore.slnx --filter "AC=AC-519|AC=AC-520|AC=AC-522|AC=JS-AC-013"` passes
- [ ] `dotnet build v3-gpt/OrcaCore.slnx` - zero warnings
- [ ] Core and Providers.InMemory still reference only Abstractions
- [ ] PROGRESS.md updated; committed as "T4B-02: acquisition as wait (MG-062)"
