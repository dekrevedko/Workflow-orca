using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Execution;

public sealed class DurableCheckpointMapperTests
{
    [Fact]
    public void ToAggregateCheckpoint_MapsProviderCheckpointRuntimeState()
    {
        var child = new WorkflowChildMaterialization
        {
            Index = 0,
            ChildInstanceId = InstanceIdValue(11),
            ChildDefinitionId = DefinitionIdValue(12),
            ChildDefinitionVersion = new DefinitionVersion(2),
            ItemSnapshot = "item-0"
        };
        var ticket = new ResourcePoolTicket(
            GuidValue(20),
            "workers",
            2,
            InstanceIdValue(1),
            "holder-1",
            Timestamp(2),
            Timestamp(60));
        var checkpoint = new CheckpointWrite(
            InstanceIdValue(1),
            new StreamVersion(42),
            "application/json",
            [1, 2, 3])
        {
            ParentInstanceId = InstanceIdValue(2),
            RootInstanceId = InstanceIdValue(3),
            DefinitionId = DefinitionIdValue(4),
            DefinitionVersion = new DefinitionVersion(5),
            Status = WorkflowStatus.Waiting,
            LastStepPath = "approve",
            ErrorSummary = "none",
            OutcomeName = "accepted",
            ContinueAsNewGeneration = 6,
            RuntimeState = new WorkflowRuntimeCheckpointState
            {
                ActiveTimers =
                [
                    new CheckpointActiveTimer(
                        TimerIdValue(7),
                        Timestamp(30),
                        "timeout",
                        Timestamp(3))
                ],
                ActiveWaits =
                [
                    new CheckpointActiveWait(
                        WaitIdValue(8),
                        "Approved",
                        new CorrelationId("order-1"),
                        Timestamp(4),
                        WaitMode.Cold,
                        "branch-a")
                ],
                BufferedDeliveries =
                [
                    new CheckpointBufferedDelivery(
                        EventIdValue(9),
                        "Approved",
                        new CorrelationId("order-1"),
                        "branch-a")
                ],
                BufferedTimers =
                [
                    new CheckpointBufferedTimer(
                        TimerIdValue(10),
                        "paused-timeout",
                        Timestamp(5))
                ],
                ActiveChildren =
                [
                    new CheckpointActiveChild(
                        "group-1",
                        InstanceIdValue(11),
                        WaitIdValue(12),
                        RunChildFailurePolicy.PropagateFailure,
                        RunChildrenJoinPolicy.WhenAny,
                        RunChildrenResidualPolicy.CancelRemaining,
                        "item-0")
                ],
                ActiveChildGroups =
                [
                    new CheckpointActiveChildGroup(
                        "group-1",
                        RunChildFailurePolicy.PropagateFailure,
                        RunChildrenJoinPolicy.WhenAll,
                        RunChildrenResidualPolicy.DetachRemaining,
                        3,
                        1,
                        [child])
                ],
                ActiveResourceTickets = [ticket],
                ActiveExternalJobs =
                [
                    new CheckpointActiveExternalJob(
                        "job-1",
                        WaitIdValue(13),
                        TimerIdValue(14))
                ]
            }
        };

        var aggregate = DurableCheckpointMapper.ToAggregateCheckpoint(checkpoint);

        aggregate.InstanceId.Should().Be(InstanceIdValue(1));
        aggregate.StreamVersion.Should().Be(new StreamVersion(42));
        aggregate.ParentInstanceId.Should().Be(InstanceIdValue(2));
        aggregate.RootInstanceId.Should().Be(InstanceIdValue(3));
        aggregate.DefinitionId.Should().Be(DefinitionIdValue(4));
        aggregate.DefinitionVersion.Should().Be(new DefinitionVersion(5));
        aggregate.Status.Should().Be(WorkflowStatus.Waiting);
        aggregate.CreatedAt.Should().BeNull();
        aggregate.UpdatedAt.Should().BeNull();
        aggregate.LastStepPath.Should().Be("approve");
        aggregate.ErrorSummary.Should().Be("none");
        aggregate.OutcomeName.Should().Be("accepted");
        aggregate.ContinueAsNewGeneration.Should().Be(6);
        aggregate.ContentType.Should().Be("application/json");
        aggregate.Payload.Should().Equal(1, 2, 3);
        aggregate.ActiveTimers.Should().ContainSingle().Which.Should().Be(
            new DurableActiveTimer(TimerIdValue(7), Timestamp(30), "timeout", Timestamp(3)));
        aggregate.ActiveWaits.Should().ContainSingle().Which.Should().Be(
            new DurableActiveWait(
                WaitIdValue(8),
                "Approved",
                new CorrelationId("order-1"),
                Timestamp(4),
                WaitMode.Cold,
                "branch-a"));
        aggregate.BufferedDeliveries.Should().ContainSingle().Which.Should().Be(
            new DurableBufferedDelivery(EventIdValue(9), "Approved", new CorrelationId("order-1"), "branch-a"));
        aggregate.BufferedTimers.Should().ContainSingle().Which.Should().Be(
            new DurableBufferedTimer(TimerIdValue(10), "paused-timeout", Timestamp(5)));
        aggregate.ActiveChildren.Should().ContainSingle().Which.Should().Be(
            new DurableActiveChild(
                "group-1",
                InstanceIdValue(11),
                WaitIdValue(12),
                RunChildFailurePolicy.PropagateFailure,
                RunChildrenJoinPolicy.WhenAny,
                RunChildrenResidualPolicy.CancelRemaining,
                "item-0"));
        var activeGroup = aggregate.ActiveChildGroups.Should().ContainSingle().Which;
        activeGroup.GroupId.Should().Be("group-1");
        activeGroup.MaxConcurrency.Should().Be(3);
        activeGroup.NextDispatchIndex.Should().Be(1);
        activeGroup.Children.Should().ContainSingle().Which.Should().Be(child);
        aggregate.ActiveResourceTickets.Should().ContainSingle().Which.Should().Be(ticket);
        aggregate.ActiveExternalJobs.Should().ContainSingle().Which.Should().Be(
            new DurableActiveExternalJob("job-1", WaitIdValue(13), TimerIdValue(14)));
    }

    [Fact]
    public void ToAggregateCheckpoint_CopiesPayloadBytes()
    {
        var payload = new byte[] { 1, 2, 3 };
        var checkpoint = new CheckpointWrite(
            InstanceIdValue(1),
            new StreamVersion(1),
            "application/octet-stream",
            payload);

        var aggregate = DurableCheckpointMapper.ToAggregateCheckpoint(checkpoint);
        payload[0] = 9;

        aggregate.Payload.Should().Equal(1, 2, 3);
        aggregate.Payload.Should().NotBeSameAs(payload);
    }

    private static DateTimeOffset Timestamp(int seconds)
    {
        return new DateTimeOffset(2026, 7, 3, 22, 0, 0, TimeSpan.Zero).AddSeconds(seconds);
    }

    private static DefinitionId DefinitionIdValue(int value)
    {
        return new DefinitionId(GuidValue(value));
    }

    private static EventId EventIdValue(int value)
    {
        return new EventId(GuidValue(value));
    }

    private static InstanceId InstanceIdValue(int value)
    {
        return new InstanceId(GuidValue(value));
    }

    private static TimerId TimerIdValue(int value)
    {
        return new TimerId(GuidValue(value));
    }

    private static WaitId WaitIdValue(int value)
    {
        return new WaitId(GuidValue(value));
    }

    private static Guid GuidValue(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }
}
