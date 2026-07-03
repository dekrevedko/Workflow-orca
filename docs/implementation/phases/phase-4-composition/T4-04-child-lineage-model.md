# T4-04: Add child lineage model

**Difficulty**: Haiku        **Depends on**: T4-03
**Spec**: CP-020        **AC**: AC-614

## Goal
Add durable lineage metadata for parent, child, and root instance relationships. Lineage
must be visible in snapshots and projections without loading business payloads.

## Read first
- `v3-gpt/src/OrcaCore.Abstractions/Instances/WorkflowInstanceSnapshot.cs`
- `v3-gpt/src/OrcaCore.Abstractions/Providers/ProviderCommitContracts.cs`
- `v3-gpt/src/OrcaCore.Engine.Durable/Aggregates/DurableWorkflowAggregate.cs`
- `v3-gpt/src/OrcaCore.Providers.InMemory/InMemoryWorkflowProvider.cs`
- `v3-gpt/src/OrcaCore.Providers.PostgreSql/PostgreSqlWorkflowStore.cs`
- Spec: `docs/specs/08-requirements-composition.md`
- Spec: `docs/specs/12-acceptance-criteria.md`

## Deliverables
- Parent/root lineage fields on durable commands/events/projections/snapshots
- InMemory and PostgreSQL projection support
- Durable management query support for lineage

## Tests to write FIRST
In `v3-gpt/tests/OrcaCore.Engine.Durable.Tests/Composition/ChildLineageTests.cs`:
1. `ChildLineage_ProjectionCarriesParentAndRootIds`
2. `[Trait("AC","AC-614")] ChildLineage_QueryReturnsTree`
In `v3-gpt/tests/OrcaCore.Acceptance.Tests/ChildWorkflowAcceptanceTests.cs`:
3. `[Trait("AC","AC-614")] ChildWorkflow_LineageQueriesTraverseTree`

## Implementation notes
This task introduces metadata only. Do not add `RunChild` authoring or outbox spawning yet.

## Out of scope
Starting children, completion propagation, throttling, and compensation.

## Definition of done
- [ ] New tests fail before implementation and pass after
- [ ] `dotnet test v3-gpt/OrcaCore.slnx --filter "ChildLineage|AC=AC-614"` passes
- [ ] `dotnet build v3-gpt/OrcaCore.slnx` - zero warnings
- [ ] PROGRESS.md updated; committed as "T4-04: child lineage model (CP-020, AC-614)"
