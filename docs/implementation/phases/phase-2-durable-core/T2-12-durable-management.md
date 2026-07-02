# T2-12: Add durable management commands

**Difficulty**: Sonnet        **Depends on**: T2-11
**Spec**: MG-011, MG-012, MG-013, DU-050, DU-051        **AC**: AC-512, AC-513, AC-514, AC-515, AC-517

## Goal
Add durable-only pause/resume, retry, delete/purge, and retention baseline management commands.
Pause buffers events durably and resume can replay or discard the pause-window buffer.

## Read first
- `v3-gpt/src/OrcaCore.Engine.Durable/`
- `v3-gpt/src/OrcaCore.Engine.Ephemeral/Management/EphemeralManagement.cs`
- `v3-gpt/src/OrcaCore.Abstractions/Providers/`
- `v3-gpt/src/OrcaCore.Abstractions/Instances/WorkflowStatus.cs`
- Spec: `docs/specs/09-requirements-management-operations.md` sections 9.2 and 9.6

## Deliverables
- Durable management surface with Pause, Resume(Replay/Discard), Retry, Delete/Purge baseline
- Pause buffering and discard audit records in inbox/projections
- Retention-safe command guards.

## Tests to write FIRST
In `v3-gpt/tests/OrcaCore.Engine.Durable.Tests/Management/DurableManagementTests.cs`:
1. `[Trait("AC","AC-512")] Pause_RunningInstance_StopsAfterSafeBoundary`
2. `[Trait("AC","AC-513")] EventsDuringPause_AreBufferedAndDoNotResume`
3. `[Trait("AC","AC-514")] ResumeReplay_ProcessesBufferedDeliveriesInOrder`
4. `[Trait("AC","AC-515")] PausedInstance_RehydratesAsPausedAfterRestart`
5. `[Trait("AC","AC-517")] ResumeDiscard_DropsBufferAuditablyAndDedupsDiscardedEvents`
6. `EphemeralManagement_DoesNotExposePauseResumeRetryHistoryArchivePurge`

## Implementation notes
For Phase 2 AC-513, verify event-buffering only; timer-firing clause returns in Phase 3.

## Out of scope
Durable timers, history-pressure statistics, archive storage plugin details.

## Definition of done
- [ ] All new tests green; full affected suites green
- [ ] `dotnet build v3-gpt/OrcaCore.slnx` - zero warnings
- [ ] AC-512 through AC-515 and AC-517 are green
- [ ] PROGRESS.md updated; committed as "T2-12: durable management commands (AC-512-515, AC-517)"
