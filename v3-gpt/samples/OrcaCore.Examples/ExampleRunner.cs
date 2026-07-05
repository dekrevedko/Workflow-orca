using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Management;
using OrcaCore.Engine.Ephemeral;
using OrcaCore.Hosting;
using OrcaCore.Providers.InMemory;

namespace OrcaCore.Examples;

/// <summary>
/// A runnable, self-contained tour of the OrcaCore public API.
/// </summary>
/// <remarks>
/// WHAT: six examples that build up from a single in-process workflow to durable, host-integrated
/// operations. HOW: each example is fully independent — it constructs its own engine/host, authors
/// a definition with <see cref="WorkflowBuilder{TState}"/>, runs it, and prints the resulting
/// snapshot so you can see the observable outcome. WHY: the two OrcaCore execution modes are
/// deliberately different surfaces — the <b>ephemeral</b> engine runs a workflow to a result
/// in-process (examples 01–05), while the <b>durable</b> path is command-driven and persists every
/// transition (example 06). Reading them in order shows where each mode fits.
/// </remarks>
public static class ExampleRunner
{
    public static async Task RunAllAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("OrcaCore examples");
        Console.WriteLine("Examples progress from in-process authoring to durable host APIs.");
        Console.WriteLine();

        var examples = new (string Title, Func<CancellationToken, Task> Run)[]
        {
            ("01 simple ephemeral workflow", RunSimpleEphemeralWorkflowAsync),
            ("02 event wait and correlation routing", RunEventWaitAsync),
            ("03 fanout with management inspection", RunFanoutManagementAsync),
            ("04 timers and transient timer pump", RunTimerAsync),
            ("05 ephemeral saga compensation", RunEphemeralSagaAsync),
            ("06 durable host APIs and management", RunDurableHostApiAsync)
        };

