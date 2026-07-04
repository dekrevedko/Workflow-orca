# Code Quality Remediation Summary - 2026-07-04

Scope: `v3-gpt/src/**`, `v3-gpt/tests/**`, and targeted review documentation.

## Findings Fixed

- Production wall-clock statics were removed from `v3-gpt/src/**`.
  - Outbox default claims now use injected `TimeProvider` seams in the durable outbox pump and in-memory, PostgreSQL, and SQL Server providers.
  - Relational migration journal timestamps now use an optional `TimeProvider`.
  - `RepositoryGuardTests.ProductionSources_DoNotUseWallClockStatics` prevents regression.
- SQL Server provider registration parity was added through `AddOrcaCoreSqlServer`.
  - The extension registers the SQL Server workflow store against event, inbox, start-idempotency, outbox, projection, timer, retention, and resource-pool provider ports.
- SQL Server projection list/count/statistics filtering now uses SQL predicates instead of materializing all snapshots and filtering in memory.
  - `SqlServerProjectionQueryBuilder` centralizes provider-side predicates for instance id, parent/root id, definition, version, status, active-wait event name, and active-wait correlation.
  - SQL Server statistics now use SQL grouping and report provider pressure metrics.
- PostgreSQL and SQL Server workflow store facades were reduced below 1000 lines with real collaborators, not partial classes.
  - PostgreSQL now delegates projection, timer, and retention responsibilities to `PostgreSqlProjectionStore`, `PostgreSqlTimerScheduler`, and `PostgreSqlWorkflowRetentionStore`.
  - SQL Server now delegates projection and timer responsibilities to `SqlServerProjectionStore` and `SqlServerTimerScheduler`.
- `DurableWorkflowAggregate` was reduced below both the 1000-line hard threshold and the 500-line warning threshold with real internal modules.
  - Command decisions now route through concept-specific command handlers: lifecycle, waits/timers, child workflows, resource pools, external jobs, and saga.
  - Replay now routes through `DurableWorkflowReplayApplier`, which separates event-family replay for lifecycle, waits/timers, child workflows, resource pools, external jobs, and saga.
  - The aggregate remains the consistency root and facade for identity, stream version, snapshots, checkpoints, projection creation, and the existing `Decide*` surface.
- A durable runtime observer seam was added without introducing Rx or a broad diagnostics dependency.
  - `IWorkflowRuntimeObserver` receives command outcome observations for committed, conflict, eviction, poisoned, and no-op outcomes.
  - `DurableCommandProcessor` uses a null-object observer by default and isolates observer failures so diagnostics cannot corrupt committed workflow changes.
  - `AddOrcaCore` wires an optionally registered runtime observer into the durable command processor.

## Line-Budget Result

No `v3-gpt/src/**` production implementation file remains above the 1000-line hard threshold after the durable aggregate and relational provider decompositions.

`DurableWorkflowAggregate.cs` is now 478 lines. The earlier temporary waiver was removed because the aggregate was refactored into command decision handlers and replay-family appliers without using partial classes.

## 500-Line Triage

| File | Lines | Decision |
| --- | ---: | --- |
| `v3-gpt/src/OrcaCore.Providers.PostgreSql/PostgreSqlResourcePoolStore.cs` | 833 | Keep short-term; resource-pool behavior is cohesive but should be split into ticket, waiter, audit, and expiry collaborators if it grows. |
| `v3-gpt/src/OrcaCore.Providers.SqlServer/SqlServerResourcePoolStore.cs` | 783 | Keep short-term; same resource-pool split candidate as PostgreSQL. |
| `v3-gpt/src/OrcaCore.Engine.Ephemeral/Execution/WorkflowInstance.cs` | 752 | Keep short-term; cohesive in-memory runtime state object. Split only around lifecycle/history/active-work collections if behavior starts diverging. |
| `v3-gpt/src/OrcaCore.Providers.SqlServer/SqlServerWorkflowStore.cs` | 718 | Accepted after collaborator extraction; facade remains dense because it adapts all workflow provider ports. |
| `v3-gpt/src/OrcaCore.Providers.PostgreSql/PostgreSqlWorkflowStore.cs` | 700 | Accepted after collaborator extraction; facade remains the provider adapter over event, inbox, outbox, and checkpoint persistence. |
| `v3-gpt/src/OrcaCore.Providers.InMemory/InMemoryWorkflowProvider.cs` | 637 | Keep as reference provider; split only when a port-specific collaborator reduces duplication or test complexity. |
| `v3-gpt/src/OrcaCore.Core/Building/WorkflowBuilder.cs` | 637 | Keep as public fluent API surface; avoid splitting into noisy helper types unless validation/construction responsibilities expand. |
| `v3-gpt/src/OrcaCore.Engine.Ephemeral/Management/EphemeralManagement.cs` | 568 | Keep short-term; candidate for query/terminal-command collaborators if management grows. |
| `v3-gpt/src/OrcaCore.Engine.Ephemeral/EphemeralWorkflowEngine.cs` | 543 | Keep short-term; already delegates execution internals, and further splitting should follow runtime lifecycle responsibilities. |
| `v3-gpt/src/OrcaCore.Engine.Durable/Execution/DurableCommandProcessor.cs` | 538 | Accepted after commit-pipeline extraction and observer seam; remains the command facade while commit, materialization, aggregate loading, inbox preflight, and resource-pool side effects stay in collaborators. |
| `v3-gpt/src/OrcaCore.Abstractions/Durable/WorkflowEvent.cs` | 528 | Accepted contract exception; closed public event family is easier to audit together than scattered by event type. |

## Verification

- `dotnet build .\OrcaCore.slnx --no-restore` from `v3-gpt/`: passed after refactors, 0 warnings.
- `rg -n "DateTimeOffset\.(UtcNow|Now)|DateTime\.(UtcNow|Now)" v3-gpt\src`: no matches.
- `docker info --format '{{.ServerVersion}}'`: Docker available, server version 29.2.1.
- `dotnet test .\tests\OrcaCore.Core.Tests\OrcaCore.Core.Tests.csproj --no-build`: passed, 257 tests.
- `dotnet test .\tests\OrcaCore.Engine.Durable.Tests\OrcaCore.Engine.Durable.Tests.csproj --no-build`: passed, 176 tests.
- `dotnet test .\tests\OrcaCore.Engine.Ephemeral.Tests\OrcaCore.Engine.Ephemeral.Tests.csproj --no-build`: passed, 125 tests.
- `dotnet test .\tests\OrcaCore.Hosting.Tests\OrcaCore.Hosting.Tests.csproj --no-build`: passed, 10 tests.
- `dotnet test .\tests\OrcaCore.Providers.PostgreSql.Tests\OrcaCore.Providers.PostgreSql.Tests.csproj --no-build`: passed, 60 tests.
- `dotnet test .\tests\OrcaCore.Providers.SqlServer.Tests\OrcaCore.Providers.SqlServer.Tests.csproj --no-build`: passed, 46 tests.

## Remaining Work

- No remaining actionable item from `code-quality-rescan-2026-07-04.md` is blocked. Rx remains intentionally unintroduced because the remediation only needed a small observer seam.
