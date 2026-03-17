
namespace OrcaCore.Tests;

public class SelectionScopeTests
{
    private sealed class MyState { }

    [Fact]
    public async Task All_returns_all_instances()
    {
        await using var engine = new WorkflowEngine();
        var def = new WorkflowBuilder<MyState>("TestDef").Init().End().Build();
        var typed = engine.ForDefinition(def);

        await typed.Start(new MyState());
        await typed.Start(new MyState());

        Assert.Equal(2, engine.All().Count());
        Assert.Equal(2, engine.All().List().Count);
    }

    [Fact]
    public async Task Where_filters_by_predicate()
    {
        await using var engine = new WorkflowEngine();
        var def1 = new WorkflowBuilder<MyState>("Def1").Init().End().Build();
        var def2 = new WorkflowBuilder<MyState>("Def2").Init().End().Build();

        await engine.ForDefinition(def1).Start(new MyState());
        await engine.ForDefinition(def2).Start(new MyState());
        await engine.ForDefinition(def2).Start(new MyState());

        var result = engine.Where(x => x.DefinitionId == "Def2").List();
        Assert.Equal(2, result.Count);
        Assert.All(result, s => Assert.Equal("Def2", s.DefinitionId));
    }

    [Fact]
    public async Task Where_with_status_filter()
    {
        await using var engine = new WorkflowEngine();
        var def = new WorkflowBuilder<MyState>("TestDef").Init().End().Build();
        var typed = engine.ForDefinition(def);

        await typed.Start(new MyState());

        var waiting = engine.Where(x => x.Status == WorkflowStatus.Waiting).Count();
        var completed = engine.Where(x => x.Status == WorkflowStatus.Completed).Count();

        Assert.Equal(0, waiting);
        Assert.Equal(1, completed);
    }

    [Fact]
    public async Task TypedEngine_All_scopes_to_definition()
    {
        await using var engine = new WorkflowEngine();
        var def1 = new WorkflowBuilder<MyState>("Def1").Init().End().Build();
        var def2 = new WorkflowBuilder<MyState>("Def2").Init().End().Build();
        var typed1 = engine.ForDefinition(def1);
        var typed2 = engine.ForDefinition(def2);

        await typed1.Start(new MyState());
        await typed2.Start(new MyState());
        await typed2.Start(new MyState());

        Assert.Equal(1, typed1.All().Count());
        Assert.Equal(2, typed2.All().Count());
    }
}
