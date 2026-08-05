# OrcaCore Ephemeral Engine Developer Guide

> **Pre-refactor implementation guide:** examples below describe the currently checked-in
> engine and may contain provisional `WhenFirst`, Saga, factory, or raw pool-key APIs. They are
> not the approved first-release contract. Task 9.5 of the active developer-surface change will
> rewrite this guide after product source reaches
> [spec 17](specs/17-selected-mode-capability-matrix.md); do not copy these signatures into new
> compile guards.

> **Approved v1 corrections:** exact declarations are in
> [`17-public-authoring-contract.cs`](specs/17-public-authoring-contract.cs). V1 uses staged typed
> `Init`/`End`, `StepContext<TState>.State`/`ReplaceState`, and the fixed certified
> `orcacore-json-v1` codec. It retains fixed root `Parallel`, supports finite root `ForEach`
> (including a valid empty snapshot), and offers `WhenAll` plus success/failure-only
> `WhenAllOutcomes`; an ancestor cancellation/termination/deadline suppresses merge. V1 concurrency
> is bulk-synchronous fork-join: flattening and sequential staging are the two sanctioned root-only
> encodings, subject to their explicit budget, dependency, and observation preconditions. Builder
> handles are phase- and scope-bound, and a root terminal freezes the definition snapshot. Retry is
> `.WithRetry(maxAttempts, fixedDelay)` after a step, step attempts use `.WithStepTimeout`, and
> the root uses `.CompleteWithin`. Ephemeral transient governance uses strong
> `TransientPoolName` plus host-owned path tokens, not raw `WithPoolKey`. Caller-created
> string-backed strong values use private constructors plus the sole public `Create(string)`
> factory; constructor/factory-shaped examples later in this pre-refactor guide are not the approved
> surface. The v1 host surface has
> no independent advancement/general-body ceiling or fail-fast/capacity-wait-timeout policy: it uses
> `MaxConcurrentExecutionPathsPerInstance`, exact-type `StepThrottles`, and at most one transient
> pool decorator per step. Event dedup is per target
> instance/event ID and correlation routing admits one active wait per
> `(DefinitionId, EventName, CorrelationId)`; definition fanout is deferred. V1 management exposes
> typed snapshot/state/output, cancellation request, and termination only. `WhenFirst`, Saga,
> public child/external-job nodes, nested `Parallel`/`While`/`ForEach`, definition retry, public
> pause/resume/archive/purge, `WaitLong`, and author `Yield` do not ship. Hosting uses
> `AddOrcaCoreEphemeralEngine(EphemeralEngineHostOptions)`; catch-all `AddOrcaCore` and a separate
> hosted-service toggle do not ship.

This guide explains how to build and run workflows with the current root implementation
ephemeral engine.

The ephemeral engine is the in-process OrcaCore runtime. It keeps definitions,
instances, waits, timers, lifecycle metadata, and saga runtime state in memory.
It is useful for local orchestration, tests, short-lived worker workflows, demos,
and application code that does not need restart recovery. It is not a durability
layer.

## What Ephemeral Mode Guarantees

Use ephemeral mode when these constraints are acceptable:

- Runtime state is process-local and is lost when the process exits.
- Events, waits, timers, lifecycle events, management state, and saga audit data
  are inspectable only while the owning process is alive.
- Each workflow instance is advanced through a serialized execution lane, so two
  concurrent events for the same instance cannot commit two conflicting
  continuations.
- Event routing is in-memory and supports instance-targeted delivery,
  correlation-targeted delivery, and definition-scoped fanout.
- Timers are transient. The host must call `FireDueTimersAsync` to fire due
  delay and timeout work.
- Durable-only capabilities such as restart recovery, long waits, durable
  history, archive, purge, pause, resume, durable child workflow execution, and
  durable outbox dispatch are outside this engine.

Use durable mode instead when an instance must survive process restart, multiple
nodes must coordinate the same workflow population, or operators need durable
history and recovery actions.

## Project Layout

The current implementation lives at the repository root.

| Path | Purpose |
|------|---------|
| `src/OrcaCore.Abstractions` | Public contracts such as IDs, `IStep<TState>`, `StepContext<TState>`, `StepResult`, `EventEnvelope`, and snapshots. |
| `src/OrcaCore.Core` | Definition builders and immutable definition model. |
| `src/OrcaCore.Engine.Ephemeral` | In-process workflow engine, execution loop, timers, governance, and management surface. |
| `tests/OrcaCore.Engine.Ephemeral.Tests` | Focused engine behavior tests. |
| `tests/OrcaCore.Acceptance.Tests` | Scenario and acceptance coverage shared across the product scope. |

