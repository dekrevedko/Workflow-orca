# Minimal Core Scope

Reviewed on March 15, 2026.

## Purpose

This document defines the first implementation scope for OrcaCore.

It is intentionally narrower than the full product vision.

The goal is to choose the smallest coherent runtime slice that:

- proves the engine shape
- gives a real base for TDD
- avoids premature infrastructure and durability commitments
- still exercises the most important workflow semantics

This document answers three questions:

1. What is the minimal core scope?
2. What open questions still matter inside that scope?
3. What acceptance tests should drive interface and contract design for that scope?

## Design decision

### DD-010: The first implementation scope should be regular workflow plus ephemeral mode only

Decision:

The first implementation slice should support only regular workflows running in ephemeral mode.

Why:

- it is the smallest coherent slice that still proves workflow structure, waits, events, branching, and management
- it avoids premature commitment to persistence contracts, rehydration, versioning, outbox consistency, and durable provider abstractions
- it allows the first interfaces to be discovered through tests without mixing durable and saga semantics too early
- it is still rich enough to surface the hardest core semantic questions such as step outcomes, wait matching, and parallel join behavior

Implication:

The first implementation scope is not the full product baseline. It is only the first TDD slice.

**Scope expansion note** (added after review): the event subsystem was deliberately expanded beyond the original "instance-targeted only" plan to include correlation-targeted routing, definition-targeted fanout, and a correlation index. This was driven by the design insight that correlation-targeted routing (CorrelationId-only event delivery) is a basic usability requirement for external integration, and that designing the routing subsystem holistically (including fanout) avoids contradictions in the correlation index structure. This expansion adds ~4 types and 2 acceptance tests (MC-AT-017, MC-AT-018) but proves the full event routing subsystem before moving to durable mode. See DD-123 and DD-124 for rationale.

## 1. Minimal core scope

### Included semantic axes

- definition semantics:
  - regular workflow only
- execution mode:
  - ephemeral only

### Included runtime capabilities

The minimal core should include:

- code-first workflow definitions
- strongly typed business state for the first slice
- engine-owned runtime state
- stable in-memory `InstanceId`
- async business step execution contracts
- serialized execution per instance inside one process
- explicit step-result contract
- external-event waiting
- correlated resume of a waiting instance
- three event routing modes: instance-targeted, correlation-targeted, definition-targeted (fanout)
- correlation index for CorrelationId-only event delivery
- parallel branches
- `WhenAll` join semantics
- minimal management API
- runtime inspection of status and active waits

### Included infrastructure/control-flow primitives

The first slice should include only these shared primitives:

- `Init`
- `End`
- `If`
- `While`
- `Parallel`
- `WhenAll`
- `Wait`

### Included management surface

The first slice should include only the smallest useful management surface:

- `WorkflowEngine.Instance(id)` â†’ `InstanceScope`
- `WorkflowEngine.ForDefinition<TState>(definitionId)` â†’ `WorkflowEngine<TState>`
- `WorkflowEngine.All()` â†’ `SelectionScope`
- `WorkflowEngine.Where(...)` â†’ `SelectionScope`
- `WorkflowEngine.RaiseEvent(envelope)` â€” correlation-targeted routing (resolves instance by CorrelationId)
- `WorkflowEngine<TState>.Start(input)` â†’ `WorkflowInstanceSnapshot`
- `WorkflowEngine<TState>.RaiseEvent(envelope)` â€” definition-targeted fanout
- `InstanceScope.RaiseEvent(envelope)` â€” instance-targeted delivery
- `InstanceScope.GetActiveWaits()`
- `InstanceScope.Get()` â†’ `WorkflowInstanceSnapshot`
- `InstanceScope.GetState<TState>()` â€” inspection API, returns snapshot copy
- `SelectionScope.List()`

Optional for the same slice if it stays simple:

- `SelectionScope.Count()`

### Included runtime states

The first slice should support at least:

- `Running`
- `Waiting`
- `Completed`
- `Failed`

A distinct `Created` or `CompletedWithError` state may be introduced later if tests justify it.

### Included observability

The first slice should expose enough runtime metadata to support tests and operator inspection:

- instance identifier
- definition identifier
- current status
- active waits
- branch state for active parallel execution
- last transition timestamp
- terminal error details when failed

## 2. Explicit exclusions from the first slice

These are intentionally out of scope for the first implementation.

### Excluded semantic kinds

