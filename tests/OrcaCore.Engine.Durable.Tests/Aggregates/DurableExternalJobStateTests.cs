using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Aggregates;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Aggregates;

public sealed class DurableExternalJobStateTests
{
    [Fact]
    public void ApplyStartedThenCompleted_TracksActiveJob()
    {
        var state = DurableExternalJobState.FromSnapshot([]);

        state.Apply(Started("job-1", WaitIdValue(1), TimerIdValue(1)));
        state.Find("job-1")!.FiberId.Should().Be(new FiberId("job-fiber"));
        state.Find("job-1")!.ScopeId.Should().Be(new ScopeId("job-scope"));

        state.Apply(Completed("job-1"));
        state.Find("job-1").Should().BeNull();
    }

    [Fact]
    public void CreateStopRequestedEvents_UsesActiveJobsInOrder()
    {
        var state = DurableExternalJobState.FromSnapshot(
            [
                new DurableActiveExternalJob("job-a", WaitIdValue(1), null),
                new DurableActiveExternalJob("job-b", WaitIdValue(2), TimerIdValue(2))
            ]);

        var events = state.CreateStopRequestedEvents(ExternalJobContext(CommandIdValue(10), Timestamp(10)));

        events.Select(stop => stop.ExternalJobId).Should().Equal("job-a", "job-b");
        events.Should().AllSatisfy(stop =>
        {
            stop.InstanceId.Should().Be(InstanceIdValue(1));
            stop.CommandId.Should().Be(CommandIdValue(10));
            stop.CausationId.Should().Be(CausationIdValue(10));
            stop.OccurredAt.Should().Be(Timestamp(10));
        });
    }

    [Fact]
    public void CreateCheckpointActiveExternalJobs_RoundTripsActiveJobs()
    {
        var state = DurableExternalJobState.FromSnapshot(
            [new DurableActiveExternalJob("job-1", WaitIdValue(1), TimerIdValue(2))
            {
                FiberId = new FiberId("job-fiber"),
                ScopeId = new ScopeId("job-scope")
            }]);

        var checkpointJobs = state.CreateCheckpointActiveExternalJobs();

        var checkpoint = checkpointJobs.Should().ContainSingle().Which;
        checkpoint.FiberId.Should().Be(new FiberId("job-fiber"));
        checkpoint.ScopeId.Should().Be(new ScopeId("job-scope"));
    }

    private static WorkflowExternalJobStartedEvent Started(
        string externalJobId,
        WaitId waitId,
        TimerId? timeoutTimerId)
    {
        return new WorkflowExternalJobStartedEvent
        {
            EventId = EventIdValue(1),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(1),
            CausationId = CausationIdValue(1),
            OccurredAt = Timestamp(1),
            ExternalJobId = externalJobId,
            Payload = [],
            WaitId = waitId,
            TimeoutTimerId = timeoutTimerId,
            FiberId = new FiberId("job-fiber"),
            ScopeId = new ScopeId("job-scope")
        };
    }

    private static WorkflowExternalJobCompletedEvent Completed(string externalJobId)
    {
        return new WorkflowExternalJobCompletedEvent
        {
            EventId = EventIdValue(2),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(2),
            CausationId = CausationIdValue(2),
            OccurredAt = Timestamp(2),
            ExternalJobId = externalJobId,
            CompletionEventId = EventIdValue(20)
        };
    }

    private static DurableExternalJobEventContext ExternalJobContext(
        CommandId commandId,
        DateTimeOffset requestedAt)
    {
        return new DurableExternalJobEventContext(
            commandId,
            InstanceIdValue(1),
            requestedAt,
            ParentInstanceId: null,
            InstanceIdValue(1));
    }

    private static DateTimeOffset Timestamp(int seconds)
    {
        return new DateTimeOffset(2026, 7, 3, 14, 0, seconds, TimeSpan.Zero);
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

    private static WaitId WaitIdValue(int value)
    {
        return WaitId.Parse(GuidValue(value).ToString());
    }

    private static TimerId TimerIdValue(int value)
    {
        return new TimerId(GuidValue(value));
    }

    private static Guid GuidValue(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }
}
