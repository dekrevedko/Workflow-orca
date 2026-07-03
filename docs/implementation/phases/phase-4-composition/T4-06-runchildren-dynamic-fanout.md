# T4-06: Add RunChildren dynamic fanout

**Difficulty**: Sonnet        **Depends on**: T4-05
**Spec**: CP-022, CP-025, CP-030        **AC**: AC-607, AC-608

## Goal
Add durable `RunChildren` group materialization for dynamic child fanout. Child identities,
item snapshots, and partitioning must be deterministic across restart.

## Read first
- `v3-gpt/src/OrcaCore.Engine.Durable/Aggregates/DurableWorkflowAggregate.cs`
- `v3-gpt/src/OrcaCore.Engine.Durable/Execution/DurableCommandProcessor.cs`
- `v3-gpt/src/OrcaCore.Core/Definitions/`
- `v3-gpt/src/OrcaCore.Abstractions/Durable/WorkflowEvent.cs`
- `v3-gpt/tests/OrcaCore.Engine.Durable.Tests/Composition/RunChildTests.cs`
- Spec: `docs/specs/08-requirements-composition.md`
- Spec: `docs/specs/12-acceptance-criteria.md`

## Deliverables
- Durable child group state records
- Deterministic child id generation
- Stable item snapshot metadata
- Initial child-start window enqueue

## Tests to write FIRST
In `v3-gpt/tests/OrcaCore.Engine.Durable.Tests/Composition/RunChildrenTests.cs`:
1. `[Trait("AC","AC-607")] RunChildren_RestartDoesNotDuplicateChildIds`
2. `[Trait("AC","AC-608")] RunChildren_ItemSnapshotsRemainStableAcrossRestart`
In `v3-gpt/tests/OrcaCore.Acceptance.Tests/ChildWorkflowAcceptanceTests.cs`:
3. `[Trait("AC","AC-607")] RunChildren_ChildIdsAreDeterministic`
4. `[Trait("AC","AC-608")] RunChildren_ItemSnapshotsAreStable`

## Implementation notes
Persist group materialization before emitting child-start outbox records. Do not deserialize
business payloads for inspection.

## Out of scope
Throttling after the initial window, exactly-once resume tokens, residual policies, and saga
compensation.

## Definition of done
- [ ] New tests fail before implementation and pass after
- [ ] `dotnet test v3-gpt/OrcaCore.slnx --filter "RunChildren|AC=AC-607|AC=AC-608"` passes
- [ ] `dotnet build v3-gpt/OrcaCore.slnx` - zero warnings
- [ ] PROGRESS.md updated; committed as "T4-06: RunChildren deterministic fanout (CP-025, AC-607, AC-608)"
