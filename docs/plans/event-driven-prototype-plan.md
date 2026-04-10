# Event-Driven Prototype Plan

Reviewed on March 29, 2026.

## Purpose

This document defines a phased implementation and research plan for an event-driven OrcaCore prototype.

It is explicitly a prototype/research track, not yet a decision to replace the current engine.

Its purpose is to answer:

- can the event-driven architecture satisfy the same product semantics?
- does it materially simplify durable and saga concerns?
- what is the operational and implementation cost?

## Prototype Goals

The prototype should prove or disprove these claims:

1. Event-driven durable truth is easier to reason about than snapshot-first durability.
2. Projections can satisfy the query and routing requirements cleanly.
3. `WaitLong`, inbox/outbox, and crash-safe recovery become simpler or at least clearer.
4. Saga compensation fits the event-driven substrate better than the current durable engine.
5. The architecture remains practical for an embedded .NET engine and does not become “workflow platform only.”

## Prototype Non-Goals

The prototype is not initially trying to deliver:

- production-grade multi-host execution
- multiple providers
- full migration from the current engine
- polished public API
- full archive/retention lifecycle

## Success Criteria

The prototype is successful if it can demonstrate all of the following:

- deterministic per-instance serialized command handling
- durable wait and restart-safe resume
- projection-based query and correlation routing
- inbox-based dedup after restart
- outbox publication model with at-least-once semantics
- one clear recovery model: checkpoint + stream tail replay
- at least one narrow saga flow with compensation

## Phase 0: Freeze Shared Product Semantics

Before implementation:

- freeze shared semantics between engines:
  - `Wait`
  - correlation routing
  - duplicate event rules
  - lifecycle meanings
  - `Parallel` + `WhenAll`
  - version binding
- define which semantics are intentionally durable-only:
  - `WaitLong`
  - durable timers
  - retention/history
  - saga support in the prototype track

Deliverables:

- semantic contract note
- acceptance criteria crosswalk between current docs and prototype scope

Exit criteria:

- no unresolved contradictions in the feature matrix

## Phase 1: Write-Model Specification

Design the event-driven write model before coding.

Specify:

- command catalog
- workflow event catalog
- aggregate state shape
- checkpoint shape
- stream version / concurrency contract
- command idempotency rules
- durable transaction boundary

Minimum command set:

- `StartWorkflowCommand`
- `DeliverInstanceEventCommand`
- `DeliverCorrelationEventCommand`
- `DeliverDefinitionEventCommand`
- `FireTimerCommand`

Minimum event set:

- `WorkflowStarted`
- `DefinitionVersionBound`
- `StepSucceeded`
- `StepFailed`
- `WaitRegistered`
- `EventBuffered`
- `BufferedEventConsumed`
- `WaitMatched`
- `BranchCompleted`
- `JoinSatisfied`
- `WorkflowCompleted`
- `WorkflowFailed`

Deliverables:

- command/event catalog doc
- stream and checkpoint schema doc

Exit criteria:

- no ambiguity about what durable facts are appended for each accepted mutation

## Phase 2: Projection Specification

Define the read-side projections required for the prototype.

Minimum projections:

- `InstanceSummaryProjection`
- `ActiveWaitProjection`
- `PendingEventProjection`
- `OutboxProjection`
- `HistoryProjection`

Decide:

- projection update timing
- rebuild rules
- projection consistency expectations
- routing dependence on projections

Deliverables:

- projection catalog
- query and routing contract doc

Exit criteria:

- every required management/read use case maps to one projection

## Phase 3: Prototype Infrastructure Contracts

Implement narrow prototype contracts.

Suggested contracts:

- `IWorkflowEventStore`
- `IWorkflowCheckpointStore`
- `IWorkflowProjectionStore`
- `IWorkflowInboxStore`
- `IWorkflowOutboxStore`
- `ITimerScheduler`

Build one reference provider only:

- simplest possible in-memory or local durable reference

Do not optimize for multiple backends yet.

Deliverables:

- prototype contracts
- reference provider

