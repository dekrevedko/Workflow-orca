using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrcaCore;
using OrcaCore.Hosting;

var evidence = new List<string>();
for (var repetition = 0; repetition < 3; repetition++)
{
    foreach (var recipe in new[] { "ordered-host", "explicit-host", "plain-di" })
    {
        foreach (var mode in new[] { "natural", "application-event", "delay", "retry" })
        {
            await Experiments.Drain(recipe, mode);
            evidence.Add($"{recipe}/{mode}/{repetition}:passed");
        }
        await Experiments.Incomplete(recipe);
        evidence.Add($"{recipe}/incomplete-retains-owner/{repetition}:passed");
    }
}
await Experiments.LocalWaitCancellation(); evidence.Add("output-wait-cancellation:passed");
await Experiments.ConcurrentStops(); evidence.Add("concurrent-stops-no-drain-window:passed");
await Experiments.RunAsyncNegative(); evidence.Add("RunAsync-incomplete-disposes-owner-negative:passed");
Console.WriteLine(JsonSerializer.Serialize(new { cases = evidence.Count, passed = evidence, internalApiOrProjectReferences = false }));

internal static class Experiments
{
    internal static readonly TimeSpan Guard = TimeSpan.FromSeconds(15);
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    internal static async Task Drain(string recipe, string mode)
    {
        var state = new Session(mode);
        var owner = Create(recipe, state);
        await owner.StartHost();
        var handle = owner.Registry.Register(Definition(mode)).GetHandleOrThrow();
        state.Pending = state.Submit(() => handle.StartOrGetAsync("drained", StartIdempotencyKey.Create("drain")));
        if (mode is "natural" or "application-event") await state.Entered.Task.WaitAsync(Guard);
        else Check((await state.Pending.WaitAsync(Guard)).GetHandleOrThrow() is not null, "suspended start must be accepted before close");
        state.Close();
        Check(await Error(async () => await state.Submit(() => handle.StartOrGetAsync("rejected", StartIdempotencyKey.Create("app-closed")))) is InvalidOperationException,
            "the app gate must refuse additional submitters before draining pending starts");
        Task? hostedStop = null;
        if (recipe == "ordered-host")
        {
            hostedStop = owner.Host!.StopAsync();
            await state.DrainerEntered.Task.WaitAsync(Guard);
            Check(owner.Host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping.IsCancellationRequested, "host must establish Closed before drain");
            var rejected = await Error(async () => await handle.StartOrGetAsync("new", StartIdempotencyKey.Create("closed")));
            Check(rejected is EphemeralHostStoppingException, "Closed must refuse new starts");
        }
        else if (recipe == "explicit-host") owner.Host!.Services.GetRequiredService<IHostApplicationLifetime>().StopApplication();

        Check(owner.DependenciesAlive, "draining must retain the actual provider and dependencies");
        if (mode == "application-event") state.Event.TrySetResult();
        else state.Release.TrySetResult();
        if (mode is "delay" or "retry") state.Clock.Advance(TimeSpan.FromSeconds(10));
        var start = await state.Pending.WaitAsync(Guard);
        var output = recipe == "ordered-host" ? null : await start.GetHandleOrThrow().WaitForOutputAsync().AsTask().WaitAsync(Guard);
        if (hostedStop is not null)
        {
            await hostedStop.WaitAsync(Guard);
            output = state.DrainedOutput;
        }
        Check(output == "drained", "initialized pending starts and accepted handles must drain successfully");
        Check(state.BodyCompleted, "normal body completion must precede shutdown completion");
        Check(owner.DependenciesAlive, "host stop is not provider disposal");
        Check(await owner.Lifetime.StopAsync(Timeout.InfiniteTimeSpan).AsTask().WaitAsync(Guard) is EphemeralHostShutdownResult.Completed, "drained owner must join");
        state.SkipDrain = true;
        await owner.Finish();
        Check(owner.DependenciesDisposedSafely, "dependency disposal must follow the completed join");
    }

