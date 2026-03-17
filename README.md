# OrcaCore

Research project for a .NET 10 workflow engine focused on in-process orchestration with optional durability, reusable workflow steps, and pluggable infrastructure.

## Goals

- Build an in-app workflow engine based on reusable steps.
- Support infrastructure steps such as `Init`, `End`, `If`, `While`, `Parallel`, `WhenAll`, `WhenFirst`, `Wait`, and `WaitLong`.
- Allow user-defined business steps with async execution.
- Publish and consume events so waiting workflows can resume when external signals arrive.
- Support both ephemeral in-memory execution and durable persisted execution.
- Keep storage and messaging infrastructure replaceable through adapters.

## Non-goals for the first stage

- Production-ready distributed execution.
- Full visual designer or DSL editor.
- Broad connector ecosystem before the core runtime model is stable.

## Core idea

A workflow definition should describe control flow and business steps without being tightly coupled to a specific database or message broker. Runtime state, event delivery, resumability, and durability should be handled by abstractions so the same workflow model can run in either ephemeral mode or durable mode depending on configured providers.

## Architecture note

When a capability can reasonably vary by runtime, infrastructure, or integration boundary, prefer an interface-first and pluggable design over hardcoded internal implementations.

Examples:
- persistence is modeled behind store contracts
- outbox dispatch is modeled behind `IOutboxDispatcher`
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
- automatic outbox replay is available when an `IOutboxDispatcher` is configured, and delivery remains at-least-once
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

## Initial concepts

- `WorkflowDefinition`: regular workflow definition.
- `SagaDefinition`: saga-specific workflow definition with compensation-oriented semantics.
- `WorkflowInstance`: execution record containing runtime state and business state.
- `RuntimeState`: engine-owned orchestration metadata such as status, branch state, waits, correlation, checkpoints, and version binding.
- `BusinessState`: workflow-owned serializable application data used by steps.
- `Step`: async unit of work that can complete, branch, wait, fail, or publish events.
- `EventEnvelope`: normalized event contract used by the runtime.
- `Runtime`: executes ready steps, schedules waits, resumes instances, manages active-instance lifetime, and enforces orchestration semantics.
- `StorageProvider`: optional persistence for definitions, instances, checkpoints, subscriptions, and workflow data.
- `EventProvider`: publishes events and delivers them back to waiting workflows.

## Research questions

1. What is the minimal execution model that supports both short waits and durable long waits without overcomplicating the API?
2. How should parallel branches be represented so `WhenAll` and `WhenFirst` behave predictably?
3. Where is the boundary between workflow runtime responsibilities and infrastructure adapter responsibilities?
4. How should idempotency and exactly-once vs at-least-once semantics be expressed?
5. What persistence shape best supports resume, replay, and debugging?

## Proposed repository layout

```text
src/
  OrcaCore.Abstractions/
  OrcaCore.Runtime/
  OrcaCore.Persistence/
  OrcaCore.Messaging/
tests/
docs/
```

## Recommended Starting Point

Read [Documentation map](/X:/Projects/GitHub/Workflow-orca/docs/README.md) for the organized docs tree.

Read [Requirements tree](/X:/Projects/GitHub/Workflow-orca/docs/requirements/README.md) first for the current delivery baseline.

Then read:

- [Regular / Initial requirements](/X:/Projects/GitHub/Workflow-orca/docs/requirements/regular/initial/requirements.md)
- [Regular / Initial acceptance criteria](/X:/Projects/GitHub/Workflow-orca/docs/requirements/regular/initial/acceptance-criteria.md)
- [Design proposal: minimal core](/X:/Projects/GitHub/Workflow-orca/docs/architecture/design-proposal-minimal-core.md)
- [Implementation plan: minimal core](/X:/Projects/GitHub/Workflow-orca/docs/plans/implementation-plan-minimal-core.md)

## Research artifacts

