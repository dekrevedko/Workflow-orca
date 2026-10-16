# T1-08: Add resident waits and instance-targeted matching

**Difficulty**: Haiku        **Depends on**: T1-06
**Spec**: EV-020, EV-021, EV-022, EV-023, EV-040, CR-040        **AC**: AC-006, AC-101, AC-102, AC-103

## Goal
Implement the first event-driven suspension path for the ephemeral engine. A `Wait` result
creates an active resident wait record and moves the instance to `Waiting`. An
instance-targeted matching event resumes exactly once through the execution lane and exposes
the payload to the next step.

## Read first
- `src/OrcaCore.Abstractions/Events/EventEnvelope.cs`
- `src/OrcaCore.Abstractions/Steps/StepContext.cs`
- `src/OrcaCore.Engine.Ephemeral/EphemeralWorkflowEngine.cs`
- `src/OrcaCore.Engine.Ephemeral/Execution/WorkflowInstance.cs`
- Spec: `docs/specs/05-requirements-events-waits-timers.md` sections 5.3 and 5.5

## Deliverables
- Runtime wait model with `WaitId`, event name, correlation ID, registered timestamp,
  optional branch identity placeholder, status, and resident mode.
- Public ephemeral instance-targeted event delivery method on the engine facade.
- `StepContext<TState>` resumed-event accessor for the first step after resume only.
- Active-wait snapshot projection sufficient for management and acceptance inspection.

## Tests to write FIRST
In `tests/OrcaCore.Engine.Ephemeral.Tests/Execution/WaitMatchingTests.cs`:
1. `Run_WaitResult_RegistersActiveWaitAndSetsWaiting`
2. `RaiseEventAsync_MatchingEvent_ResumesAndClearsWait`
3. `RaiseEventAsync_MatchingEvent_ProvidesPayloadToNextStepOnly`
4. `RaiseEventAsync_WrongNameOrCorrelation_LeavesInstanceWaiting`
5. `RaiseEventAsync_TwoConcurrentMatches_OnlyOneContinuationCommits`

In `tests/OrcaCore.Acceptance.Tests/WaitAcceptanceTests.cs`:
6. `[Trait("AC","AC-101")] Wait_EntersWaiting_WithInspectableActiveWait`
7. `[Trait("AC","AC-102")] MatchingEvent_ResumesExactlyOnce_WithPayload`
8. `[Trait("AC","AC-103")] NonMatchingEvent_DoesNotResume`
9. `[Trait("AC","AC-006")] ConcurrentResumeAttempts_ProduceOneSequentialOutcome`

Use AwesomeAssertions for assertions.

## Implementation notes
Route event delivery through the T1-06 lane. Matching is instance-local only in this task:
correlation-targeted lookup and fanout arrive in T1-10. Do not buffer early events here; a
non-matching event can return a clear no-match result.

## Out of scope
Mailbox buffering, EventId deduplication, correlation index, definition fanout, loop
iteration wait isolation, parallel branch identity enforcement.

## Definition of done
- [ ] All new tests green; full affected suites green
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] AC-006 and AC-101 through AC-103 are green in `OrcaCore.Acceptance.Tests`
- [ ] PROGRESS.md updated; committed as "T1-08: resident waits and matching (AC-006, AC-101-103)"