The projects target `net10.0` and use the SDK pinned by `global.json`.

```powershell
dotnet build OrcaCore.slnx
dotnet test tests/OrcaCore.Engine.Ephemeral.Tests/OrcaCore.Engine.Ephemeral.Tests.csproj
dotnet test tests/OrcaCore.Acceptance.Tests/OrcaCore.Acceptance.Tests.csproj --filter "AC=AC-001|AC=AC-101|AC=AC-501"
```

## Referencing The Engine

Application code needs the abstractions, core builder, and ephemeral engine
projects or packages:

```xml
<ItemGroup>
  <ProjectReference Include="path/to/src/OrcaCore.Abstractions/OrcaCore.csproj" />
  <ProjectReference Include="path/to/src/OrcaCore.Core/OrcaCore.Core.csproj" />
  <ProjectReference Include="path/to/src/OrcaCore.Engine.Ephemeral/OrcaCore.Engine.Ephemeral.csproj" />
</ItemGroup>
```

Typical using directives:

```csharp
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Ephemeral;
```

## Minimal Workflow

A regular workflow has three pieces:

- A mutable state object.
- One or more `IStep<TState>` implementations.
- A `WorkflowDefinition<TState>` built with the selected ephemeral builder.

```csharp
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;

var engine = new EphemeralWorkflowEngine();

var definition = Workflow.Ephemeral<OrderState>(DefinitionId.New(), DefinitionVersion.Initial)
    .Init<OrderInput>(input => new OrderState
    {
        OrderId = input.OrderId
    })
    .Then(() => new ReserveInventoryStep("sku-123", quantity: 2))
    .End("Reserved")
    .Build();

engine.RegisterDefinition(definition);

var snapshot = await engine.AwaitCompletionAsync<OrderInput, OrderState>(
    definition.DefinitionId,
    new OrderInput("order-001"),
    CancellationToken.None);

Console.WriteLine(snapshot.Status);         // Completed
Console.WriteLine(snapshot.EndOutcomeName); // Reserved

var state = engine.Management.Instance(snapshot.InstanceId).GetState<OrderState>();
Console.WriteLine(string.Join(", ", state.Log));

public sealed record OrderInput(string OrderId);

public sealed class OrderState
{
    public string OrderId { get; set; } = string.Empty;

    public decimal Total { get; set; }

    public int RetryCount { get; set; }

    public bool Ready { get; set; }

    public List<string> Log { get; set; } = [];
}

public sealed class ReserveInventoryStep(string sku, int quantity) : IStep<OrderState>
{
    public ValueTask<StepResult> ExecuteAsync(
        StepContext<OrderState> context,
        CancellationToken cancellationToken)
    {
        context.State.Log.Add($"Reserved {quantity} of {sku} for {context.State.OrderId}");
        return ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }
}
```

`AwaitCompletionAsync` is a convenience API for workflows expected to finish
synchronously. If the workflow enters `Waiting`, it throws a lifecycle exception
because the workflow did not reach a terminal state inline. Use `StartAsync` for
workflows that can wait for events or timers.

Later snippets use illustrative step names such as `SendReminderStep`. Implement
those steps with the same `IStep<TState>` shape shown above.

## State And Steps

`StepContext<TState>` provides the only intended mutation channel for business
state:

```csharp
public ValueTask<StepResult> ExecuteAsync(
    StepContext<MyState> context,
    CancellationToken cancellationToken)
{
    context.State.Attempts++;
    return ValueTask.FromResult<StepResult>(new StepResult.Completed());
}
```

The context includes:

- `State`: the mutable workflow state object for this instance.
- `ResumedEvent`: the event that resumed the instance after a wait, available to
  the first resumed step only.
- `TimeProvider`: deterministic time for delay, timeout, and testability.

`StepResult` is a closed set of control intents:

| Result | Meaning |
|--------|---------|
| `new StepResult.Completed()` | The step is done and the engine should advance. |
| `new StepResult.Failed(error)` | The instance should fail with an OrcaCore error. |
| `new StepResult.WaitForEvent(name, correlation)` | The step itself creates a wait. Most workflows should prefer builder-level `.Wait(...)` for readability. |
| `new StepResult.Yield()` | Commit current state progress and cooperatively reschedule the same step. |

Expected business failures should use `StepResult.Failed`. Unexpected exceptions
are also caught by the engine and recorded as failed workflow state, except for
operation cancellation and unsupported runtime shapes.

## Building Definitions

The builder creates immutable definition versions.

