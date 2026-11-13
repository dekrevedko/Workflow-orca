## MODIFIED Requirements

### Requirement: First-release provider ownership and schemas are exact
The first release SHALL ship exactly the InMemory development provider plus PostgreSQL, SQL Server, and single-application SQLite
production providers named by the package manifest. Provisional RabbitMQ, Redis, shared Relational,
and ZeroMQ provider projects, inactive tests, SDK dependencies, packages, and orphan source roots
SHALL be absent. Broker-specific mapping SHALL remain application/companion-owned through
`IWorkflowEventDispatcher`. PostgreSQL, SQL Server, and SQLite SHALL each implement the complete current split
provider ports, including operational and maintenance ownership, without depending on one another or
reviving the deleted provisional SQL Server shape. Each production provider's first-create schema
SHALL contain its complete current table/index and persisted ordering shape, its migration journal SHALL bind an
applied id to a canonical content digest, and its migrations SHALL contain no compatibility
`ALTER TABLE` upgrade DDL or renamed equivalent.

InMemory, PostgreSQL, SQL Server, and SQLite SHALL implement identical typed DAG stream/record/checkpoint, fenced lease/version append, discovery, caller-key binding, node/admission index and subscriptions. Production FIRST-CREATE scripts SHALL include complete dag_runs, dag_run_events, dag_run_operations, dag_run_nodes and dag_run_subscriptions tables, shared-outbox DAG owner/recipient columns, exact ordinal keys, state/version/fence constraints and discovery/claim/subscription indexes. No compatibility ALTER/upgrade or DAG-private transport SHALL be introduced. One transaction SHALL serialize ownership checks and all related records/notifications. Real-storage certification SHALL cover restart, competing owners/limits, append crash points, disjoint selectors, eager decode/null, separate-stream child reconciliation, cancellation/failure closure and notification races; in-memory success SHALL NOT establish durable support.

The complete first-create schema SHALL additionally contain dag_run_lease_requests with stable FIFO sequence, exact boot-owner/request/purpose, live-waiter expiry, absolute grant deadline, consumed receipt retention and one live request per owner/run. All providers SHALL enforce no same-owner bypass and no renewal beyond the absolute deadline. For PostgreSQL and SQL Server, real-storage DG-P14/DG-P15 SHALL prove remote child joins/cancellation progress behind a continuously renewing owner, with cancellation triggered by another DAG-role host and child cancellation consumed on a role-free host; DG-P16/DG-P17/DG-P18 SHALL prove release-before-idle, orphan-observer cleanup and compact-event/checkpoint parity.

The runtime SHALL evaluate mapping outside turn ownership, retain prepared immutable results and use short fenced recheck-and-append only. Provider transaction time SHALL use the explicit production DB-clock/internal certification-clock seam. Same-host requests SHALL use local arrival FIFO, with OwnerBusy for a second distinct live provider request. Real-storage DG-P19–24 SHALL certify slow valid mapper admission, distinct isolated run evaluation bounds, zero look-ahead and authored-order admission, exact pressure, local/provider arbitration, cross-run slow/stuck isolation and acknowledged/lost/poisoned ready-hint recovery. MaxConcurrentNodes SHALL NOT become a host-global mapper bound.

SQLite SHALL use a dedicated local file with one active owning service provider and implement the same logical atomicity, ordering, messaging, governance, and DAG invariants within that scope. It SHALL use complete record tables and SQLite-native transactional ordering/claim primitives without provider-wide history snapshots, server lock syntax, or cross-provider dependencies. Its production transaction clock SHALL sample database UTC and retain the approved internal certification-clock seam. Required WAL/FULL/foreign-key settings and actual exclusive file ownership SHALL be verified before readiness. Live multi-host scenarios SHALL remain PostgreSQL/SQL Server obligations; SQLite SHALL explicitly certify second-owner rejection, sequential boot replacement, and equivalent concurrent same-host logical-owner fencing/FIFO behavior. An unsupported deployment SHALL NOT waive a logical invariant. SQLite full completion SHALL include the approved shared DAG source/bridge and its complete first-create schema; a workflow-only intermediate milestone SHALL NOT establish final parity.

#### Scenario: Repository provider set is inspected
- **WHEN** project, package, source-root, SDK-version, migration, and future-registry guards run
- **THEN** only the four approved durable providers remain, PostgreSQL, SQL Server, and SQLite initialize complete provider-native schemas from their first migrations, and every removed provider family has an explicit recoverable disposition

#### Scenario: FIFO fairness survives a continuously renewing owner
- **WHEN** host A keeps renewing and host B has an earlier waiting join or cancellation request
- **THEN** the 15-second hard deadline and stable FIFO order give B the next eligible turn on PostgreSQL and SQL Server

#### Scenario: Consumed waiter cannot retake its old position
- **WHEN** a released request ID is retried after another host obtains a turn
- **THEN** the provider returns StaleFence without resurrecting the old sequence or lease

#### Scenario: SQLite first-create and owner scope are inspected
- **WHEN** a fresh file is initialized and a second independent SQLite owner attempts startup
- **THEN** the first owner initializes the complete declared schema with a digest-bound journal, and the second owner fails before accepting durable work

#### Scenario: SQLite logical owners contend for a DAG turn
- **WHEN** different same-host logical owners contend while an earlier owner renews within the approved absolute deadline
- **THEN** persisted FIFO order, the hard deadline, receipt retention, and stale-fence rejection preserve the same logical turn guarantees as the server providers

### Requirement: Provider implementations certify messaging ownership parity
The in-memory development provider and every shipped durable production provider SHALL implement
the same logical route inbox, target inbox, fanout snapshot, pending start-intent, continuation,
external outbox, poison, and retention ownership contracts. Production-provider certification
SHALL exercise restart against real storage. PostgreSQL and SQL Server SHALL also exercise concurrent competing-host boundaries; SQLite SHALL exercise exclusive-owner rejection, sequential host replacement, and concurrent same-host logical ownership. In-memory-only success
SHALL NOT establish durable support.

#### Scenario: Provider host is replaced after acceptance
- **WHEN** one host accepts an event or commits an outbound record and another host claims the resulting work
- **THEN** the replacement observes the same envelope, route/target ownership, identity fingerprint, retry state, and payload bytes and completes at most one durable transition

#### Scenario: Provider cannot atomically own a fanout snapshot
- **WHEN** the complete target set exceeds the provider's approved atomic acceptance limit
- **THEN** ingress returns `Rejected(FanoutLimitExceeded)` before committing the envelope or any target ownership

#### Scenario: SQLite accepts work before an engine host exists
- **WHEN** an owning callback-only host accepts a message/start intent and then disposes the file before a later engine host takes ownership
- **THEN** the engine observes the same durable envelope, binding, target ownership, and retry state without relying on broker redelivery or concurrent callback-host access
