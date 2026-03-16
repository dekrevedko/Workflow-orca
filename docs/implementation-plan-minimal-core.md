# Implementation Plan: Minimal Core TDD

## Approach

Test-driven development. Each step writes a failing acceptance test first, then the minimum types and implementation to make it pass. Types and interfaces are discovered through tests, not designed upfront in isolation.

The plan follows the TDD order from `minimal-core-scope.md` section 7.

---

## Step 0: Project scaffolding

**Goal**: Runnable empty test project with correct dependency structure.

**Actions**:

1. Create `global.json` pinning .NET 10 stable SDK version (use locally installed version)
2. `dotnet new sln -n OrcaCore` at repository root
3. `dotnet new classlib -n OrcaCore.Abstractions -o src/OrcaCore.Abstractions -f net10.0`
4. `dotnet new classlib -n OrcaCore.Runtime -o src/OrcaCore.Runtime -f net10.0`
5. `dotnet add src/OrcaCore.Runtime reference src/OrcaCore.Abstractions`
6. `dotnet new xunit -n OrcaCore.Tests -o tests/OrcaCore.Tests -f net10.0`
7. `dotnet add tests/OrcaCore.Tests reference src/OrcaCore.Runtime`
8. `dotnet sln add src/OrcaCore.Abstractions src/OrcaCore.Runtime tests/OrcaCore.Tests`
9. Verify `dotnet build` succeeds
10. Verify `dotnet test` succeeds (zero tests)

**.NET conventions** (established in Step 0, applied to all projects):
- `<Nullable>enable</Nullable>` in all projects
- `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` in all projects
- Prefer `record` for immutable contracts (`StepResult`, `EventEnvelope`, `WaitRecord`, `WorkflowInstanceSnapshot`, etc.)
- Prefer `record struct` for small value-semantic types where heap allocation is unnecessary
- Use primary constructors where they simplify record/class declarations
- `CancellationToken cancellationToken = default` on all public async methods (including `RaiseEvent`, `Start`)
- `WorkflowEngine` implements `IAsyncDisposable` from day one (disposes per-instance semaphores; cheap now, avoids breaking change when durable mode needs graceful shutdown)

**Tooling**: Use `dotnet` CLI for all project operations — `dotnet new` for files/projects, `dotnet add` for references/packages, `dotnet sln` for solution management.

**Produces**: Build infrastructure with .NET 10 stable quality baseline. No types yet.

**Validation**: `dotnet build` and `dotnet test` both succeed cleanly with zero warnings.

---

## Step 1: MC-AT-001 — Simple workflow runs to completion

**Goal**: Prove the straight-line execution path: define a workflow, start it, observe completion.

**Test scenario**:
```
Given a workflow: Init → BusinessStep (sets state.Result = "done") → End
When the workflow is started with input
Then snapshot.Status == Completed
And engine.Instance(snapshot.InstanceId).GetState<MyState>().Result == "done"
```

**Test writes first, then drives creation of**:

Types in Abstractions (discovered by what the test needs to compile):
- `WorkflowStatus` — enum: `Running`, `Waiting`, `Completed`, `Failed`
- `StepResult` — `abstract record` with `sealed record Completed`, `sealed record Failed(Exception Error)` subclasses (minimum needed; `WaitForEvent` and `Yield` added when tests require them)
- `StepContext<TState>` — record/class: `State`, `InstanceId`, `CancellationToken`
- `IStep<TState>` — interface: `StepId`, `ExecuteAsync(StepContext<TState>)`
- `WorkflowInstanceSnapshot` — immutable record: `InstanceId`, `DefinitionId`, `Status`, `CreatedAt`

