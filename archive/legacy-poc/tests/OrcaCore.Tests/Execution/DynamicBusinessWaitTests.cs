
namespace OrcaCore.Tests;

public class DynamicBusinessWaitTests
{
    private sealed class DynamicWaitState
    {
        public string Id { get; set; } = "corr-1";
        public bool ShouldWait { get; set; } = true;
        public string? Payload { get; set; }
    }

    private sealed class DynamicWaitStep : IStep<DynamicWaitState>
    {
        public string StepId => "DynamicWait";

        public Task<StepResult> ExecuteAsync(StepContext<DynamicWaitState> context)
        {
            if (context.State.ShouldWait)
            {
                context.State.ShouldWait = false;
                return Task.FromResult<StepResult>(
                    new StepResult.WaitForEvent("Approval", context.State.Id));
            }
            return Task.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class CapturePayloadStep : IStep<DynamicWaitState>
    {
        public string StepId => "CapturePayload";

        public Task<StepResult> ExecuteAsync(StepContext<DynamicWaitState> context)
        {
            context.State.Payload = context.ResumedEvent?.Payload as string;
            return Task.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    [Fact]
    public async Task Business_step_can_dynamically_request_wait_and_resume_with_payload()
    {
        var definition = new WorkflowBuilder<DynamicWaitState>("DynamicWaitWorkflow")
            .Init()
            .Then<DynamicWaitStep>()
            .Then<CapturePayloadStep>()
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(definition).Start(new DynamicWaitState());

        Assert.Equal(WorkflowStatus.Waiting, snapshot.Status);

        var scope = engine.Instance(snapshot.InstanceId);
        await scope.RaiseEvent(new EventEnvelope("Approval", "corr-1", "approved", "evt-1"));

        var state = scope.GetState<DynamicWaitState>();
        Assert.Equal(WorkflowStatus.Completed, scope.Get().Status);
        Assert.Equal("approved", state.Payload);
    }
}

