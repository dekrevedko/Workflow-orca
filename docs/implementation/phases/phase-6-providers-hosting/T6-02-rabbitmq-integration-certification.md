# T6-02: Certify RabbitMQ dispatcher integration

**Difficulty**: Sonnet        **Depends on**: T6-01
**Spec**: PR-015, DU-032        **AC**: none

## Goal
Prove the RabbitMQ dispatcher against a real broker. Integration tests must cover publisher
confirms, retryable broker failures, and permanent poison/dead-letter behavior through the
normalized dispatch contract.

## Read first
- `src/OrcaCore.Providers.RabbitMq/OrcaCore.Providers.RabbitMq.csproj`
- `src/OrcaCore.Providers.RabbitMq/RabbitMqMessageDispatcher.cs`
- `tests/OrcaCore.Providers.RabbitMq.Tests/RabbitMqMessageDispatcherTests.cs`
- `Directory.Packages.props`
- `OrcaCore.slnx`
- Spec: `docs/specs/10-provider-model-and-extensibility.md` PR-015 and PR-024
- Spec: `docs/specs/06-requirements-durable-execution.md` DU-032

## Deliverables
- Add RabbitMQ Testcontainers package version to `Directory.Packages.props`
- Add integration tests in `tests/OrcaCore.Providers.RabbitMq.Tests/RabbitMqDispatcherIntegrationTests.cs`
- Add a plugin README at `src/OrcaCore.Providers.RabbitMq/README.md`

## Tests to write FIRST
In `tests/OrcaCore.Providers.RabbitMq.Tests/RabbitMqDispatcherIntegrationTests.cs`:
1. `DispatchAsync_RealBrokerConfirmedPublish_DeliversMessage` - a confirmed publish reaches the configured exchange/queue.
2. `DispatchAsync_BrokerUnavailable_ReturnsRetryableFailure` - an unavailable broker produces a retryable outcome.
3. `DispatchAsync_PermanentRoutingFailure_IsDocumentedAndReturned` - permanent routing failure is surfaced and documented.

## Implementation notes
Honor the repository's container skip convention if one exists for provider tests. The README
must state the delivery envelope: at-least-once dispatch, publisher confirms, retryable vs
permanent outcomes, and no exactly-once delivery claim.

## Out of scope
Hosting registration, RabbitMQ consumer ingestion, Kubernetes adapters, and workflow
definition APIs.

## Definition of done
- [ ] New integration tests are red before implementation and green after
- [ ] `dotnet test OrcaCore.slnx --filter FullyQualifiedName~RabbitMqDispatcherIntegrationTests` passes when containers are enabled
- [ ] `dotnet build OrcaCore.slnx` - zero warnings
- [ ] PROGRESS.md updated; committed as "T6-02: certify rabbitmq dispatcher integration (PR-015)"
