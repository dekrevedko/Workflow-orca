# T1-11: Isolate waits created inside loops

**Difficulty**: Haiku        **Depends on**: T1-07, T1-09
**Spec**: EV-043        **AC**: AC-109

## Goal
Ensure each `While` iteration creates a distinct wait identity and that stale events cannot
resume a later iteration. Loop waits remain normal resident waits, but their runtime identity
includes enough iteration context to prevent accidental reuse.

## Read first
- `src/OrcaCore.Core/Definitions/ExecutionPointer.cs`
- `src/OrcaCore.Engine.Ephemeral/Execution/Interpreter.cs`
- `src/OrcaCore.Engine.Ephemeral/Execution/WorkflowInstance.cs`
- `tests/OrcaCore.Engine.Ephemeral.Tests/Execution/WaitMatchingTests.cs`
- Spec: `docs/specs/05-requirements-events-waits-timers.md` section 5.5

## Deliverables
- Loop-iteration identity in runtime wait records where needed for stale-event rejection.
- Mailbox/wait matching updates so consumed or stale iteration events cannot resume later
  waits.
- Acceptance coverage for AC-109.

## Tests to write FIRST
In `tests/OrcaCore.Engine.Ephemeral.Tests/Execution/LoopWaitTests.cs`:
1. `Run_WhileRegistersWaitEachIteration_CreatesFreshWaitIds`
2. `RaiseEventAsync_EventForPreviousIteration_DoesNotResumeLaterIteration`
3. `RaiseEventAsync_CurrentIterationEvent_ResumesCurrentWait`
4. `Mailbox_PreviousIterationEvent_RemainsStaleForLaterWait`

In `tests/OrcaCore.Acceptance.Tests/LoopWaitAcceptanceTests.cs`:
5. `[Trait("AC","AC-109")] WaitInLoop_PreviousIterationEvent_CannotResumeLaterIteration`

Use AwesomeAssertions for assertions.

## Implementation notes
Prefer deriving iteration identity from the execution frame stack instead of adding public
authoring concepts. Do not weaken the EV-020 matching rule; iteration identity is runtime
wait identity, not a new caller-supplied matching dimension.

## Out of scope
Parallel branch wait isolation, timers, durable replay of loop waits.

## Definition of done
- [ ] All new tests green; full affected suites green
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] AC-109 is green in `OrcaCore.Acceptance.Tests`
- [ ] PROGRESS.md updated; committed as "T1-11: wait-in-loop isolation (AC-109)"
