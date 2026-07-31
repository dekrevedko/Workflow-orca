using OrcaCore;

var definition = Workflow.Durable<State>(DefinitionId.New(), DefinitionVersion.Initial)
    .Init<Input>(input => new State(input.Correlation))
    .Then<PersistedStep>()
    .Wait(EventName.Create("approved"), state => state.Value.Correlation)
    .Then<ConsumeApprovalStep>()
    .End(state => new Output(state.Value.Correlation.Value), WorkflowOutcomeName.Create("approved"))
    .Build();

async ValueTask<Output> RunAsync(
    IWorkflowDefinitionRegistry registry,
    IWorkflowEventClient events,
    Input input,
    CancellationToken token)
{
    var handle = registry.Register(definition).GetHandleOrThrow();
    var start = await handle.StartOrGetAsync(input, StartIdempotencyKey.Create("durable-start"), token);
    var instance = start.GetHandleOrThrow();
    var envelope = WorkflowEvent.Create(
        EventId.Create("approval-1"), EventName.Create("approved"), input.Correlation, DateTimeOffset.UtcNow);
    _ = await events.DeliverToInstanceAsync(instance.InstanceId, envelope, token);
    return await start.WaitForOutputAsync(token);
}

_ = (Func<IWorkflowDefinitionRegistry, IWorkflowEventClient, Input, CancellationToken, ValueTask<Output>>)RunAsync;

internal sealed record Input(CorrelationId Correlation);
internal sealed record State(CorrelationId Correlation);
internal sealed record Output(string Correlation);
internal sealed class PersistedStep : IStep<State>
{
    public ValueTask<StepResult> ExecuteAsync(StepContext<State> context, CancellationToken token) =>
        ValueTask.FromResult<StepResult>(new StepResult.Completed());
}
internal sealed class ConsumeApprovalStep : IStep<State>
{
    public ValueTask<StepResult> ExecuteAsync(StepContext<State> context, CancellationToken token)
    {
        _ = context.ResumedEvent ?? throw new InvalidOperationException("approval event required");
        return ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }
}
