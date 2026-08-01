# Design Decisions Tracking

This document tracks architectural and design pattern choices for the minimal core implementation, with rationale.

---

## DD-100: Use Interpreter pattern for the execution engine

**Decision**: The runtime walks the workflow definition graph and executes steps based on current state, rather than steps driving their own sequencing.

**Why**: The strongest research lesson across Temporal, Durable Functions, and Orleans is "separate orchestration logic from side effects." The interpreter pattern makes the runtime the owner of control flow. Steps are passive â€” they execute and return results. The runtime decides what happens next. This prevents steps from making hidden orchestration decisions and makes the execution model testable, serializable, and eventually replayable.

**Alternatives considered**:
- **Self-advancing steps** (each step calls the next): violates orchestration/side-effect separation. Makes serialization and replay impossible. Rejected.
- **Event-driven choreography** (steps publish events, other steps subscribe): loses centralized orchestration guarantees. Appropriate for inter-service coordination, not intra-engine control flow. Rejected for core engine.

**Risk**: The interpreter must handle nested structures (Parallel > Branch > Wait) correctly. The execution pointer / call-stack model adds complexity. Mitigated by acceptance tests MC-AT-006 through MC-AT-008.

---

## DD-101: Use Composite pattern for workflow definition structure

**Decision**: Infrastructure steps (`Parallel`, `If`) and business steps share a common `IStep<TState>` interface. Complex steps contain child steps, forming a tree.

**Why**: The workflow definition is naturally a tree â€” sequential flow at the root, with branches inside `If` and `Parallel` nodes. Composite lets the interpreter walk the tree uniformly without type-switching at every level. It also lets business steps and infrastructure steps be composed freely.

