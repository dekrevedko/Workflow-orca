using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Execution;

public sealed class DurableCommitPipelineTests
{
    [Fact]
    public async Task CommitAsync_WhenNoMutationInboundEvent_AppendsPoisonedInboxOnlyCommit()
    {
        var instanceId = InstanceIdValue(1);
        var inboxEventId = EventIdValue(99);
        var aggregate = DurableWorkflowAggregate.Rehydrate(null, [Started(instanceId)]);
        var eventStore = new RecordingEventStore();
        var pipeline = CreatePipeline(eventStore);

        var result = await pipeline.CommitAsync(
            instanceId,
            aggregate,
            DurableDecision.Empty,
            inboxEventId,
            TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(DurableCommandOutcome.Poisoned);
        result.Message.Should().Be("No active wait matched the inbound event.");
        result.StreamVersion.Should().Be(new StreamVersion(2));
        eventStore.AppendedBatch.Should().NotBeNull();
        eventStore.AppendedBatch!.ExpectedVersion.Should().Be(new StreamVersion(1));
        eventStore.AppendedBatch.Events.Should().BeEmpty();
        eventStore.AppendedBatch.InboxOperations.Should().ContainSingle().Which.Should().Be(
            new InboxWrite(inboxEventId, InboxRecordState.Poisoned));
    }

    [Fact]
    public async Task CommitAsync_WhenNoMutationEvicts_ReturnsEvictedWithoutAppend()
    {
        var instanceId = InstanceIdValue(1);
        var aggregate = DurableWorkflowAggregate.Rehydrate(null, [Started(instanceId)]);
        var eventStore = new RecordingEventStore();
        var pipeline = CreatePipeline(eventStore);

        var result = await pipeline.CommitAsync(
            instanceId,
            aggregate,
            new DurableDecision([], evictAfterCommit: true),
            inboxEventId: null,
            TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(DurableCommandOutcome.Evicted);
        result.Evicted.Should().BeTrue();
        result.StreamVersion.Should().Be(new StreamVersion(1));
        eventStore.AppendCount.Should().Be(0);
    }

    [Fact]
    public async Task CommitAsync_WhenAppendConflicts_ReturnsConflictAtOriginalVersion()
    {
        var instanceId = InstanceIdValue(1);
        var aggregate = DurableWorkflowAggregate.Rehydrate(null, [Started(instanceId)]);
        var eventStore = new RecordingEventStore
        {
            AppendResult = Result<AppendEventsResult>.Failure(
                new WorkflowConcurrencyException("expected version conflict"))
        };
        var pipeline = CreatePipeline(eventStore);

        var result = await pipeline.CommitAsync(
            instanceId,
            aggregate,
            new DurableDecision([StepCompleted(instanceId)]),
            inboxEventId: null,
            TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(DurableCommandOutcome.Conflict);
        result.Message.Should().Contain("expected version conflict");
        result.StreamVersion.Should().Be(new StreamVersion(1));
        eventStore.AppendedBatch.Should().NotBeNull();
        eventStore.AppendedBatch!.Events.Should().ContainSingle()
            .Which.Should().BeOfType<WorkflowStepCompletedEvent>();
    }

    [Fact]
    public async Task CommitAsync_WhenAppendConflicts_RollsBackGrantedResourceTickets()
    {
        var instanceId = InstanceIdValue(1);
        var aggregate = DurableWorkflowAggregate.Rehydrate(null, [Started(instanceId)]);
        var pools = new InMemoryResourcePoolStore();
        await pools.UpsertPoolAsync(Pool("db", 1), TestContext.Current.CancellationToken);
        var acquire = await pools.AcquireAsync(
            AcquireRequest(instanceId, "root/1"),
            TestContext.Current.CancellationToken);
        var eventStore = new RecordingEventStore
        {
            AppendResult = Result<AppendEventsResult>.Failure(
                new WorkflowConcurrencyException("expected version conflict"))
        };
        var pipeline = CreatePipeline(eventStore, pools);

        await pipeline.CommitAsync(
            instanceId,
            aggregate,
            new DurableDecision([Acquired(instanceId, "root/1", acquire.Tickets)]),
            inboxEventId: null,
            TestContext.Current.CancellationToken);
        var snapshot = await pools.GetPoolAsync("db", TestContext.Current.CancellationToken);

        snapshot.Value.HeldTickets.Should().BeEmpty();
    }

    [Fact]
    public async Task CommitAsync_WhenAppendSucceeds_ReleasesCommittedResourceTickets()
    {
        var instanceId = InstanceIdValue(1);
        var aggregate = DurableWorkflowAggregate.Rehydrate(null, [Started(instanceId)]);
        var pools = new InMemoryResourcePoolStore();
        await pools.UpsertPoolAsync(Pool("db", 1), TestContext.Current.CancellationToken);
        var acquire = await pools.AcquireAsync(
            AcquireRequest(instanceId, "root/1"),
            TestContext.Current.CancellationToken);
        var pipeline = CreatePipeline(new RecordingEventStore(), pools);

        var result = await pipeline.CommitAsync(
            instanceId,
            aggregate,
            new DurableDecision([Released(instanceId, "root/1", acquire.Tickets)]),
            inboxEventId: null,
            TestContext.Current.CancellationToken);
        var snapshot = await pools.GetPoolAsync("db", TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(DurableCommandOutcome.Committed);
        snapshot.Value.HeldTickets.Should().BeEmpty();
    }

    [Fact]
    public async Task CommitAsync_WhenPostCommitReleaseFails_StillReturnsCommitted()
    {
        var instanceId = InstanceIdValue(1);
        var aggregate = DurableWorkflowAggregate.Rehydrate(null, [Started(instanceId)]);
        var pools = new InMemoryResourcePoolStore();
        await pools.UpsertPoolAsync(Pool("db", 1), TestContext.Current.CancellationToken);
        var acquire = await pools.AcquireAsync(
            AcquireRequest(instanceId, "root/1"),
            TestContext.Current.CancellationToken);
        var pipeline = CreatePipeline(new RecordingEventStore(), new ReleaseThrowingResourcePoolStore(pools));

        var result = await pipeline.CommitAsync(
            instanceId,
            aggregate,
            new DurableDecision([Released(instanceId, "root/1", acquire.Tickets)]),
            inboxEventId: null,
            TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(
            DurableCommandOutcome.Committed,
            "the append already succeeded; a ticket-release failure is recovered by lease expiry " +
            "and must not make the committed command look failed");
    }

    private static DurableCommitPipeline CreatePipeline(
        RecordingEventStore eventStore,
        IResourcePoolStore? resourcePoolStore = null)
    {
        return new DurableCommitPipeline(
            eventStore,
            new DurableCommitMaterializer(),
            new DurableResourcePoolCommitEffects(resourcePoolStore));
    }

    private static WorkflowStartedEvent Started(InstanceId instanceId)
    {
        return new WorkflowStartedEvent
        {
            EventId = EventIdValue(1),
            InstanceId = instanceId,
            CommandId = CommandIdValue(1),
            CausationId = CausationIdValue(1),
            OccurredAt = Timestamp(1),
            DefinitionId = DefinitionIdValue(1),
            DefinitionVersion = DefinitionVersion.Initial
        };
    }

    private static WorkflowStepCompletedEvent StepCompleted(InstanceId instanceId)
    {
        return new WorkflowStepCompletedEvent
        {
            EventId = EventIdValue(2),
            InstanceId = instanceId,
            CommandId = CommandIdValue(2),
            CausationId = CausationIdValue(2),
            OccurredAt = Timestamp(2),
            StepPath = "root/1"
        };
    }

    private static WorkflowResourcePoolAcquiredEvent Acquired(
        InstanceId instanceId,
        string holderKey,
        IReadOnlyList<ResourcePoolTicket> tickets)
    {
        return new WorkflowResourcePoolAcquiredEvent
        {
            EventId = EventIdValue(3),
            InstanceId = instanceId,
            CommandId = CommandIdValue(3),
            CausationId = CausationIdValue(3),
            OccurredAt = Timestamp(3),
            HolderKey = holderKey,
            Tickets = tickets
        };
    }

    private static WorkflowResourcePoolReleasedEvent Released(
        InstanceId instanceId,
        string holderKey,
        IReadOnlyList<ResourcePoolTicket> tickets)
    {
        return new WorkflowResourcePoolReleasedEvent
        {
            EventId = EventIdValue(4),
            InstanceId = instanceId,
            CommandId = CommandIdValue(4),
            CausationId = CausationIdValue(4),
            OccurredAt = Timestamp(4),
            HolderKey = holderKey,
            Tickets = tickets
        };
    }

    private static ResourcePoolDefinition Pool(string name, int capacity)
    {
        return new ResourcePoolDefinition(name, capacity, TimeSpan.FromMinutes(30));
    }

    private static ResourcePoolAcquireRequest AcquireRequest(InstanceId instanceId, string holderKey)
    {
        return new ResourcePoolAcquireRequest(
            instanceId,
            holderKey,
            [new ResourcePoolRequirement("db", 1)],
            Timestamp(1),
            Timestamp(31));
    }

    private static DateTimeOffset Timestamp(int seconds)
    {
        return new DateTimeOffset(2026, 7, 3, 20, 0, seconds, TimeSpan.Zero);
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

    private static Guid GuidValue(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }

    private sealed class ReleaseThrowingResourcePoolStore(IResourcePoolStore inner) : IResourcePoolStore
    {
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
            throw new InvalidOperationException("release transport failure");
        }

        public Task<Option<ResourcePoolSnapshot>> GetPoolAsync(string poolName, CancellationToken cancellationToken)
        {
            return inner.GetPoolAsync(poolName, cancellationToken);
        }

        public Task ResizePoolAsync(string poolName, int capacity, CancellationToken cancellationToken)
        {
            return inner.ResizePoolAsync(poolName, capacity, cancellationToken);
        }

        public Task<ResourcePoolExpiryResult> ExpireTicketsAsync(
            DateTimeOffset expiredAt,
            CancellationToken cancellationToken)
        {
            return inner.ExpireTicketsAsync(expiredAt, cancellationToken);
        }

        public Task<ResourcePoolForceReleaseResult> ForceReleaseTicketAsync(
            Guid ticketId,
            string operatorId,
            DateTimeOffset releasedAt,
            CancellationToken cancellationToken)
        {
            return inner.ForceReleaseTicketAsync(ticketId, operatorId, releasedAt, cancellationToken);
        }
    }

    private sealed class RecordingEventStore : IWorkflowEventStore
    {
        public Result<AppendEventsResult>? AppendResult { get; init; }

        public int AppendCount { get; private set; }

        public ProviderCommitBatch? AppendedBatch { get; private set; }

        public Task<Option<CheckpointWrite>> LoadCheckpointAsync(
            InstanceId instanceId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Option<CheckpointWrite>.None);
        }

        public Task<Result<AppendEventsResult>> AppendAsync(
            ProviderCommitBatch batch,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AppendCount++;
            AppendedBatch = batch;
            return Task.FromResult(AppendResult
                ?? Result<AppendEventsResult>.Success(new AppendEventsResult(batch.ExpectedVersion.Next())));
        }

        public Task<IReadOnlyList<WorkflowEvent>> LoadTailAsync(
            WorkflowStreamId streamId,
            StreamVersion afterVersion,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<WorkflowEvent>>([]);
        }
    }
}
