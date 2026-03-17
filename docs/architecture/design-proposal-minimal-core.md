# Design Proposal: Minimal Core

## Scope reminder

This design covers **regular workflow + ephemeral mode only** â€” the minimal core defined in `minimal-core-scope.md`.

Primitives: `Init`, `End`, `If`, `While`, `Parallel`, `WhenAll`, `Wait`.
States: `Running`, `Waiting`, `Completed`, `Failed`.
No saga, no durability, no retry/timeout policies.

---

## 1. Applicable design patterns

### 1.1 Interpreter (core execution engine)

The runtime **interprets** a workflow definition graph against current instance state to determine the next step to execute. This is the central pattern.

Why: the workflow definition is data (a graph of steps). The runtime walks that graph, evaluating conditions, branching, and waiting. This cleanly separates definition from execution â€” the strongest research lesson.

### 1.2 Composite (workflow definition structure)

Workflow definitions form a **tree of steps**. `Parallel` contains branches, branches contain step sequences. `If` contains conditional paths. This is a natural composite.

Why: allows infrastructure steps (Parallel, If) and business steps to share a common `IStep` contract. The runtime interpreter walks the composite uniformly.

### 1.3 Command / Result Object (step outcomes)

Steps return **intent objects** rather than mutating engine state. `StepResult` is a discriminated union: `Completed`, `Failed`, `WaitForEvent`, `Yield`.

Why: this is the "separate orchestration from side effects" principle. Steps express what happened; the runtime decides what to do about it. This is the single most important contract in the engine.

### 1.4 State Machine (instance lifecycle)

Each workflow instance has a state machine for its lifecycle with **explicit legal transitions and triggers**, inspired by Stateless-style discipline. Transitions are enforced; illegal triggers are rejected.

Why: prevents illegal state transitions (e.g., resuming a completed workflow). Maps directly to the `WorkflowStatus` enum and the serialized execution guarantee. Making transitions and triggers explicit produces cleaner acceptance tests and catches lifecycle bugs at the contract level, not deep inside the interpreter.

### 1.5 Builder (workflow definition authoring)

Fluent builder API for composing workflow definitions from steps. `WorkflowBuilder<TState>` produces an immutable `WorkflowDefinition<TState>`.

Why: the pseudo-DSL research shows the intended authoring experience is fluent and code-first. Builder pattern makes definitions immutable after construction, preventing runtime mutation.

### 1.6 Mediator (runtime as central coordinator)

The `WorkflowRuntime` mediates between workflow definitions, instance state, event delivery, and the management API. No component talks directly to another.

Why: prevents coupling between steps and instance storage, between events and execution, between management queries and internal state. This is the provider-neutral architecture.

### 1.7 Repository (instance store)

`IInstanceStore` abstracts instance storage. For minimal core: an in-memory `ConcurrentDictionary`-backed implementation. The abstraction exists from day one to avoid coupling runtime logic to storage details.

Why: even in ephemeral mode, the runtime needs to find instances by ID, query by status, and enumerate. Making this an interface now avoids a painful refactor when durable mode arrives.

---

## 2. Component architecture

```
â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
â”‚                  WorkflowEngine<T>                       â”‚
â”‚              (fluent management API)                     â”‚
â”œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¤
â”‚                  WorkflowRuntime                         â”‚
â”‚          (interpreter / mediator / coordinator)          â”‚
â”œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¬â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¬â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¬â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¤
â”‚ Definitionâ”‚ Instance â”‚  Event Router â”‚  Execution        â”‚
â”‚ Registry  â”‚ Store    â”‚  + Matcher    â”‚  Serializer       â”‚
â”‚           â”‚          â”‚               â”‚  (per-instance    â”‚
â”‚           â”‚          â”‚               â”‚   lock)           â”‚
â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”´â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”´â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”´â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
```

### Layer 1: Abstractions

Pure interfaces and data types. No behavior. No dependencies.

- `IWorkflowDefinition<TState>` â€” immutable workflow graph
- `IStep<TState>` â€” individual step contract
- `StepResult` â€” step outcome discriminated union
- `StepContext<TState>` â€” execution context passed to steps
- `WorkflowStatus` â€” lifecycle state enum
- `WorkflowInstance<TState>` â€” runtime + business state container
- `RuntimeState` â€” engine-owned metadata
- `WaitDescriptor` â€” what the instance is waiting for
- `EventEnvelope` â€” external event data
- `IInstanceStore` â€” storage abstraction (save must be atomic: runtime state + business state + pending events + consumed event IDs)
- `WorkflowInstanceSnapshot` â€” immutable public view of instance state

### Layer 2: Runtime

Engine behavior. Depends only on abstractions.

- `WorkflowRuntime` â€” the interpreter/executor
- `InstanceExecutionSerializer` â€” per-instance lock (mailbox/semaphore)
- `EventRouter` â€” resolves target instance(s) from routing mode (instance-targeted â†’ direct, correlation-targeted â†’ index lookup, fanout â†’ definition iteration)
- `EventMatcher` â€” matches an `EventEnvelope` against `WaitRecord` entries within a single instance
- `DefinitionRegistry` â€” stores registered workflow definitions

### Layer 3: Management

Public API surface. Depends on runtime.

