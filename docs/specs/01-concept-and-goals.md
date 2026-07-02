# 1. Concept and Goals

## 1.1 Problem statement

Applications regularly need to orchestrate multi-step business processes: some steps complete
immediately, some branch or fan out into parallel work, and some suspend until an external
signal (an event, a human decision, a timer) arrives. Today teams either adopt a heavyweight
external workflow platform (Temporal, Durable Functions, Dapr) or hand-roll fragile
orchestration with queues, flags, and polling.

**OrcaCore** is an **embeddable .NET workflow engine** for application-local orchestration.
It lets an application define workflows as reusable steps and execute them asynchronously
inside the application's own process, with **optional durability**: when a persistence
provider is configured, suspended workflows survive process restarts and can be resumed by
correlated external events, timers, or operator action.

## 1.2 Product definition

OrcaCore is a library (not a platform, not a hosted service) with:

- **Code-first authoring** — workflows are composed from reusable control-flow primitives and
  user-defined business steps through a fluent builder that produces immutable, validated
  definitions.
- **Two orthogonal axes** that define the product matrix:
  - *Definition semantics*: **regular workflow** vs **saga** (compensation-aware).
  - *Execution mode*: **ephemeral** (in-memory, lost on restart) vs **durable**
    (persistence-backed, restart-safe).
- **First-class waits and events** — suspension, correlation, resume, buffering, and
  deduplication are core runtime semantics, not conventions layered on a message broker.
- **Pluggable infrastructure** — persistence, message dispatch, timers, and serialization sit
  behind provider contracts; no hard dependency on a specific database or broker.
- **Operational visibility as a product feature** — instance inspection, wait inspection,
  history, statistics, stuck detection, and lifecycle events are part of the contract.

## 1.3 The two engines

The product ships two execution engines that share contracts and authoring concepts but make
different guarantee/complexity trade-offs:

1. **Quick (ephemeral) engine** — in-memory orchestration with mutable runtime state.
   Fast, minimal infrastructure, suitable for short-lived orchestration and short waits.
   Instances are lost on process exit; durable-only features are absent from its API.

2. **Durable event-driven engine** — the durable execution mode is built on an
   **append-only, event-sourced core**: every accepted mutation of an instance appends
   immutable workflow facts to a per-instance stream; checkpoints bound replay cost;
   projections serve queries and routing; inbox/outbox records make external delivery
   consistent with committed state. This hybrid model (stream + checkpoint + projections)
   is the accepted durable architecture (see [06-requirements-durable-execution.md](06-requirements-durable-execution.md)).

Both engines expose the same high-level concepts (steps, waits, events, correlation) and the
same management-surface style, so users move between modes by changing guarantees, not by
relearning the model.

## 1.4 Goals

1. **Composable workflows** from control-flow primitives (`Init`, `End`, `If`, `While`,
   `Parallel`, `WhenAll`, `WhenFirst`, `Wait`, durable-only `WaitLong`, timers, fanout,
   child workflows) plus user-defined async business steps.
2. **Explicit, deterministic semantics** for every feature interaction — branching, joins,
   wait races, cancellation, retries, timeouts. Semantic clarity is prioritized over surface
   area growth.
3. **Serialized execution per instance** — one logical mutator per workflow instance, always.
4. **Durability as an explicit mode**, never an implication: ephemeral mode is honest about
   its limits; durable mode delivers crash safety, rehydration, versioning, and retention.
5. **Replaceable infrastructure** — providers vary; engine-owned semantics do not.
6. **Operational excellence** — queryable runtime metadata, lifecycle events, statistics,
   stuck/timeout detection, and a fluent, composable management API.
7. **Saga support as a distinct semantic kind** with first-class compensation.

## 1.5 Non-goals

- A distributed workflow **platform** or standalone server. OrcaCore is embedded in the host
  application.
- A visual designer or serialized DSL editor in the initial phases. Code-first authoring must
  stabilize before any serialized definition format is introduced (a second definition format
  is a second semantic contract that must match the first — see prior-art lessons).
- A broad connector/adapter ecosystem before the core runtime model is stable.
- Production-ready multi-node distributed execution in early phases. The design must not
  preclude it (ownership/lease seams are specified), but single-host correctness comes first.
- Forcing business-domain event sourcing on users. The engine event-sources its own
  orchestration facts; user business state remains an ordinary typed model.

## 1.6 Guiding principles

These principles resolve design disputes throughout the package:

1. **Semantics before features.** The biggest risk is not missing features; it is ambiguous
   semantics where features interact. Every primitive ships with explicit interaction rules
   and acceptance tests.
2. **The runtime owns orchestration; steps own business state.** Steps express intent through
   result objects; only the runtime mutates orchestration state.
3. **One logical mutator per instance.** Committed transitions for an instance form a single
   serial order, whatever concurrency exists around it.
4. **Memory is a cache; durable state is the truth** (in durable mode). In-memory activations
   are disposable and evictable at any safe boundary.
5. **Durability is explicit.** Durable-only features are separated at the API level where
   practical and fail fast everywhere else.
6. **Fail fast for unsupported semantics.** Reject clearly; never silently downgrade.
7. **Runtime metadata stays queryable.** Never persist the instance as one opaque blob.
8. **Providers implement capabilities; the engine owns the contract.** Broker- or
   database-specific behavior must not leak into workflow definitions.
9. **Structure, policy, management, and semantics are separate concerns.** Steps describe
   structure; decorators describe policy; management commands operate on instances; the
   workflow/saga split describes meaning; the ephemeral/durable split describes guarantees.

## 1.7 Target users and primary scenarios

- **Application developers** embedding orchestration in a .NET service: request/reply with
  external systems, human-in-the-loop approvals, multi-service coordination, batch fanout.
- **Operators** who need to see what instances exist, what they wait for, why they failed,
  and to intervene (retry, cancel, terminate, purge) safely.

Representative scenarios used throughout the acceptance criteria:

- *Price update fanout*: validate, fan out updates to several subsystems in parallel, wait
  for correlated confirmations, publish a final event.
- *Customer approval*: send a request, `WaitLong` days for a correlated approval event with a
  timeout, then branch on the outcome — surviving any number of restarts in between.
- *Order fulfillment saga*: reserve inventory, authorize payment, create shipment, with
  compensations (release, refund, cancel) running in reverse order on failure.
