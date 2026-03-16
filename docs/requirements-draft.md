# Requirements Draft

Derived from comparative research on March 15, 2026.

## Product direction

OrcaCore should be an embeddable .NET 10 workflow engine for application-local orchestration.

Persistence should be optional, not mandatory. The engine must support two orthogonal axes:

- definition semantics:
  - regular workflow
  - saga workflow
- execution mode:
  - durable mode, when a persistence provider is configured
  - ephemeral mode, when no persistence provider is configured

The available guarantees depend on the configured mode and must be explicit.

## Runtime modes

### Durable mode

When a persistence provider is configured, the engine may provide durable suspension, rehydration, post-restart resume, durable inspection, retention, and long-running coordination guarantees.

### Ephemeral mode

When no persistence provider is configured, the engine should still support in-process workflow execution, but only with non-durable guarantees.

In ephemeral mode:

- workflow instances exist only in memory
- process restart loses instance state
- durable rehydration is unavailable
- `WaitLong` is unavailable from ephemeral-facing APIs or rejected by configuration
- retention and historical inspection are memory-bound only
- active eviction must not destroy instances that still need to exist logically

## Primary use cases

### 1. In-app business workflow

An application defines a workflow in code using reusable infrastructure steps and domain-specific business steps.

### 2. Event-driven resume

A workflow pauses while waiting for an external event, then resumes later when the event arrives.

### 3. Long-running coordination

A workflow survives process restarts and long waiting periods without losing progress when durable mode is enabled.

### 4. Parallel coordination

A workflow can spawn parallel branches and later continue when all branches or the first branch completes.

## Functional requirements

### Workflow definition

- The engine must support code-first workflow definitions in .NET.
- The engine must support distinct semantic definition kinds for regular workflow and saga workflow.
- A workflow definition must be composed of reusable steps.
- The engine must support at least these shared infrastructure step types:
  - `Init`
  - `End`
  - `If`
  - `While`
  - `Parallel`
  - `WhenAll`
  - `WhenFirst`
  - `Wait`
  - `WaitLong`
- The engine should keep saga-specific compensation primitives separate from regular workflow primitives.
- The engine must support user-defined business steps.
- Steps should support optional timeout policy configuration.
- All step execution APIs must be async.

### Workflow instance model

- A workflow instance must contain runtime state and business state.
- Runtime state must be owned by the engine and describe orchestration progress.
- Business state must be owned by the workflow and be serializable/deserializable across persistence boundaries when persistence exists.
- The engine must allow business state to survive suspension and later resumption in durable mode.
- A workflow instance must have a stable identity independent from its current in-memory activation.
- A durable workflow instance must be bound to a workflow definition version.

### Runtime execution

- The engine must create and execute workflow instances from workflow definitions.
- The engine must support branch tracking for parallel execution.
- The engine must support completion, failure, cancellation, suspension, timeout, and termination states.
- The engine must guarantee serialized execution per workflow instance by default.
- Concurrent attempts to advance the same workflow instance must resolve as a documented serialized outcome.
- Idle in-memory instances must be safely evictable without losing correctness in durable mode.
- Completed, terminated, canceled, and faulted instances must be eligible for active-memory eviction after terminal handling is complete.
- Meaningful state transitions must be persisted when durable mode is enabled.
- Suspension and later post-restart resumption require durable mode.
- Orchestration control logic and side-effecting execution must be separated by a clear runtime contract.

### Waiting and events

- The engine must support waiting for external events.
- The engine must support resuming a specific workflow instance from a correlated event.
- The engine must support waiting for any of multiple events.
- The engine must support waiting for all of multiple events.
- The engine must support publishing events from running workflows.
- The engine must support short-lived waits in all modes.
- The engine must support durable long-lived waits only when durable mode is enabled.
- If no persistence provider is configured, unsupported durable features must fail fast with clear diagnostics.
- Durable timers or equivalent durable wake-up primitives must exist in durable mode.
- Unresolved runtime-owned waits and timers must participate in documented completion and cancellation semantics.

### Persistence and providers

- Persistence provider support must be optional.
- Event delivery must be abstracted behind provider interfaces.
- The engine must allow different database implementations when persistence is enabled.
- The engine must allow different messaging/event implementations such as SQS, RabbitMQ, and Kafka.
- An in-memory runtime mode should exist for tests and local experimentation without a persistence provider.
- If persistence is enabled, runtime metadata must remain queryable without requiring deserialization of business payload only.
- If persistence is enabled, business state may be serialized, but waits, lifecycle state, branch state, and correlation metadata must remain first-class persisted data.
- If persistence is enabled, providers must support the engine's per-instance concurrency contract through optimistic concurrency, leases, or an equivalent documented mechanism.
- If persistence is enabled, provider contracts must define retention, cleanup, and active-eviction related invariants.
- If replay or history-based recovery is used, history growth and checkpoint pressure must be observable.

### Lifecycle, timeouts, and resource management

- The engine must track lifetime metadata for workflow instances.
- The engine must track lifetime metadata for executing steps.
- The engine must support detection of stuck steps and stuck workflow instances.
- The engine must support configurable timeout policy for steps.
- The engine must support configurable retention and cleanup policy for terminal instances in durable mode.
- The engine must support active-instance eviction policy for idle in-memory instances in durable mode.
- The engine must distinguish active-memory eviction from durable deletion when persistence is enabled.

