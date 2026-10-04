# 3. Domain Model and Glossary

This document fixes the canonical vocabulary and the conceptual model. All other documents
use these terms with exactly these meanings.

## 3.1 The first-release product axis

The first release has one definition semantic kind, **workflow**, and two explicit execution
modes:

| Execution mode | What it changes |
|------|-----------------|
| `ephemeral` | In-process execution with no restart-survival claim |
| `durable` | Persisted execution, restart-safe waits/timers, rehydration, retention, and version binding |

Mode changes guarantees, not the meaning of shared workflow structure. Durable-only features
must be absent from ephemeral-facing APIs where practical (compile-time separation preferred)
and rejected before execution otherwise. Saga/compensation is a separate future semantic kind,
documented in [document 07](07-requirements-saga.md), and is not a first-release axis or public
surface.

## 3.2 Glossary

### Definitions and authoring

- **Workflow definition** — immutable, versioned, authored description of control flow and
  business steps. Produced by a fluent builder; validated at build time.
- **Typed workflow contract** — an immutable definition contract with one declared input type
  and, for resultful workflows, one declared successful-output type. Reusable typed workflow
  references carry those types; resultless workflows use a one-arity definition/reference
  rather than a phantom output. Starting and DAG composition infer the contract rather than
  accepting untyped payload bags.
- **Definition identity** — validated immutable reference values `DefinitionId` +
  `DefinitionVersion`. `DefinitionId` is factory-created with canonical parse/round-trip;
  `DefinitionId.New()` never returns `Guid.Empty`, parsing its canonical text is invalid, and
  `TryParse` returns false/null for it. `DefinitionVersion` rejects non-positive values and
  provides an explicit initial value.
  Instances bind to both, so language `default(struct)` cannot bypass their invariants.
- **Compiled plan** - the immutable, validated instruction/scope graph produced by the
  `DefinitionCompiler`. Engines execute this plan; they do not rediscover control flow from
  builder objects at runtime.
- **Plan fingerprint** - the canonical hash over compiler format and authored structure:
  node/member kinds and order, validated strong values, referenced step/workflow types, static
  request values, and the fixed codec format. It deliberately excludes delegate IL, selector/
  projector/merge bodies, DI configuration, mapping logic, and external-request construction.
  Changing excluded opaque behavior requires a new `DefinitionVersion`; the version bump is the
  sole v1 detection contract for that change.
- **Fiber** - one linear instruction position with private branch state, lifecycle phase,
  ownership, and scheduling metadata. A fiber is cooperative work, not an operating-system
  thread and not an independently addressable workflow instance.
- **Execution scope** - the runtime-owned parent/child boundary created by root `Parallel`,
  root `ForEach`, or scoped durable resource acquisition. It owns child fibers, blocked obligations,
  committed results, cancellation, join state, and exactly one parent continuation.
- **Quantum** - one bounded runtime-owned turn of a runnable fiber. It ends after one user-step
  invocation, suspension, branch return, failure, or the internal-instruction/time limit.
- **Branch return** - the single reachable terminal instruction of a branch fiber. It
  serializes one typed result for its owning scope; it does not end the workflow.
- **Merge** - the pure synchronous function evaluated at a scope join over a read-only parent
  snapshot and canonical committed result input. It returns the complete replacement parent
  state, may be reevaluated before a winning append, and contributes to exactly one committed
  scope join.
- **Step** — unit of workflow structure. Two families:
  - *Infrastructure (control-flow) steps*: `Init`, `End`, `If`, root `While`, root `Parallel`
    joined by `WhenAll` or `WhenAllOutcomes`, `Wait`, `Delay`/`Timer`, bounded root `ForEach`,
    durable root `ContinueAsNew`, ephemeral transient-pool scopes, and durable scoped
    `AcquireResources`, plus durable workflow-authored `Publish`. Workflow-authored `Cancel` is deferred.
  - *Business steps*: user-defined async units implementing the step contract.
