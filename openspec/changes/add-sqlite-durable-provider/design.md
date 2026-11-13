## Context

Status: **Proposed, 2026-10-29; implementation pending explicit approval.** This document describes a future implementation. No source, dependency, executable API baseline, canonical specification, or existing task is activated by creating it.

The durable host currently selects one complete provider role. The PostgreSQL and SQL Server registrations bind ten ports: `IWorkflowEventStore`, `IWorkflowInboxStore`, `IWorkflowStartIdempotencyStore`, `IWorkflowOutboxStore`, `IWorkflowProjectionStore`, `IWorkflowOperationalStore`, `IWorkflowProviderMaintenanceStore`, `ITimerScheduler`, `IResourcePoolStore`, and `IDurableResourceGovernanceStore`. The provider is responsible for atomic ownership, exact protocol storage, and recovery; the engine owns workflow semantics. Existing architecture guards admit only the current package inventory and exact friend graph.

The PostgreSQL provider stores individual durable records. SQL Server uses transactionally restored state documents. The shared provider-certification project already covers append conflicts, inbox/outbox, timers, continue-as-new, retention, resource pools, and governance. SQLite should reuse those behavioral contracts and tests rather than reference another provider or implement a second workflow engine.

`admit-durable-dag-run-progression` has approved planning for typed run streams, fenced FIFO turns, compact records, child reconciliation, discovery, and subscriptions, but its source/provider and bridge tasks remain open. SQLite's final feature-parity gate includes that work; neither a complete proposal nor a workflow-only SQLite lane establishes complete durable DAG support. Saga and several other authoring constructs remain deferred in the selected-mode matrix and are not made public here.

The application owns the file location and deployment. The provider owns its dedicated database file and all tables inside it. One application/project can have many workflows and internal participants; it has one active database-owning service provider, not one database per workflow. Different projects can use different files independently.

## Goals / Non-Goals

**Goals:**

- Supply the complete durable provider role with the same supported application features, payload identity, commit rules, management behavior, and restart guarantees as the existing durable providers.
- Support concurrent workflows inside one application host, including waits, event routes/fanout/start-or-deliver, durable publish, timers, retries/deadlines, cancellation, continue-as-new, resource pools, and resource governance.
- Require SQLite DAG parity before final full-support completion, using the shared runtime bridge and its approved contract.
- Provide local-file deployment, deterministic ownership rejection, finite backpressure and lock waits, recoverable shutdown/crash behavior, supported backup/restore instructions, and real-file evidence.
- Keep provider-native code/dependencies local, preserve the current friend graph, and make review/checkpoint boundaries explicit.

**Non-Goals:**

- Multiple concurrently owning hosts/processes/service providers for one file; a separate live callback host sharing an engine host's file; network shares, remote mounts, or distributed failover.
- Changing workflow semantics, adding deferred authoring features, introducing new engine/provider SPIs, a shared public Relational facade, or copying SQL Server's transition engine into a second provider.
- Supporting arbitrary application-owned tables/connections in the provider's file, attaching multiple databases into one commit, database-per-workflow sharding, or importing PostgreSQL/SQL Server databases.
- Encryption, a public backup API, public SQLite connection access, custom SQLite bundles, configurable durability downgrades, schema-name options, or SQLite trimming/Native AOT qualification.
- External publication, release tagging, or registry verification as part of proposal creation or implementation approval.

## Decisions

### 1. Approval precedes contract synchronization and implementation

All tasks remain unchecked at proposal creation. Phase 0 obtains explicit approval of this proposal's scope/API/spec deltas, independent contract review, and the required focused planning checkpoint. Phase 1 separately prepares and reviews canonical documentation/specification reconciliation before source work. Later coherent phases require their own validation, review, and checkpoint before the next phase.

An OpenSpec artifact status of `done` means the file exists; a validation success means the artifact structure is valid. Neither changes approval state. Existing dated verdicts stay immutable. Pending API additions stay documentation-only until their named source phase is authorized. Any change to this scope or public signature requires an amended review target.

### 2. One optional provider package with a small explicit application surface

