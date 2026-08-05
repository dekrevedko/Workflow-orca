using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using Xunit;

namespace OrcaCore.ProviderCertification;

public abstract class RetentionCertificationTests
{
    protected abstract IRetentionCertificationFixture CreateFixture();

    [Fact]
    [Trait("AC", "AC-314")]
    public async Task PurgePolicy_InactiveTerminalInstance_RemovesProjectionAndStream()
    {
        var fixture = CreateFixture();
        var instanceId = InstanceIdValue(4);
        await SeedInstanceAsync(fixture, instanceId, WorkflowInstanceStatus.Completed);

        var result = await fixture.PurgeForRetentionAsync(instanceId, TestContext.Current.CancellationToken);
        var snapshot = await fixture.ProjectionStore.GetAsync(
            instanceId,
            TestContext.Current.CancellationToken);
        var tail = await fixture.EventStore.LoadTailAsync(
            new WorkflowStreamId(instanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        result.Purged.Should().BeTrue();
        result.Reason.Should().BeNull();
        snapshot.HasValue.Should().BeFalse();
        tail.Should().BeEmpty();
    }

    [Fact]
    [Trait("AC", "AC-314")]
    public async Task ArchivePolicy_ActiveInstance_IsRejectedWithoutDeletingState()
    {
        var fixture = CreateFixture();
        var instanceId = InstanceIdValue(1);
        await SeedInstanceAsync(fixture, instanceId, WorkflowInstanceStatus.Running);

        var result = await fixture.PurgeForRetentionAsync(instanceId, TestContext.Current.CancellationToken);
        var snapshot = await fixture.ProjectionStore.GetAsync(
            instanceId,
            TestContext.Current.CancellationToken);
        var tail = await fixture.EventStore.LoadTailAsync(
            new WorkflowStreamId(instanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        result.Purged.Should().BeFalse();
        result.Reason.Should().Be("Instance is active.");
        snapshot.Value.InstanceId.Should().Be(instanceId);
        tail.Should().NotBeEmpty();
    }

    [Fact]
    [Trait("AC", "AC-314")]
    public async Task PurgePolicy_InFlightOutbox_IsRejectedWithoutBreakingDispatch()
    {
        var fixture = CreateFixture();
        var instanceId = InstanceIdValue(2);
        var outboxRecordId = OutboxRecordIdValue(1);
        await SeedInstanceAsync(fixture, instanceId, WorkflowInstanceStatus.Completed, outboxRecordId);
        var claimed = await fixture.OutboxStore.ClaimAsync(1, TestContext.Current.CancellationToken);

        var result = await fixture.PurgeForRetentionAsync(instanceId, TestContext.Current.CancellationToken);
        var outboxState = await fixture.OutboxStore.GetStateAsync(outboxRecordId, TestContext.Current.CancellationToken);
        var tail = await fixture.EventStore.LoadTailAsync(
            new WorkflowStreamId(instanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        claimed.Should().ContainSingle()
            .Which.OutboxRecordId.Should().Be(outboxRecordId);
        result.Purged.Should().BeFalse();
        result.Reason.Should().Be("Instance has claimed outbox records.");
        outboxState.Value.Should().Be(OutboxRecordState.Claimed);
        tail.Should().NotBeEmpty();
    }

    [Fact]
    [Trait("AC", "AC-314")]
    public async Task PurgePolicy_RemovesDurableTimersForPurgedInstance()
    {
        var fixture = CreateFixture();
        var instanceId = InstanceIdValue(3);
        await SeedInstanceAsync(fixture, instanceId, WorkflowInstanceStatus.Completed);
        await fixture.TimerScheduler.ScheduleAsync(
            new TimerScheduleRequest
            {
                TimerId = TimerIdValue(1),
                InstanceId = instanceId,
                CommandId = CommandIdValue(3),
                FireAt = Timestamp(5),
                WakeupName = "retention-timeout"
            },
            TestContext.Current.CancellationToken);

        var result = await fixture.PurgeForRetentionAsync(instanceId, TestContext.Current.CancellationToken);
        var claimed = await fixture.TimerScheduler.ClaimDueAsync(
            Timestamp(6),
            maxCount: 10,
            TestContext.Current.CancellationToken);

        result.Purged.Should().BeTrue();
        claimed.Should().BeEmpty();
    }

    private static async Task SeedInstanceAsync(
        IRetentionCertificationFixture fixture,
        InstanceId instanceId,
        WorkflowInstanceStatus status,
        OutboxRecordId? outboxRecordId = null)
    {
        var definitionId = DefinitionIdValue(1);
        await fixture.EventStore.AppendAsync(
            new ProviderCommitBatch
            {
                StreamId = new WorkflowStreamId(instanceId),
                ExpectedVersion = StreamVersion.Empty,
                Events = [Started(instanceId, definitionId)],
                ProjectionOperations =
                [
                    new ProjectionWrite(instanceId, ProjectionOperationKind.UpsertSummary)
                    {
                        InstanceSnapshot = new WorkflowProjectionSnapshot
                        {
                            InstanceId = instanceId,
                            RootInstanceId = instanceId,
                            DefinitionId = definitionId,
                            DefinitionVersion = DefinitionVersion.Initial,
                            Status = status,
                            CreatedAt = Timestamp(1),
                            UpdatedAt = Timestamp(1)
                        }
                    }
                ],
                OutboxRecords = outboxRecordId is { } recordId
                    ? [new OutboxWrite(recordId, "external-message", [1])]
                    : []
            },
            TestContext.Current.CancellationToken);
    }

    private static WorkflowStartedEvent Started(InstanceId instanceId, DefinitionId definitionId)
    {
        return new WorkflowStartedEvent
        {
            EventId = EventIdValue(1),
            InstanceId = instanceId,
            CommandId = CommandIdValue(1),
            CausationId = CausationIdValue(1),
            OccurredAt = Timestamp(1),
            RootInstanceId = instanceId,
            DefinitionId = definitionId,
            DefinitionVersion = DefinitionVersion.Initial
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

    private static OutboxRecordId OutboxRecordIdValue(int value)
    {
        return new OutboxRecordId(GuidValue(value));
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

public interface IRetentionCertificationFixture
{
    IWorkflowEventStore EventStore { get; }

    IWorkflowOutboxStore OutboxStore { get; }

    IWorkflowProjectionStore ProjectionStore { get; }

    Task<(bool Purged, string? Reason)> PurgeForRetentionAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken);

    ITimerScheduler TimerScheduler { get; }
}
