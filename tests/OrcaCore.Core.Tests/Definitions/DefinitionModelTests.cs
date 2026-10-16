using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Definitions;
using Xunit;

namespace OrcaCore.Core.Tests.Definitions;

public sealed class DefinitionModelTests
{
    [Fact]
    public void CompiledPlanFingerprint_BindsTheFixedCodecFormat()
    {
        var definitionId = DefinitionId.Parse("018f3d31-7f2d-7ad0-a2b6-53e0ddcaf001");
        var plan = new CompiledWorkflowPlan(
            WorkflowExecutionMode.Durable,
            definitionId,
            DefinitionVersion.Initial,
            "codec-binding-test");
        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            "orcacore-json-v1|codec-binding-test")));

        plan.Fingerprint.Should().Be(expected);
    }

    [Fact]
    public void CompiledPlanFingerprint_ExcludesFormatModeIdentityVersionAndEveryCompilerOption()
    {
        var baseline = new CompiledWorkflowPlan(
            WorkflowExecutionMode.Ephemeral,
            DefinitionId.New(),
            DefinitionVersion.Initial,
            "same-structure",
            compilerOptions: new DefinitionCompilerOptions()).Fingerprint;
        var changed = new[]
        {
            new CompiledWorkflowPlan(
                WorkflowExecutionMode.Durable,
                DefinitionId.New(),
                new DefinitionVersion(99),
                "same-structure",
                compilerOptions: new DefinitionCompilerOptions
                {
                    MaxInternalInstructionsPerQuantum = 2048
                }).Fingerprint,
            new CompiledWorkflowPlan(
                WorkflowExecutionMode.Durable,
                DefinitionId.New(),
                new DefinitionVersion(2),
                "same-structure",
                compilerOptions: new DefinitionCompilerOptions
                {
                    MaxScopeDepth = 64
                }).Fingerprint,
            new CompiledWorkflowPlan(
                WorkflowExecutionMode.Ephemeral,
                DefinitionId.New(),
                new DefinitionVersion(3),
                "same-structure",
                compilerOptions: new DefinitionCompilerOptions
                {
                    MaxSerializedResultBytes = 1024
                }).Fingerprint,
            new CompiledWorkflowPlan(
                WorkflowExecutionMode.Ephemeral,
                DefinitionId.New(),
                new DefinitionVersion(4),
                "same-structure",
                compilerOptions: new DefinitionCompilerOptions
                {
                    MaxSerializedEnvelopeBytes = 8192
                }).Fingerprint
        };

        changed.Should().OnlyContain(fingerprint => fingerprint == baseline);
        typeof(DefinitionCompilerOptions).GetProperties()
            .Select(property => property.Name)
            .Should().BeEquivalentTo(
                nameof(DefinitionCompilerOptions.MaxInternalInstructionsPerQuantum),
                nameof(DefinitionCompilerOptions.MaxScopeDepth),
                nameof(DefinitionCompilerOptions.MaxSerializedResultBytes),
                nameof(DefinitionCompilerOptions.MaxSerializedEnvelopeBytes));
    }

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
            sequence,
            new CompiledWorkflowPlan(
                WorkflowExecutionMode.Ephemeral,
                DefinitionId.New(),
                DefinitionVersion.Initial,
                "definition-immutability-test"));

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
        var businessStep = new BusinessStepNode<TestState>(
            "step",
            () => new TestStep(),
            typeof(TestStep));
        var wait = new WaitNode<TestState>("wait", "Approved", state => CorrelationId.Create(state.CorrelationId));
        var nestedLoop = new WhileNode<TestState>(
            "loop",
            state => state.ShouldRoute,
            new SequenceNode<TestState>("loop-body", [wait]));
        var ifNode = new IfNode<TestState>(
            "if",
            state => state.ShouldRoute,
            new SequenceNode<TestState>("then", [businessStep, nestedLoop]),
            new SequenceNode<TestState>("else", [new EndNode<TestState>("else-end", "Skipped")]));

        var root = new SequenceNode<TestState>("root", [ifNode]);

        root.Children.Should().ContainSingle().Which.Should().Be(ifNode);
        ifNode.Then.Children.Should().Equal([businessStep, nestedLoop]);
        nestedLoop.Body.Children.Should().ContainSingle().Which.Should().Be(wait);
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