The proposed owner is `OrcaCore.Providers.Sqlite`. Its only direct OrcaCore dependencies are `OrcaCore`, `OrcaCore.Runtime.Protocol`, and `OrcaCore.Provider.Abstractions`. SQLite tests use public provider ports and real database observations; no new product or test friendship is proposed. There is no provider-to-provider or provider-to-engine edge.

Proposed declarations, to be approved before exposure:

```csharp
namespace OrcaCore.Providers.Sqlite;

public sealed class SqliteDurableProviderOptions
{
    public SqliteDurableProviderOptions(string databasePath);
    public string DatabasePath { get; }
}

public static class OrcaCoreSqliteProviderServiceCollectionExtensions
{
    public static IServiceCollection AddOrcaCoreSqliteDurableProvider(
        this IServiceCollection services,
        SqliteDurableProviderOptions options);
}
```

The options constructor validates a nonblank filesystem path and captures its absolute form once, so later working-directory changes cannot redirect the file. Registration validates/freeze-copies the profile without opening a file, creating a directory, or starting a thread. Identical role/profile registration is idempotent; conflicting roles/options or pre-registered partial ports fail before adding provider services. File permissions and database ownership are startup failures, not registration side effects. Missing parent directories are reported; the application creates them deliberately.

Use a dedicated local file. Reject memory/temporary-database sentinels, SQLite URI/connection-string input, and known network paths. Document that a valid-looking local path can still reside on an unsupported remote filesystem; path normalization alone is not a filesystem-locality proof. SQLite's file locking, rather than string-path equality or a process-local dictionary, is the authority for aliases and independent providers. Resolve path/alias behavior on each qualified OS in the ownership tests.

### 3. One lifetime connection and SQLite-enforced exclusive ownership

A dedicated provider worker owns one non-pooled, private-cache SQLite connection for its full lifetime. Configure and verify `locking_mode=EXCLUSIVE`, acquire the actual database lock, validate file/schema ownership, apply the required journal/connection settings, and initialize the schema before reporting readiness or admitting durable work. The exact acquisition sequence must be proven on fresh and existing WAL databases without mutating an unrecognized database's schema. Do not infer ownership from setting the pragma alone.

All store operations, including reads, use this worker/connection. A second connection cannot serve as a hidden read path. The exclusive lock stays held across transaction boundaries; a second service provider/process targeting the same file fails startup within a finite budget and performs no durable acceptance. SQLite releases its file locks after the owning process dies. A replacement host reopens and recovers the existing journal rather than deleting a stale marker or expiring a liveness timestamp.

This choice gives deterministic single-application ownership and avoids a second sidecar-lock authority. It also means database browsers and application ORMs must close the provider before opening its file. A named mutex or lock file alone would not fence physical file aliases or native SQLite users. Multi-connection WAL readers and concurrent independent hosts are deferred deployment profiles requiring a separate amendment.

### 4. Provider-native record tables preserve the complete commit boundary

Use parameterized `Microsoft.Data.Sqlite` commands with a complete embedded first-create script and a migration journal binding each applied identifier to its canonical SHA-256 digest. The database records an OrcaCore ownership marker and supported schema/format version. Reject an unrelated non-empty database, unsupported/newer version, or changed applied digest without reset, repair, or partial schema installation. Initial creation is transactional and restart-idempotent; there is no compatibility `ALTER TABLE` script in this change.

Use the PostgreSQL logical record model as a reference, not its SQL dialect or locks:

| Record family | SQLite responsibility |
| --- | --- |
| Events, stream heads, checkpoints | Versioned append, immutable history, exact checkpoint/tail recovery |
| Inbox routes/targets, fanout membership, pending starts, start bindings | Global event identity, stable ordering, input/definition binding, atomic ownership |
| Outbox and timers | Persisted claim/fence/retry/poison/completion state and disjoint runtime/application selectors |
| Instance/wait/history/operational projections | Consistent runtime queries and side-effect-free statistics reads |
| Resource pools and governance streams | Atomic multi-pool obligations, ordered append, checksums, quarantine, and reconciliation |
| DAG runs/events/operations/nodes/subscriptions/lease requests | Complete approved DAG schema, compact events, FIFO fenced turns, discovery and shared-outbox notifications |