- **Step contract** — a business step receives a typed execution context (business state,
  resumed-event access, execution identity, and cancellation token) and returns a **step
  result** expressing completion, failure, or a dynamically selected event wait. Structural
  authoring owns ordinary waits, branching, joins, timers, leasing, and other orchestration;
  `StepResult.WaitForEvent` exists only when the event/correlation is known after the business
  step runs. Every invocation receives a fixed-codec-detached attempt-local state copy. Mutable
  state may be changed through `StepContext<TState>.State`; immutable/value state is replaced
  through `ReplaceState`. Step results never carry business-state changes, and only a winning
  successful attempt may commit its detached state.
- **Step operation** — one logical visit to one business step. Its runtime-created opaque
  `StepOperationId` remains stable across retries, timeout handling, crash recovery, replay,
  and competing durable hosts. A new loop visit, parallel branch occurrence, `ForEach` item,
  or continue-as-new generation receives a new operation ID. `AttemptNumber` identifies the
  durable retry-policy ordinal and is diagnostic only: physical redispatch after host loss reuses
  it, and only a committed eligible failure/timeout retry transition advances it. It is never an
  external-effect idempotency key.
- **Decorator / policy** — a behavior modifier attached to the immediately preceding eligible
  business step.
  V1 exposes `WithRetry(maxAttempts, fixedDelay?)`, `WithStepTimeout(timeout)`, and the
  ephemeral-only transient-pool guard. Retry counts the initial attempt and has only an optional
  fixed delay. Duplicate or misplaced decorators fail authoring. A persisted durable resource
  acquisition and whole-workflow `CompleteWithin` are structural, not step decorators.
- **Public runtime failure** — every public runtime exception derives from `OrcaCoreException`
  and exposes one nonblank stable `Code`. Built-in classes bind fixed codes; normalized arbitrary
  author/integration exceptions use the documented generic code. CLR type names and messages are
  diagnostics, not protocol identity.

### Named matching values

A text value that must match across authoring and configuration, two application call sites,
two hosts, or application and provider boundaries SHALL be represented by a named immutable
type rather than a raw `string`. The initial family is `EventName`, `WorkflowOutcomeName`,
`AuthoredBranchId`, `DagNodeId`, `ResourcePoolName`, `TransientPoolName`,
`StartIdempotencyKey`, `InstanceId`, `CorrelationId`, `EventId`, `WaitId`, `AuthoredLocation`,
`DefinitionFingerprint`, `PayloadFingerprint`, `StepOperationId`, `LeaseProtectionToken`,
`StopConfirmationId`, `ResourcePoolOperationId`, and `ResourceGovernancePartitionId`.

Each named type has one validating construction family, exposes its scalar value read-only, and
has no implicit conversion from or to `string`. Caller-created string-backed values --
`EventName`, `WorkflowOutcomeName`, `AuthoredBranchId`, `DagNodeId`, `ResourcePoolName`,
`TransientPoolName`, `StartIdempotencyKey`, `CorrelationId`, `EventId`, `StopConfirmationId`,
`ResourcePoolOperationId`, and `ResourceGovernancePartitionId` -- have private constructors and
one public `Create(string)` factory. They expose no public constructor, `New`, `Parse`/`TryParse`,
raw-string overload, or construction alias. Runtime-created identities instead expose their
approved canonical `Parse`/`TryParse` paths; `DefinitionId` retains `New` plus parsing, and
`DefinitionVersion` retains its validating integer constructor and `Initial` value.
`InstanceId`, `WaitId`, and `DagRunId` reject the canonical `Guid.Empty` text through the same
throw/false-null parser rule as `DefinitionId`.

