# Child Workflow Orchestration Design (v2)

Drafted on April 10, 2026.

This document supersedes `child-workflow-orchestration-design.md`. It keeps the
public shape proposed in v1 (`RunChild` / `RunChildren`, partitioners, rich
runtime inspection) and adds the runtime mechanism, engine-scoping rules, and
correctness invariants needed to implement it. The v1 document remains as
reference for comparison.

## Purpose

Provide a first-class OrcaCore feature for orchestrating:

- one child workflow
- many child workflows created dynamically at runtime from runtime-determined
  batches

The goal is to fully cover:

- runtime batch size known only during execution
- split and join all children before parent continues
- split and let each child continue independently
- per-item workflow execution
- per-batch workflow execution
- distributed orchestration across service bus and external services
- saga-style compensation of completed children after later-stage failure

This is a first-class orchestration feature, not a business-step workaround.

Child workflows in this design are intentionally resultless. They expose
lifecycle, lineage, failure, cancellation, inspection, and compensation
semantics, but they do not return a typed value to the parent. If OrcaCore
later needs parent-visible typed child outcomes, that must be introduced as a
separate step type with its own typing and persistence contract rather than
overloading `RunChild` / `RunChildren` with function-call semantics.

## Engine Awareness Invariant

The ephemeral engine must have **zero** saga awareness at three levels:

1. **Code level** — the ephemeral runtime has no types, references, or
   conditionals involving saga or compensation concepts.
2. **API level** — the ephemeral builder surface cannot mention saga-aware
   types at all.
3. **Assembly/namespace level** — saga types live in
   `OrcaCore.Runtime.Durable.Sagas`, which the ephemeral runtime does not
   reference. This is the enforcement mechanism: if the reference does not
   exist, the leak cannot happen.

Everything else in this design — the DSL, the runtime mechanism, the join
policies, the failure policies, the inspection API, cancellation — is
engine-agnostic and lives in core. The only engine-scoped concerns are:

- **Saga / compensation** — durable-only, isolated in `Durable.Sagas`
- **Outbox durability** — core defines the shape; ephemeral has a trivial
  in-memory pump, durable has a persistent pump
- **Group store durability** — same pattern

## Design Principles

The following rules apply across the design and any future extensions.

### Structured retries rule

Wherever retry appears in the system, it must be structured, not a bare enum.

A retry policy must always carry:

- `MaxAttempts` — explicit upper bound
- `Backoff` — strategy (constant, linear, exponential, decorrelated jitter)
- `ShouldRetry` — optional predicate over failure context
- explicit terminal condition — what happens after exhaustion

Group-level retry of child workflows is intentionally **not** part of this
design. Retry belongs at the operation level — inside child workflow steps or
inside infrastructure adapters. Group-level retry conflates transient failure,
invalid input, downstream business rejection, and orchestration failure, and is
easier to misuse than to use correctly. If a concrete scenario later proves
group-level retry is necessary, it can be introduced deliberately with durable
counters, structured backoff, and explicit terminal semantics.

### Orthogonal policy axes

Join policy (how the parent waits) and failure policy (how failures are
interpreted) are orthogonal and are expressed as two independent parameters on
`RunChildren`. They are never combined into a single enum.

### Explicit compensation, never implicit

Compensation is always triggered by explicit workflow logic, never as a
side effect of join resolution or cancellation. `RunChildren` does not have
a failure-policy value that implicitly compensates.

### Exactly-once resume

Parent resume after join satisfaction must fire exactly once per group.
Late child terminations update the group record but must not re-trigger the
parent. This is a correctness requirement, not a performance optimization.

### Cooperative cancellation

Cancellation is never hard. It is an intent that children observe at
well-defined boundaries. Past side effects are not rewound by cancellation —
only by explicit compensation.

## Motivation

There is a recurring class of workflow problems where:

- the parent discovers work items at runtime
- work items may be individual ids or runtime-determined batches
- each work item may require multiple steps, waits, retries, status messages,
  or compensation
- the parent needs explicit continuation policy (wait for all, wait for any,
  continue each independently, fire and forget)

Examples:

- e-store order item processing
- price refresh batches where batch size is known only at runtime
- service-bus orchestration across many services
- fanout/fanin batch coordination with aggregation and compensation

Static `Parallel` is not sufficient:

- branch count is fixed at definition time
- runtime-created batches are not representable as static branches
- item/batch lifecycle needs explicit tracking and inspection

## Proposed Feature

Two public primitives:

- `RunChild` — orchestrate a single child workflow
- `RunChildren` — orchestrate many child workflows created dynamically

Both are orchestration primitives, not business steps. The parent workflow
selects inputs, optionally partitions them into batches, starts child workflow
instances, tracks their lifecycle, applies join/failure policy, and optionally
aggregates results. Each child is a real workflow instance with its own
definition, state, and lifecycle.

