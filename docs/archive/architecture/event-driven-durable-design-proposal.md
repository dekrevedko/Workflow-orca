# Event-Driven Durable Design Proposal

Reviewed on March 17, 2026.

## Purpose

This document proposes a new OrcaCore design that is intentionally separate from the current implementation.

The proposal is centered on:

- event-driven orchestration
- append-only durable facts
- projection-driven querying
- per-instance serialized command handling
- optional event-sourced recovery with checkpoint compaction

It is designed to satisfy the currently collected requirements and acceptance criteria across:

- regular workflow
- durable workflow
- initial saga semantics
- advanced durable direction

## Executive Summary

The recommended direction is not a pure interpreter with mutable persisted snapshots as the primary truth.

It is:

- command-driven at the write side
- event-driven at the domain/runtime level
- projection-driven at the read side
- hybrid event-sourced for durability

In concrete terms:

1. Every instance mutation is requested as a command.
2. The engine loads the instance write model from the event stream plus latest checkpoint.
3. A deterministic decision layer produces domain events.
4. Those events are appended atomically with outbox/inbox effects.
5. Read models are updated from the committed event stream.
6. Hot memory is only a cache of the durable stream and projections.

This satisfies the strongest requirements in the repo better than the current snapshot-first design, especially:

- crash safety
- restart-safe deduplication
- durable inspection without business-state-only deserialization
- durable long waits
- saga compensation traceability
- future multi-node execution
- future continue-as-new / history compaction

## Why This Fits The Requirements Better

The repo requirements repeatedly push toward the same shape:

- one logical mutator per instance
- durable wait ownership
- queryable runtime metadata
- crash-safe committed state only
- outbox/inbox consistency
- future multi-node ownership
- future history pressure visibility

Those are all natural fits for:

- append-only durable facts
- optimistic concurrency on stream version
- explicit command and event boundaries
- projections for querying and routing

They are a worse fit for:

- opaque mutable snapshots as the only source of truth
- in-memory routing indexes as the primary durable mechanism
- reconstructing semantics from ad hoc persisted DTOs only

## Core Design

### 1. Main Runtime Concepts

The design has six core concepts:

1. `WorkflowDefinition`
   - immutable authored model
   - regular or saga semantic kind
   - versioned

2. `WorkflowInstanceStream`
   - append-only event stream per `InstanceId`
   - the durable write-side truth

3. `WorkflowAggregate`
   - deterministic in-memory decision model reconstructed from stream + checkpoint
   - owns runtime state and business state

4. `WorkflowCommand`
   - intent to mutate one instance
   - examples:
     - `StartWorkflow`
     - `DeliverEvent`
     - `FireTimer`
     - `CompensateSaga`
     - `DeleteInstance`

5. `WorkflowEvent`
   - committed durable fact
   - examples:
     - `WorkflowStarted`
     - `StepCompleted`
     - `WaitRegistered`
     - `EventBuffered`
     - `BufferedEventConsumed`
     - `WaitMatched`
     - `WorkflowCompleted`
     - `WorkflowFailed`
     - `CompensationStarted`
     - `CompensationStepCompleted`

6. `Projection`
   - read model derived from the event stream
   - optimized for querying, routing, retention, and operations

### 2. Write Side

Each instance is treated as an aggregate with a single serialized command lane.

Write path:

1. receive command
2. resolve target instance or create new one
3. load checkpoint + subsequent events
4. rebuild aggregate state deterministically
5. decide new events from command + current state
6. append events with expected stream version
7. update inbox/outbox and projections in the same durability boundary or in a clearly defined transactional chain

This is the key correctness contract:

- commands are not durable truth
- events are durable truth
- projections are derived truth
- hot memory is disposable

### 3. Read Side

The read side should not query workflow business payloads directly.

Instead, maintain projections such as:

- `InstanceSummaryProjection`
  - `InstanceId`
  - `DefinitionId`
  - `DefinitionVersion`
  - `Status`
  - `CreatedAt`
  - `UpdatedAt`
  - `CurrentStepPath`
  - `CurrentCheckpointVersion`

