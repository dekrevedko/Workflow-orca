# T6-04: Backfill continue-as-new contracts and aggregate behavior

**Difficulty**: Sonnet        **Depends on**: T6-03
**Spec**: DU-042        **AC**: AC-313

## Goal
Backfill continue-as-new as an early durable history-control operation. Spec open question
10 is resolved to Slice 2: durable event-sourced execution should have introduced DU-042
before Phase 6. This task corrects the current `current implementation` implementation by adding the
rollover command/event facts and aggregate behavior.

## Read first
- `src/OrcaCore.Abstractions/Durable/WorkflowCommand.cs`
- `src/OrcaCore.Abstractions/Durable/WorkflowEvent.cs`
- `src/OrcaCore.Abstractions/Providers/ProviderCommitContracts.cs`
- `src/OrcaCore.Engine.Durable/Aggregates/DurableWorkflowAggregate.cs`
- `src/OrcaCore.Engine.Durable/Execution/DurableCommandProcessor.cs`
- Spec: `docs/specs/06-requirements-durable-execution.md` DU-042
- Spec: `docs/specs/12-acceptance-criteria.md` AC-313

## Deliverables
- Update durable command/event contracts under `src/OrcaCore.Abstractions/Durable/`
- Update aggregate decision code in `src/OrcaCore.Engine.Durable/Aggregates/DurableWorkflowAggregate.cs`
- Update command processing in `src/OrcaCore.Engine.Durable/Execution/DurableCommandProcessor.cs`
- Add tests in `tests/OrcaCore.Engine.Durable.Tests/Continuations/ContinueAsNewAggregateTests.cs`

## Tests to write FIRST
In `tests/OrcaCore.Engine.Durable.Tests/Continuations/ContinueAsNewAggregateTests.cs`:
1. `ContinueAsNew_RunningInstance_EmitsRolloverAndCheckpointBaseline` - accepted rollover emits the expected event and checkpoint write.
2. `ContinueAsNew_PreservesLogicalIdentityAndIncrementsGeneration` - the logical instance stays stable while lineage/generation advances.
3. `ContinueAsNew_TerminalInstance_IsRejected` - terminal instances cannot roll over.

## Implementation notes
Keep the operation durable-only. Do not purge/archive old history in this task; only create
the facts and state needed by providers and projections. This is a corrective backfill for
an early Slice 2 requirement, not a Phase 6-only feature.

## Out of scope
Provider-specific persistence, retention policy, management query surface, and public
hosting APIs.

## Definition of done
- [ ] New tests are red before implementation and green after
- [ ] `dotnet test OrcaCore.slnx --filter "AC=AC-313|FullyQualifiedName~ContinueAsNewAggregateTests"` passes
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] PROGRESS.md updated; committed as "T6-04: continue-as-new contracts and aggregate (DU-042)"
