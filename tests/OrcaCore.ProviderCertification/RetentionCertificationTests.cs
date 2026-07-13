using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.ProviderCertification;

public abstract class RetentionCertificationTests
{
    protected abstract IRetentionCertificationFixture CreateFixture();

    [Fact]
    [Trait("AC", "AC-314")]
    public async Task ArchivePolicy_InactiveInstance_MarksProjectionArchived()
    {
        var fixture = CreateFixture();
        var instanceId = InstanceIdValue(4);
        await SeedInstanceAsync(fixture, instanceId, WorkflowStatus.Completed);

        var result = await fixture.RetentionStore.ArchiveAsync(
            new RetentionPolicy
            {
                InstanceId = instanceId,
                RequestedAt = Timestamp(7),
                Reason = "retention elapsed"
            },
            TestContext.Current.CancellationToken);
        var snapshots = await fixture.ProjectionStore.ListAsync(
            new WorkflowProjectionQuery { InstanceId = instanceId },
            TestContext.Current.CancellationToken);

        result.Archived.Should().BeTrue();
        snapshots.Should().ContainSingle()
            .Which.ArchivedAt.Should().Be(Timestamp(7));
    }

    [Fact]
    [Trait("AC", "AC-314")]
    public async Task ArchivePolicy_ActiveInstance_IsRejectedWithoutDeletingState()
    {
        var fixture = CreateFixture();
        var instanceId = InstanceIdValue(1);
        await SeedInstanceAsync(fixture, instanceId, WorkflowStatus.Running);

        var result = await fixture.RetentionStore.ArchiveAsync(
            new RetentionPolicy
            {
                InstanceId = instanceId,
                RequestedAt = Timestamp(2),
                Reason = "active safety check"
            },
            TestContext.Current.CancellationToken);
        var snapshots = await fixture.ProjectionStore.ListAsync(
            new WorkflowProjectionQuery { InstanceId = instanceId },
            TestContext.Current.CancellationToken);
        var tail = await fixture.EventStore.LoadTailAsync(
            new WorkflowStreamId(instanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        result.Archived.Should().BeFalse();
        result.Reason.Should().Be("Instance is active.");
        snapshots.Should().ContainSingle()
            .Which.InstanceId.Should().Be(instanceId);
        tail.Should().NotBeEmpty();
    }

    [Fact]
    [Trait("AC", "AC-314")]
    public async Task PurgePolicy_InFlightOutbox_IsRejectedWithoutBreakingDispatch()
    {
        var fixture = CreateFixture();
        var instanceId = InstanceIdValue(2);
        var outboxRecordId = OutboxRecordIdValue(1);
        await SeedInstanceAsync(fixture, instanceId, WorkflowStatus.Completed, outboxRecordId);
        var claimed = await fixture.OutboxStore.ClaimAsync(1, TestContext.Current.CancellationToken);

        var result = await fixture.RetentionStore.PurgeAsync(
            new RetentionPolicy
            {
                InstanceId = instanceId,
                RequestedAt = Timestamp(3),
                Reason = "claimed outbox safety check"
            },
            TestContext.Current.CancellationToken);
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
        await SeedInstanceAsync(fixture, instanceId, WorkflowStatus.Completed);
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

        var result = await fixture.RetentionStore.PurgeAsync(
            new RetentionPolicy
            {
                InstanceId = instanceId,
                RequestedAt = Timestamp(6),
                Reason = "retention elapsed"
            },
            TestContext.Current.CancellationToken);
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
        WorkflowStatus status,
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
                        InstanceSnapshot = new WorkflowInstanceSnapshot
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
        return new EventId(GuidValue(value));
    }

    private static InstanceId InstanceIdValue(int value)
    {
        return new InstanceId(GuidValue(value));
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
        return new DefinitionId(GuidValue(value));
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

    IWorkflowRetentionStore RetentionStore { get; }

    ITimerScheduler TimerScheduler { get; }
}

public sealed class InMemoryRetentionCertificationTests : RetentionCertificationTests
{
    protected override IRetentionCertificationFixture CreateFixture()
    {
        return new InMemoryRetentionCertificationFixture(new InMemoryWorkflowProvider());
    }

    private sealed class InMemoryRetentionCertificationFixture(InMemoryWorkflowProvider provider)
        : IRetentionCertificationFixture
    {
        public IWorkflowEventStore EventStore => provider;

        public IWorkflowOutboxStore OutboxStore => provider;

        public IWorkflowProjectionStore ProjectionStore => provider;

        public IWorkflowRetentionStore RetentionStore => provider;

        public ITimerScheduler TimerScheduler => provider;
    }
}
