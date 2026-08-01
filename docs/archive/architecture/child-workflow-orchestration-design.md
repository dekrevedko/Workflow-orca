# Child Workflow Orchestration Design

Reviewed on April 10, 2026.

## Purpose

This document proposes a first-class OrcaCore feature for orchestrating:

- one child workflow
- many child workflows created dynamically at runtime

The goal is to fully cover cases such as:

- runtime batch size known only during execution
- split and join all children before parent continues
- split and let each child continue independently
- per-item workflow execution
- per-batch workflow execution
- distributed orchestration across service bus and external services

This design is intended as a first-class orchestration feature, not a business-step workaround.

## Motivation

There is a recurring class of workflow problems:

- parent workflow discovers work items at runtime
- work items may be individual ids or runtime-created batches
- each work item may require multiple steps, waits, retries, status messages, or compensation
- the parent needs explicit continuation policy:
  - wait for all
  - wait for any
  - continue each independently
  - fire and forget

Examples:

- e-store order item processing
- price refresh batches where batch size is known only at runtime
- service-bus orchestration across many services
- fanout/fanin batch coordination

Static `Parallel` is not sufficient because:

- branch count is fixed at definition time
- runtime-created batches/items are not representable as static branches
- item/batch lifecycle needs explicit tracking and inspection

## Proposed Feature

Add a first-class child workflow orchestration capability with two public concepts:

- `RunChild`
- `RunChildren`

These are orchestration primitives, not business steps.

## Short Model

Parent workflow:

- selects input items
- optionally partitions them into batches
- starts child workflow instance(s)
- tracks their lifecycle
- applies join/failure policy
- optionally aggregates results

Child workflow:

- is a separate workflow definition
- owns its own logic
- may have multiple steps
- may wait for events
- may publish status
- may succeed or fail independently

## High-Level API

### One child

```csharp
builder.RunChild(
    input: s => new ReserveOrderInput(s.OrderId),
    child: c => c.Workflow<ReserveOrderWorkflow>(),
    policy: ChildJoinPolicy.Wait);
```

### Many children from runtime items

```csharp
builder.RunChildren(
    items: s => s.OrderItemIds,
    child: c => c.Workflow<ReserveOrderItemWorkflow>(),
    policy: ChildJoinPolicy.WhenAll);
```

### Many children from runtime batches

```csharp
builder.RunChildren(
    items: s => s.ListingIds,
    partition: p => p.Batch(sizeSelector: s => s.BatchSize),
    child: c => c.Workflow<UpdatePriceBatchWorkflow>(),
    policy: ChildJoinPolicy.WhenAll);
```

### Independent continuation

```csharp
builder.RunChildren(
    items: s => s.ListingIds,
    child: c => c.Workflow<ProcessListingWorkflow>(),
    policy: ChildJoinPolicy.ContinueEach);
```

## Parent Policies

### Join policy

```csharp
public enum ChildJoinPolicy
{
    Wait,
    WhenAll,
    WhenAny,
    ContinueEach,
    FireAndForget
}
```

Meaning:

- `Wait`
  - for one child
  - parent waits for child completion

- `WhenAll`
  - for many children
  - parent continues only after all children complete

- `WhenAny`
  - parent continues after first successful child
  - remaining children are cancelled or ignored according to explicit policy

- `ContinueEach`
  - each child continues independently
  - parent does not perform one aggregate join continuation

- `FireAndForget`
  - parent does not wait
  - children are launched and tracked only operationally or optionally not tracked at all

### Failure policy

```csharp
public enum ChildFailurePolicy
{
    FailFast,
    WaitAllThenFail,
    ContinueWithPartialResults,
    RetryFailedChildren,
    CompensateCompletedChildren
}
```

Meaning:

- `FailFast`
  - first failure fails parent or cancels remaining children according to join semantics

- `WaitAllThenFail`
  - allow all children to complete
  - fail parent afterwards if any child failed

- `ContinueWithPartialResults`
  - continue parent even if some children failed
  - expose partial completion summary in runtime state/results

- `RetryFailedChildren`
  - runtime policy may reschedule failed children

- `CompensateCompletedChildren`
  - for saga-oriented cases
  - child successes become compensation candidates

## Partitioning

Partitioning is runtime-aware.

Built-in partitioners:

```csharp
partition.Item()
partition.Batch(10)
partition.Batch(sizeSelector: s => s.RuntimeBatchSize)
partition.Custom((state, items) => ...)
```

This solves the important runtime-only batch size case directly.

Example:

```csharp
builder.RunChildren(
    items: s => s.ListingIds,
    partition: p => p.Batch(sizeSelector: s => s.PriceServiceMaxBatchSize),
    child: c => c.Workflow<UpdatePriceBatchWorkflow>(),
    policy: ChildJoinPolicy.WhenAll);
```

## Runtime State Model

Child orchestration must be engine-owned and inspectable.

### Parent runtime state

```csharp
public sealed record ChildWorkflowGroup(
    string GroupId,
    string ParentInstanceId,
    string NodePath,
    ChildJoinPolicy JoinPolicy,
    ChildFailurePolicy FailurePolicy,
    ChildGroupStatus Status,
    int TotalChildren,
    int CompletedChildren,
    int FailedChildren,
    IReadOnlyDictionary<string, ChildWorkflowRef> Children);
```

### Child runtime reference

```csharp
public sealed record ChildWorkflowRef(
    string ChildInstanceId,
    int Index,
    string ChildDefinitionId,
    string ChildDefinitionVersion,
    object Input,
    ChildWorkflowStatus Status,
    object? Result,
    string? Error);
```

### Status enums

