# T6-03: Add hosting registration package

> **Superseded historical task (2026-07-19):** do not execute the signatures, project ownership,
> or toggles below. Current authority is
> [document 17](../../../specs/17-selected-mode-capability-matrix.md#175-package-and-integration-boundary)
> plus reshape tasks 3.7 and 7.10. V1 has no `OrcaCore.Hosting` PackageId, catch-all registration,
> provider default, or separate hosted-service toggle; hosting entry points belong to their exact
> engine/provider/DAG assemblies.

**Difficulty**: Haiku        **Depends on**: T6-02
**Spec**: PR-040        **AC**: none

## Goal
This file records the pre-simplification hosting proposal only. New implementation follows the
role-specific `AddOrcaCoreEphemeralEngine`, `AddOrcaCoreDurableEngine`, callback-only
`AddOrcaCoreDurableEventIngress`, `AddOrcaCoreInMemoryDurableProvider`, production
`AddOrcaCorePostgreSqlDurableProvider`, and `AddOrcaCoreDag` contracts in the current authority.

## Read first
- `src/OrcaCore.Hosting/OrcaCore.Hosting.csproj`
- `src/OrcaCore.Engine.Durable/Outbox/DurableOutboxPump.cs`
- `src/OrcaCore.Engine.Ephemeral/Timers/EphemeralTimerService.cs`
- `src/OrcaCore.Providers.InMemory/InMemoryWorkflowProvider.cs`
- `src/OrcaCore.Engine.Ephemeral/EphemeralWorkflowEngineOptions.cs`
- Spec: `docs/specs/10-provider-model-and-extensibility.md` PR-040

## Deliverables
- Add hosting extension classes under `src/OrcaCore.Hosting/`
- Add hosted-service adapters under `src/OrcaCore.Hosting/Services/`
- Add `tests/OrcaCore.Hosting.Tests/OrcaCoreHostingServiceCollectionTests.cs`
- Update `OrcaCore.slnx` if a new hosting test project is needed

## Tests to write FIRST
In `tests/OrcaCore.Hosting.Tests/OrcaCoreHostingServiceCollectionTests.cs`:
1. Replace this historical test list with the exact role-specific registration fixtures required by
   reshape tasks 3.7 and 7.10.
2. Prove PostgreSQL supplies one complete production durable role and in-memory remains development/test.
3. Prove registration copies and validates programmatic options, includes each role's required hosted
   loops, rejects mixed/conflicting roles, and exposes no catch-all or separate hosted-service toggle.

## Implementation notes
Use Microsoft DI abstractions only. Keep registration explicit; do not add assembly scanning,
a third-party container, configuration-binder facade, catch-all method, or implicit provider/default.

## Out of scope
ASP.NET endpoints, package publishing, provider implementation changes, and sample apps.

## Definition of done
- [ ] New tests are red before implementation and green after
- [ ] `dotnet test OrcaCore.slnx --filter FullyQualifiedName~OrcaCoreHostingServiceCollectionTests` passes
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] PROGRESS.md updated; committed as "T6-03: hosting registration package (PR-040)"