String-backed construction rejects null, empty, all-whitespace, and leading/trailing-whitespace
values rather than trimming or otherwise normalizing them.
Equality, hashing, routing, persistence, and provider lookup use exact ordinal case-sensitive
semantics. JSON and protocol converters encode the value as one scalar string. Every provider
SHALL preserve the same equality and any limit explicitly specified for that type; a provider
SHALL NOT introduce a smaller value space and provider-default collation MUST NOT change the
contract. `default`/null is rejected again at every public and advanced seam because language
defaults can bypass construction.

`AuthoredBranchId` identifies a branch in the authored graph. It is distinct from any internal
composite branch address, `FiberId`, `ScopeId`, or scope-entry occurrence. `WorkflowOutcomeName`
is optional fixed metadata authored on a successful root completion; runtime state does not
select among dynamic outcome names. Dynamic classification belongs in the typed workflow
output. Unnamed completion is represented by absence, not an empty/default name.

### Instances and state

- **Workflow instance** — one logical execution of a definition. Identified by a globally
  unique, stable **`InstanceId`** that is independent of process, thread, memory object, or
  activation. Optional identity extensions: `StartIdempotencyKey` (caller-selected durable
  start-deduplication identity, distinct from `InstanceId`),
  `ParentInstanceId` and `RootInstanceId` (child-workflow lineage), and a monotonic
  **execution epoch / stream version** for concurrency control.
- **Runtime state** — engine-owned orchestration metadata: lifecycle status, execution
  position, branch/join state, active waits, accepted-event records, consumed event IDs, timer
  subscriptions, failure details, timestamps, version/epoch.
  Always queryable without deserializing business state.
- **Business state** — workflow-owned, typed (`TState`) application data encoded by the fixed
  `orcacore-json-v1` codec. The runtime gives each attempt a codec-detached copy of the last
  committed root/branch/item state. A successful attempt's winning commit replaces committed
  state; failed, timed-out, fenced, and token-ignoring late attempts are discarded. A retry
  starts from the last committed copy even when an older physical invocation is still returning.
  A durable leased retry is stricter: it cannot overlap a still-running prior in-process body;
  host-loss retry remains possible with the same operation/protection identity and reserved
  tickets.
- **Snapshot** — immutable, metadata-only view of an instance returned by public APIs
  (`InstanceId`, `DefinitionId`, `DefinitionVersion`, `Status`, timestamps, error summary…).
  Live internal instances are never exposed. Business state is read via an explicit typed
  accessor that returns a copy.
- **Activation** — the disposable in-memory execution object for an instance. Cache, not
  identity; evictable at safe boundaries; reconstructable by rehydration.
- **Rehydration** — deterministic reconstruction of an executable activation from durable
  state (checkpoint + stream tail + wait records + definition identity). Requires no prior
  in-memory references.

### Lifecycle

- **Workflow status** — the shared `WorkflowInstanceStatus` set: `Pending`, `Running`,
  `Waiting`, `CancellationRequested`, `Completed`, `Failed`, `TimedOut`, `Cancelled`, and
  `Terminated`. `Paused` and `Parked` are not v1 public lifecycle states. Transitions form an
  explicit table with named triggers; illegal triggers are rejected.
- **Terminal state** — `Completed`, `Failed`, `TimedOut`, `Cancelled`, or `Terminated`. V1
  never reopens a terminal instance. Terminal instances
  leave active memory quickly and remain durably
  inspectable per retention policy.

### Events, waits, correlation

- **Event envelope** — the normalized inbound event shape: `EventId` (dedup identity),
  `EventName`, `CorrelationId`, payload, timestamps.
- **`EventName`** — the validated matching contract shared by authored waits, inbound event
  envelopes, facade routing operations, and active-wait projections. It follows the exact
  ordinal semantics defined under named matching values.
- **CorrelationId** — first-class request-reply identity: the same value leaves with an
  outbound request and returns on the response event. Not merely a filter.
- **Wait (wait record)** — engine-owned suspension record: `WaitId`, `EventName`,
  `CorrelationId`, registration timestamp, branch identity, optional deadline, and
  **WaitStatus** (`Active`, `Matched`, `Cancelled`).