- [Requirements tree](/X:/Projects/GitHub/Workflow-orca/docs/requirements/README.md)
- [Regular / Initial requirements](/X:/Projects/GitHub/Workflow-orca/docs/requirements/regular/initial/requirements.md)
- [Regular / Initial acceptance criteria](/X:/Projects/GitHub/Workflow-orca/docs/requirements/regular/initial/acceptance-criteria.md)
- [Regular / Advanced requirements](/X:/Projects/GitHub/Workflow-orca/docs/requirements/regular/advanced/requirements.md)
- [Regular / Advanced acceptance criteria](/X:/Projects/GitHub/Workflow-orca/docs/requirements/regular/advanced/acceptance-criteria.md)
- [Saga / Initial requirements](/X:/Projects/GitHub/Workflow-orca/docs/requirements/saga/initial/requirements.md)
- [Saga / Initial acceptance criteria](/X:/Projects/GitHub/Workflow-orca/docs/requirements/saga/initial/acceptance-criteria.md)
- [Saga / Advanced requirements](/X:/Projects/GitHub/Workflow-orca/docs/requirements/saga/advanced/requirements.md)
- [Saga / Advanced acceptance criteria](/X:/Projects/GitHub/Workflow-orca/docs/requirements/saga/advanced/acceptance-criteria.md)
- [Durable / Initial requirements](/X:/Projects/GitHub/Workflow-orca/docs/requirements/durable/initial/requirements.md)
- [Durable / Initial acceptance criteria](/X:/Projects/GitHub/Workflow-orca/docs/requirements/durable/initial/acceptance-criteria.md)
- [Durable / Advanced requirements](/X:/Projects/GitHub/Workflow-orca/docs/requirements/durable/advanced/requirements.md)
- [Durable / Advanced acceptance criteria](/X:/Projects/GitHub/Workflow-orca/docs/requirements/durable/advanced/acceptance-criteria.md)
- [Design synthesis](/X:/Projects/GitHub/Workflow-orca/docs/architecture/design-synthesis.md)
- [Workflow kinds and runtime modes](/X:/Projects/GitHub/Workflow-orca/docs/architecture/workflow-kinds-and-runtime-modes.md)
- [Management command surface](/X:/Projects/GitHub/Workflow-orca/docs/architecture/management-command-surface.md)
- [Pseudo DSL draft](/X:/Projects/GitHub/Workflow-orca/docs/architecture/pseudo-dsl-draft.md)
- [Comparative research](/X:/Projects/GitHub/Workflow-orca/docs/research/comparative-research.md)
- [Deep dive: MassTransit and Stateless](/X:/Projects/GitHub/Workflow-orca/docs/research/deep-dive-masstransit-stateless.md)
- [Durable Functions patterns](/X:/Projects/GitHub/Workflow-orca/docs/research/durable-functions-patterns.md)
- [Orleans patterns](/X:/Projects/GitHub/Workflow-orca/docs/research/orleans-patterns.md)
- [Instance identity, rehydration, and serialized execution](/X:/Projects/GitHub/Workflow-orca/docs/architecture/instance-identity-and-rehydration.md)
- [Lifecycle, resource management, and operational signals](/X:/Projects/GitHub/Workflow-orca/docs/architecture/lifecycle-resource-management.md)
- [Workflow Core competitor review](/X:/Projects/GitHub/Workflow-orca/docs/research/workflow-core-competitor-review.md)
- [Workflow Core issue pattern review](/X:/Projects/GitHub/Workflow-orca/docs/research/workflow-core-issue-pattern-review.md)
- [Acceptance test matrix](/X:/Projects/GitHub/Workflow-orca/docs/plans/acceptance-test-matrix.md)
- [Requirements draft](/X:/Projects/GitHub/Workflow-orca/docs/plans/requirements-draft.md)
- [Project foundation](/X:/Projects/GitHub/Workflow-orca/docs/architecture/project-foundation.md)
- [Research backlog](/X:/Projects/GitHub/Workflow-orca/docs/research/research-backlog.md)

## Next research steps

1. Define branch semantics for `Parallel`, `WhenAll`, and `WhenFirst`.
2. Define the event envelope, correlation rules, and deduplication strategy.
3. Define lifecycle and terminal-state semantics.
4. Define the workflow instance data model and persistence split.
5. Define provider-level enforcement of serialized execution per instance.
6. Define timeout policy, stuck detection, and active-eviction semantics.
7. Define explicit feature matrix for workflow vs saga and durable vs ephemeral.
8. Define management command surface and step decorators.
9. Define versioning and deployment rules for long-running workflow instances.
10. Turn the acceptance-test matrix into executable specs once implementation starts.