```csharp
var definitionId = DefinitionId.New();
var version = DefinitionVersion.Initial;

var definition = Workflow.Ephemeral<OrderState>(definitionId, version)
    .Init<OrderInput>(input => new OrderState { OrderId = input.OrderId })
    .Then<ValidateOrderStep>()
    .Then(() => new ReserveInventoryStep("sku-123", 2))
    .End("Accepted")
    .Build();
```

Step authoring options:

- `Then<TStep>()` for parameterless steps with a public constructor.
- `Then(IStep<TState>)` for an explicitly configured step instance.
- `Then(Func<IStep<TState>>)` for a deterministic factory.

Prefer factories or explicit instances for configured steps. The builder does
not infer constructor arguments.

Use `TryBuild` when a tool or UI should present all definition problems at
once instead of throwing:

```csharp
var validation = Workflow.Ephemeral<OrderState>(DefinitionId.New(), DefinitionVersion.Initial)
    .End()
    .TryBuild();

if (!validation.IsValid)
{
    foreach (var error in validation.Errors)
    {
        Console.WriteLine($"{error.Code}: {error.Message} at {error.Path}");
    }
}
```

Validation catches missing `Init`, missing reachable `End`, null delegates,
empty branch bodies, non-positive delays, non-positive timeouts, non-positive
fanout concurrency, and invalid retry policies.

## Lifecycle And Snapshots

Engine APIs return `WorkflowInstanceSnapshot`. A snapshot is immutable metadata,
not a live instance reference.

Important fields:

- `InstanceId`: generated instance identity.
- `DefinitionId` and `DefinitionVersion`: the definition bound at start.
- `Status`: current lifecycle state.
- `CreatedAt`, `UpdatedAt`, `CurrentStatusEnteredAt`, and `LastActiveAt`.
- `ActiveWaits`: current wait metadata.
- `ActiveStep`: currently observed running step, if any.
- `LifecycleEvents`: in-process lifecycle event snapshots.
- `CompositionOutcomes` and `ForEachGroups`: branch and fanout inspection data.
- `ErrorSummary`: failure detail when failed.
- `EndOutcomeName`: optional name from `.End("Name")`.

Terminal statuses are:

- `Completed`
- `Failed`
- `Cancelled`
- `Terminated`
- `Compensated`
- `CompensationFailed`

`Paused` is a shared contract value for durable mode. The ephemeral engine does
not produce it.

## Waiting For Events

Use `.Wait(eventName, correlationSelector)` to suspend until a matching
`EventEnvelope` arrives.

```csharp
var correlation = new CorrelationId("order-001");

var definition = Workflow.Ephemeral<OrderState>(DefinitionId.New(), DefinitionVersion.Initial)
    .Init<OrderInput>(input => new OrderState { OrderId = input.OrderId })
    .Wait("PaymentApproved", state => new CorrelationId(state.OrderId))
    .Then(() => new CapturePaymentStep())
    .End("Paid")
    .Build();

engine.RegisterDefinition(definition);

var waiting = await engine.StartAsync<OrderInput, OrderState>(
    definition.DefinitionId,
    new OrderInput("order-001"),
    CancellationToken.None);

var resumed = await engine.RaiseEventAsync<OrderState>(
    waiting.InstanceId,
    new EventEnvelope
    {
        EventId = EventId.New(),
        EventName = "PaymentApproved",
        CorrelationId = correlation,
        Payload = new PaymentApproved("txn-123"),
        OccurredAt = DateTimeOffset.UtcNow
    },
    CancellationToken.None);

public sealed record PaymentApproved(string TransactionId);

public sealed class CapturePaymentStep : IStep<OrderState>
{
    public ValueTask<StepResult> ExecuteAsync(
        StepContext<OrderState> context,
        CancellationToken cancellationToken)
    {
        if (context.ResumedEvent?.Payload is PaymentApproved payment)
        {
            context.State.Log.Add($"Payment {payment.TransactionId}");
        }

        return ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }
}
```

The event match key is `EventName` plus `CorrelationId`. `EventId` is used for
deduplication.

## Event Delivery Modes

The engine exposes three event delivery modes.

### Instance Targeted

Use this when the caller already knows the `InstanceId`.

```csharp
var snapshot = await engine.RaiseEventAsync<OrderState>(
    instanceId,
    envelope,
    cancellationToken);
```

This is the most explicit and is the right default for command handlers that
store instance IDs in application data.

### Correlation Targeted

Use this when exactly one active wait should exist for an event name and
correlation.

```csharp
var snapshot = await engine.RaiseEventByCorrelationAsync<OrderState>(
    envelope,
    cancellationToken);
```