- **`Wait`** — the only public event-wait node. It is activation-local in ephemeral mode. In
  durable mode its registration and execution position commit before suspension; the runtime
  may evict the activation immediately and rehydrates it on wake-up. Residency is an engine
  optimization, never an author-selected semantic.
- **Matching rule** — within an instance, events match waits by `EventName` + `CorrelationId`.
- **Routing modes** — a closed `WorkflowEventRoute` carried by the durable inbound envelope:
  *direct instance*, *correlation*, *definition fanout*, or *start-or-deliver*. Correlation resolves
  one active wait; fanout snapshots the complete current nonterminal target set atomically;
  start-or-deliver binds an exact definition/version/idempotency key and fixed-codec workflow input.
- **Pre-wait delivery** — durable ingress accepts and retains ownership before a matching wait
  exists. A later wait claims the persisted record without source redelivery or a hot instance.
  Ephemeral waits are process-local and make no durable acknowledgement promise.
- **Signal-stream semantics** — an event/correlation pair identifies a signal stream, not a
  loop-iteration occurrence. After one wait consumes one event, a later iteration may register
  the same pair and consume a later event. Authors that need occurrence-specific matching encode
  the occurrence in `CorrelationId`.
- **Timer** — a scheduled wake-up modeled as a durable fact plus a scheduled command
  (`TimerScheduled` → `FireTimer`), never a hidden thread timer in durable mode.

### Durable core (event-driven engine)

- **Instance stream** — append-only, per-instance sequence of **workflow events** (durable
  facts such as `WorkflowStarted`, `StepSucceeded`, `WaitRegistered`, `WaitMatched`,
  `BranchCompleted`, `JoinSatisfied`, `WorkflowCompleted`,
  `CompensationStarted`…). The write-side source of truth.
- **Command** — an intent to mutate one instance (`StartWorkflow`, `DeliverEvent`,
  `FireTimer`, `DeleteInstance`…), carrying `CommandId`, routing identity,
  timestamps, and causation/correlation metadata. Commands are not durable truth; events are.
- **Aggregate** — the deterministic in-memory decision model rebuilt from checkpoint + stream
  tail; owns runtime and business state during a command's processing.
- **Checkpoint** — materialized aggregate state at a stream version, bounding replay cost.
- **Projection** — derived provider/operator read model (instance summaries, active waits, accepted-
  event audit, history, and operational statistics) serving keyed routing and operations; it does
  not create broad application enumeration.
- **Inbox** — durable record of accepted external deliveries by global `EventId`, with per-target
  ownership for fanout and states that progress to applied or observably poisoned; provides
  restart-safe pre-wait retention and deduplication.
- **Outbox** — durable records of runtime-owned continuations, lifecycle/status, timer, internal DAG
  child intents, and workflow-authored outbound events derived in the same commit. Public workflow
  events reach only `IWorkflowEventDispatcher`; internal continuations never do.
- **DAG definition / run** — a typed acyclic graph authored through the separate
  `OrcaCore.Dag` package and driven by `OrcaCore.Dag.Hosting`. Each resultful or resultless node
  references a typed durable workflow and executes as its own child workflow instance. A node
  input projector may consume the immutable DAG-run input and typed outputs of direct successful
  resultful dependencies only; resultless nodes may be dependencies but cannot be passed to
  `OutputOf`. Internal child dispatch is a DAG implementation detail; no public
  `RunChild`/`RunChildren` node is part of the first release.

### Management and operations

- **First-release management surface** — typed definition handles register/start workflows;
  typed instance handles return immutable instance/active-wait snapshots, fixed-codec-detached
  committed root state, and typed output where declared, and expose cooperative cancellation
  request plus termination. Resultful workflow handles/start results expose notification-driven
  typed output waiting, and DAG-run handles expose notification-driven terminal-snapshot
  waiting; both use subscribe/recheck without polling, and caller cancellation is local to the
  wait. Durable `IWorkflowEventIngress` owns the closed four-route event union. Broad fluent
  selection, public instance enumeration/bulk retrieval, pause/resume, failed-instance retry,
  archive, and purge remain absent from the application surface; provider/operator maintenance
  owns retained statistics and retention policy.
