# Research Backlog

## Phase 1: Foundation

- Define the minimal abstractions for workflow definitions, steps, step results, execution context, storage, and events.
- Describe the lifecycle of a workflow instance from start to completion.
- Specify how wait states are represented and resumed.
- Define identifiers and correlation rules for workflow instances, branches, and events.
- Review MassTransit saga state machine patterns: https://masstransit.io/documentation/patterns/saga/state-machine
- Review Stateless for local state-machine modeling ideas: https://github.com/dotnet-state-machine/stateless
- Review Workflow Core as the closest embeddable competitor: https://github.com/danielgerlag/workflow-core and https://workflow-core.readthedocs.io/en/latest/
- Write a dedicated event and correlation model document.
- Write a dedicated runtime state model document.

## Phase 2: Runtime proof of concept

- Implement sequential execution.
- Add `If` and `While`.
- Add `Parallel`, `WhenAll`, and `WhenFirst`.
- Add a durable `WaitLong` path backed by persisted subscriptions.
- Demonstrate resume after process restart.

## Phase 3: Provider model

- Define storage adapter responsibilities.
- Define event adapter responsibilities.
- Create one simple in-memory provider set for fast tests.
- Create one durable reference provider to validate the extension points.

## Risks to evaluate early

- Over-designing the workflow definition model before the runtime semantics are proven.
- Hidden complexity in branch synchronization and cancellation.
- Ambiguity around delivery guarantees from external brokers.
- Mismatch between provider capabilities and the engine's durability expectations.
- Underestimating correctness gaps in advanced combinations such as waits inside loops and waits across parallel branches.

## Suggested first milestone

Deliver a proof of concept that can:

1. Start a workflow.
2. Execute a few synchronous steps.
3. Enter a durable wait.
4. Persist state.
5. Resume from an external event.
6. Finish successfully after restart.