Types in Runtime (discovered by what the test needs to run):
- `WorkflowBuilder<TState>` — fluent builder with `.Init(...)`, `.Step<T>()`, `.End()`, `.Build()`
- `WorkflowEngine` — engine-wide management entry point with `.Instance(id) → InstanceScope`
- `WorkflowEngine<T>` — definition-scoped management entry point with `.Start(input)`
- `InstanceScope` — instance-scoped management with `.Get() → WorkflowInstanceSnapshot`, `.GetState<TState>() → TState`
- `WorkflowRuntime` — interpreter loop: resolve step → execute → match result → advance
- Internal `WorkflowDefinition<TState>` — immutable graph produced by builder
- Internal `WorkflowInstance<TState>` — instance with `RuntimeState` + `BusinessState`
- Internal `RuntimeState` — `Status`, `ExecutionPointer`, `CreatedAt`, `LastTransitionAt`
- Internal `InstanceLifecycle` — state machine enforcing the transition table from design-proposal section 3.5. Validates that triggers are legal for the current state. Rejects illegal triggers with clear diagnostics. Used by the interpreter and event resume path.
- `InMemoryInstanceStore` — stores instances by ID. For Step 1, a simple `ConcurrentDictionary`-backed store is sufficient. Atomicity and transactional save semantics are introduced in Step 5 when the mailbox and event safety guarantee are tested.

**Key design decisions exercised**:
- Builder produces immutable definition
- Start returns snapshot, not live instance
- Business state is readable via `InstanceScope.GetState<TState>()`, not via snapshot
- Steps mutate `StepContext.State` directly, return `StepResult.Completed`
- Interpreter loop advances through step list
- Lifecycle transitions are explicit and enforced by `InstanceLifecycle`

**What to defer**: No `If`, no `While`, no `Parallel`, no `Wait`, no `Where(...)`. Only what MC-AT-001 requires.

**Validation**: Test passes. `dotnet test` green.

---

## Step 2: MC-AT-003 — Wait transitions instance into waiting state

**Goal**: Prove the wait primitive: a step can suspend the workflow, and the suspension is observable.

**Test scenario**:
```
Given a workflow: Init → Wait("OrderApproved", s => s.OrderId) → End
When the workflow is started
Then snapshot.Status == Waiting
And instance.GetActiveWaits() returns one WaitRecord
  with EventName == "OrderApproved" and CorrelationId == the order ID
```

**Drives creation of**:

Types in Abstractions:
- `StepResult.WaitForEvent` — subclass: `EventName`, `CorrelationId`
- `WaitRecord` — record: `WaitId`, `EventName`, `CorrelationId`, `BranchId?`, `RegisteredAt`, `Status`
- `WaitStatus` — enum: `Active`, `Matched`, `Cancelled`

Types in Runtime:
- Builder `.Wait(eventName, correlationSelector)` method
- Internal `WaitStep<TState>` — infrastructure step that returns `StepResult.WaitForEvent`
- `RuntimeState.ActiveWaits` — list of `WaitRecord`
- Interpreter: handle `WaitForEvent` result → register wait, set status to `Waiting`, stop loop
- `InstanceScope` — returned by `WorkflowEngine.Instance(id)`, with `.GetActiveWaits()`

**Key design decisions exercised**:
- Wait is a first-class runtime primitive
- Active waits are queryable through management API
- Instance scope provides per-instance operations

**Validation**: Test passes. Previous test (MC-AT-001) still passes.

---

## Step 3: MC-AT-004 — Matching event resumes the waiting workflow exactly once

**Goal**: Prove event-driven resume: raise a matching event, workflow continues to completion.

**Test scenario**:
```
Given the waiting workflow from MC-AT-003 (Init → Wait → BusinessStep → End)
When RaiseEvent is called with matching EventName + CorrelationId and Payload = { Price = 42.0 }
Then the workflow resumes from the wait point
And the BusinessStep receives ctx.ResumedEvent.Payload containing the price
And the workflow reaches Completed
And the matched WaitRecord status is Matched
```

**Drives creation of**:

Types in Abstractions:
- `EventEnvelope` — record: `EventName`, `CorrelationId`, `Payload?`, `EventId`

Types in Runtime:
- `InstanceScope.RaiseEvent(EventEnvelope)` — public method
- `EventMatcher` — matches `EventEnvelope` against `WaitRecord` by `EventName` + `CorrelationId`
- Interpreter resume path: on match → mark wait as Matched → set `StepContext.ResumedEvent = envelope` for next step → transition to Running → continue loop from wait point
- `InstanceExecutionSerializer` — `SemaphoreSlim(1,1)` per instance. Introduced now because the resume path must serialize with any ongoing execution. Even though MC-AT-012 tests this under pressure later, the serializer must exist from the first resume.
- Note: `PendingEvents` and mailbox buffering are NOT introduced here — they are deferred to Step 5 (MC-AT-015) where the test forces them