- `ActiveWaitProjection`
  - `InstanceId`
  - `WaitId`
  - `EventName`
  - `CorrelationId`
  - `BranchId`
  - `WaitMode`
  - `RegisteredAt`
  - `TimeoutAt`

- `PendingEventProjection`
  - `InstanceId`
  - `EventId`
  - `EventName`
  - `CorrelationId`
  - `ReceivedAt`

- `HistoryProjection`
  - operator-facing timeline/debug shape

- `SagaCompensationProjection`
  - completed forward actions eligible for rollback

This directly satisfies the requirement that durable metadata stay queryable without relying on business payload inspection.

## Recommended Recovery Model

Use a hybrid event-sourced model:

- short-term recovery: replay from latest checkpoint
- long-term truth: append-only event stream
- history pressure control: checkpoint compaction / continue-as-new

This aligns with `DR-A-001`.

It is better than:

- pure replay from the beginning forever
- pure snapshot-only persistence with no durable fact trail

Recommended structure:

- `WorkflowEvents` stream table/log
- `WorkflowCheckpoint` latest materialized aggregate state
- `WorkflowInbox`
- `WorkflowOutbox`
- `WorkflowProjection` tables

Recovery:

1. load checkpoint for instance
2. load stream tail after checkpoint version
3. replay tail into aggregate
4. execute next command

## Commands And Events

### Command Model

Recommended command types:

- `StartWorkflowCommand`
- `DeliverInstanceEventCommand`
- `DeliverCorrelationEventCommand`
- `DeliverDefinitionEventCommand`
- `RegisterTimerCommand`
- `FireTimerCommand`
- `DeleteInstanceCommand`
- `PurgeArtifactsCommand`
- `CompensateSagaCommand`
- `RetryStepCommand`
- `TimeoutExpiredCommand`

Commands should be explicit and idempotency-aware.

Every command should carry:

- `CommandId`
- `InstanceId` or routing envelope
- `OccurredAt`
- causation metadata
- correlation metadata where applicable

### Event Model

Recommended durable events:

- `WorkflowStarted`
- `DefinitionVersionBound`
- `StepEntered`
- `StepSucceeded`
- `StepFailed`
- `WaitRegistered`
- `WaitModeChanged`
- `EventBuffered`
- `BufferedEventDiscardedAsDuplicate`
- `BufferedEventConsumed`
- `WaitMatched`
- `TimerScheduled`
- `TimerFired`
- `BranchStarted`
- `BranchCompleted`
- `JoinSatisfied`
- `WorkflowCompleted`
- `WorkflowFailed`
- `WorkflowDeleted`
- `ForwardActionCompleted`
- `CompensationStarted`
- `CompensationActionCompleted`
- `CompensationActionFailed`
- `CompensationCompleted`

The goal is not to expose all of these as public API. The goal is to keep the engine's durable facts explicit.

## Instance Model

Each aggregate reconstructs two state categories:

- runtime state
- business state

Runtime state:

- lifecycle status
- current execution location
- active waits
- pending events
- consumed event ids
- active branches
- join state
- failure details
- saga compensation stack
- timer subscriptions
- stream version / epoch

Business state:

- typed application state

This preserves the repo requirement that runtime metadata stay separate from business state.

## Execution Model

### Serialized Per Instance

The execution rule is:

- one committed command at a time per `InstanceId`

Implementation options:

- single-host: in-memory mailbox + optimistic append
- multi-host: optimistic append plus optional lease/partition owner

The append boundary is the real correctness guarantee.

This directly satisfies:

- `RR-I-003`
- `DR-I-006`
- `AT-029`
- `AT-030`
- `DRI-AT-006`

### Parallel

Keep the same semantic rule already supported by the requirements:

- parallel branches are logically independent
- branch progress is committed serially through one aggregate
- branch completion order may vary
- final observable result must be deterministic

So this design is event-driven, but not physically concurrent per instance by default.