### Management and inspection

- The engine must expose a management command surface separate from workflow graph primitives.
- The management command API should be fluent, composable, and LINQ-like.
- The management command API should separate:
  - scope selection
  - filtering
  - terminal query operations
  - terminal command operations
- The engine should support at least these management capabilities where applicable:
  - definition-scoped creation: `Start`, `StartOrGet`
  - selection filters such as `All()`, `Where...()`, `WhereStatus(...)`
  - terminal queries such as `List()`, `Count()`, `Statistics()`, `GetActiveWaits()`, `GetHistory()`
  - terminal commands such as `Pause()`, `Resume()`, `RaiseEvent(...)`, `Cancel()`, `Terminate()`, `Retry()`, `Archive()`, `Purge()`
- The engine must expose enough metadata to inspect runtime state separately from business state.
- The engine must expose operational statistics such as counts by workflow definition, version, and status.
- The engine must expose enough information to detect long-running, timed-out, or stuck steps and instances.
- The engine should expose enough information to detect large history/checkpoint growth in durable mode when applicable.
- Durable-only commands should be hidden from ephemeral-facing APIs where practical.

### Lifecycle events

- The engine must provide lifecycle events for workflow instances.
- The engine must provide lifecycle events for steps.
- Lifecycle events must cover at least create, execute, suspend, resume, complete, fail, timeout, cancel, terminate, evict, and stuck-detected transitions where applicable.
- Lifecycle events must be queryable, publishable, or both according to configured capabilities.

### Policies and decorators

- The engine should support policy/decorator metadata separate from steps.
- The engine should support step-level policies such as retry, timeout, idempotency, and visibility metadata.
- The engine should support scope-level policies such as branch cancellation and compensation behavior.
- The engine should support definition-level policies such as retention defaults, lifecycle publication defaults, and stuck-detection thresholds.

## Non-functional requirements

- API design must be embeddable and library-first.
- The runtime must not require a specific cloud provider.
- Host-restart resilience at durable wait boundaries is required only in durable mode.
- Provider contracts must be narrow and implementation-agnostic.
- The design must support idempotent resume handling when durable mode is enabled.
- Durable-only capabilities should be hidden from ephemeral-facing APIs where practical.
- Public API should favor semantic consistency over shortcut method proliferation.

## Research-driven design requirements

### Explicit execution contract

The engine must define a formal step-result contract. A step should not directly mutate engine internals; it should return intent to the runtime.

Candidate outcomes:

- complete
- fail
- branch
- spawn parallel branches
- wait for event
- publish event

### Explicit correlation model

The engine must define:

- workflow instance identity
- branch identity
- event identity
- event correlation keys
- duplicate event handling rules

### Durable wait model

When durable mode is enabled, the engine must persist enough data to resume a waiting workflow after restart, including:

- workflow instance identifier
- current execution point
- wait/subscription descriptor
- correlation data
- optional timeout or expiration

### Durable consistency boundary

When durable mode is enabled, the engine must define how it keeps workflow state changes and outbound event publication consistent.

At minimum, this needs research around:

- transactional outbox patterns
- inbox/deduplication patterns
- optimistic concurrency for instance updates

### Versioning and evolution

The engine must define what happens when a workflow definition changes while durable instances are running or suspended.

### Observability

The engine must define a minimal observability surface:

- instance state
- current step / current waits
- last transition time
- transition history
- failure details
- timeout details
- stuck indicators

## Open questions requiring further research

1. Should the runtime be deterministic/replay-based, checkpoint/snapshot-based, or hybrid?
2. Should `Wait` and `WaitLong` be different user-facing concepts, or a single wait primitive with different provider policies?
3. Should workflow definitions be graph-based, fluent builder-based, or both?
4. Should business state be strongly typed, loosely typed, or hybrid?
5. Should business steps be allowed to publish events directly, or should they only request publication through step results?
6. What guarantees can the engine realistically provide: at-least-once, effectively-once, or stronger only under certain providers?
7. How should branch cancellation work for `WhenFirst` after a winning branch completes?
8. What is the smallest useful history model that still supports debugging and resume confidence?
9. Which lifecycle events are guaranteed durable versus best-effort published?
10. Should active-instance eviction use plain idle timeout, LRU-like pressure eviction, or a hybrid policy?
11. Which inspection and history features remain available in ephemeral mode versus durable mode?
12. Should child workflows be first-class in the initial runtime or deferred?
13. How much durable/ephemeral separation should be enforced at compile time versus runtime?
14. Should decorators be attributes, fluent builders, metadata objects, or hybrid?

## Suggested next research tracks

1. Compare event-sourced replay versus persisted checkpoint models for this engine.
2. Design the event envelope, correlation, and deduplication model.
3. Define the persistence schema responsibilities before choosing concrete databases.
4. Specify branch and synchronization semantics for `Parallel`, `WhenAll`, and `WhenFirst`.
5. Define the workflow instance data model, including runtime state versus business state.
6. Define provider-level strategies for enforcing serialized execution per instance.
7. Define lifecycle event durability, timeout policy, and active-eviction semantics.
8. Define explicit feature matrix for workflow vs saga and durable vs ephemeral.
9. Define management command surface and step decorators.
10. Define versioning and deployment rules for long-running workflow instances.
