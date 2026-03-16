# Comparative Research

Reviewed on March 14, 2026.

## Purpose

This document compares established workflow and orchestration systems against the goals of OrcaCore:

- in-application workflow execution
- reusable control-flow and business steps
- durable waiting and resume
- pluggable persistence
- pluggable messaging and event delivery
- async-first .NET programming model

The goal is not to copy a single product. The goal is to identify the strongest ideas worth borrowing and the tradeoffs we should avoid.

## Shortlist

### Temporal

What stands out:

- Strong durable execution model with persisted workflow history and replay.
- Clear separation between orchestration code and side-effecting activity code.
- First-class signals, queries, timers, retries, child workflows, and event history.
- High operational maturity and strong observability model.

Approaches worth borrowing:

- Explicit separation between pure orchestration decisions and side effects.
- Durable event history or equivalent persisted decision trail.
- First-class external signals/events for resuming blocked workflows.
- Clear workflow instance management and inspection APIs.

Tradeoffs / limitations for our use case:

- Temporal is a platform, not a lightweight embeddable in-app library.
- Its determinism model is powerful but imposes authoring constraints that may feel heavy for an in-process engine.
- Running the full Temporal service stack is far beyond the simplicity target for an app-local engine.

Relevant source:

- Temporal docs: https://docs.temporal.io/

### Azure Durable Functions / Durable Task

What stands out:

- Durable waits for external events are well modeled.
- `WaitForExternalEvent` composes naturally with `Task.WhenAny` and `Task.WhenAll`.
- Good evidence that waiting, fan-in, and first-completed semantics can map cleanly to async code.

Approaches worth borrowing:

- Make waits a first-class runtime primitive, not a custom convention.
- Support `WhenAny` / `WhenAll`-style synchronization over waits and branches.
- Treat external events as correlated messages addressed to workflow instances.

Tradeoffs / limitations for our use case:

- Durable Functions is tightly shaped by Azure Functions hosting.
- Durable Task is closer to reusable runtime infrastructure, but still carries the orchestrator determinism model.
- The model is stronger on orchestration than on embeddable step composition for arbitrary application code.

Relevant source:

- Durable Functions external events: https://learn.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-external-events

### Dapr Workflow

What stands out:

- Practical workflow capability built around portability and pluggable infrastructure.
- Strong operational APIs: start, query, pause/resume, raise event, terminate, purge.
- Explicit support for child workflows and durable timers/reminders.
- Good alignment with pub/sub and external systems.

Approaches worth borrowing:

- Treat workflow lifecycle operations as a stable management surface.
- Include child workflow support in the model even if not implemented in phase 1.
- Keep infrastructure adapters behind stable state/messaging abstractions.

Tradeoffs / limitations for our use case:

- Dapr is optimized for distributed applications around the Dapr runtime.
- It assumes a larger platform boundary than a pure embedded library.
- Its abstractions are useful, but some decisions are delegated to Dapr building blocks rather than the workflow runtime itself.

Relevant source:

- Dapr workflow overview: https://docs.dapr.io/developing-applications/building-blocks/workflow/workflow-overview/

### Elsa Workflows

What stands out:

- Good fit for .NET application embedding.
- Uses bookmarks as a first-class suspension/resume mechanism.
- Explicit distinction between inline blocking activities and trigger activities.
- Correlation and bookmark indexing are treated as core runtime concerns.

Approaches worth borrowing:

- A bookmark-like abstraction for durable waiting.
- Separation between "pause here" and "start/resume me when event X arrives".
- Correlation payload hashing or indexed correlation keys for efficient resume.

Tradeoffs / limitations for our use case:

- Elsa carries more workflow-platform surface area than we currently need.
- Its activity model is broad and extensible, but may be heavier than a narrowly scoped engine.
- Bookmark flexibility adds complexity around matching, cleanup, and retention.

Relevant source:

- Elsa blocking activities and triggers: https://docs.elsaworkflows.io/activities/blocking-and-triggers

### MassTransit

What stands out:

- Strong messaging integration and practical saga/state-machine patterns.
- Routing slips demonstrate activity chaining plus compensation.
- Saga repositories and transport integrations show how to keep infrastructure pluggable.

Approaches worth borrowing:

- Keep event providers transport-agnostic.
- Consider saga-style persisted coordination state where event-driven orchestration is more natural than pure flow execution.
- Add compensation/recovery as a later extension point.

Tradeoffs / limitations for our use case:

- Routing slips intentionally carry state on the wire; that is useful for distributed processing, but not ideal for in-app queryable workflow state.
- State-machine and routing-slip models are adjacent to workflows, not a complete fit for our target abstraction.
- MassTransit is broker-centric; our engine needs messaging to be optional and replaceable.

