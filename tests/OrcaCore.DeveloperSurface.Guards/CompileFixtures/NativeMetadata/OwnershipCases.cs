using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrcaCore;
using OrcaCore.Hosting;
using System.Text.Json.Serialization;

internal static class OwnershipCases
{
    private static readonly TimeSpan Guard = TimeSpan.FromSeconds(15);
    internal static async Task Run()
    {
        WorkflowJsonMetadata.Register<OwnershipContext>();
        foreach (var bounded in new[] { false, true })
        {
            await Drain(bounded);
            await Abort(bounded, stop: false);
            await Abort(bounded, stop: true);
        }
    }

    private static async Task Drain(bool bounded)
    {
        using var application = new DispatchApplication();
        var body = new DispatchBody();
        await using var services = Provider(body, application, bounded);
        var lifetime = services.GetRequiredService<IEphemeralWorkflowHostLifetime>();
        var handle = services.GetRequiredService<IWorkflowDefinitionRegistry>().Register(Definition()).GetHandleOrThrow();
        var pending = handle.StartOrGetAsync(7, StartIdempotencyKey.Create("drain")).AsTask();
        await body.Entered.Task.WaitAsync(Guard);
        application.StopApplication();
        Check(!body.Cancelled.Task.IsCompleted && !body.Disposed, "Closed preserves in-flight work and dependency ownership");
        Check(await Error(async () => await handle.StartOrGetAsync(8, StartIdempotencyKey.Create("closed"))) is EphemeralHostStoppingException,
            "Closed refuses fresh starts");
        body.Release.TrySetResult();
        var accepted = (await pending.WaitAsync(Guard)).GetHandleOrThrow();
        Check((await accepted.WaitForOutputAsync()).Number == 8, "pre-close initialization drains to accepted detached reference output");
        Check(await lifetime.StopAsync(Timeout.InfiniteTimeSpan).AsTask().WaitAsync(Guard) is EphemeralHostShutdownResult.Completed,
            "shutdown joins all physical work");
        Check(await Error(async () => await handle.StartOrGetAsync(9, StartIdempotencyKey.Create("completed"))) is EphemeralHostStoppingException && body.Calls == 1,
            "Completed never invokes another application body");
    }

    private static async Task Abort(bool bounded, bool stop)
    {
        using var application = new DispatchApplication();
        var body = new DispatchBody { Cooperative = !stop };
        await using var services = Provider(body, application, bounded);
        var lifetime = services.GetRequiredService<IEphemeralWorkflowHostLifetime>();
        var handle = services.GetRequiredService<IWorkflowDefinitionRegistry>().Register(Definition()).GetHandleOrThrow();
        using var caller = new CancellationTokenSource();
        var pending = handle.StartOrGetAsync(7, StartIdempotencyKey.Create("abort"), caller.Token).AsTask();
        await body.Entered.Task.WaitAsync(Guard);
        if (stop)
        {
            Check(await lifetime.StopAsync(TimeSpan.Zero) is EphemeralHostShutdownResult.Incomplete { PendingWorkCount: > 0 },
                "Stopping retains noncooperative physical work");
            await body.Cancelled.Task.WaitAsync(Guard);
        }
        else caller.Cancel();
        try
        {
            if (!stop) Check(await Error(async () => await pending.WaitAsync(Guard)) is OperationCanceledException,
                "caller cancellation fails the provisional start");
            Check(!body.Disposed && (!stop || !body.Settled.Task.IsCompleted), "logical abort never disposes running dependencies");
        }
        finally { body.Release.TrySetResult(); }
        await body.Settled.Task.WaitAsync(Guard);
        await Error(async () => await pending.WaitAsync(Guard));
        Check(await lifetime.StopAsync(Timeout.InfiniteTimeSpan).AsTask().WaitAsync(Guard) is EphemeralHostShutdownResult.Completed,
            "aborted-start physical work eventually joins in both retention modes");
    }

    private static EphemeralWorkflowDefinition<int, DispatchReference> Definition() =>
        Workflow.Ephemeral<DispatchReference>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(number => new DispatchReference(number)).Then<DispatchBody>()
            .End(value => value.Value).Build();

    private static ServiceProvider Provider(DispatchBody body, DispatchApplication application, bool bounded)
    {
        var services = new ServiceCollection();
        services.AddSingleton<DispatchBody>(_ => body).AddSingleton<IHostApplicationLifetime>(application);
        services.AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions
        {
            StructuredExecution = new() { MaxConcurrentExecutionPathsPerInstance = 2, StepThrottles = [] },
            TransientPools = [],
            TerminalRetention = bounded ? EphemeralTerminalRetentionPolicy.Create(1, TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1)) : null
        });
        return services.BuildServiceProvider();
    }

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

public sealed record DispatchReference(int Number);
public sealed class DispatchBody : IStep<DispatchReference>, IDisposable
{
    internal readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal readonly TaskCompletionSource Settled = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal readonly TaskCompletionSource Cancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal bool Cooperative;
    internal int Calls;
    internal bool Disposed;
    public async ValueTask<StepResult> ExecuteAsync(StepContext<DispatchReference> context, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref Calls);
        using var notification = cancellationToken.Register(() => Cancelled.TrySetResult());
        Entered.TrySetResult();
        try
        {
            if (Cooperative) await Release.Task.WaitAsync(cancellationToken);
            else await Release.Task;
            if (Disposed) throw new InvalidOperationException("Dependency disposed before physical settlement.");
            context.ReplaceState(context.State with { Number = context.State.Number + 1 });
            return new StepResult.Completed();
        }
        finally { Settled.TrySetResult(); }
    }
    public void Dispose() => Disposed = true;
}

internal sealed class DispatchApplication : IHostApplicationLifetime, IDisposable
{
    private readonly CancellationTokenSource stopping = new();
    public CancellationToken ApplicationStarted => CancellationToken.None;
    public CancellationToken ApplicationStopping => stopping.Token;
    public CancellationToken ApplicationStopped => CancellationToken.None;
    public void StopApplication() => stopping.Cancel();
    public void Dispose() => stopping.Dispose();
}

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(DispatchReference))]
public partial class OwnershipContext : JsonSerializerContext;
