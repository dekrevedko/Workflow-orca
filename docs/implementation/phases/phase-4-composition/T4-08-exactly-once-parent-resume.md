# T4-08: Add exactly-once parent resume tokens

**Difficulty**: Sonnet        **Depends on**: T4-07
**Spec**: CP-024        **AC**: AC-610, AC-611

## Goal
Add durable barrier tokens so concurrent child completions resume a parent exactly once.
Restart must replay the same recorded token and never mint another.

## Read first
- `v3-gpt/src/OrcaCore.Engine.Durable/Aggregates/DurableWorkflowAggregate.cs`
- `v3-gpt/src/OrcaCore.Engine.Durable/Execution/DurableCommandProcessor.cs`
- `v3-gpt/src/OrcaCore.Abstractions/Durable/WorkflowEvent.cs`
- `v3-gpt/tests/OrcaCore.Engine.Durable.Tests/Composition/DurableChildThrottlingTests.cs`
- Spec: `docs/specs/08-requirements-composition.md`
- Spec: `docs/specs/12-acceptance-criteria.md`

## Deliverables
- Durable parent resume token event/state
- Idempotent token consumption
- Restart replay behavior

## Tests to write FIRST
In `v3-gpt/tests/OrcaCore.Engine.Durable.Tests/Composition/ParentResumeTokenTests.cs`:
1. `[Trait("AC","AC-610")] ConcurrentChildCompletions_TriggerOneParentResume`
2. `[Trait("AC","AC-611")] Restart_ReplaysRecordedResumeToken`
In `v3-gpt/tests/OrcaCore.Acceptance.Tests/ChildWorkflowAcceptanceTests.cs`:
3. `[Trait("AC","AC-610")] RunChildren_BarrierFiresExactlyOnce`
4. `[Trait("AC","AC-611")] RunChildren_ResumeTokenIsReusedAfterRestart`

## Implementation notes
The token is durable truth. Rehydration must prefer the committed token over recomputing a new
one from completion state.

## Out of scope
Residual cancellation policies and compensation.

## Definition of done
- [ ] New tests fail before implementation and pass after
- [ ] `dotnet test v3-gpt/OrcaCore.slnx --filter "ParentResumeToken|AC=AC-610|AC=AC-611"` passes
- [ ] `dotnet build v3-gpt/OrcaCore.slnx` - zero warnings
- [ ] PROGRESS.md updated; committed as "T4-08: exactly-once parent resume tokens (CP-024, AC-610, AC-611)"