- `WorkflowEngine` â€” engine-wide management entry point
- `WorkflowEngine<T>` â€” definition-scoped management
- `InstanceScope` â€” instance-scoped operations
- `SelectionScope` â€” filtered selection + terminal operations

### Layer 4: In-Memory Providers

Ephemeral implementations. Depends on abstractions.

- `InMemoryInstanceStore` â€” `ConcurrentDictionary`-backed instance storage

---

## 3. Core type design

### 3.1 Workflow definition

```
IWorkflowDefinition<TState>
  â”œâ”€â”€ DefinitionId: string
  â””â”€â”€ Steps: IReadOnlyList<IStep<TState>>     // the root step sequence
```

Input-to-state construction belongs to `InitStep`, not to the definition root. The `Init` step receives the start input and produces the initial business state. This avoids a second `TInput` type parameter on the definition and keeps one clear initialization path.

Steps form a tree:

```
IStep<TState>
  â”œâ”€â”€ StepId: string
  â”œâ”€â”€ ExecuteAsync(StepContext<TState>): Task<StepResult>

Infrastructure steps (implement IStep<TState>):
  â”œâ”€â”€ InitStep<TState>         â€” sets initial state
  â”œâ”€â”€ EndStep<TState>          â€” marks terminal
  â”œâ”€â”€ IfStep<TState>           â€” condition + true-path + false-path
  â”‚     â”œâ”€â”€ Condition: Func<TState, bool>
  â”‚     â”œâ”€â”€ ThenSteps: IStep<TState>[]
  â”‚     â””â”€â”€ ElseSteps: IStep<TState>[]
  â”œâ”€â”€ WhileStep<TState>        â€” loop until condition is false
  â”‚     â”œâ”€â”€ Condition: Func<TState, bool>
  â”‚     â””â”€â”€ BodySteps: IStep<TState>[]
  â”œâ”€â”€ ParallelStep<TState>     â€” fan-out
  â”‚     â””â”€â”€ Branches: IStep<TState>[][]
  â”œâ”€â”€ WhenAllStep<TState>      â€” join (paired with Parallel)
  â””â”€â”€ WaitStep<TState>         â€” suspend for external event
        â”œâ”€â”€ EventName: string
        â””â”€â”€ CorrelationSelector: Func<TState, string>
```

Design note: `Parallel` and `WhenAll` are logically paired. The builder enforces this pairing. At the graph level they could be a single `ParallelBlock` node with branches and an implicit join, but exposing them as separate steps preserves alignment with the pseudo-DSL and makes the execution pointer model simpler.

### 3.2 Step result

```
StepResult (abstract record with sealed record subclasses)
  â”œâ”€â”€ StepResult.Completed          â€” step done, advance to next
  â”œâ”€â”€ StepResult.Failed             â€” step failed
  â”‚     â””â”€â”€ Error: Exception
  â”œâ”€â”€ StepResult.WaitForEvent       â€” suspend instance
  â”‚     â”œâ”€â”€ EventName: string
  â”‚     â””â”€â”€ CorrelationId: string
  â””â”€â”€ StepResult.Yield              â€” internal: branch spawned, control returned to runtime
```

Implementation: `abstract record StepResult` with `sealed record Completed : StepResult`, `sealed record Failed(Exception Error) : StepResult`, etc. Records give value equality, immutability, and C# pattern matching (`switch` / `is`) for exhaustive handling by the interpreter. Every step outcome is explicitly accounted for. No hidden paths.

**Mutation model**: `StepResult` carries only **control intent** â€” it tells the runtime what to do next. It does not carry business state mutations. Business state is mutated directly by the step through `StepContext.State`. This is one clear rule: steps own business state writes, the runtime owns orchestration decisions.

### 3.3 Execution context

```
StepContext<TState>
  â”œâ”€â”€ State: TState                    // business state (mutable by step)
  â”œâ”€â”€ InstanceId: string               // stable identity
  â”œâ”€â”€ ResumedEvent: EventEnvelope?     // populated when step executes after a wait resume; null otherwise
  â”œâ”€â”€ CancellationToken                // cooperative cancellation
  â””â”€â”€ (future: logger, telemetry)
```

Steps receive context, mutate `State` directly, and return a `StepResult` that expresses only control flow intent. Steps never see `WorkflowInstance`, `RuntimeState`, or engine internals. The runtime applies no business state transformation of its own â€” it only reads `StepResult` to decide the next orchestration action.

**Event payload access**: when a workflow resumes from a `Wait`, the interpreter sets `StepContext.ResumedEvent` to the matched `EventEnvelope` before executing the next step. The step can read `ctx.ResumedEvent?.Payload` to access event data. During normal (non-resume) execution, `ResumedEvent` is null. This is the only channel for event data to reach business step code.

### 3.4 Workflow instance

```
WorkflowInstance<TState>
  â”œâ”€â”€ InstanceId: string
  â”œâ”€â”€ DefinitionId: string
  â”œâ”€â”€ RuntimeState: RuntimeState
  â””â”€â”€ BusinessState: TState

RuntimeState
  â”œâ”€â”€ Status: WorkflowStatus
  â”œâ”€â”€ ExecutionPointer: ExecutionPointer   // where in the graph we are
  â”œâ”€â”€ ActiveWaits: IReadOnlyList<WaitRecord>
  â”œâ”€â”€ PendingEvents: List<PendingEvent>    // buffered events not yet matched
  â”œâ”€â”€ ConsumedEventIds: HashSet<string>    // for deduplication
  â”œâ”€â”€ BranchStates: Dictionary<string, BranchState>  // for parallel
  â”œâ”€â”€ CreatedAt: DateTimeOffset
  â”œâ”€â”€ LastTransitionAt: DateTimeOffset
  â””â”€â”€ Error: WorkflowError?               // if Failed
```

