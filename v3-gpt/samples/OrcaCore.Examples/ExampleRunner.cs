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

    private static async Task RunSimpleEphemeralWorkflowAsync(CancellationToken cancellationToken)
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = new WorkflowBuilder<OrderState>()
            .Init<OrderInput>(OrderState.From)
            .Then<ValidateOrderStep>()
            .Then(() => new RecordOrderStep("reserve inventory"))
            .End("Accepted")
            .Build(DefinitionId.New(), DefinitionVersion.Initial);

        engine.RegisterDefinition(definition);

        var snapshot = await engine.AwaitCompletionAsync<OrderInput, OrderState>(
            definition.DefinitionId,
            new OrderInput("order-1001", 149.95m, ["sku-1", "sku-2"]),
            cancellationToken).ConfigureAwait(false);
        var state = engine.Management.Instance(snapshot.InstanceId).GetState<OrderState>();

        WriteSnapshot(snapshot);
        Console.WriteLine($"  outcome={snapshot.EndOutcomeName}, log={string.Join(" -> ", state.Log)}");
    }

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
        var waiting = await engine.StartAsync<OrderInput, OrderState>(
            definition.DefinitionId,
            new OrderInput("order-2001", 88.40m, ["sku-9"]),
            cancellationToken).ConfigureAwait(false);

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

        var statistics = engine.Management.All().Statistics();
        var state = engine.Management.Instance(snapshot.InstanceId).GetState<OrderState>();

        WriteSnapshot(snapshot);
        Console.WriteLine($"  processed-items={state.ProcessedItemCount}, foreach-groups={snapshot.ForEachGroups.Count}");
        Console.WriteLine($"  management-groups={string.Join(", ", statistics.Groups.Select(GroupText))}");
    }

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

    private sealed record OrderInput(string OrderId, decimal Total, IReadOnlyList<string> Items);

    private sealed record PaymentApproved(string AuthorizationId, string InstanceId);

    private sealed record ExternalJobPayload(string Kind, string OrderId);

    private sealed class OrderState
    {
        public string OrderId { get; set; } = string.Empty;

        public decimal Total { get; set; }

        public List<string> Items { get; set; } = [];

        public int NextItemIndex { get; set; }

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
