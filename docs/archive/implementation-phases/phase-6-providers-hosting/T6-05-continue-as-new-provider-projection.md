# T6-05: Backfill continue-as-new projection state

**Difficulty**: Sonnet        **Depends on**: T6-04
**Spec**: DU-042, DU-070        **AC**: AC-313

## Goal
Backfill continue-as-new visibility through durable metadata. Spec open question 10 is
resolved to Slice 2, so in-memory and PostgreSQL providers must persist rollover lineage as
part of the early durable contract even though this corrective task runs during Phase 6.

## Read first
- `src/OrcaCore.Abstractions/Instances/WorkflowInstanceSnapshot.cs`
- `src/OrcaCore.Engine.Durable/Management/DurableManagement.cs`
- `src/OrcaCore.Providers.InMemory/InMemoryWorkflowProvider.cs`
- `src/OrcaCore.Providers.PostgreSql/PostgreSqlWorkflowStore.cs`
- `tests/OrcaCore.ProviderCertification/EventStoreCertificationTests.cs`
- Spec: `docs/specs/06-requirements-durable-execution.md` DU-042 and DU-070
- Spec: `docs/specs/12-acceptance-criteria.md` AC-313

## Deliverables
- Update instance snapshot/projection contracts for rollover lineage
- Update in-memory and PostgreSQL projection persistence
- Add certification tests in `tests/OrcaCore.ProviderCertification/ContinueAsNewCertificationTests.cs`
- Add acceptance tests in `tests/OrcaCore.Acceptance.Tests/ContinueAsNewAcceptanceTests.cs`

## Tests to write FIRST
In `tests/OrcaCore.ProviderCertification/ContinueAsNewCertificationTests.cs`:
1. `ContinueAsNewProjection_PreservesLogicalIdentityAcrossRollover` - provider projections expose stable logical identity and advanced generation.

In `tests/OrcaCore.Acceptance.Tests/ContinueAsNewAcceptanceTests.cs`:
1. `ContinueAsNew_DurableInstance_RemainsQueryableByOriginalIdentity` - AC-313 public behavior is visible through management.

## Implementation notes
Provider schema changes must be additive. Keep query support metadata-only; do not deserialize
business payloads to answer continuity queries.

## Out of scope
Retention/archive deletion, package publishing, and non-PostgreSQL providers.

## Definition of done
- [ ] New tests are red before implementation and green after
- [ ] `dotnet test OrcaCore.slnx --filter "AC=AC-313|FullyQualifiedName~ContinueAsNewCertificationTests"` passes
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] PROGRESS.md updated; committed as "T6-05: continue-as-new provider projection (DU-042)"