## Public API

### One child

```csharp
builder.RunChild(
    input:    s => new ReserveOrderInput(s.OrderId),
    child:    c => c.Workflow<ReserveOrderWorkflow>(),
    join:     ChildJoinPolicy.Wait);
```

### Many children, per-item

```csharp
builder.RunChildren(
    items:     s => s.OrderItemIds,
    partition: p => p.Item(),
    child:     c => c.Workflow<ReserveOrderItemWorkflow>(),
    join:      ChildJoinPolicy.WhenAll,
    failure:   ChildFailurePolicy.WaitAllThenFail);
```

### Many children, runtime-determined batch size

```csharp
builder.RunChildren(
    items:          s => s.ListingIds,
    partition:      p => p.Batch(sizeSelector: s => s.PriceServiceMaxBatchSize),
    child:          c => c.Workflow<UpdatePriceBatchWorkflow>(),
    join:           ChildJoinPolicy.WhenAll,
    failure:        ChildFailurePolicy.ContinueWithPartialResults,
    maxConcurrency: 4);
```

### Fire-and-forget fan-out

```csharp
builder.RunChildren(
    items:     s => s.ListingIds,
    partition: p => p.Batch(10),
    child:     c => c.Workflow<UpdatePriceBatchWorkflow>(),
    join:      ChildJoinPolicy.FireAndForget);
```

### Independent continuation

Each child runs the rest of its workflow on its own; the parent does not
aggregate-join.

```csharp
builder.RunChildren(
    items:     s => s.ListingIds,
    partition: p => p.Item(),
    child:     c => c.Workflow<ProcessListingWorkflow>(),
    join:      ChildJoinPolicy.ContinueEach);
```

## Partitioner

Partitioning is a first-class, runtime-aware abstraction. It turns
`IEnumerable<TItem>` into an ordered sequence of batches. `Item()` is the
single-item case expressed as batches of size 1, so there is one codepath for
both single-item and multi-item fan-out.

```csharp
public interface IPartitioner<TState, TItem>
{
    IEnumerable<IReadOnlyList<TItem>> Partition(TState state, IEnumerable<TItem> items);
}

public static class Partitioners
{
    public static IPartitioner<TState, TItem> Item<TState, TItem>();
    public static IPartitioner<TState, TItem> Batch<TState, TItem>(int size);
    public static IPartitioner<TState, TItem> Batch<TState, TItem>(Func<TState, int> sizeSelector);
    public static IPartitioner<TState, TItem> Custom<TState, TItem>(
        Func<TState, IEnumerable<TItem>, IEnumerable<IReadOnlyList<TItem>>> partition);
}
```

Ordering produced by the partitioner must be **deterministic and stable**
across calls for the same input. This is required so that deterministic child
ids (see below) are stable across restart.

## Policies

### Join policy

```csharp
public enum ChildJoinPolicy
{
    Wait,           // single child; parent waits for its completion
    WhenAll,        // parent continues when all children are terminal
    WhenAny,        // parent continues when first child completes successfully
    ContinueEach,   // each child continues independently; parent does not aggregate
    FireAndForget   // parent advances immediately; children are launched and untracked for join purposes
}
```

`WhenAny` residual behavior is controlled by a separate option:

```csharp
public enum WhenAnyResidualPolicy
{
    CancelRemaining,        // default
    LetRemainingComplete,
    DetachRemaining
}
```

`CancelRemaining` issues cooperative cancellation intent to remaining children.
It does not guarantee they stop immediately (see Cooperative Cancellation).

### Failure policy

```csharp
public enum ChildFailurePolicy
{
    FailFast,                   // first failure fails the parent; cancels remaining per join semantics
    WaitAllThenFail,            // wait for all children to reach terminal states, then fail if any failed
    ContinueWithPartialResults  // continue parent; expose partial results via the group record
}
```

Both enums live in core and apply to both engines without modification.
`RunChild` only permits `Wait`. `RunChildren` permits all other join values.

## Runtime State Model

### Instance lineage

Every workflow instance carries lineage metadata. Root is set once at
top-level start and inherited by every descendant.

```csharp
public sealed record InstanceLineage(
    string  InstanceId,
    string  RootInstanceId,       // stable across the whole tree
    string? ParentInstanceId,     // null for root
    string? ParentGroupId,        // null for root and for non-fanout spawns
    int     Depth);               // 0 for root, parent.Depth + 1 for children
```

Tree queries use `RootInstanceId`; lineage traces walk `ParentInstanceId`.
`Depth` is a cheap diagnostic and an accidental-recursion guard.

### Child workflow group (core)

