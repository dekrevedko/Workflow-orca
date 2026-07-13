# T6-03: Add hosting registration package

**Difficulty**: Haiku        **Depends on**: T6-02
**Spec**: PR-040        **AC**: none

## Goal
Fill the existing `OrcaCore.Hosting` project with explicit Microsoft DI registration
extensions. Hosts should be able to call `AddOrcaCore()` and provider-specific extension
methods to wire engines, in-memory defaults, outbox pumping, timer scheduling, and
operational sweeps.

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
1. `AddOrcaCore_RegistersCoreEnginesAndInMemoryDefaults` - the service collection resolves core engines with in-memory defaults.
2. `AddOrcaCoreRabbitMq_RegistersDispatcherWithoutScanningAssemblies` - provider registration is explicit and does not rely on reflection scanning.
3. `AddOrcaCoreHostedServices_RegistersPumpTimerAndSweepServices` - hosted services are registered only when requested.

## Implementation notes
Use Microsoft DI abstractions only. Keep registration explicit; do not add assembly scanning
or a third-party container. Hosted services may be thin adapters over existing engine
services.

## Out of scope
ASP.NET endpoints, package publishing, provider implementation changes, and sample apps.

## Definition of done
- [ ] New tests are red before implementation and green after
- [ ] `dotnet test OrcaCore.slnx --filter FullyQualifiedName~OrcaCoreHostingServiceCollectionTests` passes
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] PROGRESS.md updated; committed as "T6-03: hosting registration package (PR-040)"
