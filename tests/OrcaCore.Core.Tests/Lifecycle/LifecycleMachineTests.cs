using AwesomeAssertions;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Core.Lifecycle;
using Xunit;

namespace OrcaCore.Core.Tests.Lifecycle;

public sealed class LifecycleMachineTests
{
    [Theory]
    [InlineData(WorkflowStatus.Running, (int)LifecycleTrigger.EnterWait, WorkflowStatus.Waiting)]
    [InlineData(WorkflowStatus.Waiting, (int)LifecycleTrigger.MatchWait, WorkflowStatus.Running)]
    [InlineData(WorkflowStatus.Running, (int)LifecycleTrigger.Complete, WorkflowStatus.Completed)]
    [InlineData(WorkflowStatus.Running, (int)LifecycleTrigger.Fail, WorkflowStatus.Failed)]
    [InlineData(WorkflowStatus.Running, (int)LifecycleTrigger.Cancel, WorkflowStatus.Cancelled)]
    [InlineData(WorkflowStatus.Waiting, (int)LifecycleTrigger.Cancel, WorkflowStatus.Cancelled)]
    [InlineData(WorkflowStatus.Running, (int)LifecycleTrigger.Terminate, WorkflowStatus.Terminated)]
    [InlineData(WorkflowStatus.Waiting, (int)LifecycleTrigger.Terminate, WorkflowStatus.Terminated)]
    [InlineData(WorkflowStatus.Running, (int)LifecycleTrigger.Compensate, WorkflowStatus.Compensated)]
    [InlineData(WorkflowStatus.Waiting, (int)LifecycleTrigger.Compensate, WorkflowStatus.Compensated)]
    [InlineData(WorkflowStatus.Running, (int)LifecycleTrigger.FailCompensation, WorkflowStatus.CompensationFailed)]
    [InlineData(WorkflowStatus.Waiting, (int)LifecycleTrigger.FailCompensation, WorkflowStatus.CompensationFailed)]
    [InlineData(WorkflowStatus.Running, (int)LifecycleTrigger.Pause, WorkflowStatus.Paused)]
    [InlineData(WorkflowStatus.Waiting, (int)LifecycleTrigger.Pause, WorkflowStatus.Paused)]
    [InlineData(WorkflowStatus.Paused, (int)LifecycleTrigger.Resume, WorkflowStatus.Running)]
    [InlineData(WorkflowStatus.Paused, (int)LifecycleTrigger.Terminate, WorkflowStatus.Terminated)]
    [InlineData(WorkflowStatus.Paused, (int)LifecycleTrigger.Cancel, WorkflowStatus.Cancelled)]
    [InlineData(WorkflowStatus.CancellationRequested, (int)LifecycleTrigger.Cancel, WorkflowStatus.Cancelled)]
    [InlineData(WorkflowStatus.CancellationRequested, (int)LifecycleTrigger.Terminate, WorkflowStatus.Terminated)]
    public void Fire_LegalTransitions_ReturnTargetStatus(
        WorkflowStatus current,
        int triggerValue,
        WorkflowStatus expected)
    {
        var trigger = (LifecycleTrigger)triggerValue;

        var result = LifecycleMachine.Fire(current, trigger);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(IllegalTransitions))]
    public void Fire_IllegalTrigger_FailsWithClearMessage(WorkflowStatus current, int triggerValue)
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

        foreach (var status in Enum.GetValues<WorkflowStatus>())
        {
            (coveredSources.Contains(status) || LifecycleMachine.TerminalStatuses.Contains(status))
                .Should().BeTrue($"{status} must be a source status or declared terminal");
        }
    }

    [Fact]
    public void TerminalStatuses_AreExactly_RegularAndSagaTerminalStates()
    {
        LifecycleMachine.TerminalStatuses.Order().Should().Equal(
            [
                WorkflowStatus.Completed,
                WorkflowStatus.Failed,
                WorkflowStatus.Cancelled,
                WorkflowStatus.Terminated,
                WorkflowStatus.Compensated,
                WorkflowStatus.CompensationFailed,
                WorkflowStatus.TimedOut
            ]);
    }

    public static TheoryData<WorkflowStatus, int> IllegalTransitions()
    {
        var data = new TheoryData<WorkflowStatus, int>();
        foreach (var terminalStatus in new[]
                 {
                     WorkflowStatus.Completed,
                     WorkflowStatus.Failed,
                     WorkflowStatus.Cancelled,
                     WorkflowStatus.Terminated,
                     WorkflowStatus.Compensated,
                     WorkflowStatus.CompensationFailed
                 })
        {
            foreach (var trigger in Enum.GetValues<LifecycleTrigger>())
            {
                data.Add(terminalStatus, (int)trigger);
            }
        }

        data.Add(WorkflowStatus.Waiting, (int)LifecycleTrigger.Complete);
        data.Add(WorkflowStatus.Paused, (int)LifecycleTrigger.MatchWait);

        return data;
    }
}
