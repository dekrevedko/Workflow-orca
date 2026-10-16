# T1-14: Add terminal commands, named outcomes, and completion bridge

**Difficulty**: Haiku        **Depends on**: T1-13
**Spec**: CR-008, CR-016, CR-031        **AC**: AC-005, AC-011, AC-012, AC-014, AC-015, AC-516

## Goal
Complete the Slice 1 terminal-management behavior. The ephemeral engine supports graceful
cancel, forced terminate, named `End` outcome metadata, a synchronous completion bridge for
short workflows, and explicit safety semantics for broad destructive selections.

## Read first
- `src/OrcaCore.Abstractions/Instances/WorkflowStatus.cs`
- `src/OrcaCore.Core/Lifecycle/LifecycleMachine.cs`
- `src/OrcaCore.Engine.Ephemeral/EphemeralWorkflowEngine.cs`
- `src/OrcaCore.Engine.Ephemeral/Execution/WorkflowInstance.cs`
- Spec: `docs/specs/04-requirements-core-runtime.md` sections 4.1, 4.2, and 4.4

## Deliverables
- Public completion bridge that starts and awaits or polls to a terminal snapshot without
  exposing live state.
- Named end outcome propagation into runtime metadata and queryable snapshots.
- Ephemeral `Cancel` and `Terminate` management commands with per-instance results.
- Safety requirement for broad destructive `Terminate` selections.
- Clear lifecycle rejection for triggers against terminal instances.

## Tests to write FIRST
In `tests/OrcaCore.Engine.Ephemeral.Tests/Management/TerminalCommandTests.cs`:
1. `AwaitCompletionAsync_ImmediateWorkflow_ReturnsTerminalSnapshot`
2. `End_WithOutcomeName_RecordsOutcomeInSnapshotAndQueries`
3. `CancelAsync_RunningInstance_TransitionsToCancelledAndCancelsWaits`
4. `TerminateAsync_RunningInstance_TransitionsToTerminatedAndStopsAdvancement`
5. `TerminalInstance_RaiseEventCancelOrTerminate_ReturnsClearLifecycleError`
6. `AllTerminate_WithoutExplicitSafety_IsRejected`
7. `AllTerminate_WithExplicitSafety_ReturnsAffectedCounts`

In `tests/OrcaCore.Acceptance.Tests/TerminalAcceptanceTests.cs`:
8. `[Trait("AC","AC-011")] CompletionBridge_ReturnsTerminalSnapshotWithoutLiveState`
9. `[Trait("AC","AC-012")] NamedEndOutcome_IsRecordedAndQueryable`
10. `[Trait("AC","AC-014")] GracefulCancel_CancelsInFlightWorkAndActiveWaits`
11. `[Trait("AC","AC-015")] ForcedTerminate_PreventsFurtherAdvancement`
12. `[Trait("AC","AC-005")] TerminalInstances_RejectIllegalTriggers`
13. `[Trait("AC","AC-516")] BroadDestructiveSelection_RequiresExplicitSafety`

Use AwesomeAssertions for assertions.

## Implementation notes
Pause/Resume remain durable-only and must stay absent from the ephemeral public API. For
cancel, wire the execution cancellation token through the lane boundary without exposing
runtime internals. For terminate, stop at a commit boundary; do not attempt compensation.

## Out of scope
Durable lifecycle event publication, retry, archive, purge, pause/resume, compensation.

## Definition of done
- [ ] All new tests green; full affected suites green
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] Ephemeral public API has no Pause, Resume, Retry, History, Archive, or Purge commands
- [ ] AC-005, AC-011, AC-012, AC-014, AC-015, and AC-516 are green in `OrcaCore.Acceptance.Tests`
- [ ] PROGRESS.md updated; committed as "T1-14: terminal commands and bridge (AC-005, AC-011-012, AC-014-015, AC-516)"
