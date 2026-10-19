using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore;
using OrcaCore.Hosting;

namespace RetentionConsumer;

internal static class Program
{
    internal const int PayloadBytes = 48 * 1024;
    internal const int Retained = 16;
    private static readonly CancellationToken Token = CancellationToken.None;

    private static async Task Main(string[] args)
    {
        if (args.Single() == "behavior") { await Behavior(); return; }
        var bounded = args.Single() == "bounded";
        using var provider = Host(bounded ? EphemeralTerminalRetentionPolicy.Create(Retained, TimeSpan.FromHours(1), TimeSpan.FromSeconds(1)) : null);
        var registry = provider.GetRequiredService<IWorkflowDefinitionRegistry>();
        var definitions = Enumerable.Range(0, 2).Select(_ => registry.Register(Definition()).GetHandleOrThrow()).ToArray();
        var samples = new List<WorkflowInstanceHandle<ResidentOutput>>();
        var callerCopies = new List<WeakReference>();
        WorkflowInstanceHandle<ResidentOutput>? last = null;
        Checkpoint("empty", 0, bounded, samples.Count, callerCopies);
        for (var sequence = 0; sequence < 1024; sequence++)
        {
            var completed = Run(definitions[sequence % 2], new(sequence, "finite"), Key(sequence));
            last = completed.Handle;
            if (sequence < 8) { samples.Add(completed.Handle); callerCopies.Add(completed.CallerCopy); }
            if (sequence is 511 or 1023)
            {
                var before = Volatile.Read(ref EffectStep.Effects);
                var replay = await definitions[sequence % 2].StartOrGetAsync(new(sequence, "finite"), Key(sequence), Token);
                Check(replay is WorkflowStartResult<WorkflowInstanceHandle<ResidentOutput>>.Accepted { WasExisting: true }, "retained replay");
                Check(replay.GetHandleOrThrow().InstanceId == last.InstanceId && EffectStep.Effects == before, "replay did not rerun the step");
                Checkpoint($"jobs-{sequence+1}", sequence+1, bounded, samples.Count, callerCopies);
            }
        }
        var old = samples[0];
        var effectsBefore = EffectStep.Effects;
        var reuse = await definitions[0].StartOrGetAsync(new(0, "finite"), Key(0), Token);
        var accepted = reuse as WorkflowStartResult<WorkflowInstanceHandle<ResidentOutput>>.Accepted ?? throw new Exception("reuse was not accepted");
        Check(accepted.WasExisting == !bounded, "expiry controls start replay");
        Check((accepted.Handle.InstanceId == old.InstanceId) == !bounded, "fresh ID after expiry");
        Check(EffectStep.Effects == effectsBefore + (bounded ? 1 : 0), "fresh start repeats the physical effect");
        if (bounded) await Missing(old);
        else Check((await old.GetSnapshotAsync()).Status == WorkflowInstanceStatus.Completed, "omitted policy retains old handles");
        Emit(new { kind="result", bounded, jobs=1024, definitions=2, heldHandles=samples.Count,
            retainedReplay=true, keyReuse=bounded ? "fresh ID and repeated effect" : "same ID and no repeated effect",
            oldId=old.InstanceId.ToString(), newId=accepted.Handle.InstanceId.ToString(), effects=EffectStep.Effects });
        GC.KeepAlive(samples);
    }

