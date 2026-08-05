using AwesomeAssertions;
using OrcaCore;
using OrcaCore.Core.Lifecycle;
using Xunit;

namespace OrcaCore.Core.Tests.Lifecycle;

public sealed class LifecycleMachineTests
{
    [Theory]
    [InlineData(WorkflowInstanceStatus.Pending, (int)LifecycleTrigger.Start, WorkflowInstanceStatus.Running)]
    [InlineData(WorkflowInstanceStatus.Running, (int)LifecycleTrigger.EnterWait, WorkflowInstanceStatus.Waiting)]
    [InlineData(WorkflowInstanceStatus.Waiting, (int)LifecycleTrigger.MatchWait, WorkflowInstanceStatus.Running)]
    [InlineData(WorkflowInstanceStatus.Running, (int)LifecycleTrigger.Complete, WorkflowInstanceStatus.Completed)]
    [InlineData(WorkflowInstanceStatus.Running, (int)LifecycleTrigger.Fail, WorkflowInstanceStatus.Failed)]
    [InlineData(WorkflowInstanceStatus.Running, (int)LifecycleTrigger.Cancel, WorkflowInstanceStatus.Cancelled)]
    [InlineData(WorkflowInstanceStatus.Waiting, (int)LifecycleTrigger.Cancel, WorkflowInstanceStatus.Cancelled)]
    [InlineData(WorkflowInstanceStatus.Running, (int)LifecycleTrigger.Terminate, WorkflowInstanceStatus.Terminated)]
    [InlineData(WorkflowInstanceStatus.Waiting, (int)LifecycleTrigger.Terminate, WorkflowInstanceStatus.Terminated)]
    [InlineData(WorkflowInstanceStatus.CancellationRequested, (int)LifecycleTrigger.Cancel, WorkflowInstanceStatus.Cancelled)]
    [InlineData(WorkflowInstanceStatus.CancellationRequested, (int)LifecycleTrigger.Terminate, WorkflowInstanceStatus.Terminated)]
    public void Fire_LegalTransitions_ReturnTargetStatus(
        WorkflowInstanceStatus current,
        int triggerValue,
        WorkflowInstanceStatus expected)
    {
        var trigger = (LifecycleTrigger)triggerValue;

        var result = LifecycleMachine.Fire(current, trigger);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(IllegalTransitions))]
    public void Fire_IllegalTrigger_FailsWithClearMessage(WorkflowInstanceStatus current, int triggerValue)
    {
        var trigger = (LifecycleTrigger)triggerValue;

        var result = LifecycleMachine.Fire(current, trigger);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain(current.ToString());
        result.Error.Message.Should().Contain(trigger.ToString());
    }

    [Fact]
    public void Table_CoversEveryStatus()
    {
        var coveredSources = LifecycleMachine.Transitions.Keys.Select(key => key.Current).ToHashSet();

        foreach (var status in Enum.GetValues<WorkflowInstanceStatus>())
        {
            (coveredSources.Contains(status) || LifecycleMachine.TerminalStatuses.Contains(status))
                .Should().BeTrue($"{status} must be a source status or declared terminal");
        }
    }

    [Fact]
    public void TerminalStatuses_AreExactly_V1TerminalStates()
    {
        LifecycleMachine.TerminalStatuses.Order().Should().Equal(
            [
                WorkflowInstanceStatus.Completed,
                WorkflowInstanceStatus.Failed,
                WorkflowInstanceStatus.TimedOut,
                WorkflowInstanceStatus.Cancelled,
                WorkflowInstanceStatus.Terminated
            ]);
    }

    public static TheoryData<WorkflowInstanceStatus, int> IllegalTransitions()
    {
        var data = new TheoryData<WorkflowInstanceStatus, int>();
        foreach (var terminalStatus in new[]
                 {
                     WorkflowInstanceStatus.Completed,
                     WorkflowInstanceStatus.Failed,
                     WorkflowInstanceStatus.TimedOut,
                     WorkflowInstanceStatus.Cancelled,
                     WorkflowInstanceStatus.Terminated
                 })
        {
            foreach (var trigger in Enum.GetValues<LifecycleTrigger>())
            {
                data.Add(terminalStatus, (int)trigger);
            }
        }

        data.Add(WorkflowInstanceStatus.Waiting, (int)LifecycleTrigger.Complete);

        return data;
    }
}
