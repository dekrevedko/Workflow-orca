using OrcaCore.Abstractions;
using OrcaCore.Runtime;

namespace OrcaCore.Tests;

public class MC_AT_016_DuplicateEventDeduplicationTests
{
    private sealed class MyState
    {
        public string Id { get; set; } = "corr-1";
        public int ResumeCount { get; set; }
    }

    private sealed class CountStep : IStep<MyState>
    {
        public string StepId => "Count";
        public Task<StepResult> ExecuteAsync(StepContext<MyState> context)
        {
            context.State.ResumeCount++;
            return Task.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    [Fact]
    public async Task Duplicate_event_id_is_silently_ignored()
    {
        var definition = new WorkflowBuilder<MyState>("DedupWorkflow")
            .Init()
            .Wait("Approval", s => s.Id)
            .Step<CountStep>()
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var typed = engine.ForDefinition(definition);
        var snapshot = await typed.Start(new MyState());

        var scope = engine.Instance(snapshot.InstanceId);
        var envelope = new EventEnvelope("Approval", "corr-1", null, "evt-1");

        // First raise — should resume
        await scope.RaiseEvent(envelope);
        Assert.Equal(WorkflowStatus.Completed, scope.Get().Status);
        Assert.Equal(1, scope.GetState<MyState>().ResumeCount);

        // Second raise with same EventId — should be ignored
        await scope.RaiseEvent(envelope);
        Assert.Equal(1, scope.GetState<MyState>().ResumeCount);
    }

    [Fact]
    public async Task Duplicate_buffered_event_is_not_stored_twice()
    {
        var definition = new WorkflowBuilder<MyState>("DedupBufferWorkflow")
            .Init()
            .Wait("EventA", s => s.Id)
            .Step<CountStep>()
            .Wait("EventB", s => s.Id)
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var typed = engine.ForDefinition(definition);
        var snapshot = await typed.Start(new MyState());

        var scope = engine.Instance(snapshot.InstanceId);

        // Buffer EventB twice with same EventId
        var eventB = new EventEnvelope("EventB", "corr-1", null, "evt-b");
        await scope.RaiseEvent(eventB);
        await scope.RaiseEvent(eventB); // duplicate — should be ignored

        // Now send EventA to resume
        await scope.RaiseEvent(new EventEnvelope("EventA", "corr-1", null, "evt-a"));

        // Workflow should complete normally (buffered EventB consumed once)
        Assert.Equal(WorkflowStatus.Completed, scope.Get().Status);
    }
}