### Wait And WaitLong

Model waits as subscriptions owned by the instance aggregate.

`Wait`

- active wait subscription
- instance may remain hot
- still fully durable in durable mode

`WaitLong`

- same subscription shape
- `WaitMode = Cold`
- instance becomes immediately evictable after `WaitRegistered` commit

This matches the current product intent while fitting naturally into the event model.

## Routing Design

Routing should not inspect whole instance payloads.

Instead:

1. event arrives with `EventName`, `CorrelationId`, `EventId`
2. router queries `ActiveWaitProjection`
3. routing result is:
   - no match
   - one match
   - ambiguous match
4. if matched:
   - emit `DeliverInstanceEventCommand`
5. if unmatched and instance-targeted:
   - emit buffer command for that instance
6. if unmatched and correlation-targeted:
   - reject clearly

This satisfies:

- instance-targeted routing
- correlation-targeted exact-one rule
- definition fanout
- no dependency on hot in-memory indexes for durable correctness

## Inbox / Outbox Design

This is a first-class part of the design, not an add-on.

### Inbox

Inbox tracks received external deliveries by `EventId`.

Recommended states:

- `Received`
- `Applied`
- `DuplicateIgnored`
- `Poisoned`

Inbox purpose:

- restart-safe dedup
- operational audit
- replay defense

### Outbox

Outbox records publication intent from committed workflow events.

Outbox purpose:

- consistent publish-after-commit
- retryable delivery
- poison handling

The cleanest model is:

- append workflow events
- derive outbox records transactionally from those events
- dispatch asynchronously

This is strongly aligned with the research on MassTransit and durable messaging concerns.

## Saga Design

Saga should be implemented as a separate semantic definition kind on top of the same runtime substrate.

That means:

- same command/event infrastructure
- different semantic decision rules

Saga-specific events:

- `ForwardActionStarted`
- `ForwardActionCompleted`
- `CompensationRegistered`
- `CompensationStarted`
- `CompensationCompleted`
- `CompensationFailed`
- `SagaCompleted`
- `SagaCompensated`

Saga aggregate state maintains:

- completed forward actions
- compensation eligibility
- compensation order
- compensation outcome

This directly satisfies the initial saga requirements without leaking compensation semantics into regular workflows.

## Timers And Durable Wake-Up

Timers should be modeled as scheduled commands, not as hidden thread timers.

Recommended design:

- `TimerScheduled` event
- provider-owned scheduler/reminder table or broker delay mechanism
- wake-up produces `FireTimerCommand`

This cleanly supports:

- timeout policies
- `WaitLong`
- durable reminders
- future stuck-instance remediation

## Versioning

Each instance stream is bound to:

- `DefinitionId`
- `DefinitionVersion`

Version binding should be an early durable fact:

- `WorkflowStarted`
- `DefinitionVersionBound`

Registration behavior:

- same `DefinitionId` + same compatible version: allowed
- incompatible version for existing active instances: reject or route through explicit migration path

This remains explicit and easy to enforce in an event-stream model.

## Query Surface

Public management APIs can stay similar to the current requirement shape:

- `Start`
- `Instance(id)`
- `All()`
- `Where(...)`
- `ListAsync()`
- `CountAsync()`
- `GetAsync()`
- `GetStateAsync<T>()`
- `GetActiveWaitsAsync()`
- `RaiseEvent(...)`

But their backing source changes:

- snapshots and lists come from projections
- state rehydration comes from checkpoint + stream tail
- active waits come from either projection or aggregate snapshot

This keeps the user-facing shape while improving the backend architecture.

## Storage Model

Provider contract should be shaped around stream append and projection support, not only mutable instance upsert.

Recommended provider-facing contracts:

- `IWorkflowEventStore`
  - append events with expected version
  - load stream tail
  - load checkpoint
  - save checkpoint

- `IWorkflowInboxStore`
  - record inbound event
  - query by `EventId`
  - mark applied / duplicate / poison

