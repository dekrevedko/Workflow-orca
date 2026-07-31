using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Management;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Management;

public sealed class DurableQueryTests
{
    [Fact]
    [Trait("AC", "AC-308")]
    public async Task QueryColdInstances_ByMetadata_DoesNotLoadBusinessPayload()
    {
        var definitionId = DefinitionId.New();
        var store = new RecordingProjectionStore([
            Snapshot(InstanceIdValue(1), definitionId, DefinitionVersion.Initial, WorkflowStatus.Waiting)
        ]);
        var management = new DurableManagement(store);

        var snapshots = await management.All()
            .Where(instance => instance.DefinitionId == definitionId)
            .ListAsync(TestContext.Current.CancellationToken);

        snapshots.Should().ContainSingle()
            .Which.DefinitionId.Should().Be(definitionId);
        store.ListCallCount.Should().Be(1);
        store.PayloadLoadCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Where_StatusAndDefinition_UsesProjectionStoreFilter()
    {
        var definitionId = DefinitionId.New();
        var store = new RecordingProjectionStore([
            Snapshot(InstanceIdValue(1), definitionId, DefinitionVersion.Initial, WorkflowStatus.Waiting),
            Snapshot(InstanceIdValue(2), DefinitionId.New(), DefinitionVersion.Initial, WorkflowStatus.Running)
        ]);
        var management = new DurableManagement(store);

        var snapshots = await management.All()
            .Where(instance => instance.Status == WorkflowStatus.Waiting && instance.DefinitionId == definitionId)
            .ListAsync(TestContext.Current.CancellationToken);

        snapshots.Should().ContainSingle()
            .Which.InstanceId.Should().Be(InstanceIdValue(1));
        store.LastQuery.Status.Should().Be(WorkflowStatus.Waiting);
        store.LastQuery.DefinitionId.Should().Be(definitionId);
    }

    [Fact]
    public async Task GetActiveWaits_ReturnsProjectedColdWaits()
    {
        var instanceId = InstanceIdValue(1);
        var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        await processor.ProcessAsync(StartCommand(instanceId), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            new DurableWaitRegisteredCommand(
                CommandIdValue(2),
                instanceId,
                Timestamp(2),
                WaitIdValue(2),
                "Approved",
                CorrelationId.Create("order-1")),
            TestContext.Current.CancellationToken);

        var waits = await new DurableManagement(store).All()
            .GetActiveWaitsAsync(TestContext.Current.CancellationToken);

        waits.Should().ContainSingle().Which.Should().BeEquivalentTo(new ActiveWaitSnapshot
        {
            WaitId = WaitIdValue(2),
            EventName = "Approved",
            CorrelationId = CorrelationId.Create("order-1"),
            RegisteredAt = Timestamp(2),
            Status = "Active",
            Mode = "Resident"
        });
    }

    [Fact]
    public async Task Statistics_GroupsProjectedInstancesByDefinitionVersionAndStatus()
    {
        var definitionId = DefinitionId.New();
        var store = new RecordingProjectionStore([
            Snapshot(InstanceIdValue(1), definitionId, DefinitionVersion.Initial, WorkflowStatus.Waiting),
            Snapshot(InstanceIdValue(2), definitionId, DefinitionVersion.Initial, WorkflowStatus.Waiting),
            Snapshot(InstanceIdValue(3), definitionId, new DefinitionVersion(2), WorkflowStatus.Running)
        ]);
        var management = new DurableManagement(store);

        var statistics = await management.All().StatisticsAsync(TestContext.Current.CancellationToken);

        statistics.Groups.Should().BeEquivalentTo([
            new WorkflowStatisticsGroup
            {
                DefinitionId = definitionId,
                DefinitionVersion = DefinitionVersion.Initial,
                Status = WorkflowStatus.Waiting,
                Count = 2
            },
            new WorkflowStatisticsGroup
            {
                DefinitionId = definitionId,
                DefinitionVersion = new DefinitionVersion(2),
                Status = WorkflowStatus.Running,
                Count = 1
            }
        ]);
    }

    private static WorkflowInstanceSnapshot Snapshot(
        InstanceId instanceId,
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        WorkflowStatus status)
    {
        return new WorkflowInstanceSnapshot
        {
            InstanceId = instanceId,
            DefinitionId = definitionId,
            DefinitionVersion = definitionVersion,
            Status = status,
            CreatedAt = Timestamp(1),
            UpdatedAt = Timestamp(2)
        };
    }

    private static StartWorkflowCommand StartCommand(InstanceId instanceId)
    {
        return new StartWorkflowCommand
        {
            CommandId = CommandIdValue(1),
            InstanceId = instanceId,
            RequestedAt = Timestamp(1),
            DefinitionId = DefinitionId.New(),
            DefinitionVersion = DefinitionVersion.Initial
        };
    }

    private static DateTimeOffset Timestamp(int seconds)
    {
        return new DateTimeOffset(2026, 7, 2, 13, 0, seconds, TimeSpan.Zero);
    }

    private static InstanceId InstanceIdValue(int value)
    {
        return InstanceId.Parse(GuidValue(value).ToString());
    }

    private static CommandId CommandIdValue(int value)
    {
        return new CommandId(GuidValue(value));
    }

    private static WaitId WaitIdValue(int value)
    {
        return WaitId.Parse(GuidValue(value).ToString());
    }

    private static Guid GuidValue(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }

    private sealed class RecordingProjectionStore(IReadOnlyList<WorkflowInstanceSnapshot> snapshots)
        : IWorkflowProjectionStore
    {
        public WorkflowProjectionQuery LastQuery { get; private set; } = WorkflowProjectionQuery.All;

        public int ListCallCount { get; private set; }

        public int PayloadLoadCallCount { get; private set; }

        public Task ApplyAsync(IReadOnlyList<ProjectionWrite> operations, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<WorkflowInstanceSnapshot>> ListAsync(
            WorkflowProjectionQuery query,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastQuery = query;
            ListCallCount++;
            return Task.FromResult<IReadOnlyList<WorkflowInstanceSnapshot>>(Apply(query).ToArray());
        }

        public async Task<int> CountAsync(WorkflowProjectionQuery query, CancellationToken cancellationToken)
        {
            var listed = await ListAsync(query, cancellationToken);
            return listed.Count;
        }

        public Task<IReadOnlyList<ActiveWaitSnapshot>> ListActiveWaitsAsync(
            WorkflowProjectionQuery query,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastQuery = query;
            return Task.FromResult<IReadOnlyList<ActiveWaitSnapshot>>(
                Apply(query).SelectMany(snapshot => snapshot.ActiveWaits).ToArray());
        }

        public Task<WorkflowStatistics> GetStatisticsAsync(
            WorkflowProjectionQuery query,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastQuery = query;
            var groups = Apply(query)
                .GroupBy(snapshot => new
                {
                    snapshot.DefinitionId,
                    snapshot.DefinitionVersion,
                    snapshot.Status
                })
                .Select(group => new WorkflowStatisticsGroup
                {
                    DefinitionId = group.Key.DefinitionId,
                    DefinitionVersion = group.Key.DefinitionVersion,
                    Status = group.Key.Status,
                    Count = group.Count()
                })
                .ToArray();
            return Task.FromResult(new WorkflowStatistics { Groups = groups });
        }

        private IEnumerable<WorkflowInstanceSnapshot> Apply(WorkflowProjectionQuery query)
        {
            return snapshots.Where(snapshot =>
                (query.InstanceId is null || snapshot.InstanceId == query.InstanceId) &&
                (query.DefinitionId is null || snapshot.DefinitionId == query.DefinitionId) &&
                (query.DefinitionVersion is null || snapshot.DefinitionVersion == query.DefinitionVersion) &&
                (query.Status is null || snapshot.Status == query.Status));
        }
    }
}
