using OrcaCore;
using OrcaCore.Durable.Hosting;
using OrcaCore.Hosting;

var fixedDefinitionId = DefinitionId.Parse("48964a6b-aed2-403e-ae8b-93ab96611817");
var definition = Workflow.Durable<State>(fixedDefinitionId, DefinitionVersion.Initial)
    .Init<Input>(input => new State(input.Correlation))
    .Then<PersistedStep>()
    .Wait(EventContracts.Approval, state => state.Value.Correlation)
    .Then<ConsumeApprovalStep>()
    .Publish(EventContracts.Completed, state => state.Value.Correlation,
        state => new Output(state.Value.Correlation.Value))
    .End(state => new Output(state.Value.Correlation.Value), WorkflowOutcomeName.Create("approved"))
    .Build();

async ValueTask<Output> RunAsync(
    OrcaCoreDurableEngineBuilder catalog,
    IWorkflowDefinitionRegistry registry,
    IWorkflowEventIngress ingress,
    Input input,
    CancellationToken token)
{
    catalog.AddWorkflow(definition);
    DurableWorkflowRef<Input, Output> reference = definition.Reference;
    var handle = registry.GetRequiredHandle(reference);
    var start = await handle.StartOrGetAsync(input, StartIdempotencyKey.Create("durable-start"), token);
    var instance = start.GetHandleOrThrow();
    var inbound = WorkflowInboundEvent<Approval>.Create(EventContracts.Approval, EventId.Create("approval-1"),
        input.Correlation, null, DateTimeOffset.UtcNow,
        new WorkflowEventRoute.Direct(instance.InstanceId), new Approval(true));
    _ = await ingress.AcceptAsync(inbound, token);
    return await start.WaitForOutputAsync(token);
}

static async ValueTask<WorkflowEventDispatchResult> DispatchAsync(
    IWorkflowEventDispatcher dispatcher,
    WorkflowOutboundEvent outboundEvent,
    CancellationToken token)
{
    _ = outboundEvent.GetPayload(EventContracts.Completed);
    return await dispatcher.DispatchAsync(outboundEvent, token);
}

_ = (Func<OrcaCoreDurableEngineBuilder, IWorkflowDefinitionRegistry, IWorkflowEventIngress,
    Input, CancellationToken, ValueTask<Output>>)RunAsync;
_ = (Func<IWorkflowEventDispatcher, WorkflowOutboundEvent, CancellationToken,
    ValueTask<WorkflowEventDispatchResult>>)DispatchAsync;

internal sealed record Input(CorrelationId Correlation);
internal sealed record State(CorrelationId Correlation);
internal sealed record Approval(bool Approved);
internal sealed record Output(string Correlation);
internal static class EventContracts
{
    internal static readonly WorkflowEventContract<Approval> Approval = WorkflowEventContract<Approval>.Create(
        EventName.Create("approval-decided"), EventContractVersion.Initial);
    internal static readonly WorkflowEventContract<Output> Completed = WorkflowEventContract<Output>.Create(
        EventName.Create("approval-completed"), EventContractVersion.Initial);
}
internal sealed class PersistedStep : IStep<State>
{
    public ValueTask<StepResult> ExecuteAsync(StepContext<State> context, CancellationToken token) =>
        ValueTask.FromResult<StepResult>(new StepResult.Completed());
}
internal sealed class ConsumeApprovalStep : IStep<State>
{
    public ValueTask<StepResult> ExecuteAsync(StepContext<State> context, CancellationToken token)
    {
        _ = context.ResumedEvent?.GetPayload(EventContracts.Approval) ??
            throw new InvalidOperationException("approval required");
        return ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }
}
