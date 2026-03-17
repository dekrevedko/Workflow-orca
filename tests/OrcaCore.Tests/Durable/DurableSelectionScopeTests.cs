using OrcaCore.Runtime.Durable.Persistence;

namespace OrcaCore.Tests.Durable;

public sealed class DurableSelectionScopeTests
{
    private sealed record QueryState(string Id);

    [Fact]
    public async Task Durable_queries_return_persisted_snapshots_for_hot_and_cold_instances()
    {
        var store = new InMemoryWorkflowStore();
        var waitingDefinition = new DurableWorkflowBuilder<QueryState>("WaitingFlow", "v1")
            .Init()
            .WaitLong("Approval", state => state.Id)
            .End()
            .Build();
        var completedDefinition = new DurableWorkflowBuilder<QueryState>("CompletedFlow", "v1")
            .Init()
            .End()
            .Build();

        await using var engine = DurableWorkflowEngine.Create(store);
        var waiting = await (await engine.ForDefinitionAsync(waitingDefinition)).Start(new QueryState("wait-1"));
        var completed = await (await engine.ForDefinitionAsync(completedDefinition)).Start(new QueryState("done-1"));

        var all = await engine.All().ListAsync();
        var waitingOnly = await engine.Where(x => x.Status == WorkflowStatus.Waiting).ListAsync();
        var typedWaiting = await (await engine.ForDefinitionAsync(waitingDefinition)).All().ListAsync();

        Assert.Equal(2, all.Count);
        Assert.Contains(all, x => x.InstanceId == waiting.InstanceId && x.ActiveWaitCount == 1);
        Assert.Contains(all, x => x.InstanceId == completed.InstanceId && x.Status == WorkflowStatus.Completed);
        Assert.Equal([waiting.InstanceId], waitingOnly.Select(x => x.InstanceId).ToArray());
        Assert.Equal([waiting.InstanceId], typedWaiting.Select(x => x.InstanceId).ToArray());
    }

    [Fact]
    public async Task Durable_queries_validate_supported_predicates()
    {
        var store = new InMemoryWorkflowStore();
        await using var engine = DurableWorkflowEngine.Create(store);

        Assert.Throws<InvalidOperationException>(() =>
            engine.Where(x => x.InstanceId.StartsWith("abc")));
    }

    [Fact]
    public async Task DeleteAsync_removes_all_matching_instances()
    {
        var store = new InMemoryWorkflowStore();
        var definition = new DurableWorkflowBuilder<QueryState>("DeleteSelectionFlow", "v1")
            .Init()
            .Wait("Approval", state => state.Id)
            .End()
            .Build();

        await using var engine = DurableWorkflowEngine.Create(store);
        var typed = await engine.ForDefinitionAsync(definition);

        var first = await typed.Start(new QueryState("a"));
        var second = await typed.Start(new QueryState("b"));

        await engine.Where(x => x.DefinitionId == "DeleteSelectionFlow").DeleteAsync();

        Assert.Null(await store.LoadAsync(first.InstanceId, CancellationToken.None));
        Assert.Null(await store.LoadAsync(second.InstanceId, CancellationToken.None));
        Assert.Equal(0, await engine.All().CountAsync());
    }
}
