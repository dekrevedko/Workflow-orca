# T2-05: Add durable aggregate decision layer

**Difficulty**: Sonnet        **Depends on**: T2-04
**Spec**: DU-010, DU-011, DU-012, DU-013, NF-020        **AC**: none

## Goal
Create a deterministic durable aggregate that rebuilds from checkpoint plus stream tail and decides workflow engine events from commands.
The layer is pure decision logic and does not perform provider I/O.

## Read first
- `v3-gpt/src/OrcaCore.Abstractions/Durable/`
- `v3-gpt/src/OrcaCore.Abstractions/Providers/`
- `v3-gpt/src/OrcaCore.Engine.Durable/`
- `v3-gpt/src/OrcaCore.Core/Definitions/`
- Spec: `docs/specs/06-requirements-durable-execution.md` sections 6.2 and 6.3

## Deliverables
- Aggregate state and replay logic in `v3-gpt/src/OrcaCore.Engine.Durable/Aggregates/`
- Command decision functions for start, step success/failure, wait registration/match, complete, and fail
- Unit tests proving deterministic replay and event decisions.

## Tests to write FIRST
In `v3-gpt/tests/OrcaCore.Engine.Durable.Tests/Aggregates/DurableAggregateTests.cs`:
1. `Rehydrate_CheckpointPlusTail_RestoresSameStateAsFullReplay`
2. `DecideStart_EmitsStartedAndVersionBoundEvents`
3. `DecideStepCompleted_EmitsStepAndStateCheckpointFacts`
4. `DecideStepFailed_EmitsFailedAndStopsFurtherDecisions`
5. `Replay_SameEventsTwice_IsDeterministic`

## Implementation notes
Business state may be represented as serialized checkpoint payload for this task. Do not run user steps here.

## Out of scope
Command pipeline, expected-version append, actual workflow interpreter integration, waits, inbox, outbox pump.

## Definition of done
- [ ] All new tests green; full affected suites green
- [ ] `dotnet build v3-gpt/OrcaCore.slnx` - zero warnings
- [ ] Decision layer has no provider implementation dependency
- [ ] PROGRESS.md updated; committed as "T2-05: durable aggregate decisions (DU-010-013)"
