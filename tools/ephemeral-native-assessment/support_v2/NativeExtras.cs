using Microsoft.Extensions.DependencyInjection;
using OrcaCore;
using OrcaCore.Hosting;
using System.Text.Json;
using System.Text.Json.Serialization;

internal static class NativeExtras
{
    private static readonly TimeSpan Guard = TimeSpan.FromSeconds(30);

    internal static void ThrottleCapacities()
    {
        foreach (var capacity in new[] { 0, -1, int.MinValue })
        {
            try { _ = StepExecutionThrottle.For<DispatchStep>(capacity); throw new InvalidOperationException("ASSERTION: nonpositive valid-step capacity accepted"); }
            catch (ArgumentOutOfRangeException error) when (error.ParamName == "maxConcurrency") { }
            try { _ = StepExecutionThrottle.For<DispatchNotStep>(capacity); throw new InvalidOperationException("ASSERTION: capacity did not precede invalid-type check"); }
            catch (ArgumentOutOfRangeException error) when (error.ParamName == "maxConcurrency") { }
        }
        var largest = StepExecutionThrottle.For<InheritedDispatchStep>(int.MaxValue);
        Check(largest.MaxConcurrency == int.MaxValue && largest.StepType == typeof(InheritedDispatchStep),
            "full positive capacity bound preserves exact inherited named-step identity");
    }