    internal static async Task Incomplete(string recipe)
    {
        var state = new Session("natural") { SkipDrain = true };
        var owner = Create(recipe, state);
        await owner.StartHost();
        var handle = owner.Registry.Register(Definition("natural")).GetHandleOrThrow();
        state.Pending = state.Submit(() => handle.StartOrGetAsync("incomplete", StartIdempotencyKey.Create("incomplete")));
        await state.Entered.Task.WaitAsync(Guard);
        state.Close();
        EphemeralHostShutdownResult.Incomplete? result;
        if (recipe == "ordered-host")
        {
            using var observation = new CancellationTokenSource();
            var stopping = owner.Host!.StopAsync(observation.Token);
            await state.CancellationObserved.Task.WaitAsync(Guard);
            observation.Cancel();
            var error = await Error(async () => await stopping.WaitAsync(Guard));
            result = FindIncomplete(error);
            Check(result is not null, "Generic Host must surface typed incomplete stop evidence");
        }
        else
        {
            // An expired local drain observation does not call engine stop.
            using var localDrain = new CancellationTokenSource(); localDrain.Cancel();
            Check(await Error(async () => await state.Pending.WaitAsync(localDrain.Token)) is OperationCanceledException, "local drain budget only ends the local observation");
            Check(!state.CancellationObserved.Task.IsCompleted, "drain expiry alone must not cancel the workflow");
            result = await owner.Lifetime.StopAsync(TimeSpan.Zero) as EphemeralHostShutdownResult.Incomplete;
        }
        Check(result is not null && result.PendingWorkCount > 0, "a blocked physical body must keep a positive obligation");
        Check(owner.DependenciesAlive && !state.BodyCompleted, "Incomplete must retain the actual host/provider and dependencies");
        var retainedCount = result!.PendingWorkCount;
        state.Release.TrySetResult();
        var startError = await Error(async () => await state.Pending.WaitAsync(Guard));
        Check(startError is EphemeralHostStoppingException, "still-unaccepted starts must lose to Stopping");
        Check(await owner.Lifetime.StopAsync(Timeout.InfiniteTimeSpan).AsTask().WaitAsync(Guard) is EphemeralHostShutdownResult.Completed, "a fresh observation must join after physical return");
        Check(result.PendingWorkCount == retainedCount, "Incomplete is detached from later runtime progress");
        await owner.Finish();
        Check(owner.DependenciesDisposedSafely, "the retained dependency must be disposed only after join and remaining hosted stops");
    }

    internal static async Task LocalWaitCancellation()
    {
        var state = new Session("delay"); var owner = Create("plain-di", state);
        var start = await owner.Registry.Register(Definition("delay")).GetHandleOrThrow().StartOrGetAsync("output", StartIdempotencyKey.Create("wait"));
        using var local = new CancellationTokenSource();
        var cancelledWait = start.GetHandleOrThrow().WaitForOutputAsync(local.Token).AsTask(); local.Cancel();
        Check(await Error(async () => await cancelledWait.WaitAsync(Guard)) is OperationCanceledException, "wait cancellation must only detach that observer");
        var fresh = start.GetHandleOrThrow().WaitForOutputAsync().AsTask();
        state.Release.TrySetResult(); state.Clock.Advance(TimeSpan.FromSeconds(10));
        Check(await fresh.WaitAsync(Guard) == "output", "fresh output observation must still succeed");
        Check(await owner.Lifetime.StopAsync(Timeout.InfiniteTimeSpan).AsTask().WaitAsync(Guard) is EphemeralHostShutdownResult.Completed, "wait cancellation cannot poison shutdown");
        await owner.Finish();
    }

