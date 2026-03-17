
namespace OrcaCore.Tests;

public class MC_AT_008_ParallelOrderInvariantTests
{
    private sealed class MyState
    {
        public string Id { get; set; } = "corr-1";
        public string BranchAValue { get; set; } = "";
        public string BranchBValue { get; set; } = "";
    }

    private sealed class SetAStep : IStep<MyState>
    {
        public string StepId => "SetA";
        public Task<StepResult> ExecuteAsync(StepContext<MyState> context)
        {
            context.State.BranchAValue = "A-done";
            return Task.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class SetBStep : IStep<MyState>
    {
        public string StepId => "SetB";
        public Task<StepResult> ExecuteAsync(StepContext<MyState> context)
        {
            context.State.BranchBValue = "B-done";
            return Task.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    [Fact]
    public async Task Branch_A_first_then_B_produces_same_result()
    {
        var (stateAFirst, statusAFirst) = await RunWithOrder("EventA", "EventB");
        var (stateBFirst, statusBFirst) = await RunWithOrder("EventB", "EventA");

        Assert.Equal(WorkflowStatus.Completed, statusAFirst);
        Assert.Equal(WorkflowStatus.Completed, statusBFirst);
        Assert.Equal(stateAFirst.BranchAValue, stateBFirst.BranchAValue);
        Assert.Equal(stateAFirst.BranchBValue, stateBFirst.BranchBValue);
        Assert.Equal("A-done", stateAFirst.BranchAValue);
        Assert.Equal("B-done", stateAFirst.BranchBValue);
    }

    private static async Task<(MyState State, WorkflowStatus Status)> RunWithOrder(
        string firstEvent, string secondEvent)
    {
        var definition = new WorkflowBuilder<MyState>("OrderTestWorkflow")
            .Init()
            .Parallel(p => p
                .Branch("A", b => b
                    .Wait("EventA", s => s.Id)
                    .Then<SetAStep>())
                .Branch("B", b => b
                    .Wait("EventB", s => s.Id)
                    .Then<SetBStep>()))
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var typed = engine.ForDefinition(definition);
        var snapshot = await typed.Start(new MyState());
        var scope = engine.Instance(snapshot.InstanceId);

        await scope.RaiseEvent(new EventEnvelope(firstEvent, "corr-1", null, $"evt-{firstEvent}"));
        await scope.RaiseEvent(new EventEnvelope(secondEvent, "corr-1", null, $"evt-{secondEvent}"));

        return (scope.GetState<MyState>(), scope.Get().Status);
    }
}