- **Lifecycle events** — first-class notifications for instance and step transitions
  (created, activated, suspended, resumed, completed, failed, timed out, cancelled,
  terminated, evicted, stuck-detected; step scheduled/started/completed/failed/retried/
  timed-out/cancelled).
- **Stuck detection** — threshold-based identification of steps/instances without progress,
  producing observable signals and queryable results.
- **Retention** — the separated policy set: active-memory eviction ≠ durable retention ≠
  archival ≠ hard deletion.
- **Resource pool / ticket** — capacity governance in two tiers: *ephemeral-only transient
  in-process pools*, named by `TransientPoolName`, bound step execution within one host and vanish on
  restart; *durable pools*, named by the distinct `ResourcePoolName`, issue **tickets** held by
  capacity-consuming work across waits and restarts — e.g. a Kubernetes Job holding
  database connections. `ResourceLeaseRequirement` combines one `ResourcePoolName` with a
  positive integer unit count through a factory-only immutable shape. One scoped structural
  `AcquireResources(request, body)` acquires its non-empty, duplicate-free requirement set
  atomically at the durable root, a root `If`/`While` nested body, or an independent durable
  root-`Parallel` branch/root-`ForEach` item when no live ancestor lease exists. A leased body
  may sequence and decorate steps, nest `If`, wait, and delay, but exposes no `Parallel`, `ForEach`, or `While`
  and cannot acquire another lease. The scope behaves like a wait when capacity is unavailable,
  holds across suspension inside its body, and releases when that body exits normally or
  failure/cancellation is causally proven pre-effect.
- **Lease scope / obligation** — one authored acquisition-scope occurrence, identified in the
  runtime protocol by an opaque runtime-generated `LeaseObligationId`. Its ownership record
  also binds the instance, continue-as-new generation, authored node, fiber occurrence, and
  scope-entry occurrence; `AuthoredLocation` or a reusable holder string alone is not a runtime
  identity. Its normative lifecycle is `Queued -> PendingCommit -> Held -> ReviewMarked ->
  AmbiguousHeld -> Quarantined -> Released`, with cancelled-before-grant and `LeaseLost` terminal
  side paths. A retryable timeout, ambiguous submission, or recovered in-flight attempt remains
  `AmbiguousHeld` while the lexical owner is recoverable, retaining the same `StepOperationId`,
  `LeaseProtectionToken`, tickets, and capacity. A successful retry alone does not erase the
  ambiguity. Ambiguous scope exit, exhaustion, cancellation, workflow deadline, forced
  termination, or abandonment transfers to `Quarantined` before progress; only trusted stop
  proof or an end-to-end fence releases it. The record carries the exact ticket set plus a
  provider ownership generation used for fenced, idempotent grant, cancellation,
  reconciliation, and release. `LeaseObligationId` is not caller-supplied and is not an
  application-authoring type.
- **Review mark / reconciliation** — a pool-configured review deadline is an auditable signal to
  reconcile a ticket with its exact committed owner state, not a TTL or proof that ownership
  ended. Marking for review does not free capacity. A live or ambiguous owner remains held; only a
  causally proven released/never-committed obligation, or a terminal owner with proven cleanup
  plus confirmed protected-work stop/end-to-end fence, may be recovered automatically. Bare
  terminal status remains capacity-reserving quarantine. Holder-driven renewal is not part of
  the baseline.
