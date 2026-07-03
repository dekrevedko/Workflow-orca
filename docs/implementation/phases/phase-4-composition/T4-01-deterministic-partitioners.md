# T4-01: Add deterministic partitioners

**Difficulty**: Haiku        **Depends on**: T4-00
**Spec**: CP-030        **AC**: structural

## Goal
Add shared partitioning primitives for item and batch fanout. Partitioners must preserve
stable ordering for the same input without depending on hash iteration or runtime timing.

## Read first
- `v3-gpt/src/OrcaCore.Core/Building/WorkflowBuilder.cs`
- `v3-gpt/src/OrcaCore.Core/Definitions/Nodes.cs`
- `v3-gpt/tests/OrcaCore.Core.Tests/Building/WorkflowBuilderTests.cs`
- `v3-gpt/tests/OrcaCore.Core.Tests/Building/WorkflowPolicyBuilderTests.cs`
- Spec: `docs/specs/08-requirements-composition.md`

## Deliverables
- Partitioning contracts/records in `v3-gpt/src/OrcaCore.Core/Definitions/`
- Builder-facing partitioner helpers where needed by later `ForEach` and `RunChildren`
- Structural tests in `v3-gpt/tests/OrcaCore.Core.Tests/Building/PartitionerTests.cs`

## Tests to write FIRST
In `v3-gpt/tests/OrcaCore.Core.Tests/Building/PartitionerTests.cs`:
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
- [ ] `dotnet test v3-gpt/OrcaCore.slnx --filter "Partitioner"` passes
- [ ] `dotnet build v3-gpt/OrcaCore.slnx` - zero warnings
- [ ] PROGRESS.md updated; committed as "T4-01: deterministic partitioners (CP-030)"
