# Code Quality Action Plan - 2026-07-03

Scope: `src/**` implementation review for code quality, readability,
SOLID, GoF fit, .NET 10/C# usage, and large-module risk.

Context:
- Dapper plus relational migrations are accepted implementation direction for
  relational providers. This report does not recommend reverting to raw SQL.
- The active concern is implementation locality: several modules have grown past
  the point where a maintainer can safely reason about one workflow capability
  without loading unrelated capabilities.
- Verification during review: `dotnet build .\OrcaCore.slnx --no-restore`
  passed from ``; `dotnet test
  .\tests\OrcaCore.Core.Tests\OrcaCore.Core.Tests.csproj --no-build` passed
  170 tests.

## Executive Summary

The codebase is already using the .NET 10 baseline correctly: `net10.0`,
nullable, analyzers, collection expressions, source-generated workflow-event
JSON, `Guid.CreateVersion7`, `TimeProvider` in most runtime paths, and
`System.Threading.Channels` for instance lanes.

The main quality risk is architectural, not syntax. Four production files are
currently over 1000 lines:

| File | Current size | Main risk |
| --- | ---: | --- |
| `src/OrcaCore.Engine.Durable/Aggregates/DurableWorkflowAggregate.cs` | 2603 lines | One aggregate owns decisions, replay, checkpoints, projections, waits, timers, child workflows, resource pools, external jobs, and saga state. |
| `src/OrcaCore.Providers.PostgreSql/PostgreSqlWorkflowStore.cs` | 1520 lines | One provider adapter owns event store, checkpoints, inbox, outbox, projections, timers, retention, SQL mapping, and query logic. |
| `src/OrcaCore.Providers.SqlServer/SqlServerWorkflowStore.cs` | 1229 lines | Same multi-port concentration as PostgreSQL, plus incomplete provider registration parity. |
| `src/OrcaCore.Engine.Durable/Execution/DurableCommandProcessor.cs` | 1006 lines | Command facade, lane execution, rehydration, commit-batch assembly, outbox materialization, timer scheduling, and resource-pool side effects are coupled. |

Treat these as refactoring targets before adding more durable/provider features.

Line-budget policy for this track:
- 500+ lines is a warning and refactor trigger for production implementation
  files.
- 1000+ lines is a hard finding unless an explicit temporary waiver exists.
- Public closed-family contract files can be excepted only if the grouping is
  documented and improves readability.

Pattern policy:
- Prefer GoF patterns only where they make an existing seam deeper or improve
  locality.
- Natural candidates in this codebase are Strategy for command/outbox
  materializers, Chain of Responsibility for command validation/commit steps,
  State for lifecycle-specific durable behavior, Observer for runtime
  observations, Adapter/Facade for provider registration, and Template Method
  for shared provider certification or migration flow.
- Do not add patterns as labels over pass-through wrappers.

## Action 1 - Split Durable Aggregate By Durable Capability

Priority: P2  
Target files:
- `src/OrcaCore.Engine.Durable/Aggregates/DurableWorkflowAggregate.cs:10`
- Supporting tests under `tests/OrcaCore.Engine.Durable.Tests/**`

Problem:
`DurableWorkflowAggregate` has one large state bag starting at line 12, command
decision methods starting around line 247, and a large replay `switch` starting
around line 1574. This violates single responsibility in practice: adding or
fixing saga, child-workflow, external-job, timer, wait, or projection behavior
requires editing the same file and understanding unrelated state.

Recommended design:
- Keep `DurableWorkflowAggregate` as the small facade and consistency root.
- Extract internal modules by durable capability:
  - `DurableLifecycleDecisions`
  - `DurableWaitTimerDecisions`
  - `DurableChildWorkflowDecisions`
  - `DurableResourcePoolDecisions`
  - `DurableExternalJobDecisions`
  - `DurableSagaDecisions`
  - `DurableWorkflowEventApplier`
  - `DurableProjectionWriter`
  - `DurableCheckpointMapper`
