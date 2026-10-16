using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Aggregates;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Continuations;

public sealed class ContinueAsNewAggregateTests
{
    [Fact]
    [Trait("AC", "AC-313")]
    public void ContinueAsNew_RunningInstance_EmitsRolloverAndCheckpointBaseline()
    {
        var aggregate = DurableWorkflowAggregate.Rehydrate(null, [Started(), StepCompleted()]);
        var command = ContinueAsNew();

        var decision = aggregate.DecideContinueAsNew(command);

        var continued = decision.Events.Should().ContainSingle()
            .Which.Should().BeOfType<WorkflowContinuedAsNewEvent>().Subject;
        continued.InstanceId.Should().Be(command.InstanceId);
        continued.Generation.Should().Be(1);
        continued.PreviousStreamVersion.Should().Be(new StreamVersion(2));
        continued.CommandId.Should().Be(command.CommandId);

        decision.Checkpoint.Should().NotBeNull();
        decision.Checkpoint!.InstanceId.Should().Be(command.InstanceId);
        decision.Checkpoint.StreamVersion.Should().Be(new StreamVersion(3));
        decision.Checkpoint.ContentType.Should().Be(command.StateContentType);
        decision.Checkpoint.Payload.Should().Equal(command.StatePayload);
        decision.Checkpoint.DefinitionId.Should().Be(DefinitionIdValue(1));
        decision.Checkpoint.DefinitionVersion.Should().Be(new DefinitionVersion(7));
        decision.Checkpoint.Status.Should().Be(WorkflowStatus.Running);
        decision.Checkpoint.LastStepPath.Should().Be("root/1");
        decision.Checkpoint.ErrorSummary.Should().BeNull();
        decision.Checkpoint.OutcomeName.Should().BeNull();
        decision.Checkpoint.ContinueAsNewGeneration.Should().Be(1);
    }

    [Fact]
    [Trait("AC", "AC-313")]
    public void ContinueAsNew_CarriesSagaAndResumeTokenRuntimeState()
    {
        var recordedTokenId = EventIdValue(5);
        var consumedTokenId = EventIdValue(6);
        var aggregate = DurableWorkflowAggregate.Rehydrate(
            null,
            [
                Started(),
                ForwardActionCompleted(),
                CompensationRequested(),
                ParentResumeTokenRecorded(recordedTokenId),
                ParentResumeTokenConsumed(consumedTokenId)
            ]);

        var decision = aggregate.DecideContinueAsNew(ContinueAsNew(7));

        decision.Checkpoint.Should().NotBeNull();
        var runtimeState = decision.Checkpoint!.RuntimeState;
        runtimeState.CompletedSagaForwardActions.Should().ContainSingle()
            .Which.Should().Be(new CheckpointSagaForwardAction(
                "checkout",
                "reserve-stock",
                "release-stock",
                Timestamp(2)));
        runtimeState.RequestedSagaCompensationScopes.Should().ContainSingle().Which.Should().Be("checkout");
        runtimeState.RecordedParentResumeTokens.Should().ContainSingle().Which.Should().Be(recordedTokenId);
        runtimeState.ConsumedParentResumeTokens.Should().ContainSingle().Which.Should().Be(consumedTokenId);
        runtimeState.ActiveTimers.Should().BeEmpty();
        runtimeState.ActiveWaits.Should().BeEmpty();
        runtimeState.ActiveChildren.Should().BeEmpty();
    }

    [Fact]
    [Trait("AC", "AC-313")]
    public void ContinueAsNew_PreservesLogicalIdentityAndIncrementsGeneration()
    {
        var aggregate = DurableWorkflowAggregate.Rehydrate(null, [Started(), StepCompleted()]);

        var firstDecision = aggregate.DecideContinueAsNew(ContinueAsNew());
        var first = DurableWorkflowAggregate.Rehydrate(null, [Started(), StepCompleted(), .. firstDecision.Events]);
        var secondDecision = first.DecideContinueAsNew(ContinueAsNew(4));
        var second = DurableWorkflowAggregate.Rehydrate(null, [Started(), StepCompleted(), .. firstDecision.Events, .. secondDecision.Events]);

        first.Snapshot.InstanceId.Should().Be(InstanceIdValue(1));
        second.Snapshot.InstanceId.Should().Be(InstanceIdValue(1));
        second.Snapshot.DefinitionId.Should().Be(DefinitionIdValue(1));
        second.Snapshot.DefinitionVersion.Should().Be(new DefinitionVersion(7));
        second.Snapshot.RootInstanceId.Should().Be(InstanceIdValue(1));
        second.Snapshot.ContinueAsNewGeneration.Should().Be(2);
        secondDecision.Events.Should().ContainSingle()
            .Which.Should().BeOfType<WorkflowContinuedAsNewEvent>()
            .Which.Generation.Should().Be(2);
    }