    internal static async Task ConcurrentStops()
    {
        var state = new Session("natural");
        var owner = Create("ordered-host", state, concurrent: true);
        await owner.StartHost();
        state.Pending = owner.Registry.Register(Definition("natural")).GetHandleOrThrow().StartOrGetAsync("concurrent", StartIdempotencyKey.Create("concurrent")).AsTask();
        await state.Entered.Task.WaitAsync(Guard);
        var stop = owner.Host!.StopAsync();
        await state.DrainerEntered.Task.WaitAsync(Guard);
        await state.CancellationObserved.Task.WaitAsync(Guard);
        Check(owner.DependenciesAlive, "concurrent hosted stops still do not own disposal");
        state.Release.TrySetResult();
        var error = await Error(async () => await stop.WaitAsync(Guard));
        Check(error is EphemeralHostStoppingException || error is AggregateException aggregate && aggregate.Flatten().InnerExceptions.Any(item => item is EphemeralHostStoppingException), "concurrent stops cannot promise acceptance during a drain window");
        Check(await owner.Lifetime.StopAsync(Timeout.InfiniteTimeSpan).AsTask().WaitAsync(Guard) is EphemeralHostShutdownResult.Completed, "the shared operation still joins");
        state.SkipDrain = true;
        await owner.Finish();
    }

    internal static async Task RunAsyncNegative()
    {
        var state = new Session("natural") { SkipDrain = true };
        var owner = Create("ordered-host", state, shutdownBudget: TimeSpan.FromMilliseconds(100));
        var application = owner.Host!.Services.GetRequiredService<IHostApplicationLifetime>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = application.ApplicationStarted.Register(() => started.TrySetResult());
        state.Pending = owner.Registry.Register(Definition("natural")).GetHandleOrThrow()
            .StartOrGetAsync("run-negative", StartIdempotencyKey.Create("run-negative")).AsTask();
        await state.Entered.Task.WaitAsync(Guard);
        using var shutdown = new CancellationTokenSource();
        var running = owner.Host.RunAsync(shutdown.Token);
        await started.Task.WaitAsync(Guard);
        shutdown.Cancel();
        var error = await Error(async () => await running.WaitAsync(Guard));
        Check(FindIncomplete(error) is { PendingWorkCount: > 0 }, "RunAsync must surface the incomplete hosted stop");
        Check(owner.Dependency.Disposed && !state.BodyCompleted, "RunAsync's finally disposes the host even while physical work is incomplete");
        // This is the deliberate negative recipe. It has already lost its owner;
        // finish the body to record the unsafe dependency access, then release the
        // app-owned scope/external resource. It cannot claim a joined disposed root.
        state.Release.TrySetResult();
        Check(await Error(async () => await state.Pending.WaitAsync(Guard)) is not null, "the disposed-root start cannot return Accepted");
        Check(owner.Dependency.UsedAfterDisposal, "the negative recipe must expose the unsafe use that explicit ownership prevents");
        owner.FinishNegative();
    }

    private static EphemeralWorkflowDefinition<string, string> Definition(string mode)
    {
        var root = Workflow.Ephemeral<State>(DefinitionId.Parse(Guid.NewGuid().ToString()), DefinitionVersion.Initial).Init<string>(input => new State(input));
        if (mode == "delay") root.Delay(TimeSpan.FromSeconds(10));
        var step = root.Then<Body>();
        if (mode == "retry") step.WithRetry(2, TimeSpan.FromSeconds(10));
        return step.End(snapshot => snapshot.Value.Value, WorkflowOutcomeName.Create("done")).Build();
    }

