# Child Workflow And Lightweight ForEach Design (v3)

Drafted on April 10, 2026.

This document supersedes `child-workflow-orchestration-design-v2.md`.

The main change from v2 is a deliberate split:

- **Ephemeral engine** gets a lightweight, in-process, resultless `ForEach`
  primitive.
- **Durable event-driven engine** gets heavyweight `RunChild` /
  `RunChildren` orchestration with separate child workflow instances.

This is not a symmetry-driven design. The two engines solve different
problems and should not carry the same orchestration weight by default.

## Decision Summary

The orchestration surface is split by engine and intent:

| Feature | Ephemeral Engine | Durable Event-Driven Engine |
|---|---|---|
| `Parallel` | yes | optional parity only |
| lightweight `ForEach` | yes | no |
| `RunChild` / `RunChildren` | no | yes |
| child lineage tree | no | yes |
| child workflow instances | no | yes |
| durable outbox-driven spawning | no | yes |
| compensation / sagas | no | yes, via `Durable.Sagas` |

Rationale:

- the ephemeral engine should stay quick, local, and structurally simple
- the durable engine is the right place for cross-instance orchestration,
  lineage, outbox-driven spawning, and compensation
- forcing one primitive to serve both engines would blur boundaries and create
  unnecessary maintenance cost

## Design Goals

The combined design must cover:

- runtime batch size known only during execution
- split-and-join processing
- bounded concurrency over runtime-discovered work
- distributed orchestration over service bus / external services
- explicit join and failure semantics
- durable compensation for completed child workflows after later-stage failure

The design must **not** imply:

- feature parity between engines
- fake function-call semantics between parent and child workflows
- saga awareness in the ephemeral engine

## Engine Split

## Ephemeral Engine: Lightweight `ForEach`

### Purpose

`ForEach` is the data-driven fanout primitive for the ephemeral engine.

It handles:

- runtime item discovery
- optional batching
- bounded in-process concurrency
- join/failure behavior inside one workflow instance

It does **not** create child workflow instances.

### Core Semantics

- runs inside the parent workflow instance
- no child instance ids
- no child lineage tree
- no compensation
- no durable outbox
- no cross-instance orchestration
- resultless by default

The parent continues based on item/batch completion, not child workflow
completion.

### Public Shape

```csharp
builder.ForEach(
    items: s => s.ListingIds,
    partition: p => p.Batch(sizeSelector: s => s.BatchSize),
    body: f => f.Then<ProcessBatchStep>(),
    join: ForEachJoinPolicy.WhenAll,
    failure: ForEachFailurePolicy.WaitAllThenFail,
    maxConcurrency: 8);
```

### Join And Failure Policies

```csharp
public enum ForEachJoinPolicy
{
    WhenAll,
    WhenAny
}

public enum ForEachResidualPolicy
{
    CancelRemaining,
    LetRemainingComplete
}

public enum ForEachFailurePolicy
{
    FailFast,
    WaitAllThenFail,
    ContinueWithPartialFailures
}
```

`WhenAny` cancellation is cooperative and in-process only.

### Runtime Model

```csharp
public sealed record ForEachGroup(
    string                          GroupId,
    string                          NodePath,
    ForEachJoinPolicy               JoinPolicy,
    ForEachFailurePolicy            FailurePolicy,
    ForEachResidualPolicy?          ResidualPolicy,
    int?                            MaxConcurrency,
    ForEachGroupStatus              Status,
    int                             TotalItems,
    int                             NextDispatchIndex,
    int                             ActiveItems,
    int                             CompletedItems,
    int                             FailedItems,
    int                             CancelledItems,
    IReadOnlyList<ForEachItemRef>   Items);

public sealed record ForEachItemRef(
    int                 Index,
    ForEachItemStatus   Status,
    string?             Error,
    DateTimeOffset?     StartedAt,
    DateTimeOffset?     CompletedAt);
```

This is parent-owned state. There are no child workflow refs.

### When To Use

Use lightweight `ForEach` when:

- work is local and in-process
- item count is known only at runtime
- runtime batching is needed
- bounded concurrency is useful
- shared parent state or shared local resources matter
- child identity is not a business concept

Examples:

- complex in-memory DB enrichment over runtime-discovered ids
- local API batching and aggregation
- CPU or lightweight I/O work inside one host

### When Not To Use

Do not use `ForEach` for:

- service-bus conversations per item
- long waits
- cross-instance orchestration
- child compensation
- durable lineage and descendant queries

Those cases belong to `RunChild` / `RunChildren`.

## Durable Event-Driven Engine: `RunChild` / `RunChildren`

### Purpose

