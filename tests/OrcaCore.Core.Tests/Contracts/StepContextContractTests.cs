using System.Reflection;
using AwesomeAssertions;
using Xunit;

namespace OrcaCore.Core.Tests.Contracts;

public sealed class StepContextContractTests
{
    [Fact]
    public void ExecutionContext_IsImmutableAndRejectsInvalidAttempts()
    {
        var constructor = typeof(StepExecutionContext).GetConstructors(
            BindingFlags.Instance | BindingFlags.NonPublic).Should().ContainSingle().Subject;
        var instanceId = InstanceId.Parse("018f3d31-7f2d-7ad0-a2b6-53e0ddcaf001");
        var operationId = StepOperationId.Parse("step:00000001");

        var context = (StepExecutionContext)constructor.Invoke([instanceId, operationId, 2]);

        context.WorkflowInstanceId.Should().Be(instanceId);
        context.OperationId.Should().Be(operationId);
        context.AttemptNumber.Should().Be(2);
        Action invalid = () => constructor.Invoke([instanceId, operationId, 0]);
        invalid.Should().Throw<TargetInvocationException>()
            .WithInnerException<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ReplaceState_ChangesOnlyAttemptLocalState()
    {
        var executionConstructor = typeof(StepExecutionContext).GetConstructors(
            BindingFlags.Instance | BindingFlags.NonPublic).Single();
        var execution = (StepExecutionContext)executionConstructor.Invoke([
            InstanceId.Parse("018f3d31-7f2d-7ad0-a2b6-53e0ddcaf001"),
            StepOperationId.Parse("step:00000001"),
            1]);
        var contextConstructor = typeof(StepContext<State>).GetConstructors(
            BindingFlags.Instance | BindingFlags.NonPublic).Single();
        var original = new State(1);
        var replacement = new State(2);
        var context = (StepContext<State>)contextConstructor.Invoke([
            original, execution, null, TimeProvider.System, null, null]);

        context.ReplaceState(replacement);

        context.State.Should().BeSameAs(replacement);
        original.Value.Should().Be(1);
        Action replaceNull = () => context.ReplaceState(null!);
        replaceNull.Should().Throw<ArgumentNullException>().WithParameterName("replacement");
        typeof(StepContext<State>).GetConstructors(BindingFlags.Instance | BindingFlags.Public)
            .Should().BeEmpty();
    }

    private sealed record State(int Value);
}
