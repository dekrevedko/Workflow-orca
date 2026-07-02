# T2-13: Add durable query projections

**Difficulty**: Haiku        **Depends on**: T2-12
**Spec**: DU-070, EV-011, MG-001, MG-002, MG-030        **AC**: AC-308

## Goal
Back durable management queries from provider projections so hot and cold instances are queried uniformly without deserializing business state.
Queries must cover status, definition, version, wait state, correlation, and timestamps.

## Read first
- `v3-gpt/src/OrcaCore.Engine.Durable/Management/`
- `v3-gpt/src/OrcaCore.Engine.Ephemeral/Management/EphemeralManagement.cs`
- `v3-gpt/src/OrcaCore.Abstractions/Providers/`
- `v3-gpt/src/OrcaCore.Abstractions/Instances/WorkflowInstanceSnapshot.cs`
- Spec: `docs/specs/06-requirements-durable-execution.md` section 6.9

## Deliverables
- Durable query surface over projection store
- Projection-backed `Where/List/Count/GetActiveWaits/Statistics`
- Active wait and pending-event projection updates.

## Tests to write FIRST
In `v3-gpt/tests/OrcaCore.Engine.Durable.Tests/Management/DurableQueryTests.cs`:
1. `[Trait("AC","AC-308")] QueryColdInstances_ByMetadata_DoesNotLoadBusinessPayload`
2. `Where_StatusAndDefinition_UsesProjectionStoreFilter`
3. `GetActiveWaits_ReturnsProjectedColdWaits`
4. `Statistics_GroupsProjectedInstancesByDefinitionVersionAndStatus`

## Implementation notes
Reuse the expression facade shape from ephemeral management where practical; provider port receives structured query data.

## Out of scope
History timeline, stuck detection, history pressure metrics.

## Definition of done
- [ ] All new tests green; full affected suites green
- [ ] `dotnet build v3-gpt/OrcaCore.slnx` - zero warnings
- [ ] AC-308 is green
- [ ] PROGRESS.md updated; committed as "T2-13: durable query projections (AC-308)"
