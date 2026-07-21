using OrcaCore;

var definition = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
    .Init<Input>(input => new State(input.Value))
    .Then(context => { context.ReplaceState(context.State with { Value = context.State.Value + 1 }); return ValueTask.CompletedTask; })
    .End(snapshot => new Output(snapshot.Value.Value), WorkflowOutcomeName.Create("completed"))
    .Build();

async ValueTask<Output> RunAsync(IWorkflowDefinitionRegistry registry, Input input, CancellationToken token)
{
    var registration = registry.Register(definition);
    var handle = registration.GetHandleOrThrow();
    var start = await handle.StartOrGetAsync(input, StartIdempotencyKey.Create("ephemeral-start"), token);
    var output = await start.WaitForOutputAsync(token);
    var reopened = await handle.GetInstanceAsync(start.GetHandleOrThrow().InstanceId, token);
    _ = await reopened.GetSnapshotAsync(token);
    return output;
}

internal sealed record Input(int Value);
internal sealed record State(int Value);
internal sealed record Output(int Value);