- `IWorkflowOutboxStore`
  - append outgoing messages
  - claim / dispatch / poison

- `IWorkflowProjectionStore`
  - update/query instance summaries, waits, history

- `ITimerScheduler`
  - schedule wake-up
  - cancel wake-up if needed

A reference provider may compose all of those behind one logical transaction boundary.

## Where Event Sourcing Should And Should Not Be Used

Use event sourcing for:

- runtime orchestration facts
- saga progression
- waits
- branch completion
- operator history
- crash recovery semantics

Do not force business event sourcing on user state by default.

Instead:

- business state is still a typed mutable model inside the aggregate
- the engine persists workflow/runtime events
- checkpoints persist materialized business state for efficient recovery

This keeps the engine event-sourced without forcing every user into full domain event sourcing.

## How This Satisfies The Acceptance Criteria

### Regular workflow

- straight-line, `If`, `While`, `Parallel`, `WhenAll`
  - handled by deterministic command-to-event decisions
- waits and payload delivery
  - `WaitRegistered`, `WaitMatched`, payload attached to `DeliverEvent`
- out-of-order events
  - `EventBuffered`, later `BufferedEventConsumed`
- duplicate events
  - inbox dedup by `EventId`
- correlation-targeted routing
  - `ActiveWaitProjection`

### Durable workflow

- restart-safe wait resume
  - wait subscription lives in stream + projection
- crash restores committed state only
  - append-only expected-version commit boundary
- durable query
  - instance summary and wait projections
- version binding
  - explicit stream facts

### Saga

- failure triggers compensation
  - compensating commands emitted from saga decision layer
- deterministic compensation order
  - stored in compensation stack/order facts
- compensation failure observable
  - explicit saga terminal event

### Advanced durable

- recovery model
  - explicitly hybrid
- history pressure visibility
  - stream length, checkpoint lag, outbox backlog projections
- multi-node ownership
  - optimistic append now, lease later
- continue-as-new
  - natural fit by emitting rollover event and new checkpoint baseline

## Recommended Public Authoring Model

Keep the current builder-style authoring idea, but compile it into a deterministic execution plan.

Authoring model:

- fluent builder or DSL remains
- builder compiles to a versioned workflow plan
- runtime executes plan through commands and events

This means the public authoring model does not need to become “message saga only.”

That is important because the research correctly notes that OrcaCore should not collapse into a pure bus-oriented saga library.

## Tradeoffs

### Benefits

- stronger crash semantics
- cleaner durable audit/history
- natural inbox/outbox integration
- better alignment with multi-node future
- easier operator projections
- better saga fit
- clearer transaction boundaries

### Costs

- more infrastructure pieces
- more event and projection design work
- stronger determinism requirements
- versioning of runtime events becomes part of the contract
- provider implementation is more involved than simple instance upsert

## Recommended Decision

Adopt a hybrid event-driven, event-sourced durable core:

- commands in
- durable workflow events as truth
- checkpoints for efficient recovery
- projections for query and routing
- inbox/outbox as first-class durable components
- builder/DSL retained as the authoring surface

This is the best fit for the repo's current requirements.

It is especially strong if OrcaCore is intended to become:

- durable-first
- saga-capable
- operationally inspectable
- eventually multi-node

## Suggested Next Design Docs

If this direction is accepted, the next design artifacts should be:

1. write-model command/event catalog
2. projection catalog and query contract
3. provider transaction-boundary specification
4. saga decision model on top of the same substrate
5. timer and reminder specification
6. acceptance matrix rewritten for event-driven internals

## Open Questions

- Should the provider contract require event-store semantics explicitly, or should a provider be allowed to emulate them over relational tables?
- Should workflow history be mandatory in durable mode, or optional with checkpoints-only plus essential operational events?
- Should definition fanout be implemented as many commands emitted by a query projection, or as a broker-native publish pattern with instance-side correlation?
- Should `ContinueAsNew` be a durable-only primitive from the start of the redesign, given the history-growth requirements already exist?