Why separate RuntimeState: queryable without deserializing TState. This is design-synthesis best practice 3.4.

### 3.5 Lifecycle transition table

Inspired by Stateless-style discipline: every legal transition is explicit, every trigger is named, and illegal triggers are rejected with clear diagnostics.

#### Instance lifecycle

```
State        â”‚ Trigger                   â”‚ Next State  â”‚ Notes
â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¼â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¼â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¼â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
Running      â”‚ StepCompleted (last step) â”‚ Completed   â”‚ terminal
Running      â”‚ StepCompleted (more work) â”‚ Running     â”‚ advance pointer
Running      â”‚ StepFailed                â”‚ Failed      â”‚ terminal
Running      â”‚ WaitRegistered            â”‚ Waiting     â”‚ suspend execution
Waiting      â”‚ MatchingEventReceived     â”‚ Running     â”‚ resume from wait point
Waiting      â”‚ StepFailed                â”‚ Failed      â”‚ wait-time failure (future)
Completed    â”‚ (any trigger)             â”‚ â€” rejected  â”‚ terminal is final
Failed       â”‚ (any trigger)             â”‚ â€” rejected  â”‚ terminal is final (no retry in minimal core)
```

#### Branch lifecycle (inside Parallel)

```
State        â”‚ Trigger                   â”‚ Next State  â”‚ Notes
â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¼â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¼â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¼â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
Running      â”‚ BranchStepCompleted (last)â”‚ Completed   â”‚ check WhenAll join
Running      â”‚ BranchStepCompleted (more)â”‚ Running     â”‚ advance branch pointer
Running      â”‚ WaitRegistered            â”‚ Waiting     â”‚ branch suspends
Running      â”‚ BranchStepFailed          â”‚ Failed      â”‚ branch failure
Waiting      â”‚ MatchingEventReceived     â”‚ Running     â”‚ branch resumes
Completed    â”‚ (any trigger)             â”‚ â€” rejected  â”‚ branch is done
Failed       â”‚ (any trigger)             â”‚ â€” rejected  â”‚ branch is done
```

#### Trigger vocabulary

These are the named triggers the runtime recognizes. They are internal to the runtime, not a public API â€” but they define the language for lifecycle transitions.

- `StepCompleted` â€” a step returned `StepResult.Completed`
- `StepFailed` â€” a step returned `StepResult.Failed` or threw an unhandled exception
- `WaitRegistered` â€” a step returned `StepResult.WaitForEvent` and the wait record is active
- `MatchingEventReceived` â€” an incoming event matched an active `WaitRecord`
- `AllBranchesCompleted` â€” the last branch in a `Parallel` block reached `Completed`, triggering WhenAll
- `BranchStepCompleted` â€” a step within a branch returned `StepResult.Completed`
- `BranchStepFailed` â€” a step within a branch returned `StepResult.Failed`

#### Rejected triggers

When the lifecycle state machine receives an illegal trigger (e.g., `MatchingEventReceived` on a `Completed` instance), it must:
1. Not mutate state
2. Return a clear rejection result (not silently swallow)
3. Log the rejection if observability is available

This is enforced by the state machine, not by caller discipline. The interpreter and `RaiseEvent` path both go through the lifecycle transition check.

### 3.6 Execution pointer

The runtime needs to know "where are we in the definition graph."

```
ExecutionPointer
  â”œâ”€â”€ CurrentStepId: string
  â”œâ”€â”€ ParentPointers: Stack<ParentFrame>   // for nested structures
  â””â”€â”€ Phase: ExecutionPhase                // e.g., Entering, Executing, Leaving

ParentFrame
  â”œâ”€â”€ StepId: string         // the Parallel or If we're inside
  â”œâ”€â”€ BranchIndex: int?      // which branch
  â””â”€â”€ Phase: ExecutionPhase
```

This is essentially a call stack for the workflow interpreter. It tracks position in the composite tree.

### 3.6 Wait and event model

```
WaitRecord
  â”œâ”€â”€ WaitId: string              // unique within instance
  â”œâ”€â”€ EventName: string
  â”œâ”€â”€ CorrelationId: string
  â”œâ”€â”€ BranchId: string?           // which branch owns this wait
  â”œâ”€â”€ RegisteredAt: DateTimeOffset
  â””â”€â”€ Status: WaitStatus          // Active, Matched, Cancelled

EventEnvelope
  â”œâ”€â”€ EventName: string
  â”œâ”€â”€ CorrelationId: string
  â”œâ”€â”€ Payload: object?
  â””â”€â”€ EventId: string             // for dedup

PendingEvent
  â”œâ”€â”€ Envelope: EventEnvelope
  â”œâ”€â”€ ReceivedAt: DateTimeOffset
  â””â”€â”€ Consumed: bool
```

Matching rule: `EventName` + `CorrelationId` match against `WaitRecord`.

#### Event routing

Events reach instances through three routing modes (see section 5.2a for API entry points):

