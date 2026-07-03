using AwesomeAssertions;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Core.Lifecycle;

namespace OrcaCore.Core.Tests.Lifecycle;

public sealed class LifecycleMachineTests
{
    private static (WorkflowStatus Current, LifecycleTrigger Trigger, WorkflowStatus Expected)[] LegalTransitions =>
    [
        (WorkflowStatus.Running, LifecycleTrigger.EnterWait, WorkflowStatus.Waiting),
        (WorkflowStatus.Waiting, LifecycleTrigger.MatchWait, WorkflowStatus.Running),
        (WorkflowStatus.Running, LifecycleTrigger.Complete, WorkflowStatus.Completed),
        (WorkflowStatus.Running, LifecycleTrigger.Fail, WorkflowStatus.Failed),
        (WorkflowStatus.Running, LifecycleTrigger.Cancel, WorkflowStatus.Cancelled),
        (WorkflowStatus.Waiting, LifecycleTrigger.Cancel, WorkflowStatus.Cancelled),
        (WorkflowStatus.Running, LifecycleTrigger.Terminate, WorkflowStatus.Terminated),
        (WorkflowStatus.Waiting, LifecycleTrigger.Terminate, WorkflowStatus.Terminated),
        (WorkflowStatus.Running, LifecycleTrigger.Pause, WorkflowStatus.Paused),
        (WorkflowStatus.Waiting, LifecycleTrigger.Pause, WorkflowStatus.Paused),
        (WorkflowStatus.Paused, LifecycleTrigger.Resume, WorkflowStatus.Running),
        (WorkflowStatus.Paused, LifecycleTrigger.Terminate, WorkflowStatus.Terminated),
        (WorkflowStatus.Paused, LifecycleTrigger.Cancel, WorkflowStatus.Cancelled),
    ];

    private static (WorkflowStatus Current, LifecycleTrigger Trigger)[] IllegalTransitions
    {
        get
        {
            var terminals = new[]
            {
                WorkflowStatus.Completed,
                WorkflowStatus.Failed,
                WorkflowStatus.Cancelled,
                WorkflowStatus.Terminated,
            };
            var allTriggers = Enum.GetValues<LifecycleTrigger>();

            var fromTerminals = terminals.SelectMany(
                status => allTriggers,
                (status, trigger) => (status, trigger));

            return
            [
                .. fromTerminals,
                (WorkflowStatus.Waiting, LifecycleTrigger.Complete),
                (WorkflowStatus.Paused, LifecycleTrigger.MatchWait),
            ];
        }
    }

    public static IEnumerable<object[]> LegalTransitionTheoryData() =>
        LegalTransitions.Select(t => new object[] { t.Current, (int)t.Trigger, t.Expected });

    public static IEnumerable<object[]> IllegalTransitionTheoryData() =>
        IllegalTransitions.Select(t => new object[] { t.Current, (int)t.Trigger });

    [Theory]
    [MemberData(nameof(LegalTransitionTheoryData))]
    public void Fire_LegalTransitions_ReturnTargetStatus(
        WorkflowStatus current,
        int triggerValue,
        WorkflowStatus expected)
    {
        var trigger = (LifecycleTrigger)triggerValue;
        var result = LifecycleMachine.Fire(current, trigger);

        result.IsSuccess.Should().BeTrue($"{current} + {trigger} must be legal");
        result.Value.Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(IllegalTransitionTheoryData))]
    public void Fire_IllegalTrigger_FailsWithClearMessage(
        WorkflowStatus current,
        int triggerValue)
    {
        var trigger = (LifecycleTrigger)triggerValue;
        var result = LifecycleMachine.Fire(current, trigger);

        result.IsFailure.Should().BeTrue($"{current} + {trigger} must be illegal");
        result.Error.Should().BeOfType<WorkflowLifecycleException>();
        result.Error.Message.Should().Contain(current.ToString());
        result.Error.Message.Should().Contain(trigger.ToString());
    }

    [Fact]
    public void Table_CoversEveryStatus()
    {
        foreach (var status in Enum.GetValues<WorkflowStatus>())
        {
            var isSource = Enum.GetValues<LifecycleTrigger>()
                .Any(trigger => LifecycleMachine.Fire(status, trigger).IsSuccess);
            var isTerminal = LifecycleMachine.TerminalStatuses.Contains(status);

            (isSource || isTerminal).Should().BeTrue($"{status} must be a source or declared terminal");
        }
    }

    [Fact]
    public void TerminalStatuses_AreExactly_Completed_Failed_Cancelled_Terminated()
    {
        LifecycleMachine.TerminalStatuses.Should().BeEquivalentTo(
        [
            WorkflowStatus.Completed,
            WorkflowStatus.Failed,
            WorkflowStatus.Cancelled,
            WorkflowStatus.Terminated,
        ]);
    }
}
