using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrcaCore;
using OrcaCore.Dag;
using OrcaCore.Dag.Hosting;
using OrcaCore.Hosting;
using OrcaCore.Providers.InMemory;

using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
var token = deadline.Token;
var resultful = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial)
    .Init<NodeInput>(input => input.Value).End(state => new NodeOutput(state.Value + 1)).Build();
var resultless = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial)
    .Init<NodeInput>(input => input.Value).End().Build();
var waiting = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial)
    .Init<int>(input => input).Then<SignalStarted>()
    .Wait(WorkflowEventContract.Create(EventName.Create("release"), EventContractVersion.Initial),
        _ => CorrelationId.Create("held-child"))
    .End(state => state.Value).Build();
var started = new SignalStarted();
var services = new ServiceCollection();
services.AddSingleton(started);
services.AddOrcaCoreInMemoryDurableProvider();
services.AddOrcaCoreDurableEngine(new DurableEngineHostOptions
{
    StructuredExecution = new() { MaxConcurrentExecutionPathsPerInstance = 2, StepThrottles = [] },
    ResourcePools = new() { PartitionId = ResourceGovernancePartitionId.Create("packed-dag"), Pools = [] }
}).AddWorkflow(resultful).AddWorkflow(resultless).AddWorkflow(waiting);
services.AddOrcaCoreDag(new DagHostOptions { MaxConcurrentNodes = 2 });
await using var provider = services.BuildServiceProvider();
var running = new Stack<IHostedService>();
try
{
    foreach (var service in provider.GetServices<IHostedService>())
    {
        await service.StartAsync(token);
        running.Push(service);
    }
    var registry = provider.GetRequiredService<IDagDefinitionRegistry>();
    var dag = Dag.Define<RunInput>(DefinitionId.New(), DefinitionVersion.Initial);
    var produce = dag.Node(DagNodeId.Create("produce"), resultful.Reference)
        .MapInput(context => new NodeInput(context.RunInput.Value));
    var consume = dag.Node(DagNodeId.Create("consume"), resultful.Reference)
        .DependsOn(produce).MapInput(context => new NodeInput(context.OutputOf(produce).Value));
    dag.Node(DagNodeId.Create("independent"), resultless.Reference)
        .MapInput(context => new NodeInput(context.RunInput.Value));
    var definition = registry.Register(dag.Build()).GetHandleOrThrow();
    var key = StartIdempotencyKey.Create("packed-success");
    var start = await definition.StartOrGetAsync(new RunInput(40), key, token);
    if (start is DagStartResult.RunInputConflict conflict)
        throw new InvalidOperationException($"Run {conflict.ExistingRunId} already owns this key with another input contract.");
    Require(start is DagStartResult.Accepted { WasExisting: false }, "Fresh DAG start was not accepted.");
    var run = start.GetHandleOrThrow();
    var replay = await definition.StartOrGetAsync(new RunInput(40), key, token);
    Require(replay is DagStartResult.Accepted { WasExisting: true } && replay.GetHandleOrThrow().RunId.Equals(run.RunId),
        "Replaying the same start key did not return its original run.");
    Require(await definition.StartOrGetAsync(new RunInput(99), key, token) is DagStartResult.Conflict { Error: not null },
        "Changed input reused an existing start key.");
    var reopened = await definition.GetRunAsync(run.RunId, token);
    var terminal = await reopened.WaitForTerminalAsync(token);
    Require(terminal.RunId.Equals(run.RunId) && terminal.Status == DagRunStatus.Succeeded &&
        terminal.Nodes.Count == 3 && terminal.Nodes.All(node => node.Status == DagNodeStatus.Succeeded),
        "The reopened run did not complete all three nodes.");
    Require(await reopened.GetOutputAsync(consume, token) is DagNodeOutputResult<NodeOutput>.Available { Output.Value: 42 },
        "The dependent node did not return 42.");

    var heldDag = Dag.Define<int>(DefinitionId.New(), DefinitionVersion.Initial);
    var heldNode = heldDag.Node(DagNodeId.Create("held"), waiting.Reference).MapInput(context => context.RunInput);
    var heldDefinition = registry.Register(heldDag.Build()).GetHandleOrThrow();
    var held = (await heldDefinition.StartOrGetAsync(1, StartIdempotencyKey.Create("packed-cancel"), token)).GetHandleOrThrow();
    await started.Entered.Task.WaitAsync(token);
    var observer = await heldDefinition.GetRunAsync(held.RunId, token);
    var completion = observer.WaitForTerminalAsync(token);
    Require((await observer.GetSnapshotAsync(token)).Status == DagRunStatus.Running, "Held run was not running.");
    Require(await held.RequestCancellationAsync(token) == DagCancellationRequestStatus.Requested,
        "Cancellation of the running DAG was not accepted.");
    var cancelled = await completion;
    Require(cancelled.RunId.Equals(held.RunId) && cancelled.Status == DagRunStatus.Cancelled &&
        cancelled.Nodes.Single().Status == DagNodeStatus.Cancelled, "Reopened observer did not see run and child cancellation.");
    Require(await observer.GetOutputAsync(heldNode, token) is DagNodeOutputResult<int>.Unavailable { Status: DagNodeStatus.Cancelled },
        "A cancelled node exposed an output.");
    Console.WriteLine("Packed DAG verified: dependent output 42 and cancelled child.");
}
finally
{
    using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    foreach (var service in running) await service.StopAsync(stop.Token);
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

internal sealed record RunInput(int Value);
internal sealed record NodeInput(int Value);
internal sealed record NodeOutput(int Value);

internal sealed class SignalStarted : IStep<int>
{
    internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ValueTask<StepResult> ExecuteAsync(StepContext<int> context, CancellationToken cancellationToken)
    {
        Entered.TrySetResult();
        return ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }
}
