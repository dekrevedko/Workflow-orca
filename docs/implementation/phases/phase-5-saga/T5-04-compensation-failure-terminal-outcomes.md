# T5-04: Add compensation failure terminal outcomes

**Difficulty**: Haiku        **Depends on**: T5-03
**Spec**: SG-012, SG-013        **AC**: AC-404, AC-409

## Goal
Make compensation completion and failure terminal outcomes observable and idempotent.
A failed compensating action must end the saga as `CompensationFailed`, while repeated
compensation requests must not duplicate compensation effects.

## Read first
- `v3-gpt/src/OrcaCore.Engine.Durable/Aggregates/DurableWorkflowAggregate.cs`
- `v3-gpt/src/OrcaCore.Engine.Durable/Execution/DurableCommandProcessor.cs`
- `v3-gpt/src/OrcaCore.Abstractions/Durable/WorkflowCommand.cs`
- `v3-gpt/src/OrcaCore.Abstractions/Durable/WorkflowEvent.cs`
- `v3-gpt/tests/OrcaCore.Engine.Durable.Tests/Sagas/CompensationDecisionTests.cs`
- Spec: `docs/specs/07-requirements-saga.md` section 7.2
- Spec: `docs/specs/12-acceptance-criteria.md` AC-404 and AC-409

## Deliverables
- Compensation completion/failure decisions in durable aggregate
- Public command processor entry points for compensation completion/failure commands
- Tests in `v3-gpt/tests/OrcaCore.Engine.Durable.Tests/Sagas/CompensationFailureTests.cs`
- Acceptance coverage in `v3-gpt/tests/OrcaCore.Acceptance.Tests/SagaAcceptanceTests.cs`

## Tests to write FIRST
In `v3-gpt/tests/OrcaCore.Engine.Durable.Tests/Sagas/CompensationFailureTests.cs`:
1. `CompensationFailure_EndsSagaAsCompensationFailed` - failed compensating action records distinct terminal status. Trait AC-404.
2. `RepeatedCompensationRequest_DoesNotDuplicateCompensationFacts` - duplicate request is idempotent. Trait AC-409.

## Implementation notes
Terminal status must be queryable through existing projections. Keep event order explicit:
compensation outcome fact first, then terminal fact.

## Out of scope
Manual recovery, timeout-triggered compensation, child compensation, and provider-specific
schema optimization.

## Definition of done
- [ ] New tests are red before implementation and green after
- [ ] `dotnet test v3-gpt/OrcaCore.slnx --filter "AC=AC-404|AC=AC-409"` passes
- [ ] `dotnet build v3-gpt/OrcaCore.slnx` - zero warnings
- [ ] PROGRESS.md updated; committed as "T5-04: compensation failure outcomes (SG-013)"
