# Regular Workflow Initial Requirements

## Purpose

Define the first implementable requirements slice for OrcaCore regular workflows.

This is the active implementation scope.

## Scope

Semantic kind:

- regular workflow

Execution mode:

- ephemeral only

Why:

- this is the smallest slice that proves the runtime model
- it keeps durability and saga semantics out of the first implementation
- it still covers the highest-risk correctness areas: waits, loops, branches, joins, event routing, and serialized execution

## Included primitives

The initial regular workflow slice must support:

- `Init`
- business step
- `End`
- `If`
- `While`
- `Parallel`
- `WhenAll`
- `Wait`

The initial slice must also support:

- event delivery by instance-targeted routing
- event delivery by correlation-targeted routing
- definition-scoped fanout delivery
- active wait tracking
- pending-event buffering
- duplicate-event deduplication by `EventId`

## Excluded primitives and features

The initial regular workflow slice must not require:

- `WaitLong`
- `WhenFirst`
- durable timers
- persistence providers
- replay
- rehydration after restart
- saga compensation
- retry policies
- timeout policies
- lifecycle event publication
- archive/purge/retention features

Why:

- these features introduce different semantics and failure models
- they would distort the first TDD contracts before the core runtime is proven

## Core runtime requirements

### RR-I-001: Typed workflow business state

Each workflow instance must carry typed business state that steps can mutate during execution.

Why:

- business logic needs a stable state object
- tests must be able to verify outcomes

### RR-I-002: Separate runtime state and business state

The engine must keep orchestration metadata separate from workflow business state.

Runtime state must include at least:

- workflow status
- execution pointer
- active waits
- pending events
- branch state
- error details
- consumed event IDs

Why:

- orchestration metadata is engine-owned
- business state should not become the only persisted or inspectable shape later

### RR-I-003: Serialized execution per instance

For a given `InstanceId`, the engine must produce a serialized committed outcome.

Concurrent attempts to advance the same instance must resolve into one valid sequential result.

Why:

- this is the main defense against branch/wait race conditions
- it preserves the Orleans-like one-logical-mutator guarantee

### RR-I-004: Explicit lifecycle transitions

Workflow instance lifecycle must be explicit and validated.

Minimum statuses:

- `Running`
- `Waiting`
- `Completed`
- `Failed`

Illegal triggers on terminal states must be rejected clearly.

Why:

- lifecycle bugs are otherwise easy to hide in interpreter code
- acceptance tests need a precise state model

### RR-I-005: Runtime-owned orchestration

Business steps may mutate business state, but the runtime owns orchestration transitions.

Step outcomes must be expressed through `StepResult`.

Why:

- orchestration decisions must stay explicit
- this keeps future durable/replay-safe evolution possible

## Authoring requirements

### RR-I-006: Fluent workflow builder

The initial slice must expose a fluent builder for regular workflows.

The builder must be the primary public authoring path.

Why:

- it gives a stable authoring surface for TDD
- it avoids exposing internal graph construction prematurely

### RR-I-007: Immutable built definition

The builder must produce an immutable workflow definition graph.

Why:

- runtime execution should not depend on mutable definition state

## Event and wait requirements

### RR-I-008: Wait registration

`Wait` must register a runtime-owned wait record containing at least:

- `WaitId`
- `EventName`
- `CorrelationId`
- registration timestamp
- branch identity when inside parallel execution

### RR-I-009: Matching rule

Within an instance, event-to-wait matching must use:

- `EventName`
- `CorrelationId`

Why:

- this is the minimum rule that supports request-reply semantics and branch isolation

### RR-I-010: Event payload delivery

When a wait resumes an instance, the matched event envelope must be available to the next step through step context.

Why:

- resume without payload access would make waits operationally correct but practically weak

### RR-I-011: Pending-event buffering

If an event arrives before the matching wait is registered, the event must be buffered in the instance mailbox and checked again when a new wait is created.

Why:

- correctness must not depend on arrival order

### RR-I-012: Event deduplication

Duplicate delivery of the same `EventId` must not cause double resume or double continuation.

### RR-I-013: Three routing modes

The initial slice must support three event-routing entry points:

- instance-targeted
- correlation-targeted
- definition-targeted fanout

Correlation-targeted routing must require exactly one matching instance.

Fanout must deliver per definition and not affect other definitions.

Why:

- correlation-targeted delivery is a usability requirement
- fanout and correlation uniqueness shape the same routing subsystem

## Control-flow requirements

### RR-I-014: Conditional execution

`If` must execute exactly one branch and then continue after the block.

### RR-I-015: Loop execution

`While` must re-evaluate its condition between iterations and stop when the condition is false.

Waits created in one iteration must not be reusable by later iterations.

### RR-I-016: Parallel execution and join

`Parallel` must support multiple branches with isolated branch state.

`WhenAll` must fire the continuation exactly once after all branches complete.

Branch completion order must not change the final committed outcome.

## Failure and inspection requirements

### RR-I-017: Failure handling

A step that fails explicitly or throws an unhandled exception must move the workflow instance to `Failed`.

The error must be observable through instance inspection.

### RR-I-018: Management surface

The initial slice must expose a fluent management API with:

- engine-wide scope
- definition-scoped scope
- instance-scoped scope

The management API must support at least:

- `Start`
- `Instance(id)`
- `All()`
- `Where(...)`
- `List()`
- `Count()`
- `Get()`
- `GetState<TState>()`
- `GetActiveWaits()`
- `RaiseEvent(...)`

### RR-I-019: Canonical filtering model

The canonical public filter model must be `Where(x => ...)`, not special-purpose filter methods.

The supported expression subset must be constrained and query-safe.

Why:

- it keeps the API fluent and composable
- it avoids binding the public contract to arbitrary in-memory predicates

## API constraints

### RR-I-020: Durable-only features hidden from initial API

Durable-only constructs should be absent from the ephemeral-facing builder and management API where practical.

`WaitLong` must not be exposed by the initial workflow builder.

Why:

- invalid combinations should be prevented early
- compile-time absence is better than runtime confusion when practical

## Non-functional requirements

### RR-I-021: Async-first surface

All step execution and management operations that may perform work must be async and accept `CancellationToken`.

### RR-I-022: No live-instance leakage

Public APIs must not expose the mutable internal workflow instance object.

Snapshots and copied business state are allowed; live runtime objects are not.

### RR-I-023: .NET quality baseline

The initial implementation should use:

- nullable enabled
- warnings as errors
- immutable contracts where practical
- clear separation between `Abstractions`, `Runtime`, and tests

## Traceability

This document is implemented by:

- [Regular / Initial acceptance criteria](/X:/Projects/GitHub/Workflow-orca/docs/requirements/regular/initial/acceptance-criteria.md)
- [Implementation plan: minimal core](/X:/Projects/GitHub/Workflow-orca/docs/plans/implementation-plan-minimal-core.md)
