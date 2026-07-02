# OrcaCore — technical overview

This document preserves the detailed repository orientation that previously lived in the root [README.md](../README.md). For a short public summary, goals, **current stage**, and **roadmap**, start at the root README.

---

Research-oriented .NET workflow engine focused on in-process orchestration with optional durability, reusable workflow steps, and pluggable infrastructure.

## Two engines (Workflow Orca)

The repository explores **two engines** with different backing models:

1. **State-driven runtime** (`OrcaCore.Abstractions` + `OrcaCore.Runtime`) — two public entry points today:
   - **`WorkflowEngine`** with **`InMemoryInstanceStore`** — **ephemeral** instances (lost on process exit); suitable for short-lived orchestration.
   - **`DurableWorkflowEngine`** with **`IWorkflowStore`** — **durable** instances, waits, history/outbox-related persistence, and optional outbox dispatch. Orchestration still advances through **explicit runtime state** persisted as frames and related records (not an append-only domain-event log as the core model).

2. **Event-driven prototype** (`OrcaCore.EventDrivenPrototype`) — **`EventDrivenWorkflowEngine`** appends workflow facts to a **per-instance stream**, maintains **checkpoints** and **projections** (summary, active waits, inbox deduplication). Narrow feature slice; see [Event-driven prototype status](architecture/event-driven-prototype-status.md).

See [Quick vs durable event-driven engine — feature matrix](architecture/quick-vs-durable-engine-feature-matrix.md) for a capability-oriented comparison (naming there uses “quick engine” vs “durable event-driven engine”).

## Code map

Use this table when navigating the repo; types are in `src/` unless noted.

| Area | Location | Notable types |
|------|----------|----------------|
| Step contract & shared models | `OrcaCore.Abstractions/` | `IStep`, `StepContext`, `StepResult`, `EventEnvelope`, `PendingEvent`, `WaitRecord`, `WaitStatus`, `WaitMode`, `WorkflowStatus`, `WorkflowInstanceSnapshot`, outbox transport `IMessageDispatcher` / `DispatchMessage` / `DispatchPayload` / `DispatchOutcome`, payload `IPayloadSchemaResolver` / `IPayloadEnvelopeSerializer` / `SerializedPayloadEnvelope` |
| Ephemeral engine | `OrcaCore.Runtime/Engine`, `Storage`, `Execution` | `WorkflowEngine`, `InMemoryInstanceStore`, `InstanceScope`, `WorkflowRuntime`, `WorkflowInstance` |
| Definition builders (ephemeral) | `OrcaCore.Runtime/Builders` | `WorkflowBuilder`, `BranchBuilder`, `ParallelBuilder` |
| Durable engine | `OrcaCore.Runtime/Durable/Engine` | `DurableWorkflowEngine`, `DurableWorkflowEngineOptions` |
| Durable definitions | `OrcaCore.Runtime/Durable/Definitions`, `Durable/Builders` | `DurableWorkflowDefinition`, `DurableWorkflowBuilder`, `DurableBranchBuilder`, `DurableParallelBuilder` |
| Durable persistence contract | `OrcaCore.Runtime/Durable/Persistence` | `IWorkflowStore`, `InMemoryWorkflowStore`, `PersistedInstance`, inbox/outbox/history record types |
| Durable outbox | `OrcaCore.Runtime/Durable/Outbox` | `DurableOutboxPump`, `IOutboxPumpObserver`, `IOutboxPumpDelayStrategy` (transport dispatch is `IMessageDispatcher` in `OrcaCore.Abstractions/Messaging`, configured on `DurableWorkflowEngineOptions.MessageDispatcher`) |
| Durable routing & management | `OrcaCore.Runtime/Durable/Routing`, `Durable/Execution`, `Durable/Querying`, `Durable/Management` | `DurableEventRouter`, `DurableInstanceManager`, `DurableInstanceScope`, retention types |
| Execution graph nodes | `OrcaCore.Runtime/Execution/Nodes` | `IfNode`, `WhileNode`, `ParallelNode`, `WaitNode`, `WaitLongNode`, `BusinessStepNode`, … |
| Event-driven prototype | `OrcaCore.EventDrivenPrototype/` | `EventDrivenWorkflowEngine`, `InMemoryPrototypeStore`, `PrototypeCheckpointState`, projections under `Projections/` |

Solution file: **`OrcaCore.slnx`** at repository root (no separate `.sln` required for the current layout).

## Goals

