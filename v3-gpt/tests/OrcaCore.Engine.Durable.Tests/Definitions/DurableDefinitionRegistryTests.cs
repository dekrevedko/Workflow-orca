using AwesomeAssertions;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Core.Building;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Durable.Definitions;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Definitions;

public sealed class DurableDefinitionRegistryTests
{
    [Fact]
    [Trait("AC", "AC-306")]
    public void ResolveBound_UsesSnapshotDefinitionVersionAfterNewerVersionRegisters()
    {
        var registry = new DurableDefinitionRegistry();
        var definitionId = DefinitionId.New();
        var versionOne = Definition(definitionId, DefinitionVersion.Initial);
        var versionTwo = Definition(definitionId, new DefinitionVersion(2));
        registry.Register(versionOne);
        registry.Register(versionTwo);
        var snapshot = Snapshot(definitionId, DefinitionVersion.Initial);

        var bound = registry.ResolveBound<TestState>(snapshot);

        bound.Should().BeSameAs(versionOne);
        registry.Resolve<TestState>(definitionId, new DefinitionVersion(2))
            .Should().BeSameAs(versionTwo);
    }

    [Fact]
    public void Resolve_WithDifferentStateType_ThrowsDefinitionException()
    {
        var registry = new DurableDefinitionRegistry();
        var definition = Definition(DefinitionId.New(), DefinitionVersion.Initial);
        registry.Register(definition);

        var act = () => registry.Resolve<OtherState>(
            definition.DefinitionId,
            definition.DefinitionVersion);

        act.Should().Throw<WorkflowDefinitionException>()
            .Which.Message.Should().Contain("state type");
    }

    [Fact]
    public void Register_SameDefinitionVersionAndStateType_IsIdempotent()
    {
        var registry = new DurableDefinitionRegistry();
        var definitionId = DefinitionId.New();
        var first = Definition(definitionId, DefinitionVersion.Initial);
        var second = Definition(definitionId, DefinitionVersion.Initial);

        registry.Register(first);
        registry.Register(second);

        registry.List().Should().ContainSingle(definition =>
            definition.DefinitionId == definitionId &&
            definition.DefinitionVersion == DefinitionVersion.Initial &&
            definition.StateType == typeof(TestState));
    }

    private static WorkflowDefinition<TestState> Definition(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion)
    {
        return new WorkflowBuilder<TestState>()
            .Init<string>(input => new TestState(input))
            .End()
            .Build(definitionId, definitionVersion);
    }

    private static WorkflowInstanceSnapshot Snapshot(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion)
    {
        return new WorkflowInstanceSnapshot
        {
            InstanceId = InstanceId.New(),
            DefinitionId = definitionId,
            DefinitionVersion = definitionVersion,
            Status = WorkflowStatus.Waiting,
            CreatedAt = DateTimeOffset.UnixEpoch,
            UpdatedAt = DateTimeOffset.UnixEpoch
        };
    }

    private sealed record TestState(string Value);

    private sealed record OtherState(string Value);
}