- Prefer internal sealed classes or static internal modules first. Do not create
  public interfaces unless there are two real adapters.

Concrete steps:
1. Extract pure mapping code first: checkpoint mapper and projection writer.
2. Extract the replay switch into event-applier methods grouped by event family.
3. Move decision methods one family at a time, preserving current method names
   on the facade as forwarding methods.
4. After each slice, run the affected durable aggregate/command tests.
5. Add a guard test once the file is below the agreed limit.

Verification:
- `dotnet test .\tests\OrcaCore.Engine.Durable.Tests\OrcaCore.Engine.Durable.Tests.csproj --no-build`
- Focus filters for aggregate areas touched, for example saga, timers, child
  workflow, external jobs, resource pools.
- Existing integration restart tests after any checkpoint/replay extraction.

Acceptance criteria:
- No production `.cs` file remains above 1000 lines without an explicit
  documented exception.
- Durable aggregate behavior remains unchanged under existing tests.
- Each extracted module has a name matching one durable concept, not a generic
  helper name.

## Action 2 - Decompose Relational Workflow Stores Behind The Existing Ports

Priority: P2  
Target files:
- `src/OrcaCore.Providers.PostgreSql/PostgreSqlWorkflowStore.cs:18`
- `src/OrcaCore.Providers.SqlServer/SqlServerWorkflowStore.cs:18`
- `src/OrcaCore.Providers.Relational/**`

Problem:
The public ports are good seams, but the concrete provider classes are still
large adapters that implement almost every port directly. This concentrates SQL,
transaction, serialization, projection, timer, retention, and outbox behavior in
one file per provider.

Recommended design:
- Keep `PostgreSqlWorkflowStore` and `SqlServerWorkflowStore` as public facade
  adapters if that is useful for DI and tests.
- Move implementation into provider-owned internal modules:
  - `<Provider>EventStreamStore`
  - `<Provider>CheckpointStore`
  - `<Provider>InboxStore`
  - `<Provider>StartIdempotencyStore`
  - `<Provider>OutboxStore`
  - `<Provider>ProjectionStore`
  - `<Provider>TimerScheduler`
  - `<Provider>RetentionStore`
- Add a small shared relational migration module that remains Dapper-backed.
- Keep provider-specific SQL in provider projects; keep shared SQL abstractions
  provider-neutral.

Concrete steps:
1. Extract read-only projection query code first; it has the smallest write risk.
2. Extract outbox claim/mark/release next; it has a clear port interface.
3. Extract timer scheduler and retention modules.
4. Leave `AppendAsync(ProviderCommitBatch, ...)` until last because it owns the
   atomic commit boundary.
5. Keep certification tests passing after each extraction.

Verification:
- Provider certification suites for PostgreSQL and SQL Server.
- `dotnet test .\tests\OrcaCore.Providers.PostgreSql.Tests\OrcaCore.Providers.PostgreSql.Tests.csproj --no-build`
- `dotnet test .\tests\OrcaCore.Providers.SqlServer.Tests\OrcaCore.Providers.SqlServer.Tests.csproj --no-build`
- Integration tests that cover restart, outbox, timer, and retention.

Acceptance criteria:
- The public provider facade is thin and delegates to concept-named internal
  modules.
- Atomic `ProviderCommitBatch` semantics remain unchanged.
- No new public provider port is introduced without a second adapter need.

## Action 3 - Split Durable Command Processing From Commit Materialization

Priority: P2  
Target file:
- `src/OrcaCore.Engine.Durable/Execution/DurableCommandProcessor.cs:11`

Problem:
`DurableCommandProcessor` is now both command facade and commit materializer. It
selects aggregate decisions, runs lanes, rehydrates state, builds
`ProviderCommitBatch`, creates inbox operations, creates outbox records, creates
timer schedules, handles resource-pool rollback/release, and maps command
results.