- saga workflows
- compensation semantics
- `CompensationScope`
- `Compensate`
- `Try`
- `Catch`
- `Finally`

### Excluded runtime modes and durability features

- durable mode
- persistence providers
- rehydration
- `WaitLong`
- durable timers
- retention
- archive/purge
- definition version binding
- durable history/checkpoint inspection
- outbox/inbox guarantees
- multi-node ownership

### Excluded control-flow and policy features

- `WhenFirst`
- `Delay` or `Timer`
- `ChildWorkflow`
- step retry policy
- step timeout policy
- pause/resume/cancel/terminate management
- lifecycle event publication
- stuck detection
- eviction policy

### Excluded API complexity

- durable/ephemeral compile-time API split
- query translation across provider backends
- DSL parity between code-first and serialized definitions

## 3. Minimal core requirements

### 3.1 Definition model

The engine must allow a regular workflow definition composed from infrastructure primitives and business steps.

The definition model for the first slice must be sufficient to express:

- sequential flow
- conditional flow
- loop flow
- parallel fan-out
- all-branches join
- wait for external event
- terminal completion or failure

### 3.2 State model

The engine must separate:

- business state: workflow-owned typed state
- runtime state: engine-owned execution metadata

The first slice does not need persistence, but it should still preserve this separation in the public design.

### 3.3 Execution model

The engine must execute one workflow instance with serialized state mutation semantics.

Parallel branch work may progress independently, but observable instance-state commits must appear in one valid serial order.

### 3.4 Step-result contract

Business steps must not directly mutate engine internals.

The first slice needs a minimal explicit outcome model capable of expressing at least:

- continue / complete current step
- fail
- wait for event
- publish branch completion back to parent runtime semantics

Whether these are represented as return objects, discriminated unions, or builder results remains open.

### 3.5 Wait and event model

The engine must support `Wait` as an explicit primitive in ephemeral mode.

The first slice must define:

- how a wait is represented in runtime state
- how an incoming event is matched to a wait
- how non-matching events are handled
- how duplicate event delivery is handled in-memory
- how waits in parallel branches remain isolated

### 3.6 Minimal management model

The first slice must support:

- starting a workflow instance
- listing visible instances
- selecting a specific instance
- raising an event to resume a waiting instance
- inspecting active waits

The management API should already follow the intended fluent semantics:

- scope selection
- `Where(...)` filtering
- terminal query or terminal command

### 3.7 Failure model

The first slice must define only minimal failure behavior:

- a failing step moves the workflow to `Failed`
- failed workflows do not continue automatically
- no retry semantics are included yet

## 4. Open questions inside the minimal core

These questions still affect the first slice and should be resolved before or during TDD.

### Q1. What is the smallest valid authoring contract?

Options:

- graph builder
- fluent builder
- low-level object model first

Reason it matters now:

The first acceptance tests need some stable definition surface, even if it is provisional.

### Q2. What is the minimal step-result model?

This is probably the most important scope-local question.

The first slice needs a result model that can represent:

- success
- failure
- wait request
- branch completion

If this model is wrong, the rest of the runtime contracts will drift.

### Q3. How should event correlation work in the first slice? â€” ANSWERED

**Answer (DD-122, DD-123, DD-124)**: The first slice includes three event routing modes:

1. **Instance-targeted**: `engine.Instance(id).RaiseEvent(envelope)` â€” direct delivery
2. **Correlation-targeted**: `engine.RaiseEvent(envelope)` â€” engine looks up instance by `(EventName, CorrelationId)` from a multi-map correlation index; must resolve to exactly one instance
3. **Definition-targeted (fanout)**: `engine.ForDefinition<T>().RaiseEvent(envelope)` â€” broadcast to all instances of a definition type

Within each instance, matching uses `EventName + CorrelationId` against active `WaitRecord` entries. CorrelationId is a first-class request-reply identity (renamed from CorrelationKey). The correlation index is a multi-map (`key â†’ HashSet<InstanceId>`); uniqueness is enforced at correlation-targeted routing time only, not at registration time.

### Q4. What is the exact `Parallel` plus `WhenAll` contract?

The first slice must define:

- when parallel branches become active
- how branch completion is recorded
- when the join fires
- how often the join continuation may execute

Recommendation:

The join continuation must execute exactly once after the last incomplete branch commits.

### Q5. What does `Where(...)` mean in the first slice?

Because the first slice is ephemeral-only and in-memory, `Where(...)` can be implemented more freely.

