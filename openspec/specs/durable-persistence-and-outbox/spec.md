## Purpose

Define the persistence, outbox, serialization, and retention capabilities required by OrcaCore durable execution.

## Requirements

### Requirement: Workflow store persists first-class durable records
The durable system SHALL persist workflow instances, waits, inbox state, outbox state, history, and related queryable runtime records through an explicit `IWorkflowStore` boundary.

#### Scenario: Durable state is committed
- **WHEN** a workflow transition is durably recorded
- **THEN** the store contract preserves the instance snapshot and its related operational records as first-class durable data

### Requirement: Outbox delivery is adapter-driven and at-least-once
Outbound durable dispatch SHALL flow through `IMessageDispatcher` and an outbox pump so external delivery remains adapter-based and at-least-once rather than being coupled directly to workflow execution.

#### Scenario: Local commit succeeds before external delivery
- **WHEN** a workflow transition commits outbound work into the outbox
- **THEN** the dispatcher pump can retry delivery independently without losing the already committed workflow state

### Requirement: Durable payloads cross boundaries through explicit serializers
The durable system SHALL serialize workflow and event payloads through explicit payload-envelope and schema-resolution abstractions so persisted data can be restored deterministically.

#### Scenario: Persisted payload is read after restart
- **WHEN** the runtime rehydrates a durable instance or outbox record
- **THEN** payload materialization occurs through the configured serialization abstractions instead of provider-specific ad hoc logic

### Requirement: Retention and purge preserve runtime correctness
The durable system SHALL support retention and purge operations for inbox, outbox, and history artifacts without corrupting live runtime state or breaking terminal-instance inspection guarantees.

#### Scenario: Old operational artifacts are purged
- **WHEN** retention cleanup removes expired durable artifacts
- **THEN** active runtime correctness and documented inspection behavior remain intact

### Requirement: First-release provider ownership and schemas are exact
The first release SHALL ship exactly the InMemory development provider plus PostgreSQL and SQL Server
production providers named by the package manifest. Provisional RabbitMQ, Redis, shared Relational,
and ZeroMQ provider projects, inactive tests, SDK dependencies, packages, and orphan source roots
SHALL be absent. Broker-specific mapping SHALL remain application/companion-owned through
`IWorkflowEventDispatcher`. PostgreSQL and SQL Server SHALL each implement the complete current split
provider ports, including operational and maintenance ownership, without depending on one another or
reviving the deleted provisional SQL Server shape. Each production provider's first-create schema
SHALL contain its complete current table/index/sequence shape, its migration journal SHALL bind an
applied id to a canonical content digest, and its migrations SHALL contain no compatibility
`ALTER TABLE` upgrade DDL or renamed equivalent.

#### Scenario: Repository provider set is inspected
- **WHEN** project, package, source-root, SDK-version, migration, and future-registry guards run
- **THEN** only the three approved v1 providers remain, PostgreSQL and SQL Server initialize complete provider-native schemas from their first migrations, and every removed provider family has an explicit recoverable disposition

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
NOT retain unowned step-level stuck fields. Each relational production provider's periodic pressure
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
