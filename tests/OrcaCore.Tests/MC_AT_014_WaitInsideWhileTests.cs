using OrcaCore.Abstractions;
using OrcaCore.Runtime;

namespace OrcaCore.Tests;

public class MC_AT_014_WaitInsideWhileTests
{
    private sealed class LoopState
    {
        public List<string> Items { get; set; } = ["a", "b", "c"];
        public int Index { get; set; }
        public string CurrentItem { get; set; } = "";
        public List<string> ProcessedItems { get; set; } = [];
    }

    private sealed class SetCurrentItemStep : IStep<LoopState>
    {
        public string StepId => "SetCurrentItem";
        public Task<StepResult> ExecuteAsync(StepContext<LoopState> context)
        {
            context.State.CurrentItem = context.State.Items[context.State.Index];
            return Task.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class IncrementAndRecordStep : IStep<LoopState>
    {
        public string StepId => "IncrementAndRecord";
        public Task<StepResult> ExecuteAsync(StepContext<LoopState> context)
        {
            context.State.ProcessedItems.Add(context.State.CurrentItem);
            context.State.Index++;
            return Task.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    [Fact]
    public async Task Wait_inside_while_resumes_correctly_across_iterations()
    {
        var definition = new WorkflowBuilder<LoopState>("LoopWaitWorkflow")
            .Init()
            .While(s => s.Index < s.Items.Count, body => body
                .Step<SetCurrentItemStep>()
                .Wait("ItemProcessed", s => s.CurrentItem)
                .Step<IncrementAndRecordStep>())
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var typed = engine.ForDefinition(definition);
        var snapshot = await typed.Start(new LoopState());

        var scope = engine.Instance(snapshot.InstanceId);
        Assert.Equal(WorkflowStatus.Waiting, snapshot.Status);

        // Iteration 1: waiting for "a"
        var waits = scope.GetActiveWaits();
        Assert.Single(waits);
        Assert.Equal("a", waits[0].CorrelationId);

        await scope.RaiseEvent(new EventEnvelope("ItemProcessed", "a", null, "evt-a"));
        Assert.Equal(WorkflowStatus.Waiting, scope.Get().Status);

        // Iteration 2: waiting for "b"
        waits = scope.GetActiveWaits();
        Assert.Single(waits);
        Assert.Equal("b", waits[0].CorrelationId);

        await scope.RaiseEvent(new EventEnvelope("ItemProcessed", "b", null, "evt-b"));
        Assert.Equal(WorkflowStatus.Waiting, scope.Get().Status);

        // Iteration 3: waiting for "c"
        waits = scope.GetActiveWaits();
        Assert.Single(waits);
        Assert.Equal("c", waits[0].CorrelationId);

        await scope.RaiseEvent(new EventEnvelope("ItemProcessed", "c", null, "evt-c"));

        // Loop done, workflow completes
        Assert.Equal(WorkflowStatus.Completed, scope.Get().Status);
        var state = scope.GetState<LoopState>();
        Assert.Equal(3, state.Index);
        Assert.Equal(["a", "b", "c"], state.ProcessedItems);
    }

    [Fact]
    public async Task Previous_iteration_event_does_not_resume_current_iteration()
    {
        var definition = new WorkflowBuilder<LoopState>("LoopWaitWorkflow")
            .Init()
            .While(s => s.Index < s.Items.Count, body => body
                .Step<SetCurrentItemStep>()
                .Wait("ItemProcessed", s => s.CurrentItem)
                .Step<IncrementAndRecordStep>())
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var typed = engine.ForDefinition(definition);
        var snapshot = await typed.Start(new LoopState());

        var scope = engine.Instance(snapshot.InstanceId);

        // Resume iteration 1
        await scope.RaiseEvent(new EventEnvelope("ItemProcessed", "a", null, "evt-a"));

        // Now waiting for "b" — raising "a" again should not resume
        await scope.RaiseEvent(new EventEnvelope("ItemProcessed", "a", null, "evt-a-2"));
        Assert.Equal(WorkflowStatus.Waiting, scope.Get().Status);

        // "b" should still be the active wait
        var waits = scope.GetActiveWaits();
        Assert.Single(waits);
        Assert.Equal("b", waits[0].CorrelationId);
    }
}