Exit criteria:

- command handler can append events and update projections consistently

## Phase 4: Minimal Regular Workflow Prototype

Implement a narrow but end-to-end regular workflow slice.

Scope:

- `Init`
- business step
- `If`
- `While`
- `Parallel`
- `WhenAll`
- `Wait`
- instance-targeted events
- correlation-targeted routing
- definition fanout
- duplicate dedup

Do not add:

- saga
- `WaitLong`
- timers
- multi-host

Goal:

- prove shared regular workflow semantics are achievable on the event-driven substrate

Exit criteria:

- selected regular acceptance scenarios pass on prototype

## Phase 5: Durable Slice

Add true durable behavior.

Scope:

- event stream persistence
- checkpointing
- restart recovery
- `WaitLong`
- cold-instance resume
- inbox/outbox
- projection-backed queries

Goal:

- prove the main durable claims of the design

Minimum acceptance focus:

- durable wait survives restart
- last committed state only after crash
- durable query from projections
- serialized durable resume attempts

Exit criteria:

- prototype passes a narrow durable acceptance slice

## Phase 6: Timer Slice

Add durable wake-up semantics.

Scope:

- timer scheduling
- timer fire command
- deterministic timer vs event handling rules

Goal:

- validate timeout-ready infrastructure without full decorator system

Exit criteria:

- one durable timer scenario works end to end

## Phase 7: Narrow Saga Slice

Add one constrained saga capability set.

Scope:

- separate saga definition kind
- forward action completion recording
- compensation stack
- reverse compensation order
- compensation failure visibility

Do not add every saga feature yet.

Goal:

- validate whether saga is materially cleaner on the event-driven substrate

Exit criteria:

- a small saga acceptance slice passes:
  - success without compensation
  - failure triggers compensation
  - deterministic compensation order

## Phase 8: Comparative Evaluation

Run a formal comparison between:

- current engine
- event-driven prototype

Compare:

- semantic fidelity
- implementation complexity
- test complexity
- operational visibility
- provider complexity
- durability clarity
- saga fit

Deliverables:

- comparison scorecard
- recommendation memo

Exit criteria:

- decision can be made from evidence, not architecture preference alone

## Recommended Acceptance Slice For The Prototype

Use a smaller evaluation suite first.

### Regular workflow slice

- straight-line completion
- wait enters waiting state
- matching event resumes exactly once
- out-of-order event buffered and later consumed
- duplicate event deduplicated
- correlation-targeted routing resumes exactly one instance
- parallel branches join exactly once
- concurrent resume attempts serialize

### Durable slice

- durable wait survives restart
- rehydration restores committed state only
- durable instance is version-bound
- durable inspection remains queryable
- concurrent durable resume attempts serialize

### Saga slice

- successful saga completes without compensation
- failure triggers compensation
- compensation order is deterministic

## Risks

### Risk 1: Prototype becomes a second full product

Mitigation:

- keep prototype scope narrow
- gate expansion behind explicit review

### Risk 2: Projection model becomes too complex too early

Mitigation:

- build only the minimum read models needed for acceptance coverage

### Risk 3: Event catalog grows without discipline

Mitigation:

- define command/event catalog before coding
- require explicit rationale for new durable event types

### Risk 4: Shared semantics drift from the current engine

Mitigation:

- run the same acceptance scenarios against both engines where overlap exists

### Risk 5: Provider abstraction becomes overengineered

Mitigation:

- one reference provider first
- no premature multi-provider support

## Honest Cost Assessment

Expected cost is medium-high.

Most expensive parts:

- write-model design
- projection consistency
- durable provider contracts
- test duplication during the comparison phase

Least expensive parts:

- reusing public authoring concepts
- reusing many acceptance scenarios
- reusing current product semantics as the benchmark

## Recommendation

The right next move is not to replace the current engine.

The right next move is:

1. define the command/event/projection specs
2. build a narrow prototype
3. run it against shared acceptance slices
4. decide from evidence whether it should become the future durable/saga core