But public semantics should still assume a constrained query model, not arbitrary future storage execution.

Recommendation:

Write tests against the intended query semantics, not provider-specific in-memory shortcuts.

### Q6. Should `WaitLong` be absent at compile time or rejected at runtime in the first slice?

The product direction prefers API-level separation where practical.

Recommendation:

For the first slice, it is acceptable if `WaitLong` is rejected by the ephemeral authoring/runtime surface, as long as the unsupported durable semantics are impossible or clearly invalid.

## 5. Deferred questions outside the minimal core

These questions are real, but they should not block the first TDD slice:

- durable recovery model: replay vs checkpoint vs hybrid
- version binding and deployment rules
- lifecycle event durability
- outbox/inbox consistency
- active-instance eviction policy
- pause/resume/terminate semantics
- saga compensation ordering and failure behavior
- `WhenFirst` losing-branch semantics
- retry and timeout policy model
- child workflows and continue-as-new

## 6. Acceptance-test plan for the minimal core

These are the acceptance tests that should drive the first interfaces and contracts.

### Core acceptance tests for slice 1

#### MC-AT-001: Start runs a simple workflow to completion

Given a regular ephemeral workflow with `Init`, one business step, and `End`
When the workflow is started
Then one instance is created
And the workflow reaches `Completed`
And the final business state is observable

#### MC-AT-002: Conditional path selects the correct branch

Given a regular ephemeral workflow with `If`
When the condition is true or false
Then only the intended path executes
And the other path does not execute

#### MC-AT-003: Wait transitions instance into waiting state

Given a workflow that reaches `Wait`
When execution reaches the wait point
Then the instance status becomes `Waiting`
And the active wait is queryable from management APIs

#### MC-AT-004: Matching event resumes the waiting workflow exactly once

Given a waiting workflow instance
When a matching event is raised to that instance
Then the workflow resumes exactly once
And it continues to the next defined step or terminal state

#### MC-AT-005: Non-matching event does not resume the workflow

Given a waiting workflow instance
When a non-matching event is raised
Then the workflow remains `Waiting`
And the active wait remains unchanged

#### MC-AT-006: Parallel branches join exactly once with `WhenAll`

Given a workflow with parallel branches followed by `WhenAll`
When all branches complete
Then the join continuation executes exactly once
And the workflow continues deterministically

#### MC-AT-007: Different waits in parallel branches remain isolated

Given a workflow with two parallel branches waiting for different events
When matching events arrive in any order
Then each branch resumes only from its own matching event
And no branch consumes the other branch's event

#### MC-AT-008: Parallel branch completion order does not change final join behavior

Given the same parallel workflow executed multiple times with different branch interleavings
When all branches complete
Then the same final observable workflow outcome occurs

#### MC-AT-013: While loop executes body until condition is false

Given a workflow with `While` that loops a fixed number of iterations
When the workflow executes
Then the loop body executes the expected number of times
And the workflow continues past the loop when the condition becomes false

#### MC-AT-014: Wait inside a While loop resumes correctly across iterations

Given a workflow with a `While` loop containing a `Wait`
When matching events arrive for each iteration
Then each iteration waits and resumes independently
And later iterations do not accidentally match earlier wait subscriptions

#### MC-AT-009: Failing step moves workflow to failed state

Given a workflow with a business step that fails
When the failure occurs
Then the workflow reaches `Failed`
And no later step executes

#### MC-AT-010: Management query selects instances by runtime metadata

Given multiple running, waiting, completed, and failed instances
When management selection is performed with `Where(...)`
Then the selected set matches the documented runtime metadata semantics

#### MC-AT-011: `WaitLong` is unavailable in the minimal core

Given the first implementation slice is ephemeral-only
When a workflow definition attempts to use `WaitLong`
Then the definition or execution is rejected with explicit diagnostics

#### MC-AT-015: Out-of-order event arrival is handled by per-instance buffering

Given a workflow that waits for event A, then after resuming waits for event B
When event B arrives first (before the workflow reaches the wait-for-B point)
And then event A arrives
Then event B is buffered in the instance mailbox
And event A matches the active wait, resuming the workflow
And when the workflow advances to wait for event B, the buffered event is consumed immediately
And the workflow completes normally without requiring event B to be re-sent

#### MC-AT-016: Duplicate event delivery is deduplicated

Given a waiting workflow instance with an active wait
When the same event (same EventId) is raised twice
Then the workflow resumes at most once
And the second delivery is silently ignored

