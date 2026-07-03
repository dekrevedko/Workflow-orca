# T6-08: Add SQL Server event-store slice

**Difficulty**: Sonnet        **Depends on**: T6-07
**Spec**: PR-010, PR-011, PR-012, PR-020, PR-021        **AC**: AC-305, AC-309, AC-310

## Goal
Add the SQL Server provider project and implement the atomic event/checkpoint/inbox/outbox
commit path. This task proves the core durable mutation invariants before adding projections,
timers, and resource pools.

## Read first
- `v3-gpt/src/OrcaCore.Abstractions/Providers/ProviderPorts.cs`
- `v3-gpt/src/OrcaCore.Abstractions/Providers/ProviderCommitContracts.cs`
- `v3-gpt/src/OrcaCore.Providers.PostgreSql/PostgreSqlWorkflowStore.cs`
- `v3-gpt/tests/OrcaCore.ProviderCertification/EventStoreCertificationTests.cs`
- `v3-gpt/Directory.Packages.props`
- Spec: `docs/specs/10-provider-model-and-extensibility.md` PR-010 through PR-024
- Spec: `docs/specs/12-acceptance-criteria.md` AC-305, AC-309, AC-310

## Deliverables
- Add `v3-gpt/src/OrcaCore.Providers.SqlServer/OrcaCore.Providers.SqlServer.csproj`
- Add SQL Server event/checkpoint/inbox/outbox store files
- Add `v3-gpt/tests/OrcaCore.Providers.SqlServer.Tests/OrcaCore.Providers.SqlServer.Tests.csproj`
- Add SQL Server certification fixture for event store tests
- Update `v3-gpt/Directory.Packages.props` and `v3-gpt/OrcaCore.slnx`

## Tests to write FIRST
In `v3-gpt/tests/OrcaCore.Providers.SqlServer.Tests/SqlServerProviderCertificationTests.cs`:
1. Inherit the provider certification event-store tests and run them against SQL Server.

In `v3-gpt/tests/OrcaCore.Providers.SqlServer.Tests/SqlServerEventStoreTests.cs`:
1. `AppendAsync_ProjectionCommitFails_RollsBackEventsInboxAndOutbox` - the SQL transaction preserves PR-020.

## Implementation notes
Use raw `Microsoft.Data.SqlClient`; no ORM and no Dapper. Keep schema provider-owned and
definition-agnostic, mirroring the PostgreSQL contract rather than copying provider-specific
SQL blindly.

## Out of scope
Projection queries, timers, resource pools, archival policy, and hosting registration.

## Definition of done
- [ ] New tests are red before implementation and green after
- [ ] `dotnet test v3-gpt/OrcaCore.slnx --filter FullyQualifiedName~SqlServerProviderCertificationTests` passes when containers are enabled
- [ ] `dotnet build v3-gpt/OrcaCore.slnx` - zero warnings
- [ ] PROGRESS.md updated; committed as "T6-08: sql server event store slice (PR-010)"