    internal static async Task TypedWaitAndPublicValues()
    {
        WorkflowJsonMetadata.Register<FlowContext>();
        var services = new ServiceCollection().AddTransient<FlowWaitStep>();
        services.AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions
        {
            StructuredExecution = new() { MaxConcurrentExecutionPathsPerInstance = 2, StepThrottles = [] },
            TransientPools = []
        });
        await using var provider = services.BuildServiceProvider();
        var definition = Workflow.Ephemeral<FlowState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(number => new FlowState(number)).Then<FlowWaitStep>().End(state => state.Value.Number).Build();
        var registered = provider.GetRequiredService<IWorkflowDefinitionRegistry>().Register(definition).GetHandleOrThrow();
        var instance = (await registered.StartOrGetAsync(7, StartIdempotencyKey.Create("typed-wait"))).GetHandleOrThrow();
        var before = await instance.GetSnapshotAsync();
        Check(before.Status == WorkflowInstanceStatus.Waiting && before.ActiveWaits.Count == 1 &&
            before.ActiveWaits[0].EventContract.Equals(FlowEvents.Resume), "typed StepResult parks with the exact descriptor");
        var original = new FlowPayload("payload", [1, 2]);
        var inbound = WorkflowInboundEvent<FlowPayload>.Create(FlowEvents.Resume, EventId.Create("public-value"),
            FlowEvents.Correlation, null, DateTimeOffset.UtcNow, new WorkflowEventRoute.Direct(instance.InstanceId), original);
        original.Values[0] = 99;
        Check(inbound.Payload.Values.SequenceEqual([1, 2]) && !ReferenceEquals(inbound.Payload, original),
            "public typed inbound construction detaches values without claiming delivery");
        var missing = await Error(() => Task.FromResult(WorkflowInboundEvent<MissingState>.Create(
            WorkflowEventContract<MissingState>.Create(EventName.Create("missing"), EventContractVersion.Initial),
            EventId.Create("missing"), FlowEvents.Correlation, null, DateTimeOffset.UtcNow,
            new WorkflowEventRoute.Direct(instance.InstanceId), new MissingState())));
        Check(missing is NotSupportedException && missing.Message.Contains("MissingState", StringComparison.Ordinal),
            "missing public input metadata rejects before any engine delivery");
        var after = await instance.GetSnapshotAsync();
        Check(after.Status == WorkflowInstanceStatus.Waiting && after.ActiveWaits.Single().WaitId.Equals(before.ActiveWaits.Single().WaitId) &&
            (await instance.GetStateAsync<FlowState>()).Number == 7 && FlowWaitStep.Calls == 1,
            "value construction/rejection consumes no wait and changes no accepted instance");
        using var observer = new CancellationTokenSource();
        var output = instance.WaitForOutputAsync(observer.Token).AsTask();
        Check(!output.IsCompleted, "typed parked output observer remains pending");
        observer.Cancel();
        Check(await Error(async () => await output.WaitAsync(Guard)) is OperationCanceledException &&
            (await instance.GetSnapshotAsync()).Status == WorkflowInstanceStatus.Waiting,
            "observer cancellation releases its pin without cancelling the workflow");
        Check(await provider.GetRequiredService<IEphemeralWorkflowHostLifetime>().StopAsync(Timeout.InfiniteTimeSpan) is EphemeralHostShutdownResult.Completed,
            "parked typed wait and callbacks physically join shutdown");
        Check(await Error(async () => await registered.StartOrGetAsync(8, StartIdempotencyKey.Create("after-stop"))) is EphemeralHostStoppingException &&
            FlowWaitStep.Calls == 1, "Completed refuses new workflow user code");
    }

    internal static async Task TransientPoolGrant()
    {
        WorkflowJsonMetadata.Register<FlowContext>();
        var signals = new PoolSignals();
        var clock = new SupportClock();
        using var listener = new System.Diagnostics.Metrics.MeterListener();
        listener.InstrumentPublished = (instrument, owner) =>
        {
            if (instrument.Meter.Name == OrcaCore.Abstractions.Diagnostics.OrcaCoreDiagnostics.EphemeralSourceName &&
                instrument.Name == OrcaCore.Abstractions.Diagnostics.OrcaCoreMetrics.GovernanceWaitDepth)
                owner.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) =>
        {
            if (value > 0) signals.Queued.TrySetResult();
        });
        listener.Start();
        var pool = TransientPoolName.Create("native-pool");
        var services = new ServiceCollection().AddSingleton(signals).AddSingleton<TimeProvider>(clock).AddTransient<PoolBody>();
        services.AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions
        {
            StructuredExecution = new() { MaxConcurrentExecutionPathsPerInstance = 2, StepThrottles = [] },
            TransientPools = [TransientPoolDefinition.Create(pool, 1)]
        });
        await using var provider = services.BuildServiceProvider();
        var definition = Workflow.Ephemeral<FlowState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(number => new FlowState(number)).Delay(TimeSpan.FromSeconds(1))
            .Then<PoolBody>().WithTransientPool(pool).End(value => value.Value.Number).Build();
        var registry = provider.GetRequiredService<IWorkflowDefinitionRegistry>();
        var registered = registry.Register(definition).GetHandleOrThrow();
        var followingDefinition = Workflow.Ephemeral<FlowState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(number => new FlowState(number)).Then<PoolBody>().WithTransientPool(pool).End(value => value.Value.Number).Build();
        var following = registry.Register(followingDefinition).GetHandleOrThrow();
        var holder = (await registered.StartOrGetAsync(1, StartIdempotencyKey.Create("holder"))).GetHandleOrThrow();
        try
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            await signals.Entered.Task.WaitAsync(Guard);
            var pendingFollower = following.StartOrGetAsync(2, StartIdempotencyKey.Create("follower")).AsTask();
            using var watchdog = new CancellationTokenSource(Guard);
            while (!signals.Queued.Task.IsCompleted)
            {
                watchdog.Token.ThrowIfCancellationRequested();
                listener.RecordObservableInstruments();
                await Task.Yield();
            }
            Check(signals.Active == 1 && !signals.Granted.Task.IsCompleted,
                "public governance gauge proves queuing while first body physically owns the pool");
            signals.Release.TrySetResult();
            await signals.Granted.Task.WaitAsync(Guard);
            var follower = (await pendingFollower.WaitAsync(Guard)).GetHandleOrThrow();
            Check(await holder.WaitForOutputAsync() == 1 && await follower.WaitForOutputAsync() == 2 && signals.Active == 0,
                "pool-release callback grants the queued body once and preserves typed output/physical capacity");
        }
        finally { signals.Release.TrySetResult(); }
        Check(await provider.GetRequiredService<IEphemeralWorkflowHostLifetime>().StopAsync(Timeout.InfiniteTimeSpan) is EphemeralHostShutdownResult.Completed,
            "grant callbacks and bodies quiesce before DI disposal");
    }

    internal static async Task FirstUseRace()
    {
        var configuring = Task.Run(() => WorkflowJsonMetadata.Register<BlockingContext>());
        Check(Probe.Entered.Wait(Guard), "constructor reached the publication barrier");
        try
        {
            if (JsonSerializer.IsReflectionEnabledByDefault)
                Check(Materialize(new Payload { Value = 7 }).Value == 7, "legacy JIT first use wins wholly");
            else
                Check(await Error(() => Task.FromResult(Materialize(new Payload { Value = 7 }))) is InvalidOperationException
                    { Message: "Reflection workflow metadata is unavailable." },
                    "reflection-disabled first use fails explicitly without partial generated publication");
        }
        finally { Probe.Release.Set(); }
        var losing = await Error(async () => await configuring.WaitAsync(Guard));
        Check(losing is InvalidOperationException && losing.Message.Contains("Reflection", StringComparison.Ordinal) &&
              losing.Message.Contains("sealed=True", StringComparison.Ordinal), "registration loses atomically to the sealed first use");
    }

    internal static async Task Retention()
    {
        WorkflowJsonMetadata.Register<PinContext>();
        var clock = new SupportClock();
        var services = new ServiceCollection().AddSingleton<TimeProvider>(clock);
        services.AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions
        {
            StructuredExecution = new() { MaxConcurrentExecutionPathsPerInstance = 2, StepThrottles = [] },
            TransientPools = [],
            TerminalRetention = EphemeralTerminalRetentionPolicy.Create(1, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1))
        });
        await using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IWorkflowDefinitionRegistry>();
        var definition = Workflow.Ephemeral<PinState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(number => new PinState { Number = number }).End(value => value.Value).Build();
        var registered = registry.Register(definition).GetHandleOrThrow();
        var key = StartIdempotencyKey.Create("pin");
        var first = (await registered.StartOrGetAsync(7, key)).GetHandleOrThrow();
        Check((await first.WaitForOutputAsync()).Number == 7, "initial detached output");
        PinState.Block = true;
        var read = Task.Run(async () => await first.GetStateAsync<PinState>());
        Check(PinState.Entered.Wait(Guard), "public state-detachment getter reached the pin barrier");
        try
        {
            var pressure = (await registered.StartOrGetAsync(8, StartIdempotencyKey.Create("pressure"))).GetHandleOrThrow();
            Check((await pressure.WaitForOutputAsync()).Number == 8, "count pressure completes independently of the query pin");
            Check((await first.GetSnapshotAsync()).Status == WorkflowInstanceStatus.Completed, "admitted query pins old instance under count pressure");
            clock.Advance(TimeSpan.FromSeconds(10));
            Check((await first.GetSnapshotAsync()).Status == WorkflowInstanceStatus.Completed, "age sweep cannot remove pinned state during detachment");
        }
        finally { PinState.Block = false; PinState.Release.Set(); }
        Check((await read.WaitAsync(Guard)).Number == 7, "pinned read returns a detached value");
        Check(await Error(async () => await first.GetSnapshotAsync()) is WorkflowInstanceNotFoundException,
            "final pin release permits age retirement and stale handles get not-found");
        var replacementResult = await registered.StartOrGetAsync(7, key);
        Check(replacementResult is WorkflowStartResult<WorkflowInstanceHandle<PinState>>.Accepted { WasExisting: false },
            "expired key permits a new start");
        var replacement = replacementResult.GetHandleOrThrow();
        Check(!replacement.InstanceId.Equals(first.InstanceId) && (await replacement.WaitForOutputAsync()).Number == 7,
            "replacement has distinct identity and equivalent typed output");
        Check(await provider.GetRequiredService<IEphemeralWorkflowHostLifetime>().StopAsync(Timeout.InfiniteTimeSpan) is EphemeralHostShutdownResult.Completed,
            "physical retirement and queries join shutdown");
    }

    private static T Materialize<T>(T value) => WorkflowInboundEvent<T>.Create(
        WorkflowEventContract<T>.Create(EventName.Create("native-race"), EventContractVersion.Initial),
        EventId.Create("native-race"), CorrelationId.Create("native-race"), null, DateTimeOffset.UtcNow,
        new WorkflowEventRoute.Correlation(DefinitionId.New()), value).Payload;
    private static async Task<Exception?> Error(Func<Task> action)
    {
        try { await action(); return null; }
        catch (Exception error) { return error; }
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("ASSERTION: " + message);
    }
}

