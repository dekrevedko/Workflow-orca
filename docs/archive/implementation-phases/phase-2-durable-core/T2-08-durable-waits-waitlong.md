# T2-08: Add durable waits and WaitLong

**Difficulty**: Sonnet        **Depends on**: T2-07
**Spec**: EV-021, EV-040, EV-041, EV-044, MG-050, MG-052, MG-053        **AC**: AC-303, AC-304, AC-504, AC-505, AC-506

## Goal
Add durable wait records, durable-only `WaitLong`, cold eviction after committed waits, and lazy resume from durable state.
The ephemeral API must remain free of `WaitLong`.

## Read first
- `src/OrcaCore.Core/Building/WorkflowBuilder.cs`
- `src/OrcaCore.Engine.Durable/Execution/`
- `src/OrcaCore.Abstractions/Providers/`
- `src/OrcaCore.Abstractions/Instances/ActiveWaitSnapshot.cs`
- Spec: `docs/specs/05-requirements-events-waits-timers.md` sections 5.3 and 5.5

## Deliverables
- Durable authoring surface for `WaitLong`
- Durable wait records with `WaitMode` Resident/Cold and cancellation semantics
- Eviction/lazy resume behavior backed by provider projections
- Tests proving ephemeral `WaitLong` absence.

## Tests to write FIRST
In `tests/OrcaCore.Engine.Durable.Tests/Execution/DurableWaitTests.cs`:
1. `[Trait("AC","AC-303")] WaitLong_DurableSurfaceRegistersColdWait`
2. `[Trait("AC","AC-304")] WaitLong_AfterCommit_EvictsAndLazyResumes`
3. `[Trait("AC","AC-504")] IdleWaitingInstance_EvictsSafelyAndResumes`
4. `[Trait("AC","AC-505")] TerminalInstance_EvictsAfterTerminalHandlingAndRemainsQueryable`
5. `[Trait("AC","AC-506")] ConcurrentEvictAndResume_ProducesSingleMutator`
6. `EphemeralBuilder_DoesNotExposeWaitLong`

## Implementation notes
Keep cold eviction observable through management snapshots/projections. Do not implement timers.

## Out of scope
Inbox restart-safe dedup, pause/resume, timer wakeups, PostgreSQL.

## Definition of done
- [ ] All new tests green; full affected suites green
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] AC-303, AC-304, and AC-504 through AC-506 are green
- [ ] PROGRESS.md updated; committed as "T2-08: durable WaitLong and cold eviction (AC-303, AC-304, AC-504-506)"
