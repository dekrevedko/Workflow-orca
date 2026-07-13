# T4-07: Add durable child throttling

**Difficulty**: Haiku        **Depends on**: T4-06
**Spec**: CP-023        **AC**: AC-609

## Goal
Enforce durable `RunChildren` max-concurrency from persisted scheduler state. Restart must
reconstruct the active window from group records.

## Read first
- `src/OrcaCore.Engine.Durable/Aggregates/DurableWorkflowAggregate.cs`
- `src/OrcaCore.Engine.Durable/Execution/DurableCommandProcessor.cs`
- `tests/OrcaCore.Engine.Durable.Tests/Composition/RunChildrenTests.cs`
- `src/OrcaCore.Abstractions/Durable/WorkflowEvent.cs`
- Spec: `docs/specs/08-requirements-composition.md`
- Spec: `docs/specs/12-acceptance-criteria.md`

## Deliverables
- `NextDispatchIndex` and active-child count in durable group state
- Rehydration logic for throttled windows
- Child-start outbox enqueue when capacity frees

## Tests to write FIRST
In `tests/OrcaCore.Engine.Durable.Tests/Composition/DurableChildThrottlingTests.cs`:
1. `[Trait("AC","AC-609")] RunChildren_MaxConcurrency_HoldsAcrossRestart`
In `tests/OrcaCore.Acceptance.Tests/ChildWorkflowAcceptanceTests.cs`:
2. `[Trait("AC","AC-609")] RunChildren_DurableThrottlingSurvivesRestart`

## Implementation notes
Throttle state is durable scheduler state, not an in-memory semaphore.

## Out of scope
Barrier resume tokens, residual cancellation, and durable resource pools.

## Definition of done
- [ ] New tests fail before implementation and pass after
- [ ] `dotnet test OrcaCore.slnx --filter "DurableChildThrottling|AC=AC-609"` passes
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] PROGRESS.md updated; committed as "T4-07: durable child throttling (CP-023, AC-609)"