Store opaque payload/checkpoint bytes as `BLOB`; provider-owned scalar metadata uses explicit SQLite types and constraints. Strong matching keys use exact case-sensitive equality without normalization/truncation. Preserve canonical fingerprint strings, format IDs, checksums, identity scoping, and defensively copied payloads. Choose one lossless UTC representation for persisted runtime timestamps; production turn time is sampled inside the SQLite transaction, with the approved internal deterministic-clock seam for certification and no new public clock option.

Avoid provider-wide JSON snapshots: rewriting all retained workflow history on every mutation would make a single-user application's growing history increasingly expensive. Native record tables permit indexed recovery, maintenance, and bounded claims. Avoid an EF Core model/migration layer because the existing provider contract needs explicit atomic batches and already uses provider-owned SQL.

### 5. Serialize database mutations without serializing workflow bodies

Use short immediate write transactions for read-check-write mutations. Replace server-specific advisory/row locks, `SKIP LOCKED`, sequence objects, JSON operators, and time expressions with SQLite-specific operations. Expected-version checks, persisted monotonic acceptance/request order, unique bindings, and conditional claim/fence updates remain authoritative even with one connection. Replay, conflicting internal drivers, stale callbacks, and replacement boots still require those guards.

One acknowledged append commits its complete events/checkpoint/inbox/outbox/projection batch or none. Retried work reuses operation/event identities; commit ambiguity is reconciled through stored receipts or the existing idempotency contract. A storage exception must not become a logical version conflict, and a known committed operation must not be reported as rolled back merely because cancellation arrived afterward. Dispatcher delivery remains at-least-once with the same `EventId`; no exactly-once external-effect promise is added.

The database worker executes no application step, mapper, broker send, user callback, capacity wait, or subscriber continuation inside a transaction. It returns detached results and completes callers outside the transaction/worker stack. Concurrent workflow bodies remain subject to existing host limits; only their database operations queue. Reads do not rewrite durable state. Fair queueing, finite capacity, cancellation-aware admission, and pressure telemetry prevent timers/recovery/maintenance from being silently starved.

### 6. Make synchronous I/O, cancellation, and disposal honest

Microsoft.Data.Sqlite executes its asynchronous ADO.NET methods synchronously. Keep the async provider-port contract, but run native work on the owned database worker instead of the UI/request thread or an unbounded collection of `Task.Run` calls. Use finite command/busy timeouts; record exact bounds and queue capacity in the implementation review packet. Driver retries and any provider retry budget share one finite operation budget.

Cancellation before execution removes or neutralizes queued work without a mutation. Once execution starts, complete the transaction outcome safely; cancellation does not authorize disposal of a connection still in use. Shutdown closes admission, settles admitted operations according to their actual commit outcome, closes readers/commands/transactions on the owning worker, then closes the connection and releases file ownership. Startup failure follows the same disposal order. If a native operation outlives a stop budget, surface incomplete/failure through the hosting lifecycle and retain ownership until the operation actually exits; do not pretend cancellation force-stopped SQLite.

Configure and read back WAL, `synchronous=FULL`, and foreign-key enforcement. Keep automatic WAL checkpointing enabled and monitor checkpoint delay/file growth. Failure to checkpoint is observable and must not delete accepted work or a live WAL. A failed checkpoint after an already successful transaction does not retroactively undo or relabel that commit. Use finite maintenance batches and existing retention rules; never purge active instances, unacknowledged messaging, protected lease obligations, or required deduplication/confirmation evidence.

### 7. Full parity includes the current durable semantics and pending DAG contract

All ten ports must be real durable implementations before SQLite is registered as a complete role (`IsDevelopmentOnly=false`). A missing capability must fail registration/readiness rather than install a no-op port or fall back to process-local authority. Callback-only ingress can own the database, persist input-bound start intents, stop, and hand the file to a later engine host; concurrent ingress in an executing application uses that engine's existing ingress service.

