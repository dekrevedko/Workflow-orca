# T3-05: Implement timer and event race semantics

**Difficulty**: Sonnet        **Depends on**: T3-04
**Spec**: EV-051, EV-044, CR-032        **AC**: AC-112

## Goal
Define and enforce deterministic race behavior when an event wait and timer are both
eligible. Exactly one winner advances the workflow and the loser is cancelled or ignored by
explicit policy.

## Read first
- `src/OrcaCore.Engine.Ephemeral/Execution/WorkflowInstance.cs`
- `src/OrcaCore.Engine.Ephemeral/Execution/Interpreter.cs`
- `src/OrcaCore.Engine.Durable/Aggregates/DurableWorkflowAggregate.cs`
- `src/OrcaCore.Engine.Durable/Execution/DurableCommandProcessor.cs`
- `tests/OrcaCore.TestSupport/RaceCoordinator.cs`
- Spec: `docs/specs/05-requirements-events-waits-timers.md`
- Spec: `docs/specs/12-acceptance-criteria.md`

## Deliverables
- Race policy model if needed under `src/OrcaCore.Core/Definitions/`
- Ephemeral and durable race handling
- Deterministic race tests in engine test projects
- Acceptance coverage for AC-112

## Tests to write FIRST
In `tests/OrcaCore.Engine.Ephemeral.Tests/Timers/TimerEventRaceTests.cs`:
1. `EventAndTimerBecomeEligible_EventPolicyWinner_ConsumesEventCancelsTimer`
2. `EventAndTimerBecomeEligible_TimerPolicyWinner_CancelsWait`
In `tests/OrcaCore.Acceptance.Tests/TimerAcceptanceTests.cs`:
3. `[Trait("AC","AC-112")] TimerEventRace_SelectsOneWinnerDeterministically`

## Implementation notes
Use deterministic gates, not repeated stress loops. Losing wait/timer state must be
observable via snapshots or lifecycle metadata.

## Out of scope
`WhenFirst`, retry/timeout policy decorators beyond the minimal race policy, and saga
compensation.

## Definition of done
- [ ] New tests fail before implementation and pass after
- [ ] `dotnet test OrcaCore.slnx --filter "TimerEventRace|AC=AC-112"` passes
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] PROGRESS.md updated; committed as "T3-05: timer event race semantics (EV-051, AC-112)"
