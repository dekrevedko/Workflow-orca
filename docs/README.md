# Documentation map

This folder holds **requirements**, **architecture**, **plans**, and **research**. Treat **`src/`** and **`tests/`** as the source of truth for behavior; docs explain intent and history and are updated when the model shifts.

## Start here

1. [Project README](../README.md) — audience, **two engines**, build/test, roadmap.
2. [Project technical overview](project-technical-overview.md) — durable notes, concepts, **code map**, and links to the full doc tree.

3. [Current roadmap](plans/current-roadmap.md) - implemented features, in-progress tracks, and planned scope.

## Source layout (aligned with the solution)

The solution file at the repo root is **`OrcaCore.slnx`**. Projects:

| Project | Purpose |
|---------|---------|
| [OrcaCore.Abstractions](../src/OrcaCore.Abstractions) | Shared contracts and models: `IStep`, `StepContext`, `StepResult`, `EventEnvelope`, `WaitRecord`, `WaitStatus`, `WaitMode`, `WorkflowStatus`, etc. |
| [OrcaCore.Runtime](../src/OrcaCore.Runtime) | **State-driven** orchestration: `WorkflowEngine` + `InMemoryInstanceStore` (ephemeral), `DurableWorkflowEngine` + `IWorkflowStore` (durable), builders, nodes, routing, durable outbox. |
| [OrcaCore.EventDrivenPrototype](../src/OrcaCore.EventDrivenPrototype) | **Event-driven** prototype: `EventDrivenWorkflowEngine`, append-only stream types, checkpoints, projections, in-memory prototype store. |
| [OrcaCore.Tests](../tests/OrcaCore.Tests) | Main test suite (acceptance + durable + unit). |
| [OrcaCore.EventDrivenPrototype.Tests](../tests/OrcaCore.EventDrivenPrototype.Tests) | Prototype tests. |

There are **no** separate `OrcaCore.Persistence` or `OrcaCore.Messaging` packages today; persistence abstractions for the durable **state-driven** path live under `OrcaCore.Runtime` (e.g. `Durable/Persistence`).

## Architecture docs tied to current code

- [Event-driven prototype status](architecture/event-driven-prototype-status.md) — what the prototype implements and what is missing.
- [Quick vs durable event-driven engine — feature matrix](architecture/quick-vs-durable-engine-feature-matrix.md) — compares “quick” vs event-driven durable positioning.
- [Project foundation](architecture/project-foundation.md) — problem statement, ephemeral vs durable modes (conceptual).

## Folder index

- `specs/` — **consolidated product requirements & specifications** (self-contained package for a from-scratch implementation; see [specs/README.md](specs/README.md)).
- `implementation/` — **agent-executable implementation guide** (stack decisions, conventions, TDD workflow, phased task files sized for small-context LLM agents; see [implementation/README.md](implementation/README.md)).
- `orleans-engine/` — **Orleans engine package**: self-contained specs (`OE-`/`OE-AC-`), architecture, and phased implementation plan for `OrcaCore.Engine.Orleans` — durable workflows hosted on Orleans grains, reusing the durable core and provider ports (see [orleans-engine/README.md](orleans-engine/README.md)).
- `architecture/` — design decisions, runtime shape, lifecycle, identity, event-driven notes.
- `durable/` — durable-runtime plans, remediation, component inventory.
- `plans/` — implementation plans, acceptance matrix, requirements draft.
- `research/` — pattern studies, competitor analysis, backlog.
- `reviews/` — consolidated review findings.
- `requirements/` — capability requirements and acceptance criteria.

## Suggested reading order

1. [requirements/README.md](requirements/README.md)
2. [project-technical-overview.md](project-technical-overview.md) (especially **Code map** and **Concepts vs code**)
3. [architecture/project-foundation.md](architecture/project-foundation.md)
4. [architecture/design-proposal-minimal-core.md](architecture/design-proposal-minimal-core.md)
5. [plans/implementation-plan-minimal-core.md](plans/implementation-plan-minimal-core.md)
6. [durable/durable-implementation-plan.md](durable/durable-implementation-plan.md)
7. [durable/durable-review-remediation-plan.md](durable/durable-review-remediation-plan.md)
