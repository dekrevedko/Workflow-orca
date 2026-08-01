# T1-12: Execute Parallel branches with WhenAll join

**Difficulty**: Sonnet        **Depends on**: T1-06
**Spec**: CP-001, CP-002, CP-003, CR-044        **AC**: AC-007, AC-110, AC-201, AC-202, AC-203

## Goal
Implement `Parallel` for Slice 1 with isolated branch state and an atomic `WhenAll` join.
Branches may execute concurrently, but every branch-state commit and join check goes through
the per-instance lane. Waits inside branches stay branch-scoped.

## Read first
- `src/OrcaCore.Core/Definitions/Nodes.cs`
- `src/OrcaCore.Engine.Ephemeral/Execution/Interpreter.cs`
- `src/OrcaCore.Engine.Ephemeral/Execution/WorkflowInstance.cs`
- `tests/OrcaCore.TestSupport/RaceCoordinator.cs`
- Spec: `docs/specs/08-requirements-composition.md` section 8.1

## Deliverables
- Interpreter support for `ParallelNode<TState>` with branch identities.
- Runtime branch-state tracking and atomic `WhenAll` continuation.
- Branch identity participation in wait records and matching.
- Acceptance coverage for parallel join, deterministic branch order, and branch-scoped waits.

## Tests to write FIRST
In `tests/OrcaCore.Engine.Ephemeral.Tests/Execution/ParallelTests.cs`:
1. `Run_ParallelBranches_AllBranchesExecuteBeforeContinuation`
2. `Run_ParallelBranchesCompletingTogether_ContinuationRunsOnce`
3. `Run_ParallelBranchesDifferentOrders_FinalStateIsDeterministic`
4. `Run_ParallelWithNoOpShapeChange_ContinuationBehaviorUnchanged`
5. `RaiseEventAsync_ParallelBranchWait_ResumesOnlyMatchingBranch`
6. `Run_ParallelBranchCommits_RouteThroughInstanceLane`

In `tests/OrcaCore.Acceptance.Tests/ParallelAcceptanceTests.cs`:
7. `[Trait("AC","AC-201")] ParallelWhenAll_ContinuationRunsExactlyOnce`
8. `[Trait("AC","AC-202")] ParallelWhenAll_OrderInsensitiveOutcome`
9. `[Trait("AC","AC-203")] ParallelWhenAll_GraphShapeInsensitive`
10. `[Trait("AC","AC-110")] ParallelWaits_MatchingEventResumesOnlyItsBranch`
11. `[Trait("AC","AC-007")] RacingBranchCompletions_SerializeDeterministically`

Use AwesomeAssertions for assertions.

## Implementation notes
Keep branch commit state internal and snapshot-only. Avoid implementing `WhenFirst` or losing
branch policies. If the task grows beyond sizing limits, split branch waits into a follow-up
before implementation.

## Out of scope
`WhenFirst`, `ForEach`, branch concurrency limits, timers, durable branch persistence.

## Definition of done
- [ ] All new tests green; full affected suites green
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] AC-007, AC-110, and AC-201 through AC-203 are green in `OrcaCore.Acceptance.Tests`
- [ ] PROGRESS.md updated; committed as "T1-12: Parallel WhenAll (AC-007, AC-110, AC-201-203)"