The engine throws `WorkflowRoutingException` when no active wait matches, or when
multiple active waits match and delivery would be ambiguous.

### Definition Fanout

Use this when one event should be delivered to every matching active wait for one
definition.

```csharp
IReadOnlyList<WorkflowInstanceSnapshot> snapshots =
    await engine.RaiseEventByDefinitionAsync<OrderState>(
        definition.DefinitionId,
        envelope,
        cancellationToken);
```

Fanout is scoped to a single `DefinitionId`.

## Mailbox, Deduplication, And Loop Waits

The engine keeps a process-local mailbox per instance.

- An event with a non-matching active wait can be buffered and consumed by a
  later matching wait in the same instance.
- A duplicate `EventId` is consumed once.
- If an instance reaches `End` while unclaimed runtime work remains, the engine
  fails the instance instead of silently dropping that work.
- Waits inside loops are isolated by the current iteration state. A stale event
  for a previous iteration does not resume a later iteration.

Use stable correlation values that represent the specific external fact being
waited on. In loops, include the iteration or item identity in the correlation
when stale events must not match future iterations.

## Delays And Wait Timeouts

Use `.Delay(duration)` for a first-class timer node:

```csharp
var definition = Workflow.Ephemeral<OrderState>(DefinitionId.New(), DefinitionVersion.Initial)
    .Init<OrderInput>(input => new OrderState { OrderId = input.OrderId })
    .Delay(TimeSpan.FromMinutes(5))
    .Then(() => new SendReminderStep())
    .End("ReminderSent")
    .Build();

var waiting = await engine.StartAsync<OrderInput, OrderState>(
    definition.DefinitionId,
    new OrderInput("order-001"),
    CancellationToken.None);

// The host decides when to pump due transient timers.
IReadOnlyList<WorkflowInstanceSnapshot> fired =
    await engine.FireDueTimersAsync(CancellationToken.None);
```

Use `.Wait(eventName, correlationSelector, timeout)` to race an event against a
timeout:

```csharp
var definition = Workflow.Ephemeral<OrderState>(DefinitionId.New(), DefinitionVersion.Initial)
    .Init<OrderInput>(input => new OrderState { OrderId = input.OrderId })
    .Wait(
        "PaymentApproved",
        state => new CorrelationId(state.OrderId),
        TimeSpan.FromMinutes(30))
    .Then(() => new RecordPaymentOrTimeoutStep())
    .End()
    .Build();
```

When a wait timeout fires, the resumed step receives `ResumedEvent == null`.
When the external event wins, `ResumedEvent` contains the delivered envelope.

Timers are not durable. If the process exits before `FireDueTimersAsync`, the
timer is gone.

## Control Flow

### If

```csharp
var definition = Workflow.Ephemeral<OrderState>(DefinitionId.New(), DefinitionVersion.Initial)
    .Init<OrderInput>(input => new OrderState { OrderId = input.OrderId })
    .If(
        state => state.Total >= 100,
        then => then.Then(() => new RequireManualReviewStep()),
        otherwise => otherwise.Then(() => new AutoApproveStep()))
    .End()
    .Build();
```

Exactly one branch executes, then the parent sequence continues.

### While

```csharp
var definition = Workflow.Ephemeral<OrderState>(DefinitionId.New(), DefinitionVersion.Initial)
    .Init<OrderInput>(input => new OrderState { OrderId = input.OrderId })
    .While(
        state => state.RetryCount < 3 && !state.Ready,
        body => body
            .Then(() => new PollStatusStep())
            .Delay(TimeSpan.FromSeconds(10)))
    .End()
    .Build();
```

The condition is re-evaluated before each iteration.

## Parallel And WhenFirst

`Parallel` runs isolated branch fibers cooperatively. Branches receive copied private state,
return one typed result, and cannot mutate parent or sibling state. The parent changes only
through the explicit merge after all branches complete.

```csharp
var definition = Workflow.Ephemeral<OrderState>(definitionId, DefinitionVersion.Initial)
    .Init<OrderInput>(input => new OrderState(input.OrderId, []))
    .Parallel<string>(
        branches => branches
            .Branch<ReservationState>(
                "inventory",
                parent => new ReservationState(parent.Value.OrderId),
                branch => branch
                    .Wait("InventoryReserved", state => new CorrelationId(state.OrderId))
                    .Then<CaptureInventoryStep>()
                    .Return(state => state.Value.Result))
            .Branch<ReservationState>(
                "payment",
                parent => new ReservationState(parent.Value.OrderId),
                branch => branch
                    .Wait("PaymentApproved", state => new CorrelationId(state.OrderId))
                    .Then<CapturePaymentStep>()
                    .Return(state => state.Value.Result)),
        (parent, results) => parent.Value with
        {
            Reservations = results.Select(result => result.Value).ToArray()
        })
    .Then(() => new FinalizeOrderStep())
    .End("ReadyToShip")
    .Build();
```