`RunChild` and `RunChildren` are the heavyweight orchestration primitives for
the durable engine.

They handle:

- one child workflow
- many child workflows created dynamically from runtime items or batches
- durable child lifecycle tracking
- lineage and tree queries
- durable outbox-driven spawn
- durable throttling
- exactly-once parent resume
- explicit compensation via `Durable.Sagas`

### Core Semantics

- each child is a separate workflow instance
- parent and child are linked by lineage metadata
- children are resultless by default
- parent observes child completion/failure/cancellation, not a return value
- compensation is explicit and durable-only

If a future scenario requires typed child outcomes, that must be introduced as
a separate step type with its own typing and persistence contract.

### Public Shape

```csharp
builder.RunChild(
    input: s => new ReserveOrderInput(s.OrderId),
    child: c => c.Workflow<ReserveOrderWorkflow>(),
    join: ChildJoinPolicy.Wait);
```

```csharp
builder.RunChildren(
    items: s => s.OrderItemIds,
    partition: p => p.Item(),
    child: c => c.Workflow<ReserveOrderItemWorkflow>(),
    join: ChildJoinPolicy.WhenAll,
    failure: ChildFailurePolicy.WaitAllThenFail);
```

```csharp
builder.RunChildren(
    items: s => s.ListingIds,
    partition: p => p.Batch(sizeSelector: s => s.PriceServiceMaxBatchSize),
    child: c => c.Workflow<UpdatePriceBatchWorkflow>(),
    join: ChildJoinPolicy.WhenAll,
    failure: ChildFailurePolicy.ContinueWithPartialResults,
    maxConcurrency: 4);
```

### Join And Failure Policies

```csharp
public enum ChildJoinPolicy
{
    Wait,
    WhenAll,
    WhenAny,
    ContinueEach,
    FireAndForget
}

public enum WhenAnyResidualPolicy
{
    CancelRemaining,
    LetRemainingComplete,
    DetachRemaining
}

public enum ChildFailurePolicy
{
    FailFast,
    WaitAllThenFail,
    ContinueWithPartialResults
}
```

Group-level retry is intentionally out of scope. Retries belong inside child
workflow steps or infrastructure adapters.

### Runtime Model

```csharp
public sealed record ChildWorkflowGroup(
    string                             GroupId,
    string                             ParentInstanceId,
    string                             RootInstanceId,
    string                             NodePath,
    string                             ChildDefinitionId,
    string                             ChildDefinitionVersion,
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
    string                   ChildInstanceId,
    int                      Index,
    string                   ChildDefinitionId,
    string                   ChildDefinitionVersion,
    object                   Input,
    ChildWorkflowStatus      Status,
    string?                  Error,
    DateTimeOffset?          StartedAt,
    DateTimeOffset?          CompletedAt);
```

### Durable Mechanism

`RunChildren` lowers to durable records and outbox operations:

1. materialize items
2. partition deterministically
3. build the group with all refs in `Pending`
4. register the parent synthetic wait for non-fire-and-forget joins
5. enqueue the initial start window according to `MaxConcurrency`
6. advance the parent to `Waiting` or onward for `FireAndForget`

### Throttling

`MaxConcurrency` is enforced by durable scheduler state:

- `NextDispatchIndex`
- `ActiveChildren`

As children reach terminal states:

- `ActiveChildren` is decremented
- new start commands are enqueued until the window is full again

Restart reconstructs this from the persisted group state. `MaxConcurrency`
is an execution rule, not an observational hint.

### Exactly-Once Parent Resume

The barrier is driven by an explicit durable resume token:

- winner records `ResumeTokenId`
- for `WhenAny + CancelRemaining`, residual cancellation intent is recorded
  before resume is emitted
- parent consumes resume token idempotently
- restart replays the same token if needed and never mints a second one

This avoids relying on whether the parent's synthetic wait still exists after
a crash.

### Unified Outbox

One physical outbox is shared for durable post-commit dispatch:

- `ExternalMessage`
- `ChildStartCommand`
- `StatusMessage`

Saga compensation extends the outbox in `Durable.Sagas` through durable-only
registration. Compensation must not leak into core or ephemeral APIs.

### Compensation

Compensation is durable-only and explicit:

- child completion does not imply compensation
- cancellation does not imply compensation
- parent or saga logic explicitly invokes `CompensateAsync(groupId, spec)`

Compensation remains outside the ephemeral engine entirely.

## Shared Concepts

Some concepts stay structurally shared even though the features differ:

- partitioners
- structured retry rule
- cooperative cancellation rule
- deterministic item ordering for restart-sensitive fanout
- explicit join/failure policy axes

## Partitioner Contract