1. **Instance-targeted**: caller specifies `InstanceId`, event delivered directly.
2. **Correlation-targeted**: engine looks up `(EventName, CorrelationId)` in the correlation index to find the target instance. The pair must uniquely identify one active wait across the engine.
3. **Definition-targeted (fanout)**: event delivered to all instances of a specific definition type. Each instance matches independently.

**Correlation index**: the engine maintains an index of `(EventName, CorrelationId) â†’ HashSet<InstanceId>` for all active `WaitRecord` entries. Updated on wait registration, matching, and cancellation. Registration always succeeds â€” multiple instances may register the same key. Uniqueness is enforced at **correlation-targeted routing time only**: when `engine.RaiseEvent(envelope)` is called, the index must resolve to exactly one instance (count == 1). If zero â†’ throw "no active wait". If multiple â†’ throw "ambiguous correlation â€” use instance-targeted or fanout routing". Fanout and instance-targeted routing bypass the index entirely.

In ephemeral mode, this is an in-memory dictionary. In durable mode (future), it becomes a queryable index in the persistence provider.

Once routing determines the target instance(s), delivery follows the same per-instance mailbox rules below.

#### Per-instance event buffer (mailbox)

Events and waits are **independent**: events may arrive before, during, or after a wait is registered. The runtime must handle all orderings correctly.

Each workflow instance has a **per-instance event buffer** (mailbox). The matching process works bidirectionally:

**When an event arrives** (via `RaiseEvent`):
1. Check active `WaitRecord` entries for a match
2. If match found â†’ consume immediately (mark wait as Matched, resume)
3. If no match â†’ **buffer the event** as a `PendingEvent` in the instance mailbox

**When a wait is registered** (via `StepResult.WaitForEvent`):
1. Check the instance mailbox for a pending event that matches the new wait
2. If match found â†’ consume the pending event immediately (mark wait as Matched, resume without suspending)
3. If no match â†’ register the wait as Active, suspend execution

This ensures that event arrival order does not matter. A workflow expecting A-then-B works correctly whether events arrive in order (A, B), reverse order (B, A), or simultaneously.

**Mailbox rules**:
- Events are instance-scoped: each instance has its own mailbox
- Events in the buffer are matched by `EventName` + `CorrelationId`, same as active waits
- Duplicate events (same `EventId`) are deduplicated: if an event with the same `EventId` was already consumed or is already buffered, the duplicate is ignored
- Pending events are cleaned up when the instance reaches a terminal state (`Completed`, `Failed`)
- The mailbox operates under the per-instance serializer â€” no race between arrival and registration

#### Event safety guarantee: no event loss

**Core rule**: an event must never be lost. An event is considered safely consumed only when **both** conditions are met:

1. The event has been matched to a wait
2. The resulting state transition has been committed (state saved)

Until both conditions hold, the event must remain available â€” in the mailbox, in the provider, or both.

**Transactional consumption**: event consumption and state transition are atomic. The runtime must not remove an event from the mailbox before the new instance state (with the wait marked as `Matched` and execution advanced) is committed. If the state commit fails, the event remains unconsumed and available for retry.

In practice, the commit sequence is:

```
1. Match event to wait (in memory)
2. Compute new state (advance execution, mark wait Matched)
3. Commit new state atomically (save to store)
4. Only after successful commit: mark event as consumed
```

If the process crashes between steps 2 and 3, the event is still in the mailbox/provider. On rehydration, the instance loads the last committed state (which still has the wait Active), and the event can be re-delivered.

**Ephemeral mode**: process restart loses all state â€” this is the documented ephemeral limitation. But even in ephemeral mode, the in-memory implementation follows the same transactional pattern: event removal happens after state commit, not before. This ensures correctness during normal operation (e.g., a step failure after event match does not lose the event).

**Durable mode contract** (future, designed for now):
- The provider must persist pending events as part of instance state
- On rehydration, all unconsumed pending events are loaded from the store
- The provider's save operation must atomically commit: runtime state + business state + consumed event list + remaining pending events
- Events from external sources (event provider) must not be acknowledged/deleted until the instance state reflecting their consumption is durably saved
- This is essentially the **transactional outbox/inbox pattern**: the event provider and state provider participate in one logical transaction

**Why design this now**: if the mailbox contract allows event loss in the minimal core, durable mode cannot fix it later without breaking the contract. The transactional consumption rule must be part of the core contract from day one, even if ephemeral mode only enforces it in-memory.

### 3.7 Branch state for parallel execution

```
BranchState
  â”œâ”€â”€ BranchId: string
  â”œâ”€â”€ Status: BranchStatus    // Running, Waiting, Completed, Failed
  â”œâ”€â”€ CurrentPointer: ExecutionPointer
  â””â”€â”€ Result: object?         // branch output
```

Join rule for `WhenAll`: the join continuation fires exactly once, after the last branch transitions to `Completed`. The runtime checks `AllBranches.All(b => b.Status == Completed)` under the instance lock.

---

## 4. Runtime execution model

### 4.1 Interpreter loop

The core execution is a loop (conceptual):

```
while instance is Running:
    step = resolve current step from definition + execution pointer
    result = await step.ExecuteAsync(context)

    match result:
        Completed â†’
            advance pointer to next step
            if no next step â†’ mark Completed

        Failed â†’
            record error
            mark instance Failed
            stop

        WaitForEvent â†’
            register WaitRecord
            check mailbox for pending match
            if pending match found â†’ consume it, continue loop (do not suspend)
            if no match â†’ mark instance Waiting, stop (yield control)

        Yield â†’
            (internal: parallel branches spawned)
            stop (runtime manages branches)
```