Results reach the merge in authored order. Branch waits are owned by their fiber, and the
continuation after the merge runs once.

`WhenFirst` uses the same isolated-state contract. The first committed terminal branch wins;
authored order breaks a same-transition tie. A failed winner fails the scope without merge,
and every losing descendant is cancelled before parent continuation.

```csharp
var definition = Workflow.Ephemeral<OrderState>(definitionId, DefinitionVersion.Initial)
    .Init<OrderInput>(input => new OrderState(input.OrderId, []))
    .WhenFirst<string>(
        branches => branches
            .Branch<DecisionState>("approved", ProjectDecision, branch => branch
                .Wait("Approved", state => new CorrelationId(state.OrderId))
                .Return(_ => "approved"))
            .Branch<DecisionState>("rejected", ProjectDecision, branch => branch
                .Wait("Rejected", state => new CorrelationId(state.OrderId))
                .Return(_ => "rejected")),
        (parent, winner) => parent.Value with
        {
            Reservations = [.. parent.Value.Reservations, winner.Value]
        })
    .End()
    .Build();
```

Detached residual policies are not part of the API.

## ForEach Fanout

`ForEach` is an ephemeral-only dynamic scope. Each admitted item fiber receives isolated
item state and returns a typed result. `maxConcurrency` limits admitted nonterminal item
fibers; step bodies still run cooperatively one at a time for an instance.

```csharp
var definition = Workflow.Ephemeral<BatchState>(definitionId, DefinitionVersion.Initial)
    .Init<BatchInput>(input => new BatchState(input.Items, []))
    .ForEach<int, BatchItemState, int>(
        parent => parent.Value.Items,
        WorkflowPartitioner<int>.Batch(10),
        item => new BatchItemState(item.Index, item.Items),
        body => body
            .Then<ProcessBatchStep>()
            .Return(item => item.Value.Items.Count),
        ForEachJoinPolicy.WhenAll,
        ForEachFailurePolicy.FailFast,
        maxConcurrency: 2,
        merge: (parent, outcomes) => parent.Value with
        {
            PartitionSizes = outcomes.Select(outcome => outcome.Result).ToArray()
        })
    .Then(() => new MarkBatchCompleteStep())
    .End("Processed")
    .Build();
```

Partitioners:

- `WorkflowPartitioner<T>.Items()` creates one work item per input item.
- `WorkflowPartitioner<T>.Batch(size)` creates fixed-size batches.
- `WorkflowPartitioner<T>.BatchBy(keySelector)` groups by key.
- `WorkflowPartitioner<T>.Custom(func)` delegates partitioning to caller code.

Policies:

- `ForEachJoinPolicy.WhenAll` waits for all work items.
- `ForEachJoinPolicy.WhenAny` selects the first committed terminal item and cancels every
  admitted or pending residual item before continuation.
- `ForEachFailurePolicy.FailFast` fails early.
- `ForEachFailurePolicy.WaitAllThenFail` observes all work before failing.
- `WhenAny` accepts only `FailFast`; incoherent failure-policy combinations fail compilation.

## Yield

`StepResult.Yield` lets a long-running step commit progress and resume
cooperatively without leaving the step.

```csharp
public sealed class ImportPageStep : IStep<ImportState>
{
    public ValueTask<StepResult> ExecuteAsync(
        StepContext<ImportState> context,
        CancellationToken cancellationToken)
    {
        if (context.State.RemainingPages > 0)
        {
            context.State.RemainingPages--;
            context.State.ImportedPages++;
            return ValueTask.FromResult<StepResult>(new StepResult.Yield());
        }

        return ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }
}
```

Use yield when each pass has a meaningful committed progress point. Do not use
it as a replacement for waiting on external events.

## Step Policies And Resource Governance

Policies are decorators on the preceding authored step:

```csharp
var definition = Workflow.Ephemeral<OrderState>(DefinitionId.New(), DefinitionVersion.Initial)
    .Init<OrderInput>(input => new OrderState { OrderId = input.OrderId })
    .Then<ChargePaymentStep>()
    .WithRetry(maxAttempts: 3, fixedDelay: TimeSpan.FromSeconds(1))
    .WithStepTimeout(TimeSpan.FromSeconds(30))
    .WithTransientPool(TransientPoolName.Create("payment-gateway"))
    .End()
    .Build();
```

Policy notes:

