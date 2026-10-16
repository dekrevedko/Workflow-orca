## Purpose

Define the persistence, outbox, serialization, and retention capabilities required by OrcaCore durable execution.

## Requirements

### Requirement: Workflow store persists first-class durable records
The durable system SHALL persist workflow instances, checkpoints, waits, timers, route-level and
per-instance inbox records, consumed-event identity, fanout target snapshots, pending start intents,
internal continuations, external outbox records, poison/dead-letter state, history, and operational
projections through explicit provider boundaries. One accepted route or workflow transition SHALL
commit its complete related record set atomically; no host-local dictionary or residency state may
be the authority after acknowledgement or restart.

The system SHALL persist distinct typed dag-run-v1 streams, immutable graph/input/bindings, node input/output presence and bytes, failures/blocking, cancellation and terminal facts. WorkflowStreamId SHALL preserve workflow signatures/rendering and add DurableAggregateKind (Workflow=0, DagRun=1), AggregateKind, Guid? DagRunId and ForDagRun(Guid); empty/cross-kind collisions and workflow-only use of DAG streams SHALL fail closed. ProviderCommitBatch SHALL add DagRunAppendV1? DagRun, require it for DAG streams, reject workflow-only writes there and use the real IWorkflowEventStore.AppendAsync(ProviderCommitBatch). The 21 WorkflowEventCodec entries and orcacore-json-v1 bytes SHALL stay unchanged; separate DAG version-1 record/checkpoint framing SHALL reject unknown, extra, missing or inconsistent fields. Exact typed lease/load/discovery/subscription operations SHALL serialize run ownership; append SHALL compare current live owner/token/expiry and expected version, then atomically commit records, operation receipt, checkpoint, node/index and subscriber/outbox writes or none. Exact operation replay SHALL reuse its receipt; divergent reuse SHALL fail. DAG caller start keys SHALL bind atomically in a separate DAG namespace; child starts SHALL NOT use caller keys. Present-null SHALL commit presence and four UTF-8 bytes null. Separate child-stream commits SHALL reconcile deterministically, never claim cross-stream atomicity.

Run ownership SHALL use provider-persisted FIFO acquisition requests with stable request/owner/purpose/sequence, 30-second waiting expiry and retained consumed receipts. Bridge-only BeginTurn/RenewTurn/EndTurn/AbandonTurnRequest SHALL expose fences without a Dag.Hosting provider-port bypass. A grant SHALL have at most a 5-second lease and an absolute 15-second deadline that renewal cannot extend; one mutation turn SHALL release before idle/child/capacity waits. Compact non-created events SHALL contain only changed-node facts/value writes, with one overwritten full current checkpoint; immutable graph/input SHALL be persisted at creation, never copied into every event.

#### Scenario: Durable state is committed
- **WHEN** a workflow transition creates state, wait/timer ownership, a continuation, and an outbound event
- **THEN** the provider commits the instance/checkpoint and every related ownership/outbox record as one atomic batch or commits none

#### Scenario: Definition fanout is accepted
- **WHEN** ingress accepts a definition-scoped fanout route
- **THEN** the provider atomically persists the complete current nonterminal target snapshot plus independently deduplicated target inbox ownership or rejects before partial ownership

#### Scenario: Start-or-deliver is accepted before a definition host exists
- **WHEN** callback-only ingress accepts an exact-definition route
- **THEN** the provider persists the input-bound pending start intent, event, and handoff without requiring a local definition or inferring workflow input from event payload

#### Scenario: DAG append rejects an expired turn
- **WHEN** a provider write arrives after its short turn's absolute deadline
- **THEN** the provider rejects its old fence without input/start/checkpoint/outbox effects

#### Scenario: Compact DAG tail replays with the current checkpoint
- **WHEN** a run transitions one node after creation
- **THEN** the event stores only changed-node facts while checkpoint-plus-tail reconstructs the same bound graph/input/node state

### Requirement: Outbox delivery is adapter-driven and at-least-once
Outbound durable application events SHALL flow through the external outbox pump and the
application-shaped `IWorkflowEventDispatcher`, never through a provider record callback or broker
SDK owned by OrcaCore. The dispatcher SHALL receive only `WorkflowOutboundEvent` and return the
closed `Succeeded`, `RetryableFailure`, or `PermanentFailure` result. Success marks the record
dispatched; retryable failure or exception retains retryability; cancellation releases the claim;
permanent failure commits observable poison. Ambiguous send recovery SHALL reuse the same outbound
`EventId`. Internal continuation records SHALL use their own runtime pump and SHALL never reach the
application dispatcher.

The runtime SHALL partition one logical outbox into continue-only, workflow-event-only, the three DAG child kinds dag-child-start-v1/dag-child-join-v1/dag-child-cancel-v1, DAG-role-only dag-run-ready-v1, and remaining internal dispatch. Every durable host SHALL install the dedicated three-child-kind pump regardless of dispatcher/DAG role. A role-free host SHALL NOT claim ready records. IMessageDispatcher remains the internal dispatch port, not a compatibility transport or DAG runtime pump; its selector SHALL exclude all six reserved kinds. IWorkflowEventDispatcher SHALL receive only workflow-event. Wrong-lane records SHALL release/reject before callback or acknowledgement, never silently become Dispatched. Trusted owner coordinates SHALL survive malformed payloads; poison/retry SHALL preserve uncertain admission. Ready hints SHALL use existing outbox records with null progression recipient or exact boot-host observer recipient/subscription. Subscribe-and-version-recheck plus atomic append notifications SHALL close lost-wake races; delivery acknowledgement SHALL NOT release admission.

