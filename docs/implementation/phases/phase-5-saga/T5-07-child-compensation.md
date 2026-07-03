# T5-07: Add explicit child compensation

**Difficulty**: Sonnet        **Depends on**: T5-03, T5-06
**Spec**: CP-035, SG-011, SG-012        **AC**: AC-616

## Goal
Add durable-only child compensation semantics. `Compensate(group, spec)` must spawn one
compensation per completed child, never be triggered implicitly by cancellation or child
completion, and be idempotent on repeat.

## Read first
- `v3-gpt/src/OrcaCore.Engine.Durable/Aggregates/DurableWorkflowAggregate.cs`
- `v3-gpt/src/OrcaCore.Engine.Durable/Execution/DurableCommandProcessor.cs`
- `v3-gpt/src/OrcaCore.Abstractions/Durable/RunChildrenPolicies.cs`
- `v3-gpt/src/OrcaCore.Abstractions/Durable/WorkflowEvent.cs`
- `v3-gpt/tests/OrcaCore.Engine.Durable.Tests/Composition/RunChildrenTests.cs`
- Spec: `docs/specs/08-requirements-composition.md` CP-035
- Spec: `docs/specs/12-acceptance-criteria.md` AC-616

## Deliverables
- Durable child-compensation command/event contracts
- Aggregate decisions that materialize child compensation once per completed child
- Outbox records for child compensation starts if needed
- Tests in `v3-gpt/tests/OrcaCore.Engine.Durable.Tests/Sagas/ChildCompensationTests.cs`
- Acceptance coverage in `v3-gpt/tests/OrcaCore.Acceptance.Tests/ChildCompensationAcceptanceTests.cs`

## Tests to write FIRST
In `v3-gpt/tests/OrcaCore.Engine.Durable.Tests/Sagas/ChildCompensationTests.cs`:
1. `CompensateChildGroup_SpawnsOneCompensationPerCompletedChild` - completed children each get one compensation. Trait AC-616.
2. `CancelChildGroup_DoesNotTriggerCompensation` - cancellation alone records no compensation. Trait AC-616.
3. `RepeatChildGroupCompensation_IsIdempotent` - repeated command does not duplicate compensation. Trait AC-616.

## Implementation notes
Compensation APIs must not appear on ephemeral or core regular workflow surfaces. Child
compensation is explicit and durable-only.

## Out of scope
General fanout mechanics changes, RabbitMQ native fanout, and non-child compensation audit
beyond data needed for AC-616.

## Definition of done
- [ ] New tests are red before implementation and green after
- [ ] `dotnet test v3-gpt/OrcaCore.slnx --filter "AC=AC-616"` passes
- [ ] `dotnet build v3-gpt/OrcaCore.slnx` - zero warnings
- [ ] PROGRESS.md updated; committed as "T5-07: explicit child compensation (CP-035)"
