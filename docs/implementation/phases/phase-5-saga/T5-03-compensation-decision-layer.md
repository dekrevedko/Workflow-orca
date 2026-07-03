# T5-03: Add durable compensation decision layer

**Difficulty**: Sonnet        **Depends on**: T5-01, T5-02
**Spec**: SG-010, SG-011, SG-020        **AC**: AC-401, AC-402, AC-403

## Goal
Teach the durable engine to record completed compensatable forward actions and trigger
compensation when saga failure policy requires it. Compensation order defaults to reverse
successful-completion order and is persisted as durable facts.

## Read first
- `v3-gpt/src/OrcaCore.Engine.Durable/Aggregates/DurableWorkflowAggregate.cs`
- `v3-gpt/src/OrcaCore.Engine.Durable/Execution/DurableCommandProcessor.cs`
- `v3-gpt/src/OrcaCore.Abstractions/Durable/WorkflowCommand.cs`
- `v3-gpt/src/OrcaCore.Abstractions/Durable/WorkflowEvent.cs`
- `v3-gpt/tests/OrcaCore.Engine.Durable.Tests/Aggregates/DurableAggregateTests.cs`
- Spec: `docs/specs/07-requirements-saga.md` sections 7.2 and 7.3
- Spec: `docs/specs/12-acceptance-criteria.md` AC-401 through AC-403

## Deliverables
- Durable saga decision methods in `v3-gpt/src/OrcaCore.Engine.Durable/Aggregates/DurableWorkflowAggregate.cs`
- Command processor entry points in `v3-gpt/src/OrcaCore.Engine.Durable/Execution/DurableCommandProcessor.cs`
- Tests in `v3-gpt/tests/OrcaCore.Engine.Durable.Tests/Sagas/CompensationDecisionTests.cs`
- Acceptance coverage in `v3-gpt/tests/OrcaCore.Acceptance.Tests/SagaAcceptanceTests.cs`

## Tests to write FIRST
In `v3-gpt/tests/OrcaCore.Engine.Durable.Tests/Sagas/CompensationDecisionTests.cs`:
1. `SagaSuccess_WhenNoFailure_CompletesWithoutCompensation` - successful forward path commits `Completed` and no compensation facts. Trait AC-401.
2. `SagaFailure_AfterForwardActions_RecordsCompensationPlan` - failure records a compensation plan for eligible actions. Trait AC-402.
3. `CompensationPlan_UsesReverseSuccessfulCompletionOrder` - default ordering is reverse completion order and durable. Trait AC-403.

In `v3-gpt/tests/OrcaCore.Acceptance.Tests/SagaAcceptanceTests.cs`:
1. `SagaFailure_CompensatesCompletedActionsInReverseOrder` - public path proves the ordering.

## Implementation notes
Use durable events as the source of truth; do not keep compensation state in volatile-only
collections. Repeated commands must become idempotent no-ops once the relevant compensation
facts already exist.

## Out of scope
Compensation action execution failures, timeout policies, operator recovery, provider
serializer updates beyond what is needed for in-memory tests, and child compensation.

## Definition of done
- [ ] New tests are red before implementation and green after
- [ ] `dotnet test v3-gpt/OrcaCore.slnx --filter "AC=AC-401|AC=AC-402|AC=AC-403"` passes
- [ ] `dotnet build v3-gpt/OrcaCore.slnx` - zero warnings
- [ ] PROGRESS.md updated; committed as "T5-03: durable compensation decision layer (SG-010, SG-011)"
