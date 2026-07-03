# T5-05: Add saga timeout and cancellation interaction

**Difficulty**: Haiku        **Depends on**: T5-03, T5-04
**Spec**: SG-011, SG-014, EV-052        **AC**: AC-405

## Goal
Document and enforce how saga forward-step timeout and cancellation interact with
compensation. Timeout follows explicit compensation policy; cancellation never implies
compensation.

## Read first
- `v3-gpt/src/OrcaCore.Core/Definitions/WorkflowPolicySet.cs`
- `v3-gpt/src/OrcaCore.Engine.Durable/Aggregates/DurableWorkflowAggregate.cs`
- `v3-gpt/src/OrcaCore.Engine.Durable/Execution/DurableCommandProcessor.cs`
- `v3-gpt/src/OrcaCore.Abstractions/Durable/WorkflowCommand.cs`
- `v3-gpt/tests/OrcaCore.Engine.Durable.Tests/ExternalJobs/ExternalJobCancellationTests.cs`
- Spec: `docs/specs/07-requirements-saga.md` sections SG-011 and SG-014
- Spec: `docs/specs/12-acceptance-criteria.md` AC-405

## Deliverables
- Saga timeout policy metadata as needed under `v3-gpt/src/OrcaCore.Core/Definitions/`
- Durable decisions for timeout-triggered compensation policy
- Cancellation path tests proving no implicit compensation
- Tests in `v3-gpt/tests/OrcaCore.Engine.Durable.Tests/Sagas/SagaPolicyInteractionTests.cs`
- Acceptance coverage in `v3-gpt/tests/OrcaCore.Acceptance.Tests/SagaAcceptanceTests.cs`

## Tests to write FIRST
In `v3-gpt/tests/OrcaCore.Engine.Durable.Tests/Sagas/SagaPolicyInteractionTests.cs`:
1. `ForwardTimeout_WhenPolicyRequiresCompensation_RecordsCompensationPlan` - timeout applies compensation policy. Trait AC-405.
2. `SagaCancellation_NeverTriggersCompensation` - cancellation records cancellation without compensation facts. Trait AC-405.

## Implementation notes
Keep cancellation semantics aligned with the already durable external-job cancellation path:
record cancellation intent before terminal state, but do not convert cancellation into
compensation.

## Out of scope
Retry policy overhaul, external job adapter behavior, operator recovery, and ephemeral saga
mode.

## Definition of done
- [ ] New tests are red before implementation and green after
- [ ] `dotnet test v3-gpt/OrcaCore.slnx --filter "AC=AC-405"` passes
- [ ] `dotnet build v3-gpt/OrcaCore.slnx` - zero warnings
- [ ] PROGRESS.md updated; committed as "T5-05: saga timeout and cancellation policy (SG-014)"
