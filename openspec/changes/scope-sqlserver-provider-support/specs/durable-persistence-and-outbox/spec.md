## MODIFIED Requirements

### Requirement: First-release provider ownership and schemas are exact
The first release SHALL ship exactly the InMemory development provider, the PostgreSQL production target and the experimental SQL Server provider named by the package manifest. Provisional RabbitMQ, Redis, shared Relational,
and ZeroMQ provider projects, inactive tests, SDK dependencies, packages, and orphan source roots
SHALL be absent. Broker-specific mapping SHALL remain application/companion-owned through
`IWorkflowEventDispatcher`. PostgreSQL and SQL Server SHALL each implement the complete current split
provider ports, including operational and maintenance ownership, without depending on one another or
reviving the deleted provisional SQL Server shape. Each shipped relational provider's first-create schema
SHALL contain its complete current table/index/sequence shape, its migration journal SHALL bind an
applied id to a canonical content digest, and its migrations SHALL contain no compatibility
`ALTER TABLE` upgrade DDL or renamed equivalent.

InMemory, PostgreSQL and SQL Server SHALL implement identical typed DAG stream/record/checkpoint, fenced lease/version append, discovery, caller-key binding, node/admission index and subscriptions. Production FIRST-CREATE scripts SHALL include complete dag_runs, dag_run_events, dag_run_operations, dag_run_nodes and dag_run_subscriptions tables, shared-outbox DAG owner/recipient columns, exact ordinal keys, state/version/fence constraints and discovery/claim/subscription indexes. No compatibility ALTER/upgrade or DAG-private transport SHALL be introduced. One transaction SHALL serialize ownership checks and all related records/notifications. Real-storage certification SHALL cover restart, competing owners/limits, append crash points, disjoint selectors, eager decode/null, separate-stream child reconciliation, cancellation/failure closure and notification races; in-memory success SHALL NOT establish durable support.

The complete first-create schema SHALL additionally contain dag_run_lease_requests with stable FIFO sequence, exact boot-owner/request/purpose, live-waiter expiry, absolute grant deadline, consumed receipt retention and one live request per owner/run. All providers SHALL enforce no same-owner bypass and no renewal beyond the absolute deadline. Real-storage DG-P14/DG-P15 SHALL prove remote child joins/cancellation progress behind a continuously renewing owner, with cancellation triggered by another DAG-role host and child cancellation consumed on a role-free host; DG-P16/DG-P17/DG-P18 SHALL prove release-before-idle, orphan-observer cleanup and compact-event/checkpoint parity.

The runtime SHALL evaluate mapping outside turn ownership, retain prepared immutable results and use short fenced recheck-and-append only. Provider transaction time SHALL use the explicit production DB-clock/internal certification-clock seam. Same-host requests SHALL use local arrival FIFO, with OwnerBusy for a second distinct live provider request. Real-storage DG-P19–24 SHALL certify slow valid mapper admission, distinct isolated run evaluation bounds, zero look-ahead and authored-order admission, exact pressure, local/provider arbitration, cross-run slow/stuck isolation and acknowledged/lost/poisoned ready-hint recovery. MaxConcurrentNodes SHALL NOT become a host-global mapper bound.

SQL Server SHALL continue to implement and certify the full durable role on real storage. Its ordinary workflow and governance paths read shared state documents and serialize mutations within the corresponding schema and state key. DAG streams use separate relational tables. SQL Server has no qualified operating envelope. Promotion SHALL require a separately reviewed storage change and qualification of independent-stream contention, recovery I/O as history grows, and tail latency under a declared workload.

#### Scenario: An application selects a v1 durable store
- **WHEN** an application needs durable execution covered by the v1 production storage target
- **THEN** the package guidance identifies PostgreSQL and discloses the experimental tier and document-storage limits of SQL Server

#### Scenario: Repository provider set is inspected
- **WHEN** project, package, source-root, SDK-version, migration, and future-registry guards run
- **THEN** only the three approved v1 providers remain, PostgreSQL and SQL Server initialize complete provider-native schemas from their first migrations, and every removed provider family has an explicit recoverable disposition

#### Scenario: FIFO fairness survives a continuously renewing owner
- **WHEN** host A keeps renewing and host B has an earlier waiting join or cancellation request
- **THEN** the 15-second hard deadline and stable FIFO order give B the next eligible turn on PostgreSQL and SQL Server

#### Scenario: Consumed waiter cannot retake its old position
- **WHEN** a released request ID is retried after another host obtains a turn
- **THEN** the provider returns StaleFence without resurrecting the old sequence or lease

### Requirement: Operational projections expose durable messaging pressure
Provider/host operator projections SHALL expose pending, retryable, claimed, permanent-failure, and
poison counts separately for internal continuations and external application events, together with
stream growth, checkpoint count/lag, and active-instance pressure required by the canonical
management and observability contracts. Corresponding BCL observable gauges SHALL derive from the
same authoritative projections within the documented scrape interval without adding public
application enumeration or `Statistics()` APIs. Durable aggregate projection writes SHALL record
runtime-owned last activity from committed progress. A host/operator sweep SHALL explicitly refresh
stuck-state using a runtime-owned observation time and positive inactivity threshold before reading
a side-effect-free statistics snapshot; providers SHALL mark stale pending, running, or
cancellation-requested instances as stuck without classifying an ordinary external wait solely by
age. Durable projections SHALL persist only stuck fields produced by the durable runtime and SHALL
NOT retain unowned step-level stuck fields. Each shipped relational provider's periodic pressure
collection SHALL derive event growth and checkpoint lag from a transactionally maintained
stream-head/checkpoint join that includes stream-only artifacts, and SHALL NOT aggregate the
append-only event relation on every host sweep.

#### Scenario: Operator compares projection and telemetry
- **WHEN** a fixture creates pending continuation, external dispatch retry, poison, stream, and checkpoint states
- **THEN** the grouped operator projection and matching `orca.*` gauges report the same classified values within one scrape interval

#### Scenario: Durable instance stops making committed progress
- **WHEN** a pending, running, or cancellation-requested durable instance exceeds the validated host/operator inactivity threshold
- **THEN** provider-authoritative statistics and `orca.instances.stuck` include it using engine-authored last activity rather than a fixture-seeded flag

#### Scenario: Statistics are read without refreshing provider state
- **WHEN** a caller reads an operator-statistics snapshot without invoking the explicit stuck-state refresh operation
- **THEN** the provider returns the current immutable snapshot without writing durable state

#### Scenario: Stream exists without an aggregate projection
- **WHEN** an accepted durable stream has committed events but no current aggregate projection or checkpoint
- **THEN** provider pressure includes its stream growth and checkpoint lag through the maintained stream head

### Requirement: Provider implementations certify messaging ownership parity
The in-memory development provider and both shipped relational providers SHALL implement
the same logical route inbox, target inbox, fanout snapshot, pending start-intent, continuation,
external outbox, poison, and retention ownership contracts. Certification of each shipped relational provider
SHALL exercise restart and competing-host boundaries against real storage; in-memory-only success
SHALL NOT establish durable support.

#### Scenario: Provider host is replaced after acceptance
- **WHEN** one host accepts an event or commits an outbound record and another host claims the resulting work
- **THEN** the replacement observes the same envelope, route/target ownership, identity fingerprint, retry state, and payload bytes and completes at most one durable transition

#### Scenario: Provider cannot atomically own a fanout snapshot
- **WHEN** the complete target set exceeds the provider's approved atomic acceptance limit
- **THEN** ingress returns `Rejected(FanoutLimitExceeded)` before committing the envelope or any target ownership
