# T1-15: Implement Yield continuation

**Difficulty**: Haiku        **Depends on**: T1-06
**Spec**: CR-017        **AC**: AC-013

## Goal
Implement `StepResult.Yield` in the ephemeral interpreter. A yielding step commits the state
progress made so far, releases the instance lane, remains `Running`, and reschedules the
same step until it completes without duplicate effects.

## Read first
- `v3-gpt/src/OrcaCore.Abstractions/Steps/StepResult.cs`
- `v3-gpt/src/OrcaCore.Engine.Ephemeral/Execution/Interpreter.cs`
- `v3-gpt/src/OrcaCore.Engine.Ephemeral/Execution/InstanceExecutionLane.cs`
- `v3-gpt/src/OrcaCore.Engine.Ephemeral/Execution/WorkflowInstance.cs`
- Spec: `docs/specs/04-requirements-core-runtime.md` section 4.2

## Deliverables
- Interpreter handling for `StepResult.Yield`.
- Same-step continuation scheduling through the per-instance lane.
- Observable committed-progress behavior for each yield in ephemeral mode.
- Acceptance coverage for the ephemeral portion of AC-013.

## Tests to write FIRST
In `v3-gpt/tests/OrcaCore.Engine.Ephemeral.Tests/Execution/YieldTests.cs`:
1. `Run_YieldingStep_RemainsRunningBetweenContinuations`
2. `Run_YieldingStep_ReentersSameStepUntilCompleted`
3. `Run_YieldingStep_CommitsProgressForEachYield`
4. `Run_YieldingStep_DoesNotDuplicateCompletedEffects`
5. `Run_YieldingStep_ReleasesLaneBetweenContinuations`

In `v3-gpt/tests/OrcaCore.Acceptance.Tests/YieldAcceptanceTests.cs`:
6. `[Trait("AC","AC-013")] Yield_CommitsProgressAndCompletesExactlyOnce`

Use AwesomeAssertions for assertions.

## Implementation notes
Do not rely on local variables inside the step surviving a yield. The task only proves the
ephemeral behavior; durable crash survival is a later phase concern. Keep scheduling simple
and deterministic.

## Out of scope
Durable checkpoint persistence, fairness policies, `ForEach`, parallel governance.

## Definition of done
- [ ] All new tests green; full affected suites green
- [ ] `dotnet build v3-gpt/OrcaCore.slnx` - zero warnings
- [ ] AC-013 is green in `OrcaCore.Acceptance.Tests`
- [ ] PROGRESS.md updated; committed as "T1-15: Yield (AC-013)"