### 4.2 While loop execution

When the interpreter hits a `WhileStep`:

1. Evaluate condition against current business state
2. If false â†’ skip body, advance past the While block
3. If true â†’ push a `ParentFrame` for the While, enter the body
4. After body completes â†’ re-evaluate condition
5. If still true â†’ reset body pointer, execute body again
6. If false â†’ pop the While frame, advance

The execution pointer stack handles this naturally: the While frame stays on the stack until the condition fails.

**Wait inside While**: when a `Wait` is encountered inside the loop body, the instance suspends as normal. On resume, the interpreter continues from the wait point inside the current iteration. When that iteration completes, the While condition is re-evaluated. Each iteration gets a **fresh WaitRecord** â€” previous iteration's matched waits are not reused. This is the key invariant that MC-AT-014 tests.

### 4.3 Parallel execution

When the interpreter hits a `ParallelStep`:

1. Create `BranchState` for each branch
2. For each branch, begin interpretation independently
3. Each branch runs through the interpreter loop until it completes or waits
4. All branch state mutations go through the **instance-level serializer**
5. When a branch completes, check if all branches are done
6. If all done â†’ fire `WhenAll` continuation (advance past the parallel block)

Branch execution concurrency: branches may be scheduled on different tasks/threads, but state commits are serialized through the per-instance lock (mailbox pattern). This is the Orleans-inspired guarantee.

### 4.3 Per-instance execution serializer

```
InstanceExecutionSerializer
  â”œâ”€â”€ SemaphoreSlim(1, 1) per InstanceId
  â””â”€â”€ EnqueueAsync(instanceId, work): Task
```

All mutations to a workflow instance â€” step execution, event resume, branch completion â€” go through this serializer. This is the **single logical mutator** guarantee.

Implementation: `ConcurrentDictionary<string, SemaphoreSlim>` or `ConcurrentDictionary<string, Channel<Func<Task>>>` (mailbox).

Recommendation: **SemaphoreSlim(1,1)** for the minimal core. It's simpler and sufficient for single-node ephemeral mode. Mailbox/channel pattern can replace it later if queuing semantics are needed.

### 4.5 Event resume flow

```
DeliverToInstance(instanceId, eventEnvelope):   // shared delivery logic
    acquire instance lock
    reject if instance is in terminal state (Completed/Failed)
    deduplicate: if EventId already seen â†’ return (idempotent no-op)
    find matching WaitRecord in instance.RuntimeState.ActiveWaits
    if match:
        mark WaitRecord as Matched
        correlationIndex.Remove(waitRecord.EventName, waitRecord.CorrelationId, instanceId)
        set ResumedEvent = eventEnvelope on next step's StepContext
        if instance was Waiting and no other active waits remain:
            transition to Running
        resume interpreter loop from the wait point
    if no match:
        buffer event in instance mailbox as PendingEvent
    release instance lock
```

```
InstanceScope.RaiseEvent(envelope):                // instance-targeted
    DeliverToInstance(this.instanceId, envelope)

WorkflowEngine.RaiseEvent(envelope):               // correlation-targeted
    instanceIds = correlationIndex.Lookup(envelope.EventName, envelope.CorrelationId)
    if count == 0 â†’ throw "no active wait for this CorrelationId"
    if count > 1 â†’ throw "ambiguous correlation â€” use instance-targeted or fanout"
    DeliverToInstance(instanceIds.Single(), envelope)

WorkflowEngine<T>.RaiseEvent(envelope):            // definition-targeted (fanout)
    instances = instanceStore.GetAllByDefinition(definitionId)
    for each instance in instances:
        DeliverToInstance(instance.Id, envelope)
```

```
RegisterWait(instance, waitRecord):  // called by interpreter on WaitForEvent result
    (already under instance lock)
    add waitRecord to ActiveWaits
    correlationIndex.Add(waitRecord.EventName, waitRecord.CorrelationId, instanceId)
        // always succeeds â€” index is a multi-map (key â†’ HashSet<InstanceId>)
    check instance mailbox for pending event matching this wait
    if match found:
        mark WaitRecord as Matched
        remove/consume PendingEvent
        correlationIndex.Remove(waitRecord.EventName, waitRecord.CorrelationId, instanceId)
        do NOT suspend â€” continue interpreter loop immediately
    if no match:
        mark instance Waiting (if not already waiting on other branches)
        suspend execution
```

This bidirectional matching ensures:
- MC-AT-004 (exactly-once resume) â€” whether event arrives before or after wait
- MC-AT-012 (concurrent resume serialization) â€” all operations under instance lock
- MC-AT-015 (out-of-order events) â€” buffered events match when wait is registered
- MC-AT-017 (correlation-targeted) â€” correlation index routes without InstanceId
- MC-AT-018 (fanout) â€” definition-scoped delivery to all instances

---

## 5. Management API design

### 5.1 Entry points

