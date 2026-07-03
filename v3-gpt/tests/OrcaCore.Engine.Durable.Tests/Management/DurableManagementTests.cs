using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Management;
using OrcaCore.Engine.Ephemeral;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Management;

public sealed class DurableManagementTests
{
    [Fact]
    [Trait("AC", "AC-512")]
    public async Task Pause_RunningInstance_StopsAfterSafeBoundary()
    {
        var instanceId = InstanceIdValue(1);
        var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        await processor.ProcessAsync(StartCommand(instanceId), TestContext.Current.CancellationToken);

        var paused = await processor.ProcessAsync(
            new DurablePauseCommand(CommandIdValue(2), instanceId, Timestamp(2)),
            TestContext.Current.CancellationToken);
        var aggregate = await RehydrateAsync(store, instanceId);

        paused.Outcome.Should().Be(DurableCommandOutcome.Committed);
        aggregate.Snapshot.Status.Should().Be(WorkflowStatus.Paused);
    }

    [Fact]
    [Trait("AC", "AC-513")]
    public async Task EventsDuringPause_AreBufferedAndDoNotResume()
    {
        var instanceId = InstanceIdValue(1);
        var eventId = EventIdValue(50);
        var store = new InMemoryWorkflowProvider();
        await SeedPausedWaitAsync(store, instanceId, WaitIdValue(1));

        await new DurableCommandProcessor(store).ProcessAsync(
            DeliverCommand(instanceId, eventId, 4),
            TestContext.Current.CancellationToken);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(instanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        events.OfType<WorkflowDeliveryBufferedEvent>().Should().ContainSingle();
        events.OfType<WorkflowWaitMatchedEvent>().Should().BeEmpty();
    }

    [Fact]
    [Trait("AC", "AC-514")]
    public async Task ResumeReplay_ProcessesBufferedDeliveriesInOrder()
    {
        var instanceId = InstanceIdValue(1);
        var eventId = EventIdValue(50);
        var store = new InMemoryWorkflowProvider();
        await SeedPausedWaitAsync(store, instanceId, WaitIdValue(1));
        await new DurableCommandProcessor(store).ProcessAsync(
            DeliverCommand(instanceId, eventId, 4),
            TestContext.Current.CancellationToken);

        var replayed = await new DurableCommandProcessor(store).ProcessAsync(
            new DurableResumeCommand(CommandIdValue(5), instanceId, Timestamp(5), ResumeBufferedDeliveries.Replay),
            TestContext.Current.CancellationToken);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(instanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        replayed.Outcome.Should().Be(DurableCommandOutcome.Committed);
        events.OfType<WorkflowWaitMatchedEvent>().Single().MatchedEventId.Should().Be(eventId);
    }

    [Fact]
    [Trait("AC", "AC-515")]
    public async Task PausedInstance_RehydratesAsPausedAfterRestart()
    {
        var instanceId = InstanceIdValue(1);
        var store = new InMemoryWorkflowProvider();
        await SeedPausedWaitAsync(store, instanceId, WaitIdValue(1));

        var restarted = await RehydrateAsync(store, instanceId);

        restarted.Snapshot.Status.Should().Be(WorkflowStatus.Paused);
    }

    [Fact]
    [Trait("AC", "AC-517")]
    public async Task ResumeDiscard_DropsBufferAuditablyAndDedupsDiscardedEvents()
    {
        var instanceId = InstanceIdValue(1);
        var eventId = EventIdValue(50);
        var store = new InMemoryWorkflowProvider();
        await SeedPausedWaitAsync(store, instanceId, WaitIdValue(1));
        await new DurableCommandProcessor(store).ProcessAsync(
            DeliverCommand(instanceId, eventId, 4),
            TestContext.Current.CancellationToken);

        await new DurableCommandProcessor(store).ProcessAsync(
            new DurableResumeCommand(CommandIdValue(5), instanceId, Timestamp(5), ResumeBufferedDeliveries.Discard),
            TestContext.Current.CancellationToken);
        var redelivery = await new DurableCommandProcessor(store).ProcessAsync(
            DeliverCommand(instanceId, eventId, 6),
            TestContext.Current.CancellationToken);
        var inbox = await store.GetAsync(eventId, TestContext.Current.CancellationToken);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(instanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        inbox.Value.Should().Be(InboxRecordState.DiscardedOnResume);
        redelivery.Outcome.Should().Be(DurableCommandOutcome.NoOp);
        events.OfType<WorkflowDeliveryDiscardedEvent>().Should().ContainSingle();
    }

    [Fact]
    [Trait("AC", "AC-516")]
    public async Task DurableTerminate_RequiresExplicitDestructiveSafety()
    {
        var instanceId = InstanceIdValue(1);
        var store = new InMemoryWorkflowProvider();
        var management = new DurableManagement(store);
        await new DurableCommandProcessor(store).ProcessAsync(
            StartCommand(instanceId),
            TestContext.Current.CancellationToken);

        var unsafeTerminate = async () => await management.TerminateAsync(
            instanceId,
            Timestamp(2),
            TestContext.Current.CancellationToken);
        var terminated = await management.TerminateAsync(
            instanceId,
            Timestamp(2),
            OrcaCore.Engine.Durable.Management.DestructiveCommandSafety.Confirmed,
            TestContext.Current.CancellationToken);

        await unsafeTerminate.Should().ThrowAsync<WorkflowLifecycleException>()
            .WithMessage("*explicit destructive safety confirmation*");
        terminated.Outcome.Should().Be(DurableCommandOutcome.Committed);
    }

    [Fact]
    [Trait("AC", "AC-516")]
    public async Task DurablePurge_RequiresExplicitDestructiveSafety()
    {
        var instanceId = InstanceIdValue(1);
        var store = new InMemoryWorkflowProvider();
        var management = new DurableManagement(store);
        await new DurableCommandProcessor(store).ProcessAsync(
            StartCommand(instanceId),
            TestContext.Current.CancellationToken);
        await management.TerminateAsync(
            instanceId,
            Timestamp(2),
            OrcaCore.Engine.Durable.Management.DestructiveCommandSafety.Confirmed,
            TestContext.Current.CancellationToken);
        var policy = new RetentionPolicy
        {
            InstanceId = instanceId,
            RequestedAt = Timestamp(3),
            Reason = "retention window elapsed"
        };

        var unsafePurge = async () => await management.PurgeAsync(policy, TestContext.Current.CancellationToken);
        var purged = await management.PurgeAsync(
            policy,
            OrcaCore.Engine.Durable.Management.DestructiveCommandSafety.Confirmed,
            TestContext.Current.CancellationToken);

        await unsafePurge.Should().ThrowAsync<WorkflowLifecycleException>()
            .WithMessage("*explicit destructive safety confirmation*");
        purged.Purged.Should().BeTrue();
    }

    [Fact]
    [Trait("AC", "DU-071")]
    public async Task ReconstructDagRunAsync_LoadsTailOnlyAfterLatestCheckpoint()
    {
        var store = new CheckpointedDagStore();
        var management = new DurableManagement(store, eventStore: store);

        var snapshot = await management.ReconstructDagRunAsync(
            CheckpointedDagStore.RootInstanceId,
            TestContext.Current.CancellationToken);

        store.LoadedAfterVersion.Should().Be(new StreamVersion(12));
        snapshot.Nodes.Should().ContainSingle()
            .Which.Should().Match<DagNodeRunSnapshot>(node =>
                node.NodeId == "node-a" &&
                node.InstanceId == CheckpointedDagStore.ChildInstanceId &&
                node.Status == WorkflowStatus.Completed);
    }

    [Fact]
    public void EphemeralManagement_DoesNotExposePauseResumeRetryHistoryArchivePurge()
    {
        typeof(EphemeralManagement).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(EphemeralManagement).Namespace)
            .SelectMany(type => type.GetMethods())
            .Select(method => method.Name)
            .Should().NotContain(["Pause", "PauseAsync", "Resume", "ResumeAsync", "Retry", "RetryAsync",
                "GetHistory", "Archive", "ArchiveAsync", "Purge", "PurgeAsync"]);
    }

    private static async Task SeedPausedWaitAsync(
        InMemoryWorkflowProvider store,
        InstanceId instanceId,
        WaitId waitId)
    {
        var processor = new DurableCommandProcessor(store);
        await processor.ProcessAsync(StartCommand(instanceId), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            new DurableWaitRegisteredCommand(
                CommandIdValue(2),
                instanceId,
                Timestamp(2),
                waitId,
                "Approved",
                new CorrelationId("order-1")),
            TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            new DurablePauseCommand(CommandIdValue(3), instanceId, Timestamp(3)),
            TestContext.Current.CancellationToken);
    }

    private static async Task<DurableWorkflowAggregate> RehydrateAsync(
        InMemoryWorkflowProvider store,
        InstanceId instanceId)
    {
        var tail = await store.LoadTailAsync(
            new WorkflowStreamId(instanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        return DurableWorkflowAggregate.Rehydrate(null, tail);
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

    private static DeliverEventCommand DeliverCommand(
        InstanceId instanceId,
        EventId eventId,
        int commandValue)
    {
        return new DeliverEventCommand
        {
            CommandId = CommandIdValue(commandValue),
            InstanceId = instanceId,
            RequestedAt = Timestamp(commandValue),
            Envelope = new EventEnvelope
            {
                EventId = eventId,
                EventName = "Approved",
                CorrelationId = new CorrelationId("order-1"),
                OccurredAt = Timestamp(commandValue)
            }
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

    private static DefinitionId DefinitionIdValue(int value)
    {
        return new DefinitionId(GuidValue(value));
    }

    private static WaitId WaitIdValue(int value)
    {
        return new WaitId(GuidValue(value));
    }

    private static Guid GuidValue(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }

    private sealed class CheckpointedDagStore : IWorkflowEventStore, IWorkflowProjectionStore
    {
        internal static readonly InstanceId RootInstanceId = InstanceIdValue(10);
        internal static readonly InstanceId ChildInstanceId = InstanceIdValue(11);

        internal StreamVersion? LoadedAfterVersion { get; private set; }

        public Task<Option<CheckpointWrite>> LoadCheckpointAsync(
            InstanceId instanceId,
            CancellationToken cancellationToken)
        {
            var checkpoint = new CheckpointWrite(instanceId, new StreamVersion(12), "application/json", [])
            {
                RuntimeState = new WorkflowRuntimeCheckpointState
                {
                    ActiveChildGroups =
                    [
                        new CheckpointActiveChildGroup(
                            "group-a",
                            RunChildFailurePolicy.PropagateFailure,
                            RunChildrenJoinPolicy.WhenAll,
                            RunChildrenResidualPolicy.LetRemainingComplete,
                            1,
                            1,
                            [
                                new WorkflowChildMaterialization
                                {
                                    Index = 0,
                                    ChildInstanceId = ChildInstanceId,
                                    ChildDefinitionId = DefinitionIdValue(2),
                                    ChildDefinitionVersion = DefinitionVersion.Initial,
                                    ItemSnapshot = "node-a"
                                }
                            ])
                    ]
                }
            };

            return Task.FromResult(Option<CheckpointWrite>.Some(checkpoint));
        }

        public Task<Result<AppendEventsResult>> AppendAsync(
            ProviderCommitBatch batch,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<WorkflowEvent>> LoadTailAsync(
            WorkflowStreamId streamId,
            StreamVersion afterVersion,
            CancellationToken cancellationToken)
        {
            LoadedAfterVersion = afterVersion;
            return Task.FromResult<IReadOnlyList<WorkflowEvent>>([]);
        }

        public Task ApplyAsync(IReadOnlyList<ProjectionWrite> operations, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<WorkflowInstanceSnapshot>> ListAsync(
            WorkflowProjectionQuery query,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<WorkflowInstanceSnapshot>>(
            [
                new WorkflowInstanceSnapshot
                {
                    InstanceId = ChildInstanceId,
                    ParentInstanceId = RootInstanceId,
                    RootInstanceId = RootInstanceId,
                    DefinitionId = DefinitionIdValue(2),
                    DefinitionVersion = DefinitionVersion.Initial,
                    Status = WorkflowStatus.Completed,
                    CreatedAt = Timestamp(1),
                    UpdatedAt = Timestamp(2)
                }
            ]);
        }

        public Task<int> CountAsync(WorkflowProjectionQuery query, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<ActiveWaitSnapshot>> ListActiveWaitsAsync(
            WorkflowProjectionQuery query,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<OrcaCore.Abstractions.Instances.WorkflowStatistics> GetStatisticsAsync(
            WorkflowProjectionQuery query,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
