using System.Reflection;
using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Definitions;
using Xunit;

namespace OrcaCore.Core.Tests.Definitions;

public sealed class DefinitionModelTests
{
    [Fact]
    public void Definition_IsDeeplyImmutable()
    {
        var end = new EndNode<TestState>("end", "Approved");
        var source = new List<WorkflowNode<TestState>> { end };
        var sequence = new SequenceNode<TestState>("root", source);
        source.Add(new EndNode<TestState>("late", null));
        var definition = new WorkflowDefinition<TestState>(
            DefinitionId.New(),
            DefinitionVersion.Initial,
            sequence);

        sequence.Children.Should().Equal([end]);
        sequence.Children.Should().NotBeAssignableTo<IList<WorkflowNode<TestState>>>();
        PublicAndInternalProperties(typeof(WorkflowDefinition<TestState>))
            .Concat(PublicAndInternalProperties(typeof(SequenceNode<TestState>)))
            .Should().OnlyContain(property => property.SetMethod == null);
        definition.RootSequence.Children.Should().Equal([end]);
    }

    [Fact]
    public void NodeTree_Nesting_ComposesFreely()
    {
        var businessStep = new BusinessStepNode<TestState>("step", () => new TestStep());
        var wait = new WaitNode<TestState>("wait", "Approved", state => new CorrelationId(state.CorrelationId));
        var branch = new ParallelBranch<TestState>(
            new BranchId(0, "approval"),
            new SequenceNode<TestState>("approval-sequence", [wait]));
        var parallel = new ParallelNode<TestState>("parallel", [branch]);
        var ifNode = new IfNode<TestState>(
            "if",
            state => state.ShouldRoute,
            new SequenceNode<TestState>("then", [businessStep, parallel]),
            new SequenceNode<TestState>("else", [new EndNode<TestState>("else-end", "Skipped")]));

        var root = new SequenceNode<TestState>("root", [ifNode]);

        root.Children.Should().ContainSingle().Which.Should().Be(ifNode);
        ifNode.Then.Children.Should().Equal([businessStep, parallel]);
        parallel.Branches.Should().ContainSingle().Which.Sequence.Children.Should().ContainSingle().Which.Should().Be(wait);
    }

    [Fact]
    public void ExecutionPointer_PushPop_TracksNestedPosition()
    {
        var pointer = ExecutionPointer.Empty
            .Push(new ExecutionFrame("if", SequenceIndex: 0))
            .Push(new ExecutionFrame("if/then", BranchId: new BranchId(0, "then")))
            .Push(new ExecutionFrame("if/then/step", SequenceIndex: 2));

        pointer.Frames.Should().Equal(
            [
                new ExecutionFrame("if", SequenceIndex: 0),
                new ExecutionFrame("if/then", BranchId: new BranchId(0, "then")),
                new ExecutionFrame("if/then/step", SequenceIndex: 2)
            ]);
        pointer.Pop().Frames.Should().Equal(
            [
                new ExecutionFrame("if", SequenceIndex: 0),
                new ExecutionFrame("if/then", BranchId: new BranchId(0, "then"))
            ]);
    }

    [Fact]
    public void ExecutionPointer_ValueEquality_HoldsForSamePath()
    {
        var first = ExecutionPointer.Empty.Push(new ExecutionFrame("root", SequenceIndex: 1));
        var second = ExecutionPointer.Empty.Push(new ExecutionFrame("root", SequenceIndex: 1));

        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Fact]
    public void ExecutionPointer_LoopFrames_CarryIterationCounter()
    {
        var firstIteration = ExecutionPointer.Empty.Push(new ExecutionFrame("loop", LoopIteration: 0));
        var secondIteration = ExecutionPointer.Empty.Push(new ExecutionFrame("loop", LoopIteration: 1));

        firstIteration.Should().NotBe(secondIteration);
        firstIteration.Frames.Single().LoopIteration.Should().Be(0);
        secondIteration.Frames.Single().LoopIteration.Should().Be(1);
    }

    private static IEnumerable<PropertyInfo> PublicAndInternalProperties(Type type)
    {
        return type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(property => property.GetMethod is { IsPublic: true } or { IsAssembly: true });
    }

    private sealed record TestState(string CorrelationId = "order-123", bool ShouldRoute = true);

    private sealed class TestStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }
}