```csharp
public enum ChildGroupStatus
{
    Scheduling,
    Waiting,
    Completed,
    Failed,
    Cancelled
}

public enum ChildWorkflowStatus
{
    Pending,
    Started,
    Running,
    Waiting,
    Completed,
    Failed,
    Cancelled
}
```

This allows:

- group inspection
- child inspection
- join progress
- status publishing
- failure analysis

## Public Management Surface

Add management/query operations such as:

```csharp
engine.Instance(id).GetChildGroupsAsync()
engine.Instance(id).GetChildGroupAsync(groupId)
engine.Instance(id).GetChildWorkflowsAsync(groupId)
engine.Instance(id).GetChildWorkflowAsync(childInstanceId)
```

This should be available at least in durable mode.

## Event-Driven Durable Semantics

This feature fits especially well in the event-driven engine.

### Parent events

Examples:

- `ChildGroupCreated`
- `ChildScheduled`
- `ChildStarted`
- `ChildCompleted`
- `ChildFailed`
- `ChildGroupCompleted`
- `ChildGroupFailed`
- `ChildJoinSatisfied`

### Child workflow lifecycle

Each child workflow is itself a workflow instance with:

- its own event stream
- its own checkpoint
- its own query surface

Parent-child linkage is explicit through:

- `ParentInstanceId`
- `ChildGroupId`
- optional `RootInstanceId`

### Parent continuation

When children complete, parent reacts through committed facts:

- one child completion event updates group state
- join condition is re-evaluated
- parent continuation is triggered when policy is satisfied

This is a natural event-driven orchestration model.

## Snapshot-Based Engine Semantics

This feature can also exist in the current state-based engine, but the design is heavier there.

The engine would need to support:

- child group runtime state
- child instance registry or references
- parent continuation waiting on child completion
- runtime-created group state and tracking

This is still viable, but:

- durability is weaker
- audit/history is less natural
- distributed child workflows fit less naturally

Recommendation:

- implement this feature first in the event-driven durable engine
- backport a simpler version to the state engine only if a strong quick-engine scenario appears

## Usage Patterns

### Pattern 1: Split and join all

Example:

- parent gets 100 ids
- runtime batch size is 10
- 10 child workflows are created
- parent waits for all
- parent aggregates final result

### Pattern 2: Split and continue independently

Example:

- parent gets 100 ids
- one child workflow per id
- each child runs the rest of the workflow independently
- parent either completes immediately or only tracks group creation

### Pattern 3: Distributed service-bus orchestration

Example:

- child workflow publishes command
- child waits for event response
- child updates status
- child completes or fails
- parent uses child completion events for join logic

### Pattern 4: Saga fanout

Example:

- parent starts many child reservation workflows
- if later stage fails
- completed children become compensation targets

## Why Child Workflow Is Better Than Only Dynamic `ForEach`

Advantages:

- child logic is reusable
- child has stable identity
- child can have many steps
- child can wait, retry, fail, compensate
- child can publish its own progress
- parent-child relationship is operationally visible

This is especially strong for:

- distributed workflows
- service-bus flows
- long-running operations
- saga-like use cases

## Why Lightweight `ForEach` May Still Be Useful Later

There is still room for a lighter inline primitive later:

- repeated in-memory processing
- no child identity needed
- no independent child lifecycle needed

So the likely long-term model is:

- `ForEach` for lightweight inline fanout
- `RunChildren` for heavyweight orchestration fanout

But if only one is implemented first, `RunChildren` is the better durable/event-driven choice.

## Example Mappings

### E-store order process

Parent:

- `RunChildren(items: OrderItemIds, child: ReserveOrderItemWorkflow, policy: WhenAll)`
- then capture payment
- on later failure, compensate completed children

### Price refresh

Parent:

- collect listing ids
- dedup
- `RunChildren(partition: Batch(sizeSelector: s => s.BatchSize), child: UpdatePriceBatchWorkflow, policy: WhenAll)`
- publish summary or downstream event

Child:

- call `Prices`
- map returned prices
- optionally publish batch status

### Multi-step service-bus orchestration

Parent:

- start many child workflows
- each child performs one external conversation
- parent joins or continues according to policy

### Complex in-memory logic

This case may not need child workflows at all.

Use child workflows only if:

- subflow reuse matters
- subflow isolation matters
- independent lifecycle/inspection matters

## Acceptance Criteria To Add

### CW-AT-001: Parent waits for all child workflows

Given a parent workflow that creates child workflows from runtime input
When `ChildJoinPolicy.WhenAll` is used
Then the parent continues only after all children complete

### CW-AT-002: Runtime batch size controls child count

Given runtime input of 23 ids
And runtime batch size 10
When the parent partitions input into child workflows
Then 3 child workflows are created

### CW-AT-003: Child workflows can continue independently

Given a parent workflow using `ChildJoinPolicy.ContinueEach`
When children are created
Then each child continues independently
And the parent does not wait for one aggregate join continuation

### CW-AT-004: Parent survives restart and still joins children correctly

Given a durable parent waiting on child completions
When the host restarts
Then the parent still joins correctly from durable child-group state

### CW-AT-005: Child failure policy is explicit

Given child workflows where one fails
When `ChildFailurePolicy.WaitAllThenFail` is used
Then the parent waits for all children
And then fails predictably

## Recommended Implementation Order

1. define public API and runtime state model
2. implement `RunChild`
3. implement `RunChildren` with:
   - `Item`
   - `Batch`
   - `WhenAll`
4. add inspection APIs for child groups and child refs
5. add failure policies
6. add `ContinueEach`
7. add saga compensation interaction

## Recommendation

Make child workflow orchestration a first-class OrcaCore feature.

If implemented incrementally:

- first target the event-driven durable engine
- use it as the primary answer for dynamic distributed fanout and join
- optionally add a lighter inline `ForEach` later for local/in-memory repetition