```csharp
public sealed record ChildWorkflowGroup(
    string                             GroupId,
    string                             ParentInstanceId,
    string                             RootInstanceId,
    string                             NodePath,
    string                             ChildDefinitionId,
    string                             ChildDefinitionVersion,   // pinned at creation
    ChildJoinPolicy                    JoinPolicy,
    ChildFailurePolicy                 FailurePolicy,
    WhenAnyResidualPolicy?             WhenAnyResidual,
    int?                               MaxConcurrency,
    ChildGroupStatus                   Status,
    BarrierState                       Barrier,
    string?                            ResumeTokenId,
    DateTimeOffset?                    ResumeRecordedAt,
    int                                TotalChildren,
    int                                NextDispatchIndex,
    int                                ActiveChildren,
    int                                CompletedChildren,
    int                                FailedChildren,
    int                                CancelledChildren,
    IReadOnlyList<ChildWorkflowRef>    Children,
    DateTimeOffset                     CreatedAt,
    DateTimeOffset?                    CompletedAt);

public sealed record ChildWorkflowRef(
    string                   ChildInstanceId,   // deterministic: Derive(parentId, groupId, index)
    int                      Index,             // stable position
    string                   ChildDefinitionId,
    string                   ChildDefinitionVersion,
    object                   Input,             // materialized snapshot
    ChildWorkflowStatus      Status,
    string?                  Error,
    DateTimeOffset?          StartedAt,
    DateTimeOffset?          CompletedAt);

public enum ChildGroupStatus
{
    Scheduling,
    Waiting,
    Completed,
    Failed,
    Cancelled,
    PartiallyCompleted       // for ContinueWithPartialResults
}

public enum ChildWorkflowStatus
{
    Pending,
    Scheduled,
    Started,
    Running,
    Waiting,
    CancellationRequested,   // cooperative-cancel intent delivered, not yet observed
    Completed,
    Failed,
    Cancelled
}

public enum BarrierState
{
    Pending,
    Resolving,
    Resolved
}
```

Runtime queries and inspection operate over child status, lineage, timestamps,
errors, and child-instance state. The parent does not receive a typed return
value from the child.

`MaxConcurrency`, `NextDispatchIndex`, and `ActiveChildren` are executable
scheduler state, not reporting-only metadata. They exist so throttling can be
enforced durably and reconstructed on restart.

### Durable extension for sagas

Saga-related fields live in a separate durable record. The ephemeral engine
never sees these fields.

```csharp
// OrcaCore.Runtime.Durable.Sagas
public sealed record DurableChildWorkflowGroup(
    ChildWorkflowGroup                         Core,
    IReadOnlyList<DurableChildWorkflowRef>     Refs,
    IReadOnlyList<CompensationRecord>          Compensations);

public sealed record DurableChildWorkflowRef(
    ChildWorkflowRef  Core,
    object?           CompensationPayload,   // set by child via ctx.SetCompensationPayload(...)
    bool              IsCompensated);
```

`CompensationPayload` is a durable-only payload and must satisfy the same
serializer contract as child input snapshots. Durable saga APIs may expose a
typed authoring surface later, but the persistence rule is already fixed here:
payloads must be serializer-supported and fail fast on write if they are not.

## Runtime Mechanism

This section specifies how `RunChildren` lowers onto the runtime. The slice
engine and the event-driven engine implement the same conceptual mechanism
with different durability backing.

### Group creation is atomic

When the runtime reaches a `RunChildren` node, a single commit records all of
the following together:

1. Evaluate the `items` selector and materialize the full list.
2. Run the partitioner to produce an ordered sequence of batches.
3. Generate `GroupId` and deterministic `ChildInstanceId` values
   (see below).
4. Build the `ChildWorkflowGroup` with all `ChildWorkflowRef`s in `Pending`
   status, inputs fully materialized, barrier `Pending`, `NextDispatchIndex = 0`,
   and `ActiveChildren = 0`.
5. For non-`FireAndForget` joins: register a synthetic `WaitRecord` on the
    parent with `EventName = "__child_group__"` and `CorrelationId = groupId`.
6. Write `ChildStartCommand` records only for the first dispatch window:
   - all children if `MaxConcurrency` is null
   - otherwise only the first `MaxConcurrency` children
   and advance `NextDispatchIndex` / `ActiveChildren` accordingly.
7. Transition the parent to `Waiting` (or advance for `FireAndForget`).

Steps 4, 5, and 6 must commit atomically. On crash between any of them, the
runtime has either all of group creation or none of it.

### Deterministic child ids

Child instance ids are derived from `(ParentInstanceId, GroupId, Index)` via a
stable hash. This makes child spawn idempotent under retry: the outbox pump
uses `CreateInstanceIfNotExists(childId, ...)` so re-dispatch after crash
cannot produce duplicates.

Deterministic ids require deterministic partitioner output. The partitioner
contract states this explicitly; the materialized snapshot in step 1 pins the
item order so that restart replays the same sequence.

### Item snapshot

