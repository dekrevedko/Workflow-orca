using System.Text.Json;
using OrcaCore.Runtime.Durable.Persistence;

namespace OrcaCore.Tests.Durable;

public sealed class DefinitionVersionMismatchTests
{
    private sealed record MyState(string Id = "corr-1");
    private sealed record OtherState(string Id = "corr-2");

    [Fact]
    public async Task Registering_different_definition_version_for_waiting_instance_throws_explicit_exception()
    {
        var store = new InMemoryWorkflowStore();
        await store.CreateAsync(
            new PersistedInstance(
                "inst-1",
                "VersionedFlow",
                "v1",
                0,
                JsonSerializer.SerializeToElement(new MyState()),
                new PersistedRuntimeState(
                    WorkflowStatus.Waiting,
                    DateTimeOffset.UtcNow.AddMinutes(-2),
                    DateTimeOffset.UtcNow.AddMinutes(-1),
                    [
                        new PersistedWaitRecord(
                            "wait-1",
                            "Evt",
                            "corr-1",
                            BranchId: null,
                            DateTimeOffset.UtcNow.AddMinutes(-1),
                            WaitStatus.Active,
                            WaitMode.Cold)
                    ],
                    [],
                    [],
                    Error: null,
                    new PersistedExecutionPath(BranchId: null, [new PersistedFrame(PersistedFrameKind.Root, "", 1, ScopeId: null)]),
                    ActiveParallel: null)),
            CancellationToken.None);

        await using var engine = DurableWorkflowEngine.Create(store);
        var definition = new DurableWorkflowBuilder<MyState>("VersionedFlow", "v2")
            .Init()
            .WaitLong("Evt", state => state.Id)
            .End()
            .Build();

        var ex = await Assert.ThrowsAsync<DefinitionVersionMismatchException>(() => engine.ForDefinitionAsync(definition));
        Assert.Equal("VersionedFlow", ex.DefinitionId);
        Assert.Equal("v2", ex.ExpectedVersion);
        Assert.Equal("v1", ex.ActualVersion);
        Assert.Equal("inst-1", ex.InstanceId);
    }

    [Fact]
    public async Task Registering_same_definition_id_and_version_twice_is_idempotent()
    {
        var store = new InMemoryWorkflowStore();
        await using var engine = DurableWorkflowEngine.Create(store);

        var definition = new DurableWorkflowBuilder<MyState>("VersionedFlow", "v1")
            .Init()
            .WaitLong("Evt", state => state.Id)
            .End()
            .Build();

        var first = await engine.ForDefinitionAsync(definition);
        var second = await engine.ForDefinitionAsync(definition);

        var started = await second.Start(new MyState());

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal("VersionedFlow", started.DefinitionId);
        Assert.Equal("v1", started.DefinitionVersion);
    }

    [Fact]
    public async Task Registering_same_definition_id_with_different_version_is_rejected_even_without_waiting_instances()
    {
        var store = new InMemoryWorkflowStore();
        await using var engine = DurableWorkflowEngine.Create(store);

        var v1 = new DurableWorkflowBuilder<MyState>("VersionedFlow", "v1")
            .Init()
            .WaitLong("Evt", state => state.Id)
            .End()
            .Build();
        var v2 = new DurableWorkflowBuilder<MyState>("VersionedFlow", "v2")
            .Init()
            .WaitLong("Evt", state => state.Id)
            .End()
            .Build();

        await engine.ForDefinitionAsync(v1);

        var ex = await Assert.ThrowsAsync<DefinitionAlreadyRegisteredException>(() => engine.ForDefinitionAsync(v2));
        Assert.Equal("VersionedFlow", ex.DefinitionId);
        Assert.Equal("v2", ex.RequestedVersion);
        Assert.Equal("v1", ex.RegisteredVersion);
    }

    [Fact]
    public async Task Registering_same_definition_id_and_version_with_different_state_type_is_rejected()
    {
        var store = new InMemoryWorkflowStore();
        await using var engine = DurableWorkflowEngine.Create(store);

        var first = new DurableWorkflowBuilder<MyState>("VersionedFlow", "v1")
            .Init()
            .WaitLong("Evt", state => state.Id)
            .End()
            .Build();
        var second = new DurableWorkflowBuilder<OtherState>("VersionedFlow", "v1")
            .Init()
            .WaitLong("Evt", state => state.Id)
            .End()
            .Build();

        await engine.ForDefinitionAsync(first);

        var ex = await Assert.ThrowsAsync<DefinitionAlreadyRegisteredException>(() => engine.ForDefinitionAsync(second));
        Assert.Equal("VersionedFlow", ex.DefinitionId);
        Assert.Equal("v1", ex.RequestedVersion);
        Assert.Equal("v1", ex.RegisteredVersion);
    }
}
