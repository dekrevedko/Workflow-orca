
namespace OrcaCore.Tests;

public class MC_AT_018_FanoutEventTests
{
    private sealed class MarketState
    {
        public string Date { get; set; } = "2026-03-15";
        public bool Closed { get; set; }
    }

    private sealed class MarkClosedStep : IStep<MarketState>
    {
        public string StepId => "MarkClosed";
        public Task<StepResult> ExecuteAsync(StepContext<MarketState> context)
        {
            context.State.Closed = true;
            return Task.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class OtherState
    {
        public string Date { get; set; } = "2026-03-15";
        public bool Closed { get; set; }
    }

    private sealed class OtherMarkClosedStep : IStep<OtherState>
    {
        public string StepId => "OtherMarkClosed";
        public Task<StepResult> ExecuteAsync(StepContext<OtherState> context)
        {
            context.State.Closed = true;
            return Task.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    [Fact]
    public async Task Fanout_resumes_all_matching_instances_of_definition_type()
    {
        var myDef = new WorkflowBuilder<MarketState>("MyWorkflow")
            .Init()
            .Wait("MarketClosed", s => s.Date)
            .Then<MarkClosedStep>()
            .End()
            .Build();

        var otherDef = new WorkflowBuilder<OtherState>("OtherWorkflow")
            .Init()
            .Wait("MarketClosed", s => s.Date)
            .Then<OtherMarkClosedStep>()
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var myTyped = engine.ForDefinition(myDef);
        var otherTyped = engine.ForDefinition(otherDef);

        var my1 = await myTyped.Start(new MarketState());
        var my2 = await myTyped.Start(new MarketState());
        var my3 = await myTyped.Start(new MarketState());
        var other = await otherTyped.Start(new OtherState());

        // All waiting
        Assert.Equal(WorkflowStatus.Waiting, my1.Status);
        Assert.Equal(WorkflowStatus.Waiting, my2.Status);
        Assert.Equal(WorkflowStatus.Waiting, my3.Status);
        Assert.Equal(WorkflowStatus.Waiting, other.Status);

        // Act â€” fanout to MyWorkflow only
        var envelope = new EventEnvelope("MarketClosed", "2026-03-15", null, "evt-fanout");
        await myTyped.RaiseEvent(envelope);

        // Assert â€” all three MyWorkflow instances completed
        Assert.Equal(WorkflowStatus.Completed, engine.Instance(my1.InstanceId).Get().Status);
        Assert.Equal(WorkflowStatus.Completed, engine.Instance(my2.InstanceId).Get().Status);
        Assert.Equal(WorkflowStatus.Completed, engine.Instance(my3.InstanceId).Get().Status);
        Assert.True(engine.Instance(my1.InstanceId).GetState<MarketState>().Closed);

        // OtherWorkflow still waiting
        Assert.Equal(WorkflowStatus.Waiting, engine.Instance(other.InstanceId).Get().Status);
        Assert.False(engine.Instance(other.InstanceId).GetState<OtherState>().Closed);
    }
}