**Key design decisions exercised**:
- Instance-targeted event delivery
- EventName + CorrelationId matching
- Resume continues from the wait point (execution pointer preserved)
- Event payload delivered via `StepContext.ResumedEvent` (DD-126)
- Per-instance serialization from day one

**Validation**: Test passes. MC-AT-001 and MC-AT-003 still pass.

---

## Step 4: MC-AT-005 — Non-matching event does not resume the workflow

**Goal**: Prove negative matching: wrong event name or wrong correlation key leaves the workflow waiting.

**Test scenario**:
```
Given a waiting workflow expecting EventName="OrderApproved", CorrelationId="order-123"
When RaiseEvent is called with EventName="OrderApproved", CorrelationId="order-999"
Then the workflow remains Waiting
And the active wait is unchanged
When RaiseEvent is called with EventName="WrongEvent", CorrelationId="order-123"
Then the workflow remains Waiting
```

**Drives creation of**: No new types expected. Tests the negative path in `EventMatcher`.

**Also verify**: a non-matching event is buffered in the instance mailbox (not silently dropped). This sets up the contract for Step 5.

**Validation**: Test passes. All previous tests still pass.

---

## Step 5: MC-AT-015 — Out-of-order event arrival is handled by per-instance buffering

**Goal**: Prove that events arriving before a wait is registered are buffered and consumed when the wait appears.

**Test scenario**:
```
Given a workflow: Init → Step A → Wait("EventA", s => s.Id) → Step B → Wait("EventB", s => s.Id) → End
When started, workflow executes Step A, reaches Wait("EventA")
RaiseEvent("EventB", id) — no active wait for EventB yet → buffered
RaiseEvent("EventA", id) — matches active wait → workflow resumes
Workflow executes Step B, reaches Wait("EventB")
Wait registration checks mailbox → finds buffered EventB → consumes immediately
Workflow completes without EventB needing to be re-sent
```

**Drives creation of**:

Types in Abstractions:
- `PendingEvent` — record: `Envelope`, `ReceivedAt`, `Consumed`

Types in Runtime:
- `RaiseEvent` path: when no active wait matches → buffer event as `PendingEvent` in `RuntimeState.PendingEvents`
- `RegisterWait` path: when a new `WaitRecord` is created → check mailbox for pending match → if found, consume immediately and do not suspend
- Mailbox cleanup: pending events are removed when consumed or when instance reaches terminal state

**Key design decisions exercised**:
- Bidirectional matching: events find waits AND waits find events
- Event arrival order is irrelevant to workflow correctness
- All mailbox operations happen under the per-instance serializer
- **Event safety guarantee**: pending events remain in the mailbox until consumed AND state is committed. `IInstanceStore.Save()` atomically commits runtime state including pending events. Event removal happens only after successful save.

**Validation**: Test passes. All previous tests still pass. Verify MC-AT-004 still works (direct match path is unchanged).

---

## Step 6: MC-AT-016 — Duplicate event delivery is deduplicated

**Goal**: Prove that the same event (same `EventId`) cannot be consumed twice.

**Test scenario**:
```
Given a waiting workflow
When RaiseEvent is called with EventId="evt-1", matching the active wait → workflow resumes
When RaiseEvent is called again with EventId="evt-1"
Then the second call is silently ignored (idempotent no-op)
And the workflow does not resume again or advance further
```

**Also test**: duplicate event arriving while buffered (same EventId buffered twice → only one copy exists).

**Drives creation of**:
- `RuntimeState.ConsumedEventIds` — `HashSet<string>` tracking consumed event IDs
- Dedup check in `RaiseEvent` path: if `EventId` is in `ConsumedEventIds` or already in `PendingEvents`, ignore

**Validation**: Test passes. All previous tests still pass.

---