```
WorkflowEngine                              // engine-wide scope
  â”œâ”€â”€ .Instance(id) â†’ InstanceScope
  â”œâ”€â”€ .ForDefinition<TState>(definitionId) â†’ WorkflowEngine<TState>
  â”œâ”€â”€ .RaiseEvent(envelope) â†’ Task          // correlation-targeted routing
  â”œâ”€â”€ .All() â†’ SelectionScope
  â””â”€â”€ .Where(x => x.Status == Running) â†’ SelectionScope

WorkflowEngine<TState>                      // definition-scoped (obtained via ForDefinition<TState>)
  â”œâ”€â”€ .Start(input) â†’ WorkflowInstanceSnapshot
  â”œâ”€â”€ .RaiseEvent(envelope) â†’ Task          // fanout: deliver to all instances of this definition
  â”œâ”€â”€ .All() â†’ SelectionScope
  â””â”€â”€ .Where(x => x.Status == Running) â†’ SelectionScope
```

**Access path**: `WorkflowEngine<TState>` is obtained from `engine.ForDefinition<TState>(definitionId)`. The engine-wide instance acts as a factory. `WorkflowEngine<TState>` can also be registered in DI for convenience, but the factory method is the primary access path.

`Start` returns `WorkflowInstanceSnapshot` â€” an immutable record containing `InstanceId`, `Status`, `DefinitionId`, and `CreatedAt`. It does not expose the live mutable `WorkflowInstance<TState>`. The caller gets an identifier and a status confirmation, not engine internals.

`InstanceScope.GetState<TState>()` is an **inspection API** that returns a snapshot copy of the business state. It is the only way to read business state from outside the engine. The caller must know the state type; a type mismatch throws `InvalidOperationException`. The returned value is a copy â€” it cannot be used to mutate engine state. This keeps `WorkflowInstanceSnapshot` metadata-only while making business state observable for tests and result inspection. This is not a casual management primitive â€” operational tooling should prefer `WorkflowInstanceSnapshot` metadata for most queries.

### 5.2a Event routing modes

The management API supports three event routing modes, determined by the API entry point. All three use the method name `.RaiseEvent(envelope)` â€” the routing mode is determined by which scope object the method is called on, not by the method name. This is deliberate: the method name describes the caller's intent (raise an event), the scope object describes the targeting strategy.

| Mode | Entry point | Routing logic |
|------|-------------|---------------|
| **Instance-targeted** | `engine.Instance(id).RaiseEvent(envelope)` | Direct delivery to the specified instance. No index lookup. |
| **Correlation-targeted** | `engine.RaiseEvent(envelope)` | Engine looks up the instance by `(EventName, CorrelationId)` from the correlation index. Exactly one instance must have a matching active wait. Error if no match or ambiguous. |
| **Definition-targeted (fanout)** | `engine.ForDefinition<T>().RaiseEvent(envelope)` | Deliver the event to every instance of that definition type. Each instance matches independently using the standard `EventName + CorrelationId` rule. Instances without a matching active wait buffer the event per mailbox semantics. |

**Correlation index**: the engine maintains an in-memory index of `(EventName, CorrelationId) â†’ HashSet<InstanceId>` for all active `WaitRecord` entries. The index is updated when waits are registered, matched, or cancelled. Registration always succeeds â€” the index is a lookup structure, not a constraint enforcer.

**Uniqueness at routing time**: for correlation-targeted routing, the index must resolve to exactly one instance. If zero matches â†’ throw with "no active wait found". If multiple matches â†’ throw with "ambiguous correlation â€” use instance-targeted or fanout routing". This is enforced at the moment `engine.RaiseEvent(envelope)` is called, not at wait registration time. This means multiple instances can register the same key (needed for fanout), but correlation-targeted routing requires unambiguous resolution.

**Fanout bypasses the index**: `WorkflowEngine<T>.RaiseEvent(envelope)` iterates all instances of the definition type directly. It does not consult the correlation index. Multiple instances of the same definition may wait for the same `EventName + CorrelationId` â€” this is the expected broadcast pattern.

**No-match behavior**:
- Correlation-targeted: throws or returns an error indicating "no active wait found for this CorrelationId". The caller needs to know the event was not consumed.
- Fanout: silently buffers in instances without a matching wait. This is normal broadcast behavior â€” not all instances may be ready for the event yet.
- Instance-targeted: buffers in the instance mailbox if no matching wait (existing behavior).

### 5.2 Terminal operations (minimal core)

```
InstanceScope
  â”œâ”€â”€ .RaiseEvent(envelope) â†’ Task
  â”œâ”€â”€ .GetActiveWaits() â†’ IReadOnlyList<WaitRecord>
  â”œâ”€â”€ .Get() â†’ WorkflowInstanceSnapshot
  â””â”€â”€ .GetState<TState>() â†’ TState

SelectionScope
  â”œâ”€â”€ .List() â†’ IReadOnlyList<WorkflowInstanceSnapshot>
  â””â”€â”€ .Count() â†’ int
```

### 5.3 Query model

`Where(...)` accepts expression-based predicates over a constrained query model, following the canonical LINQ-like shape from the management baseline:

```
.Where(x => x.Status == Running)
.Where(x => x.DefinitionId == "PriceUpdate")
.Where(x => x.CreatedAt < cutoff)
```

The public API uses `Expression<Func<WorkflowInstanceSnapshot, bool>>`. Internally, the runtime translates these expressions into a structured query representation for execution. The structured query model is an **implementation detail** â€” it is not part of the public API surface. This keeps the public contract LINQ-like and composable while ensuring provider-translatable semantics internally.