    [Fact]
    [Trait("AC", "AC-313")]
    public void ContinueAsNew_TerminalInstance_IsRejected()
    {
        var aggregate = DurableWorkflowAggregate.Rehydrate(null, [Started(), Terminal()]);

        var decision = aggregate.DecideContinueAsNew(ContinueAsNew());

        decision.Events.Should().BeEmpty();
        decision.Checkpoint.Should().BeNull();
    }

    private static ContinueAsNewCommand ContinueAsNew(int commandValue = 3)
    {
        return new ContinueAsNewCommand
        {
            CommandId = CommandIdValue(commandValue),
            InstanceId = InstanceIdValue(1),
            RequestedAt = Timestamp(commandValue),
            StateContentType = "application/json",
            StatePayload = [1, 2, (byte)commandValue]
        };
    }

    private static WorkflowStartedEvent Started()
    {
        return new WorkflowStartedEvent
        {
            EventId = EventIdValue(1),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(1),
            CausationId = CausationIdValue(1),
            OccurredAt = Timestamp(1),
            DefinitionId = DefinitionIdValue(1),
            DefinitionVersion = new DefinitionVersion(7)
        };
    }

    private static SagaForwardActionCompletedEvent ForwardActionCompleted()
    {
        return new SagaForwardActionCompletedEvent
        {
            EventId = EventIdValue(2),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(2),
            CausationId = CausationIdValue(2),
            OccurredAt = Timestamp(2),
            ScopeId = "checkout",
            ActionKey = "reserve-stock",
            CompensationKey = "release-stock"
        };
    }

    private static SagaCompensationRequestedEvent CompensationRequested()
    {
        return new SagaCompensationRequestedEvent
        {
            EventId = EventIdValue(3),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(3),
            CausationId = CausationIdValue(3),
            OccurredAt = Timestamp(3),
            ScopeId = "checkout",
            Reason = "driver rollover"
        };
    }

    private static WorkflowParentResumeTokenRecordedEvent ParentResumeTokenRecorded(EventId resumeTokenId)
    {
        return new WorkflowParentResumeTokenRecordedEvent
        {
            EventId = EventIdValue(4),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(4),
            CausationId = CausationIdValue(4),
            OccurredAt = Timestamp(4),
            GroupId = "children",
            ResumeTokenId = resumeTokenId
        };
    }

    private static WorkflowParentResumeTokenConsumedEvent ParentResumeTokenConsumed(EventId resumeTokenId)
    {
        return new WorkflowParentResumeTokenConsumedEvent
        {
            EventId = EventIdValue(7),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(7),
            CausationId = CausationIdValue(7),
            OccurredAt = Timestamp(7),
            GroupId = "children",
            ResumeTokenId = resumeTokenId
        };
    }

    private static WorkflowStepCompletedEvent StepCompleted()
    {
        return new WorkflowStepCompletedEvent
        {
            EventId = EventIdValue(2),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(2),
            CausationId = CausationIdValue(2),
            OccurredAt = Timestamp(2),
            StepPath = "root/1"
        };
    }

    private static WorkflowTerminalEvent Terminal()
    {
        return new WorkflowTerminalEvent
        {
            EventId = EventIdValue(2),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(2),
            CausationId = CausationIdValue(2),
            OccurredAt = Timestamp(2),
            Status = WorkflowStatus.Completed
        };
    }

    private static DateTimeOffset Timestamp(int seconds)
    {
        return new DateTimeOffset(2026, 7, 2, 12, 0, seconds, TimeSpan.Zero);
    }

    private static EventId EventIdValue(int value)
    {
        return EventId.Create(GuidValue(value).ToString());
    }

    private static InstanceId InstanceIdValue(int value)
    {
        return InstanceId.Parse(GuidValue(value).ToString());
    }

    private static CommandId CommandIdValue(int value)
    {
        return new CommandId(GuidValue(value));
    }

    private static CausationId CausationIdValue(int value)
    {
        return new CausationId(GuidValue(value));
    }

    private static DefinitionId DefinitionIdValue(int value)
    {
        return DefinitionId.Parse(GuidValue(value).ToString());
    }

    private static Guid GuidValue(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }
}