public sealed class PinState
{
    internal static volatile bool Block;
    internal static readonly ManualResetEventSlim Entered = new();
    internal static readonly ManualResetEventSlim Release = new();
    private int number;
    public int Number
    {
        get
        {
            if (Block && number == 7)
            {
                Entered.Set();
                if (!Release.Wait(TimeSpan.FromSeconds(30))) throw new InvalidOperationException("pin getter barrier watchdog");
            }
            return number;
        }
        set => number = value;
    }
}

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(PinState))]
public partial class PinContext : JsonSerializerContext;

public readonly record struct FlowState(int Number);
public sealed record FlowPayload(string Name, List<int> Values);
public static class FlowEvents
{
    public static readonly WorkflowEventContract<FlowPayload> Resume = WorkflowEventContract<FlowPayload>.Create(
        EventName.Create("native-resume"), EventContractVersion.Initial);
    public static readonly CorrelationId Correlation = CorrelationId.Create("native-flow");
}
public sealed class FlowWaitStep : IStep<FlowState>
{
    internal static int Calls;
    public ValueTask<StepResult> ExecuteAsync(StepContext<FlowState> context, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref Calls);
        return ValueTask.FromResult<StepResult>(new StepResult.WaitForEvent<FlowPayload>(FlowEvents.Resume, FlowEvents.Correlation));
    }
}
public sealed class PoolSignals
{
    internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal readonly TaskCompletionSource Granted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal readonly TaskCompletionSource Queued = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal int Active;
}
public sealed class PoolBody(PoolSignals signals) : IStep<FlowState>
{
    public async ValueTask<StepResult> ExecuteAsync(StepContext<FlowState> context, CancellationToken cancellationToken)
    {
        if (Interlocked.Increment(ref signals.Active) != 1) throw new InvalidOperationException("ASSERTION: physical pool slot released too early");
        try
        {
            if (context.State.Number == 1)
            {
                signals.Entered.TrySetResult();
                await signals.Release.Task.WaitAsync(cancellationToken);
            }
            else signals.Granted.TrySetResult();
            return new StepResult.Completed();
        }
        finally { Interlocked.Decrement(ref signals.Active); }
    }
}
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(FlowState))]
[JsonSerializable(typeof(FlowPayload))]
public partial class FlowContext : JsonSerializerContext;
