# T6-06: Add archival and retention policy

**Difficulty**: Sonnet        **Depends on**: T6-05
**Spec**: DU-050, DU-051, PR-022        **AC**: AC-314

## Goal
Introduce retention policy as a first-class durable provider contract. Archive, purge,
active-memory eviction, and hard deletion must be separate concepts; purge/archive must
never remove active instances or break in-flight dispatch.

## Read first
- `src/OrcaCore.Abstractions/Providers/ProviderPorts.cs`
- `src/OrcaCore.Abstractions/Providers/PurgeResult.cs`
- `src/OrcaCore.Engine.Durable/Management/DurableManagement.cs`
- `src/OrcaCore.Providers.InMemory/InMemoryWorkflowProvider.cs`
- `src/OrcaCore.Providers.PostgreSql/PostgreSqlWorkflowStore.cs`
- Spec: `docs/specs/06-requirements-durable-execution.md` DU-050 and DU-051
- Spec: `docs/specs/12-acceptance-criteria.md` AC-314

## Deliverables
- Add retention/archive policy contracts under `src/OrcaCore.Abstractions/Providers/`
- Update in-memory and PostgreSQL retention behavior
- Add certification tests in `tests/OrcaCore.ProviderCertification/RetentionCertificationTests.cs`
- Add acceptance tests in `tests/OrcaCore.Acceptance.Tests/RetentionAcceptanceTests.cs`

## Tests to write FIRST
In `tests/OrcaCore.ProviderCertification/RetentionCertificationTests.cs`:
1. `ArchivePolicy_ActiveInstance_IsRejectedWithoutDeletingState` - active instances are preserved.
2. `PurgePolicy_InFlightOutbox_IsRejectedWithoutBreakingDispatch` - in-flight dispatch blocks purge.

In `tests/OrcaCore.Acceptance.Tests/RetentionAcceptanceTests.cs`:
1. `RetentionPolicy_TerminalInstance_CanBeArchivedAndNoLongerActive` - AC-314 behavior is visible from public management.

## Implementation notes
Use explicit retention policy objects, not raw date cutoffs on public APIs. Provider README
files must document retention behavior and history-window implications.

## Out of scope
Continue-as-new rollover, public package publishing, and provider-specific cold storage
tiers beyond a documented archive marker.

## Definition of done
- [ ] New tests are red before implementation and green after
- [ ] `dotnet test OrcaCore.slnx --filter "AC=AC-314|FullyQualifiedName~RetentionCertificationTests"` passes
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] PROGRESS.md updated; committed as "T6-06: archival retention policy (DU-050, DU-051)"