## Step 7: MC-AT-017 — Correlation-targeted event routes to correct instance

**Goal**: Prove that events can be routed by CorrelationId without specifying an InstanceId.

**Test scenario**:
```
Given two workflow instances (A and B) of the same definition, both waiting
  Instance A waits with EventName = "PriceResponse", CorrelationId = "req-1"
  Instance B waits with EventName = "PriceResponse", CorrelationId = "req-2"
When engine.RaiseEvent(envelope with EventName = "PriceResponse", CorrelationId = "req-1")
Then only instance A resumes and reaches Completed
And instance B remains Waiting
```

**Also test**:
- No matching wait: `engine.RaiseEvent(envelope with CorrelationId = "req-unknown")` throws `InvalidOperationException` with message "no active wait found for this CorrelationId"
- Ambiguous correlation: two instances register the same `(EventName, CorrelationId)`. Registration succeeds (index is a multi-map). `engine.RaiseEvent(envelope)` throws `InvalidOperationException` with message "ambiguous correlation — use instance-targeted or fanout routing"

**Note**: This step may refactor the `RaiseEvent` path from Step 3 to introduce the routing layer. Step 3's `InstanceScope.RaiseEvent` becomes a thin wrapper around `DeliverToInstance` via `EventRouter`.

**Drives creation of**:
- `CorrelationIndex` — in-memory multi-map: `Dictionary<(string EventName, string CorrelationId), HashSet<string InstanceId>>` for active waits
- `EventRouter` — routing logic: determines target instance(s) based on API entry point
- `WorkflowEngine.RaiseEvent(envelope)` — engine-wide correlation-targeted entry point
- Index maintenance in `RegisterWait` (add to index) and wait matching/cancellation (remove from index)

**Key design decisions exercised**:
- DD-122: CorrelationId is a first-class request-reply identity
- DD-123: Three routing modes — correlation-targeted proven here
- DD-124: Uniqueness constraint — `(EventName, CorrelationId)` must map to exactly one instance

**Validation**: Test passes. All previous tests still pass. Instance-targeted RaiseEvent (MC-AT-004) is unaffected.

---

## Step 8: MC-AT-018 — Fanout event resumes all matching instances of a definition type

**Goal**: Prove definition-targeted fanout: one event resumes all matching instances of a definition, without affecting other definitions.

**Test scenario**:
```
Given three instances of MyWorkflow, all waiting for EventName = "MarketClosed", CorrelationId = "2026-03-15"
  And one instance of OtherWorkflow, also waiting for EventName = "MarketClosed", CorrelationId = "2026-03-15"
When engine.ForDefinition<MyWorkflow>().RaiseEvent(envelope with EventName = "MarketClosed", CorrelationId = "2026-03-15")
Then all three MyWorkflow instances resume
And the OtherWorkflow instance remains Waiting
```

**Also test**: fanout to instances where some are waiting and some are not — non-waiting instances buffer the event as a `PendingEvent` in their mailbox (not silently dropped). Verify by subsequently registering a matching wait on a non-waiting instance and confirming the buffered event is consumed immediately.

**Drives creation of**:
- `WorkflowEngine<T>.RaiseEvent(envelope)` — definition-scoped fanout entry point
- `InMemoryInstanceStore.GetAllByDefinition(definitionId)` — instance lookup by definition
- Fanout delivery logic in `EventRouter`: iterate instances, deliver to each independently

**Key design decisions exercised**:
- DD-123: Three routing modes — fanout proven here
- DD-124: Fanout bypasses correlation index uniqueness constraint — multiple instances may wait for the same `(EventName, CorrelationId)`

**Validation**: Test passes. All previous tests still pass. Correlation-targeted routing (MC-AT-017) is unaffected.

---

## Step 9: MC-AT-002 — Conditional path selects the correct branch

**Goal**: Prove conditional branching: `If` evaluates a condition and executes only the matching path.

**Test scenario**:
```
Given a workflow: Init → If(s => s.IsValid, then: Step A → End, else: Step B → End)
When started with IsValid = true → Step A executes, Step B does not
When started with IsValid = false → Step B executes, Step A does not
(Two test cases, or parameterized)
```