#### Allowed expression subset

The following operations are translatable and form the supported query surface:

**Allowed left-hand operands** (properties of `WorkflowInstanceSnapshot`):
- `x.Status` â€” `WorkflowStatus` enum
- `x.DefinitionId` â€” string
- `x.InstanceId` â€” string
- `x.CreatedAt` â€” `DateTimeOffset`
- `x.LastTransitionAt` â€” `DateTimeOffset`

**Allowed comparison operators**:
- `==`, `!=` for enum and string properties
- `<`, `<=`, `>`, `>=` for `DateTimeOffset` properties

**Allowed composition**:
- `&&` (logical AND) to combine multiple conditions
- `||` (logical OR) to combine multiple conditions

**Not allowed** (rejected at expression translation time with clear diagnostics):
- Method calls (`x.DefinitionId.Contains(...)`, `x.ToString()`)
- Captured closures that reference external services or mutable state
- Arithmetic or string manipulation on properties
- Negation of complex sub-expressions beyond simple `!=`
- Any property not on `WorkflowInstanceSnapshot`

**Right-hand values** must be constants or captured local variables that resolve to constants at evaluation time (e.g., `var cutoff = DateTimeOffset.UtcNow; .Where(x => x.CreatedAt < cutoff)` is valid because `cutoff` resolves to a constant).

This subset is deliberately narrow. It is sufficient for MC-AT-010 and for the management baseline examples. It is fully translatable to SQL `WHERE` clauses when durable providers arrive. New properties or operators can be added as the query model evolves â€” additions are non-breaking.

---

## 6. Definition builder (authoring API)

```
WorkflowBuilder<TState>
  .Init(ctx => { ... })
  .Then<MyBusinessStep>()
  .If(state => state.IsValid,
      then: b => b.Then<ProcessOrder>(),
      @else: b => b.End(EndReason.ValidationFailed))
  .While(state => state.PendingItems.Count > 0, body => body
      .Then<ProcessNextItem>()
      .Wait("ItemConfirmed", s => s.CurrentItemId))
  .Parallel(p => p
      .Branch("Catalog", b => b
          .Then<SendCatalogUpdate>()
          .Wait("CatalogConfirmed", s => s.RequestId))
      .Branch("Search", b => b
          .Then<SendSearchUpdate>()
          .Wait("SearchConfirmed", s => s.RequestId)))
  .WhenAll()
  .End()
  .Build()  â†’  IWorkflowDefinition<TState>
```

The builder produces an immutable definition graph. `Build()` validates structural integrity (e.g., every `Parallel` has a matching `WhenAll`, no orphan branches).

---

## 7. Project structure

```
src/
  OrcaCore.Abstractions/        # Layer 1: interfaces, types, contracts
    IWorkflowDefinition.cs
    IStep.cs
    StepResult.cs
    StepContext.cs
    WorkflowStatus.cs
    WorkflowInstance.cs
    RuntimeState.cs
    ExecutionPointer.cs
    WaitRecord.cs
    EventEnvelope.cs
    BranchState.cs
    IInstanceStore.cs
    WorkflowInstanceSnapshot.cs

  OrcaCore.Runtime/             # Layer 2 + 3: engine, management
    WorkflowRuntime.cs               # interpreter loop
    InstanceExecutionSerializer.cs   # per-instance lock
    EventMatcher.cs                  # event-to-wait matching
    EventRouter.cs                   # routing: instance/correlation/fanout
    CorrelationIndex.cs              # (EventName, CorrelationId) â†’ InstanceId
    DefinitionRegistry.cs            # registered definitions
    WorkflowEngine.cs                # management API (engine-wide)
    WorkflowEngineT.cs               # management API (definition-scoped)
    InstanceScope.cs                 # management API (instance-scoped)
    SelectionScope.cs                # management API (filtered selection)
    InMemoryInstanceStore.cs         # ephemeral provider

tests/
  OrcaCore.Tests/
    AcceptanceTests/
      MC_AT_001_SimpleWorkflowTest.cs
      MC_AT_002_ConditionalPathTest.cs
      ... (18 acceptance tests)
    Unit/
      StepResultTests.cs
      EventMatcherTests.cs
      ExecutionSerializerTests.cs
      ...
```

Two projects initially: `Abstractions` and `Runtime`. When durable mode arrives, `Runtime` stays lean and providers become separate projects.

---

## 8. Dependency flow

```
OrcaCore.Abstractions  â†  depends on nothing
         â†‘
OrcaCore.Runtime       â†  depends on Abstractions only
         â†‘
OrcaCore.Tests         â†  depends on Runtime (and transitively Abstractions)
```

User workflow code depends on `Abstractions` for step contracts and on `Runtime` for the engine.

---

## 9. Key invariants the design must protect

