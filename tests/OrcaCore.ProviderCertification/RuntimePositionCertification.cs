using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Providers.Dag;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Driver;
using Xunit;

namespace OrcaCore.ProviderCertification;

// The caller must supply separately constructed stores for the same database/schema.
public static class RuntimePositionCertification
{
    public static async Task PositionOnlyCommitRejectsASecondHostAtTheOldVersion(
        IWorkflowEventStore store, IWorkflowEventStore otherStore, bool fiberFailure)
    {
        Assert.NotSame(store, otherStore);
        var first = new DurableCommandProcessor(store);
        var second = new DurableCommandProcessor(otherStore);
        var runtime = new DurableWorkflowRuntime(first, new DurableDefinitionRegistry(), TimeProvider.System);
        var definition = Workflow.Durable<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(v => new State(v)).Delay(TimeSpan.FromDays(1)).End().Build();
        runtime.RegisterDefinition(definition);
        var start = await runtime.StartOrGetAsync<string, State>("position-version", definition.DefinitionId,
            definition.DefinitionVersion, "value", TestContext.Current.CancellationToken);
        var checkpoint = (await store.LoadCheckpointAsync(start.InstanceId, TestContext.Current.CancellationToken)).Value;
        var parent = InstanceId.Parse(Guid.CreateVersion7().ToString());
        var root = InstanceId.Parse(Guid.CreateVersion7().ToString());
        var marker = new WorkflowExecutionPositionChangedEvent
        {
            EventId = EventId.Create(Guid.CreateVersion7().ToString()), InstanceId = start.InstanceId,
            CommandId = CommandId.New(), CausationId = CausationId.New(), OccurredAt = DateTimeOffset.UtcNow,
            ParentInstanceId = parent, RootInstanceId = root
        };
        var withLineage = checkpoint with
        {
            StreamVersion = checkpoint.StreamVersion.Next(), ParentInstanceId = parent, RootInstanceId = root
        };
        Assert.True((await store.AppendAsync(new ProviderCommitBatch
        {
            StreamId = new WorkflowStreamId(start.InstanceId), ExpectedVersion = checkpoint.StreamVersion,
            Events = [marker], Checkpoint = withLineage
        }, TestContext.Current.CancellationToken)).IsSuccess);
        checkpoint = (await otherStore.LoadCheckpointAsync(start.InstanceId, TestContext.Current.CancellationToken)).Value;
        Assert.Equal(parent, checkpoint.ParentInstanceId);
        Assert.Equal(root, checkpoint.RootInstanceId);
        var oldEnvelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint.Payload);
        DurableCheckpointPayload envelope = oldEnvelope with
        {
            Fibers = oldEnvelope.Fibers.Select(fiber => fiber with { QuantumRotationCount = fiber.QuantumRotationCount + 1 }).ToArray()
        };
        var commandId = new CommandId(Guid.NewGuid());
        var accepted = fiberFailure
            ? await first.ProcessAsync(new DurableFiberFailedCommand(commandId, start.InstanceId, DateTimeOffset.UtcNow,
                "root/position", "failed branch", envelope) { ExpectedStreamVersion = checkpoint.StreamVersion }, TestContext.Current.CancellationToken)
            : await first.ProcessAsync(new DurableYieldCommand(commandId, start.InstanceId, DateTimeOffset.UtcNow,
                "root/position", envelope) { ExpectedStreamVersion = checkpoint.StreamVersion }, TestContext.Current.CancellationToken);
        Assert.Equal(DurableCommandOutcome.Committed, accepted.Outcome);
        var current = (await otherStore.LoadCheckpointAsync(start.InstanceId, TestContext.Current.CancellationToken)).Value;
        Assert.True(current.StreamVersion.Value > checkpoint.StreamVersion.Value);
        Assert.Equal(envelope.Payload, current.Payload);
        var stale = await second.ProcessAsync(new DurableYieldCommand(new CommandId(Guid.NewGuid()),
            start.InstanceId, DateTimeOffset.UtcNow, "root/stale", oldEnvelope)
            { ExpectedStreamVersion = checkpoint.StreamVersion }, TestContext.Current.CancellationToken);
        Assert.Equal(DurableCommandOutcome.Conflict, stale.Outcome);
        var winner = (await otherStore.LoadCheckpointAsync(start.InstanceId, TestContext.Current.CancellationToken)).Value;
        Assert.Equal(current.StreamVersion, winner.StreamVersion);
        Assert.Equal(envelope.Payload, winner.Payload);
        Assert.Equal(parent, winner.ParentInstanceId);
        Assert.Equal(root, winner.RootInstanceId);
        var tail = await otherStore.LoadTailAsync(new WorkflowStreamId(start.InstanceId), checkpoint.StreamVersion,
            TestContext.Current.CancellationToken);
        var committed = Assert.IsType<WorkflowExecutionPositionChangedEvent>(Assert.Single(tail));
        Assert.Equal(parent, committed.ParentInstanceId);
        Assert.Equal(root, committed.RootInstanceId);
    }

    public static async Task InterleavedCheckpointLoadMustNotCommitPublishTwice(
        IWorkflowEventStore store, IWorkflowEventStore otherStore, IWorkflowOutboxStore outbox, bool afterTail)
    {
        Assert.NotSame(store, otherStore);
        var other = new DurableWorkflowRuntime(new DurableCommandProcessor(otherStore), new DurableDefinitionRegistry(), TimeProvider.System);
        var wrapped = new InterleavingStore(store, async id => { await other.DriveAsync(id, TestContext.Current.CancellationToken); })
        { AdvanceAfterTail = afterTail };
        var runtime = new DurableWorkflowRuntime(new DurableCommandProcessor(wrapped), new DurableDefinitionRegistry(), TimeProvider.System);
        var outgoing = WorkflowEventContract.Create(EventName.Create("review-published"), EventContractVersion.Initial);
        var correlation = CorrelationId.Create("review-race");
        var definition = Workflow.Durable<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(v => new State(v))
            .Publish(outgoing, _ => correlation)
            .Delay(TimeSpan.FromDays(1))
            .End().Build();
        runtime.RegisterDefinition(definition);
        other.RegisterDefinition(definition);
        var started = await runtime.StartOrGetAsync<string, State>("review-key", definition.DefinitionId, definition.DefinitionVersion, "value", TestContext.Current.CancellationToken);
        var published = await outbox.ClaimAsync(new OutboxClaimRequest(10, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1))
        { KindSelector = OutboxKindSelector.Including(OutboxKinds.WorkflowEvent) }, TestContext.Current.CancellationToken);
        Assert.Single(published);
        Assert.Equal(1, wrapped.Interleavings);
        var checkpoint = (await otherStore.LoadCheckpointAsync(started.InstanceId, TestContext.Current.CancellationToken)).Value;
        Assert.Single(checkpoint.RuntimeState.ActiveTimers);
        Assert.False((await new DurableAggregateLoader(otherStore).LoadAsync(started.InstanceId,
            TestContext.Current.CancellationToken)).IsTerminal);
        Assert.False((await new DurableAggregateLoader(otherStore).LoadExecutionStateAsync(started.InstanceId,
            TestContext.Current.CancellationToken)).NeedsPositionReload);
    }

    public sealed record State(string Value);

    private sealed class InterleavingStore(
        IWorkflowEventStore inner,
        Func<InstanceId, Task> advanceOtherHost) : IWorkflowEventStore, IWorkflowStartIdempotencyStore
    {
        internal bool AdvanceAfterTail { get; init; }
        internal int Interleavings { get; private set; }
        public Task<Option<DagRunCheckpointV1>> LoadDagRunCheckpointAsync(
            WorkflowStreamId streamId, CancellationToken cancellationToken) =>
            inner.LoadDagRunCheckpointAsync(streamId, cancellationToken);

        public Task<Option<DagRunDiscoveryV1>> GetDagRunByStartKeyAsync(
            DagRunStartKeyV1 startKey, CancellationToken cancellationToken) =>
            inner.GetDagRunByStartKeyAsync(startKey, cancellationToken);

        public Task<IReadOnlyList<DagRunRecordV1>> LoadDagRunTailAsync(
            WorkflowStreamId streamId, StreamVersion afterVersion, CancellationToken cancellationToken) =>
            inner.LoadDagRunTailAsync(streamId, afterVersion, cancellationToken);

        public Task<DagRunLeaseResultV1> AcquireDagRunLeaseAsync(
            DagRunLeaseRequestV1 request, CancellationToken cancellationToken) =>
            inner.AcquireDagRunLeaseAsync(request, cancellationToken);

        public Task<DagRunLeaseResultV1> RenewDagRunLeaseAsync(
            DagRunLeaseRequestV1 request, CancellationToken cancellationToken) =>
            inner.RenewDagRunLeaseAsync(request, cancellationToken);

        public Task<DagRunLeaseResultV1> ReleaseDagRunLeaseAsync(
            DagRunFenceV1 fence, CancellationToken cancellationToken) =>
            inner.ReleaseDagRunLeaseAsync(fence, cancellationToken);

        public Task CancelDagRunLeaseRequestAsync(
            Guid runId, Guid ownerId, Guid requestId, CancellationToken cancellationToken) =>
            inner.CancelDagRunLeaseRequestAsync(runId, ownerId, requestId, cancellationToken);

        public Task<IReadOnlyList<DagRunDiscoveryV1>> ListRecoverableDagRunsAsync(
            DagRunDiscoveryRequestV1 request, CancellationToken cancellationToken) =>
            inner.ListRecoverableDagRunsAsync(request, cancellationToken);

        public Task<DagRunSubscribeResultV1> SubscribeDagRunAsync(
            DagRunSubscriptionV1 subscription, CancellationToken cancellationToken) =>
            inner.SubscribeDagRunAsync(subscription, cancellationToken);

        public Task UnsubscribeDagRunAsync(Guid subscriptionId, Guid recipientHostId,
            CancellationToken cancellationToken) =>
            inner.UnsubscribeDagRunAsync(subscriptionId, recipientHostId, cancellationToken);

        public Task<int> PruneExpiredDagRunObserversAsync(
            int maxCount, CancellationToken cancellationToken) =>
            inner.PruneExpiredDagRunObserversAsync(maxCount, cancellationToken);

        private int armed = 1;
        public async Task<Option<CheckpointWrite>> LoadCheckpointAsync(InstanceId instanceId, CancellationToken cancellationToken)
        {
            var captured = await inner.LoadCheckpointAsync(instanceId, cancellationToken);
            var tail = await inner.LoadTailAsync(new WorkflowStreamId(instanceId), StreamVersion.Empty, cancellationToken);
            if (!AdvanceAfterTail && tail.Count > 0 && Interlocked.Exchange(ref armed, 0) == 1)
            {
                Interleavings++;
                await advanceOtherHost(instanceId);
            }
            return captured;
        }
        public Task<Result<AppendEventsResult>> AppendAsync(ProviderCommitBatch batch, CancellationToken cancellationToken) =>
            inner.AppendAsync(batch, cancellationToken);
        public async Task<IReadOnlyList<global::OrcaCore.Abstractions.Durable.WorkflowEvent>> LoadTailAsync(
            WorkflowStreamId streamId, StreamVersion afterVersion, CancellationToken cancellationToken)
        {
            var tail = await inner.LoadTailAsync(streamId, afterVersion, cancellationToken);
            if (AdvanceAfterTail && tail.Count > 0 && Interlocked.Exchange(ref armed, 0) == 1)
            {
                Interleavings++;
                await advanceOtherHost(streamId.InstanceId);
            }
            return tail;
        }

        public Task<Option<StartedWorkflowIdempotencyRecord>> GetStartedAsync(
            string idempotencyKey,
            CancellationToken cancellationToken) =>
            ((IWorkflowStartIdempotencyStore)inner).GetStartedAsync(idempotencyKey, cancellationToken);
    }

}
