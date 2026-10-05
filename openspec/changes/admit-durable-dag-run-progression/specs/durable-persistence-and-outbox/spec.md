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

The durable system SHALL also persist a first-class `dag-run-v1` aggregate with its immutable run input, authored node/dependency graph, workflow bindings, admitted node identities, mapping failures and dependency-blocked closure, child outcomes, cancellation and terminal state through explicit provider boundaries. A DAG run stream SHALL have a distinct typed identity rather than impersonating a workflow instance; this requires an explicit public/advanced stream-identity and provider-record amendment, not plain reuse of today's workflow-only `WorkflowStreamId`. Existing workflow identities and fixed-codec bytes SHALL remain stable. The atomic append boundary SHALL be `IWorkflowEventStore.AppendAsync(ProviderCommitBatch)`, evolved explicitly where its workflow-oriented record shapes cannot express the run aggregate. No `IWorkflowCommitStore` port SHALL be assumed. Each run transition SHALL atomically commit its complete run-related records; separate child workflow stream commits SHALL be reconciled deterministically and SHALL NOT be described as cross-stream atomic writes. Present-null mapped input SHALL be persisted as explicit input presence and the four UTF-8 fixed-codec bytes `null`, never confused with absent input.

#### Scenario: Run survives loss of all local DAG state
- **WHEN** a replacement fenced owner recovers a DAG run after creation or a committed transition
- **THEN** provider records reproduce the run input, graph, bindings, admission and outcomes without caller-owned sets or a host-local dictionary

#### Scenario: Run and child start straddle host failure
- **WHEN** a run stream commits a node-input/start intent and the host fails before the child workflow stream commits
- **THEN** recovery uses deterministic bound child identity to start or reattach exactly that occurrence without claiming a cross-stream transaction

#### Scenario: Mapper successfully returns null
- **WHEN** the approved declared input type accepts a present-null mapped value
- **THEN** the durable bridge commits explicit presence, the fixed-codec null bytes and their fingerprint before child start

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

The reserved `dag-child-start-v1`, `dag-child-join-v1` and `dag-child-cancel-v1` record kinds SHALL use a dedicated runtime pump installed on every durable host, regardless of external-dispatcher registration or DAG role. Both external pumps, including any `IMessageDispatcher` compatibility transport and `IWorkflowEventDispatcher`, SHALL exclude all reserved DAG kinds from their claims and reject a wrong-lane record after claim. A wrong-lane record SHALL be released without Dispatched acknowledgement; retries and poison SHALL preserve unresolved admission until authoritative child reconciliation. The distinct `dag-run-ready-v1` kind SHALL be claimed only by DAG-role hosts with a registered internal ready sink. Hosts without that role SHALL exclude it from claims rather than repeatedly claim/release it. Ready notifications SHALL cross a Durable.Hosting-owned sink seam implemented by Dag.Hosting, preserving the one-way package edge and never invoking a persisted mapper on the durable side.

#### Scenario: Durable host has no application dispatcher or DAG role
- **WHEN** the host claims a reserved DAG child-protocol record
- **THEN** its runtime pump progresses or retains observable retry/poison ownership without external dispatch, while the host never claims a DAG-ready record

#### Scenario: External pump receives a wrong-lane DAG record
- **WHEN** a provider returns a reserved DAG kind despite the external claim filter
- **THEN** the pump releases that claim without invoking either external dispatcher or marking the record Dispatched

#### Scenario: Ready node awaits a capable host
- **WHEN** only hosts without the DAG role are available
- **THEN** ready work remains durable and unclaimed until a DAG-role sink can process it without claim/release churn or false success

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

All three first-release providers SHALL implement the same proposed DAG run, typed stream identity, fenced discovery/recovery, claim partitions and complete atomic run-transition record contract. The exact advanced port and protocol shapes SHALL be approved before source, with explicit application/advanced API baseline changes wherever stream identity is public. PostgreSQL and SQL Server first-create schemas SHALL contain the run aggregate's complete records, indexes and ownership/fence fields without compatibility upgrade DDL; certification SHALL exercise real-storage restart, competing hosts and wrong-lane claims. A workflow-only port or checkpoint projection SHALL NOT be silently treated as DAG storage. No extra provider family, package dependency or friend grant SHALL be introduced for DAG persistence.

#### Scenario: DAG persistence contract is prepared for source
- **WHEN** the public/advanced stream and record shapes are frozen for implementation
- **THEN** every port, protocol, baseline and both production schema changes are explicitly owned by the approved amendment before source begins

#### Scenario: Provider restarts during run progression
- **WHEN** competing production hosts recover a run and claim its runtime work
- **THEN** real-storage certification proves fenced admission, atomic related records and kind-filtered delivery parity with InMemory
