# T4-09: Add child WhenAny residuals and mixed outbox records

**Difficulty**: Sonnet        **Depends on**: T4-08
**Spec**: CP-021, CP-024, DU-033        **AC**: AC-612, AC-613

## Goal
Complete durable child `WhenAny` residual handling and verify unified outbox mixed record
kinds. Residual cancellation intent must be recorded before parent resume.

## Read first
- `src/OrcaCore.Engine.Durable/Aggregates/DurableWorkflowAggregate.cs`
- `src/OrcaCore.Engine.Durable/Execution/DurableCommandProcessor.cs`
- `src/OrcaCore.Engine.Durable/Outbox/DurableOutboxPump.cs`
- `src/OrcaCore.Abstractions/Providers/ProviderCommitContracts.cs`
- `tests/OrcaCore.Engine.Durable.Tests/Outbox/DurableOutboxTests.cs`
- Spec: `docs/specs/08-requirements-composition.md`
- Spec: `docs/specs/12-acceptance-criteria.md`

## Deliverables
- Durable residual policy state: `CancelRemaining`, `LetRemainingComplete`, `DetachRemaining`
- Residual intent event before parent resume emission
- Unified outbox mixed child-start and external-message records

## Tests to write FIRST
In `tests/OrcaCore.Engine.Durable.Tests/Composition/ChildResidualPolicyTests.cs`:
1. `[Trait("AC","AC-612")] WhenAny_CancelRemaining_RecordsResidualBeforeResume`
2. `[Trait("AC","AC-613")] UnifiedOutbox_CarriesChildStartAndExternalRecords`
In `tests/OrcaCore.Acceptance.Tests/ChildWorkflowAcceptanceTests.cs`:
3. `[Trait("AC","AC-612")] RunChildren_WhenAnyResidualIsDurableBeforeParentResume`
4. `[Trait("AC","AC-613")] RunChildren_OutboxSupportsMixedKinds`

## Implementation notes
Detached children remain queryable by lineage. Cancellation is intent plus child command, not
best-effort in-memory cancellation.

## Out of scope
Saga compensation and durable pool tickets.

## Definition of done
- [ ] New tests fail before implementation and pass after
- [ ] `dotnet test OrcaCore.slnx --filter "ChildResidualPolicy|AC=AC-612|AC=AC-613"` passes
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] Phase 4 exit AC list AC-601..615 green, except AC-616
- [ ] PROGRESS.md updated; committed as "T4-09: child residual policies and mixed outbox (CP-021, AC-612, AC-613)"
