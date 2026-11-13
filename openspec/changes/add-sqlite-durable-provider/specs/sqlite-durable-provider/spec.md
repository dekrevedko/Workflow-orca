## ADDED Requirements

### Requirement: SQLite ownership belongs to one application project
`OrcaCore.Providers.Sqlite` SHALL supply a complete durable provider role for one application/project using one dedicated local filesystem database and one active owning service provider. Multiple workflows, concurrent internal drivers, resource-governance participants, and application ingress SHALL remain supported within that host. Different projects SHALL be able to use different files independently. Live multi-host file sharing, remote/network filesystems, and unrelated application-owned database connections/tables SHALL be outside this deployment contract.

#### Scenario: One application runs concurrent workflows
- **WHEN** one SQLite-backed durable host progresses several workflows and accepts application events concurrently
- **THEN** their database operations are serialized safely while the existing runtime permits workflow-body concurrency under its normal host limits

#### Scenario: Independent projects run together
- **WHEN** two application hosts select different dedicated database files
- **THEN** their ownership, durable identities, queues, and recovery remain independent without a process-global provider singleton

### Requirement: SQLite registration exposes one immutable path profile
The provider SHALL own `OrcaCore.Providers.Sqlite.SqliteDurableProviderOptions` with one public constructor `SqliteDurableProviderOptions(string databasePath)` and one get-only `string DatabasePath`, plus `OrcaCoreSqliteProviderServiceCollectionExtensions.AddOrcaCoreSqliteDurableProvider(IServiceCollection, SqliteDurableProviderOptions)` returning `IServiceCollection`. Construction SHALL validate a nonblank filesystem path and capture its absolute form once. Registration SHALL freeze the profile, be idempotent only for the same role/profile, and reject null options, conflicting profiles/roles, or pre-registered partial ports before adding provider services. Registration SHALL perform no file/directory creation, connection opening, or worker startup. Schema-name, connection-string, binder, raw-connection, codec, and durability-downgrade overloads SHALL be absent.

#### Scenario: Working directory changes after options construction
- **WHEN** an application constructs options from a relative file path and later changes its working directory
- **THEN** registration and startup continue to target the originally captured absolute database path

#### Scenario: A conflicting provider is already registered
- **WHEN** SQLite registration encounters another durable role, different SQLite options, or manually registered provider ports
- **THEN** it rejects the composition without leaving a partial SQLite role

#### Scenario: A path cannot supply durable local storage
- **WHEN** configuration supplies a memory/temporary sentinel, URI/connection-string input, or a known network path
- **THEN** profile validation rejects it rather than substituting temporary, shared-cache, or in-memory storage

### Requirement: SQLite enforces exclusive database lifetime ownership
The provider SHALL hold actual SQLite exclusive file ownership on its one non-pooled private-cache connection from successful initialization until the owning worker has finished and closed the connection. Ownership SHALL be established and verified before readiness, schema mutation, or durable acceptance. A second provider instance/process targeting the same physical file SHALL fail startup within a finite budget without accepting work. A process-local mutex, normalized path string, lock-file existence, or liveness/expiry timestamp SHALL NOT substitute for SQLite file ownership. Startup failure and normal disposal SHALL release resources in dependency order; abrupt process death SHALL permit a replacement owner to recover through SQLite without deleting journal files.

#### Scenario: Another service provider targets the live file
- **WHEN** a second independently constructed provider attempts to initialize the database already owned by a running host
- **THEN** the second provider reports ownership/startup failure within the tested budget and the first host's committed state is unchanged

#### Scenario: Two processes use aliases of one file
- **WHEN** separate processes target supported path aliases referring to the same physical database
- **THEN** exactly one obtains database ownership and the other performs no durable acceptance

#### Scenario: The owner is terminated abruptly
- **WHEN** the owning process dies while a journal exists and a fresh host later opens the file
- **THEN** the new host obtains ownership and performs SQLite recovery without treating process death as proof that protected external work stopped