The materialized `items` list and partitioner output are stored in the group
record at creation time. On restart, the outbox pump does not re-evaluate the
selector and does not re-run the partitioner — it reads the pre-computed input
payloads directly from `ChildWorkflowRef.Input`. This guarantees idempotency
even if the source data has changed between crash and restart.

`TItem` must be serializable by the configured persistence serializer.
See Item Serializability below.

### Outbox pump, child start, and throttling

The unified outbox pump (see Unified Outbox) reads `ChildStartCommand` records
and dispatches each to `ChildStartDispatcher`, which calls
`InstanceStore.CreateInstanceIfNotExists(childId, definitionId, input,
lineage)`. The child instance inherits `RootInstanceId` from the parent's
lineage and records `ParentInstanceId` and `ParentGroupId` on its own lineage.

On success, the outbox record is marked `Dispatched` and the corresponding
`ChildWorkflowRef` transitions `Pending → Scheduled → Started`.

If `MaxConcurrency` is set, the group acts as a durable scheduler:

- `ActiveChildren` counts children in non-terminal started states
- `NextDispatchIndex` points at the next not-yet-dispatched child ref
- a child terminal transition decrements `ActiveChildren`
- after decrement, the runtime enqueues additional `ChildStartCommand` records
  until `ActiveChildren == MaxConcurrency` or no pending children remain

Restart reconstructs the scheduler from persisted group state:

- `ActiveChildren` is restored directly from the group record or recomputed
  from child statuses if repair is needed
- `NextDispatchIndex` is restored from the persisted group record
- the dispatcher emits start commands only for the remaining window, never for
  already-started children

Without this scheduler state, `maxConcurrency` would be only advisory. In this
design it is a durable execution constraint.

### Child lifecycle hook

When any instance transitions to a terminal state
(`Completed`, `Failed`, `Cancelled`), a hook in `InstanceLifecycle`:

1. Looks up the instance's `ParentGroupId` via `ChildRelationshipIndex`.
   If null, it is a root or non-fanout child and the hook exits.
2. Writes the child's `Error` and `CompletedAt` onto its `ChildWorkflowRef`.
3. Atomically increments the appropriate counter on the group
   (`CompletedChildren`, `FailedChildren`, or `CancelledChildren`).
4. Evaluates the join predicate (see Barrier State Machine).
5. If resolution fires, raises a synthetic `"__child_group__"` event with
   `CorrelationId = groupId` toward the parent instance.

Step 2 and 3 must be atomic with respect to the group record. Step 4 and 5
are guarded by the barrier CAS below.

If the parent needs business data produced by children, it must obtain it via
one of these explicit channels:

- inspect child workflow state directly
- query a shared projection/read model
- react to child-published status or domain messages

This design intentionally does not provide a built-in child return value.

In the `Durable.Sagas` namespace, an additional extension is available only
to the durable engine:

```csharp
// OrcaCore.Runtime.Durable.Sagas
public static class DurableStepContextExtensions
{
    public static void SetCompensationPayload<T>(
        this IStepContext<TState> ctx, T payload);
}
```

### Barrier state machine (exactly-once resume)

The barrier has three states: `Pending → Resolving → Resolved`. Transitions
are one-way and CAS-guarded.

Barrier resolution is driven by an explicit durable resume token, not by
assuming the parent's synthetic wait still exists. The winning transition must
record `ResumeTokenId` on the group before parent resume is dispatched. Parent
resume consumes that token idempotently; restart logic consults the token, not
the parent wait, when deciding whether another resume attempt is allowed.

On each terminal child event, after updating the ref and counters, the hook
evaluates the join predicate:

- **WhenAll**:
  `CompletedChildren + FailedChildren + CancelledChildren == TotalChildren`
- **WhenAny**: `CompletedChildren >= 1`

If the predicate holds and the barrier is `Pending`, the hook attempts
`CAS(Pending → Resolving)`. The winning thread:

1. Records a durable `ResumeTokenId` and `ResumeRecordedAt` on the group.
2. For `WhenAny + CancelRemaining`, records `CancellationRequested` on all
   eligible residual children before the parent resume is emitted.
3. Emits the synthetic `"__child_group__"` event to the parent, carrying the
   recorded resume token.
4. Updates the group `Status` per failure policy:
   - `WhenAll` + `FailFast` + any failure → group `Failed`
   - `WhenAll` + `WaitAllThenFail` + any failure → group `Failed`
   - `WhenAll` + `ContinueWithPartialResults` + any failure → group `PartiallyCompleted`
   - `WhenAll` + no failure → group `Completed`
   - `WhenAny` + success → group `Completed`
5. CAS `Resolving → Resolved` after the event commit and group update both
   succeed.

Losing threads (concurrent terminations whose predicate check also passed) see
the barrier already in `Resolving` or `Resolved` and take no resume action.
They still commit their ref/counter updates, so the group record is accurate
for later inspection.

