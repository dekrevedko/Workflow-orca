# T1-13: Add management query baseline

**Difficulty**: Sonnet        **Depends on**: T1-05
**Spec**: MG-001, MG-002, MG-003, MG-005, MG-010, EV-013        **AC**: AC-009, AC-115, AC-501, AC-502, AC-503

## Goal
Expose the first management surface for ephemeral instances: scoped selection, constrained
metadata filters, snapshot queries, typed state reads, active wait inspection, and grouped
statistics. Public results are immutable snapshots or copies, never live runtime objects.

## Read first
- `src/OrcaCore.Abstractions/Instances/WorkflowInstanceSnapshot.cs`
- `src/OrcaCore.Engine.Ephemeral/EphemeralWorkflowEngine.cs`
- `src/OrcaCore.Engine.Ephemeral/Execution/IInstanceRegistry.cs`
- `src/OrcaCore.Engine.Ephemeral/Execution/WorkflowInstance.cs`
- Spec: `docs/specs/09-requirements-management-operations.md` sections 9.1 and 9.2

## Deliverables
- Public ephemeral management entry point with `All()`, definition scope, `Instance(id)`,
  constrained `Where(...)`, `List`, `Count`, `Get`, `GetState<TState>`, `GetActiveWaits`,
  and `Statistics`.
- Internal query model derived from expression-style metadata predicates.
- Bulk retrieval paths for ID sets and filters.
- Immutable snapshot/copy behavior for every public query result.

## Tests to write FIRST
In `tests/OrcaCore.Engine.Ephemeral.Tests/Management/ManagementQueryTests.cs`:
1. `Where_StatusFilter_ListAndCountReturnSameSelection`
2. `Get_ReturnsSnapshotCopy_NotLiveInstance`
3. `GetState_ReturnsCopy_ExternalMutationDoesNotAffectEngineState`
4. `GetState_WrongType_ReturnsClearFailure`
5. `GetActiveWaits_ReturnsActiveWaitSnapshotsOnly`
6. `Statistics_GroupsByDefinitionVersionAndStatus`
7. `BulkGet_ByIdsOrFilter_UsesSingleRegistryOperation`

In `tests/OrcaCore.Acceptance.Tests/ManagementAcceptanceTests.cs`:
8. `[Trait("AC","AC-501")] WhereOverSnapshots_ListsAndCountsMatchingInstances`
9. `[Trait("AC","AC-502")] SameSelection_DrivesListAndSupportedCommand`
10. `[Trait("AC","AC-503")] Statistics_GroupCountsByDefinitionAndStatus`
11. `[Trait("AC","AC-009")] PublicApiResults_DoNotLeakLiveInstances`
12. `[Trait("AC","AC-115")] BulkRetrieval_ReturnsMatchesWithoutPerInstanceRoundTrips`

Use AwesomeAssertions for assertions.

## Implementation notes
Keep unsupported durable-only commands absent from the ephemeral API. AC-502 can use a
supported ephemeral command available by this point, such as event delivery over a selected
wait set, rather than durable-only retry.

## Out of scope
Cancel, Terminate, Pause/Resume, Retry, history, archive, purge, durable provider queries.

## Definition of done
- [ ] All new tests green; full affected suites green
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] No public result exposes mutable live runtime state
- [ ] AC-009, AC-115, and AC-501 through AC-503 are green in `OrcaCore.Acceptance.Tests`
- [ ] PROGRESS.md updated; committed as "T1-13: management query baseline (AC-009, AC-115, AC-501-503)"