### Requirement: SQLite schema and stored values preserve provider integrity
The provider SHALL initialize a complete provider-native first-create schema for its declared capability set, with transactional creation, indexed durable records, a provider ownership/schema marker, and a migration journal binding applied identifiers to canonical content digests. It SHALL reject unrelated non-empty databases, unsupported/newer schemas, changed applied migration content, and malformed persisted formats without automatic reset or partial schema installation. This change SHALL introduce no legacy-provider import or compatibility `ALTER TABLE` upgrade. Opaque payloads/checkpoints SHALL retain exact bytes and format IDs; fingerprints, strong matching keys, checksums, ordered sequences, and timestamps SHALL preserve their existing contract without normalization, case folding, truncation, or lossy conversion. Provider-wide snapshots of all workflow history SHALL NOT be the per-mutation persistence mechanism.

#### Scenario: Database creation is interrupted
- **WHEN** a process terminates during first-create schema or journal installation
- **THEN** a replacement initialization sees either the complete recognized schema/journal or an uncommitted creation that can be retried atomically

#### Scenario: Migration content changes under an applied identifier
- **WHEN** a database records a migration identifier whose stored digest differs from the selected embedded script
- **THEN** startup fails before progression and preserves the database for investigation

#### Scenario: Values differ only by case or payload bytes
- **WHEN** records contain ordinally distinct strong keys or different fixed-codec payload bytes
- **THEN** lookup, uniqueness, conflict detection, and round-trip recovery preserve those differences exactly

### Requirement: SQLite acknowledgements follow atomic durable commits
SQLite SHALL implement expected-version append and the complete existing provider commit batch as one atomic transaction or no effects. An accepted inbox/fanout/start-or-deliver mutation SHALL own its complete related records atomically. Claims SHALL preserve the existing selector, fence, receipt, retry, and idempotency rules. Success SHALL be acknowledged only after commit; storage failures SHALL remain distinguishable from logical conflicts. Commit ambiguity SHALL be reconciled through existing durable identity/receipt contracts, and cancellation after a known commit SHALL NOT be reported as proof of rollback. SQLite SHALL verify WAL, `synchronous=FULL`, and foreign-key enforcement before readiness and SHALL never silently downgrade those settings.

#### Scenario: A workflow transition loses a version race
- **WHEN** two logical drivers submit different transitions against the same expected stream version
- **THEN** exactly one commits and the other receives the existing conflict result without partial checkpoint, inbox, outbox, or projection effects

#### Scenario: A process dies around acknowledgement
- **WHEN** the process terminates before commit or after commit but before returning success
- **THEN** recovery observes either no mutation or the complete mutation, and identity-stable retry reconciles the committed outcome

#### Scenario: Durability settings cannot be established
- **WHEN** startup cannot read back the required journal, synchronization, or foreign-key configuration
- **THEN** it fails before reporting the provider ready or accepting work

### Requirement: SQLite database work has bounded admission and honest cancellation
The provider SHALL execute all native connection/command/transaction/reader work on one owned database worker, including reads, behind fair finite-capacity admission and finite busy/command budgets. It SHALL preserve the existing asynchronous port signatures without claiming asynchronous SQLite I/O. It SHALL execute no application step, mapper, dispatcher send, user continuation, capacity wait, or subscriber callback inside a database transaction. Queued cancellation SHALL prevent mutation; running cancellation SHALL preserve the actual commit outcome. Shutdown SHALL close admission, settle admitted operations, and dispose native resources on the owner only after their work exits. A native operation exceeding the stop budget SHALL produce observable incomplete/failure behavior and SHALL NOT cause premature connection disposal or ownership release.

#### Scenario: The database queue is saturated
- **WHEN** concurrent workflows fill the bounded database queue
- **THEN** callers receive cancellation-aware backpressure, pending work remains observable, and no accepted durable work is silently discarded