#### MC-AT-012: Concurrent resume attempts resolve as one valid serialized outcome

Given a waiting workflow instance
When two matching resume attempts race in the same process
Then the final observable outcome is equivalent to a valid serialized order
And the workflow does not continue twice

#### MC-AT-017: Correlation-targeted event routes to correct instance without InstanceId

Given two workflow instances (A and B) of the same definition, both waiting
  Instance A waits with EventName = "PriceResponse", CorrelationId = "req-1"
  Instance B waits with EventName = "PriceResponse", CorrelationId = "req-2"
When engine.RaiseEvent(envelope with EventName = "PriceResponse", CorrelationId = "req-1") is called
Then only instance A resumes
And instance B remains Waiting
And the caller did not need to specify an InstanceId

#### MC-AT-018: Fanout event resumes all matching instances of a definition type

Given three workflow instances of MyWorkflow, all waiting for EventName = "MarketClosed", CorrelationId = "2026-03-15"
  And one workflow instance of OtherWorkflow, also waiting for EventName = "MarketClosed", CorrelationId = "2026-03-15"
When engine.ForDefinition<MyWorkflow>().RaiseEvent(envelope with EventName = "MarketClosed", CorrelationId = "2026-03-15") is called
Then all three MyWorkflow instances resume
And the OtherWorkflow instance remains Waiting

## 7. Test-order recommendation

The first TDD pass should implement the acceptance tests in this order:

1. `MC-AT-001` Start runs a simple workflow to completion
2. `MC-AT-003` Wait transitions instance into waiting state
3. `MC-AT-004` Matching event resumes the waiting workflow exactly once
4. `MC-AT-005` Non-matching event does not resume the workflow
5. `MC-AT-015` Out-of-order event arrival is handled by per-instance buffering
6. `MC-AT-016` Duplicate event delivery is deduplicated
7. `MC-AT-017` Correlation-targeted event routes to correct instance without InstanceId
8. `MC-AT-018` Fanout event resumes all matching instances of a definition type
9. `MC-AT-002` Conditional path selects the correct branch
10. `MC-AT-013` While loop executes body until condition is false
11. `MC-AT-009` Failing step moves workflow to failed state
12. `MC-AT-006` Parallel branches join exactly once with `WhenAll`
13. `MC-AT-007` Different waits in parallel branches remain isolated
14. `MC-AT-008` Parallel branch completion order does not change final join behavior
15. `MC-AT-014` Wait inside a While loop resumes correctly across iterations
16. `MC-AT-010` Management query selects instances by runtime metadata
17. `MC-AT-012` Concurrent resume attempts resolve as one valid serialized outcome
18. `MC-AT-011` `WaitLong` is unavailable in the minimal core

Why this order:

- it proves the straight-line execution model first
- it introduces wait/resume before branch complexity
- it proves out-of-order and dedup right after basic wait/resume, before any other feature depends on event semantics
- it proves correlation-targeted and fanout routing right after the event subsystem is solid, before branching complexity
- it adds conditional flow, then loop flow before failure
- it adds failure before parallelism
- it tests wait-inside-loop after both parallel and loop are proven independently
- it adds management queries only after there is meaningful runtime state to inspect
- it leaves unsupported-feature guarding until the core slice already exists

## 8. What this scope should produce

The first TDD slice should produce enough contract clarity to later define:

- workflow definition interfaces
- step-result interfaces
- runtime instance model
- active wait model
- minimal management interfaces

It should not yet try to solve:

- durable persistence interfaces
- provider capability abstractions
- saga compensation contracts
- lifecycle publication contracts

## 9. Final position

The minimal core for OrcaCore should be:

- regular workflow only
- ephemeral mode only
- `Init`, `End`, `If`, `While`, `Parallel`, `WhenAll`, `Wait`
- typed business state
- explicit runtime state
- serialized execution per instance
- minimal fluent management surface
- per-instance event buffer (mailbox) for order-independent event delivery
- event deduplication by EventId
- three event routing modes: instance-targeted, correlation-targeted (CorrelationId-only), definition-targeted (fanout)
- correlation index for CorrelationId-only event delivery without requiring InstanceId
- acceptance tests focused on start, wait/resume, out-of-order events, dedup, correlation routing, fanout, loops, branch isolation, join determinism, wait-in-loop isolation, failure, and inspection

That is the smallest useful scope that still tests the real engine semantics, including the wait-inside-loop risk, order-independent event delivery, and the full event routing subsystem.