    private static Owner Create(string recipe, Session session, bool concurrent = false, TimeSpan? shutdownBudget = null)
    {
        void Configure(IServiceCollection services)
        {
            services.AddSingleton(session).AddSingleton<TimeProvider>(session.Clock).AddSingleton<Sentinel>()
                .AddScoped<ScopeSentinel>().AddSingleton(session.External).AddSingleton<Body>();
            services.AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions
            {
                StructuredExecution = new StructuredExecutionHostOptions { MaxConcurrentExecutionPathsPerInstance = 2, StepThrottles = [] },
                TransientPools = []
            });
            services.Configure<HostOptions>(options => { options.ServicesStopConcurrently = concurrent; options.ShutdownTimeout = shutdownBudget ?? TimeSpan.FromSeconds(30); });
            if (recipe == "ordered-host") services.AddHostedService<Drainer>(); // Reverse stop order: drainer then engine.
        }
        if (recipe != "plain-di")
        {
            var host = new HostBuilder().ConfigureServices((_, services) => Configure(services)).Build();
            return new Owner(host.Services, host, null);
        }
        var collection = new ServiceCollection(); Configure(collection);
        var provider = collection.BuildServiceProvider(); return new Owner(provider, null, provider);
    }

    private static async Task<Exception?> Error(Func<Task> action) { try { await action(); return null; } catch (Exception error) { return error; } }
    private static EphemeralHostShutdownResult.Incomplete? FindIncomplete(Exception? error) => error switch
    {
        EphemeralHostShutdownIncompleteException incomplete => incomplete.Result,
        AggregateException aggregate => aggregate.Flatten().InnerExceptions.Select(FindIncomplete).FirstOrDefault(value => value is not null),
        _ => null
    };

    private sealed class Owner
    {
        private readonly IServiceProvider services;
        private readonly IHost? host;
        private readonly ServiceProvider? provider;
        private readonly IServiceScope scope;
        private readonly ScopeSentinel scoped;
        private readonly ExternalSentinel external;
        internal Owner(IServiceProvider services, IHost? host, ServiceProvider? provider)
        {
            this.services = services; this.host = host; this.provider = provider;
            Dependency = services.GetRequiredService<Sentinel>();
            external = services.GetRequiredService<ExternalSentinel>();
            scope = services.CreateScope();
            scoped = scope.ServiceProvider.GetRequiredService<ScopeSentinel>();
            services.GetRequiredService<Session>().Scope = scoped;
            Check(ReferenceEquals(Lifetime, scope.ServiceProvider.GetRequiredService<IEphemeralWorkflowHostLifetime>()), "child scopes must resolve the same root lifetime");
        }
        internal IHost? Host => host;
        internal IWorkflowDefinitionRegistry Registry => services.GetRequiredService<IWorkflowDefinitionRegistry>();
        internal IEphemeralWorkflowHostLifetime Lifetime => services.GetRequiredService<IEphemeralWorkflowHostLifetime>();
        internal Sentinel Dependency { get; }
        internal bool DependenciesAlive => !Dependency.Disposed && !scoped.Disposed && !external.Disposed;
        internal bool DependenciesDisposedSafely => Dependency.Disposed && scoped.Disposed && external.Disposed &&
            !Dependency.UsedAfterDisposal && !scoped.UsedAfterDisposal && !external.UsedAfterDisposal;
        internal Task StartHost() => host is null ? Task.CompletedTask : host.StartAsync();
        internal void FinishNegative() { scope.Dispose(); external.Dispose(); host!.Dispose(); }
        internal async Task Finish()
        {
            if (host is not null) await host.StopAsync().WaitAsync(Guard);
            scope.Dispose();
            Check(!external.Disposed, "the externally supplied dependency is owned by the application");
            external.Dispose();
            if (host is not null) host.Dispose(); else provider!.Dispose();
        }
    }
}

internal sealed record State(string Value);

