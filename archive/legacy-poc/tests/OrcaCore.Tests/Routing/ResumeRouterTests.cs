
namespace OrcaCore.Tests;

public class ResumeRouterTests
{
    private sealed class TopLevelState
    {
        public string Id { get; set; } = "corr-1";
        public string? Payload { get; set; }
    }

    private sealed class CapturePayloadStep : IStep<TopLevelState>
    {
        public string StepId => "CapturePayload";

        public Task<StepResult> ExecuteAsync(StepContext<TopLevelState> context)
        {
            context.State.Payload = context.ResumedEvent?.Payload as string;
            return Task.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    [Fact]
    public async Task Top_level_resume_delivers_payload_to_next_step()
    {
        var definition = new WorkflowBuilder<TopLevelState>("TopLevelResume")
            .Init()
            .Wait("Approval", s => s.Id)
            .Then<CapturePayloadStep>()
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var typed = engine.ForDefinition(definition);
        var snapshot = await typed.Start(new TopLevelState());

        await engine.Instance(snapshot.InstanceId)
            .RaiseEvent(new EventEnvelope("Approval", "corr-1", "payload", "evt-1"));

        var state = engine.Instance(snapshot.InstanceId).GetState<TopLevelState>();
        Assert.Equal("payload", state.Payload);
    }
}