- Build an in-app workflow engine based on reusable steps.
- Support infrastructure primitives exposed by builders: `Init`, `End`, `If`, `While`, `Parallel` (multi-branch; join semantics covered by acceptance tests), `Wait`, and **`WaitLong` on the durable builder only** (`DurableWorkflowBuilder`).
- Allow user-defined business steps with async execution.
- Publish and consume events so waiting workflows can resume when external signals arrive.
- Support both ephemeral in-memory execution and durable persisted execution.
- Keep storage and messaging infrastructure replaceable through adapters.

## Non-goals for the first stage

- Production-ready distributed execution.
- Full visual designer or DSL editor.
- Broad connector ecosystem before the core runtime model is stable.

## Core idea

A workflow definition should describe control flow and business steps without being tightly coupled to a specific database or message broker. **How** that definition runs can differ: the quick engine centers on **mutable runtime state and transitions**; the durable event-driven engine centers on **facts appended to a stream** and derived state. In both cases, resumability, event delivery, and storage should remain behind abstractions so providers can vary.

## Architecture note

When a capability can reasonably vary by runtime, infrastructure, or integration boundary, prefer an interface-first and pluggable design over hardcoded internal implementations.

Examples:

- persistence is modeled behind store contracts
- outbox dispatch is modeled behind `IMessageDispatcher` (`DispatchMessage` / `DispatchOutcome`), with the durable pump mapping leased `OutboxRecord` rows to `DispatchMessage` before calling the adapter
- automatic outbox pump observability and retry timing are modeled behind `IOutboxPumpObserver` and `IOutboxPumpDelayStrategy`
- durable runtime behavior is configured through engine options rather than hidden static behavior

This should be the default direction for future extensibility points such as step decorators, retry/timeout policies, messaging adapters, and operational hooks. Use concrete internal implementations only when there is no meaningful extension boundary yet.

## Durable runtime notes

- `Wait` and `WaitLong` are both durable waits, but they differ in residency policy:
  - `Wait` remains a resident wait when the instance stays hot
  - `WaitLong` checkpoints and is expected to go cold until resumed
- durable wait semantics are modeled with two axes:
  - `WaitStatus` for lifecycle (`Active`, `Matched`, `Cancelled`)
  - `WaitMode` for residency policy (`Resident`, `Cold`)
- `Parallel` currently means coordinated sequential branch execution, not true concurrent branch execution
- automatic outbox replay is available when an `IMessageDispatcher` is configured on `DurableWorkflowEngineOptions` (`MessageDispatcher`, or the init-only `OutboxDispatcher` alias for the same backing field), and delivery remains at-least-once
- durable management operations are available through instance and selection scopes:
  - delete instance state
  - purge old inbox/outbox/history artifacts
- retention policy remains intentionally narrow:
  - runtime/application code should use `DurableArtifactRetentionPolicy`
  - explicit cutoff purge remains provider/operator-oriented through the store contract
  - archival/default operator policy remains a follow-up design area

## Runtime axes

- `Definition semantics`:
  - regular workflow
  - saga workflow
- `Execution mode`:
  - ephemeral mode
  - durable mode

## Concepts vs code

**Implemented in this repository**

- **`WorkflowDefinition` / `DurableWorkflowDefinition`** — graph of `IWorkflowNode` steps; durable definitions carry a version string for store registration.
- **`WorkflowInstance` / `IWorkflowInstance`** — in-memory execution record with **runtime** orchestration metadata (`RuntimeState`) and typed **business** state `TState`.
- **`IStep` / `StepContext` / `StepResult`** — user-defined async steps; results can complete, publish events, fail, or schedule waits depending on path.
- **`EventEnvelope`** — normalized inbound event shape (`OrcaCore.Abstractions`); both engines expose `RaiseEvent`-style APIs for correlation/instance routing.
- **`WorkflowRuntime`** — interprets the node graph for ephemeral instances (`OrcaCore.Runtime`).
- **`IWorkflowStore`** — durable persistence contract for the **state-driven** engine (instances, frames, waits, inbox/outbox/history as modeled in `Durable/Persistence`).

**Specified in docs / requirements only (not separate types in `src/` yet)**

- **`SagaDefinition`** and saga-specific compensation flows — see [requirements/saga/](requirements/saga/initial/requirements.md).
- Standalone **`OrcaCore.Persistence`** or **`OrcaCore.Messaging`** assemblies — not present; use `IWorkflowStore` and host-supplied dispatch for outbox delivery.

**Naming note:** older docs may say `StorageProvider` / `EventProvider`; the durable code path centers on **`IWorkflowStore`** plus **`WorkflowEngine` / `DurableWorkflowEngine`** event APIs and **`CorrelationIndex`** / durable router.

## Research questions