| Invariant | Enforced by |
|-----------|-------------|
| One logical mutator per instance | `InstanceExecutionSerializer` |
| Steps own business state, runtime owns orchestration | `StepContext.State` for business mutation; `StepResult` for control intent only |
| Runtime state queryable without business state | `RuntimeState` is separate type |
| Wait matching is isolated per branch | `WaitRecord.BranchId` scoping |
| Join fires exactly once | Branch completion check under instance lock |
| Failed instance stops | Interpreter loop exits on `Failed` |
| Each loop iteration gets fresh WaitRecords | Interpreter creates new WaitRecord per iteration, matched waits are not reused |
| Lifecycle transitions are explicit | State machine rejects illegal triggers; terminal states reject all triggers |
| Event arrival order does not matter | Per-instance mailbox buffers early events; bidirectional matching on arrival and on wait registration |
| Events are never lost | Event removed from mailbox only after state transition is committed; transactional consumption |
| Events are deduplicated by EventId | `ConsumedEventIds` set prevents double consumption |
| Correlation-targeted routing finds exactly one instance | `CorrelationIndex` is a multi-map; uniqueness checked at routing time (count must be 1) |
| Fanout delivers to all instances of a definition | `EventRouter` iterates definition instances; each matches independently |
| `WaitLong` rejected in ephemeral | Not present in API / builder rejects it |

### Orchestration ownership rule (Temporal-inspired)

Runtime control flow is engine-owned. Business steps may mutate business state through `StepContext.State`, but they must not decide orchestration outside `StepResult`. Concretely:

- Steps must not call back into the runtime to start/resume/cancel other instances
- Steps must not spawn threads or tasks that outlive the step execution
- Steps must not bypass the `StepResult` contract by throwing specific exception types to signal orchestration intent
- Steps must not read or depend on `RuntimeState`, execution pointer position, or branch identity

This is not enforced by the type system in the minimal core (unlike Temporal's strict determinism rules). It is enforced by convention: `StepContext` does not expose any runtime or orchestration surface. If a step needs to signal wait, failure, or completion, the only channel is `StepResult`.

This discipline keeps the door open for replay-based durable execution in the future, where the runtime must be able to re-execute orchestration decisions deterministically.

---

## 10. What this design does NOT decide yet

These are explicitly deferred to avoid premature commitment:

- Durable persistence contracts
- Replay vs checkpoint recovery model
- Event provider abstraction details
- Saga compensation semantics
- `WhenFirst` losing-branch policy
- Retry/timeout policy model
- Version binding and deployment rules
- Lifecycle event publication contracts
- Multi-node ownership / leasing
- Serialized definition format (JSON/YAML workflow import)

### Replay-safety note

The minimal core does not use replay-based execution. However, contracts should avoid blocking future replay-safe durable mode. Specifically:

- `StepResult` is already a serializable intent object â€” it can be recorded and replayed
- Business state mutation through `StepContext.State` is direct (not replay-safe). For durable replay mode, this may need to change to a recorded-delta or snapshot model. That decision is deferred.
- The orchestration ownership rule above ensures that runtime decisions flow through one deterministic path (the interpreter), which is the prerequisite for replay
- Infrastructure steps (`If`, `While`, `Parallel`, `WhenAll`, `Wait`) are already deterministic â€” their behavior depends only on business state and definition structure, not on external I/O or timing

No contracts in the minimal core actively prevent replay. The main future pressure points are:
- Business state mutation model (direct vs recorded)
- Event safety guarantee already supports durable mode: transactional consumption ensures events survive restart when backed by a persistent provider. The contract is designed now; the persistence is added later.

---

## 11. TDD entry sequence

Following the recommended test order from `minimal-core-scope.md`:

1. **MC-AT-001**: Forces `WorkflowBuilder`, `IStep`, `StepResult.Completed`, `WorkflowRuntime.ExecuteAsync`, `WorkflowEngine<T>.Start`
2. **MC-AT-003**: Forces `WaitStep`, `StepResult.WaitForEvent`, `WaitRecord`, `RuntimeState.ActiveWaits`
3. **MC-AT-004**: Forces `EventEnvelope`, `EventMatcher`, `RaiseEvent`, resume path in interpreter
4. **MC-AT-005**: Forces negative matching logic in `EventMatcher`
5. **MC-AT-015**: Forces per-instance event buffer (mailbox), bidirectional matching, `PendingEvent`
6. **MC-AT-016**: Forces `EventId` deduplication, `ConsumedEventIds`
7. **MC-AT-017**: Forces `CorrelationIndex`, `EventRouter`, `WorkflowEngine.RaiseEvent` (correlation-targeted routing)
8. **MC-AT-018**: Forces `WorkflowEngine<T>.RaiseEvent` (fanout), `InMemoryInstanceStore.GetAllByDefinition`
9. **MC-AT-002**: Forces `IfStep`, conditional branching in interpreter
10. **MC-AT-013**: Forces `WhileStep`, loop handling in interpreter, execution pointer re-entry
11. **MC-AT-009**: Forces `StepResult.Failed`, failure transition
12. **MC-AT-006**: Forces `ParallelStep`, `BranchState`, `WhenAll` join logic
13. **MC-AT-007**: Forces branch-scoped wait isolation
14. **MC-AT-008**: Forces deterministic join regardless of ordering
15. **MC-AT-014**: Forces wait-inside-loop isolation â€” each iteration gets fresh WaitRecords
16. **MC-AT-010**: Forces `Where(x => ...)` expression translation, `SelectionScope.List()`, `WorkflowInstanceSnapshot`
17. **MC-AT-012**: Forces `InstanceExecutionSerializer` under concurrent pressure
18. **MC-AT-011**: Forces `WaitLong` rejection at builder or runtime level

Each test drives the discovery of the next interface. The design above is the target shape, but TDD will refine it.

