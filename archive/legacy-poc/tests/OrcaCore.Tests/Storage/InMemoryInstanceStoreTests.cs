
namespace OrcaCore.Tests;

public class InMemoryInstanceStoreTests
{
    private sealed class MyState { public string Value { get; set; } = "init"; }

    private static WorkflowDefinition<MyState> MakeDefinition(string id = "TestDef")
    {
        return new WorkflowBuilder<MyState>(id)
            .Init()
            .End()
            .Build();
    }

    [Fact]
    public void Add_and_Get_roundtrips_instance()
    {
        var store = new InMemoryInstanceStore();
        var def = MakeDefinition();
        store.RegisterDefinition(def);
        var instance = new WorkflowInstance<MyState>("inst-1", def.DefinitionId, new MyState());
        store.Add(instance);

        var retrieved = store.Get<MyState>("inst-1");

        Assert.Same(instance, retrieved);
    }

    [Fact]
    public void Add_duplicate_instance_throws()
    {
        var store = new InMemoryInstanceStore();
        var def = MakeDefinition();
        store.RegisterDefinition(def);
        var instance = new WorkflowInstance<MyState>("inst-1", def.DefinitionId, new MyState());
        store.Add(instance);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            store.Add(new WorkflowInstance<MyState>("inst-1", def.DefinitionId, new MyState())));
        Assert.Contains("already exists", ex.Message);
    }

    [Fact]
    public void Get_nonexistent_instance_throws()
    {
        var store = new InMemoryInstanceStore();

        Assert.Throws<KeyNotFoundException>(() => store.Get<MyState>("nope"));
    }

    [Fact]
    public void GetUntyped_nonexistent_throws()
    {
        var store = new InMemoryInstanceStore();

        Assert.Throws<KeyNotFoundException>(() => store.GetUntyped("nope"));
    }

    [Fact]
    public void GetDefinition_nonexistent_throws()
    {
        var store = new InMemoryInstanceStore();

        Assert.Throws<KeyNotFoundException>(() => store.GetDefinition<MyState>("nope"));
    }

    [Fact]
    public void GetInstanceIdsByDefinition_filters_correctly()
    {
        var store = new InMemoryInstanceStore();
        var def1 = MakeDefinition("Def1");
        var def2 = MakeDefinition("Def2");
        store.RegisterDefinition(def1);
        store.RegisterDefinition(def2);

        store.Add(new WorkflowInstance<MyState>("a", "Def1", new MyState()));
        store.Add(new WorkflowInstance<MyState>("b", "Def2", new MyState()));
        store.Add(new WorkflowInstance<MyState>("c", "Def1", new MyState()));

        var ids = store.GetInstanceIdsByDefinition("Def1");
        Assert.Equal(2, ids.Count);
        Assert.Contains("a", ids);
        Assert.Contains("c", ids);
    }

    [Fact]
    public void GetInstanceIdsByDefinition_returns_empty_for_unknown()
    {
        var store = new InMemoryInstanceStore();

        Assert.Empty(store.GetInstanceIdsByDefinition("Unknown"));
    }

    [Fact]
    public void GetAllSnapshots_returns_all_instances()
    {
        var store = new InMemoryInstanceStore();
        var def = MakeDefinition();
        store.RegisterDefinition(def);
        store.Add(new WorkflowInstance<MyState>("a", def.DefinitionId, new MyState()));
        store.Add(new WorkflowInstance<MyState>("b", def.DefinitionId, new MyState()));

        var snapshots = store.GetAllSnapshots().ToList();
        Assert.Equal(2, snapshots.Count);
    }

    [Fact]
    public void GetResumeDelegate_nonexistent_throws()
    {
        var store = new InMemoryInstanceStore();

        Assert.Throws<KeyNotFoundException>(() => store.GetResumeDelegate("nope"));
    }

    [Fact]
    public void DisposeAllLocks_does_not_throw_on_empty_store()
    {
        var store = new InMemoryInstanceStore();
        store.DisposeAllLocks();
    }
}