    private static ServiceProvider Host(EphemeralTerminalRetentionPolicy? policy)
    {
        var services = new ServiceCollection();
        services.AddSingleton<PhysicalGate>();
        services.AddTransient<EffectStep>();
        services.AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions
        {
            TerminalRetention=policy,
            StructuredExecution=new StructuredExecutionHostOptions {MaxConcurrentExecutionPathsPerInstance=2,StepThrottles=[]},
            TransientPools=[]
        });
        return services.BuildServiceProvider();
    }

    private static EphemeralWorkflowDefinition<JobInput, ResidentOutput> Definition(bool timeout=false, bool waiting=false)
    {
        var root = Workflow.Ephemeral<ResidentState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<JobInput>(input => new ResidentState(input.Sequence, input.Kind, new byte[PayloadBytes],
                new ResidentOutput(input.Sequence, new byte[PayloadBytes])));
        if (waiting) root.Wait(WorkflowEventContract.Create(EventName.Create("consumer-continue"), EventContractVersion.Initial), _ => CorrelationId.Create("consumer-wait"));
        var step = root.Then<EffectStep>();
        if (timeout) step.WithStepTimeout(TimeSpan.FromMilliseconds(100));
        return step.End(state => state.Value.Output).Build();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WorkflowInstanceHandle<ResidentOutput> Handle, WeakReference CallerCopy) Run(
        EphemeralDefinitionHandle<JobInput, ResidentOutput> definition, JobInput input, StartIdempotencyKey key)
    {
        var handle = definition.StartOrGetAsync(input, key, Token).AsTask().GetAwaiter().GetResult().GetHandleOrThrow();
        var output = handle.WaitForOutputAsync(Token).AsTask().GetAwaiter().GetResult();
        Check(output.Sequence==input.Sequence && output.Bytes.Length==PayloadBytes && output.Bytes[17]==(byte)((17*31+input.Sequence)%251), "detached output payload");
        return (handle, new WeakReference(output));
    }

    private static void Checkpoint(string phase, int jobs, bool bounded, int handles, List<WeakReference> copies)
    {
        // Diagnostic GC is part of this measurement only; it is never product behavior.
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Check(copies.All(copy => !copy.IsAlive), "caller output copies were dropped");
        using var process=Process.GetCurrentProcess();
        Emit(new { kind="checkpoint", phase, jobs, bounded, heldHandles=handles, pid=Environment.ProcessId,
            statePayloadBytes=PayloadBytes, outputPayloadBytes=PayloadBytes,
            allocated=GC.GetTotalAllocatedBytes(true), managedBytes=GC.GetTotalMemory(false),
            heapBytes=GC.GetGCMemoryInfo().HeapSizeBytes, privateBytes=process.PrivateMemorySize64,
            workingSet=process.WorkingSet64, diagnosticGc=true, callerCopiesAlive=copies.Count(copy=>copy.IsAlive) });
        Check(Console.ReadLine()=="continue", "measurement controller acknowledged the checkpoint");
    }

    private static async Task Behavior()
    {
        using var observations = new Evictions();
        using var provider = Host(EphemeralTerminalRetentionPolicy.Create(Retained, TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(20)));
        var registry=provider.GetRequiredService<IWorkflowDefinitionRegistry>();
        var finite=registry.Register(Definition()).GetHandleOrThrow();
        var stuck=registry.Register(Definition(timeout:true)).GetHandleOrThrow();
        var waiting=registry.Register(Definition(waiting:true)).GetHandleOrThrow();
        var pending=await waiting.StartOrGetAsync(new(-2,"waiting"),StartIdempotencyKey.Create("waiting"));
        var waitingHandle=pending.GetHandleOrThrow();
        Check((await waitingHandle.GetSnapshotAsync()).Status==WorkflowInstanceStatus.Waiting,"waiting fixture");
        var start=stuck.StartOrGetAsync(new(-1,"stuck"),StartIdempotencyKey.Create("stuck")).AsTask();
        var gates=provider.GetRequiredService<PhysicalGate>();
        await gates.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
        var stuckHandle=(await start.WaitAsync(TimeSpan.FromSeconds(30))).GetHandleOrThrow();
        try { await stuckHandle.WaitForOutputAsync(); throw new Exception("timeout expected"); }
        catch(WorkflowOutputUnavailableException error) { Check(error.Status==WorkflowInstanceStatus.Failed,"logical timeout"); }
        for(var i=0;i<64;i++) Run(finite,new(i,"finite"),Key(i));
        // No further workflow calls until the background timer has expired every finite job.
        await observations.Wait(64);
        Check((await waitingHandle.GetSnapshotAsync()).Status==WorkflowInstanceStatus.Waiting,"waiting instance protected beyond age and count");
        Check((await stuckHandle.GetSnapshotAsync()).Status==WorkflowInstanceStatus.Failed,"unsettled terminal protected beyond age and count");
        var protectedReplay=await stuck.StartOrGetAsync(new(-1,"stuck"),StartIdempotencyKey.Create("stuck"));
        Check(protectedReplay is WorkflowStartResult<WorkflowInstanceHandle<ResidentOutput>>.Accepted {WasExisting:true},"protected key remains bound");
        Check(protectedReplay.GetHandleOrThrow().InstanceId==stuckHandle.InstanceId,"protected identity");
        gates.Release.TrySetResult();
        await observations.Wait(65);
        await Missing(stuckHandle);
        await waitingHandle.TerminateAsync();
        await observations.Wait(66);
        await Missing(waitingHandle);
        Emit(new {kind="result", scenario="behavior", idleAge=true, waitingProtected=true,
            physicalWorkProtected=true, protectedReplay=true, releasedAfterPhysicalReturn=true,
            finiteJobs=64, evictions=observations.Count});
    }

    private static async Task Missing(WorkflowInstanceHandle handle)
    {
        try { await handle.GetSnapshotAsync(); throw new Exception("not-found expected"); }
        catch(WorkflowInstanceNotFoundException error) { Check(error.InstanceId==handle.InstanceId,"not-found preserves original ID"); }
    }
    private static StartIdempotencyKey Key(int sequence)=>StartIdempotencyKey.Create($"job-{sequence}");
    private static void Check(bool value,string message) { if(!value) throw new Exception(message); }
    private static void Emit(object value) { Console.WriteLine(JsonSerializer.Serialize(value)); Console.Out.Flush(); }
}