1. What is the minimal execution model that supports both short waits and durable long waits without overcomplicating the API?
2. How should parallel branches be represented so `WhenAll` and `WhenFirst` behave predictably?
3. Where is the boundary between workflow runtime responsibilities and infrastructure adapter responsibilities?
4. How should idempotency and exactly-once vs at-least-once semantics be expressed?
5. What persistence shape best supports resume, replay, and debugging?

## Repository layout (conceptual)

```text
src/
  OrcaCore.Abstractions/
  OrcaCore.Runtime/
  OrcaCore.EventDrivenPrototype/
tests/
  OrcaCore.Tests/
  OrcaCore.EventDrivenPrototype.Tests/
docs/
OrcaCore.slnx
```

There is no `OrcaCore.Persistence` or `OrcaCore.Messaging` project; durable persistence for the state-driven engine lives under `OrcaCore.Runtime/Durable/Persistence`.

## Recommended starting point (documentation)

1. [Documentation map](README.md)
2. [Requirements tree](requirements/README.md)
3. [Regular / Initial requirements](requirements/regular/initial/requirements.md)
4. [Regular / Initial acceptance criteria](requirements/regular/initial/acceptance-criteria.md)
5. [Design proposal: minimal core](architecture/design-proposal-minimal-core.md)
6. [Implementation plan: minimal core](plans/implementation-plan-minimal-core.md)

## Research artifacts

- [Requirements tree](requirements/README.md)
- [Regular / Initial requirements](requirements/regular/initial/requirements.md)
- [Regular / Initial acceptance criteria](requirements/regular/initial/acceptance-criteria.md)
- [Regular / Advanced requirements](requirements/regular/advanced/requirements.md)
- [Regular / Advanced acceptance criteria](requirements/regular/advanced/acceptance-criteria.md)
- [Saga / Initial requirements](requirements/saga/initial/requirements.md)
- [Saga / Initial acceptance criteria](requirements/saga/initial/acceptance-criteria.md)
- [Saga / Advanced requirements](requirements/saga/advanced/requirements.md)
- [Saga / Advanced acceptance criteria](requirements/saga/advanced/acceptance-criteria.md)
- [Durable / Initial requirements](requirements/durable/initial/requirements.md)
- [Durable / Initial acceptance criteria](requirements/durable/initial/acceptance-criteria.md)
- [Durable / Advanced requirements](requirements/durable/advanced/requirements.md)
- [Durable / Advanced acceptance criteria](requirements/durable/advanced/acceptance-criteria.md)
- [Design synthesis](architecture/design-synthesis.md)
- [Workflow kinds and runtime modes](architecture/workflow-kinds-and-runtime-modes.md)
- [Management command surface](architecture/management-command-surface.md)
- [Pseudo DSL draft](architecture/pseudo-dsl-draft.md)
- [Comparative research](research/comparative-research.md)
- [Deep dive: MassTransit and Stateless](research/deep-dive-masstransit-stateless.md)
- [Durable Functions patterns](research/durable-functions-patterns.md)
- [Orleans patterns](research/orleans-patterns.md)
- [Instance identity, rehydration, and serialized execution](architecture/instance-identity-and-rehydration.md)
- [Lifecycle, resource management, and operational signals](architecture/lifecycle-resource-management.md)
- [Workflow Core competitor review](research/workflow-core-competitor-review.md)
- [Workflow Core issue pattern review](research/workflow-core-issue-pattern-review.md)
- [Acceptance test matrix](plans/acceptance-test-matrix.md)
- [Requirements draft](plans/requirements-draft.md)
- [Project foundation](architecture/project-foundation.md)
- [Research backlog](research/research-backlog.md)
- [Event-driven prototype status](architecture/event-driven-prototype-status.md)

## Open design / research follow-ups

Many baseline behaviors are already covered by `OrcaCore.Tests`; the list below tracks **remaining** product/design questions, not “greenfield” items.

1. Evolve **`Parallel` / join** semantics (and any future `WhenFirst`-style API) and keep ephemeral vs durable aligned.
2. Harden **idempotency, deduplication, and correlation** rules across stores and hosts (inbox patterns exist on both paths; edge cases remain).
3. **Lifecycle, timeouts, stuck detection, and eviction** — operational semantics and APIs.
4. **Saga** model and implementation once saga requirements stabilize.
5. **Event-driven prototype** — broaden control flow, extract real store contracts from `InMemoryPrototypeStore`, timer/outbox paths.
6. **Versioning and deployment** for long-running instances across definition upgrades.
7. Keep [acceptance test matrix](plans/acceptance-test-matrix.md) synchronized with executable tests as features land.
