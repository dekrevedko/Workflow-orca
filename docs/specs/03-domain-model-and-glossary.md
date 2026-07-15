# 3. Domain Model and Glossary

This document fixes the canonical vocabulary and the conceptual model. All other documents
use these terms with exactly these meanings.

## 3.1 The two product axes

| Axis | Values | What it changes |
|------|--------|-----------------|
| **Definition semantics** | `regular workflow` \| `saga` | Business meaning of failure, cancellation, and completion (sagas add compensation) |
| **Execution mode** | `ephemeral` \| `durable` | Runtime guarantees: restart survival, rehydration, durable waits/timers, retention, versioning |

The four combinations are all valid, with explicit support levels:

| Combination | Support level |
|-------------|---------------|
| Ephemeral regular workflow | Full (simplest mode) |
| Durable regular workflow | Full (primary production mode) |
| Ephemeral saga | **Limited** — compensation within one process lifetime only; no durable recovery; explicitly labeled reduced-guarantee |
| Durable saga | Full (most complete saga behavior; highest requirements) |

Durable-only features must be absent from ephemeral-facing APIs where practical (compile-time
separation preferred) and rejected fast at runtime otherwise.

## 3.2 Glossary

### Definitions and authoring

- **Workflow definition** — immutable, versioned, authored description of control flow and
  business steps. Produced by a fluent builder; validated at build time.
- **Saga definition** — a separate semantic definition kind with compensation scopes,
  compensation handlers, and saga-specific terminal states. Not a flag on a workflow.
- **Definition identity** — `DefinitionId` + `DefinitionVersion`. Instances bind to both.
- **Compiled plan** - the immutable, validated instruction/scope graph produced by the
  `DefinitionCompiler`. Engines execute this plan; they do not rediscover control flow from
  builder objects at runtime.
- **Plan fingerprint** - the canonical hash over compiler format, graph structure, policies,
  types, and configuration. Durable envelopes bind to it so same-version graph drift parks
  instead of resuming under different semantics.
- **Fiber** - one linear instruction position with private branch state, lifecycle phase,
  ownership, and scheduling metadata. A fiber is cooperative work, not an operating-system
  thread and not an independently addressable workflow instance.
- **Execution scope** - the runtime-owned parent/child boundary created by `Parallel`,
  `WhenFirst`, or ephemeral `ForEach`. It owns child fibers, their blocked obligations,
  committed results, cancellation, join state, and exactly one parent continuation.
- **Quantum** - one bounded turn of a runnable fiber. It ends after one user-step invocation,
  suspension, branch return, failure, cooperative yield, or the internal-instruction limit.
- **Branch return** - the single reachable terminal instruction of a branch fiber. It
  serializes one typed result for its owning scope; it does not end the workflow.
- **Merge** - the pure synchronous function evaluated at a scope join over a read-only parent
  snapshot and canonical committed result input. It returns the complete replacement parent
  state and runs at most once per committed scope join.
- **Step** — unit of workflow structure. Two families:
  - *Infrastructure (control-flow) steps*: `Init`, `End`, `If`, `While`, `Parallel`,
    `WhenAll`, `WhenFirst`, `Wait`, `WaitLong` (durable-only), `Delay`/`Timer`, `ForEach`
    (ephemeral), `RunChild`/`RunChildren` (durable), `Publish`, `Cancel`; saga-specific:
    `CompensationScope`, `Compensate`, `Try`/`Catch`/`Finally`.
  - *Business steps*: user-defined async units implementing the step contract.
- **Step contract** — a business step receives a typed execution context (business state,
  resumed-event access, cancellation token) and returns a **step result** expressing control
  intent only: `Completed`, `Failed`, `WaitForEvent`, `Yield` (extended by later phases,
  e.g. publication requests). Steps mutate business state directly through the context;
  step results never carry business-state changes (one concern per channel).
- **Decorator / policy** — a behavior modifier attached to a step, scope, or definition
  (retry, timeout, cancellation, compensation binding, idempotency, visibility, durability
  requirement, resource-pool hint). Policies modify behavior; they never replace structure.

### Instances and state

- **Workflow instance** — one logical execution of a definition. Identified by a globally
  unique, stable **`InstanceId`** that is independent of process, thread, memory object, or
  activation. Optional identity extensions: `StartIdempotencyKey` (client reference),
  `ParentInstanceId` and `RootInstanceId` (child-workflow lineage), and a monotonic
  **execution epoch / stream version** for concurrency control.
- **Runtime state** — engine-owned orchestration metadata: lifecycle status, execution
  position, branch/join state, active waits, pending events, consumed event IDs, timer
  subscriptions, compensation stack (sagas), failure details, timestamps, version/epoch.
  Always queryable without deserializing business state.
- **Business state** — workflow-owned, typed (`TState`), serializable application data.
  Mutated only by steps; never by the runtime.
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

- **Workflow status** — shared set (both modes): `Running`, `Waiting`, `Completed`,
  `Failed`, `Cancelled`, `Terminated`; durable mode adds `Paused`; sagas add `Compensated`,
  `CompensationFailed`. Transitions form an explicit table with named triggers; illegal
  triggers are rejected; terminal states reject all triggers.
