# T6-11: Add ZeroMQ dispatcher plugin

**Difficulty**: Sonnet        **Depends on**: T6-10
**Spec**: PR-015, DU-032, NF-021        **AC**: none

## Goal
Add a brokerless ZeroMQ dispatcher plugin over NetMQ. The plugin must implement
`IMessageDispatcher` and document the weaker delivery envelope compared with broker-backed
dispatchers.

## Read first
- `v3-gpt/src/OrcaCore.Abstractions/Providers/ProviderPorts.cs`
- `v3-gpt/src/OrcaCore.Abstractions/Providers/ProviderCommitContracts.cs`
- `v3-gpt/src/OrcaCore.Engine.Durable/Outbox/DurableOutboxPump.cs`
- `v3-gpt/Directory.Packages.props`
- `v3-gpt/OrcaCore.slnx`
- Spec: `docs/specs/10-provider-model-and-extensibility.md` PR-015
- Spec: `docs/specs/11-non-functional-requirements.md` NF-021

## Deliverables
- Add `v3-gpt/src/OrcaCore.Providers.ZeroMq/OrcaCore.Providers.ZeroMq.csproj`
- Add ZeroMQ dispatcher/options files under `v3-gpt/src/OrcaCore.Providers.ZeroMq/`
- Add `v3-gpt/tests/OrcaCore.Providers.ZeroMq.Tests/ZeroMqMessageDispatcherTests.cs`
- Add `v3-gpt/src/OrcaCore.Providers.ZeroMq/README.md`
- Update `v3-gpt/Directory.Packages.props` and `v3-gpt/OrcaCore.slnx`

## Tests to write FIRST
In `v3-gpt/tests/OrcaCore.Providers.ZeroMq.Tests/ZeroMqMessageDispatcherTests.cs`:
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
- [ ] `dotnet test v3-gpt/OrcaCore.slnx --filter FullyQualifiedName~ZeroMqMessageDispatcherTests` passes
- [ ] `dotnet build v3-gpt/OrcaCore.slnx` - zero warnings
- [ ] PROGRESS.md updated; committed as "T6-11: zeromq dispatcher plugin (PR-015)"
