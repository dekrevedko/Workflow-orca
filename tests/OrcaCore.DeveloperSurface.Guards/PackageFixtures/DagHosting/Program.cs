using OrcaCore;
using OrcaCore.Dag;

static (WorkflowDagPlan<RunInput> Plan, DagNodeRef<NodeOutput> OutputNode) BuildPlan(
    DurableWorkflowRef<NodeInput, NodeOutput> resultful,
    DurableWorkflowRef<NodeInput> resultless)
{
    var dag = WorkflowDag.Define<RunInput>(DefinitionId.New(), DefinitionVersion.Initial);
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
    _ = await run.GetSnapshotAsync(token);
    return await run.WaitForTerminalAsync(token);
}

internal sealed record RunInput(int Value);
internal sealed record NodeInput(int Value);
internal sealed record NodeOutput(int Value);