- **Terminal state** — `Completed`, `Failed`, `Cancelled`, `Terminated`, `Compensated`,
  `CompensationFailed`. Terminal instances leave active memory quickly and remain durably
  inspectable per retention policy.

### Events, waits, correlation

- **Event envelope** — the normalized inbound event shape: `EventId` (dedup identity),
  `EventName`, `CorrelationId`, payload, timestamps.
- **CorrelationId** — first-class request-reply identity: the same value leaves with an
  outbound request and returns on the response event. Not merely a filter.
- **Wait (wait record)** — engine-owned suspension record: `WaitId`, `EventName`,
  `CorrelationId`, registration timestamp, branch identity, optional timeout. Two axes:
  - **WaitStatus** (lifecycle): `Active`, `Matched`, `Cancelled`.
  - **WaitMode** (residency): `Resident` (instance may stay hot) vs `Cold` (instance is
    evictable immediately after the wait is durably registered).
- **`Wait`** — resident-mode wait; durable in durable mode, activation-local in ephemeral.
- **`WaitLong`** — durable-only cold wait: checkpoint then evict; resume rehydrates.
- **Matching rule** — within an instance, events match waits by `EventName` + `CorrelationId`.
- **Routing modes** — how an event reaches an instance (a caller decision, not an envelope
  property): *instance-targeted*, *correlation-targeted* (must resolve to exactly one
  instance), *definition-targeted fanout*.
- **Mailbox / pending events** — per-instance buffer for events arriving before their wait
  exists; matching is bidirectional (events find waits; new waits find buffered events).
- **Timer** — a scheduled wake-up modeled as a durable fact plus a scheduled command
  (`TimerScheduled` → `FireTimer`), never a hidden thread timer in durable mode.

### Durable core (event-driven engine)

- **Instance stream** — append-only, per-instance sequence of **workflow events** (durable
  facts such as `WorkflowStarted`, `StepSucceeded`, `WaitRegistered`, `WaitMatched`,
  `EventBuffered`, `BranchCompleted`, `JoinSatisfied`, `WorkflowCompleted`,
  `CompensationStarted`…). The write-side source of truth.
- **Command** — an intent to mutate one instance (`StartWorkflow`, `DeliverEvent`,
  `FireTimer`, `CompensateSaga`, `DeleteInstance`…), carrying `CommandId`, routing identity,
  timestamps, and causation/correlation metadata. Commands are not durable truth; events are.
- **Aggregate** — the deterministic in-memory decision model rebuilt from checkpoint + stream
  tail; owns runtime and business state during a command's processing.
- **Checkpoint** — materialized aggregate state at a stream version, bounding replay cost.
- **Projection** — derived read model (instance summaries, active waits, pending events,
  history, saga compensation state) serving queries and routing.
- **Inbox** — durable record of received external deliveries by `EventId` with states
  `Received` / `Applied` / `DuplicateIgnored` / `Poisoned`; provides restart-safe dedup.
- **Outbox** — durable records of outbound publication intent derived from committed workflow
  events in the same commit boundary; dispatched asynchronously, retryably, at-least-once.

### Management and operations

- **Management surface** — fluent, LINQ-like API separate from workflow structure:
  scope selection (engine-wide / definition / instance / step) → constrained filtering
  (`Where(...)`) → terminal query (`List`, `Count`, `Statistics`, `GetHistory`,
  `GetActiveWaits`…) or terminal command (`Pause`, `Resume`, `RaiseEvent`, `Cancel`,
  `Terminate`, `Retry`, `Archive`, `Purge`…).
- **Lifecycle events** — first-class notifications for instance and step transitions
  (created, activated, suspended, resumed, completed, failed, timed out, cancelled,
  terminated, evicted, stuck-detected; step scheduled/started/completed/failed/retried/
  timed-out/cancelled/compensated).
- **Stuck detection** — threshold-based identification of steps/instances without progress,
  producing observable signals and queryable results.
- **Retention** — the separated policy set: active-memory eviction ≠ durable retention ≠
  archival ≠ hard deletion.
- **Resource pool / ticket** — capacity governance in two tiers: *transient in-process
  pools* bound step execution within one host and vanish on restart; *durable pools* issue
  **tickets** (durable leases: pool, holder, acquired-at, expires-at) held by
  capacity-consuming work across cold waits and restarts — e.g. an external job holding
  database connections. Acquisition is declared as a decorator, behaves like a wait when the
  pool is exhausted, and releases symmetrically on every terminal path.

## 3.3 Conceptual layering

```text
Layer 1 — Abstractions   contracts: steps, results, envelopes, waits, statuses,
                         snapshots, dispatch models, serialization seams
Layer 2 — Runtime        the engines: interpretation/decision, scheduling, routing,
                         serialization of instance execution, management surface
Layer 3 — Providers      adapters: persistence (event store, projections, inbox/outbox),
                         message dispatch, timer scheduling — relational/document DBs,
                         RabbitMQ/SQS/Kafka, etc.
```

Dependency rule: Runtime depends on Abstractions; Providers implement Abstractions;
never the reverse. User workflow code references only Abstractions (plus builders).