Both features rely on deterministic partitioning:

```csharp
public interface IPartitioner<TState, TItem>
{
    IEnumerable<IReadOnlyList<TItem>> Partition(TState state, IEnumerable<TItem> items);
}
```

Built-ins:

```csharp
Partitioners.Item<TState, TItem>()
Partitioners.Batch<TState, TItem>(int size)
Partitioners.Batch<TState, TItem>(Func<TState, int> sizeSelector)
Partitioners.Custom<TState, TItem>(...)
```

Output ordering must be deterministic and stable for the same input.

## Structured Retry Rule

Where retry exists, it must be structured:

- `MaxAttempts`
- `Backoff`
- optional `ShouldRetry`
- explicit terminal condition

This rule applies to step-level retry and infrastructure retry.

It does **not** imply a group-level retry policy for either `ForEach` or
`RunChildren`.

## Resultless By Design

Both features are intentionally resultless.

Parent follow-up happens through:

- completion/failure/cancellation status
- direct child state inspection (`RunChildren`)
- shared projections
- status or domain messages

If OrcaCore later needs parent-visible typed child outcomes, that must be a
separate feature with a separate design.

## Example Mapping

### Example 1: Complex in-memory local logic

Use ephemeral `ForEach`.

- local DB requests
- bounded concurrency
- no child identity
- no durability requirement

### Example 2: Price refresh with service boundary

Use durable `RunChildren` if:

- batching is runtime-determined
- each batch conversation is durable
- publish/restart semantics matter

Use ephemeral `ForEach` only if the whole flow is local, rerunnable, and does
not require durable child identity.

### Example 3: Service-bus orchestration

Use durable `RunChildren`.

- each child can own one external conversation
- parent joins or continues independently
- lineage, outbox, and restart semantics matter

### Example 4: Saga fanout

Use durable `RunChildren` plus `Durable.Sagas`.

## Acceptance Criteria

### Ephemeral `ForEach`

- **FE-AT-001** runtime batch size of 10 over 23 items creates exactly 3 in-parent work items
- **FE-AT-002** `WhenAll` continues parent only after all work items complete
- **FE-AT-003** `maxConcurrency` limits in-process active work items
- **FE-AT-004** `WaitAllThenFail` waits for all work items, then fails parent
- **FE-AT-005** `WhenAny + CancelRemaining` records cancellation intent before parent continuation

### Durable `RunChildren`

- **CW-AT-001** parent with `WhenAll` continues only after all children complete
- **CW-AT-002** deterministic child ids survive restart without duplication
- **CW-AT-003** item snapshot stability preserves partitioning across restart
- **CW-AT-004** `maxConcurrency` is honored across restart
- **CW-AT-005** barrier fires exactly once under concurrent child completions
- **CW-AT-006** recorded resume token is reused on restart; no second token is minted
- **CW-AT-007** `WhenAny + CancelRemaining` durably records residual cancellation intent before parent resume
- **CW-AT-008** one physical outbox can carry child-start and external-message records concurrently
- **CW-AT-009** lineage via `RootInstanceId` and `ParentInstanceId` works across nested child groups

### Durable Sagas

- **CW-AT-Saga-001** explicit `CompensateAsync(groupId, spec)` spawns one compensation per completed child
- **CW-AT-Saga-002** compensation is never triggered implicitly by cancellation
- **CW-AT-Saga-003** compensation payload obeys the durable serializer contract
- **CW-AT-Saga-004** repeated compensation requests are idempotent

## Recommended Implementation Order

1. add lightweight `ForEach` design surface to the ephemeral engine
2. implement ephemeral `ForEach` with `Item`, `Batch`, `WhenAll`, and bounded concurrency
3. implement durable unified outbox infrastructure
4. implement durable `RunChild` with `Wait`
5. implement durable `RunChildren` with `Item`, `WhenAll`, and durable scheduler state
6. add `Batch(size)` / `Batch(sizeSelector)` for durable fanout
7. add `WaitAllThenFail` and `ContinueWithPartialResults`
8. add `WhenAny` and residual policies with explicit cancellation ordering
9. add `ContinueEach` and `FireAndForget`
10. add tree queries and inspection surfaces
11. add `Durable.Sagas` compensation APIs

## Deferred Decisions

- typed child outcomes
- lightweight durable `ForEach`
- physical outbox partitioning by kind
- multi-host child scheduling
- global max-depth enforcement

## Recommendation

Adopt the split explicitly:

- **ephemeral engine**: lightweight `ForEach`
- **durable engine**: `RunChild` / `RunChildren`

Do not try to keep one orchestration primitive across both engines. The split
is cleaner, more honest, and easier to maintain.