Resource governance retains one expected-version serialized aggregate, whole-batch validation, multi-pool atomicity, tombstones, and stop-confirmation rules. Local deployment does not make lease expiry or process death evidence that protected external work stopped.

The final DAG phase follows `admit-durable-dag-run-progression` rather than introducing a SQLite child API. Implement the approved typed streams, separate record framing, complete first-create tables, operation receipts, node/admission indexes, FIFO short-turn ownership, hard deadlines, subscriptions/discovery, and shared-outbox selectors. Run the shared child-start/join/cancel bridge and full recovery journey. Test multiple logical owners/drivers inside the single application and stale owners across sequential boots. Server-provider remote-host scenarios remain server-provider obligations; SQLite's equivalent single-host scenarios must be explicitly mapped, not silently skipped or counted as multi-host proof.

Workflow-only evidence can be retained as an intermediate milestone. It must not close this change, advertise full feature support, or turn pending DAG scenarios green. If the DAG dependency is still blocked, preserve the reviewed workflow milestone and leave the DAG/final-closeout tasks open.

### 8. Qualification uses actual files, exact packages, and explicit operating limits

Add a black-box SQLite test project referencing the provider and shared certification. Use disposable real files with all connections closed between restart phases; an in-memory SQLite database cannot establish persistence or file-lock behavior. Test two independent processes for owner rejection and abrupt owner death, plus multiple same-host logical writers for concurrency/fencing. Retain kill-before/after-commit, event/wait race, timer recovery, outbox ambiguity, corruption/unsupported-format rejection, read-only/disk-full, migration-digest, cancellation, retention, and failure-path-disposal evidence.

Create an applicability map for every shared case: logical semantics run unchanged; server multi-host deployment cases remain required for PostgreSQL/SQL Server and map to SQLite owner rejection, sequential boot replacement, or concurrent same-host logical owners as appropriate. An unsupported live multi-host deployment is reported as such, never as a skipped logical invariant. Keep existing expected-red DAG source guards and unrelated changes intact.

Select and centrally pin a supported `Microsoft.Data.Sqlite`/bundled native-runtime combination during the approved contract phase. Require SQLite 3.51.3 or later, which includes the published WAL-reset fix, and verify `sqlite_version()`/`sqlite_source_id()` from the actual restored native library in each qualification lane. This minimum is a known-fix floor, not a claim that every newer build is qualified. Review current advisories and platform assets before freezing the package tuple; no native bundle is added while implementation is pending.

Qualify .NET 10 untrimmed JIT consumers on native Windows x64 and native Linux x64. Add SQLite's file-backed lane to CI and keep the existing server-provider lanes. Packed consumers reference only the documented application/hosting/provider packages, use a fresh restore cache/local feed with SHA-512 package identity checks, and verify optional SQLite assets do not enter the minimal ephemeral or other providers' closures. SQLite Native AOT/trim support requires separate evidence.

Measure transaction/queue latency, sustained commit throughput, timer lateness, startup/recovery time, retained-history growth, maintenance cost, and WAL growth under the selected durable settings. Freeze the workload, hardware, limits, and pass thresholds before performance qualification; do not advertise an invented concurrency ceiling or silently trade durability for throughput. Diagnostics use existing provider-name fields, BCL logging/activities/meters, and host-owned exporters, with no new public diagnostics facade.

### 9. Back up and restore through an explicit stopped-host procedure

Document a cold backup/restore recipe: stop and dispose the owning host, verify the connection/lock has been released, create a consistent SQLite backup with application-owned tooling, and validate the backup by opening a fresh host and resuming pending work. `SqliteConnection.BackupDatabase` can be used by that tooling after ownership is released. A bare copy of a live `.db` file is not a supported backup; preserve WAL state until SQLite has safely handled it. No code deletes `-wal`/`-shm` files as a recovery or cleanup shortcut.

Restore only while no provider owns the destination. Restore a validated matching-schema backup, keep the original available until recovery succeeds, and preserve the normal at-least-once external-effect caveat for records resent after restoring an older backup. Application input/output schemas and referenced workflow definitions must still be available and compatible. Do not add a public backup/import API or reset corrupted databases automatically.