**Drives creation of**:

Types in Runtime:
- Internal `IfStep<TState>` — infrastructure step with `Condition`, `ThenSteps`, `ElseSteps`
- Builder `.If(condition, then, else)` method
- Interpreter: handle `IfStep` → evaluate condition → enter the correct child step sequence → on completion, advance past the If block
- `ExecutionPointer` refinement — needs to track entry into a child structure (If) and return to the parent after the branch completes. This is where the `ParentFrame` stack is introduced or at minimum the pointer-advance logic for nested step sequences.

**Design question to resolve during this step**: `IfStep` implements `IStep<TState>`, but the interpreter handles it by type-checking (`if step is IfStep<TState>`), not by calling `ExecuteAsync`. This means `ExecuteAsync` is dead code on infrastructure steps. Options:
- Keep `IStep<TState>` uniform, infrastructure steps throw `NotSupportedException` from `ExecuteAsync` (pragmatic)
- Split into `IBusinessStep<TState>` (with `ExecuteAsync`) and `IControlFlowNode<TState>` (without) (cleaner)
- Make infrastructure steps internal interpreter nodes that don't implement `IStep` at all

Resolve based on what feels cleanest when the test passes. Document the decision.

**Key design decisions exercised**:
- Composite pattern: If contains child step lists
- Execution pointer handles nesting (at least one level)

**Validation**: Test passes. All previous tests still pass.

---

## Step 10: MC-AT-013 — While loop executes body until condition is false

**Goal**: Prove loop execution: `While` evaluates a condition, executes body, re-evaluates, stops when false.

**Test scenario**:
```
Given a workflow: Init (set counter = 0) → While(s => s.Counter < 3, body: Step(increment counter)) → End
When the workflow is started
Then the body executes exactly 3 times
And the workflow reaches Completed with Counter == 3
```

**Drives creation of**:

Types in Runtime:
- Internal `WhileStep<TState>` — infrastructure step with `Condition`, `BodySteps`
- Builder `.While(condition, bodyBuilder)` method
- Interpreter: handle `WhileStep` → evaluate condition → if true, enter body → on body completion, re-evaluate → if false, advance past While
- Execution pointer: While frame stays on parent stack, body pointer resets on each iteration

**Key design decisions exercised**:
- Composite pattern: While contains body step list
- Execution pointer re-entry for loops
- Condition re-evaluation between iterations

**Validation**: Test passes. All previous tests still pass.

---

## Step 11: MC-AT-009 — Failing step moves workflow to failed state

**Goal**: Prove failure handling: a step that returns `Failed` terminates the workflow.

**Test scenario**:
```
Given a workflow: Init → FailingStep (returns StepResult.Failed) → Step C → End
When the workflow is started
Then the workflow reaches Failed
And Step C never executes
And the error details are observable through Get()
```

**Drives creation of**:

Types in Abstractions:
- `WorkflowError` — record: `Exception`, `StepId`, `Timestamp` (or similar)

Types in Runtime:
- `RuntimeState.Error` — populated on failure
- `WorkflowInstanceSnapshot.Error` — error details exposed in snapshot
- Interpreter: handle `StepResult.Failed` → record error → set status to Failed → stop loop
- `InstanceScope.Get()` — returns snapshot including error if failed

**Also test**:
- Unhandled exception thrown by a step (not just explicit `StepResult.Failed`). The runtime should catch it and treat it as a failure. This is a second test case within MC-AT-009.
- **Lifecycle rejection**: after a workflow reaches `Failed`, calling `RaiseEvent` on it must be rejected (not silently ignored). Similarly, after `Completed`, any trigger must be rejected. These are lifecycle transition table invariants — the `InstanceLifecycle` state machine rejects illegal triggers on terminal states.

**Validation**: Test passes. All previous tests still pass.

---

## Step 12: MC-AT-006 — Parallel branches join exactly once with WhenAll

**Goal**: Prove parallel fan-out and join: multiple branches execute, `WhenAll` fires the continuation exactly once after all complete.