public sealed record JobInput(int Sequence,string Kind);
public sealed record ResidentOutput(int Sequence,byte[] Bytes);
public sealed record ResidentState(int Sequence,string Kind,byte[] Payload,ResidentOutput Output);
public sealed class PhysicalGate
{
    public TaskCompletionSource Entered {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
}
public sealed class EffectStep(PhysicalGate gate):IStep<ResidentState>
{
    public static int Effects;
    public async ValueTask<StepResult> ExecuteAsync(StepContext<ResidentState> context,CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref Effects);
        if(context.State.Kind=="stuck") {gate.Entered.TrySetResult(); await gate.Release.Task;}
        for(var i=0;i<context.State.Payload.Length;i++) context.State.Payload[i]=(byte)((i*17+context.State.Sequence)%251);
        for(var i=0;i<context.State.Output.Bytes.Length;i++) context.State.Output.Bytes[i]=(byte)((i*31+context.State.Sequence)%251);
        return new StepResult.Completed();
    }
}
internal sealed class Evictions:IDisposable
{
    private readonly object gate=new();
    private TaskCompletionSource changed=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly MeterListener listener=new();
    internal int Count {get;private set;}
    internal Evictions()
    {
        listener.InstrumentPublished=(instrument,current)=>{if(instrument.Meter.Name=="OrcaCore.Engine.Ephemeral" && instrument.Name=="orca.lifecycle.events")current.EnableMeasurementEvents(instrument);};
        listener.SetMeasurementEventCallback<long>((_,value,tags,_)=>
        {
            if(!tags.ToArray().Any(tag=>tag.Key=="event.name" && Equals(tag.Value,"evicted")))return;
            TaskCompletionSource notify;
            lock(gate){Count+=checked((int)value);notify=changed;changed=new(TaskCreationOptions.RunContinuationsAsynchronously);}
            notify.TrySetResult();
        });
        listener.Start();
    }
    internal async Task Wait(int target)
    {
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while(true)
        {
            Task notification;
            lock(gate){if(Count>=target)return;notification=changed.Task;}
            await notification.WaitAsync(deadline.Token);
        }
    }
    public void Dispose()=>listener.Dispose();
}
