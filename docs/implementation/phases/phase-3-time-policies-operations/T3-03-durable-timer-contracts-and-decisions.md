# T3-03: Add durable timer contracts and decisions

**Difficulty**: Sonnet        **Depends on**: T3-02
**Spec**: EV-050, DU-011..012, PR-014        **AC**: AC-111

## Goal
Represent durable timers as engine facts and commands. The durable aggregate records timer
scheduling and accepts a fire command without depending on a provider implementation yet.

## Read first
- `src/OrcaCore.Abstractions/Providers/ProviderPorts.cs`
- `src/OrcaCore.Abstractions/Durable/WorkflowCommand.cs`
- `src/OrcaCore.Abstractions/Durable/WorkflowEvent.cs`
- `src/OrcaCore.Engine.Durable/Aggregates/DurableWorkflowAggregate.cs`
- `tests/OrcaCore.Engine.Durable.Tests/Aggregates/DurableAggregateTests.cs`
- Spec: `docs/specs/05-requirements-events-waits-timers.md`
- Spec: `docs/specs/10-provider-model-and-extensibility.md`

## Deliverables
- Durable timer command/event contracts in `src/OrcaCore.Abstractions/Durable/`
- Aggregate decisions for scheduling and firing timers
- Unit tests in `tests/OrcaCore.Engine.Durable.Tests/Aggregates/`

## Tests to write FIRST
In `tests/OrcaCore.Engine.Durable.Tests/Aggregates/DurableTimerAggregateTests.cs`:
1. `ScheduleTimer_RecordsTimerScheduledEvent` - aggregate emits a timer scheduled fact with due time.
2. `FireTimer_ForActiveTimer_RecordsTimerFiredEvent` - aggregate emits one fired fact.
3. `FireTimer_ForAlreadyFiredTimer_IsNoOp` - repeated fire command does not duplicate outcome.

## Implementation notes
Do not implement InMemory or PostgreSQL scheduling here. Use strongly typed `TimerId` and
`CommandId`; keep provider-specific storage out of aggregate contracts.

## Out of scope
Provider scheduler storage, paused buffering, transient timers, timeout policy, and races.

## Definition of done
- [ ] New tests fail before implementation and pass after
- [ ] `dotnet test OrcaCore.slnx --filter DurableTimer` passes
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] PROGRESS.md updated; committed as "T3-03: durable timer contracts (EV-050, PR-014)"