Recommended design:
- Keep `DurableCommandProcessor` as the command-facing facade.
- Extract:
  - `DurableCommandCommitPipeline` for load, rehydrate, decide, append, result.
  - `DurableCommitBatchFactory` for `ProviderCommitBatch`.
  - `DurableOutboxMaterializer` for lifecycle, child, residual, and external-job
    outbox records.
  - `DurableTimerScheduleMaterializer`.
  - `DurableResourcePoolCommitCoordinator` for acquire rollback and release.

Concrete steps:
1. Extract static materializers first; use direct tests around event-to-write
   mapping.
2. Extract commit-batch factory and keep processor behavior identical.
3. Extract resource-pool side-effect coordination after event materializers are
   isolated.
4. Add tests that cover a representative committed command still creates the
   same outbox/timer/inbox/projection writes.

Verification:
- Durable command pipeline tests.
- Outbox tests.
- Resource pool tests.
- Multi-node/restart integration tests if commit sequencing changes.

Acceptance criteria:
- Command overloads remain easy to scan.
- Commit materialization can be tested without invoking the whole processor.
- New durable event families do not require editing a 1000-line processor.

## Action 4 - Push SQL Server Projection Filtering Into SQL

Priority: P2  
Target file:
- `src/OrcaCore.Providers.SqlServer/SqlServerWorkflowStore.cs:587`

Problem:
SQL Server `ListCoreAsync` loads all instance projection rows, loads active
waits for all returned rows, and filters in memory via `Matches` at line 1200.
PostgreSQL already pushes equivalent filters into SQL. This creates provider
parity and scalability risk.

Recommended design:
- Add a SQL Server projection-query builder equivalent to PostgreSQL
  `AddProjectionQueryParameters`.
- Push `InstanceId`, parent/root, definition, version, status, active wait event
  name, and active wait correlation filters into SQL.
- Make `CountCoreAsync` execute `count(*)` in SQL instead of calling `ListCoreAsync`.

Concrete steps:
1. Add SQL text and parameters for projection filters.
2. Add provider tests that insert multiple projections and verify filtered list,
   count, wait filtering, and statistics.
3. Remove or reduce the in-memory `Matches` fallback after SQL coverage exists.

Verification:
- SQL Server projection tests.
- Provider certification tests that cover projection queries.
- Integration tests that use durable management queries on SQL Server.

Acceptance criteria:
- SQL Server query behavior matches PostgreSQL behavior.
- Count no longer materializes all snapshots.
- Active-wait filters are executed by SQL.

## Action 5 - Finish Deterministic Time Injection

Priority: P3  
Target files:
- `src/OrcaCore.Engine.Durable/Outbox/DurableOutboxPump.cs:23`
- `src/OrcaCore.Providers.PostgreSql/PostgreSqlWorkflowStore.cs:260`
- `src/OrcaCore.Providers.SqlServer/SqlServerWorkflowStore.cs:108`
- `src/OrcaCore.Providers.InMemory/InMemoryWorkflowProvider.cs:150`
- `src/OrcaCore.Providers.Relational/RelationalMigrationRunner.cs:50`

Problem:
Most runtime code uses `TimeProvider`, but a few production paths still use
`DateTimeOffset.UtcNow` for claim timestamps and migration journal timestamps.
That keeps lease behavior and tests partly tied to wall clock.

Recommended design:
- Inject `TimeProvider` into outbox pump and provider facades.
- Keep overloads that accept explicit `OutboxClaimRequest` for tests and
  advanced callers.
- For migration journal timestamps, pass `TimeProvider` into
  `RelationalMigrationRunner.ApplyAsync` or accept an explicit applied-at value.

Concrete steps:
1. Add optional `TimeProvider` constructor parameters defaulting to
   `TimeProvider.System`.
2. Use `timeProvider.GetUtcNow()` for default claim requests and migration
   journal writes.
3. Add guard coverage in `RepositoryGuardTests` for production wall-clock static
   usage, with deliberate exceptions only if documented.