- `WithRetry(maxAttempts, fixedDelay)` retries the preceding step when it returns a failed result
  or throws a handled exception. Attempts are bounded.
- `WithStepTimeout(duration)` signals cancellation for the preceding step when it exceeds the duration.
  Steps should honor the supplied cancellation token.
- `WithTransientPool(pool)` gives the preceding ephemeral step exactly one
  host-local, named pool requirement.

Host governance is configured with `EphemeralEngineHostOptions` and the
role-specific registration entry point:

```csharp
var database = TransientPoolName.Create("payment-gateway");

services.AddOrcaCoreEphemeralEngine(
    new EphemeralEngineHostOptions
    {
        StructuredExecution = new StructuredExecutionHostOptions
        {
            MaxConcurrentExecutionPathsPerInstance = 8,
            StepThrottles =
            [
                StepExecutionThrottle.For<ChargePaymentStep>(2)
            ]
        },
        TransientPools =
        [
            TransientPoolDefinition.Create(database, 4)
        ]
    });
```

`MaxConcurrentExecutionPathsPerInstance` counts runnable root, branch, and item
paths for one instance. A path releases its token when it parks on a wait, delay,
resource request, or join. A fan-out parent releases its token before children
are scheduled and reacquires one only for merge or continuation, so a limit of
one does not deadlock a root `Parallel` or `ForEach`. This admission limit changes
timing, not the existing serialized mutation rule for one workflow instance.

A root `ForEach` node's `ForEachOptions.MaxConcurrency`, when present, composes
with the host limit by taking the lower value. Unlike runnable path tokens, the
node-local limit counts every admitted nonterminal item scope, including an item
parked on a wait, delay, or resource request. Pending items therefore are not
promised progress while admitted items remain parked.

`StepExecutionThrottle.For<TStep>(N)` is host-wide and matches only the exact
named type authored by `Then<TStep>()`. It does not match base or assignable
types, and lambda steps have no inferred throttle target. A timed-out or fenced
body loses logical commit authority and its path token, but retains any physical
step-throttle slot until the body actually returns.

An ephemeral step may apply one
`.WithTransientPool(TransientPoolName)` decorator. Lookup is exact and
case-sensitive; all missing pool names are reported during definition
registration before registry mutation. Transient-pool slots coordinate instances
only inside the current host process and reset on restart. Pending owners are
re-admitted under the replacement host's current limits.

V1 deliberately has no host-wide advancement ceiling, no general-body ceiling,
no fail-fast or capacity-wait timeout, and no custom transient-governance SPI.
Engine pumps and other infrastructure dispatch do not consume business
execution-path, exact-step, or transient-pool capacity. Saturation parks the
exact requesting owner until grant or governing cancellation; it is not a
rejection.

Durable mode supports the same per-instance path ceiling and exact named-step
throttles through `DurableEngineHostOptions`, but it does not expose
`WithTransientPool`. Cross-host durable capacity uses `ResourcePoolName` and
`AcquireResources` instead. Durable lease authoring is limited to the durable
root, root-nested `If`/`While` bodies, root-`Parallel` branch bodies, and
root-`ForEach` item bodies when no live ancestor lease exists. Dedicated leased
builders omit fan-out, nested acquisition, and `ContinueAsNew`.

Durable leases are persisted ownership, not host-local throttles. A successful
retry retains `AmbiguousHeld` ownership; an unproven exit transfers ownership to
quarantine before workflow progression. Review and reconciliation use causal
owner/provider evidence and never reclaim capacity merely because time elapsed;
there is no renewal or force-release shortcut.

## Management API

Every engine has a management root:

```csharp
EphemeralManagement management = engine.Management;
```

Common queries:

```csharp
var all = management.All().List();

var waiting = management.All()
    .Where(instance => instance.Status == WorkflowStatus.Waiting)
    .List();

var forDefinition = management.ForDefinition(definition.DefinitionId).List();

var selected = management.Instances([firstInstanceId, secondInstanceId]).List();

var one = management.Instance(instanceId).Get();
var stateCopy = management.Instance(instanceId).GetState<OrderState>();
var waits = management.Instance(instanceId).GetActiveWaits();
var events = management.Instance(instanceId).GetLifecycleEvents();
```

`GetState<TState>()` returns a detached JSON copy. Mutating the returned object
does not mutate the running instance.

Management predicates are intentionally constrained to provider-shaped metadata
expressions. Supported operations include comparisons and `&&` / `||` over
`WorkflowInstanceQueryModel` properties. Method calls, object construction,
indexers, delegate invocation, and conditional expressions are rejected.

Examples:

```csharp
var safe = management.All()
    .Where(instance =>
        instance.Status == WorkflowStatus.Waiting &&
        instance.DefinitionId == definition.DefinitionId)
    .List();

// Rejected: method calls are not part of the management predicate subset.
var rejected = management.All()
    .Where(instance => instance.EndOutcomeName!.StartsWith("Approved"));
```

Statistics:

```csharp
WorkflowStatistics statistics = management.All().Statistics();

foreach (var group in statistics.Groups)
{
    Console.WriteLine($"{group.DefinitionId} v{group.DefinitionVersion}: {group.Status} = {group.Count}");
}
```

Stuck detection:

```csharp
IReadOnlyList<WorkflowInstanceSnapshot> stuck =
    management.All().DetectStuck(TimeSpan.FromMinutes(5));
```

Selection-scoped event delivery:

```csharp
IReadOnlyList<WorkflowInstanceSnapshot> resumed =
    await management.All()
        .Where(instance => instance.Status == WorkflowStatus.Waiting)
        .RaiseEventAsync<OrderState>(envelope, CancellationToken.None);
```

Terminal commands:

```csharp
var cancelled = await management.Instance(instanceId)
    .CancelAsync(CancellationToken.None);

var terminated = await management.Instance(instanceId)
    .TerminateAsync(CancellationToken.None);

var report = await management.All()
    .TerminateAsync(DestructiveCommandSafety.Confirmed, CancellationToken.None);
```

Broad `TerminateAsync` requires `DestructiveCommandSafety.Confirmed`.
`CancelAsync` and `TerminateAsync` affect only non-terminal instances in the
selection. Terminal instances reject illegal lifecycle triggers.

Step-scoped management:

```csharp
var activeStep = management.Instance(instanceId)
    .Step("root/1")
    .GetActiveStep();

var stepEvents = management.Instance(instanceId)
    .Step("root/1")
    .GetLifecycleEvents();
```

Step paths are definition node paths such as `root/1` or
`root/2/branches/0/1`. They are useful for diagnostics and tests, but should not
be treated as stable business identifiers across definition rewrites.

Memory retention and eviction:

```csharp
// Remove one terminal instance from process memory (throws while it is active).
bool evicted = management.Evict(instanceId);

// Remove every terminal instance; returns how many were evicted.
int count = management.EvictTerminal();
```