After `Resolved`, subsequent child terminations continue to update refs and
counters but never emit another resume event. This gives exactly-once parent
resume regardless of termination order, concurrent terminations, or crash
boundaries.

On restart, the durable engine reloads the group record. If the barrier is
already `Resolved`, no action is taken beyond honoring any still-pending
state mutations. If the barrier is `Pending`, the runtime may re-evaluate the
predicate and attempt a fresh winning transition. If the barrier is
`Resolving`, restart logic checks `ResumeTokenId`:

- if no token was recorded durably, the winner crashed before recording resume
  intent, so a new winning transition may be attempted
- if a token was recorded, the runtime retries dispatch/consumption of that
  same token idempotently and must not mint a new one

This closes the crash window between "parent resume was attempted" and
"barrier reached `Resolved`" without relying on whether the parent's synthetic
wait still happens to exist.

### Cooperative cancellation

`CancelChildGroupAsync(groupId)` and `WhenAny + CancelRemaining` both issue
cancellation **intent**, not hard stops. Affected children transition
`Running → CancellationRequested` (or `Waiting → CancellationRequested`).

For `WhenAny + CancelRemaining`, the ordering rule is explicit:

1. the winning barrier transition records `CancellationRequested` on eligible
   residual children
2. only after that durable update commits may the parent resume token be
   emitted

This means the parent may continue while residual children are still running,
but not before they have at least been durably told to stop.

Children observe cancellation at:

