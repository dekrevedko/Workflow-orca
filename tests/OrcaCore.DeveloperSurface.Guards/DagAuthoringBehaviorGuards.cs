using AwesomeAssertions;
using OrcaCore;
using OrcaCore.Dag;
using DagAuthoring = OrcaCore.Dag.Dag;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class DagAuthoringBehaviorGuards
{
    [Fact]
    public void BuildAndTryBuild_AgreeOnOrderedDiagnostics()
    {
        var child = ResultlessChild();
        var builder = DagAuthoring.Define<int>(DefinitionId.New(), DefinitionVersion.Initial);
        var first = builder.Node(DagNodeId.Create("same"), child);
        var second = builder.Node(DagNodeId.Create("same"), child);
        var firstRef = first.MapInput(context => context.RunInput);
        second.DependsOn(firstRef, firstRef);
        second.MapInput(context => context.RunInput);
        second.MapInput(context => context.RunInput);

        var validation = builder.TryBuild();
        validation.IsValid.Should().BeFalse();
        validation.Diagnostics.Select(diagnostic => diagnostic.Code).Should().Equal(
            "DAG-AUTH-DEPENDENCY-001", "DAG-AUTH-MAP-002", "DAG-AUTH-NODE-001");
        validation.Diagnostics.Select(diagnostic => diagnostic.Location.Value)
            .Should().OnlyContain(location => location == "dag:$/dag-node:00000001");

        var exception = () => builder.Build();
        exception.Should().Throw<WorkflowDefinitionException>()
            .Which.Diagnostics.Select(diagnostic => diagnostic.Code)
            .Should().Equal(validation.Diagnostics.Select(diagnostic => diagnostic.Code));
    }

    [Fact]
    public void Build_PreservesOrdinalsAndCopiesDependencies()
    {
        var child = ResultlessChild();
        var builder = DagAuthoring.Define<int>(DefinitionId.New(), DefinitionVersion.Initial);
        var first = builder.Node(DagNodeId.Create("first"), child)
            .MapInput(context => context.RunInput);
        var dependencies = new DagNodeRef[] { first };
        builder.Node(DagNodeId.Create("second"), child)
            .DependsOn(dependencies)
            .MapInput(context => context.RunInput);
        dependencies[0] = builder.Node(DagNodeId.Create("third"), child)
            .MapInput(context => context.RunInput);

        var plan = builder.Build();
        plan.Nodes.Select(node => node.NodeId.Value).Should().Equal("first", "second", "third");
        plan.DefinitionFingerprint.Value.Should().MatchRegex("^[0-9A-F]{64}$");
        var repeated = builder.TryBuild();
        repeated.TryGetValue(out var secondPlan).Should().BeTrue();
        secondPlan!.DefinitionFingerprint.Should().Be(plan.DefinitionFingerprint);
    }

    [Fact]
    public void Build_FingerprintBindsStructureButNotOpaqueMapperBody()
    {
        var child = ResultlessChild();
        var identity = DefinitionId.New();
        var mapperCalls = 0;

        WorkflowDagPlan<int> Plan(string firstId, int offset)
        {
            var builder = DagAuthoring.Define<int>(identity, DefinitionVersion.Initial);
            builder.Node(DagNodeId.Create(firstId), child)
                .MapInput(context =>
                {
                    mapperCalls++;
                    return context.RunInput + offset;
                });
            return builder.Build();
        }

        Plan("first", 1).DefinitionFingerprint.Should().Be(Plan("first", 2).DefinitionFingerprint);
        Plan("first", 1).DefinitionFingerprint.Should().NotBe(Plan("renamed", 1).DefinitionFingerprint);
        mapperCalls.Should().Be(0, "building a DAG must not execute opaque mapping delegates");
    }

    [Fact]
    public void TryBuild_RejectsForeignSelfAndCyclicDependenciesAndMissingMapping()
    {
        var child = ResultlessChild();
        var foreign = DagAuthoring.Define<int>(DefinitionId.New(), DefinitionVersion.Initial)
            .Node(DagNodeId.Create("foreign"), child)
            .MapInput(context => context.RunInput);
        var builder = DagAuthoring.Define<int>(DefinitionId.New(), DefinitionVersion.Initial);
        var firstBuilder = builder.Node(DagNodeId.Create("first"), child);
        var secondBuilder = builder.Node(DagNodeId.Create("second"), child);
        var first = firstBuilder.MapInput(context => context.RunInput);
        var second = secondBuilder.DependsOn(first, foreign)
            .MapInput(context => context.RunInput);
        firstBuilder.DependsOn(first, second);
        _ = builder.Node(DagNodeId.Create("unmapped"), child);

        var validation = builder.TryBuild();
        validation.IsValid.Should().BeFalse();
        validation.Diagnostics.Select(diagnostic => diagnostic.Code).Should().Contain(
            "DAG-AUTH-DEPENDENCY-002", "DAG-AUTH-DEPENDENCY-003",
            "DAG-AUTH-DEPENDENCY-004", "DAG-AUTH-MAP-001");
    }

    [Theory]
    [InlineData("DAG-AUTH-NODE-001")]
    [InlineData("DAG-AUTH-MAP-001")]
    [InlineData("DAG-AUTH-MAP-002")]
    [InlineData("DAG-AUTH-DEPENDENCY-001")]
    [InlineData("DAG-AUTH-DEPENDENCY-002")]
    [InlineData("DAG-AUTH-DEPENDENCY-003")]
    [InlineData("DAG-AUTH-DEPENDENCY-004")]
    public void TryBuild_ReportsEachAuthoringFailureAsAnExactSingleCode(string expectedCode)
    {
        var child = ResultlessChild();
        var builder = DagAuthoring.Define<int>(DefinitionId.New(), DefinitionVersion.Initial);
        var first = builder.Node(DagNodeId.Create("first"), child);
        var second = builder.Node(DagNodeId.Create(
            expectedCode == "DAG-AUTH-NODE-001" ? "first" : "second"), child);
        var firstRef = expectedCode == "DAG-AUTH-MAP-001"
            ? null : first.MapInput(context => context.RunInput);
        var secondRef = second.MapInput(context => context.RunInput);

        switch (expectedCode)
        {
            case "DAG-AUTH-MAP-002":
                first.MapInput(context => context.RunInput);
                break;
            case "DAG-AUTH-DEPENDENCY-001":
                second.DependsOn(firstRef!, firstRef!);
                break;
            case "DAG-AUTH-DEPENDENCY-002":
                second.DependsOn(secondRef);
                break;
            case "DAG-AUTH-DEPENDENCY-003":
                first.DependsOn(secondRef);
                second.DependsOn(firstRef!);
                break;
            case "DAG-AUTH-DEPENDENCY-004":
                var foreign = DagAuthoring.Define<int>(DefinitionId.New(), DefinitionVersion.Initial)
                    .Node(DagNodeId.Create("foreign"), child)
                    .MapInput(context => context.RunInput);
                second.DependsOn(foreign);
                break;
        }

        builder.TryBuild().Diagnostics.Select(diagnostic => diagnostic.Code)
            .Should().Equal(expectedCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TryBuild_AcceptsLongAcyclicDependencyChainsWithoutStackGrowth(bool forward)
    {
        const int nodeCount = 100_000;
        var child = ResultlessChild();
        var builder = DagAuthoring.Define<int>(DefinitionId.New(), DefinitionVersion.Initial);
        var nodes = new DagNodeBuilder<int, int>[nodeCount];
        var references = new DagNodeRef[nodeCount];
        for (var ordinal = 0; ordinal < nodeCount; ordinal++)
        {
            nodes[ordinal] = builder.Node(DagNodeId.Create($"node-{ordinal:D6}"), child);
            references[ordinal] = nodes[ordinal].MapInput(context => context.RunInput);
        }

        for (var ordinal = 0; ordinal < nodeCount; ordinal++)
        {
            var dependency = forward ? ordinal + 1 : ordinal - 1;
            if (dependency >= 0 && dependency < nodeCount)
            {
                nodes[ordinal].DependsOn(references[dependency]);
            }
        }

        var validation = builder.TryBuild();
        validation.IsValid.Should().BeTrue();
        validation.Diagnostics.Should().BeEmpty();
        validation.TryGetValue(out var plan).Should().BeTrue();
        plan!.Nodes.Should().HaveCount(nodeCount);
    }

    private static DurableWorkflowRef<int> ResultlessChild() =>
        Workflow.Durable<ChildState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(value => new ChildState(value))
            .End()
            .Build()
            .Reference;

    private sealed record ChildState(int Value);
}