- **Lease protection / stop confirmation** — a runtime-created round-trippable
  `LeaseProtectionToken` binds external work to one exact lease obligation. A trusted host
  reconciler uses caller-created idempotent `StopConfirmationId` through the advanced generic
  `IDurableResourceLeaseRecovery` seam only after token-bound work is proven terminal, absent,
  or fenced. Neither value is workflow author identity or infrastructure-specific.
- **Lease diagnostics** — trusted in-process advanced host management uses
  `IDurableResourceLeaseDiagnostics` to enumerate outstanding obligations and query one exact
  `LeaseProtectionToken`. Immutable projections correlate workflow owner, authored scope,
  lifecycle/confirmation state, pool/units, review state, and provider generation. This is not
  ordinary application bulk management; any remote endpoint supplies authorization and
  redaction.
- **Resource-governance aggregate** — one serialized durable aggregate per configured
  `ResourceGovernancePartitionId`, owning every pool definition, atomic multi-pool request,
  queue position, ticket, review mark, resize operation, stop-confirmation binding, and
  tombstone in that partition. `IDurableResourceGovernanceStore` loads the partition stream and
  performs one expected-version append. The full stream factory defensively copies and validates
  exact `1..Version` continuity; append validates and copies one non-empty
  `expectedVersion + 1..N` batch and commits all or conflicts. Four friend-only post-commit
  certification barriers cover workflow pending obligation, governance reservation, workflow
  activation, and governance ownership confirmation with immutable correlated ownership/ticket
  facts. Pool resize returns closed `Applied`/`Conflict`; `ListAsync`/`GetAsync` remain trusted
  pool administration. A per-workflow-stream-only provider cannot satisfy v1 durable leasing.

## 3.3 Conceptual layering

```text
Layer 1 — Abstractions   contracts: typed workflow references, steps, results, envelopes,
                         waits, statuses, snapshots, fixed-codec payload contracts
Layer 2 — Runtime        the engines: interpretation/decision, scheduling, routing,
                         serialization of instance execution, management surface
Layer 3 — Providers      adapters: persistence (event store, projections, inbox/outbox),
                         message dispatch, timer scheduling — relational/document DBs,
                         RabbitMQ/SQS/Kafka, etc.
Layer 4 — Companions     outward integrations and applications (for example Kubernetes/AWS
                         job schedulers) that depend on OrcaCore; OrcaCore never depends on them
```

Dependency rule: Runtime depends on Abstractions; Providers implement advanced ports;
`OrcaCore.Dag` depends directly only on the `OrcaCore` package and consumes public workflow
references. The independently approved post-gate `admit-dag-authoring-friend-boundary` contract names one
authoring-only `OrcaCore -> OrcaCore.Dag` internal friend for compiler-created validation,
diagnostic, location, fingerprint, and definition-exception values; the grant is compiled in the
independently approved Task 8.2 checkpoint `a9f835f939d683500ca231c7ba491ab8eae2aaae`.
`OrcaCore.Dag.Hosting` alone consumes the named internal durable child-start/join bridge.
The independently approved `admit-dag-hosting-runtime-view` contract specifies only
`OrcaCore.Dag -> OrcaCore.Dag.Hosting` for immutable descriptors and one evaluator,
guarded at exact non-public type/member signatures. Its contract is independently approved at `43d869fc29e7daa3ec567d4602960f458eb98492` and canonical at independently approved checkpoint `a0da21ba9597e3864a3d4134120fbb0138417bd7`; compiled and independently source-approved at checkpoint `4eb2e8d3a68e0ae7d873bd4f54c53735beefb132`, with direct-child evidence `2fdfa59a747a5d1d1667abd63a8def88168a172b`, and
does not widen the authoring grant. The durable bridge decodes successful committed outputs
before evaluation and owns fixed-codec input normalization, fingerprints, commit and child start.
Companions may depend on OrcaCore, `OrcaCore.Dag`, and infrastructure SDKs. No OrcaCore package
references a Kubernetes, AWS, or scheduler companion. User workflow code references only
application contracts plus the authoring package it selected.