**Test scenario**:
```
Given a workflow:
  Init → Parallel(
    Branch "A": Step(set state.A = true),
    Branch "B": Step(set state.B = true)
  ) → WhenAll → Step(set state.Joined = true) → End
When the workflow is started
Then state.A == true, state.B == true, state.Joined == true
And the workflow reaches Completed
And the join continuation executed exactly once (verified by counter or flag)
```

**Drives creation of**:

Types in Abstractions:
- `BranchState` — record: `BranchId`, `Status`, `CurrentPointer`
- `BranchStatus` — enum: `Running`, `Waiting`, `Completed`, `Failed`
- `StepResult.Yield` — internal signal: branches spawned

Types in Runtime:
- Internal `ParallelStep<TState>` — infrastructure step with `Branches`
- Internal `WhenAllStep<TState>` — join marker (may be implicit in the graph)
- Builder `.Parallel(config)` with `.Branch(name, builder)` and `.WhenAll()`
- `RuntimeState.BranchStates` — dictionary tracking branch progress
- Interpreter: handle `ParallelStep` → create `BranchState` per branch → execute branches → on each branch completion, check if all done → if all done, advance past WhenAll
- Branch execution: each branch runs through the interpreter loop independently. For this test, sequential branch execution is sufficient (no concurrent tasks yet). Concurrent scheduling is an optimization; the correctness contract is the same.

**Key design decisions exercised**:
- Composite: Parallel contains branch step lists
- Branch state tracking
- Join fires exactly once (checked under instance lock)

**Important**: the `InstanceExecutionSerializer` is already in place from Step 3. Branch completions already go through it.

**Validation**: Test passes. All previous tests still pass.

---

## Step 13: MC-AT-007 — Different waits in parallel branches remain isolated

**Goal**: Prove branch-scoped wait isolation: each branch's wait matches only its own event.

**Test scenario**:
```
Given a workflow:
  Init → Parallel(
    Branch "A": Wait("EventA", s => s.Id),
    Branch "B": Wait("EventB", s => s.Id)
  ) → WhenAll → End
When started, both branches enter Waiting
When RaiseEvent("EventA", id) is called
Then only Branch A resumes, Branch B remains Waiting
When RaiseEvent("EventB", id) is called
Then Branch B resumes, WhenAll fires, workflow reaches Completed
```

**Drives creation of**:

- `WaitRecord.BranchId` — scoping waits to branches (already in the type, now tested)
- `EventMatcher` refinement: when matching inside a parallel block, match against the specific branch's waits only
- Instance status logic: with parallel branches, instance status is `Waiting` if any branch is waiting, `Running` if any branch is running, `Completed` only when all branches complete through WhenAll

**Validation**: Test passes. All previous tests still pass.

---

## Step 14: MC-AT-008 — Parallel branch completion order does not change final join behavior

**Goal**: Prove deterministic join regardless of ordering.

**Test scenario**:
```
Given the same parallel workflow with two branches that each set a state field
Run 1: Branch A completes before Branch B
Run 2: Branch B completes before Branch A
Both runs must produce the same final state after WhenAll
```

This test may require controlling branch execution order. Options:
- Use waits in branches and raise events in different orders
- Use a controllable step that blocks until signaled

**Recommended approach**: Use waits. Branch A waits for "EventA", Branch B waits for "EventB". In Run 1, raise EventA first then EventB. In Run 2, raise EventB first then EventA. Assert same final state.

**Drives creation of**: No new types expected. Tests the ordering invariant of the join logic.

**Validation**: Test passes. All previous tests still pass.

---

## Step 15: MC-AT-014 — Wait inside a While loop resumes correctly across iterations

**Goal**: Prove that each loop iteration creates fresh wait records and previous-iteration waits don't interfere.

**Test scenario**:
```
Given a workflow:
  Init (set items = ["a", "b", "c"], index = 0)
  → While(s => s.Index < s.Items.Count, body:
      Step(set currentItem = items[index])
      → Wait("ItemProcessed", s => s.CurrentItem)
      → Step(increment index))
  → End
When started, workflow waits with CorrelationId = "a"
When RaiseEvent("ItemProcessed", "a"), workflow resumes, increments, waits with key = "b"
When RaiseEvent("ItemProcessed", "b"), workflow resumes, increments, waits with key = "c"
When RaiseEvent("ItemProcessed", "c"), workflow resumes, increments, loop exits, reaches Completed
```

