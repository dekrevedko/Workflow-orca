using Microsoft.Extensions.DependencyInjection;
using OrcaCore;
using OrcaCore.Hosting;
using System.Text.Json.Serialization;

internal static class FailureSettlementCases
{
    private static readonly TimeSpan Guard = TimeSpan.FromSeconds(15);

    internal static async Task Run(bool generated)
    {
        if (generated) WorkflowJsonMetadata.Register<FailureSettlementContext>();
        foreach (var bounded in new[] { false, true })
        {
            await ResourceRecipe(bounded);
            await NoHandleResourceRecipe(bounded);
        }
        await ExternalChildRemainsApplicationOwned();
        // Independent exact mixed legacy/v2 bytes through the public fingerprint boundary.
        await CodecCases.Failures();
        Console.WriteLine("FS16 TYPES " + typeof(IOException).FullName + "|" + typeof(FailureConsumerError).FullName);
    }

    private static async Task ResourceRecipe(bool bounded)
    {
        var clock = new FailureSettlementClock();
        var resource = new FailureConsumerResource();
        var body = new FailureBlockingStep(resource);
        var known = new FailureConsumerError("known message");
        var services = new ServiceCollection().AddSingleton<TimeProvider>(clock)
            .AddSingleton(body).AddSingleton(new FailureReturnedStep(known));
        services.AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions
        {
            StructuredExecution = new() { MaxConcurrentExecutionPathsPerInstance = 2, StepThrottles = [] }, TransientPools = [],
            TerminalRetention = bounded ? EphemeralTerminalRetentionPolicy.Create(1, TimeSpan.FromHours(1), TimeSpan.FromMinutes(1)) : null
        });
        var provider = services.BuildServiceProvider();
        var lifetime = provider.GetRequiredService<IEphemeralWorkflowHostLifetime>();
        WorkflowInstanceHandle? old = null;
        try
        {
            var registry = provider.GetRequiredService<IWorkflowDefinitionRegistry>();
            var original = new IOException("original business failure");
            var throwing = registry.Register(Workflow.Ephemeral<FailureConsumerState>(DefinitionId.New(), DefinitionVersion.Initial)
                .Init<int>(value => new FailureConsumerState(value)).Then(_ => throw original).End(state => state.Value.Value).Build()).GetHandleOrThrow();
            var start = await throwing.StartOrGetAsync(1, StartIdempotencyKey.Create("original"));
            var thrown = await Unavailable(() => start.WaitForOutputAsync().AsTask());
            Check(ReferenceEquals(thrown.InnerException, original) && thrown.Failure?.Code == "WF-STEP-UNHANDLED" &&
                thrown.Failure.ExceptionType == typeof(IOException).FullName && thrown.Failure.Message == original.Message, "exact thrown original and detached type/code");
            old = start.GetHandleOrThrow();
            await old.WaitForSettlementAsync().AsTask().WaitAsync(Guard);
            var returning = registry.Register(Workflow.Ephemeral<FailureConsumerState>(DefinitionId.New(), DefinitionVersion.Initial)
                .Init<int>(value => new FailureConsumerState(value)).Then<FailureReturnedStep>().End(state => state.Value.Value).Build()).GetHandleOrThrow();
            var returnedStart = await returning.StartOrGetAsync(1, StartIdempotencyKey.Create("returned"));
            var returned = await Unavailable(() => returnedStart.WaitForOutputAsync().AsTask());
            Check(ReferenceEquals(returned.InnerException, known) && returned.Failure?.Code == "APP-CONSUMER" &&
                returned.Failure.ExceptionType == typeof(FailureConsumerError).FullName, "returned known error retains its object and opaque type name");
            var detached = (await returnedStart.GetHandleOrThrow().GetSnapshotAsync()).Failure!;
            Check(detached.ExceptionType == typeof(FailureConsumerError).FullName, "snapshot carries detached type text");

            var definition = registry.Register(Workflow.Ephemeral<FailureConsumerState>(DefinitionId.New(), DefinitionVersion.Initial)
                .Init<int>(value => new FailureConsumerState(value)).Then<FailureBlockingStep>().WithStepTimeout(TimeSpan.FromSeconds(1))
                .End(state => state.Value.Value).Build()).GetHandleOrThrow();
            var pending = definition.StartOrGetAsync(1, StartIdempotencyKey.Create("resource")).AsTask();
            await body.Entered.Task.WaitAsync(Guard);
            clock.Advance(TimeSpan.FromSeconds(1));
            var instance = (await pending.WaitAsync(Guard)).GetHandleOrThrow();
            var timeout = await Unavailable(() => instance.WaitForOutputAsync().AsTask());
            var receipt = instance.WaitForSettlementAsync().AsTask();
            using var cancelled = new CancellationTokenSource();
            var abandoned = instance.WaitForSettlementAsync(cancelled.Token).AsTask();
            cancelled.Cancel();
            try { await abandoned.WaitAsync(Guard); throw new InvalidOperationException("observer did not cancel"); }
            catch (OperationCanceledException) { }
            Check(!receipt.IsCompleted && resource.Disposals == 0 && timeout.InnerException is null, "logical timeout and observer cancellation do not release a live resource");
            body.Release.TrySetResult();
            await receipt.WaitAsync(Guard);
            resource.Dispose();
            Check(resource.Disposals == 1 && resource.Uses == 2, "release exactly once after final physical proof");
            var after = await Unavailable(() => instance.WaitForOutputAsync().AsTask());
            Check(after.InnerException is null && after.Failure?.Code == timeout.Failure?.Code, "late body exception cannot replace the timeout winner");
            await old.WaitForSettlementAsync();
            Check(await lifetime.StopAsync(Timeout.InfiniteTimeSpan).AsTask().WaitAsync(Guard) is EphemeralHostShutdownResult.Completed, "receipt observers do not block host join");
        }
        finally
        {
            body.Release.TrySetResult();
            Check(await lifetime.StopAsync(Timeout.InfiniteTimeSpan).AsTask().WaitAsync(Guard) is EphemeralHostShutdownResult.Completed, "join before provider disposal");
            await provider.DisposeAsync();
        }
        if (old is not null) Check(old.WaitForSettlementAsync().IsCompletedSuccessfully, "issued completed receipt is passive after owner disposal");
    }

    private static async Task ExternalChildRemainsApplicationOwned()
    {
        var resource = new FailureConsumerResource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task child = Task.CompletedTask;
        var services = new ServiceCollection();
        services.AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions
        { StructuredExecution = new() { MaxConcurrentExecutionPathsPerInstance = 2, StepThrottles = [] }, TransientPools = [] });
        var provider = services.BuildServiceProvider();
        var lifetime = provider.GetRequiredService<IEphemeralWorkflowHostLifetime>();
        try
        {
            var definition = provider.GetRequiredService<IWorkflowDefinitionRegistry>().Register(
                Workflow.Ephemeral<FailureConsumerState>(DefinitionId.New(), DefinitionVersion.Initial)
                    .Init<int>(value => new FailureConsumerState(value)).Then(_ =>
                    {
                        child = Task.Run(async () => { entered.TrySetResult(); await release.Task; resource.Use(); });
                        return ValueTask.CompletedTask;
                    }).End().Build()).GetHandleOrThrow();
            var instance = (await definition.StartOrGetAsync(1, StartIdempotencyKey.Create("external-child"))).GetHandleOrThrow();
            await entered.Task.WaitAsync(Guard);
            await instance.WaitForSettlementAsync().AsTask().WaitAsync(Guard);
            Check(!child.IsCompleted && resource.Disposals == 0, "an unjoined child process or IO task remains the application's obligation");
            release.TrySetResult();
            await child.WaitAsync(Guard);
            resource.Dispose();
            Check(resource.Disposals == 1 && resource.Uses == 1, "an external owner releases after its own child join");
        }
        finally
        {
            release.TrySetResult();
            await child.WaitAsync(Guard);
            Check(await lifetime.StopAsync(Timeout.InfiniteTimeSpan).AsTask().WaitAsync(Guard) is EphemeralHostShutdownResult.Completed, "external recipe joins the engine separately");
            await provider.DisposeAsync();
        }
    }

    private static async Task NoHandleResourceRecipe(bool bounded)
    {
        var clock = new FailureSettlementClock();
        var resource = new FailureConsumerResource();
        var body = new FailureBlockingStep(resource);
        var services = new ServiceCollection().AddSingleton<TimeProvider>(clock).AddSingleton(body);
        services.AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions
        {
            StructuredExecution = new() { MaxConcurrentExecutionPathsPerInstance = 2, StepThrottles = [] }, TransientPools = [],
            TerminalRetention = bounded ? EphemeralTerminalRetentionPolicy.Create(1, TimeSpan.FromHours(1), TimeSpan.FromMinutes(1)) : null
        });
        var provider = services.BuildServiceProvider();
        var lifetime = provider.GetRequiredService<IEphemeralWorkflowHostLifetime>();
        try
        {
            var definition = provider.GetRequiredService<IWorkflowDefinitionRegistry>().Register(
                Workflow.Ephemeral<FailureConsumerState>(DefinitionId.New(), DefinitionVersion.Initial)
                    .Init<int>(value => new FailureConsumerState(value)).Then<FailureBlockingStep>()
                    .WithStepTimeout(TimeSpan.FromSeconds(1)).End().Build()).GetHandleOrThrow();
            using var cancelled = new CancellationTokenSource();
            var pending = definition.StartOrGetAsync(1, StartIdempotencyKey.Create("no-handle"), cancelled.Token).AsTask();
            await body.Entered.Task.WaitAsync(Guard);
            cancelled.Cancel();
            clock.Advance(TimeSpan.FromSeconds(1));
            try { await pending.WaitAsync(Guard); throw new InvalidOperationException("An aborted start returned a handle."); }
            catch (OperationCanceledException) { }
            var incomplete = await lifetime.StopAsync(TimeSpan.Zero).AsTask().WaitAsync(Guard);
            Check(incomplete is EphemeralHostShutdownResult.Incomplete { PendingWorkCount: > 0 } && resource.Disposals == 0,
                "without a handle, resources remain owned until the application/host proves physical settlement");
            body.Release.TrySetResult();
            Check(await lifetime.StopAsync(Timeout.InfiniteTimeSpan).AsTask().WaitAsync(Guard) is EphemeralHostShutdownResult.Completed,
                "host-wide join supplies the no-handle resource proof");
            resource.Dispose();
            Check(resource.Uses == 2 && resource.Disposals == 1, "the no-handle owner disposes exactly once after the physical body returns");
        }
        finally
        {
            body.Release.TrySetResult();
            Check(await lifetime.StopAsync(Timeout.InfiniteTimeSpan).AsTask().WaitAsync(Guard) is EphemeralHostShutdownResult.Completed, "join aborted start before DI disposal");
            await provider.DisposeAsync();
        }
    }

    private static async Task<WorkflowOutputUnavailableException> Unavailable(Func<Task<int>> action)
    {
        try { await action().WaitAsync(Guard); }
        catch (WorkflowOutputUnavailableException error) { return error; }
        throw new InvalidOperationException("Expected output-unavailable failure.");
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("ASSERTION FS16: " + message);
    }
}

