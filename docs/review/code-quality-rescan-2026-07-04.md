# Code Quality Rescan - 2026-07-04

Scope: `src/**` production C# files, excluding `bin/` and `obj/`.

This is a delta review after the latest code update. The review accepts the
current Dapper plus migrations direction for relational setup; it does not
recommend reverting that choice. The remaining concerns are about locality,
testable seams, and provider parity.

## Verification

- `dotnet build .\OrcaCore.slnx --no-restore` from ``: passed, 0 warnings.
- `dotnet test .\tests\OrcaCore.Core.Tests\OrcaCore.Core.Tests.csproj --no-build`: passed, 254 tests.
- `dotnet test .\tests\OrcaCore.Engine.Durable.Tests\OrcaCore.Engine.Durable.Tests.csproj --no-build`: passed, 172 tests.
- `dotnet test .\tests\OrcaCore.Engine.Ephemeral.Tests\OrcaCore.Engine.Ephemeral.Tests.csproj --no-build`: passed, 125 tests.

## Current Size Baseline

Production source count: 178 C# files.

| Bucket | Count |
| --- | ---: |
| `<500` lines | 167 |
| `500-999` lines | 8 |
| `1000+` lines | 3 |

Current large files:

| File | Lines | Status |
| --- | ---: | --- |
| `src/OrcaCore.Engine.Durable/Aggregates/DurableWorkflowAggregate.cs` | 1714 | hard finding |
| `src/OrcaCore.Providers.PostgreSql/PostgreSqlWorkflowStore.cs` | 1405 | hard finding |
| `src/OrcaCore.Providers.SqlServer/SqlServerWorkflowStore.cs` | 1167 | hard finding |
| `src/OrcaCore.Providers.PostgreSql/PostgreSqlResourcePoolStore.cs` | 833 | warning |
| `src/OrcaCore.Providers.SqlServer/SqlServerResourcePoolStore.cs` | 783 | warning |
| `src/OrcaCore.Engine.Ephemeral/Execution/WorkflowInstance.cs` | 752 | warning |
| `src/OrcaCore.Core/Building/WorkflowBuilder.cs` | 637 | warning |
| `src/OrcaCore.Providers.InMemory/InMemoryWorkflowProvider.cs` | 629 | warning |
| `src/OrcaCore.Engine.Ephemeral/Management/EphemeralManagement.cs` | 568 | warning |
| `src/OrcaCore.Engine.Ephemeral/EphemeralWorkflowEngine.cs` | 543 | warning |
| `src/OrcaCore.Abstractions/Durable/WorkflowEvent.cs` | 528 | warning or documented contract exception |

## What Improved

- `DurableCommandProcessor` is no longer a hard-size finding. It is now 472
  lines and delegates commit concerns to `DurableCommitPipeline`,
  `DurableCommitMaterializer`, `DurableCheckpointMapper`,
  `DurableAggregateLoader`, and `DurableResourcePoolCommitEffects`.
- `DurableCommitPipeline` is a good extraction. It gives commit/no-mutation/
  inbox-only paths a small, testable home without introducing a public interface
  too early.
- `DurableWorkflowAggregate` now delegates capability state to
  `DurableChildWorkflowState`, `DurableExternalJobState`,
  `DurableResourcePoolState`, `DurableTimerState`, `DurableWaitState`, and
  `DurableSagaState` at `DurableWorkflowAggregate.cs:11`.
- `DurableChildWorkflowState` is now 498 lines, just under the warning
  threshold. It is still worth watching, but it is no longer an immediate
  line-budget finding.
- In-memory provider locking now uses `System.Threading.Lock`, which is the
  right .NET 10-era primitive for private synchronous critical sections.
- Relational migrations are explicit. `RelationalMigrationRunner` uses Dapper
  for migration journal reads/writes, and both PostgreSQL and SQL Server have
  versioned migration SQL files.

## Remaining Findings

### [P2] Durable aggregate is still a god aggregate - `src/OrcaCore.Engine.Durable/Aggregates/DurableWorkflowAggregate.cs:9`

Evidence:
- The aggregate remains 1714 lines.
- It owns the root state plus six capability state modules at
  `DurableWorkflowAggregate.cs:11`.
- Replay still centralizes event dispatch in one large `switch` starting at
  `DurableWorkflowAggregate.cs:1327`.

Recommendation:
- Keep `DurableWorkflowAggregate` as the consistency root and facade.
- Extract replay into event-family appliers, for example lifecycle, waits,
  timers, child workflows, resource pools, external jobs, and saga.
- Move support records and mapping helpers out of the aggregate file.
- Use State where the behavior is genuinely lifecycle/capability-specific, and
  use small Strategy-style event appliers only where they remove switch growth.

Acceptance target:
- Bring the aggregate file below 1000 lines first.
- Then decide whether the remaining facade is cohesive enough to keep above 500
  with a documented temporary exception.

### [P2] Relational workflow stores remain oversized provider facades - `src/OrcaCore.Providers.PostgreSql/PostgreSqlWorkflowStore.cs:18`, `src/OrcaCore.Providers.SqlServer/SqlServerWorkflowStore.cs:18`

Evidence:
- PostgreSQL workflow store is 1405 lines.
- SQL Server workflow store is 1167 lines.
- Store classes still mix event store, checkpoint, inbox, outbox, timer,
  projection, retention, SQL mapping, and serialization concerns.
- Dapper is currently concentrated in `RelationalMigrationRunner`; the workflow
  stores still mostly use provider-specific command APIs directly.

