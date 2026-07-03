using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Definitions;

namespace OrcaCore.Core.Tests.Definitions;

public class DefinitionModelTests
{
    private sealed class FakeState
    {
        public int Value { get; set; }
    }

    private sealed class FakeStep : IStep<FakeState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<FakeState> context, CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }

    [Fact]
    public void Definition_IsDeeplyImmutable()
    {
        var init = new InitNode<FakeState, int>(input => new FakeState { Value = input });
        var end = new EndNode("Approved");
        var root = new SequenceNode([init, end]);

        var definition = new WorkflowDefinition<FakeState>(
            new DefinitionId("order-workflow"),
            new DefinitionVersion(1),
            root);

        definition.Should().BeEquivalentTo(definition);
        root.Steps.Should().BeAssignableTo<IReadOnlyList<DefinitionNode>>();

        // Records expose no setters — verified via reflection: every public property is get-only (init or none).
        var properties = typeof(WorkflowDefinition<FakeState>).GetProperties();
        properties.Should().NotBeEmpty();
        foreach (var property in properties)
        {
            var setMethod = property.SetMethod;
            if (setMethod is not null)
            {
                setMethod.ReturnParameter.GetRequiredCustomModifiers()
                    .Should().Contain(typeof(System.Runtime.CompilerServices.IsExternalInit),
                        $"{property.Name} must be init-only, not fully mutable");
            }
        }
    }

    [Fact]
    public void NodeTree_Nesting_ComposesFreely()
    {
        var waitNode = new WaitNode("ApprovalReceived", _ => new CorrelationId("order-1"));
        var branchA = new ParallelBranch(new BranchId(0, "A"), new SequenceNode([waitNode]));
        var branchB = new ParallelBranch(new BranchId(1, "B"), new SequenceNode([new EndNode(null)]));
        var parallel = new ParallelNode([branchA, branchB]);

        var ifNode = new IfNode(
            _ => true,
            new SequenceNode([parallel]),
            new SequenceNode([]));

        var root = new SequenceNode([ifNode]);

        root.Steps.Should().ContainSingle().Which.Should().BeOfType<IfNode>();
        var reconstructedIf = (IfNode)root.Steps[0];
        reconstructedIf.Then.Steps.Should().ContainSingle().Which.Should().BeOfType<ParallelNode>();

        var reconstructedParallel = (ParallelNode)reconstructedIf.Then.Steps[0];
        reconstructedParallel.Branches.Should().HaveCount(2);
        reconstructedParallel.Branches[0].Body.Steps.Should().ContainSingle().Which.Should().BeOfType<WaitNode>();
        ((WaitNode)reconstructedParallel.Branches[0].Body.Steps[0]).EventName.Should().Be("ApprovalReceived");
    }

    [Fact]
    public void ExecutionPointer_PushPop_TracksNestedPosition()
    {
        var root = ExecutionPointer.Empty
            .Push(Frame.AtSequenceIndex(0))
            .Push(Frame.AtSequenceIndex(1));

        root.Frames.Should().HaveCount(2);
        root.Frames[0].Should().Be(Frame.AtSequenceIndex(0));
        root.Frames[1].Should().Be(Frame.AtSequenceIndex(1));

        var popped = root.Pop();

        popped.Frames.Should().ContainSingle();
        popped.Frames[0].Should().Be(Frame.AtSequenceIndex(0));
    }

    [Fact]
    public void ExecutionPointer_ValueEquality_HoldsForSamePath()
    {
        var pointerA = ExecutionPointer.Empty
            .Push(Frame.AtSequenceIndex(0))
            .Push(Frame.InBranch(new BranchId(1, "B")));

        var pointerB = ExecutionPointer.Empty
            .Push(Frame.AtSequenceIndex(0))
            .Push(Frame.InBranch(new BranchId(1, "B")));

        pointerA.Should().Be(pointerB);
        (pointerA == pointerB).Should().BeTrue();
        pointerA.GetHashCode().Should().Be(pointerB.GetHashCode());
    }

    [Fact]
    public void ExecutionPointer_LoopFrames_CarryIterationCounter()
    {
        var iteration0 = ExecutionPointer.Empty.Push(Frame.AtLoopIteration(0));
        var iteration1 = ExecutionPointer.Empty.Push(Frame.AtLoopIteration(1));

        iteration0.Should().NotBe(iteration1);
        iteration0.Frames[0].LoopIteration.Should().Be(0);
        iteration1.Frames[0].LoopIteration.Should().Be(1);
    }
}