public sealed record FailureConsumerState(int Value);
public sealed class FailureConsumerError(string message) : OrcaCoreException("APP-CONSUMER", message);
public sealed class FailureReturnedStep(FailureConsumerError error) : IStep<FailureConsumerState>
{
    public ValueTask<StepResult> ExecuteAsync(StepContext<FailureConsumerState> context, CancellationToken cancellationToken) => ValueTask.FromResult<StepResult>(new StepResult.Failed(error));
}
public sealed class FailureBlockingStep(FailureConsumerResource resource) : IStep<FailureConsumerState>
{
    internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async ValueTask<StepResult> ExecuteAsync(StepContext<FailureConsumerState> context, CancellationToken cancellationToken)
    {
        resource.Use(); Entered.TrySetResult();
        await Release.Task;
        resource.Use();
        throw new IOException("late resource failure");
    }
}
public sealed class FailureConsumerResource : IDisposable
{
    internal int Uses;
    internal int Disposals;
    internal void Use() { if (Disposals != 0) throw new ObjectDisposedException(nameof(FailureConsumerResource)); Uses++; }
    public void Dispose() => Disposals++;
}
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(FailureConsumerState))]
[JsonSerializable(typeof(Payload))]
[JsonSerializable(typeof(TextState))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(IReadOnlyList<string>))]
public partial class FailureSettlementContext : JsonSerializerContext;