**Also verify**: raising event with key "a" during the second iteration (when waiting for "b") does NOT resume the workflow. Previous iteration's matched wait is not reusable.

**Drives creation of**: No new types expected. Tests the wait lifecycle inside loops — the critical risk that motivated including `While` in the minimal core.

**Validation**: Test passes. All previous tests still pass.

---

## Step 16: MC-AT-010 — Management query selects instances by runtime metadata

**Goal**: Prove the fluent management API: `Where(x => ...)` filters instances correctly.

**Test scenario**:
```
Given 4 workflows started:
  - Workflow A: reaches Completed
  - Workflow B: reaches Waiting
  - Workflow C: reaches Failed
  - Workflow D: reaches Running (use a controllable step that doesn't complete immediately)
When WorkflowEngine.Where(x => x.Status == WorkflowStatus.Waiting).List()
Then exactly Workflow B is returned
When WorkflowEngine.Where(x => x.Status == WorkflowStatus.Completed).Count()
Then result == 1
When WorkflowEngine<T>.All().List()
Then all 4 instances are returned
```

**Drives creation of**:

Types in Runtime:
- `WorkflowEngine` (non-generic) — engine-wide entry point with `.All()`, `.Where(...)`, `.Instance(id)`
- `SelectionScope` — returned by `All()` / `Where()`, with `.List()` and `.Count()`
- Expression translation: parse `Expression<Func<WorkflowInstanceSnapshot, bool>>` into filtering logic
- For the ephemeral in-memory provider, expression translation can evaluate the compiled predicate directly against snapshots. But the expression must still be validated against the allowed subset (only snapshot properties, allowed operators).
- `InMemoryInstanceStore` — needs `Query(expression)` or similar method

**Also test**: an invalid expression (e.g., method call) is rejected with a clear diagnostic.

**Validation**: Test passes. All previous tests still pass.

---

## Step 17: MC-AT-012 — Concurrent resume attempts resolve as one valid serialized outcome

**Goal**: Prove the serialized execution guarantee under real concurrency.

**Test scenario**:
```
Given a waiting workflow instance
When two tasks simultaneously call RaiseEvent with the same matching event
Then the workflow resumes exactly once
And the final state is equivalent to one valid sequential execution
And no double-continuation occurs
```

**Implementation approach**:
- Start a workflow that reaches Wait
- Use `ManualResetEventSlim` barriers to ensure two threads reach `RaiseEvent` simultaneously:
  1. Create a barrier (count = 2)
  2. Launch two `Task.Run` calls that each: signal the barrier, wait for both to arrive, then call `RaiseEvent`
  3. This forces actual thread interleaving at the critical point
- Assert: workflow completed exactly once, no exceptions, state is consistent
- Supplementary: also run in a loop (e.g., 50 iterations) to exercise varied timing

**Drives creation of**: No new types. Tests the `InstanceExecutionSerializer` under real concurrent pressure. If the SemaphoreSlim implementation has bugs, this test catches them.

**Validation**: Test passes reliably under repeated runs. All previous tests still pass.

---

## Step 18: MC-AT-011 — WaitLong is unavailable in the minimal core

**Goal**: Prove that durable-only features are rejected in the ephemeral-only engine.

**Test scenario**:
```
Given the ephemeral workflow builder
When a definition attempts to use WaitLong (or equivalent durable-only method)
Then the builder throws / the runtime rejects with a clear error message
```

**Implementation approach**: The ephemeral builder does not expose a `WaitLong` method — this is the primary compile-time guard. The acceptance test verifies the runtime fallback: if a `WaitLongStep` is manually constructed and added to a definition (bypassing the builder), the runtime rejects it at registration or execution time with a clear error.

**Test mechanism**:
```
Given a workflow definition containing a manually constructed WaitLongStep
When the definition is registered or the workflow is started
Then the engine throws InvalidOperationException with a message indicating
     that WaitLong requires durable mode
```

