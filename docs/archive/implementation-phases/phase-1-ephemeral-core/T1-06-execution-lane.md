# T1-06: Add per-instance execution lane

**Difficulty**: Sonnet        **Depends on**: T1-05
**Spec**: CR-040, CR-042        **AC**: none

## Goal
Add the ephemeral engine's per-instance serialization primitive. All state mutations for one
instance flow through one async lane so concurrent commands are observed as a valid serial
order. This task installs the lane and proves non-reentrant advancement without implementing
wait/event resume yet.

## Read first
- `src/OrcaCore.Engine.Ephemeral/EphemeralWorkflowEngine.cs`
- `src/OrcaCore.Engine.Ephemeral/Execution/WorkflowInstance.cs`
- `src/OrcaCore.Engine.Ephemeral/Execution/Interpreter.cs`
- `tests/OrcaCore.TestSupport/RaceCoordinator.cs`
- Spec: `docs/specs/04-requirements-core-runtime.md` section 4.5

## Deliverables
- `src/OrcaCore.Engine.Ephemeral/Execution/InstanceExecutionLane.cs` internal async
  serializer keyed by `InstanceId`.
- Updates to the ephemeral engine/registry so start and future advancement operations enter
  through the lane.
- No public concurrency policy surface yet; rejection/queue behavior remains internal.

## Tests to write FIRST
In `tests/OrcaCore.Engine.Ephemeral.Tests/Execution/ExecutionLaneTests.cs`:
1. `RunAsync_ConcurrentCallsForSameInstance_DoNotOverlap` - given two blocked operations,
   when both are submitted, then the second starts only after the first exits.
2. `RunAsync_OperationsForDifferentInstances_CanOverlap` - given two instance IDs, then
   independent lanes do not serialize each other.
3. `StartAsync_ConcurrentStartsForDifferentInstances_AllComplete` - public engine smoke test
   proving the lane integration does not globally lock the engine.
4. `RunAsync_WhenOperationThrows_ReleasesLaneForNextOperation` - a failed mutation cannot
   permanently block future mutations.

Use AwesomeAssertions for assertions.

## Implementation notes
Keep the lane internal to the ephemeral engine and wrap the narrowest mutation boundary
already present after T1-05. Do not add waits, event delivery, or retry behavior here.
The AC-006 acceptance test moves to T1-08 because the public wait/resume behavior it names
does not exist until that task.

## Out of scope
Wait records, event routing, mailbox buffering, deduplication, parallel branch commits.

## Definition of done
- [ ] All new tests green; full affected suites green
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] No public API exposes the lane or live instance objects
- [ ] PROGRESS.md updated; committed as "T1-06: per-instance execution lane (CR-040/042)"