        foreach (var example in examples)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Console.WriteLine($"== {example.Title} ==");
            await example.Run(cancellationToken).ConfigureAwait(false);
            Console.WriteLine();
        }

        Console.WriteLine("Advanced dashboard sample:");
        Console.WriteLine("  dotnet run --project samples/OrcaCore.Dashboard/OrcaCore.Dashboard.csproj");
        Console.WriteLine("The dashboard includes durable operations and local Kubernetes job scheduling.");
    }

    // WHAT: the smallest useful workflow — validate, do work, finish with a named outcome.
    // HOW: WorkflowBuilder authors an immutable definition (Init seeds state from input; each Then
    //   adds a step; End names the terminal outcome). AwaitCompletionAsync runs it to a terminal
    //   snapshot in one call.
    // WHY: use the ephemeral engine + AwaitCompletionAsync for short, synchronous request/response
    //   work where you want the result now and do not need persistence across a restart.
    private static async Task RunSimpleEphemeralWorkflowAsync(CancellationToken cancellationToken)
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = new WorkflowBuilder<OrderState>()
            .Init<OrderInput>(OrderState.From)
            .Then<ValidateOrderStep>()
            .Then(() => new RecordOrderStep("reserve inventory"))
            .End("Accepted")
            .Build(DefinitionId.New(), DefinitionVersion.Initial);

        // A definition is registered once, then started many times; it is immutable and reusable.
        engine.RegisterDefinition(definition);

        var snapshot = await engine.AwaitCompletionAsync<OrderInput, OrderState>(
            definition.DefinitionId,
            new OrderInput("order-1001", 149.95m, ["sku-1", "sku-2"]),
            cancellationToken).ConfigureAwait(false);

        // Business state is never exposed live; read a typed copy through the management surface.
        var state = engine.Management.Instance(snapshot.InstanceId).GetState<OrderState>();

        WriteSnapshot(snapshot);
        Console.WriteLine($"  outcome={snapshot.EndOutcomeName}, log={string.Join(" -> ", state.Log)}");
    }

    // WHAT: a workflow that pauses for an external event and resumes when it arrives.
    // HOW: Wait declares "suspend here until a 'PaymentApproved' event whose correlation matches
    //   this order arrives". StartAsync returns while the instance is Waiting; RaiseEventByCorrelation
    //   delivers the event, the engine matches it by correlation id, and the instance resumes.
    // WHY: correlation is the request/reply identity — it lets an out-of-band signal (a webhook, a
    //   human approval) find the exact instance that is waiting for it, without the caller knowing
    //   the instance id. The resumed step reads the event payload via context.ResumedEvent.
    private static async Task RunEventWaitAsync(CancellationToken cancellationToken)
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = new WorkflowBuilder<OrderState>()
            .Init<OrderInput>(OrderState.From)
            .Wait("PaymentApproved", state => new CorrelationId(state.OrderId))
            .Then<CapturePaymentApprovalStep>()
            .End("Paid")
            .Build(DefinitionId.New(), DefinitionVersion.Initial);

        engine.RegisterDefinition(definition);

        // StartAsync returns as soon as the instance suspends at the Wait — status is Waiting.
        var waiting = await engine.StartAsync<OrderInput, OrderState>(
            definition.DefinitionId,
            new OrderInput("order-2001", 88.40m, ["sku-9"]),
            cancellationToken).ConfigureAwait(false);

        // The active wait exposes the correlation the engine will match the inbound event against.
        var wait = waiting.ActiveWaits.Single();
        var resumed = await engine.RaiseEventByCorrelationAsync<OrderState>(
            Event(
                "PaymentApproved",
                wait.CorrelationId,
                new PaymentApproved("auth-77", waiting.InstanceId.ToString())),
            cancellationToken).ConfigureAwait(false);
        var state = engine.Management.Instance(resumed.InstanceId).GetState<OrderState>();

        WriteSnapshot(waiting);
        WriteSnapshot(resumed);
        Console.WriteLine($"  routed by correlation={wait.CorrelationId}, log={string.Join(" -> ", state.Log)}");
    }

    // WHAT: fan out over a collection with bounded concurrency, plus a governed resource pool, then
    //   inspect fleet-level statistics through the management API.
    // HOW: engine options cap how much runs at once (advancements/steps) and declare a named pool
    //   "fulfillment" with capacity 1. WithPoolKey binds the next step to that pool. ForEach expands
    //   state.Items into work items (partitioned by Items()), runs the body per item under
    //   maxConcurrency, and joins (WhenAll by default) before continuing.
    // WHY: use ForEach for bounded parallel work and pools to throttle contention on scarce
    //   resources; Management.All().Statistics() answers "how many instances are in each state"
    //   without touching business state.
    private static async Task RunFanoutManagementAsync(CancellationToken cancellationToken)
    {
        var options = new EphemeralWorkflowEngineOptions
        {
            MaxConcurrentAdvancements = 4,
            MaxConcurrentSteps = 2
        };
        options.NamedPools["fulfillment"] = 1;

        var engine = new EphemeralWorkflowEngine(TimeProvider.System, options);
        var definition = new WorkflowBuilder<OrderState>()
            .Init<OrderInput>(OrderState.From)
            .WithPoolKey("fulfillment")
            .Then(() => new RecordOrderStep("entered fulfillment pool"))
            .ForEach(
                state => state.Items,
                WorkflowPartitioner<string>.Items(),
                branch => branch.Then<CountFanoutItemStep>(),
                maxConcurrency: 2)
            .Then(() => new RecordOrderStep("all item work completed"))
            .End("Fulfilled")
            .Build(DefinitionId.New(), DefinitionVersion.Initial);

        engine.RegisterDefinition(definition);
        var snapshot = await engine.StartAsync<OrderInput, OrderState>(
            definition.DefinitionId,
            new OrderInput("order-3001", 230.00m, ["pick", "pack", "label", "handoff"]),
            cancellationToken).ConfigureAwait(false);

        // Statistics is a metadata-only aggregate over all instances — cheap fleet visibility.
        var statistics = engine.Management.All().Statistics();
        var state = engine.Management.Instance(snapshot.InstanceId).GetState<OrderState>();

        WriteSnapshot(snapshot);
        Console.WriteLine($"  processed-items={state.ProcessedItemCount}, foreach-groups={snapshot.ForEachGroups.Count}");
        Console.WriteLine($"  management-groups={string.Join(", ", statistics.Groups.Select(GroupText))}");
    }

    // WHAT: a workflow that waits on a timer, and how the ephemeral engine advances due timers.
    // HOW: Delay suspends the instance until its fire time. The ephemeral engine does not own a
    //   background clock, so the caller pumps due timers explicitly with FireDueTimersAsync, which
    //   resumes every instance whose delay has elapsed and returns their terminal snapshots.
    // WHY: explicit pumping keeps the ephemeral engine deterministic and host-agnostic — you decide
    //   when time advances (a loop, a scheduler tick, a test's fake clock). The durable engine, by
    //   contrast, persists timers and fires them from a hosted sweep (see the SampleHost/dashboard).
    // NOTE: the real 25 ms Task.Delay below simply lets the 15 ms workflow timer become due; it is a
    //   demo convenience, not a pattern to copy into tests (use a TimeProvider fake clock there).
    private static async Task RunTimerAsync(CancellationToken cancellationToken)
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = new WorkflowBuilder<OrderState>()
            .Init<OrderInput>(OrderState.From)
            .Delay(TimeSpan.FromMilliseconds(15))
            .Then(() => new RecordOrderStep("timer fired"))
            .End("ReminderSent")
            .Build(DefinitionId.New(), DefinitionVersion.Initial);

        engine.RegisterDefinition(definition);
        var waiting = await engine.StartAsync<OrderInput, OrderState>(
            definition.DefinitionId,
            new OrderInput("order-4001", 42.00m, []),
            cancellationToken).ConfigureAwait(false);

        await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken).ConfigureAwait(false);
        var fired = await engine.FireDueTimersAsync(cancellationToken).ConfigureAwait(false);
        var completed = fired.Single();
        var state = engine.Management.Instance(completed.InstanceId).GetState<OrderState>();

        WriteSnapshot(waiting);
        WriteSnapshot(completed);
        Console.WriteLine($"  transient-timer-log={string.Join(" -> ", state.Log)}");
    }

    // WHAT: a saga — a sequence of steps that each register a compensating action, so a later
    //   failure unwinds the earlier successful work.
    // HOW: SagaBuilder pairs each forward Then with a CompensateBy. When FailSagaStep fails, the
    //   engine runs the registered compensations for the completed steps in reverse order, and the
    //   instance ends in the Compensated state (not Failed).
    // WHY: sagas are how you get "all-or-nothing" semantics across steps that have real side effects
    //   and cannot share a transaction (charge a card, reserve stock). Note compensation runs only
    //   on failure — a graceful Cancel does not trigger it (SG-011).
    private static async Task RunEphemeralSagaAsync(CancellationToken cancellationToken)
    {
        var saga = new SagaBuilder<SagaState>()
            .Init<string>(_ => new SagaState())
            .Then(() => new RecordSagaStep("reserve inventory"))
            .CompensateBy(() => new RecordSagaStep("release inventory"))
            .Then(() => new RecordSagaStep("authorize payment"))
            .CompensateBy(() => new RecordSagaStep("refund payment"))
            .Then<FailSagaStep>()
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);

        var engine = new EphemeralWorkflowEngine();
        var snapshot = await engine.StartSagaAsync<string, SagaState>(
            saga,
            "checkout-5001",
            cancellationToken).ConfigureAwait(false);
        var state = engine.Management.Instance(snapshot.InstanceId).GetState<SagaState>();

        WriteSnapshot(snapshot);
        Console.WriteLine($"  saga-log={string.Join(" -> ", state.Log)}");
    }

    // WHAT: the durable path — start-or-get idempotency, an external job that borrows a resource
    //   pool, inbox deduplication of a repeated completion, and durable management queries.
    // HOW: AddOrcaCore wires the durable stack over the in-memory provider. StartOrGetAsync is keyed
    //   by an idempotency key, so a retry returns the same instance (Created=false) instead of a
    //   duplicate. Durable work is driven by explicit commands through DurableCommandProcessor;
    //   commands carry ids so the engine can dedupe (the second CompleteExternalJob with the same
    //   CompletionEventId returns NoOp).
    // WHY: durable mode is for long-running, crash-safe orchestration where every transition is
    //   persisted and exactly-once matters. The command surface is what a host (or the hosted
    //   services in SampleHost) drives; here we call it directly to show the guarantees.
    private static async Task RunDurableHostApiAsync(CancellationToken cancellationToken)
    {
        using var host = CreateHost();
        var runtime = host.Services.GetRequiredService<DurableWorkflowRuntime>();
        var processor = host.Services.GetRequiredService<DurableCommandProcessor>();
        var management = host.Services.GetRequiredService<DurableManagement>();
        var pools = host.Services.GetRequiredService<InMemoryResourcePoolStore>();

        var definition = new WorkflowBuilder<OrderState>()
            .Init<OrderInput>(OrderState.From)
            .End("DurableStarted")
            .Build(DefinitionId.New(), DefinitionVersion.Initial);

        // Same idempotency key twice: the first call creates the instance, the second returns it.
        var firstStart = await runtime.StartOrGetAsync<OrderInput, OrderState>(
            "orders/order-6001",
            definition,
            new OrderInput("order-6001", 601.00m, ["risk-check"]),
            cancellationToken).ConfigureAwait(false);
        var secondStart = await runtime.StartOrGetAsync<OrderInput, OrderState>(
            "orders/order-6001",
            definition,
            new OrderInput("order-6001", 601.00m, ["risk-check"]),
            cancellationToken).ConfigureAwait(false);

        // The external job requires a "risk-workers" pool ticket (capacity 1); the engine acquires
        // it as part of starting the job and releases it when the job completes.
        await pools.UpsertPoolAsync(
            new ResourcePoolDefinition("risk-workers", 1, TimeSpan.FromMinutes(30)),
            cancellationToken).ConfigureAwait(false);
        await processor.ProcessAsync(
            new RunExternalJobCommand
            {
                CommandId = CommandId.New(),
                InstanceId = firstStart.InstanceId,
                RequestedAt = DateTimeOffset.UtcNow,
                ExternalJobId = "risk/order-6001",
                Payload = JsonSerializer.SerializeToUtf8Bytes(new ExternalJobPayload("risk-score", "order-6001")),
                Requirements = [new ResourcePoolRequirement("risk-workers", 1)],
                TimeoutAt = DateTimeOffset.UtcNow.AddMinutes(15)
            },
            cancellationToken).ConfigureAwait(false);

        // Complete the job once, then replay the identical completion. The shared CompletionEventId
        // is the inbox dedup key, so the replay is a NoOp rather than a second completion.
        var completionEventId = EventId.New();
        var completed = await processor.ProcessAsync(
            new CompleteExternalJobCommand
            {
                CommandId = CommandId.New(),
                InstanceId = firstStart.InstanceId,
                RequestedAt = DateTimeOffset.UtcNow,
                ExternalJobId = "risk/order-6001",
                CompletionEventId = completionEventId
            },
            cancellationToken).ConfigureAwait(false);
        var duplicate = await processor.ProcessAsync(
            new CompleteExternalJobCommand
            {
                CommandId = CommandId.New(),
                InstanceId = firstStart.InstanceId,
                RequestedAt = DateTimeOffset.UtcNow,
                ExternalJobId = "risk/order-6001",
                CompletionEventId = completionEventId
            },
            cancellationToken).ConfigureAwait(false);

        // Durable execution is command-driven: StartOrGetAsync records the start but does not
        // interpret the definition to its End, so the instance stays Running until commands
        // (steps, completion) advance it. That is why the snapshot below reports Running.
        var snapshot = await management.Instance(firstStart.InstanceId)
            .GetAsync(cancellationToken).ConfigureAwait(false);
        var statistics = await management.All()
            .StatisticsAsync(cancellationToken).ConfigureAwait(false);

        Console.WriteLine($"  start-created={firstStart.Created}, duplicate-created={secondStart.Created}");
        Console.WriteLine($"  external-job-result={completed.Outcome}, duplicate-completion={duplicate.Outcome}");
        WriteSnapshot(snapshot);
        Console.WriteLine($"  durable-groups={string.Join(", ", statistics.Groups.Select(GroupText))}");
    }

    // AddOrcaCore registers the full durable stack (engine, command processor, management, and the
    // in-memory provider). A real host swaps the provider (e.g. AddOrcaCorePostgreSql) and adds
    // AddOrcaCoreHostedServices to run the outbox pump and timer sweep — see OrcaCore.SampleHost.
    private static IHost CreateHost()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddOrcaCore();
        return builder.Build();
    }

    private static EventEnvelope Event(string name, CorrelationId correlationId, object? payload)
    {
        return new EventEnvelope
        {
            EventId = EventId.New(),
            EventName = name,
            CorrelationId = correlationId,
            Payload = payload,
            OccurredAt = DateTimeOffset.UtcNow
        };
    }

    private static void WriteSnapshot(WorkflowInstanceSnapshot snapshot)
    {
        Console.WriteLine(
            $"  {Short(snapshot.InstanceId)} {snapshot.Status} waits={snapshot.ActiveWaits.Count} stream={snapshot.StreamVersion?.ToString() ?? "ephemeral"}");
    }

    private static string GroupText(OrcaCore.Engine.Ephemeral.WorkflowStatisticsGroup group)
    {
        return $"{Short(group.DefinitionId)} v{group.DefinitionVersion.Value} {group.Status}:{group.Count}";
    }

    private static string GroupText(OrcaCore.Abstractions.Instances.WorkflowStatisticsGroup group)
    {
        return $"{Short(group.DefinitionId)} v{group.DefinitionVersion.Value} {group.Status}:{group.Count}";
    }

    private static string Short(InstanceId value)
    {
        return Short(value.ToString());
    }

    private static string Short(DefinitionId value)
    {
        return Short(value.ToString());
    }

    private static string Short(string value)
    {
        // Use the trailing characters: version-7 ids share a leading time prefix, so instances
        // created in the same run would otherwise all render with an identical head.
        return value.Length <= 8 ? value : value[^8..];
    }

    // The types below are what a workflow author writes: the start input, event payloads, the typed
    // business state, and the steps. OrcaCore never sees business meaning — it owns orchestration
    // (status, waits, position); steps own the state (CR-020). Steps mutate state only through the
    // context and return a StepResult to signal control flow.

    private sealed record OrderInput(string OrderId, decimal Total, IReadOnlyList<string> Items);

    private sealed record PaymentApproved(string AuthorizationId, string InstanceId);

    private sealed record ExternalJobPayload(string Kind, string OrderId);

    private sealed class OrderState
    {
        public string OrderId { get; set; } = string.Empty;

        public decimal Total { get; set; }

        public List<string> Items { get; set; } = [];

        public int ProcessedItemCount { get; set; }

        public List<string> Log { get; set; } = [];

        public static OrderState From(OrderInput input)
        {
            return new OrderState
            {
                OrderId = input.OrderId,
                Total = input.Total,
                Items = input.Items.ToList()
            };
        }
    }

    private sealed class ValidateOrderStep : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<OrderState> context,
            CancellationToken cancellationToken)
        {
            if (context.State.Total <= 0)
            {
                // Business-rule failures use OrcaCoreException (the general base). Reserve
                // WorkflowDefinitionException for malformed definitions, not runtime failures.
                return ValueTask.FromResult<StepResult>(
                    new StepResult.Failed(new OrcaCoreException("Order total must be positive.")));
            }

            context.State.Log.Add("validated order");
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class RecordOrderStep(string message) : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<OrderState> context,
            CancellationToken cancellationToken)
        {
            context.State.Log.Add(message);
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class CapturePaymentApprovalStep : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<OrderState> context,
            CancellationToken cancellationToken)
        {
            if (context.ResumedEvent?.Payload is PaymentApproved approved)
            {
                context.State.Log.Add($"payment {approved.AuthorizationId}");
            }

            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class CountFanoutItemStep : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<OrderState> context,
            CancellationToken cancellationToken)
        {
            // NOTE: a ForEach body currently has no per-item accessor on StepContext, so this
            // counts completed items rather than identifying "which item am I". That is safe only
            // because this step is synchronous; a body that suspends (I/O) under maxConcurrency>1
            // could interleave, so do not rely on a mutable counter to map back to a specific item.
            context.State.ProcessedItemCount++;
            context.State.Log.Add("processed a fanout item");

            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class SagaState
    {
        public List<string> Log { get; set; } = [];
    }

    private sealed class RecordSagaStep(string message) : IStep<SagaState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<SagaState> context,
            CancellationToken cancellationToken)
        {
            context.State.Log.Add(message);
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class FailSagaStep : IStep<SagaState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<SagaState> context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(
                new StepResult.Failed(new OrcaCoreException("payment capture failed")));
        }
    }
}