**Alternatives considered**:
- **Flat step list with pointer arithmetic**: simpler for linear flows but breaks down with nesting. Workflow Core uses a flat model and it leads to complex pointer management and bugs (issue #273). Rejected.
- **Separate graph model with edges**: more general but over-engineered for the tree structures we need. Deferred â€” may be useful if serialized definitions require graph import later.

**Risk**: Deep nesting could make the execution pointer complex. Mitigated by limiting nesting depth in phase 1 (Parallel cannot contain Parallel in the first slice â€” can be relaxed later).

---

## DD-102: Step results carry control intent; business state is mutated directly

**Decision**: Steps return `StepResult` discriminated union (`Completed`, `Failed`, `WaitForEvent`, `Yield`) to express control flow intent. Business state is mutated directly by the step through `StepContext.State`. `StepResult` carries no business state changes.

**Why**: This is the most critical contract in the engine. Steps must not mutate orchestration state (status, execution pointer, wait records, branch state) â€” that belongs to the runtime. But business state (order data, accumulated results, application fields) is owned by the step.

The split is: steps own business state writes through `StepContext.State`. The runtime owns orchestration decisions through `StepResult`. One concern per channel.

By making step results into control-only data objects, the runtime can:
- Validate the result before applying orchestration changes
- Apply orchestration changes atomically under the instance lock
- Log/record the transition
- Eventually replay it

**Alternatives considered**:
- **StepResult carries business state mutations** (e.g., `StateUpdate: Action<TState>?` on `Completed`): creates two mutation channels and ambiguity about when deferred mutations apply relative to direct mutations. Rejected â€” see DD-112.
- **StepContext.State is read-only; steps return new state**: purer functional model, more replay-friendly. But impractical for .NET reference-type state without deep cloning or immutable state discipline. May be revisited for durable/replay mode.
- **Async callback pattern** (steps call `context.Complete()` or `context.WaitFor(...)`): viable but makes the step contract less pure and harder to test in isolation. The result-object approach is more functional and testable.

**Supersedes**: original DD-102 which included `StateUpdate: Action<TState>?` on `StepResult.Completed`. That was removed by DD-112.

---

## DD-103: Per-instance execution serializer using SemaphoreSlim(1,1)

**Decision**: Each workflow instance gets a `SemaphoreSlim(1,1)` to serialize all mutations â€” step execution, event resume, branch completion.

**Why**: This is the Orleans-inspired "one logical mutator per instance" guarantee. It eliminates an entire class of concurrency bugs: branch/wait races, duplicate event processing, partial state corruption. The acceptance tests MC-AT-012 (concurrent resume) and MC-AT-008 (branch completion order) depend on this.

**Alternatives considered**:
- **Global lock**: too coarse. Blocks all instances when only one needs serialization. Rejected.
- **Channel/mailbox per instance**: stronger queuing semantics but more complex. The mailbox pattern is better when message ordering matters or when we need backpressure. For the minimal core, SemaphoreSlim is sufficient. Can be upgraded later.
- **Optimistic concurrency (version check on write)**: needed for durable mode but insufficient alone for in-memory mode where we want true serialization, not retry-on-conflict. Will be added as a second layer when durable providers arrive.
- **No synchronization (trust single-threaded host)**: dangerous. Even in single-process mode, async code and parallel branches create real concurrency. Rejected.

**Risk**: SemaphoreSlim per instance means a `ConcurrentDictionary<string, SemaphoreSlim>` that grows with active instances. Acceptable for ephemeral mode. For durable mode, the serializer will be paired with optimistic concurrency on the provider side.

---

## DD-104: Separate RuntimeState from BusinessState

**Decision**: `WorkflowInstance<TState>` contains two distinct state categories: `RuntimeState` (engine-owned) and `TState` (workflow-owned business data).

**Why**: Design-synthesis best practice 3.4. Runtime metadata (status, execution pointer, active waits, timestamps, errors) must be queryable without deserializing business payload. This matters for:
- Management API queries (`Where(x => x.Status == Running)`)
- Operational visibility (active wait inspection, stuck detection)
- Future provider efficiency (query runtime columns without loading business blob)

Workflow Core's biggest operational pain point is the opaque JSON blob â€” you can't query instance status without loading and deserializing the entire state. Anti-pattern 4.1.

**Alternatives considered**:
- **Single state object**: simpler API but forces deserializing everything for any query. Rejected based on competitor lessons.
- **Full separation with no shared container**: too fragmented. The instance is the natural unit of work. Having a single `WorkflowInstance<TState>` that contains both is the right granularity.

**Risk**: Adds model complexity. Two state objects means two things to serialize, two things to version. Acceptable because the alternative (opaque blob) is a known production pain point.

---

## DD-105: Fluent builder for workflow definitions producing immutable graphs

**Decision**: `WorkflowBuilder<TState>` provides a fluent API for composing workflow definitions. `Build()` produces an immutable `IWorkflowDefinition<TState>`.

**Why**:
- The pseudo-DSL research shows the intended authoring is code-first and fluent
- Immutability after construction prevents runtime mutation of definitions
- The builder can validate structural integrity (matching Parallel/WhenAll, no orphan branches) at build time rather than runtime
- Builder pattern is idiomatic in .NET (e.g., `HostBuilder`, `WebApplicationBuilder`)

**Alternatives considered**:
- **Graph object model (nodes + edges)**: more general but harder to author. Better suited for serialized/imported definitions. May be added as an alternative input path later, but builder is the primary authoring experience.
- **Attribute-based definition** (annotate classes/methods): less flexible, harder to compose, doesn't express nesting well. Rejected for primary API.
- **YAML/JSON definition**: not appropriate for phase 1. No serialized format yet.

**Risk**: The builder API shape may need revision as acceptance tests reveal authoring friction. Acceptable â€” the builder is a thin construction layer over the immutable graph model. The graph model is the stable contract; the builder is sugar.

---

## DD-106: Structured query model for Where(...), not arbitrary predicates

**Decision**: `Where(...)` accepts a structured `WorkflowInstanceQuery` (or expression over a constrained query model), not arbitrary `Func<WorkflowInstance, bool>`.

**Why**: Design decision DD-009 from management-command-surface.md. Arbitrary predicates cannot be translated to database queries when durable providers arrive. Starting with unconstrained in-memory predicates would create an API that breaks when persistence is added.

The first slice is ephemeral-only, so technically any predicate could work in-memory. But the public API contract should already assume provider-translatable semantics.

**Alternatives considered**:
- **Arbitrary Func predicate**: simpler API but creates a false promise. Users would write predicates that work in-memory but fail against SQL. Rejected.
- **Expression trees with full LINQ translation**: over-engineered for phase 1. The structured query model is simpler and sufficient.
- **String-based query DSL**: too foreign for .NET developers. Rejected.

**Risk**: The structured query model may feel limited compared to full LINQ. Acceptable â€” it can be extended with new fields. Expression-tree sugar can be added later over the same model.

---

## DD-107: Event matching uses EventName + CorrelationId

**Decision**: Within an instance, event-to-wait matching uses `EventName` + `CorrelationId` against active `WaitRecord` entries. CorrelationId was renamed from CorrelationKey (see DD-122) to reflect its role as a first-class request-reply identity.

**Why**: The combination of `EventName` + `CorrelationId` is the minimum matching rule that supports isolated branch waits (MC-AT-007) and request-reply patterns. EventName alone is too weak. Adding more matching dimensions (e.g., payload predicates) can be done later without breaking this contract.

**Alternatives considered**:
- **EventName only** (no correlation): too weak. Parallel branches waiting for different instances of the same event type need correlation to stay isolated (MC-AT-007).
- **Payload-based matching**: more flexible but introduces serialization and predicate evaluation into the hot matching path. Deferred.

**Note**: The routing question (how events reach the right instance) is separate from the matching question (how events match waits within an instance). Routing is addressed by DD-123 (three routing modes: instance-targeted, correlation-targeted, definition-targeted fanout) and DD-124 (correlation index as multi-map with routing-time uniqueness).

---

## DD-108: Execution pointer as call stack, not flat pointer

**Decision**: The runtime tracks position in the definition graph using an `ExecutionPointer` with a stack of `ParentFrame` entries, not a single flat step index.

**Why**: The composite definition tree means the interpreter can be inside a branch inside a Parallel inside an If. A flat pointer (like Workflow Core's `ExecutionPointerId`) cannot represent this without external metadata. A stack-based pointer naturally represents "I'm at step X, inside branch 2 of Parallel Y, inside the then-path of If Z."

This also makes rehydration straightforward: restore the pointer stack, and the interpreter knows exactly where to resume.

**Alternatives considered**:
- **Flat step ID + external pointer table**: Workflow Core's approach. Works but leads to complex pointer management and the #273-style bugs. Rejected.
- **Step path string** (e.g., "root/parallel-1/branch-2/step-3"): simpler but harder to manipulate programmatically. The stack is more ergonomic for the interpreter.

**Risk**: Stack depth grows with nesting. Acceptable â€” workflow nesting is typically shallow (2-3 levels).

---

## DD-109: Two projects initially: Abstractions and Runtime

**Decision**: Start with two projects: `OrcaCore.Abstractions` (pure contracts) and `OrcaCore.Runtime` (engine + ephemeral provider + management API).

**Why**: The abstraction/implementation split exists from day one to enforce the dependency rule: runtime depends on abstractions, never the reverse. User workflow code can reference `Abstractions` without pulling in the runtime.

However, splitting further (separate Management project, separate InMemory provider project) is premature. The minimal core is small enough that one Runtime project keeps the build simple and avoids over-engineering the project structure.

**Alternatives considered**:
- **Single project**: simpler but mixes contracts with implementation. Users who only need step interfaces would pull in the entire runtime. Rejected.
- **Four+ projects** (Abstractions, Runtime, Management, InMemory): too granular for the minimal core. Creates build complexity without value at this scale. Can be split later when durable providers arrive.

**Risk**: The Runtime project does multiple things (execution, management, in-memory storage). Acceptable for the minimal core. Natural split points will emerge when durable mode is added.

---

## DD-110: Parallel branches may execute concurrently but WhenAll join is serialized

**Decision**: When the interpreter hits a `ParallelStep`, branches are started as independent execution paths. They may run on separate tasks. But all branch state mutations and the final `WhenAll` join check happen under the per-instance serializer.

**Why**: This balances real concurrency (branches doing I/O in parallel) with correctness (no race on join). The acceptance tests MC-AT-006 (join exactly once), MC-AT-008 (order independence), and MC-AT-012 (serialized outcome) all depend on this.

The model is: branches fan out, but they converge through a single serialized gate. The last branch to complete triggers the join continuation â€” checked atomically under the lock.

**Alternatives considered**:
- **Sequential branch execution**: simpler but defeats the purpose of Parallel. Business steps in branches often involve async I/O that benefits from real concurrency. Rejected.
- **Unserialized join**: dangerous. Two branches completing simultaneously could both think they're the last one and trigger the continuation twice. This is exactly Workflow Core issue #273. Rejected.

**Risk**: Branch execution on separate tasks means the SemaphoreSlim will see contention when branches complete close together. Acceptable â€” the critical section (state update + join check) should be fast.

---

## DD-111: Include `While` in the minimal core to discover wait-in-loop risks early

**Decision**: Add `While` to the first implementation slice alongside `If`, `Parallel`, `WhenAll`, and `Wait`. Add two acceptance tests: MC-AT-013 (basic loop) and MC-AT-014 (wait inside loop).

**Why**: The full acceptance test matrix (AT-004) identifies repeated waits inside loops as a real risk area â€” Workflow Core users encountered bugs where later loop iterations accidentally matched earlier wait subscriptions. Deferring `While` to phase 2 would leave this risk untested during the period when core interfaces are being shaped.

`While` is simpler to implement than `Parallel` (it's essentially a conditional jump in the execution pointer), but its interaction with `Wait` reveals whether the WaitRecord lifecycle model is correct: each iteration must create fresh WaitRecords and must not reuse or re-match records from previous iterations.

**Alternatives considered**:
- **Defer to phase 2**: the original minimal-core-scope position. Lower scope, but the wait-in-loop risk goes untested. If the WaitRecord model turns out to be wrong for loops, the refactor is more expensive after Parallel has been built on top of it. Rejected.
- **Include While but without wait-in-loop test**: misses the point. The loop body without Wait is trivially correct. The risk is specifically the Wait interaction. Rejected.

**Risk**: Adds two acceptance tests and one infrastructure step (`WhileStep`) to the minimal core. The implementation cost is low â€” `WhileStep` is a condition check + pointer reset in the interpreter. The testing cost is moderate â€” MC-AT-014 requires a multi-iteration workflow with per-iteration event correlation, which is a meaningful integration scenario.

---

## DD-112: Superseded â€” merged into DD-102

The dual-mutation-channel issue identified by DD-112 has been resolved by updating DD-102 directly. See DD-102 for the current position: steps mutate business state through `StepContext.State`; `StepResult` carries only control intent. The original `StateUpdate: Action<TState>?` on `StepResult.Completed` was removed.

---

## DD-113: Where(...) uses expression-based LINQ-like predicates; structured query is internal

**Decision**: The public `Where(...)` API accepts `Expression<Func<WorkflowInstanceSnapshot, bool>>`, matching the canonical LINQ-like shape from the management baseline (DD-007, DD-008, DD-009). The structured query model is an internal implementation detail used by providers, not a public contract.

**Why**: The original proposal made `Where(query)` with a structured `WorkflowInstanceQuery` object the primary public form. This regressed the agreed management API shape from `management-command-surface.md` and `minimal-core-scope.md`, which both specify LINQ-like `Where(x => x.Status == Running)` as the canonical form. Pushing the structured query into the public API would establish the wrong contracts in early tests and conflict with the fluent composable model.

The structured query model remains valid internally â€” it is how providers translate expressions. But the public surface must look like the agreed baseline.

---

## DD-114: Start returns WorkflowInstanceSnapshot, not the live instance

**Decision**: `WorkflowEngine<T>.Start(input)` returns `WorkflowInstanceSnapshot` â€” an immutable record with `InstanceId`, `Status`, `DefinitionId`, and `CreatedAt`. It does not return the live `WorkflowInstance<TState>`.

**Why**: Returning the mutable `WorkflowInstance<TState>` from `Start` would leak engine internals through the management surface. Callers could bypass the serialized execution guarantee by mutating state directly. It also couples the management API to the internal instance model, making future changes (e.g., adding durable providers) harder.

The management surface should return handles and snapshots. The live instance is engine-internal.

Business state is observable through `InstanceScope.GetState<TState>()`, which returns a snapshot copy of the current business state. This keeps `WorkflowInstanceSnapshot` metadata-only while still allowing tests and callers to inspect workflow results. The caller must know the state type; a type mismatch throws with clear diagnostics.

---

## DD-121: GetState returns a snapshot copy, not a live reference

**Decision**: `InstanceScope.GetState<TState>()` returns a copy of the business state, not a reference to the live internal state. This is the only public path to read business state from outside the engine.

**Why**: MC-AT-001 needs to assert that a completed workflow produced the expected business result. But `Start` correctly returns only `WorkflowInstanceSnapshot` (DD-114), and the snapshot is metadata-only. Without a typed state reader, there is no public API path to verify business outcomes. `GetState<TState>()` closes this gap while preserving the internal/external boundary â€” the returned value is a snapshot, so the caller cannot mutate engine state.

**Alternatives considered**:
- **Include business state in WorkflowInstanceSnapshot**: rejected. The snapshot would need a generic type parameter, losing the ability to handle instances of different definition types uniformly. Also forces serialization concerns into the metadata model.
- **Return live reference**: rejected. Violates the serialized execution guarantee by allowing external mutation outside the engine's lock.

**Risk**: The caller must know the correct `TState` type. Mitigated by throwing `InvalidOperationException` with a clear message on type mismatch.

---

## DD-115: Init step owns input-to-state construction

**Decision**: The `Init` step is the single owner of input-to-state construction. `IWorkflowDefinition<TState>` does not carry a `CreateInitialState` function.

**Why**: The original proposal had both `CreateInitialState: Func<TInput, TState>` on the definition root and `InitStep` as a primitive. This created two initialization paths and left `TInput` as an unresolved type parameter on the definition. One clear rule: `Init` receives the start input, produces the initial business state. The definition root describes structure; the `Init` step describes initialization behavior.

---

## DD-116: Observer pattern removed from minimal core

**Decision**: The Observer pattern (lifecycle event hooks) is not part of the minimal core design. Internal state tracking is normal runtime behavior.

**Why**: The minimal core scope explicitly excludes lifecycle event publication (`minimal-core-scope.md`, excluded control-flow and policy features). Including Observer as a design pattern commitment would introduce hook contracts and publication semantics that are premature. The acceptance tests verify state through the management API (`Get()`, `GetActiveWaits()`, `List()`), not through event subscriptions. Observer/lifecycle hooks will be introduced when lifecycle event publication enters scope.

---

## DD-117: Explicit lifecycle transition table with named triggers (Stateless-inspired)

**Decision**: Instance and branch lifecycle transitions are defined by an explicit transition table with named triggers. Illegal triggers on any state are rejected, not silently ignored. Terminal states (`Completed`, `Failed`) reject all triggers.

**Why**: Inspired by Stateless-style state machine discipline. The original proposal described lifecycle transitions in prose ("Running â†’ Waiting â†’ Running â†’ Completed/Failed") but did not define them as a formal table with explicit triggers. This leaves room for ambiguity: can you resume a completed workflow? What happens if a branch receives an event after the parent is failed?

Making transitions explicit produces:
- Cleaner acceptance tests (test the transition, not just the outcome)
- Earlier detection of illegal operations (reject at the state machine, not deep in the interpreter)
- A debuggable/exportable transition graph

**What was borrowed from Stateless**: explicit legal transitions, named triggers, rejected vs ignored distinction. **What was not borrowed**: Stateless as a runtime architecture. The workflow engine is an interpreter, not a state machine library. The lifecycle state machine is one component inside the runtime.

---

## DD-118: Orchestration ownership rule (Temporal-inspired)

**Decision**: Runtime control flow is engine-owned. Steps must not decide orchestration outside `StepResult`. Steps must not call back into the runtime, spawn outliving tasks, or use exceptions as orchestration signals.

**Why**: Inspired by Temporal's execution discipline. The minimal core does not implement replay, but the orchestration ownership rule is the prerequisite for future replay-safe durable mode. If steps can make orchestration decisions outside the `StepResult` channel, replay becomes impossible.

This is enforced by API shape (StepContext exposes no runtime surface), not by runtime detection. It is a convention backed by contract design.

**Future pressure**: if durable replay mode is added, the business state mutation model (direct via `StepContext.State`) may need to become recorded. The orchestration ownership rule ensures that the runtime decision path is already deterministic and replayable â€” only the business state channel would need to change.

---

## DD-119: Per-instance event buffer (mailbox) for order-independent event delivery

**Decision**: Each workflow instance has a per-instance event buffer (mailbox). Events that arrive before a matching wait is registered are buffered as `PendingEvent` entries. When a new wait is registered, the mailbox is checked for a pending match. Matching is bidirectional: events find waits, and waits find events.

**Why**: Without buffering, the `Wait` primitive is order-dependent. If a workflow expects event A then event B in sequence, but B arrives first, B is lost forever â€” the workflow hangs. This is a common real-world scenario in any system with async event sources.

The per-instance mailbox solves this without introducing a global event bus or external messaging infrastructure. It is bounded to the instance (not a shared topic), operates under the per-instance serializer (no race between arrival and registration), and is cleaned up on terminal state.

**What was borrowed**: The mailbox principle from enterprise messaging â€” events are buffered and consumed by subscribers independently. **What was not borrowed**: global topics, fan-out routing, subscription management, or external broker integration. Those belong to the event provider layer (durable mode), not the core runtime.

**Additional rules**:
- Events are deduplicated by `EventId` â€” duplicate delivery is silently ignored
- `ConsumedEventIds` tracks which events have been processed
- Pending events are removed when consumed or when the instance reaches a terminal state
- The mailbox is part of `RuntimeState`, not business state

**Alternatives considered**:
- **Reject and require caller retry**: pushes complexity to the caller. Fragile in async systems. Rejected.
- **Global event bus with subscriptions**: too much infrastructure for an embeddable engine. Appropriate for the event provider layer in durable mode, not the core runtime. Rejected for minimal core.
- **Ordered delivery guarantee**: require events to arrive in order. Unrealistic constraint for most real-world integrations. Rejected.

---

## DD-120: Event safety guarantee â€” no event loss, transactional consumption

**Decision**: An event is considered consumed only when both conditions hold: (1) it has been matched to a wait, and (2) the resulting state transition has been committed. Until both conditions are met, the event must remain available. Event removal from the mailbox happens after state commit, never before.

**Why**: Events are the primary mechanism for advancing waiting workflows. If an event is lost â€” consumed from the mailbox but the state change fails to commit â€” the workflow hangs forever with no way to recover. This is a data-loss bug that cannot be fixed by retry at the caller level, because the caller believes the event was delivered.

The transactional consumption rule prevents this: the commit sequence is match â†’ compute new state â†’ commit state â†’ remove event. If the process crashes between computing and committing, the event remains in the mailbox/provider. On rehydration, the last committed state still shows the wait as Active, and the event can be re-matched.

**Contract scope**: This is a core contract, not a durable-mode-only concern. Even in ephemeral mode, the in-memory implementation follows the same pattern to ensure correctness during normal operation (e.g., step failure after event match). In durable mode, the provider must persist pending events as part of instance state and atomically commit the full state (runtime state + business state + pending events + consumed IDs).

**Provider implications** (future, designed now):
- `IInstanceStore.Save()` must atomically commit all instance state including pending events
- External event providers must not acknowledge/delete source events until the consuming state transition is durably saved
- This is the transactional inbox pattern: event ingestion and state mutation in one logical transaction

**Alternatives considered**:
- **Best-effort delivery** (remove event on match, before commit): simpler but creates a data-loss window. A crash between match and commit loses the event permanently. Rejected â€” this is the exact anti-pattern from design-synthesis 4.4 ("depending on shutdown hooks for critical persistence").
- **Idempotent re-delivery from external source**: pushes responsibility to the caller. Works for some integrations but not all (some event sources are not re-readable). The engine should not depend on external re-delivery guarantees for correctness. Rejected as the primary mechanism.

---

## DD-122: CorrelationId is a first-class request-reply identity, not just a matching filter

**Decision**: Rename `CorrelationKey` â†’ `CorrelationId` across all contracts (`WaitRecord`, `EventEnvelope`, `StepResult.WaitForEvent`, `WaitStep.CorrelationSelector`). The CorrelationId is the identity that links a wait to its response event across the full lifecycle: workflow publishes outbound request with CorrelationId â†’ external system processes â†’ response event carries same CorrelationId â†’ engine matches response to waiting instance.

**Why**: `CorrelationKey` sounded like a filter or lookup key. What it actually represents is a request-reply identity â€” the same value appears on the outbound event and the inbound response. This is the standard correlation pattern from MassTransit (`CorrelatedBy<Guid>`), Temporal (workflow ID as correlation), and enterprise messaging. The rename makes the contract semantics explicit: this is the identity that ties the publish-and-wait cycle together, enabling event routing and end-to-end traceability.

**What changed**: naming only. The matching rule (`EventName` + `CorrelationId`), the `CorrelationSelector: Func<TState, string>` on `WaitStep`, and the per-instance matching logic all remain structurally identical.

---

## DD-123: Three event routing modes in minimal core

**Decision**: The minimal core supports three event routing modes, determined by the API entry point:

1. **Instance-targeted**: `engine.Instance(id).RaiseEvent(envelope)` â€” direct delivery.
2. **Correlation-targeted**: `engine.RaiseEvent(envelope)` â€” engine looks up the instance by `(EventName, CorrelationId)` from the correlation index.
3. **Definition-targeted (fanout)**: `engine.ForDefinition<T>().RaiseEvent(envelope)` â€” deliver to all instances of that definition type.

**Why**: Designing the correlation index as a 1:1 mapping (correlation-targeted only) and retrofitting fanout later would force a redesign of the routing subsystem. The three modes are architecturally entangled: correlation-targeted routing needs a uniqueness constraint that fanout must relax. Designing both together avoids contradictions.

Additionally, correlation-targeted routing is a basic usability requirement â€” external systems responding to workflow requests only know the CorrelationId, not the InstanceId. Requiring InstanceId forces the integration layer to maintain an external mapping that the engine should own.

**Correlation index**: `(EventName, CorrelationId) â†’ HashSet<InstanceId>` multi-map for all active `WaitRecord` entries (see DD-124). Updated on wait registration, matching, and cancellation. Registration always succeeds. Uniqueness is enforced at correlation-targeted **routing time** only (must resolve to exactly one instance). Fanout bypasses the index and iterates by definition.

**Alternatives considered**:
- **Defer fanout**: would create a 1:1 assumption in the correlation index that breaks when fanout is added. Rejected.
- **Fanout on engine-wide scope** (`engine.RaiseEvent()` to all instances of all definitions): too broad. Fanout should be definition-scoped, not global. Rejected.
- **Routing mode on EventEnvelope** (flag/enum): mixes routing intent with event data. The routing mode is a caller decision, not an event property. API entry point is clearer. Rejected.

**Risk**: Fanout to many instances could be expensive. In ephemeral mode this is bounded by process memory. In durable mode (future), fanout may need batching or pagination. Acceptable for the minimal core â€” the correctness semantics matter more than scale optimization at this stage.

---

## DD-124: Correlation index is a multi-map; uniqueness enforced at routing time only

**Decision**: The engine maintains a correlation index as a multi-map: `(EventName, CorrelationId) â†’ HashSet<InstanceId>`. Wait registration always succeeds â€” the index records all active waits. Uniqueness is enforced only at **correlation-targeted routing time**: `engine.RaiseEvent(envelope)` requires the index to resolve to exactly one instance (count == 1). If count == 0, throw "no active wait found." If count > 1, throw "ambiguous correlation â€” use instance-targeted or fanout routing."

**Why**: CorrelationId is a request-reply identity (DD-122) for correlation-targeted routing. But fanout requires multiple instances to wait for the same `(EventName, CorrelationId)`. A 1:1 dictionary cannot support both. A multi-map with routing-time validation supports both modes cleanly: registration is always safe, and the routing mode determines whether uniqueness matters.

**Key rules**:
1. `RegisterWait` always adds to the index. No registration-time uniqueness check.
2. `DeliverToInstance` (after match) removes the entry from the index.
3. `WorkflowEngine.RaiseEvent()` (correlation-targeted) checks count == 1, then delivers.
4. `WorkflowEngine<T>.RaiseEvent()` (fanout) bypasses the index entirely â€” iterates by definition.
5. `InstanceScope.RaiseEvent()` (instance-targeted) bypasses the index â€” direct delivery.
6. `WaitRecord` has no routing-mode flag. Routing is a caller concern, not a definition concern.

**Alternatives considered**:
- **1:1 dictionary with registration-time uniqueness**: blocks fanout. Rejected.
- **Registration-time uniqueness with a flag on WaitRecord**: couples wait declaration to routing topology. Rejected.
- **No uniqueness check at all**: ambiguous correlation-targeted routing produces non-deterministic behavior. Rejected â€” correlation-targeted must be unambiguous.

**Risk**: If multiple instances share a key and a caller uses correlation-targeted routing, the call fails with a clear error guiding them to use fanout or instance-targeted routing instead. This is intentional.

---

## DD-125: StepResult uses abstract record with sealed record subclasses

**Decision**: `StepResult` is implemented as `abstract record StepResult` with `sealed record Completed : StepResult`, `sealed record Failed(Exception Error) : StepResult`, `sealed record WaitForEvent(string EventName, string CorrelationId) : StepResult`, `sealed record Yield : StepResult`.

**Why**: C# does not allow `abstract sealed` on a class (the original design doc notation was pseudo-syntax). Records provide: value equality (two `Completed` results are equal), immutability (results cannot be modified after creation), built-in `ToString()` for diagnostics, and C# pattern matching (`switch` expressions / `is` patterns) for exhaustive handling in the interpreter. `sealed` on each subclass prevents third-party extension â€” the runtime must handle every possible result.

**Alternatives considered**:
- **Abstract class with sealed nested classes**: works but loses value equality and requires manual `Equals`/`GetHashCode`. More boilerplate for no gain.
- **C# 14 discriminated unions**: not yet stable in .NET 10 preview. Can migrate later if available.
- **Enum + data object**: loses type safety and forces casting to access subclass-specific data like `Error` or `EventName`.

---

## DD-126: Event payload delivered to step via StepContext.ResumedEvent

**Decision**: `StepContext<TState>` has a `ResumedEvent: EventEnvelope?` property. When a workflow resumes from a `Wait`, the interpreter sets `ResumedEvent` to the matched `EventEnvelope` before executing the next step. During normal (non-resume) execution, `ResumedEvent` is null.

**Why**: `EventEnvelope` has `Payload: object?`, but no contract existed for how the payload reaches business step code after a wait resume. Without this, the step following a Wait has no way to access the event data â€” a gap that would block any real-world use case (e.g., "wait for price response, read the price from the event").

**Key rules**:
1. `ResumedEvent` is set on the `StepContext` of the **first step executing after a wait resume**
2. Subsequent steps in the same execution run see `ResumedEvent == null` (it is not propagated)
3. The step can read `ctx.ResumedEvent?.Payload` and cast/deserialize as needed
4. The step can also write event data into business state via `ctx.State` for downstream steps to use

**Alternatives considered**:
- **Payload handler on WaitStep** (`onEvent: Action<TState, object?>`): more structured but adds complexity to the WaitStep infrastructure step and the builder API. Can be added later as a convenience layer on top of `ResumedEvent`.
- **Inject payload directly into business state**: the runtime would need to know the state shape. Violates the separation â€” runtime should not mutate business state.
- **Separate ResumeContext type**: over-engineering for the minimal core. `ResumedEvent` on `StepContext` is sufficient.

---

## Summary: Pattern usage map

| Pattern | Where used | Why chosen |
|---------|-----------|------------|
| Interpreter | `WorkflowRuntime` | Separates orchestration from side effects |
| Composite | `IStep<TState>` tree | Natural tree structure for workflow definitions |
| Command/Result | `StepResult` | Steps express intent, runtime owns state changes |
| State Machine | `WorkflowStatus` transitions | Explicit lifecycle, prevents illegal transitions |
| Builder | `WorkflowBuilder<TState>` | Fluent authoring, immutable output |
| Mediator | `WorkflowRuntime` | Central coordinator, prevents component coupling |
| Repository | `IInstanceStore` | Storage abstraction from day one |

Note: Observer pattern was removed from the minimal core. Lifecycle event publication is explicitly excluded from the first slice (minimal-core-scope.md). Internal state tracking is normal runtime behavior, not a pattern commitment. Observer/lifecycle hooks will be introduced when lifecycle event publication enters scope.
