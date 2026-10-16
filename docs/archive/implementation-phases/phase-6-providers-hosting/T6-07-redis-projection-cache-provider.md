# T6-07: Add Redis projection and cache provider profile

**Difficulty**: Sonnet        **Depends on**: T6-06
**Spec**: PR-002, PR-013, PR-023        **AC**: none

## Goal
Add a Redis provider profile only for explicitly supported projection/cache roles. The
provider must not pretend to be a full durable event store unless the port invariants are
implemented and certified.

## Read first
- `src/OrcaCore.Abstractions/Providers/ProviderPorts.cs`
- `src/OrcaCore.Abstractions/Providers/ProviderCommitContracts.cs`
- `src/OrcaCore.Engine.Durable/Management/WorkflowInstanceQueryModel.cs`
- `tests/OrcaCore.ProviderCertification/EventStoreCertificationTests.cs`
- `Directory.Packages.props`
- Spec: `docs/specs/10-provider-model-and-extensibility.md` PR-013, PR-023, PR-024

## Deliverables
- Add `src/OrcaCore.Providers.Redis/OrcaCore.Providers.Redis.csproj`
- Add Redis projection/cache implementation files under `src/OrcaCore.Providers.Redis/`
- Add `tests/OrcaCore.Providers.Redis.Tests/OrcaCore.Providers.Redis.Tests.csproj`
- Add certification or integration tests only for implemented ports
- Add `src/OrcaCore.Providers.Redis/README.md`
- Update `Directory.Packages.props` and `OrcaCore.slnx`

## Tests to write FIRST
In `tests/OrcaCore.Providers.Redis.Tests/RedisProjectionProviderTests.cs`:
1. `ListAsync_MetadataFilters_ReturnsMatchingSnapshotsWithoutPayloadDeserialization` - projection queries use metadata.
2. `ProviderProfile_UnsupportedEventStorePort_IsNotRegistered` - Redis does not advertise unimplemented durable store capabilities.

## Implementation notes
Use `StackExchange.Redis` only inside the Redis plugin. The README must list implemented
ports and explicitly state non-goals.

## Out of scope
Full event-store semantics, package publishing, and replacing PostgreSQL as the reference
durable store.

## Definition of done
- [ ] New tests are red before implementation and green after
- [ ] `dotnet test OrcaCore.slnx --filter FullyQualifiedName~RedisProjectionProviderTests` passes
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] PROGRESS.md updated; committed as "T6-07: redis projection cache provider (PR-013)"
