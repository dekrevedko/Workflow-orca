using OrcaCore.Abstractions;
using OrcaCore.Runtime;

namespace OrcaCore.Tests;

public class MC_AT_017_CorrelationTargetedRoutingTests
{
    private sealed class PriceState
    {
        public string RequestId { get; set; } = "";
        public bool Completed { get; set; }
    }

    private sealed class CompleteStep : IStep<PriceState>
    {
        public string StepId => "Complete";
        public Task<StepResult> ExecuteAsync(StepContext<PriceState> context)
        {
            context.State.Completed = true;
            return Task.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    [Fact]
    public async Task Correlation_targeted_event_routes_to_correct_instance()
    {
        var definition = new WorkflowBuilder<PriceState>("PriceWorkflow")
            .Init()
            .Wait("PriceResponse", s => s.RequestId)
            .Step<CompleteStep>()
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var typed = engine.ForDefinition(definition);

        var snapshotA = await typed.Start(new PriceState { RequestId = "req-1" });
        var snapshotB = await typed.Start(new PriceState { RequestId = "req-2" });

        Assert.Equal(WorkflowStatus.Waiting, snapshotA.Status);
        Assert.Equal(WorkflowStatus.Waiting, snapshotB.Status);

        // Act — correlation-targeted routing, no InstanceId specified
        await engine.RaiseEvent(new EventEnvelope("PriceResponse", "req-1", null, "evt-1"));

        // Assert — only A resumed
        Assert.Equal(WorkflowStatus.Completed, engine.Instance(snapshotA.InstanceId).Get().Status);
        Assert.True(engine.Instance(snapshotA.InstanceId).GetState<PriceState>().Completed);

        Assert.Equal(WorkflowStatus.Waiting, engine.Instance(snapshotB.InstanceId).Get().Status);
        Assert.False(engine.Instance(snapshotB.InstanceId).GetState<PriceState>().Completed);
    }

    [Fact]
    public async Task Correlation_targeted_event_with_unknown_correlation_throws()
    {
        var definition = new WorkflowBuilder<PriceState>("PriceWorkflow")
            .Init()
            .Wait("PriceResponse", s => s.RequestId)
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var typed = engine.ForDefinition(definition);
        await typed.Start(new PriceState { RequestId = "req-1" });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RaiseEvent(new EventEnvelope("PriceResponse", "req-unknown", null, "evt-1")));

        Assert.Contains("no active wait", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Ambiguous_correlation_throws()
    {
        var definition = new WorkflowBuilder<PriceState>("PriceWorkflow")
            .Init()
            .Wait("PriceResponse", s => s.RequestId)
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var typed = engine.ForDefinition(definition);

        // Both instances register the same (EventName, CorrelationId)
        await typed.Start(new PriceState { RequestId = "req-same" });
        await typed.Start(new PriceState { RequestId = "req-same" });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RaiseEvent(new EventEnvelope("PriceResponse", "req-same", null, "evt-1")));

        Assert.Contains("ambiguous", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