Relevant sources:

- Routing slips monitored via saga: https://masstransit.io/documentation/patterns/routing-slips/monitor-via-saga
- Saga state machine: https://masstransit.io/documentation/patterns/saga/state-machine

### Stateless

What stands out:

- Lightweight .NET state machine library with a compact programming model.
- Useful reference for explicit state transitions, guards, triggers, and entry/exit actions.
- Good fit for reasoning about local runtime state transitions even though it is not a workflow engine.

Approaches worth borrowing:

- Keep transition rules explicit and testable.
- Model guards and trigger handling clearly instead of hiding transition logic in ad hoc code.
- Preserve a compact authoring style where possible.

Tradeoffs / limitations for our use case:

- Stateless is a state machine library, not a durable workflow runtime.
- It does not solve persistence, event durability, branch synchronization, or long-running waits by itself.
- It is best used as a conceptual reference for runtime state modeling, not as the architecture target.

Relevant source:

- Stateless repository: https://github.com/dotnet-state-machine/stateless

### Workflow Core

What stands out:

- Lightweight .NET workflow engine with control structures and external events.
- Straightforward code-first model and embeddable mindset.
- Good signal that simple API shape matters.

Approaches worth borrowing:

- Keep the authoring surface compact and approachable.
- Support event wait primitives directly in the fluent/code model.
- Preserve embeddability as a primary design constraint.

Tradeoffs / limitations for our use case:

- Simplicity comes with a less opinionated durability model than systems like Temporal.
- External event handling exists, but the runtime semantics and operational model are less rigorous than the leading durable execution systems.
- The project is a useful reference, but not the target bar for runtime guarantees.

Relevant source:

- Workflow Core external events: https://workflow-core.readthedocs.io/en/latest/external-events/

### Orleans

What stands out:

- Excellent reference for durable timers/reminders and activation lifecycle.
- Helpful model for distinguishing short-lived in-memory scheduling from durable wake-up mechanisms.

Approaches worth borrowing:

- Separate transient timers from durable reminders/wakeups.
- Make resumption infrastructure explicit rather than hiding it behind a single primitive with unclear guarantees.

Tradeoffs / limitations for our use case:

- Orleans is a virtual actor runtime, not a workflow engine.
- It helps with hosting and wake-up semantics, but not with workflow structure directly.

Relevant source:

- Orleans timers and reminders: https://learn.microsoft.com/en-us/dotnet/orleans/grains/timers-and-reminders

## Cross-project patterns worth adopting

### 1. Durable suspension must be a first-class runtime concept

Implication for OrcaCore:

- `Wait` and `WaitLong` should map to explicit persisted suspension records with correlation metadata and resume semantics.

### 2. External events need a normalized correlation model

Implication for OrcaCore:

- Correlation must be part of the core model.
- We need a canonical event envelope and explicit matching rules.

### 3. Parallelism requires a real branch model

Implication for OrcaCore:

- `Parallel`, `WhenAll`, and `WhenFirst` require branch instance tracking, cancellation policy, and aggregation rules.

### 4. Storage and broker abstraction is necessary, but semantics must stay owned by the runtime

Implication for OrcaCore:

- Adapter interfaces should expose capabilities needed by the engine.
- The engine should not leak broker-specific semantics into workflow definitions.

### 5. Operational visibility is not optional

Implication for OrcaCore:

- Instance inspection, waiting-state inspection, event subscription inspection, and history/debug output belong in the requirements.

## Major gaps in the current project idea

### Runtime semantics gap

We listed steps, but not the exact execution contract for a step result.

### Wait/resume gap

`Wait` and `WaitLong` are named, but their difference is not defined yet.

### Event model gap

We say workflows can publish events and resume from events, but we have not defined event shape, correlation, guarantees, deduplication, or ordering.

### Persistence gap

We want pluggable storage, but have not yet defined the minimum persistence model.

### Consistency gap

There is no requirement yet for how state changes and outbound event publication stay consistent.

### Versioning gap

We have not decided what happens when a workflow definition changes while instances are paused.

### Failure policy gap

Retries, compensation, cancellation, branch failure, and timeout behavior are not yet defined.

### Observability gap

We do not yet require query APIs, state snapshots, history, metrics, or diagnostic tracing.

## Initial conclusion

OrcaCore should be a focused embedded .NET runtime that borrows:

- durable wait/resume and runtime-owned decisions from Temporal and Durable Task
- bookmark/correlation ideas from Elsa and WF-style systems
- provider abstraction from Dapr, MassTransit, and similar messaging platforms
- simple embeddable authoring from Workflow Core
- durable wake-up distinction from Orleans reminders