## Risks / Trade-offs

- **One database worker bounds write throughput and queues reads** -> Keep transactions short, use indexed record tables, measure queue/timer latency, and publish the tested operating envelope; retain full workflow correctness under pressure.
- **Exclusive locking prevents external live inspection** -> State the dedicated-file ownership contract and provide stopped-host backup/inspection instructions; qualification proves ownership across process/path aliases.
- **Synchronous native work may outlive cancellation** -> Use bounded busy waits and cancellation-aware queueing, keep disposal on the owner, and report an incomplete stop without releasing a still-used connection.
- **Disk errors or a lying filesystem can defeat expectations** -> Verify settings and native runtime, test I/O/disk-full paths, acknowledge only after commit, and separate process-crash proof from physical power-loss/storage qualification.
- **Global provider guards can reject the new package even when its tests pass** -> Reconcile the exact manifest, source map, API owners, package inventories, and affected live deltas in a separately reviewed canonical phase.
- **DAG runtime source may remain unfinished** -> Make DAG parity and final closeout dependent tasks; preserve narrower workflow evidence without claiming full completion.
- **A broad shared provider refactor could obscure the addition** -> Keep SQLite implementation provider-owned; any reusable extraction needs its own reviewed scope and existing-provider regression evidence.

## Migration Plan

1. Obtain scope/API/delta approval and checkpoint the planning target. Reconcile canonical docs/specs and affected active deltas in the next reviewed checkpoint before source changes.
2. Add the optional package/schema/worker in focused phases. Create a new dedicated file from the complete first-create migration; apply no speculative legacy-provider upgrades or cross-provider import.
3. Certify complete workflow ports and application journeys. Keep this milestone explicitly short of full completion while DAG source remains pending.
4. After the shared DAG implementation is approved, build the complete SQLite first-create schema at the selected release target and certify DAG parity. Pre-release intermediate database files have no promised upgrade path; any decision to support them requires a separately reviewed migration amendment.
5. Package and qualify exact consumers, native runtime assets, backups, performance, and failure handling. Obtain independent final approval and checkpoint the final evidence. Publication remains a separate request.

Rollback before publication means revert only the focused SQLite source/inventory changes and retain users' database files. A previous binary must refuse a newer/unknown schema; it must not reset or attempt an implicit downgrade. Operational rollback uses the documented compatible backup and stopped-host restore procedure.

## Open Questions

No product-scope question is deferred: the target is full durable parity for one application/project with one active local owner. Explicit implementation approval is still required. Before its corresponding source/qualification phase, review must freeze the package/native tuple, exact first-create DDL, finite queue/timeout constants, and measured workload thresholds. Those choices cannot relax the approved role, ownership, atomicity, or final DAG gates.

## References

- Repository contracts: `openspec/specs/{repository-foundation,developer-facing-surface,durable-persistence-and-outbox,quality-and-verification}/spec.md`; `docs/specs/10-provider-model-and-extensibility.md`; `docs/specs/17-selected-mode-capability-matrix.md`.
- Existing implementation references: PostgreSQL `Migrations/001_initial.sql`, SQL Server `Internal/SqlServerStateDocumentStore.cs`, the two provider registration extensions, and `tests/OrcaCore.ProviderCertification/`.
- DAG dependency: `openspec/changes/admit-durable-dag-run-progression/{design,tasks}.md`; its latest approved contract and fingerprint amendment must be re-resolved before implementation.
- [SQLite transaction control](https://sqlite.org/lang_transaction.html) and [exclusive locking](https://sqlite.org/pragma.html#locking_mode) support the transaction/ownership choices.
- [SQLite WAL](https://sqlite.org/wal.html) documents local-host requirements, companion-file handling, checkpointing, durability, and the WAL-reset fix floor.
- Microsoft documents [synchronous async-method behavior](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/async), [locking and timeouts](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/database-errors), [bundled native runtimes](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/custom-versions), and the [backup API](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/backup).