This is a standard runtime-rejection test, not a compile-fail harness. The compile-time guard (no method on builder) is validated implicitly by the fact that no other test in the suite uses `WaitLong` through the builder.

**Drives creation of**:
- Durable-feature validation logic in runtime registration or interpreter

**Layering decision**: `WaitLongStep<TState>` can be placed in Runtime (internal) rather than Abstractions to avoid polluting the minimal abstraction assembly with an out-of-scope durable-only primitive. The test can construct it via an internal test helper or `[InternalsVisibleTo]`. This keeps Abstractions clean for slice 1. If a later slice needs cross-assembly recognition, the type can be promoted then.

**Validation**: Test passes. All previous tests still pass. Full suite green.

---

## Post-completion verification

After all 18 tests pass:

1. Run full test suite: `dotnet test` — all green
2. Verify no skipped or ignored tests
3. Review all public types in Abstractions — confirm they match the design proposal
4. Review all public methods in Runtime — confirm management API matches the baseline
5. Check that no internal types leaked into public API

---

## Summary: what each step produces

| Step | Test | New types (approx) | Cumulative types |
|------|------|-------------------|------------------|
| 0 | — | 0 (scaffolding) | 0 |
| 1 | MC-AT-001 | ~12 (core skeleton + lifecycle + management scopes) | 12 |
| 2 | MC-AT-003 | ~4 (wait model) | 16 |
| 3 | MC-AT-004 | ~3 (event, matcher, serializer) | 19 |
| 4 | MC-AT-005 | 0 (negative path) | 19 |
| 5 | MC-AT-015 | ~2 (PendingEvent, mailbox logic) | 21 |
| 6 | MC-AT-016 | ~1 (ConsumedEventIds dedup) | 22 |
| 7 | MC-AT-017 | ~3 (CorrelationIndex, EventRouter, engine RaiseEvent) | 25 |
| 8 | MC-AT-018 | ~1 (fanout delivery, GetAllByDefinition) | 26 |
| 9 | MC-AT-002 | ~2 (IfStep, builder method) | 28 |
| 10 | MC-AT-013 | ~2 (WhileStep, builder method) | 30 |
| 11 | MC-AT-009 | ~2 (error model, snapshot error) | 32 |
| 12 | MC-AT-006 | ~4 (parallel, branch, join) | 36 |
| 13 | MC-AT-007 | 0 (branch-scoped matching) | 36 |
| 14 | MC-AT-008 | 0 (ordering invariant) | 36 |
| 15 | MC-AT-014 | 0 (wait-in-loop lifecycle) | 36 |
| 16 | MC-AT-010 | ~1 (selection, expr — engine scopes already exist) | 37 |
| 17 | MC-AT-012 | 0 (concurrency stress) | 37 |
| 18 | MC-AT-011 | ~2 (WaitLongStep, validation) | 39 |

Approximately 39 types total. Each discovered by what the test needs, not pre-built.

---

## Rules for the TDD process

1. **Write the test first**. It will not compile. That is expected.
2. **Create only the types the test needs to compile and pass**. No speculative types.
3. **Start with the simplest implementation**. Refactor when a later test demands it.
4. **Each step must leave all previous tests green**. No regressions.
5. **Infrastructure steps are internal to Runtime**. Only `IStep<TState>`, `StepResult`, `StepContext<TState>`, and management types are public in Abstractions. Exception: if the IStep hierarchy is split during Step 9 (see design question), infrastructure nodes may not implement `IStep` at all.
6. **The builder is the only public way to create definitions**. No public constructors for `WorkflowDefinition` or infrastructure steps.
7. **WorkflowInstance<TState> is internal**. The public API returns `WorkflowInstanceSnapshot` for metadata and `GetState<TState>()` (inspection API) for business state. Neither exposes the live instance. `GetState` returns a copy.
8. **Do not add saga or durable-mode types to Abstractions**. They are out of scope. `WaitLongStep` (Step 18) is placed in Runtime, not Abstractions.
9. **When a test forces a design question not covered by the proposal**, stop and document the decision before proceeding.
10. **Commit after each green step**. Each step is a logical commit point.