Terminal instances stay queryable until evicted or the process exits — that is
the ephemeral queryability guarantee, but it also means a long-lived host that
never evicts grows memory without bound. Call `EvictTerminal()` periodically (or
`Evict(id)` after consuming an instance's outcome) in services that run many
workflows. Eviction also releases saga runtime state; evicted instances are no
longer visible to management queries or event routing.

## Ephemeral Saga Mode

Ephemeral saga support is intentionally reduced-guarantee. It runs forward
actions and compensations in process. It does not provide durable recovery,
durable compensation audit, or post-restart operator remediation.

```csharp
var saga = new SagaBuilder<CheckoutSagaState>()
    .Init<string>(_ => new CheckoutSagaState())
    .Then(() => new ReserveInventorySagaStep())
    .CompensateBy(() => new ReleaseInventorySagaStep())
    .Then(() => new ChargePaymentSagaStep())
    .CompensateBy(() => new RefundPaymentSagaStep())
    .End()
    .Build(DefinitionId.New(), DefinitionVersion.Initial);

var snapshot = await engine.StartSagaAsync<string, CheckoutSagaState>(
    saga,
    "checkout-001",
    CancellationToken.None);
```

If a forward action fails after earlier actions completed, compensations run in
reverse completion order. A repeated in-process compensation request is
idempotent for the live runtime state:

```csharp
var compensated = await engine.RequestSagaCompensationAsync<CheckoutSagaState>(
    snapshot.InstanceId,
    CancellationToken.None);
```

Use durable saga execution for production-grade compensation history and
operator recovery.

## Unsupported Or Durable-Only Shapes

The selected `EphemeralWorkflowBuilder<TState>` exposes only ephemeral capabilities.
Durable child workflows and `ContinueAsNew` are absent from that authoring surface;
the compiler also rejects unsupported manually constructed graphs before registration.

Do not expect these capabilities from ephemeral mode:

- Restart-safe waits or timers.
- Multi-node ownership and recovery.
- Durable inbox, outbox, projections, history, archive, purge, pause, or resume.
- Durable child workflow execution.
- Durable saga audit and operator recovery.
- Background timer pumping unless the host explicitly schedules
  `FireDueTimersAsync`.

## Host Responsibilities

The ephemeral engine is a library. The host application owns:

- Threading and lifetime for calls into `StartAsync`, `RaiseEventAsync`, and
  `FireDueTimersAsync`.
- Authentication and authorization before exposing management commands.
- Scheduling a timer pump if delays or wait timeouts are used.
- Translating external messages into `EventEnvelope`.
- Persisting any application-level correlation between business objects and
  `InstanceId` when instance-targeted delivery is needed.
- Idempotency for external side effects performed by workflow steps.

## Testing Workflows

The existing tests are the best examples for current behavior:

- `tests/OrcaCore.Acceptance.Tests/StraightLineAcceptanceTests.cs`
- `tests/OrcaCore.Acceptance.Tests/WaitAcceptanceTests.cs`
- `tests/OrcaCore.Acceptance.Tests/RoutingAcceptanceTests.cs`
- `tests/OrcaCore.Acceptance.Tests/MailboxAcceptanceTests.cs`
- `tests/OrcaCore.Acceptance.Tests/TimerAcceptanceTests.cs`
- `tests/OrcaCore.Acceptance.Tests/ParallelAcceptanceTests.cs`
- `tests/OrcaCore.Acceptance.Tests/WhenFirstAcceptanceTests.cs`
- `tests/OrcaCore.Acceptance.Tests/ForEachAcceptanceTests.cs`
- `tests/OrcaCore.Acceptance.Tests/ManagementAcceptanceTests.cs`
- `tests/OrcaCore.Engine.Ephemeral.Tests`

Useful targeted commands:

```powershell
dotnet test tests/OrcaCore.Engine.Ephemeral.Tests/OrcaCore.Engine.Ephemeral.Tests.csproj --no-restore
dotnet test tests/OrcaCore.Acceptance.Tests/OrcaCore.Acceptance.Tests.csproj --no-restore --filter "AC=AC-101|AC=AC-102|AC=AC-103"
dotnet test OrcaCore.slnx --no-restore --filter "FullyQualifiedName~OrcaCore.Engine.Ephemeral.Tests"
```

For timer-heavy tests, construct the engine with a controllable `TimeProvider`.
The repository test support includes a `Clock` helper in
`tests/OrcaCore.TestSupport`.

## Troubleshooting

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| `No workflow definition is registered` | `RegisterDefinition` was not called for the `DefinitionId`. | Register the exact definition before `StartAsync`. |
| `Workflow definition ... was not registered for state type ...` | The generic `TState` passed to `StartAsync` does not match the registered definition. | Use the same state type selected by `Workflow.Ephemeral<TState>`. |
| `AwaitCompletionAsync` throws because the instance did not reach a terminal state | The workflow entered `Waiting` on an event or timer. | Use `StartAsync`, then resume with an event or `FireDueTimersAsync`. |
| Correlation delivery says no active wait exists | No in-memory active wait matches the event name and correlation. | Check the event name, correlation selector, and whether the instance is still waiting. |
| Correlation delivery says delivery is ambiguous | More than one active wait matches the same event name and correlation. | Use instance-targeted delivery or make correlations unique. |
| Timer does not continue the workflow | The host has not pumped due timers, or time has not advanced past the due time. | Call `FireDueTimersAsync`; in tests, advance the injected `TimeProvider`. |
| Management predicate throws `NotSupportedException` | The predicate used a method call, indexer, object creation, invocation, or conditional expression. | Restrict predicates to metadata property comparisons with `&&` and `||`. |
| Broad terminate throws explicit safety error | `Management.All().TerminateAsync(...)` was called without `DestructiveCommandSafety.Confirmed`. | Pass the safety token after host-side operator authorization. |
| Compiler reports an unsupported capability | A durable-only node was introduced into an ephemeral graph outside the selected builder. | Use `Workflow.Durable<TState>` for child workflows, or ephemeral `ForEach` for in-instance fanout. |

## Design Checklist For New Ephemeral Workflows

Before adding a workflow, answer these questions:

- What is the state type, and is all step-visible mutation stored on it?
- What external events can resume the workflow, and what stable correlation IDs
  identify them?
- Can the workflow complete synchronously, or should callers use `StartAsync`
  and later event/timer delivery?
- Are any waits in loops protected from stale events by iteration-specific
  correlation?
- Are external side effects idempotent if a step retries or the caller retries an
  event delivery?
- Do timers matter after process restart? If yes, ephemeral mode is the wrong
  guarantee level.
- Do operators need durable history, pause, resume, archive, purge, or retry? If
  yes, use durable mode.
- Does fanout stay within one in-memory instance? If yes, use `ForEach`; if it
  needs child workflow identities and durable joins, use durable mode.
