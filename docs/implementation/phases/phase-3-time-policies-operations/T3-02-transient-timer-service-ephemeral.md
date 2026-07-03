# T3-02: Add transient timer service for ephemeral workflows

**Difficulty**: Haiku        **Depends on**: T3-01
**Spec**: EV-050, CR-040        **AC**: AC-111

## Goal
Implement ephemeral `Delay` execution using `TimeProvider`-driven transient timers. A delay
continues the workflow exactly once after its due time and makes no durability claim.

## Read first
- `v3-gpt/src/OrcaCore.Engine.Ephemeral/Execution/Interpreter.cs`
- `v3-gpt/src/OrcaCore.Engine.Ephemeral/Execution/WorkflowInstance.cs`
- `v3-gpt/tests/OrcaCore.TestSupport/Clock.cs`
- `v3-gpt/tests/OrcaCore.Engine.Ephemeral.Tests/Execution/WaitMatchingTests.cs`
- `v3-gpt/tests/OrcaCore.Acceptance.Tests/WaitAcceptanceTests.cs`
- Spec: `docs/specs/05-requirements-events-waits-timers.md`
- Spec: `docs/specs/12-acceptance-criteria.md`

## Deliverables
- Timer service module under `v3-gpt/src/OrcaCore.Engine.Ephemeral/Timers/`
- Interpreter support for `DelayNode`
- Tests under `v3-gpt/tests/OrcaCore.Engine.Ephemeral.Tests/Timers/`
- Acceptance coverage in `v3-gpt/tests/OrcaCore.Acceptance.Tests/TimerAcceptanceTests.cs`

## Tests to write FIRST
In `v3-gpt/tests/OrcaCore.Engine.Ephemeral.Tests/Timers/EphemeralTimerTests.cs`:
1. `Delay_BeforeDueTime_RemainsWaiting` - fake time before due does not continue.
2. `Delay_AfterDueTime_ContinuesExactlyOnce` - advancing fake time resumes once.
In `v3-gpt/tests/OrcaCore.Acceptance.Tests/TimerAcceptanceTests.cs`:
3. `[Trait("AC","AC-111")] EphemeralDelay_CompletesAfterDueTime`

## Implementation notes
Use `FakeTimeProvider` in tests; no sleeps. The transient timer may be activation-local and
lost on process exit per EV-050.

## Out of scope
Durable timers, timer/event races, policy timeouts, and OpenTelemetry exporters.

## Definition of done
- [ ] New tests fail before implementation and pass after
- [ ] `dotnet test v3-gpt/OrcaCore.slnx --filter "Delay|AC=AC-111"` passes
- [ ] `dotnet build v3-gpt/OrcaCore.slnx` - zero warnings
- [ ] PROGRESS.md updated; committed as "T3-02: transient timer service (EV-050, AC-111)"
