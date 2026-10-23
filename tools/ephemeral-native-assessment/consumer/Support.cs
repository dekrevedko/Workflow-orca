using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrcaCore;
using OrcaCore.Hosting;

public sealed class Session
{
    public bool Block, Retry, FailSecond, BodySettled;
    public int DependencyTouches;
    public ScopedSentinel? Scope;
    public readonly ExternalSentinel External = new();
    public readonly ManualClock Clock = new();
    public readonly ConcurrentQueue<(string Operation, int Attempt)> Attempts = new();
    public readonly ConcurrentQueue<int> ItemsStarted = new();
    public readonly ConcurrentQueue<int> ItemsFinished = new();
    public readonly TaskCompletionSource Entered = Signal(), Release = Signal(), Settled = Signal(), CancellationObserved = Signal(), TwoEntered = Signal();
    public readonly TaskCompletionSource[] ItemEntered = Enumerable.Range(0, 3).Select(_ => Signal()).ToArray();
    public readonly TaskCompletionSource[] ItemFinished = Enumerable.Range(0, 3).Select(_ => Signal()).ToArray();
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}

public class Sentinel : IDisposable
{
    public bool Disposed, UsedAfterDisposal;
    public void Touch()
    {
        if (Disposed) { UsedAfterDisposal = true; throw new InvalidOperationException("sentinel used after disposal"); }
    }
    public void Dispose() => Disposed = true;
}
public sealed class ScopedSentinel : Sentinel;
public sealed class ExternalSentinel : Sentinel;

public sealed class Owner : IAsyncDisposable
{
    private readonly Session session;
    private readonly IHost host;
    private readonly IServiceScope scope;
    private readonly Sentinel dependency;
    private readonly ScopedSentinel scoped;
    public IWorkflowDefinitionRegistry Registry { get; }
    public IEphemeralWorkflowHostLifetime Lifetime { get; }
    public bool Disposed { get; private set; }
    public bool Alive => !Disposed && !dependency.Disposed && !scoped.Disposed && !session.External.Disposed;
    public bool ScopedSharesLifetime => ReferenceEquals(Lifetime, scope.ServiceProvider.GetRequiredService<IEphemeralWorkflowHostLifetime>());

    public Owner(Session session, int pathCeiling = 2)
    {
        this.session = session;
        host = new HostBuilder().ConfigureServices((_, services) =>
        {
            services.AddSingleton(session).AddSingleton<TimeProvider>(session.Clock)
                .AddSingleton<Sentinel>().AddScoped<ScopedSentinel>().AddSingleton(session.External)
                .AddSingleton<ReferenceStep>().AddSingleton<ValueStep>().AddSingleton<StrongStep>().AddSingleton<ItemStep>().AddSingleton<ItemFinishStep>();
            services.AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions
            {
                StructuredExecution = new StructuredExecutionHostOptions
                {
                    MaxConcurrentExecutionPathsPerInstance = pathCeiling, StepThrottles = []
                },
                TransientPools = []
            });
        }).Build();
        dependency = host.Services.GetRequiredService<Sentinel>();
        scope = host.Services.CreateScope();
        scoped = session.Scope = scope.ServiceProvider.GetRequiredService<ScopedSentinel>();
        Lifetime = host.Services.GetRequiredService<IEphemeralWorkflowHostLifetime>();
        Registry = host.Services.GetRequiredService<IWorkflowDefinitionRegistry>();
        host.StartAsync().GetAwaiter().GetResult();
    }
    public void Close() => host.Services.GetRequiredService<IHostApplicationLifetime>().StopApplication();
    public async Task Join()
    {
        var result = await Lifetime.StopAsync(Timeout.InfiniteTimeSpan).AsTask().WaitAsync(Probe.Guard);
        Probe.Check(result is EphemeralHostShutdownResult.Completed, "real physical shutdown join completes");
    }
    public async ValueTask DisposeAsync()
    {
        if (Disposed) return;
        // Failure cleanup releases only application-owned barriers, then joins before disposal.
        session.Release.TrySetResult();
        await Join();
        await host.StopAsync().WaitAsync(Probe.Guard);
        scope.Dispose();
        Probe.Check(!session.External.Disposed, "container must not dispose the externally supplied sentinel");
        session.External.Dispose(); host.Dispose(); Disposed = true;
        Probe.Check(dependency.Disposed && scoped.Disposed && session.External.Disposed, "actual owners are disposed after Completed");
        Probe.Check(!dependency.UsedAfterDisposal && !scoped.UsedAfterDisposal && !session.External.UsedAfterDisposal, "no user dependency accessed after disposal");
    }
}

public sealed class ManualClock : TimeProvider
{
    private readonly object gate = new();
    private readonly List<ManualTimer> timers = [];
    private long ticks;
    private TaskCompletionSource changed = NewSignal();
    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private void Changed() { var old = changed; changed = NewSignal(); old.TrySetResult(); }
    public async Task WaitForDue(TimeSpan absolute)
    {
        while (true)
        {
            Task wait;
            lock (gate)
            {
                if (timers.Any(timer => !timer.Disposed && timer.Due == absolute.Ticks)) return;
                wait = changed.Task;
            }
            await wait;
        }
    }
    private async Task<long> NextDue(CancellationToken token)
    {
        while (true)
        {
            Task wait;
            lock (gate)
            {
                var due = timers.Where(timer => !timer.Disposed).Select(timer => timer.Due).DefaultIfEmpty(long.MaxValue).Min();
                if (due != long.MaxValue) return due;
                wait = changed.Task;
            }
            await wait.WaitAsync(token);
        }
    }
    public async Task Drive(Task completion)
    {
        using var stop = new CancellationTokenSource();
        try
        {
            while (!completion.IsCompleted)
            {
                var next = NextDue(stop.Token);
                if (await Task.WhenAny(completion, next) == completion) break;
                var due = await next;
                Advance(TimeSpan.FromTicks(Math.Max(0, due - GetTimestamp())));
                await Task.Yield();
            }
            await completion;
        }
        finally { stop.Cancel(); }
    }
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() { lock (gate) return ticks; }
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(GetTimestamp());
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        lock (gate)
        {
            var timer = new ManualTimer(this, callback, state); timers.Add(timer);
            timer.Change(dueTime, period); return timer;
        }
    }
    public void Advance(TimeSpan duration)
    {
        Action[] due;
        lock (gate)
        {
            ticks += duration.Ticks;
            due = timers.Where(timer => !timer.Disposed && timer.Due <= ticks).Select(timer => timer.Take()).ToArray();
        }
        foreach (var callback in due) callback();
    }
    private sealed class ManualTimer(ManualClock owner, TimerCallback callback, object? state) : ITimer
    {
        internal bool Disposed;
        internal long Due = long.MaxValue;
        private TimeSpan period;
        public bool Change(TimeSpan dueTime, TimeSpan newPeriod)
        {
            lock (owner.gate)
            {
                if (Disposed) return false;
                period = newPeriod;
                Due = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : owner.ticks + dueTime.Ticks;
                owner.Changed();
                return true;
            }
        }
        internal Action Take()
        {
            Due = period > TimeSpan.Zero ? owner.ticks + period.Ticks : long.MaxValue;
            return () => callback(state);
        }
        public void Dispose() { lock (owner.gate) { Disposed = true; Due = long.MaxValue; } }
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
