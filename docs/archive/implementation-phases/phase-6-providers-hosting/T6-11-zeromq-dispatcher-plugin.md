# T6-11: Add ZeroMQ dispatcher plugin

**Difficulty**: Sonnet        **Depends on**: T6-10
**Spec**: PR-015, DU-032, NF-021        **AC**: none

## Goal
Add a brokerless ZeroMQ dispatcher plugin over NetMQ. The plugin must implement
`IMessageDispatcher` and document the weaker delivery envelope compared with broker-backed
dispatchers.

## Read first
- `src/OrcaCore.Abstractions/Providers/ProviderPorts.cs`
- `src/OrcaCore.Abstractions/Providers/ProviderCommitContracts.cs`
- `src/OrcaCore.Engine.Durable/Outbox/DurableOutboxPump.cs`
- `Directory.Packages.props`
- `OrcaCore.slnx`
- Spec: `docs/specs/10-provider-model-and-extensibility.md` PR-015
- Spec: `docs/specs/11-non-functional-requirements.md` NF-021

## Deliverables
- Add `src/OrcaCore.Providers.ZeroMq/OrcaCore.Providers.ZeroMq.csproj`
- Add ZeroMQ dispatcher/options files under `src/OrcaCore.Providers.ZeroMq/`
- Add `tests/OrcaCore.Providers.ZeroMq.Tests/ZeroMqMessageDispatcherTests.cs`
- Add `src/OrcaCore.Providers.ZeroMq/README.md`
- Update `Directory.Packages.props` and `OrcaCore.slnx`

## Tests to write FIRST
In `tests/OrcaCore.Providers.ZeroMq.Tests/ZeroMqMessageDispatcherTests.cs`:
1. `DispatchAsync_PeerAcceptsMessage_ReturnsSuccess` - accepted send maps to success.
2. `DispatchAsync_PeerUnavailable_ReturnsRetryableFailure` - unavailable peer maps to retry.
3. `Readme_DocumentsBrokerlessDeliveryEnvelope` - README states at-least-once outbox intent and weaker brokerless guarantees.

## Implementation notes
Use NetMQ only inside the plugin. Do not claim durable broker semantics, publisher confirms,
or dead-letter support if the adapter cannot provide them.

## Out of scope
RabbitMQ, consumer ingestion, exactly-once delivery, and provider persistence.

## Definition of done
- [ ] New tests are red before implementation and green after
- [ ] `dotnet test OrcaCore.slnx --filter FullyQualifiedName~ZeroMqMessageDispatcherTests` passes
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] PROGRESS.md updated; committed as "T6-11: zeromq dispatcher plugin (PR-015)"