#### Scenario: A queued operation is cancelled
- **WHEN** its caller cancels before the database worker starts it
- **THEN** the request has no persistent effects and consumes no later queue turn that can mutate state

#### Scenario: Shutdown overlaps an executing native command
- **WHEN** the host stop budget elapses while the owned worker is still executing SQLite work
- **THEN** lifecycle reporting records incomplete/failure and the connection remains owned until safe command/transaction completion and disposal

### Requirement: SQLite implements every workflow and messaging provider port
SQLite SHALL durably implement `IWorkflowEventStore`, `IWorkflowInboxStore`, `IWorkflowStartIdempotencyStore`, `IWorkflowOutboxStore`, `IWorkflowProjectionStore`, `IWorkflowOperationalStore`, `IWorkflowProviderMaintenanceStore`, `ITimerScheduler`, `IResourcePoolStore`, and `IDurableResourceGovernanceStore`, including route/target inboxes and disjoint external/internal outbox partitions. It SHALL register one complete non-development-only `IDurableProviderRole` and SHALL install no no-op port or process-local authority fallback. The shared durable host SHALL preserve every supported workflow construct, retry/deadline/cancellation behavior, continue-as-new rule, query, and recovery operation. External effects SHALL retain at-least-once identity-stable delivery semantics.

#### Scenario: A wait and its timeout race after restart
- **WHEN** a recovered workflow receives both a matching accepted event and a due timer
- **THEN** existing runtime/provider fencing selects one valid continuation and retains the losing record's correct ownership outcome

#### Scenario: A callback-only host hands off to an engine host
- **WHEN** the callback-only owning host accepts an input-bound start intent, closes the database, and a later engine host opens it
- **THEN** the engine resumes that exact accepted intent and event without broker redelivery, a changed payload binding, or a concurrent standalone callback host

#### Scenario: An outbound send is ambiguous
- **WHEN** delivery succeeds externally and the process dies before recording its acknowledgement
- **THEN** recovery retries the same outbound EventId through the application dispatcher and never exposes an internal continuation record to that dispatcher

### Requirement: SQLite governance preserves protected obligations
SQLite SHALL persist the complete expected-version resource-governance aggregate with defensive copy, supported format, checksum, exact sequence continuity, atomic multi-pool reservations, queueing, resize/review history, quarantine, reconciliation, stop-confirmation bindings, and retained tombstones. A governance batch SHALL append wholly or conflict without partial persistence. Single-application deployment, lease review expiry, cancellation, and process death SHALL NOT independently authorize reclaiming protected physical work.

#### Scenario: A multi-pool reservation is interrupted
- **WHEN** a crash occurs while recording a request spanning multiple resource pools
- **THEN** recovery sees either the complete reservation/obligation or none, with no partial capacity allocation

#### Scenario: A replacement host finds an uncertain protected lease
- **WHEN** restart recovers an obligation without the contractually required stop or release evidence
- **THEN** the obligation remains held or quarantined for reconciliation rather than being freed because the prior owner died

### Requirement: SQLite full support includes durable DAG parity
Final SQLite completion SHALL implement and certify the approved durable DAG contract through the shared `OrcaCore.Dag.Hosting` bridge, including typed run streams, record/checkpoint framing, graph/input/output presence and bytes, compact history, operation receipts, node/admission indexes, FIFO fenced short-turn ownership, hard deadlines, discovery/subscriptions, shared-outbox partitioning, and deterministic child start/join/cancel recovery. This phase SHALL depend on the approved source/provider/bridge work in `admit-durable-dag-run-progression`; this change SHALL not bypass that gate or create a SQLite-specific child API. Same-host competing logical owners and sequential boots SHALL cover SQLite's deployment-equivalent fencing and recovery. Workflow-only evidence SHALL remain an intermediate milestone and SHALL NOT establish full-feature completion.