internal sealed class FailureSettlementClock : TimeProvider
{
    private readonly object gate = new();
    private readonly List<Timer> timers = [];
    private long ticks;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() { lock (gate) return ticks; }
    public override DateTimeOffset GetUtcNow() { lock (gate) return DateTimeOffset.UnixEpoch.AddTicks(ticks); }
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new Timer(this, callback, state);
        lock (gate) { timers.Add(timer); timer.Change(dueTime, period); }
        return timer;
    }
    internal void Advance(TimeSpan amount)
    {
        Timer[] due;
        lock (gate) { ticks += amount.Ticks; due = timers.Where(timer => timer.Due <= ticks).ToArray(); foreach (var timer in due) timer.Due = long.MaxValue; }
        foreach (var timer in due) timer.Callback(timer.State);
    }
    private sealed class Timer(FailureSettlementClock owner, TimerCallback callback, object? state) : ITimer
    {
        internal readonly TimerCallback Callback = callback;
        internal readonly object? State = state;
        internal long Due = long.MaxValue;
        public bool Change(TimeSpan dueTime, TimeSpan period) { lock (owner.gate) Due = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : owner.ticks + dueTime.Ticks; return true; }
        public void Dispose() { lock (owner.gate) { Due = long.MaxValue; owner.timers.Remove(this); } }
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
