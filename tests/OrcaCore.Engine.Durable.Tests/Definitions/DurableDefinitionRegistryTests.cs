using AwesomeAssertions;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Core.Building;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Driver;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Definitions;

public sealed class DurableDefinitionRegistryTests
{
    [Fact]
    public void Register_LegacyEmptyPlanIsRejectedBeforeExecutorPublication()
    {
        var definition = new WorkflowBuilder<TestState>()
            .Init<string>(input => new TestState(input))
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
        var registry = new DurableDefinitionRegistry();

        var register = () => registry.Register(definition);

        register.Should().Throw<WorkflowDefinitionException>()
            .WithMessage("*compiled*plan*");
        registry.ResolveExecutor(definition.DefinitionId, definition.DefinitionVersion)
            .Should().BeNull();
    }

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

    [Fact]
    public void Register_SameDefinitionVersionWithFingerprintDrift_IsRejected()
    {
        var registry = new DurableDefinitionRegistry();
        var definitionId = DefinitionId.New();
        var first = Workflow.Durable<TestState>(definitionId, DefinitionVersion.Initial)
            .Init<string>(input => new TestState(input))
            .End("first")
            .Build();
        var drifted = Workflow.Durable<TestState>(definitionId, DefinitionVersion.Initial)
            .Init<string>(input => new TestState(input))
            .End("second")
            .Build();
        registry.Register(first);

        var act = () => registry.Register(drifted);

        act.Should().Throw<WorkflowDefinitionException>()
            .Which.Message.Should().Contain("fingerprint");
    }

    [Fact]
    public void Register_CancellationInducingStructuredShapesHaveDurableProtocols()
    {
        var childDefinitionId = DefinitionId.New();
        var definitions = new WorkflowDefinition<CancellationState>[]
        {
            Workflow.Durable<CancellationState>(DefinitionId.New(), DefinitionVersion.Initial)
                .Init<string>(_ => new CancellationState())
                .WhenFirst<string>(
                    branches => branches
                        .Branch<CancellationBranchState>(
                            "wait",
                            _ => new CancellationBranchState(),
                            branch => branch
                                .Wait("resume", _ => new CorrelationId("registration"))
                                .Return(_ => "wait"))
                        .Branch<CancellationBranchState>(
                            "delay",
                            _ => new CancellationBranchState(),
                            branch => branch
                                .Delay(TimeSpan.FromMinutes(1))
                                .Return(_ => "delay")),
                    (parent, _) => parent.Value)
                .End()
                .Build(),
            Workflow.Durable<CancellationState>(DefinitionId.New(), DefinitionVersion.Initial)
                .Init<string>(_ => new CancellationState())
                .If(
                    _ => true,
                    branch => branch.RunChildren(
                        childDefinitionId,
                        DefinitionVersion.Initial,
                        _ => ["one", "two"],
                        maxConcurrency: 1,
                        failurePolicy: RunChildFailurePolicy.PropagateFailure,
                        joinPolicy: RunChildrenJoinPolicy.WhenAll,
                        residualPolicy: RunChildrenResidualPolicy.CancelRemaining))
                .End()
                .Build(),
            Workflow.Durable<CancellationState>(DefinitionId.New(), DefinitionVersion.Initial)
                .Init<string>(_ => new CancellationState())
                .Parallel<int>(
                    branches => branches
                        .Branch<CancellationBranchState>(
                            "first",
                            _ => new CancellationBranchState(),
                            branch => branch.Return(_ => 1))
                        .Branch<CancellationBranchState>(
                            "second",
                            _ => new CancellationBranchState(),
                            branch => branch.Return(_ => 2)),
                    (parent, _) => parent.Value)
                .End()
                .Build()
        };
        var registry = new DurableDefinitionRegistry();

        foreach (var definition in definitions)
        {
            registry.Register(definition);
        }

        registry.List().Should().HaveCount(definitions.Length);
        foreach (var definition in definitions)
        {
            registry.ResolveExecutor(definition.DefinitionId, definition.DefinitionVersion)
                .Should().BeOfType<DurableFiberDriverExecutor<CancellationState>>();
        }
    }

    private static WorkflowDefinition<TestState> Definition(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion)
    {
        return Workflow.Durable<TestState>(definitionId, definitionVersion)
            .Init<string>(input => new TestState(input))
            .End()
            .Build();
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

    private sealed class CancellationState;

    private sealed class CancellationBranchState;
}