#### Scenario: A DAG run resumes from the SQLite file
- **WHEN** a fresh owning host opens a database containing a partially completed DAG and its parked children
- **THEN** it reconstructs the committed run/node/input/output state, reattaches to stable child identities, and completes through the shared runtime bridge

#### Scenario: A stale turn attempts a mutation
- **WHEN** a stale, expired, or previously consumed logical owner submits an append after another eligible same-host owner or replacement boot has progressed
- **THEN** SQLite rejects the stale fence without changing run events, checkpoints, receipts, node state, or notifications

#### Scenario: The shared DAG bridge is not implemented
- **WHEN** SQLite workflow tests pass while the approved shared DAG source/provider/bridge tasks remain incomplete
- **THEN** SQLite retains a workflow-only milestone and this change's DAG and final-completion tasks remain open

### Requirement: SQLite operations preserve journals and retained ownership
Operational statistics reads SHALL have no durable write side effects. Maintenance SHALL use finite batches and the existing reference/lease/inbox/outbox retention rules, preserve active and uncertain obligations, and expose backlog/queue/checkpoint pressure through existing BCL diagnostics conventions. SQLite SHALL retain safe checkpoint behavior and SHALL never delete live WAL/shared-memory files to recover space or clear ownership. Supported backup/restore instructions SHALL require a stopped/disposed provider, a consistent SQLite backup, exclusive restore access, schema/definition validation, and a demonstrated resume journey; no public backup API is introduced.

#### Scenario: Retention meets a pending outbox or protected lease
- **WHEN** maintenance inspects a terminal workflow with unresolved messaging or protected obligations
- **THEN** the existing blocker prevents removal and the obligation remains inspectable

#### Scenario: A checkpoint fails after a successful commit
- **WHEN** WAL checkpoint maintenance cannot complete after a transaction has already committed
- **THEN** the commit remains acknowledged as committed, pressure/failure is observable, and SQLite journal state is preserved

#### Scenario: A stopped-host backup is restored
- **WHEN** a consistent matching-schema backup is restored while no provider owns the destination and a fresh host starts
- **THEN** pending waits, timers, messages, workflows, and supported DAG runs resume under their existing identity and redelivery guarantees

### Requirement: SQLite qualification uses exact real-file evidence
SQLite SHALL pass the shared logical provider certification plus real-file schema, restart, abrupt-process termination, ownership rejection, cancellation/disposal, contention, disk-full/read-only, malformed-state, retention, and application-journey checks. Each server-specific live multi-host case SHALL have an explicit applicability disposition and equivalent SQLite single-host logical/boot-boundary evidence where applicable; an unsupported deployment SHALL never hide a missing logical invariant. The actual native runtime SHALL be a reviewed pinned build of SQLite 3.51.3 or later, including the published WAL-reset fix, and its version/source identity SHALL be recorded. Exact package-only .NET 10 untrimmed JIT consumers SHALL execute on native Windows x64 and Linux x64. SQLite assets SHALL remain absent from unselected package closures. Performance evidence SHALL record the workload, durability settings, hardware, pass thresholds, throughput/latency, timer lag, recovery cost, and history/WAL growth without claiming physical power-loss or Native AOT qualification from software tests.

#### Scenario: An in-memory SQLite lane passes
- **WHEN** tests succeed only with an in-memory database or one continuously open test host
- **THEN** they provide no evidence for file durability, independent-process ownership, or restart support

#### Scenario: A package restores a different native SQLite build
- **WHEN** actual native version/source identity differs from the qualified tuple or is below the required fixed-version floor
- **THEN** package qualification fails rather than relying on the managed package version as proof

#### Scenario: Full support is proposed for release
- **WHEN** final SQLite approval is requested
- **THEN** reviewed evidence covers all workflow and DAG obligations in the supported single-application scope, native/runtime/package identity, the measured operating envelope, and all unresolved limitations without treating passing tests as approval