Recommendation:
- Keep public provider classes as Adapter/Facade entry points.
- Move implementation behind concept-named internal modules:
  `<Provider>EventStreamStore`, `<Provider>CheckpointStore`,
  `<Provider>InboxStore`, `<Provider>OutboxStore`,
  `<Provider>ProjectionStore`, `<Provider>TimerScheduler`, and
  `<Provider>RetentionStore`.
- Apply Dapper consistently where it simplifies query/parameter mapping, but do
  not force abstraction over provider-specific SQL dialect.

Acceptance target:
- No provider workflow store above 1000 lines.
- Commit atomicity around `ProviderCommitBatch` remains covered by provider
  certification tests.

### [P2] SQL Server projection queries still materialize and filter in memory - `src/OrcaCore.Providers.SqlServer/SqlServerWorkflowStore.cs:587`

Evidence:
- `ListCoreAsync` selects all instance projections ordered by instance id.
- It loads active waits for the materialized snapshots at
  `SqlServerWorkflowStore.cs:627`.
- It filters with `.Where(snapshot => Matches(snapshot, query))` at
  `SqlServerWorkflowStore.cs:634`.
- `CountCoreAsync` calls `ListCoreAsync` and returns `snapshots.Count` at
  `SqlServerWorkflowStore.cs:639`.

Recommendation:
- Extract a SQL Server projection-query builder.
- Push instance id, parent/root, definition, version, status, active-wait event
  name, and active-wait correlation filters into SQL.
- Make count execute a SQL `count(*)`.
- Add provider tests for filtered list, count, wait-filtered list, and
  statistics parity.

Natural pattern fit:
- Strategy is a clean fit if PostgreSQL and SQL Server each own a dialect
  projection query strategy behind the same provider-facing behavior.

### [P3] Deterministic time injection is still incomplete - multiple files

Remaining wall-clock statics:

- `src/OrcaCore.Providers.SqlServer/SqlServerWorkflowStore.cs:108`
- `src/OrcaCore.Engine.Durable/Outbox/DurableOutboxPump.cs:23`
- `src/OrcaCore.Providers.PostgreSql/PostgreSqlWorkflowStore.cs:267`
- `src/OrcaCore.Providers.Relational/RelationalMigrationRunner.cs:50`
- `src/OrcaCore.Providers.InMemory/InMemoryWorkflowProvider.cs:150`

Recommendation:
- Inject optional `TimeProvider` with `TimeProvider.System` defaults.
- Use `timeProvider.GetUtcNow()` for outbox claim defaults and migration journal
  timestamps.
- Add a guard test for production wall-clock static usage.

### [P3] SQL Server still lacks service registration parity - `src/OrcaCore.Providers.SqlServer/SqlServerWorkflowStore.cs:18`

Evidence:
- Search found no `AddOrcaCoreSqlServer` service registration extension.
- PostgreSQL already has `OrcaCorePostgreSqlServiceCollectionExtensions`.

Recommendation:
- Add `OrcaCoreSqlServerServiceCollectionExtensions`.
- Register the SQL Server provider facade against the workflow provider ports it
  implements.
- Add service-collection tests mirroring PostgreSQL provider tests.

Natural pattern fit:
- Adapter/Facade is the right frame here. The public registration extension
  should hide provider construction details from hosting code.

### [P3] 500-line warnings need triage, not blanket refactors - current 500-999 list

The 500 threshold is useful, but it should not become noise. These files need
triage:

- `PostgreSqlResourcePoolStore` and `SqlServerResourcePoolStore`: likely split
  with the relational provider decomposition work.
- `WorkflowInstance`, `EphemeralWorkflowEngine`, and `EphemeralManagement`:
  inspect for cohesive runtime vs management responsibilities before splitting.
- `WorkflowBuilder`: probably acceptable short-term if most of the size is
  public fluent API, but add a convention note or split validation/construction
  helpers if it grows.
- `InMemoryWorkflowProvider`: reference provider can stay denser than adapters,
  but keep it below 1000 and watch for port-specific extraction points.
- `WorkflowEvent`: either split by event family or document a closed-family
  public contract exception.

## GoF Pattern Guidance After This Rescan

- Strategy: natural for provider projection query builders, outbox
  materializers by durable event family, and possibly serialization/mapping
  differences between relational providers.
- Chain of Responsibility: natural only for command preflight/commit stages with
  independent exit points, such as idempotency, rehydrate, decide, commit,
  rollback/release effects. Keep the current `DurableCommitPipeline` simple
  until branching complexity justifies a chain.
- State: natural inside durable capability modules where behavior changes by
  workflow lifecycle, child group state, saga progress, wait/timer state, or
  resource-ticket state.
- Observer: add a small `IWorkflowRuntimeObserver` domain seam before adding
  Reactive/Rx. Bridge it to diagnostics or Rx only when stream composition is a
  proven need.
- Adapter/Facade: keep provider public classes and service registration as thin
  facades over internal stores.
- Template Method: use cautiously for common relational migration/test flows.
  Prefer composition over inheritance for provider stores unless the repeated
  algorithm is stable and provider-specific steps are obvious.

## Updated Recommended Order

1. Finish TimeProvider injection and add the guard test.
2. Add SQL Server service registration parity.
3. Push SQL Server projection filtering/counting into SQL.
4. Continue durable aggregate decomposition, starting with replay appliers and
   support-record moves.
5. Decompose relational workflow stores and resource-pool stores by provider
   port.
6. Triage the remaining 500-999 files and document intentional exceptions.
7. Add a runtime observer seam only after the commit/materialization paths are
   settled.

## Verdict

The latest update meaningfully improves durable execution locality. I would not
rerun the whole review from scratch yet; the remaining work is targeted. The
hard blockers for this quality track are now three files above 1000 lines plus
the SQL Server projection and registration parity gaps.
