using OrcaCore;
using OrcaCore.Hosting;

var fixedDefinitionId = DefinitionId.Parse("53080b19-8ce5-410b-b7fd-612093ff2a03");
var increment = WorkflowEventContract.Create(EventName.Create("increment"), EventContractVersion.Initial);
var definition = Workflow.Ephemeral<State>(fixedDefinitionId, DefinitionVersion.Initial)
    .Init<Input>(input => new State(input.Value, input.Correlation))
    .Wait(increment, state => state.Value.Correlation)
    .Then(context => { context.ReplaceState(context.State with { Value = context.State.Value + 1 }); return ValueTask.CompletedTask; })
    .End(snapshot => new Output(snapshot.Value.Value), WorkflowOutcomeName.Create("completed"))
    .Build();

async ValueTask<Output> RunAsync(
    OrcaCoreEphemeralEngineBuilder catalog,
    IWorkflowDefinitionRegistry registry,
    Input input,
    CancellationToken token)
{
    catalog.AddWorkflow(definition);
    EphemeralWorkflowRef<Input, Output> reference = definition.Reference;
    var handle = registry.GetRequiredHandle(reference);
    var start = await handle.StartOrGetAsync(input, StartIdempotencyKey.Create("ephemeral-start"), token);
    return await start.WaitForOutputAsync(token);
}

static (DefinitionId, DefinitionVersion, DefinitionFingerprint) MissingDefinitionIdentity(
    WorkflowDefinitionNotRegisteredException exception) =>
    (exception.DefinitionId, exception.DefinitionVersion, exception.DefinitionFingerprint);

_ = (Func<OrcaCoreEphemeralEngineBuilder, IWorkflowDefinitionRegistry, Input, CancellationToken, ValueTask<Output>>)RunAsync;
_ = (Func<WorkflowDefinitionNotRegisteredException,
    (DefinitionId, DefinitionVersion, DefinitionFingerprint)>)MissingDefinitionIdentity;


static async ValueTask<string> ObserveEphemeralShutdownAsync(IEphemeralWorkflowHostLifetime lifetime, CancellationToken token)
{
    try
    {
        EphemeralHostShutdownResult result = await lifetime.StopAsync(TimeSpan.Zero, token);
        _ = lifetime.StopAsync(Timeout.InfiniteTimeSpan);
        return result switch
        {
            EphemeralHostShutdownResult.Completed => "completed",
            EphemeralHostShutdownResult.Incomplete pending => pending.Reason switch
            {
                EphemeralHostShutdownIncompleteReason.DeadlineElapsed => $"deadline:{pending.PendingWorkCount}",
                EphemeralHostShutdownIncompleteReason.ObservationCancelled => $"cancelled:{pending.PendingWorkCount}",
                EphemeralHostShutdownIncompleteReason.ShutdownFaulted => $"faulted:{pending.PendingWorkCount}",
                _ => throw new InvalidOperationException()
            },
            _ => throw new InvalidOperationException()
        };
    }
    catch (EphemeralHostStoppingException stopping) { return stopping.Code; }
    catch (EphemeralHostShutdownIncompleteException incomplete) { return incomplete.Result.Reason.ToString(); }
}
_ = (Func<IEphemeralWorkflowHostLifetime, CancellationToken, ValueTask<string>>)ObserveEphemeralShutdownAsync;

internal sealed record Input(int Value, CorrelationId Correlation);
internal sealed record State(int Value, CorrelationId Correlation);
internal sealed record Output(int Value);
