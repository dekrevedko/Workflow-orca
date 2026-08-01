# T4-02: Add ephemeral ForEach dispatch

**Difficulty**: Sonnet        **Depends on**: T4-01
**Spec**: CP-010, CP-011, CP-012, CP-013        **AC**: AC-601, AC-602, AC-603

## Goal
Add ephemeral `ForEach` as an in-instance composition primitive. The parent instance owns
group and item state, dispatches items in deterministic order, and honors max concurrency.

## Read first
- `src/OrcaCore.Core/Building/WorkflowBuilder.cs`
- `src/OrcaCore.Core/Definitions/Nodes.cs`
- `src/OrcaCore.Engine.Ephemeral/Execution/Interpreter.cs`
- `src/OrcaCore.Engine.Ephemeral/Execution/WorkflowInstance.cs`
- `tests/OrcaCore.Engine.Ephemeral.Tests/Execution/ParallelTests.cs`
- Spec: `docs/specs/08-requirements-composition.md`
- Spec: `docs/specs/12-acceptance-criteria.md`

## Deliverables
- `ForEach` builder and definition model
- Ephemeral runtime group/item tracking and bounded dispatch
- Queryable group/item snapshots if required for tests

## Tests to write FIRST
In `tests/OrcaCore.Engine.Ephemeral.Tests/Execution/ForEachTests.cs`:
1. `[Trait("AC","AC-601")] ForEach_BatchSize_CreatesExpectedWorkItems`
2. `[Trait("AC","AC-602")] ForEach_WhenAll_ParentContinuesAfterAllItemsComplete`
3. `[Trait("AC","AC-603")] ForEach_MaxConcurrency_BoundsActiveItems`
In `tests/OrcaCore.Acceptance.Tests/ForEachAcceptanceTests.cs`:
4. `[Trait("AC","AC-601")] ForEach_RuntimeBatchingCreatesExpectedItems`
5. `[Trait("AC","AC-602")] ForEach_WhenAllCompletesParent`
6. `[Trait("AC","AC-603")] ForEach_HonorsMaxConcurrency`

## Implementation notes
`ForEach` is ephemeral-only and never creates child instances, lineage, or outbox records.
Use existing in-process governance/lanes rather than `Task.Run`.

## Out of scope
Failure policies, `WhenAny`, residual cancellation, durable children, and compensation.

## Definition of done
- [ ] New tests fail before implementation and pass after
- [ ] `dotnet test OrcaCore.slnx --filter "ForEach|AC=AC-601|AC=AC-602|AC=AC-603"` passes
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] PROGRESS.md updated; committed as "T4-02: ephemeral ForEach dispatch (CP-010, AC-601, AC-602, AC-603)"
