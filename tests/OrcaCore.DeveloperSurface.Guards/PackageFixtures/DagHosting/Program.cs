using OrcaCore;
using OrcaCore.Dag;

static (WorkflowDagPlan<RunInput> Plan, DagNodeRef<NodeOutput> OutputNode) BuildPlan(
    DurableWorkflowRef<NodeInput, NodeOutput> resultful,
    DurableWorkflowRef<NodeInput> resultless)
{
    var dag = Dag.Define<RunInput>(DefinitionId.New(), DefinitionVersion.Initial);
    var produce = dag.Node(DagNodeId.Create("produce"), resultful)
        .MapInput(context => new NodeInput(context.RunInput.Value));
    _ = dag.Node(DagNodeId.Create("consume"), resultless)
        .DependsOn(produce)
        .MapInput(context => new NodeInput(context.OutputOf(produce).Value));
    _ = dag.Node(DagNodeId.Create("independent"), resultless)
        .MapInput(context => new NodeInput(context.RunInput.Value));
    return (dag.Build(), produce);
}

static async ValueTask<DagRunSnapshot> RunAsync(
    IDagDefinitionRegistry registry,
    WorkflowDagPlan<RunInput> plan,
    RunInput input,
    CancellationToken token)
{
    var definition = registry.Register(plan).GetHandleOrThrow();
    var start = await definition.StartOrGetAsync(input, StartIdempotencyKey.Create("dag-start"), token);
    var run = start.GetHandleOrThrow();
    _ = start switch
    {
        DagStartResult.Accepted accepted => accepted.WasExisting,
        DagStartResult.Conflict conflict => conflict.Error is not null,
        DagStartResult.RunInputConflict conflict => conflict.ExistingRunId == run.RunId,
        _ => throw new InvalidOperationException()
    };
    _ = await definition.GetRunAsync(run.RunId, token);
    _ = await run.GetSnapshotAsync(token);
    _ = await run.GetOutputAsync((DagNodeRef<NodeOutput>)plan.Nodes[0], token);
    _ = await run.RequestCancellationAsync(token);
    return await run.WaitForTerminalAsync(token);
}

_ = (Func<
    DurableWorkflowRef<NodeInput, NodeOutput>,
    DurableWorkflowRef<NodeInput>,
    (WorkflowDagPlan<RunInput> Plan, DagNodeRef<NodeOutput> OutputNode)>)BuildPlan;
_ = (Func<
    IDagDefinitionRegistry,
    WorkflowDagPlan<RunInput>,
    RunInput,
    CancellationToken,
    ValueTask<DagRunSnapshot>>)RunAsync;

internal sealed record RunInput(int Value);
internal sealed record NodeInput(int Value);
internal sealed record NodeOutput(int Value);