Verification:
- Core repository guard tests.
- Outbox pump tests using fake time.
- Provider claim tests using deterministic `OutboxClaimRequest`.

Acceptance criteria:
- No production `DateTimeOffset.UtcNow` remains outside documented exceptions.
- Lease tests can run without wall-clock assumptions.

## Action 6 - Add SQL Server DI Registration Parity

Priority: P3  
Target project:
- `src/OrcaCore.Providers.SqlServer`

Problem:
PostgreSQL, Redis, and RabbitMQ expose `IServiceCollection` registration
extensions. SQL Server provider tests instantiate `SqlServerWorkflowStore`
directly, and no `AddOrcaCoreSqlServer` extension was found. This makes SQL
Server less usable as a plugin and increases composition drift.

Recommended design:
- Add `OrcaCoreSqlServerServiceCollectionExtensions` mirroring PostgreSQL.
- Register `SqlServerWorkflowStore` and map all implemented workflow ports.
- Register SQL Server resource-pool/retention behavior through the same concrete
  store or extracted modules after Action 2.

Concrete steps:
1. Add extension overload for connection string.
2. Register `IWorkflowEventStore`, `IWorkflowInboxStore`,
   `IWorkflowStartIdempotencyStore`, `IWorkflowOutboxStore`,
   `IWorkflowProjectionStore`, `ITimerScheduler`, `IWorkflowRetentionStore`, and
   `IResourcePoolStore`.
3. Add service-collection tests matching PostgreSQL's tests.

Verification:
- SQL Server provider service-collection tests.
- Hosting composition integration tests if SQL Server is wired through the host.

Acceptance criteria:
- SQL Server provider is explicitly registerable like other plugins.
- Resolved services point to the SQL Server provider adapter.

## Action 7 - Introduce A Workflow Runtime Observer Seam Before Adding Rx

Priority: P3  
Target areas:
- `src/OrcaCore.Engine.Ephemeral/Execution/WorkflowInstance.cs`
- `src/OrcaCore.Engine.Durable/Execution/DurableCommandProcessor.cs`
- `src/OrcaCore.Engine.Durable/Outbox/IOutboxPumpObserver.cs`
- `src/OrcaCore.Abstractions/Diagnostics/OrcaCoreDiagnostics.cs`

Problem:
The outbox pump has an observer seam, but lifecycle/runtime observations are
still produced in scattered code paths. Adding `System.Reactive` now would add a
dependency before the domain observation contract is clear.

Recommended design:
- Add a small observer seam first:
  - `IWorkflowRuntimeObserver`
  - `WorkflowRuntimeObservation`
  - observation kinds for lifecycle, command committed, command no-op, outbox
    materialized, wait registered/matched, timer scheduled/fired.
- Use a null-object observer by default.
- Bridge observer events to `ActivitySource`/`Meter` in hosting or diagnostics
  adapters.
- Consider Rx only if multiple consumers need stream composition, filtering, or
  backpressure beyond the observer contract.

Concrete steps:
1. Define observation records in the owning engine or diagnostics area.
2. Emit observations from existing lifecycle/outbox materialization points.
3. Keep observers non-blocking or explicitly async with cancellation.
4. Add tests that observer exceptions cannot corrupt workflow commits unless
   explicitly configured as fail-fast.

Verification:
- Durable outbox observer tests.
- Ephemeral lifecycle event tests.
- Hosting diagnostics tests if a bridge is added.

Acceptance criteria:
- Observability has one domain seam instead of scattered ad hoc calls.
- No Rx dependency is added until a concrete stream-composition use case exists.

## Action 8 - Split Dense Public Contract Files Or Document Exceptions

Priority: P3  
Target files:
- `src/OrcaCore.Abstractions/Durable/WorkflowEvent.cs:10`
- `src/OrcaCore.Abstractions/Durable/WorkflowCommand.cs:11`
- `src/OrcaCore.Abstractions/Providers/ProviderCommitContracts.cs:32`

