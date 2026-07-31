using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Management;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Management;

public sealed class DurableStatisticsTests
{
    [Fact]
    public async Task Statistics_GroupsCountsByDefinitionVersionAndStatus()
    {
        var definitionId = DefinitionIdValue(1);
        var store = new InMemoryWorkflowProvider();
        await store.ApplyAsync(
            [
                SummaryProjection(InstanceIdValue(1), definitionId, DefinitionVersion.Initial, WorkflowStatus.Running),
                SummaryProjection(InstanceIdValue(2), definitionId, DefinitionVersion.Initial, WorkflowStatus.Running),
                SummaryProjection(InstanceIdValue(3), definitionId, new DefinitionVersion(2), WorkflowStatus.Waiting)
            ],
            TestContext.Current.CancellationToken);

        var statistics = await new DurableManagement(store).All()
            .StatisticsAsync(TestContext.Current.CancellationToken);

        statistics.Groups.Should().BeEquivalentTo([
            new WorkflowStatisticsGroup
            {
                DefinitionId = definitionId,
                DefinitionVersion = DefinitionVersion.Initial,
                Status = WorkflowStatus.Running,
                Count = 2
            },
            new WorkflowStatisticsGroup
            {
                DefinitionId = definitionId,
                DefinitionVersion = new DefinitionVersion(2),
                Status = WorkflowStatus.Waiting,
                Count = 1
            }
        ]);
    }

    [Fact]
    public async Task Statistics_IncludesHistoryCheckpointAndOutboxPressure()
    {
        var instanceId = InstanceIdValue(1);
        var store = new InMemoryWorkflowProvider();
        await store.AppendAsync(
            new ProviderCommitBatch
            {
                StreamId = new WorkflowStreamId(instanceId),
                ExpectedVersion = StreamVersion.Empty,
                Events = [Started(instanceId, DefinitionIdValue(1))],
                Checkpoint = new CheckpointWrite(instanceId, new StreamVersion(1), "application/json", [1, 2, 3]),
                OutboxRecords = [new OutboxWrite(OutboxRecordIdValue(1), "lifecycle-event", [4, 5])],
                ProjectionOperations =
                [
                    SummaryProjection(
                        instanceId,
                        DefinitionIdValue(1),
                        DefinitionVersion.Initial,
                        WorkflowStatus.Running)
                ]
            },
            TestContext.Current.CancellationToken);

        var statistics = await new DurableManagement(store).All()
            .StatisticsAsync(TestContext.Current.CancellationToken);

        statistics.Pressure.Should().BeEquivalentTo(new
        {
            TotalStreamEvents = 1L,
            CheckpointCount = 1,
            PendingOutboxCount = 1,
            ActiveInstanceCount = 1
        });
    }

    private static ProjectionWrite SummaryProjection(
        InstanceId instanceId,
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        WorkflowStatus status)
    {
        return new ProjectionWrite(instanceId, ProjectionOperationKind.UpsertSummary)
        {
            InstanceSnapshot = new WorkflowInstanceSnapshot
            {
                InstanceId = instanceId,
                DefinitionId = definitionId,
                DefinitionVersion = definitionVersion,
                Status = status,
                CreatedAt = Timestamp(1),
                UpdatedAt = Timestamp(2)
            }
        };
    }

    private static WorkflowStartedEvent Started(InstanceId instanceId, DefinitionId definitionId)
    {
        return new WorkflowStartedEvent
        {
            EventId = EventId.Create(Guid.CreateVersion7().ToString()),
            InstanceId = instanceId,
            CommandId = CommandIdValue(1),
            CausationId = CausationId.New(),
            OccurredAt = Timestamp(1),
            DefinitionId = definitionId,
            DefinitionVersion = DefinitionVersion.Initial
        };
    }

    private static DateTimeOffset Timestamp(int seconds)
    {
        return new DateTimeOffset(2026, 7, 2, 14, 0, seconds, TimeSpan.Zero);
    }

    private static InstanceId InstanceIdValue(int value)
    {
        return InstanceId.Parse(GuidValue(value).ToString());
    }

    private static CommandId CommandIdValue(int value)
    {
        return new CommandId(GuidValue(value));
    }

    private static DefinitionId DefinitionIdValue(int value)
    {
        return DefinitionId.Parse(GuidValue(value).ToString());
    }

    private static OutboxRecordId OutboxRecordIdValue(int value)
    {
        return new OutboxRecordId(GuidValue(value));
    }

    private static Guid GuidValue(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }
}
