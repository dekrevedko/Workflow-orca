## MODIFIED Requirements

### Requirement: Workflow store persists first-class durable records
The durable system SHALL persist workflow instances, checkpoints, waits, timers, route-level and
per-instance inbox records, consumed-event identity, fanout target snapshots, pending start intents,
internal continuations, external outbox records, poison/dead-letter state, history, and operational
projections through explicit provider boundaries. One accepted route or workflow transition SHALL
commit its complete related record set atomically; no host-local dictionary or residency state may
be the authority after acknowledgement or restart.

#### Scenario: Durable state is committed
- **WHEN** a workflow transition creates state, wait/timer ownership, a continuation, and an outbound event
- **THEN** the provider commits the instance/checkpoint and every related ownership/outbox record as one atomic batch or commits none

#### Scenario: Definition fanout is accepted
- **WHEN** ingress accepts a definition-scoped fanout route
- **THEN** the provider atomically persists the complete current nonterminal target snapshot plus independently deduplicated target inbox ownership or rejects before partial ownership

#### Scenario: Start-or-deliver is accepted before a definition host exists
- **WHEN** callback-only ingress accepts an exact-definition route
- **THEN** the provider persists the input-bound pending start intent, event, and handoff without requiring a local definition or inferring workflow input from event payload

### Requirement: Outbox delivery is adapter-driven and at-least-once
Outbound durable application events SHALL flow through the external outbox pump and the
application-shaped `IWorkflowEventDispatcher`, never through a provider record callback or broker
SDK owned by OrcaCore. The dispatcher SHALL receive only `WorkflowOutboundEvent` and return the
closed `Succeeded`, `RetryableFailure`, or `PermanentFailure` result. Success marks the record
dispatched; retryable failure or exception retains retryability; cancellation releases the claim;
permanent failure commits observable poison. Ambiguous send recovery SHALL reuse the same outbound
`EventId`. Internal continuation records SHALL use their own runtime pump and SHALL never reach the
application dispatcher.

#### Scenario: Local commit succeeds before external delivery
- **WHEN** workflow progression and one outbound application event commit before the broker send
- **THEN** dispatcher retry proceeds independently with the same complete event identity and cannot roll back the committed workflow transition

#### Scenario: Host fails after broker send
- **WHEN** external dispatch succeeds but the host fails before committing its acknowledgement
- **THEN** the next claim may resend the same `EventId` and the system makes no exactly-once transport claim

#### Scenario: Internal continuation is claimed
- **WHEN** the continuation pump claims runtime-owned work
- **THEN** no `IWorkflowEventDispatcher` invocation or external broker mapping occurs

### Requirement: Durable payloads cross boundaries through explicit serializers
Every persisted workflow input, state, output, accepted-event payload, pending start input, and
outbound-event payload SHALL use the single fixed `orcacore-json-v1` workflow value codec with
declared-type graph validation before commit. Event contract descriptors SHALL bind payload type and
schema identity explicitly. Provider-native JSON, configurable serializer/converter hooks, CLR or
assembly type names, and application-selected content types SHALL NOT define persisted workflow
value identity. Provider record framing MAY use provider-owned protocol serialization but SHALL NOT
alter or reinterpret fixed-codec value bytes.

#### Scenario: Persisted payload is read after restart
- **WHEN** a replacement host rehydrates workflow state, a pending event, start intent, or outbound record
- **THEN** fixed-codec validation/materialization reproduces the declared value without provider-specific or application-replaceable serialization

#### Scenario: Unsupported value graph reaches ingress or publish
- **WHEN** a declared/runtime graph contains unsupported polymorphism, converter policy, cyclic shape, or unapproved collection representation
- **THEN** validation fails before event ownership, start intent, workflow progression, or outbox commit

### Requirement: Retention and purge preserve runtime correctness
Provider-owned retention, archival, and physical cleanup SHALL be distinct from active-memory
eviction and SHALL never remove active instances, pending or claimed inbox events, fanout target
ownership, unresolved start intents, internal continuations, retryable/claimed external outbox
records, lease obligations, confirmation tombstones, poison evidence required by policy, or audit
state needed for supported inspection. Public application `Archive` and `Purge` members SHALL remain
deferred and absent. Cleanup SHALL be reachable through a documented host/provider maintenance
owner and certified against live references; a test-only provider helper is insufficient.
Archive timestamps SHALL be provider-owned metadata: an aggregate projection upsert SHALL preserve
an existing archive timestamp, and an event stream without an instance projection SHALL NOT be
reported as successfully archived.

#### Scenario: Old operational artifacts are purged
- **WHEN** provider maintenance evaluates an artifact that has passed the configured retention boundary
- **THEN** it removes the artifact only after proving no live runtime, inbox, outbox, lease, start-intent, poison, or inspection reference requires it

#### Scenario: Application package is inspected
- **WHEN** public API and packed-consumer baselines are checked
- **THEN** no `Archive`, `Purge`, retention-policy, or provider-cleanup member appears on ordinary workflow definitions, references, handles, or snapshots

## ADDED Requirements

### Requirement: Provider implementations certify messaging ownership parity
The in-memory development provider and every shipped durable production provider SHALL implement
the same logical route inbox, target inbox, fanout snapshot, pending start-intent, continuation,
external outbox, poison, and retention ownership contracts. Production-provider certification
SHALL exercise restart and competing-host boundaries against real storage; in-memory-only success
SHALL NOT establish durable support.

#### Scenario: Provider host is replaced after acceptance
- **WHEN** one host accepts an event or commits an outbound record and another host claims the resulting work
- **THEN** the replacement observes the same envelope, route/target ownership, identity fingerprint, retry state, and payload bytes and completes at most one durable transition

#### Scenario: Provider cannot atomically own a fanout snapshot
- **WHEN** the complete target set exceeds the provider's approved atomic acceptance limit
- **THEN** ingress returns `Rejected(FanoutLimitExceeded)` before committing the envelope or any target ownership

### Requirement: First-release provider ownership and schemas are exact
The first release SHALL ship exactly the InMemory development provider and PostgreSQL production
provider named by the package manifest. Provisional RabbitMQ, Redis, shared Relational, SQL Server,
and ZeroMQ provider projects, inactive tests, SDK dependencies, packages, and orphan source roots
SHALL be absent. Broker-specific mapping SHALL remain application/companion-owned through
`IWorkflowEventDispatcher`; an additional durable storage provider, including SQL Server, SHALL
require a future amendment and complete current-port certification. The PostgreSQL first-create
schema SHALL contain the complete current table/index/sequence shape and provider migrations SHALL
contain no compatibility `ALTER TABLE` upgrade DDL or renamed equivalent.

#### Scenario: Repository provider set is inspected
- **WHEN** project, package, source-root, SDK-version, migration, and future-registry guards run
- **THEN** only the two approved v1 providers remain, PostgreSQL initializes the complete schema from its first migration, and every removed provider family has an explicit recoverable disposition

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
NOT retain unowned step-level stuck fields. PostgreSQL periodic pressure collection SHALL derive
event growth and checkpoint lag from a transactionally maintained stream-head/checkpoint join that
includes stream-only artifacts, and SHALL NOT aggregate the append-only event relation on every
host sweep.

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