Addressed observer hints SHALL have an exact trusted subscription coordinate. Expired/unsubscribed dead boot-host observers SHALL be pruned within the bounded maintenance interval or after an active claim expires, without removing unaddressed progression, child, poison or uncertain admission records. Observer maintenance SHALL NOT count as business pending-message pressure. Unaddressed ready work SHALL remain recoverable on any capable DAG host.

The three child kinds and unaddressed ready hints SHALL count as internal DAG pressure; addressed observers SHALL have separate operational counters/gauges and SHALL NOT count as external/business backlog. Exact WorkflowOperationalPressure additions and gauge names SHALL use existing operational snapshot/stuck-refresh APIs without adding application enumeration. DAG stream/checkpoint/stuck projections SHALL derive from committed heads and preserve existing workflow semantics.

Unaddressed dag-run-ready-v1 hints SHALL acknowledge bounded validated/coalesced scheduler wake acceptance, not mapper completion or child admission. No ready claim SHALL span mapper evaluation. DAG-role hosts SHALL continuously page authoritative nonterminal-run discovery, wrapping after every full pass with at most five seconds between passes, so acknowledged/lost/poisoned hints and death during evaluation do not erase Ready work. Addressed observer/child acknowledgements SHALL keep their distinct hub/committed-transition rules. DG-P24 SHALL certify these boundaries on real PostgreSQL and SQL Server.

#### Scenario: Local commit succeeds before external delivery
- **WHEN** workflow progression and one outbound application event commit before the broker send
- **THEN** dispatcher retry proceeds independently with the same complete event identity and cannot roll back the committed workflow transition

#### Scenario: Host fails after broker send
- **WHEN** external dispatch succeeds but the host fails before committing its acknowledgement
- **THEN** the next claim may resend the same `EventId` and the system makes no exactly-once transport claim

#### Scenario: Internal continuation is claimed
- **WHEN** the continuation pump claims runtime-owned work
- **THEN** no `IWorkflowEventDispatcher` invocation or external broker mapping occurs

#### Scenario: Wrong runtime lane cannot acknowledge a DAG record
- **WHEN** an application or IMessageDispatcher pump receives a DAG record
- **THEN** it releases/rejects the claim without invoking the transport or marking Dispatched

#### Scenario: Durable host has no DAG role
- **WHEN** the every-host child pump has a terminal child to join
- **THEN** it progresses the join but never claims a dag-run-ready-v1 record

#### Scenario: Dead observer notice is purged without losing progression
- **WHEN** a boot host dies and its subscription and any claim lease expire
- **THEN** bounded maintenance removes only its addressed notices and preserves every unaddressed/child/poison admission record

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

InMemory, PostgreSQL and SQL Server SHALL implement identical typed DAG stream/record/checkpoint, fenced lease/version append, discovery, caller-key binding, node/admission index and subscriptions. Production FIRST-CREATE scripts SHALL include complete dag_runs, dag_run_events, dag_run_operations, dag_run_nodes and dag_run_subscriptions tables, shared-outbox DAG owner/recipient columns, exact ordinal keys, state/version/fence constraints and discovery/claim/subscription indexes. No compatibility ALTER/upgrade or DAG-private transport SHALL be introduced. One transaction SHALL serialize ownership checks and all related records/notifications. Real-storage certification SHALL cover restart, competing owners/limits, append crash points, disjoint selectors, eager decode/null, separate-stream child reconciliation, cancellation/failure closure and notification races; in-memory success SHALL NOT establish durable support.

The complete first-create schema SHALL additionally contain dag_run_lease_requests with stable FIFO sequence, exact boot-owner/request/purpose, live-waiter expiry, absolute grant deadline, consumed receipt retention and one live request per owner/run. All providers SHALL enforce no same-owner bypass and no renewal beyond the absolute deadline. Real-storage DG-P14/DG-P15 SHALL prove remote child joins/cancellation progress behind a continuously renewing owner, with cancellation triggered by another DAG-role host and child cancellation consumed on a role-free host; DG-P16/DG-P17/DG-P18 SHALL prove release-before-idle, orphan-observer cleanup and compact-event/checkpoint parity.

The runtime SHALL evaluate mapping outside turn ownership, retain prepared immutable results and use short fenced recheck-and-append only. Provider transaction time SHALL use the explicit production DB-clock/internal certification-clock seam. Same-host requests SHALL use local arrival FIFO, with OwnerBusy for a second distinct live provider request. Real-storage DG-P19–24 SHALL certify slow valid mapper admission, distinct isolated run evaluation bounds, zero look-ahead and authored-order admission, exact pressure, local/provider arbitration, cross-run slow/stuck isolation and acknowledged/lost/poisoned ready-hint recovery. MaxConcurrentNodes SHALL NOT become a host-global mapper bound.

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
