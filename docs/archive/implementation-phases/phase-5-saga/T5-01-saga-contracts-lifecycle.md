# T5-01: Add saga contracts and lifecycle states

**Difficulty**: Haiku        **Depends on**: T5-00
**Spec**: SG-001, SG-013, DU-012        **AC**: AC-401, AC-404

## Goal
Introduce the minimal public contracts that let OrcaCore represent saga lifecycle and
durable saga facts. `Compensated` and `CompensationFailed` must be distinct terminal
statuses, and saga command/event types must exist without changing regular workflow
builder semantics.

## Read first
- `src/OrcaCore.Abstractions/Instances/WorkflowStatus.cs`
- `src/OrcaCore.Core/Lifecycle/LifecycleMachine.cs`
- `src/OrcaCore.Core/Lifecycle/LifecycleTrigger.cs`
- `src/OrcaCore.Abstractions/Durable/WorkflowCommand.cs`
- `src/OrcaCore.Abstractions/Durable/WorkflowEvent.cs`
- Spec: `docs/specs/07-requirements-saga.md` sections 7.1 and 7.2
- Spec: `docs/specs/12-acceptance-criteria.md` AC-401 and AC-404

## Deliverables
- Update `src/OrcaCore.Abstractions/Instances/WorkflowStatus.cs`
- Update `src/OrcaCore.Core/Lifecycle/LifecycleMachine.cs`
- Update `src/OrcaCore.Core/Lifecycle/LifecycleTrigger.cs`
- Add or update saga command/event contracts under `src/OrcaCore.Abstractions/Durable/`
- Tests in `tests/OrcaCore.Core.Tests/Lifecycle/SagaLifecycleTests.cs`
- Tests in `tests/OrcaCore.Core.Tests/Durable/SagaContractTests.cs`

## Tests to write FIRST
In `tests/OrcaCore.Core.Tests/Lifecycle/SagaLifecycleTests.cs`:
1. `SagaLifecycle_CompensatedAndCompensationFailed_AreTerminal` - the lifecycle machine treats both statuses as terminal. Traits: AC-401, AC-404.

In `tests/OrcaCore.Core.Tests/Durable/SagaContractTests.cs`:
1. `SagaCommandAndEventCatalog_ExposesCompensationFacts` - public durable contracts represent compensation requested, started, completed, and failed facts.

## Implementation notes
Keep the contract surface provider-neutral and serialization-friendly. Do not add a
process-manager-only saga track; spec open question 12 is resolved to compensation-heavy
sagas first. Avoid adding behavior to durable aggregate decisions in this task.

## Out of scope
Saga builders, compensation ordering, audit projections, provider serializers, and runtime
command processing.

## Definition of done
- [ ] New tests are red before implementation and green after
- [ ] `dotnet test OrcaCore.slnx --filter "AC=AC-401|AC=AC-404"` passes for tests introduced in this task
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] PROGRESS.md updated; committed as "T5-01: saga contracts and lifecycle states (SG-001, SG-013)"
