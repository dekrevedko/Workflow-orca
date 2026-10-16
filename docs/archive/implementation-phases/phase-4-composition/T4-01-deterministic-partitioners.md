# T4-01: Add deterministic partitioners

**Difficulty**: Haiku        **Depends on**: T4-00
**Spec**: CP-030        **AC**: structural

## Goal
Add shared partitioning primitives for item and batch fanout. Partitioners must preserve
stable ordering for the same input without depending on hash iteration or runtime timing.

## Read first
- `src/OrcaCore.Core/Building/WorkflowBuilder.cs`
- `src/OrcaCore.Core/Definitions/Nodes.cs`
- `tests/OrcaCore.Core.Tests/Building/WorkflowBuilderTests.cs`
- `tests/OrcaCore.Core.Tests/Building/WorkflowPolicyBuilderTests.cs`
- Spec: `docs/specs/08-requirements-composition.md`

## Deliverables
- Partitioning contracts/records in `src/OrcaCore.Core/Definitions/`
- Builder-facing partitioner helpers where needed by later `ForEach` and `RunChildren`
- Structural tests in `tests/OrcaCore.Core.Tests/Building/PartitionerTests.cs`

## Tests to write FIRST
In `tests/OrcaCore.Core.Tests/Building/PartitionerTests.cs`:
1. `ItemPartitioner_PreservesInputOrder`
2. `FixedBatchPartitioner_CreatesStableBatches`
3. `SelectorBatchPartitioner_UsesDeterministicSelector`

## Implementation notes
Do not introduce execution behavior in this task. Keep custom partitioners explicit
functions; no reflection or hidden construction.

## Out of scope
`ForEach`, child workflows, durable scheduler state, and runtime dispatch.

## Definition of done
- [ ] New tests fail before implementation and pass after
- [ ] `dotnet test OrcaCore.slnx --filter "Partitioner"` passes
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] PROGRESS.md updated; committed as "T4-01: deterministic partitioners (CP-030)"
