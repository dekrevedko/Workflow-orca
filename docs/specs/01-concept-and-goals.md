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

- **Typed code-first authoring** — staged `Init`/body/`End` builders compose reusable control
  flow and business steps into immutable, structurally fingerprinted definitions with typed
  input, private state, and typed successful output. Opaque code changes require an explicit
  `DefinitionVersion` bump; v1 does not pretend to hash delegate behavior.
- **One v1 semantic kind, two explicit modes** — workflow is the first-release definition
  kind; **ephemeral** is in-memory and **durable** is persistence-backed/restart-safe. Saga is
  documented future work, not a v1 axis or placeholder API.
- **First-class waits and events** — suspension, correlation, resume, active-wait matching,
  accepted-event deduplication, and non-consuming pre-wait rejection are core runtime semantics,
  not conventions layered on a message broker. V1 does not buffer events that arrive before a
  matching wait.
- **Pluggable infrastructure** — persistence, message dispatch, and timers sit behind provider
  contracts; no hard dependency on a specific database or broker. Payload encoding is the fixed,
  nonreplaceable v1 codec `orcacore-json-v1`, not a provider choice.
- **Operational visibility as a product feature** — typed instance snapshots, committed root
  state, typed output, active waits, cancellation request, termination, lifecycle telemetry, and
  host/operator projections are explicit contracts. Broad pause/retry/archive/purge management
  is documented future work rather than provisional v1 surface.

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

1. **Composable typed workflows** from staged `Init`/`End`, root and nested `If`, root-sequence-only
   `While`, root-sequence-only fixed `Parallel` with `WhenAll`/`WhenAllOutcomes`, root-sequence-only
   bounded `ForEach`, one cold-capable `Wait`, timers, named async business steps, and
   ephemeral-only lambda bodies.
2. **Explicit, deterministic semantics** for every feature interaction — branching, joins,
   wait races, cancellation, retries, timeouts. Semantic clarity is prioritized over surface
   area growth.
3. **Serialized execution per instance** — one logical mutator per workflow instance, always.
4. **Durability as an explicit mode**, never an implication: ephemeral mode is honest about
   its limits; durable mode delivers crash safety, rehydration, versioning, and retention.
5. **Replaceable infrastructure** — providers vary; engine-owned semantics do not.
6. **Operational excellence** — queryable runtime metadata, lifecycle events, statistics,
   stuck/timeout detection, typed instance handles, and advanced host/operator projections
   without overcommitting the first-release application API.
7. **Typed DAG scheduling** in separate `OrcaCore.Dag`, using one durable child instance per
   node, direct-dependency typed output mapping, and runtime-owned progression.
8. **Safe external capacity governance** through scoped non-empty durable leases with no author
   TTL/renewal and quarantine until protected-work stop/fence proof.
9. **Infrastructure-independent core** — Kubernetes, AWS, and job-scheduler integration points
   outward from a separate companion project and never enter OrcaCore dependencies or types.

## 1.5 Non-goals

- A distributed workflow **platform** or standalone server. OrcaCore is embedded in the host
  application.
- A visual designer or serialized DSL editor in the initial phases. Code-first authoring must
  stabilize before any serialized definition format is introduced (a second definition format
  is a second semantic contract that must match the first — see prior-art lessons).
- A broad connector/adapter ecosystem before the core runtime model is stable.
- A generic v1 external-job or public child-workflow authoring surface. Ordinary typed steps,
  waits, and the internal DAG child protocol cover the first scheduler use case while those
  generic contracts remain deferred.
- Workflow-authored `Publish` or self-`Cancel`, definition-targeted event fanout, public
  pause/resume, failed-instance retry, history query, archive, or purge in the first release.
- Saga/compensation, winner-race (`WhenFirst`), nested `Parallel`, nested dynamic `ForEach`,
  nested `While`, durable lambda steps, and definition-wide retry in the first release.
- Production-ready multi-node distributed execution in early phases. The design must not
  preclude it (ownership/lease seams are specified), but single-host correctness comes first.
- Forcing business-domain event sourcing on users. The engine event-sources its own
  orchestration facts; user business state remains an ordinary typed model.

## 1.6 Guiding principles

These principles resolve design disputes throughout the package:

1. **Semantics before features.** The biggest risk is not missing features; it is ambiguous
   semantics where features interact. Every primitive ships with explicit interaction rules
   and acceptance tests.
2. **The runtime owns orchestration; steps own business state.** Structural builders own
   ordinary control flow; steps return completion/failure or an exceptional post-step dynamic
   wait. Only the runtime mutates orchestration state.
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
9. **Structure, policy, management, and integrations are separate concerns.** Nodes describe
   structure; decorators describe policy; management commands operate on instances; execution
   mode describes guarantees; companion applications own infrastructure-specific integration.

## 1.7 Target users and primary scenarios

- **Application developers** embedding orchestration in a .NET service: request/reply with
  external systems, human-in-the-loop approvals, multi-service coordination, batch fanout.
- **Operators** who need to see what instances exist, what they wait for, why they failed,
  request cooperative cancellation, terminate progression, and diagnose resource quarantine.
  Reopening terminal instances and public archive/purge are deferred.

Representative scenarios used throughout the acceptance criteria:

- *Price update fanout*: validate, fan out updates to several subsystems in parallel, wait
  for correlated confirmations, and commit a typed final output. Workflow-authored publication
  is deferred.
- *Customer approval*: send a request, use ordinary durable `Wait` for a correlated approval
  event with a timeout, then branch on the outcome — surviving restarts and cold eviction.
- *Kubernetes DAG scheduler*: map typed dependency outputs, acquire the required durable
  capacity, idempotently create-or-observe a standard Kubernetes Job using `StepOperationId`,
  wait for a normalized watcher event, and quarantine capacity until stop proof when ambiguous.

Saga compensation remains a documented future scenario in document 07, not a first-release
acceptance target.