Problem:
The contract files contain many public types: 40 workflow-event types, 20 command
types, and 22 provider commit/query types. This can be acceptable for closed
families, but it conflicts with "one public type per file" unless the exception
is intentional and documented.

Recommended design:
- Either split by concept folder:
  - `Durable/Events/Lifecycle`
  - `Durable/Events/Timers`
  - `Durable/Events/Children`
  - `Durable/Events/Saga`
  - `Durable/Commands/...`
  - `Providers/Commit`, `Providers/Projection`, `Providers/Outbox`
- Or document an explicit closed-family exception in
  `docs/implementation/02-engineering-conventions.md`.

Concrete steps:
1. Decide whether closed-family grouping is preferred for these contracts.
2. If splitting, keep namespaces stable to avoid public API churn.
3. If documenting an exception, add guard tests so arbitrary new public types do
   not accumulate in unrelated files.

Verification:
- Full build.
- Public API contract tests if available.
- Serialization tests for workflow event type mapping.

Acceptance criteria:
- Maintainers can locate command/event/provider contract types by concept.
- The layout rule is either followed or explicitly excepted.

## Guardrails To Add After The First Refactor Slice

Add guard tests after the first large-file decomposition so the codebase does
not regress.

Suggested tests in `tests/OrcaCore.Core.Tests/RepositoryGuardTests.cs`:

1. `ProductionSources_DoNotExceedLineBudget`
   - Warn or report when a production implementation file exceeds 500 lines.
   - Fail when a production implementation file exceeds 1000 lines.
   - Allow a temporary waiver list only while the first decomposition work is in
     flight.
   - Treat public closed-family contract files separately: either split them by
     concept or require an explicit documented exception.

2. `ProductionSources_DoNotUseWallClockStatics`
   - Fail on `DateTimeOffset.UtcNow`, `DateTime.UtcNow`, `DateTimeOffset.Now`,
     and `DateTime.Now` under `src`.
   - Allow explicitly documented migration exceptions only if Action 5 is not
     done yet.

3. `SqlServerProvider_HasServiceCollectionRegistration`
   - Fail if SQL Server has no `AddOrcaCoreSqlServer` registration extension.

4. `ProviderStores_DoNotImplementTooManyPortsDirectly`
   - Warn or fail when one concrete provider class directly implements more
     than a small agreed number of provider ports after Action 2.

## Recommended Order

1. Action 5 - deterministic time injection. Small, low-risk, gives a useful
   guardrail.
2. Action 6 - SQL Server DI registration parity. Small, improves plugin shape.
3. Action 4 - SQL Server projection pushdown. Clear behavior/performance win.
4. Action 3 - command processor decomposition. Reduces durable commit coupling.
5. Action 1 - durable aggregate decomposition. Highest architectural payoff.
6. Action 2 - relational provider decomposition. Larger follow-up once tests are
   green and SQL behavior is stable.
7. Action 7 - observer seam. Best after command/outbox materialization is no
   longer buried.
8. Action 8 - public contract layout decision. Can be done anytime, but avoid
   mixing it with behavioral refactors.

## Definition Of Done For This Quality Track

- No production implementation file exceeds 1000 lines, or every exception is
  explicitly documented with a reason and owner.
- Production implementation files over 500 lines are either actively scheduled
  for decomposition or documented as cohesive enough to keep.
- Durable aggregate decisions, replay, checkpoint mapping, and projection
  writing have separate internal modules.
- Relational providers expose thin public facades with concept-named internal
  modules.
- SQL Server projection queries are provider-side, not in-memory scans.
- Runtime time sources use `TimeProvider` instead of wall-clock statics.
- SQL Server has plugin registration parity.
- Observer/diagnostics behavior has a single domain seam.
- GoF patterns used in follow-up refactors are justified by reduced coupling,
  better locality, or a smaller interface; no pass-through pattern wrappers are
  introduced for their own sake.
- Repository guard tests prevent reintroducing the same design drift.
