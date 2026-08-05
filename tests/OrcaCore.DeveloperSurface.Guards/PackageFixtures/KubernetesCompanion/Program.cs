using OrcaCore;

var lease = ResourceLeaseRequest.Create(
    ResourceLeaseRequirement.Require(ResourcePoolName.Create("kubernetes-submit")));
var definition = Workflow.Durable<JobState>(
        DefinitionId.Parse("a5237c34-087c-4027-a2ae-af4209050ac7"), DefinitionVersion.Initial)
    .Init<JobRequest>(input => new JobState(input, null))
    .AcquireResources(lease, leased => leased
        .Then<SubmitJobStep>()
        .Wait(EventContracts.JobTerminal, state => state.Value.Request.Correlation)
        .Then<ValidateTerminalJobStep>())
    .End(state => state.Value.Terminal ?? throw new InvalidOperationException("terminal payload required"))
    .Build();

internal sealed record JobRequest(string Namespace, string Name, CorrelationId Correlation);
internal sealed record JobTerminal(string JobUid, string OperationId, string ProtectionToken, bool IsTerminal);
internal sealed record JobState(JobRequest Request, JobTerminal? Terminal);
internal static class EventContracts
{
    internal static readonly WorkflowEventContract<JobTerminal> JobTerminal =
        WorkflowEventContract<JobTerminal>.Create(
            EventName.Create("job-terminal"), EventContractVersion.Initial);
}

internal sealed class SubmitJobStep : IStep<JobState>
{
    public ValueTask<StepResult> ExecuteAsync(StepContext<JobState> context, CancellationToken token)
    {
        _ = context.Execution.OperationId;
        _ = context.ResourceLease?.ProtectionToken ?? throw new InvalidOperationException("lease required");
        return ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }
}

internal sealed class ValidateTerminalJobStep : IStep<JobState>
{
    public ValueTask<StepResult> ExecuteAsync(StepContext<JobState> context, CancellationToken token)
    {
        var payload = context.ResumedEvent?.GetPayload(EventContracts.JobTerminal) ??
            throw new InvalidOperationException("terminal payload required");
        var lease = context.ResourceLease ?? throw new InvalidOperationException("lease required");
        if (!payload.IsTerminal || string.IsNullOrWhiteSpace(payload.JobUid) ||
            payload.OperationId != context.Execution.OperationId.Value ||
            payload.ProtectionToken != lease.ProtectionToken.Value)
            throw new InvalidOperationException("terminal job identity/protection mismatch");
        context.ReplaceState(context.State with { Terminal = payload });
        return ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }
}
