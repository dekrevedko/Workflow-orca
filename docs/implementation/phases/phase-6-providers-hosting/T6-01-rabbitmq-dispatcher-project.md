# T6-01: Add RabbitMQ dispatcher project

**Difficulty**: Sonnet        **Depends on**: T6-00
**Spec**: PR-002, PR-015, DU-032        **AC**: none

## Goal
Add the RabbitMQ transport plugin as an `IMessageDispatcher` implementation. The adapter
must translate normalized outbox records into broker publishes and return explicit
`DispatchResult` values without leaking RabbitMQ concepts into workflow definitions.

## Read first
- `v3-gpt/src/OrcaCore.Abstractions/Providers/ProviderPorts.cs`
- `v3-gpt/src/OrcaCore.Abstractions/Providers/ProviderCommitContracts.cs`
- `v3-gpt/src/OrcaCore.Engine.Durable/Outbox/DurableOutboxPump.cs`
- `v3-gpt/Directory.Packages.props`
- `v3-gpt/OrcaCore.slnx`
- Spec: `docs/specs/10-provider-model-and-extensibility.md` section 10.2
- Spec: `docs/specs/06-requirements-durable-execution.md` DU-032

## Deliverables
- Add `v3-gpt/src/OrcaCore.Providers.RabbitMq/OrcaCore.Providers.RabbitMq.csproj`
- Add RabbitMQ dispatcher/options files under `v3-gpt/src/OrcaCore.Providers.RabbitMq/`
- Add `v3-gpt/tests/OrcaCore.Providers.RabbitMq.Tests/OrcaCore.Providers.RabbitMq.Tests.csproj`
- Add tests in `v3-gpt/tests/OrcaCore.Providers.RabbitMq.Tests/RabbitMqMessageDispatcherTests.cs`
- Update `v3-gpt/Directory.Packages.props` and `v3-gpt/OrcaCore.slnx`

## Tests to write FIRST
In `v3-gpt/tests/OrcaCore.Providers.RabbitMq.Tests/RabbitMqMessageDispatcherTests.cs`:
1. `DispatchAsync_PublishConfirmed_ReturnsSuccess` - a confirmed publish maps to `DispatchResult.Success`.
2. `DispatchAsync_TransientBrokerFailure_ReturnsRetryableFailure` - a transient publish failure maps to `DispatchResult.RetryableFailure`.
3. `DispatchAsync_UnroutablePermanentMessage_ReturnsPermanentFailure` - an unroutable permanent case maps to `DispatchResult.PermanentFailure`.

## Implementation notes
Use `RabbitMQ.Client` only inside the plugin project. Keep broker connection/channel
construction behind explicit options or internal factories so unit tests do not require a
broker. Do not change `IMessageDispatcher` unless the provider contract itself is reviewed.

## Out of scope
Testcontainers, dead-letter exchange assertions, hosting registration, and outbox pump
changes.

## Definition of done
- [ ] New tests are red before implementation and green after
- [ ] `dotnet test v3-gpt/OrcaCore.slnx --filter FullyQualifiedName~RabbitMqMessageDispatcherTests` passes
- [ ] `dotnet build v3-gpt/OrcaCore.slnx` - zero warnings
- [ ] PROGRESS.md updated; committed as "T6-01: rabbitmq dispatcher project (PR-015)"
