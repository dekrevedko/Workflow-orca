using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Management;
using OrcaCore.Engine.Durable.Outbox;
using OrcaCore.Providers.InMemory;
using OrcaCore.TestSupport.Providers;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests;

public sealed class R4DurableEngineFindingsTests
{
    [Fact]
    public async Task R4_StartOrGet_RestartUsesDurableIdempotencyKey()
    {
        var store = new InMemoryWorkflowProvider();
        var firstStarter = new DurableStartService(new DurableCommandProcessor(store));
        var first = await firstStarter.StartOrGetAsync(
            StartRequest("order-123"),
            TestContext.Current.CancellationToken);

        var restartedStarter = new DurableStartService(new DurableCommandProcessor(store));
        var second = await restartedStarter.StartOrGetAsync(
            StartRequest("order-123"),
            TestContext.Current.CancellationToken);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(first.InstanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        first.Created.Should().BeTrue();
        second.Created.Should().BeFalse();
        second.InstanceId.Should().Be(first.InstanceId);
        events.OfType<WorkflowStartedEvent>().Should().ContainSingle();
    }

    [Fact]
    public async Task R4_EarlyInboundEvent_IsBufferedAndMatchedWhenWaitRegisters()
    {
        var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        var instanceId = InstanceIdValue(1);
        var eventId = EventIdValue(50);
        await processor.ProcessAsync(Start(1), TestContext.Current.CancellationToken);

        var delivered = await processor.ProcessAsync(
            Deliver(instanceId, eventId, 2),
            TestContext.Current.CancellationToken);
        var registered = await processor.ProcessAsync(
            WaitRegistered(instanceId, WaitIdValue(10), 3),
            TestContext.Current.CancellationToken);
        var inbox = await store.GetAsync(eventId, TestContext.Current.CancellationToken);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(instanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        delivered.Outcome.Should().Be(DurableCommandOutcome.Committed);
        registered.Outcome.Should().Be(DurableCommandOutcome.Committed);
        inbox.Value.Should().Be(InboxRecordState.Applied);
        events.OfType<WorkflowDeliveryBufferedEvent>().Should().ContainSingle();
        events.OfType<WorkflowWaitMatchedEvent>().Should().ContainSingle()
            .Which.MatchedEventId.Should().Be(eventId);
    }

    [Fact]
    public async Task R4_ResourcePoolAcquire_AppendFailureRollsBackGrantedTicket()
    {
        var store = new FakeWorkflowEventStore();
        var pools = new InMemoryResourcePoolStore();
        await pools.UpsertPoolAsync(Pool("db", 1), TestContext.Current.CancellationToken);
        var processor = new DurableCommandProcessor(store, pools);
        await processor.ProcessAsync(Start(1), TestContext.Current.CancellationToken);
        store.FailNextCommitBeforeApply();

        var result = await processor.ProcessAsync(
            Acquire(1, "node-1", Requirement("db")),
            TestContext.Current.CancellationToken);
        var pool = await pools.GetPoolAsync("db", TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(DurableCommandOutcome.Conflict);
        pool.Value.HeldTickets.Should().BeEmpty();
    }

    [Fact]
    public async Task R4_ResourcePoolRelease_RetriesTransientReleaseFailureAfterCommit()
    {
        var store = new InMemoryWorkflowProvider();
        var pools = new FlakyReleasePoolStore();
        await pools.UpsertPoolAsync(Pool("db", 1), TestContext.Current.CancellationToken);
        var processor = new DurableCommandProcessor(store, pools);
        await processor.ProcessAsync(Start(1), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(Acquire(1, "node-1", Requirement("db")), TestContext.Current.CancellationToken);

        var result = await processor.ProcessAsync(
            new DurableCompleteCommand(CommandIdValue(3), InstanceIdValue(1), Timestamp(3), null),
            TestContext.Current.CancellationToken);
        var pool = await pools.GetPoolAsync("db", TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(DurableCommandOutcome.Committed);
        pools.ReleaseAttempts.Should().Be(2);
        pool.Value.HeldTickets.Should().BeEmpty();
    }

    [Fact]
    public void R4_PausedTimerFiring_IsReplayedOnResume()
    {
        var timerId = TimerIdValue(10);
        var aggregate = DurableWorkflowAggregate.Rehydrate(
            null,
            [Started(), TimerScheduled(timerId), Paused(), TimerBuffered(timerId)]);

        var decision = aggregate.DecideResume(
            new DurableResumeCommand(CommandIdValue(5), InstanceIdValue(1), Timestamp(5)));

        decision.Events.OfType<WorkflowTimerFiredEvent>().Should().ContainSingle()
            .Which.TimerId.Should().Be(timerId);
    }

    [Fact]
    public void R4_SagaCompensation_TerminalOnlyAfterAllActionsComplete()
    {
        var aggregate = DurableWorkflowAggregate.Rehydrate(
            null,
            [Started(), CompensationStarted("release-b", 0), CompensationStarted("release-a", 1)]);

        var first = aggregate.DecideCompleteSagaCompensation(
            CompleteCompensation("release-b", 2));
        var afterFirst = DurableWorkflowAggregate.Rehydrate(
            null,
            [Started(), CompensationStarted("release-b", 0), CompensationStarted("release-a", 1), .. first.Events]);
        var second = afterFirst.DecideCompleteSagaCompensation(
            CompleteCompensation("release-a", 3));

        first.Events.OfType<WorkflowTerminalEvent>().Should().BeEmpty();
        second.Events.OfType<WorkflowTerminalEvent>().Should().ContainSingle()
            .Which.Status.Should().Be(WorkflowStatus.Compensated);
    }

    [Fact]
    public async Task R4_WaitMatching_UsesBranchIdentityWhenPresent()
    {
        var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        var instanceId = InstanceIdValue(1);
        var branchAWait = WaitIdValue(10);
        var branchBWait = WaitIdValue(11);
        await processor.ProcessAsync(Start(1), TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            WaitRegistered(instanceId, branchAWait, 2, branchId: "branch-a"),
            TestContext.Current.CancellationToken);
        await processor.ProcessAsync(
            WaitRegistered(instanceId, branchBWait, 3, branchId: "branch-b"),
            TestContext.Current.CancellationToken);

        await processor.ProcessAsync(
            Deliver(instanceId, EventIdValue(50), 4, branchId: "branch-b"),
            TestContext.Current.CancellationToken);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(instanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        events.OfType<WorkflowWaitMatchedEvent>().Should().ContainSingle()
            .Which.WaitId.Should().Be(branchBWait);
    }

    [Fact]
    public void R4_CheckpointRehydration_RestoresRuntimeCollectionsWithoutTail()
    {
        var aggregate = DurableWorkflowAggregate.Rehydrate(
            null,
            [Started(), WaitRegisteredEvent(WaitIdValue(10))]);
        var checkpoint = aggregate.CreateCheckpoint("application/json", [1]);

        var rehydrated = DurableWorkflowAggregate.Rehydrate(checkpoint, []);

        rehydrated.Snapshot.ActiveWaits.Should().ContainSingle()
            .Which.WaitId.Should().Be(WaitIdValue(10));
    }

    [Fact]
    public async Task R4_ManagementSurface_ExposesDurableOperatorCommands()
    {
        var store = new InMemoryWorkflowProvider();
        await new DurableCommandProcessor(store).ProcessAsync(Start(1), TestContext.Current.CancellationToken);
        var management = new DurableManagement(store);

        var result = await management.PauseAsync(
            InstanceIdValue(1),
            Timestamp(2),
            TestContext.Current.CancellationToken);
        var snapshot = await management.Instance(InstanceIdValue(1))
            .GetAsync(TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(DurableCommandOutcome.Committed);
        snapshot.Status.Should().Be(WorkflowStatus.Paused);
    }

    [Fact]
    public async Task R4_ManagementSurface_ExposesHistoryInspection()
    {
        var store = new InMemoryWorkflowProvider();
        await new DurableCommandProcessor(store).ProcessAsync(Start(1), TestContext.Current.CancellationToken);

        var history = await new DurableManagement(store)
            .GetHistoryAsync(InstanceIdValue(1), TestContext.Current.CancellationToken);

        history.Should().ContainSingle()
            .Which.Should().BeOfType<WorkflowStartedEvent>();
    }

    [Fact]
    public async Task R4_ProviderOptimisticConcurrency_RejectsStaleCrossHostAppend()
    {
        var store = new InMemoryWorkflowProvider();
        var streamId = new WorkflowStreamId(InstanceIdValue(1));
        var first = await store.AppendAsync(
            new ProviderCommitBatch
            {
                StreamId = streamId,
                ExpectedVersion = StreamVersion.Empty,
                Events = [Started()]
            },
            TestContext.Current.CancellationToken);
        var stale = await store.AppendAsync(
            new ProviderCommitBatch
            {
                StreamId = streamId,
                ExpectedVersion = StreamVersion.Empty,
                Events = [TimerScheduled(TimerIdValue(10))]
            },
            TestContext.Current.CancellationToken);

        first.IsSuccess.Should().BeTrue();
        stale.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void R4_OutboxPump_DoesNotUseUnboundedChannel()
    {
        var source = File.ReadAllText(FindRepoFile("src/OrcaCore.Engine.Durable/Outbox/DurableOutboxPump.cs"));

        source.Should().NotContain("Channel.CreateUnbounded");
    }

    private static StartOrGetRequest StartRequest(string key)
    {
        return new StartOrGetRequest(
            key,
            DefinitionIdValue(1),
            DefinitionVersion.Initial,
            null,
            Timestamp(1));
    }

    private static StartWorkflowCommand Start(int instance)
    {
        return new StartWorkflowCommand
        {
            CommandId = CommandIdValue(1),
            InstanceId = InstanceIdValue(instance),
            RequestedAt = Timestamp(1),
            DefinitionId = DefinitionIdValue(1),
            DefinitionVersion = DefinitionVersion.Initial
        };
    }

    private static DurableWaitRegisteredCommand WaitRegistered(
        InstanceId instanceId,
        WaitId waitId,
        int commandValue,
        string? branchId = null)
    {
        return new DurableWaitRegisteredCommand(
            CommandIdValue(commandValue),
            instanceId,
            Timestamp(commandValue),
            waitId,
            "Approved",
            new CorrelationId("order-1"),
            WaitMode.Resident,
            branchId);
    }

    private static DeliverEventCommand Deliver(
        InstanceId instanceId,
        EventId eventId,
        int commandValue,
        string? branchId = null)
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
                OccurredAt = Timestamp(commandValue),
                BranchId = branchId
            }
        };
    }

    private static AcquireResourcePoolCommand Acquire(
        int instance,
        string holderKey,
        params ResourcePoolRequirement[] requirements)
    {
        return new AcquireResourcePoolCommand
        {
            CommandId = CommandIdValue(2),
            InstanceId = InstanceIdValue(instance),
            RequestedAt = Timestamp(2),
            HolderKey = holderKey,
            Requirements = requirements,
            ExpiresAt = Timestamp(32)
        };
    }

    private static CompleteSagaCompensationCommand CompleteCompensation(string actionKey, int commandValue)
    {
        return new CompleteSagaCompensationCommand
        {
            CommandId = CommandIdValue(commandValue),
            InstanceId = InstanceIdValue(1),
            RequestedAt = Timestamp(commandValue),
            ScopeId = "scope-1",
            ActionKey = actionKey
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
            DefinitionVersion = DefinitionVersion.Initial
        };
    }

    private static WorkflowWaitRegisteredEvent WaitRegisteredEvent(WaitId waitId)
    {
        return new WorkflowWaitRegisteredEvent
        {
            EventId = EventIdValue(2),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(2),
            CausationId = CausationIdValue(2),
            OccurredAt = Timestamp(2),
            WaitId = waitId,
            EventName = "Approved",
            CorrelationId = new CorrelationId("order-1")
        };
    }

    private static WorkflowTimerScheduledEvent TimerScheduled(TimerId timerId)
    {
        return new WorkflowTimerScheduledEvent
        {
            EventId = EventIdValue(2),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(2),
            CausationId = CausationIdValue(2),
            OccurredAt = Timestamp(2),
            TimerId = timerId,
            FireAt = Timestamp(30),
            WakeupName = "approval-timeout"
        };
    }

    private static WorkflowPausedEvent Paused()
    {
        return new WorkflowPausedEvent
        {
            EventId = EventIdValue(3),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(3),
            CausationId = CausationIdValue(3),
            OccurredAt = Timestamp(3)
        };
    }

    private static WorkflowTimerBufferedEvent TimerBuffered(TimerId timerId)
    {
        return new WorkflowTimerBufferedEvent
        {
            EventId = EventIdValue(4),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(4),
            CausationId = CausationIdValue(4),
            OccurredAt = Timestamp(4),
            TimerId = timerId,
            WakeupName = "approval-timeout"
        };
    }

    private static SagaCompensationStartedEvent CompensationStarted(string actionKey, int order)
    {
        return new SagaCompensationStartedEvent
        {
            EventId = EventId.New(),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(10 + order),
            CausationId = CausationIdValue(10 + order),
            OccurredAt = Timestamp(10 + order),
            ScopeId = "scope-1",
            ActionKey = actionKey,
            Order = order
        };
    }

    private static ResourcePoolDefinition Pool(string name, int capacity)
    {
        return new ResourcePoolDefinition(name, capacity, TimeSpan.FromMinutes(30));
    }

    private static ResourcePoolRequirement Requirement(string poolName)
    {
        return new ResourcePoolRequirement(poolName, 1);
    }

    private static string FindRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find '{relativePath}'.");
    }

    private static DateTimeOffset Timestamp(int minutes)
    {
        return new DateTimeOffset(2026, 7, 2, 17, minutes, 0, TimeSpan.Zero);
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

    private static WaitId WaitIdValue(int value)
    {
        return new WaitId(GuidValue(value));
    }

    private static TimerId TimerIdValue(int value)
    {
        return new TimerId(GuidValue(value));
    }

    private static Guid GuidValue(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }

    private sealed class FlakyReleasePoolStore : IResourcePoolStore
    {
        private readonly InMemoryResourcePoolStore inner = new();
        private int failuresRemaining = 1;

        internal int ReleaseAttempts { get; private set; }

        public Task UpsertPoolAsync(ResourcePoolDefinition definition, CancellationToken cancellationToken)
        {
            return inner.UpsertPoolAsync(definition, cancellationToken);
        }

        public Task<ResourcePoolAcquireResult> AcquireAsync(
            ResourcePoolAcquireRequest request,
            CancellationToken cancellationToken)
        {
            return inner.AcquireAsync(request, cancellationToken);
        }

        public Task<ResourcePoolReleaseResult> ReleaseAsync(
            ResourcePoolReleaseRequest request,
            CancellationToken cancellationToken)
        {
            ReleaseAttempts++;
            if (failuresRemaining > 0)
            {
                failuresRemaining--;
                throw new InvalidOperationException("Transient release failure.");
            }

            return inner.ReleaseAsync(request, cancellationToken);
        }

        public Task<Option<ResourcePoolSnapshot>> GetPoolAsync(string poolName, CancellationToken cancellationToken)
        {
            return inner.GetPoolAsync(poolName, cancellationToken);
        }

        public Task ResizePoolAsync(string poolName, int capacity, CancellationToken cancellationToken)
        {
            return inner.ResizePoolAsync(poolName, capacity, cancellationToken);
        }

        public Task<ResourcePoolExpiryResult> ExpireTicketsAsync(DateTimeOffset now, CancellationToken cancellationToken)
        {
            return inner.ExpireTicketsAsync(now, cancellationToken);
        }

        public Task<ResourcePoolForceReleaseResult> ForceReleaseTicketAsync(
            Guid ticketId,
            string reason,
            DateTimeOffset releasedAt,
            CancellationToken cancellationToken)
        {
            return inner.ForceReleaseTicketAsync(ticketId, reason, releasedAt, cancellationToken);
        }
    }
}
