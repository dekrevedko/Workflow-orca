using OrcaCore.Abstractions;
using OrcaCore.Runtime;

namespace OrcaCore.Tests;

public class MC_AT_010_ManagementQueryTests
{
    private sealed class MyState
    {
        public string Id { get; set; } = "default";
    }

    private sealed class FailStep : IStep<MyState>
    {
        public string StepId => "Fail";
        public Task<StepResult> ExecuteAsync(StepContext<MyState> context) =>
            Task.FromResult<StepResult>(new StepResult.Failed(new Exception("fail")));
    }

    [Fact]
    public async Task Where_filters_by_status()
    {
        var completedDef = new WorkflowBuilder<MyState>("Completed")
            .Init().End().Build();

        var waitingDef = new WorkflowBuilder<MyState>("Waiting")
            .Init().Wait("Event", s => s.Id).End().Build();

        var failedDef = new WorkflowBuilder<MyState>("Failed")
            .Init().Step<FailStep>().End().Build();

        await using var engine = new WorkflowEngine();

        var completedSnap = await engine.ForDefinition(completedDef).Start(new MyState { Id = "c" });
        var waitingSnap = await engine.ForDefinition(waitingDef).Start(new MyState { Id = "w" });
        var failedSnap = await engine.ForDefinition(failedDef).Start(new MyState { Id = "f" });

        // Where by status
        var waiting = engine.Where(s => s.Status == WorkflowStatus.Waiting).List();
        Assert.Single(waiting);
        Assert.Equal(waitingSnap.InstanceId, waiting[0].InstanceId);

        var completed = engine.Where(s => s.Status == WorkflowStatus.Completed).List();
        Assert.Single(completed);
        Assert.Equal(completedSnap.InstanceId, completed[0].InstanceId);

        // Count
        Assert.Equal(1, engine.Where(s => s.Status == WorkflowStatus.Failed).Count());

        // All
        var all = engine.All().List();
        Assert.Equal(3, all.Count);
    }

    [Fact]
    public async Task Where_filters_by_definition_id()
    {
        var def1 = new WorkflowBuilder<MyState>("Def1").Init().End().Build();
        var def2 = new WorkflowBuilder<MyState>("Def2").Init().End().Build();

        await using var engine = new WorkflowEngine();
        await engine.ForDefinition(def1).Start(new MyState());
        await engine.ForDefinition(def1).Start(new MyState());
        await engine.ForDefinition(def2).Start(new MyState());

        var def1Instances = engine.Where(s => s.DefinitionId == "Def1").List();
        Assert.Equal(2, def1Instances.Count);
    }
}