- step boundaries (between steps in the child's sequence)
- wait registration (an active wait is cancelled and the child transitions to
  `Cancelled`)
- inside steps that honor `StepContext.CancellationToken`

A child mid-external-call that does not honor the token keeps running until
the call returns. Past side effects are not rewound. Rollback, if needed, is
a child-defined compensation concern and is not implied by cancellation.

A child that races past cancellation and completes successfully is still
recorded as `Completed`, but the parent is not re-triggered: the barrier is
already `Resolved` and the late terminal update affects inspection only.

`Cancelled` is the terminal state; `CancellationRequested` is an intermediate
observable state useful for inspection ("is this child being told to stop").

### Unified outbox

All durable post-commit dispatch uses one outbox with typed record kinds.

```csharp
public enum OutboxRecordKind
{
    ExternalMessage,            // publish to service bus, HTTP endpoint, etc.
    ChildStartCommand,          // spawn a child workflow instance
    StatusMessage               // intermediate workflow status publish
}

public sealed record OutboxRecord(
    string              RecordId,
    OutboxRecordKind    Kind,
    string              InstanceId,
    string?             GroupId,
    byte[]              Payload,
    DateTimeOffset      EnqueuedAt,
    int                 AttemptCount,
    DateTimeOffset?     NextAttemptAt,
    OutboxRecordStatus  Status);

public enum OutboxRecordStatus { Pending, InFlight, Dispatched, Poisoned }

public interface IOutboxDispatchHandler
{
    OutboxRecordKind Kind { get; }
    Task<DispatchResult> DispatchAsync(OutboxRecord record, CancellationToken ct);
}
```

One pump drains records, switches on `Kind`, and routes to a registered
handler. Handlers registered by core:

- `ExternalMessageDispatcher`
- `ChildStartDispatcher`
- `StatusMessageDispatcher`

`Durable.Sagas` extends the core outbox via durable-only handler and payload
registration. Compensation dispatch is intentionally not a core enum member and
does not appear in core or ephemeral APIs. This preserves the zero-saga-awareness
invariant: saga orchestration is a durable extension over the shared outbox
infrastructure, not a symbol leaked into core abstractions.

Retry, backoff, poison handling, observability, and retention are single
implementations shared across all kinds. Physical isolation by kind is a
later tuning knob, never an initial requirement.

The ephemeral engine provides a trivial in-memory pump that drains
synchronously before returning from the enclosing commit; the durable engine
provides a persistent pump with real retry and poison handling.

### Item serializability

Partial build-time validation plus strict runtime enforcement.

**Build-time** (`WorkflowBuilder.Build()`):

For each `RunChildren<TItem>` node, the engine's configured
`IPersistenceSerializer` probes `CanSerialize(typeof(TItem))`:

- **Error** for clearly unsupported declared types: `object`, `dynamic`,
  interfaces without registered serializer support, open generics.
- **Warning** for ambiguous types: abstract classes without discriminator,
  types with non-serializable public members of unknown shape.
- **Silent pass** when the serializer confirms support.

Build-time cannot know concrete object graphs; it only validates declared
types.

**Runtime** (`RunChildren` execution, immediately after partitioning):

Every materialized batch input is serialized before any spawn intent is
written. On serialization failure, the `RunChildren` node fails with an error
identifying the item index and type. No child is spawned, no group is
created, and the parent transitions to `Failed` per its failure policy
semantics.

Result payload serializability is intentionally out of scope because child
workflows are resultless in this design. If a future feature introduces typed
child outcomes, it must define its own serializer contract explicitly.

## Management Surface

Instance-scoped operations:

```csharp
engine.Instance(id).GetChildGroupsAsync();
engine.Instance(id).GetChildGroupAsync(groupId);
engine.Instance(id).GetChildWorkflowsAsync(groupId);
engine.Instance(id).GetChildWorkflowAsync(childInstanceId);

engine.Instance(id).CancelChildGroupAsync(groupId);     // cancel-only, engine-agnostic
engine.Instance(id).CancelChildAsync(childInstanceId);
```

Tree-scoped operations via `RootInstanceId`:

```csharp
engine.Tree(rootId).GetAllInstancesAsync();
engine.Tree(rootId).GetInstancesByStatusAsync(status);
engine.Tree(rootId).GetLineageAsync(instanceId);        // walks ParentInstanceId chain
```

Durable-only compensation API (`OrcaCore.Runtime.Durable.Sagas`):

```csharp
public interface ICompensationScope
{
    Task CompensateAsync(
        string groupId,
        CompensationSpec spec,
        CancellationToken ct = default);
}

public sealed record CompensationSpec(
    DefinitionId                       CompensationDefinitionId,
    string                             CompensationDefinitionVersion,
    Func<ChildWorkflowRef, object>     InputFactory,
    ChildJoinPolicy                    JoinPolicy = ChildJoinPolicy.WhenAll,
    int?                               MaxConcurrency = null);
```

`CompensateAsync(groupId, spec)` spawns one compensation workflow instance per
`ChildWorkflowRef` in `Completed` state. Skipped refs:

- `Failed` — nothing to compensate
- `Cancelled` — nothing to compensate
- `CancellationRequested` — wait or skip per caller's responsibility
- already `IsCompensated == true` — idempotent skip

Compensation itself uses the same `RunChildren` machinery internally: it is a
specialized fan-out over completed refs with a compensation definition. This
gives compensation join/failure semantics for free and reuses the barrier,
outbox, and inspection infrastructure.

Compensation is never triggered implicitly. It must be invoked from a parent
workflow step (typically in an error-handling branch or an explicit
"cleanup" step).

## Engine-Driven Semantics

### Event-driven engine

Fan-out maps naturally onto events. Parent events:

- `ChildGroupCreated`
- `ChildScheduled` (one per child)
- `ChildStarted` (per child)
- `ChildCompleted` (per child, with result)
- `ChildFailed` (per child, with error)
- `ChildCancellationRequested` (per child)
- `ChildCancelled` (per child)
- `ChildBarrierResolving` (internal, the CAS winner)
- `ChildGroupJoinSatisfied`
- `ChildGroupCompleted` / `ChildGroupFailed` / `ChildGroupPartiallyCompleted`

Barrier CAS is expressed as a stream-version guard on the
`ChildBarrierResolving` event: only one committer wins; losers observe the
committed event and abort.

This is the recommended first implementation target.

### Ephemeral (slice) engine

The same mechanism works with in-memory backing:

- `ChildWorkflowGroup` lives in an in-memory group store keyed by `GroupId`.
- `ChildRelationshipIndex` is an in-memory dictionary.
- The barrier CAS is a lock/`Interlocked` operation on the in-memory record.
- The outbox drains synchronously in-process before the commit returns.
- The synthetic `"__child_group__"` wait uses the existing `WaitRecord`
  infrastructure unchanged.

No new `ExecutionFrame` kind is introduced. The slice engine's stack machine
does not need to know about children — from its perspective, `RunChildren`
is just a step that commits records, registers a wait, and suspends, exactly
like any other `Wait`.

## Relationship to `Parallel` and Future `ForEach`

| Primitive | When to use |
|---|---|
| `Parallel` (existing, static) | Static branch count known at definition time; in-process shared state; no durability reason to split instances. Scenario: complex in-memory orchestration with fixed parallel structure. |
| `RunChild` / `RunChildren` (this proposal) | Dynamic batch count known only at runtime; cross-instance isolation; durability, distribution, audit, or saga per batch. Scenarios: e-store orders, price updates, service-bus orchestration. |
| `ForEach` (deferred) | Lightweight in-process runtime fan-out with shared state and no child identity. **Not built now.** Only add if a concrete scenario surfaces that cannot be served by either `Parallel` or `RunChildren`. |

`RunChildren` does not replace `Parallel`. Static in-process parallelism with
shared state is genuinely different from dynamic cross-instance fan-out, and
forcing one primitive to serve both hurts both use cases.

## Engine Scoping Summary

| Type / API | Core | Durable | Durable.Sagas |
|---|:-:|:-:|:-:|
| `ChildJoinPolicy` | yes | yes | |
| `ChildFailurePolicy` | yes | yes | |
| `WhenAnyResidualPolicy` | yes | yes | |
| `ChildWorkflowStatus` (incl. `CancellationRequested`) | yes | yes | |
| `ChildGroupStatus` | yes | yes | |
| `BarrierState` | yes | yes | |
| `IPartitioner<TState, TItem>` + built-ins | yes | yes | |
| `ChildWorkflowGroup` / `ChildWorkflowRef` | yes | yes | |
| `InstanceLineage` (Root/Parent/Depth) | yes | yes | |
| `OutboxRecord` / `OutboxRecordKind` / pump interface | yes | yes | |
| `CancelChildGroupAsync` / `CancelChildAsync` | yes | yes | |
| `engine.Tree(...)` query surface | yes | yes | |
| In-memory outbox pump | yes | | |
| Persistent outbox pump | | yes | |
| In-memory group store | yes | | |
| Persistent group store | | yes | |
| `DurableChildWorkflowGroup` / `DurableChildWorkflowRef` | | | yes |
| `CompensationPayload` / `IsCompensated` | | | yes |
| `ICompensationScope` / `CompensationSpec` | | | yes |
| `DurableStepContextExtensions.SetCompensationPayload<T>` | | | yes |
| Durable-only compensation outbox registration | | | yes |

Note: the core `ChildFailurePolicy` enum is used in both engines unchanged.
There is no `DurableChildFailurePolicy` type. Compensation is an explicit
API in `Durable.Sagas`, never a failure-policy value.

## Usage Patterns

### Pattern 1: Split and join all

Parent discovers 100 ids at runtime; runtime batch size is 10. Ten child
workflows are created, each processing a batch of 10 ids. Parent uses
`WhenAll + ContinueWithPartialResults`, then inspects child completion state
or shared projections in a follow-up step.

### Pattern 2: Split and continue independently

Parent discovers 100 ids. One child per id with `ContinueEach`. Each child
runs the rest of its own workflow independently; parent either completes
immediately or tracks only group creation.

### Pattern 3: Distributed service-bus orchestration

Each child publishes a command to the service bus, waits for the response
event, updates status, completes or fails. Parent uses `WhenAll` to join.
Status messages flow through the same unified outbox as child start
commands, with shared retry and poison semantics.

### Pattern 4: Saga fanout with compensation

Parent starts many child reservation workflows with `WhenAll + FailFast`.
A later parent step performs payment. If payment fails, the parent step
calls `CompensateAsync(reservationGroupId, spec)` from the durable saga
scope. Completed reservations are compensated; failed or cancelled ones are
skipped.

### Pattern 5: Complex in-memory logic

Does not use this feature. Use `Parallel` with shared state. Child workflow
overhead is unjustified when there is no external wait, no durability
requirement, and no audit need.

## Acceptance Criteria

### Core fan-out

- **CW-AT-001** Parent with `WhenAll` continues only after all children complete.
- **CW-AT-002** Runtime batch size of 10 over 23 items produces exactly 3 child workflows (sizes 10, 10, 3).
- **CW-AT-003** `ContinueEach` — each child continues independently; parent does not wait for aggregate join.
- **CW-AT-004** Durable parent survives host restart and still joins children correctly.
- **CW-AT-005** `WaitAllThenFail` waits for all children, then fails parent predictably.
- **CW-AT-006** Deterministic child ids — restarting during spawn produces the same child ids with no duplicates.
- **CW-AT-007** Item snapshot stability — restart before any child starts produces the same partition result even if the source selector would return different values.
- **CW-AT-008** Group inspection returns fully populated child refs with stable status, timestamps, and child instance ids after completion.
- **CW-AT-009** `maxConcurrency` is honored across restart, not only at initial spawn.
- **CW-AT-010** Tree queries via `RootInstanceId` return all descendants across two levels of nested `RunChildren`.
- **CW-AT-011** `ContinueWithPartialResults` leaves group in `PartiallyCompleted` state exposing both successes and failures; parent continues.
- **CW-AT-012** `WhenAny + CancelRemaining` — parent resumes on first success; residuals enter `CancellationRequested` before parent resume.
- **CW-AT-013** `CancelChildGroupAsync` transitions all non-terminal children through `CancellationRequested`.
- **CW-AT-014** Crash during outbox drain — pump resumes, spawns only not-yet-started children, no duplicates.

### Barrier correctness

- **CW-AT-015** Barrier fires exactly once under concurrent completions; parent receives exactly one resume event regardless of termination order or concurrency.
- **CW-AT-016** After barrier resolution, late residual terminations update refs but do not re-trigger parent.
- **CW-AT-017** Cooperative cancellation is observed at step boundary; a child mid-step does not transition to `Cancelled` until the next boundary.
- **CW-AT-018** A residual child that races past cancellation and completes successfully is recorded as completed but does not re-trigger the parent.

### Serializability and outbox

- **CW-AT-019** Build-time rejection of `RunChildren<object>` and other clearly unserializable declared types.
- **CW-AT-020** Runtime rejection of an unserializable concrete item — parent transitions to `Failed` before any spawn intent is written.
- **CW-AT-021** One physical outbox carries `ChildStartCommand` and `ExternalMessage` records concurrently; one poison message of one kind does not stall the other.
- **CW-AT-022** `RootInstanceId` propagates through two levels of nested `RunChildren`; lineage trace via `ParentInstanceId` chain works from leaf to root.

### Saga (durable only)

- **CW-AT-Saga-001** Explicit `CompensateAsync(groupId, spec)` from a parent step spawns one compensation per `Completed` child, skipping `Failed` and `Cancelled` refs.
- **CW-AT-Saga-002** Compensation is never triggered by cancellation; a cancelled group has `IsCompensated == false` for all refs until `CompensateAsync` is explicitly invoked.
- **CW-AT-Saga-003** Compensation payload set by the child via `ctx.SetCompensationPayload(...)` is available on the durable ref at compensation time.
- **CW-AT-Saga-004** Compensation is idempotent — `CompensateAsync` called twice for the same group does not double-compensate.

## Recommended Implementation Order

Event-driven durable engine first; backport to ephemeral only when the feature
is stable.

1. **Public API + core types** — `RunChild`, `RunChildren`, `ChildJoinPolicy`,
   `ChildFailurePolicy`, `ChildWorkflowStatus`, `ChildGroupStatus`,
   `BarrierState`, `IPartitioner`, `InstanceLineage`, builder integration.
2. **Unified outbox infrastructure** — `OutboxRecord`, `OutboxRecordKind`,
   pump, handler interface, retry and poison handling. This is the prerequisite
   for everything that follows.
3. **Event-driven engine: `RunChild` with `Wait`** — single child, happy path,
   deterministic id, outbox-driven start, barrier CAS on single completion.
4. **Event-driven engine: `RunChildren` with `Item()` + `WhenAll` + `FailFast`**
   — core fan-out, barrier correctness, lifecycle hook, inspection API.
5. **Runtime-determined batching** — `Batch(size)` and
   `Batch(sizeSelector)` partitioners.
6. **Failure policies** — `WaitAllThenFail`, `ContinueWithPartialResults`.
7. **`WhenAny` + residual policy** — including cooperative cancellation of
   residuals and the `CancellationRequested` intermediate state.
8. **`ContinueEach` and `FireAndForget`** — non-joining modes.
9. **Inspection and parent follow-up** — parent queries child status/state or shared projections; no child return value path.
10. **Tree queries** — `engine.Tree(rootId)` surface over `RootInstanceId`.
11. **Throttling** — `maxConcurrency` honored across restart.
12. **Build-time and runtime serializability checks**.
13. **Ephemeral engine backport** — slice engine uses the same types, in-memory
    outbox pump, in-memory group store.
14. **`Durable.Sagas` namespace and compensation API** — last. Depends on a
    stable fan-out foundation. Adds `DurableChildWorkflowGroup`,
    `CompensationSpec`, `ICompensationScope`, and durable-only compensation
    outbox registration.

## Deferred Decisions

The following are explicitly deferred and may be revisited once the feature
is in use:

- **Group-level retry of child workflows.** Not in scope. Retries live at
  step level inside the child, per the structured retries design principle.
  If a concrete scenario later justifies it, introduce as a durable-only
  structured policy, never as a bare enum.
- **Lightweight `ForEach` primitive.** Not in scope. Add only if a concrete
  scenario emerges that cannot be served by `Parallel` or `RunChildren`.
- **Typed child outcomes.** Not in scope. If a future scenario requires
  parent-visible typed child results, introduce a separate step type with its
  own typing, serializer, and persistence contract rather than extending
  `RunChild` / `RunChildren`.
- **Physical outbox partitioning by kind.** Not in scope initially. One
  physical outbox with typed records; split later only if throughput or
  isolation pressure requires it.
- **Cross-host distribution of child workflows.** The Child node design is
  compatible with multi-host execution (each child is a normal instance), but
  actual multi-host scheduling is a separate infrastructure effort.
- **Automated global max-depth guard.** `Depth` is tracked for diagnostics;
  whether to enforce a configured max depth at spawn time is deferred.

## Recommendation

Make child workflow orchestration a first-class OrcaCore feature with the
shape above.

Implement incrementally in the order listed, starting with the event-driven
durable engine. Keep `Parallel` unchanged. Do not build `ForEach` on
speculation. Keep the ephemeral engine free of saga awareness via namespace
isolation — saga types live in `OrcaCore.Runtime.Durable.Sagas` and are
referenced only by the durable engine and its consumers.