internal sealed class Session(string mode)
{
    internal string Mode => mode;
    internal ScopeSentinel Scope = null!;
    internal ExternalSentinel External { get; } = new();
    private readonly object submissionGate = new();
    private bool accepting = true;
    internal bool SkipDrain, BodyCompleted;
    internal string? DrainedOutput;
    internal Task<WorkflowStartResult<WorkflowInstanceHandle<string>>> Pending = null!;
    internal Task<WorkflowStartResult<WorkflowInstanceHandle<string>>> Submit(Func<ValueTask<WorkflowStartResult<WorkflowInstanceHandle<string>>>> start)
    {
        lock (submissionGate)
        {
            if (!accepting) throw new InvalidOperationException("application admission closed");
            return Pending = start().AsTask();
        }
    }
    internal void Close() { lock (submissionGate) accepting = false; }
    internal ManualClock Clock { get; } = new();
    internal TaskCompletionSource Entered { get; } = NewSignal();
    internal TaskCompletionSource Release { get; } = NewSignal();
    internal TaskCompletionSource Event { get; } = NewSignal();
    internal TaskCompletionSource CancellationObserved { get; } = NewSignal();
    internal TaskCompletionSource DrainerEntered { get; } = NewSignal();
    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}

internal class Sentinel : IDisposable
{
    internal bool Disposed, UsedAfterDisposal;
    public Sentinel() { }
    internal void Touch() { if (Disposed) { UsedAfterDisposal = true; throw new InvalidOperationException("dependency used after disposal"); } }
    public void Dispose() => Disposed = true;
}

internal sealed class ScopeSentinel : Sentinel { public ScopeSentinel() { } }
internal sealed class ExternalSentinel : Sentinel { public ExternalSentinel() { } }

internal sealed class Body(Session session, Sentinel dependency) : IStep<State>
{
    public async ValueTask<StepResult> ExecuteAsync(StepContext<State> context, CancellationToken cancellationToken)
    {
        dependency.Touch(); session.Scope.Touch(); session.External.Touch();
        if (session.Mode == "retry" && context.Execution.AttemptNumber == 1) return new StepResult.Failed(new Transient());
        using var registration = cancellationToken.Register(() => session.CancellationObserved.TrySetResult());
        session.Entered.TrySetResult();
        await (session.Mode == "application-event" ? session.Event.Task : session.Release.Task);
        dependency.Touch(); session.Scope.Touch(); session.External.Touch(); session.BodyCompleted = true;
        return new StepResult.Completed();
    }
    private sealed class Transient() : OrcaCoreException("WF-CONSUMER-TRANSIENT", "retry during Closed");
}

internal sealed class Drainer(Session session) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        session.Close();
        session.DrainerEntered.TrySetResult();
        if (session.SkipDrain) return;
        try
        {
            var start = await session.Pending.WaitAsync(cancellationToken);
            session.DrainedOutput = await start.GetHandleOrThrow().WaitForOutputAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { /* Shared host budget expired; yield to engine stop. */ }
    }
}

internal sealed class ManualClock : TimeProvider
{
    private readonly object gate = new();
    private readonly List<ManualTimer> timers = [];
    private long ticks;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() { lock (gate) return ticks; }
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(GetTimestamp());
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        lock (gate) { var timer = new ManualTimer(this, callback, state); timers.Add(timer); timer.Change(dueTime, period); return timer; }
    }
    internal void Advance(TimeSpan duration)
    {
        Action[] due;
        lock (gate) { ticks += duration.Ticks; due = timers.Where(timer => !timer.Disposed && timer.Due <= ticks).Select(timer => timer.Take()).ToArray(); }
        foreach (var callback in due) callback();
    }
    private sealed class ManualTimer(ManualClock owner, TimerCallback callback, object? state) : ITimer
    {
        internal bool Disposed;
        internal long Due = long.MaxValue;
        private TimeSpan period;
        public bool Change(TimeSpan dueTime, TimeSpan newPeriod)
        {
            lock (owner.gate) { if (Disposed) return false; period = newPeriod; Due = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : owner.ticks + dueTime.Ticks; return true; }
        }
        internal Action Take() { Due = period > TimeSpan.Zero ? owner.ticks + period.Ticks : long.MaxValue; return () => callback(state); }
        public void Dispose() { lock (owner.gate) { Disposed = true; Due = long.MaxValue; } }
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
