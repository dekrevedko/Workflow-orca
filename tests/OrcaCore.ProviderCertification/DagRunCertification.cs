using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics.Metrics;
using AwesomeAssertions;
using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Providers.Dag;
using OrcaCore.Abstractions.Serialization;
using OrcaCore.Abstractions.Diagnostics;
using OrcaCore.Engine.Durable.Diagnostics;

namespace OrcaCore.ProviderCertification;

// Independent wire builders deliberately do not call the providers' private reducer,
// checkpoint writer, operation hash, clock or lease implementation.
public static class DagRunCertification
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task CreationAndReplayAsync(IWorkflowEventStore store, CancellationToken token)
    {
        var snapshot = Initial();
        var batch = Creation(snapshot);
        var before = await ((IWorkflowOperationalStore)store).GetOperatorStatisticsAsync(token);
        (await store.AppendAsync(batch, token)).IsSuccess.Should().BeTrue();
        var checkpoint = await store.LoadDagRunCheckpointAsync(batch.StreamId, token);
        checkpoint.HasValue.Should().BeTrue();
        checkpoint.Value.Payload.Should().Equal(batch.DagRun!.Checkpoint.Payload);
        checkpoint.Value.Payload[0] = 0;
        (await store.LoadDagRunCheckpointAsync(batch.StreamId, token)).Value.Payload[0].Should().Be((byte)'{');
        (await store.GetDagRunByStartKeyAsync(snapshot.StartKey, token)).Value.RunId.Should().Be(snapshot.RunId);
        var tail = await store.LoadDagRunTailAsync(batch.StreamId, StreamVersion.Empty, token);
        tail.Should().ContainSingle();
        DagRunRecordCodecV1.Serialize(tail[0]).Should().Equal(DagRunRecordCodecV1.Serialize(batch.DagRun.Records[0]));
        var replay = await store.AppendAsync(batch, token);
        replay.IsSuccess.Should().BeTrue();
        replay.Value.NewVersion.Should().Be(new StreamVersion(1));
        (await store.LoadDagRunTailAsync(batch.StreamId, StreamVersion.Empty, token)).Should().ContainSingle();
        var pressure = (await ((IWorkflowOperationalStore)store).GetOperatorStatisticsAsync(token)).Pressure;
        pressure.DagRunStreamEventCount.Should().Be(before.Pressure.DagRunStreamEventCount + 1);
        pressure.DagRunCheckpointCount.Should().Be(before.Pressure.DagRunCheckpointCount + 1);
        pressure.StreamEventCount.Should().Be(before.Pressure.StreamEventCount + 1);
        pressure.CheckpointCount.Should().Be(before.Pressure.CheckpointCount + 1);
        AssertGauges(await ((IWorkflowOperationalStore)store).GetOperatorStatisticsAsync(token));
        (await store.AppendAsync(Creation(snapshot with { RunId = Guid.NewGuid() }), token)).IsFailure.Should()
            .BeTrue();
        var discovered = await store.ListRecoverableDagRunsAsync(new(0, 1000), token);
        discovered.Should().Contain(r => r.RunId == snapshot.RunId);
        discovered.Select(r => r.CreationSequence).Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
        (await store.ListRecoverableDagRunsAsync(new(discovered.Max(r => r.CreationSequence), 1), token)).Should()
            .BeEmpty();
    }

    public static async Task FencedAppendAndObserversAsync(IWorkflowEventStore store, IWorkflowOutboxStore outbox,
        CancellationToken token)
    {
        var initial = Initial();
        var creation = Creation(initial);
        (await store.AppendAsync(creation, token)).IsSuccess.Should().BeTrue();
        var host = Guid.NewGuid();
        var subscription = new DagRunSubscriptionV1(Guid.NewGuid(), host, initial.RunId,
            new StreamVersion(1), TimeSpan.FromSeconds(30));
        (await store.SubscribeDagRunAsync(subscription, token)).Outcome.Should()
            .Be(DagRunSubscribeOutcomeV1.Subscribed);
        var request = Request(initial.RunId);
        var lease = await store.AcquireDagRunLeaseAsync(request, token);
        lease.Outcome.Should().Be(DagRunLeaseOutcomeV1.Granted);
        var admitted = Admit(initial, lease.Fence!);
        var combined = Admit(Snapshot(admitted), lease.Fence!, 1);
        var secondRecord = combined.DagRun!.Records[0] with { OperationId = admitted.DagRun!.OperationId };
        (await store.AppendAsync(Batch(Snapshot(combined), [admitted.DagRun.Records[0], secondRecord], lease.Fence,
            admitted.OutboxRecords.Concat(combined.OutboxRecords).ToArray()), token)).IsFailure.Should().BeTrue("one turn is one mutation, not a batch of admissions");
        var stale = await store.AppendAsync(
            admitted with
            {
                DagRun = admitted.DagRun! with { Fence = lease.Fence! with { Token = lease.Fence.Token + 1 } }
            }, token);
        stale.IsFailure.Should().BeTrue();
        stale.Error.Message.Should().StartWith("DAG_STALE_FENCE:");
        var version = await store.AppendAsync(admitted with { ExpectedVersion = new(999) }, token);
        version.IsFailure.Should().BeTrue();
        version.Error.Message.Should().StartWith("DAG_VERSION_CONFLICT:");
        (await store.AppendAsync(Admit(initial, lease.Fence!, 1), token)).IsFailure.Should()
            .BeTrue("authored admission cannot skip a lower ready node");
        var bad = admitted with
        {
            DagRun = admitted.DagRun! with
            {
                Checkpoint = admitted.DagRun.Checkpoint with { Sha256 = new string('0', 64) }
            }
        };
        (await store.AppendAsync(bad, token)).IsFailure.Should()
            .BeTrue("invalid checkpoint must roll back before consuming the turn");
        (await store.LoadDagRunTailAsync(creation.StreamId, new StreamVersion(1), token)).Should().BeEmpty();
        (await store.AppendAsync(admitted, token)).IsSuccess.Should().BeTrue();
        var next = Admit(Snapshot(admitted), lease.Fence!, 1);
        var secondWrite = await store.AppendAsync(next, token);
        secondWrite.IsFailure.Should().BeTrue("a turn may commit only one operation");
        secondWrite.Error.Message.Should().StartWith("DAG_TURN_ALREADY_COMMITTED:");
        (await store.ReleaseDagRunLeaseAsync(lease.Fence!, token)).Outcome.Should().Be(DagRunLeaseOutcomeV1.Released);
        (await store.AppendAsync(admitted, token)).IsSuccess.Should()
            .BeTrue("exact replay precedes stale-fence/version checks");
        (await store.SubscribeDagRunAsync(subscription, token)).Outcome.Should()
            .Be(DagRunSubscribeOutcomeV1.AlreadyChanged);
        var claim = new OutboxClaimRequest(1000, DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5))
            { KindSelector = OutboxKindSelector.Including(["dag-run-ready-v1"]) };
        var foreign = await outbox.ClaimAsync(claim with { RecipientHostId = Guid.NewGuid() }, token);
        foreign.Should().NotContain(w => w.RecipientHostId == host);
        var own = await outbox.ClaimAsync(claim with { RecipientHostId = host }, token);
        own.Should().ContainSingle(w =>
            w.DagRunOwner == initial.RunId && w.DagRunSubscriptionId == subscription.SubscriptionId);
        var child = await outbox.ClaimAsync(new OutboxClaimRequest(1000, DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5))
            { KindSelector = OutboxKindSelector.Including(["dag-child-start-v1"]) }, token);
        child.Should().ContainSingle(w =>
            w.OutboxRecordId == admitted.OutboxRecords[0].OutboxRecordId && w.DagRunOwner == initial.RunId);
        var pressure = (await ((IWorkflowOperationalStore)store).GetOperatorStatisticsAsync(token)).Pressure;
        pressure.InternalDagClaimedCount.Should().Be(3);
        pressure.DagObserverClaimedCount.Should().Be(1);
        pressure.ExternalOutboxClaimedCount.Should().Be(0);
        pressure.ActiveDagObserverCount.Should().Be(1);
    }

    public static async Task LeaseQueueAsync(IWorkflowEventStore store, Func<DateTimeOffset> now,
        Action<TimeSpan> advance, CancellationToken token)
    {
        Func<Task> badRelease = () => store.ReleaseDagRunLeaseAsync(
            new(Guid.Empty, Guid.NewGuid(), 1, Guid.NewGuid()), new CancellationToken(true));
        await badRelease.Should().ThrowAsync<ArgumentException>();
        Func<Task> badCancel = () => store.CancelDagRunLeaseRequestAsync(
            Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), new CancellationToken(true));
        await badCancel.Should().ThrowAsync<ArgumentException>();
        var snapshot = Initial();
        (await store.AppendAsync(Creation(snapshot), token)).IsSuccess.Should().BeTrue();
        var a = Request(snapshot.RunId);
        var b = Request(snapshot.RunId);
        var c = Request(snapshot.RunId);
        var first = await store.AcquireDagRunLeaseAsync(a, token);
        first.Outcome.Should().Be(DagRunLeaseOutcomeV1.Granted);
        first.ExpiresAt.Should().Be(now() + TimeSpan.FromSeconds(5));
        var second = await store.AcquireDagRunLeaseAsync(b, token);
        second.Outcome.Should().Be(DagRunLeaseOutcomeV1.Queued);
        second.QueueSequence.Should().BeGreaterThan(first.QueueSequence!.Value);
        (await store.AcquireDagRunLeaseAsync(b, token)).QueueSequence.Should().Be(second.QueueSequence);
        (await store.AcquireDagRunLeaseAsync(b with { RequestId = Guid.NewGuid() }, token)).Outcome.Should()
            .Be(DagRunLeaseOutcomeV1.OwnerBusy);
        (await store.AcquireDagRunLeaseAsync(c, token)).Outcome.Should().Be(DagRunLeaseOutcomeV1.Queued);
        advance(TimeSpan.FromSeconds(4));
        var renewed = await store.RenewDagRunLeaseAsync(a with { ExpectedFence = first.Fence!.Token }, token);
        renewed.ExpiresAt.Should().Be(now() + TimeSpan.FromSeconds(5));
        advance(TimeSpan.FromSeconds(4));
        await store.RenewDagRunLeaseAsync(a with { ExpectedFence = first.Fence!.Token }, token);
        advance(TimeSpan.FromSeconds(4));
        var last = await store.RenewDagRunLeaseAsync(a with { ExpectedFence = first.Fence!.Token }, token);
        last.ExpiresAt.Should().Be(first.HardDeadline);
        advance(TimeSpan.FromSeconds(4));
        (await store.RenewDagRunLeaseAsync(a with { ExpectedFence = first.Fence!.Token }, token)).Outcome.Should()
            .Be(DagRunLeaseOutcomeV1.StaleFence);
        (await store.AcquireDagRunLeaseAsync(c, token)).Outcome.Should()
            .Be(DagRunLeaseOutcomeV1.Queued, "a later waiter cannot bypass FIFO");
        var winner = await store.AcquireDagRunLeaseAsync(b, token);
        winner.Outcome.Should().Be(DagRunLeaseOutcomeV1.Granted);
        winner.Fence!.Token.Should().BeGreaterThan(first.Fence!.Token);
        (await store.ReleaseDagRunLeaseAsync(first.Fence, token)).Outcome.Should().Be(DagRunLeaseOutcomeV1.Released);
        (await store.RenewDagRunLeaseAsync(b with { ExpectedFence = winner.Fence.Token }, token)).Outcome.Should()
            .Be(DagRunLeaseOutcomeV1.Granted);
        await store.CancelDagRunLeaseRequestAsync(c.RunId, c.OwnerId, c.RequestId, token);
        (await store.AcquireDagRunLeaseAsync(c, token)).Outcome.Should().Be(DagRunLeaseOutcomeV1.StaleFence);
        (await store.ReleaseDagRunLeaseAsync(winner.Fence, token)).Outcome.Should().Be(DagRunLeaseOutcomeV1.Released);
        (await store.AcquireDagRunLeaseAsync(b, token)).Outcome.Should().Be(DagRunLeaseOutcomeV1.StaleFence);
    }

    private static DagRunLeaseRequestV1 Request(Guid run) => new(run, Guid.NewGuid(), Guid.NewGuid(),
        DagRunLeasePurposeV1.Admission, null, TimeSpan.FromSeconds(5));

    public static async Task ObserverCleanupAsync(IWorkflowEventStore store, IWorkflowOutboxStore outbox,
        Func<DateTimeOffset> now, Action<TimeSpan> advance, CancellationToken token)
    {
        var start = Initial();
        (await store.AppendAsync(Creation(start), token)).IsSuccess.Should().BeTrue();
        var host = Guid.NewGuid();
        var subscription = new DagRunSubscriptionV1(Guid.NewGuid(), host, start.RunId, new(1), TimeSpan.FromSeconds(5));
        (await store.SubscribeDagRunAsync(subscription, token)).Outcome.Should()
            .Be(DagRunSubscribeOutcomeV1.Subscribed);
        var fence = (await store.AcquireDagRunLeaseAsync(Request(start.RunId), token)).Fence!;
        var first = Admit(start, fence);
        (await store.AppendAsync(first, token)).IsSuccess.Should().BeTrue();
        await store.ReleaseDagRunLeaseAsync(fence, token);
        var request = new OutboxClaimRequest(1000, now(), TimeSpan.FromSeconds(10))
            { RecipientHostId = host, KindSelector = OutboxKindSelector.Including(["dag-run-ready-v1"]) };
        var notices = await outbox.ClaimAsync(request, token);
        var addressed = notices.Single(w => w.DagRunSubscriptionId == subscription.SubscriptionId);
        advance(TimeSpan.FromSeconds(6));
        (await store.PruneExpiredDagRunObserversAsync(100, token)).Should()
            .Be(0, "a live outbox claim owns the notice");
        var expired = (await ((IWorkflowOperationalStore)store).GetOperatorStatisticsAsync(token)).Pressure;
        expired.ExpiredDagObserverCount.Should().Be(1);
        expired.DagObserverClaimedCount.Should().Be(1);
        advance(TimeSpan.FromSeconds(5));
        (await store.PruneExpiredDagRunObserversAsync(100, token)).Should().Be(1);
        (await outbox.GetStateAsync(addressed.OutboxRecordId, token)).HasValue.Should().BeFalse();
        var snapshot = Snapshot(first);
        var nextSubscription = subscription with { SubscriptionId = Guid.NewGuid(), AfterVersion = new(2) };
        (await store.SubscribeDagRunAsync(nextSubscription, token)).Outcome.Should()
            .Be(DagRunSubscribeOutcomeV1.Subscribed);
        var nextFence = (await store.AcquireDagRunLeaseAsync(Request(start.RunId), token)).Fence!;
        var second = Admit(snapshot, nextFence, 1);
        (await store.AppendAsync(second, token)).IsSuccess.Should().BeTrue();
        var next = await outbox.ClaimAsync(request with { ClaimedAt = now() }, token);
        var poisoned = next.Single(w => w.DagRunSubscriptionId == nextSubscription.SubscriptionId);
        await outbox.MarkPoisonedAsync(poisoned.OutboxRecordId, "DAG_PROTOCOL_INVALID",
            "retain corrupt observer evidence", token);
        var children = await outbox.ClaimAsync(new OutboxClaimRequest(1000, now(), TimeSpan.FromSeconds(5))
            { KindSelector = OutboxKindSelector.Including(["dag-child-start-v1"]) }, token);
        children.Should().HaveCount(2);
        await outbox.MarkAsync(children[0].OutboxRecordId, OutboxRecordState.Retryable, token);
        await outbox.MarkPoisonedAsync(children[1].OutboxRecordId, "DAG_PROTOCOL_INVALID", "retain child work", token);
        advance(TimeSpan.FromSeconds(6));
        (await store.PruneExpiredDagRunObserversAsync(100, token)).Should()
            .Be(0, "poison is durable evidence, not routine cleanup");
        var statistics = await ((IWorkflowOperationalStore)store).GetOperatorStatisticsAsync(token);
        statistics.Pressure.DagObserverPoisonedCount.Should().Be(1);
        statistics.Pressure.InternalDagPoisonedCount.Should().Be(1);
        statistics.Pressure.InternalDagRetryableCount.Should().Be(1);
        statistics.Pressure.ExternalOutboxPoisonedCount.Should().Be(0);
        statistics.Pressure.DagRunStreamEventCount.Should().Be(3);
        AssertGauges(statistics);
    }

    public static async Task ConcurrentLeasesAsync(IWorkflowEventStore store, IWorkflowEventStore other,
        CancellationToken token)
    {
        var start = Initial();
        var batch = Creation(start);
        (await store.AppendAsync(batch, token)).IsSuccess.Should().BeTrue();
        (await other.LoadDagRunCheckpointAsync(batch.StreamId, token)).Value.Payload.Should()
            .Equal(batch.DagRun!.Checkpoint.Payload);
        var requests = Enumerable.Range(0, 8).Select(_ => Request(start.RunId)).ToArray();
        var results = await Task.WhenAll(requests.Select((r, index) =>
            (index % 2 == 0 ? store : other).AcquireDagRunLeaseAsync(r, token)));
        results.Should().ContainSingle(r => r.Outcome == DagRunLeaseOutcomeV1.Granted);
        results.Where(r => r.Outcome == DagRunLeaseOutcomeV1.Queued).Should().HaveCount(7);
        results.Select(r => r.QueueSequence).Should().OnlyHaveUniqueItems();
        var granted = results.Single(r => r.Outcome == DagRunLeaseOutcomeV1.Granted);
        (await other.ReleaseDagRunLeaseAsync(granted.Fence!, token)).Outcome.Should().Be(DagRunLeaseOutcomeV1.Released);
        var head = results.Select((r, index) => (r, index)).Where(x => x.r.Outcome == DagRunLeaseOutcomeV1.Queued)
            .MinBy(x => x.r.QueueSequence);
        (await other.AcquireDagRunLeaseAsync(requests[head.index], token)).Outcome.Should()
            .Be(DagRunLeaseOutcomeV1.Granted);
    }

    public static async Task CancellationAndIntegrityAsync(IWorkflowEventStore store, CancellationToken token)
    {
        var snapshot = Initial();
        (await store.AppendAsync(Creation(snapshot), token)).IsSuccess.Should().BeTrue();

        async Task Apply(DagRunRecordKindV1 kind, DagRunStateV1 state, DagRunLeasePurposeV1 purpose,
            DagRunNodeStateV1[]? nodes = null, bool terminal = false)
        {
            var fence = (await store.AcquireDagRunLeaseAsync(Request(snapshot.RunId) with { Purpose = purpose }, token))
                .Fence!;
            var at = snapshot.UpdatedAt.AddTicks(1);
            var operation = Guid.NewGuid();
            var changes = nodes?.Where((n, i) => n != snapshot.Nodes[i]).Select(n => new DagRunNodeChangeV1(n.Ordinal,
                n.State, n.ChildInstanceId,
                null, null, n.FailureCode, n.ReadyAt, n.StartedAt, n.CompletedAt)).ToArray() ?? [];
            var first = new DagRunRecordV1(1, kind, snapshot.RunId, operation, snapshot.StreamVersion.Next(), state,
                null, changes, at, null);
            var after = snapshot with
            {
                StreamVersion = first.StreamVersion, State = state, Nodes = nodes ?? snapshot.Nodes, UpdatedAt = at
            };
            DagRunRecordV1[] records = [first];
            if (terminal)
            {
                after = after with
                {
                    State = DagRunStateV1.Cancelled, StreamVersion = after.StreamVersion.Next(), CompletedAt = at
                };
                records =
                [
                    first,
                    new(1, DagRunRecordKindV1.Terminal, after.RunId, operation, after.StreamVersion, after.State, null,
                        [], at, at)
                ];
            }

            var result = await store.AppendAsync(Batch(after, records, fence, []), token);
            result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.ToString() : "valid transition");
            await store.ReleaseDagRunLeaseAsync(fence, token);
            snapshot = after;
        }

        await Apply(DagRunRecordKindV1.IntegrityFaulted, DagRunStateV1.IntegrityFault,
            DagRunLeasePurposeV1.IntegrityReconciliation);
        (await store.ListRecoverableDagRunsAsync(new(0, 1000), token)).Should().Contain(r =>
            r.RunId == snapshot.RunId && r.State == DagRunStateV1.IntegrityFault);
        await Apply(DagRunRecordKindV1.PoisonReconciled, DagRunStateV1.Running,
            DagRunLeasePurposeV1.IntegrityReconciliation);
        var admissionFence = (await store.AcquireDagRunLeaseAsync(Request(snapshot.RunId), token)).Fence!;
        var admission = Admit(snapshot, admissionFence);
        (await store.AppendAsync(admission, token)).IsSuccess.Should().BeTrue();
        await store.ReleaseDagRunLeaseAsync(admissionFence, token);
        snapshot = Snapshot(admission);
        var at = snapshot.UpdatedAt.AddTicks(1);
        await Apply(DagRunRecordKindV1.CancellationRequested, DagRunStateV1.CancellationRequested,
            DagRunLeasePurposeV1.RunCancellation,
            [
                snapshot.Nodes[0] with { State = DagNodeStateV1.CancellationRequested },
                snapshot.Nodes[1] with { State = DagNodeStateV1.Cancelled, CompletedAt = at }
            ]);
        at = snapshot.UpdatedAt.AddTicks(1);
        await Apply(DagRunRecordKindV1.ChildJoined, DagRunStateV1.CancellationRequested, DagRunLeasePurposeV1.ChildJoin,
            [snapshot.Nodes[0] with { State = DagNodeStateV1.Cancelled, CompletedAt = at }, snapshot.Nodes[1]], true);
        var checkpoint = (await store.LoadDagRunCheckpointAsync(WorkflowStreamId.ForDagRun(snapshot.RunId), token))
            .Value;
        using var document = JsonDocument.Parse(checkpoint.Payload);
        document.RootElement.GetProperty("snapshot").GetProperty("state").GetString().Should().Be("Cancelled");
        var tail = await store.LoadDagRunTailAsync(WorkflowStreamId.ForDagRun(snapshot.RunId), StreamVersion.Empty,
            token);
        tail.Should().HaveCount(7);
        tail.Select(r => r.Kind).Should().Contain(DagRunRecordKindV1.IntegrityFaulted).And
            .Contain(DagRunRecordKindV1.PoisonReconciled);
    }

    public static async Task CrossAggregateIdentityAsync(IWorkflowEventStore store, IWorkflowOutboxStore outbox,
        CancellationToken token)
    {
        ProviderCommitBatch Workflow(Guid id, OutboxRecordId recordId) => new()
        {
            StreamId = new(InstanceId.Parse(id.ToString("D"))), ExpectedVersion = StreamVersion.Empty,
            Events =
            [
                new OrcaCore.Abstractions.Durable.WorkflowStartedEvent
                {
                    EventId = EventId.Create(Guid.NewGuid().ToString()),
                    InstanceId = InstanceId.Parse(id.ToString("D")), CommandId = CommandId.New(),
                    CausationId = CausationId.New(),
                    OccurredAt = DateTimeOffset.UtcNow, DefinitionId = DefinitionId.New(),
                    DefinitionVersion = DefinitionVersion.Initial
                }
            ],
            OutboxRecords = [new(recordId, "application", [1])]
        };

        var run = Initial();
        (await store.AppendAsync(Creation(run), token)).IsSuccess.Should().BeTrue();
        var forbiddenOutbox = OutboxRecordId.New();
        var collision = await store.AppendAsync(Workflow(run.RunId, forbiddenOutbox), token);
        collision.IsFailure.Should().BeTrue();
        collision.Error.Message.Should().StartWith("DAG_INTEGRITY_CONFLICT:");
        (await outbox.GetStateAsync(forbiddenOutbox, token)).HasValue.Should().BeFalse();
        var workflowId = Guid.NewGuid();
        (await store.AppendAsync(Workflow(workflowId, OutboxRecordId.New()), token)).IsSuccess.Should().BeTrue();
        var reverse = await store.AppendAsync(Creation(Initial() with { RunId = workflowId }), token);
        reverse.IsFailure.Should().BeTrue();
        reverse.Error.Message.Should().StartWith("DAG_INTEGRITY_CONFLICT:");
        (await store.LoadDagRunCheckpointAsync(WorkflowStreamId.ForDagRun(workflowId), token)).HasValue.Should()
            .BeFalse();
    }

    private static void AssertGauges(WorkflowOperatorStatistics statistics)
    {
        var values = new List<(string Name, long Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, current) =>
            {
                if (instrument.Meter.Name == OrcaCoreDiagnostics.DurableSourceName &&
                    instrument.Name.StartsWith("orca.dag.", StringComparison.Ordinal))
                    current.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            values.Add((instrument.Name, value, tags.ToArray())));
        listener.Start();
        OrcaCoreDurableDiagnostics.RefreshOperatorStatistics(statistics, []);
        listener.RecordObservableInstruments();
        var p = statistics.Pressure;
        var expected = new (string Name, string? State, long Value)[]
        {
            ("orca.dag.internal.pending.count", "Pending", p.InternalDagPendingCount),
            ("orca.dag.internal.pending.count", "Retryable", p.InternalDagRetryableCount),
            ("orca.dag.internal.pending.count", "Claimed", p.InternalDagClaimedCount),
            ("orca.dag.internal.pending.count", "Poisoned", p.InternalDagPoisonedCount),
            ("orca.dag.observer.pending.count", "Pending", p.DagObserverPendingCount),
            ("orca.dag.observer.pending.count", "Retryable", p.DagObserverRetryableCount),
            ("orca.dag.observer.pending.count", "Claimed", p.DagObserverClaimedCount),
            ("orca.dag.observer.pending.count", "Poisoned", p.DagObserverPoisonedCount),
            ("orca.dag.observer.subscription.count", "Active", p.ActiveDagObserverCount),
            ("orca.dag.observer.subscription.count", "Expired", p.ExpiredDagObserverCount),
            ("orca.dag.runs.count", "Active", p.ActiveDagRunCount),
            ("orca.dag.runs.count", "Stuck", p.StuckDagRunCount),
            ("orca.dag.stream.events.count", null, p.DagRunStreamEventCount),
            ("orca.dag.checkpoints.count", null, p.DagRunCheckpointCount),
            ("orca.dag.checkpoint.lag", null, p.DagRunCheckpointLag)
        };
        values.Should().HaveCount(expected.Length);
        foreach (var item in expected)
            values.Should().ContainSingle(v => v.Name == item.Name && v.Value == item.Value &&
                                               v.Tags.Length == (item.State == null ? 1 : 2) &&
                                               v.Tags.Any(t =>
                                                   t.Key == OrcaCoreDiagnostics.ProviderNameKey &&
                                                   Equals(t.Value, statistics.ProviderName)) &&
                                               (item.State == null || v.Tags.Any(t =>
                                                   t.Key == OrcaCoreDiagnostics.OutboxStateKey &&
                                                   Equals(t.Value, item.State))));
    }

    public static async Task FailureClosureAndTerminalAsync(IWorkflowEventStore store, CancellationToken token)
    {
        var original = Initial();
        var graph = original.Graph with
        {
            Nodes =
            [
                original.Graph.Nodes[0],
                original.Graph.Nodes[1] with { DirectDependencies = new[] { 0 } },
                original.Graph.Nodes[0] with { Ordinal = 2, NodeId = "independent" }
            ]
        };
        var start = original with
        {
            Graph = graph, Nodes =
            [
                original.Nodes[0],
                original.Nodes[1] with { State = DagNodeStateV1.Pending, ReadyAt = null },
                original.Nodes[0] with { Ordinal = 2 }
            ]
        };
        (await store.AppendAsync(Creation(start), token)).IsSuccess.Should().BeTrue();
        var fence = (await store.AcquireDagRunLeaseAsync(Request(start.RunId), token)).Fence!;
        var at = start.UpdatedAt.AddTicks(1);
        var root = start.Nodes[0] with
        {
            State = DagNodeStateV1.Failed, FailureCode = "DAG_INPUT_MAPPING_INVALID", CompletedAt = at
        };
        var incomplete = start with
        {
            StreamVersion = new(2), Nodes = [root, start.Nodes[1], start.Nodes[2]], UpdatedAt = at
        };
        var operation = Guid.NewGuid();

        DagRunNodeChangeV1 Change(DagRunNodeStateV1 n) => new(n.Ordinal, n.State, n.ChildInstanceId, null, null,
            n.FailureCode, n.ReadyAt, n.StartedAt, n.CompletedAt);

        var incompleteRecord = new DagRunRecordV1(1, DagRunRecordKindV1.MappingFailed, start.RunId, operation, new(2),
            start.State, null, [Change(root)], at, null);
        (await store.AppendAsync(Batch(incomplete, [incompleteRecord], fence, []), token)).IsFailure.Should()
            .BeTrue("the checkpoint coherently repeats the missing closure, so the reducer must catch it");
        var blocked = start.Nodes[1] with
        {
            State = DagNodeStateV1.DependencyBlocked, FailureCode = "DEPENDENCY_BLOCKED", CompletedAt = at
        };
        var failed = incomplete with { Nodes = [root, blocked, start.Nodes[2]] };
        var record = incompleteRecord with { ChangedNodes = [Change(root), Change(blocked)] };
        (await store.AppendAsync(Batch(failed, [record], fence, []), token)).IsSuccess.Should().BeTrue();
        await store.ReleaseDagRunLeaseAsync(fence, token);
        var admissionFence = (await store.AcquireDagRunLeaseAsync(Request(start.RunId), token)).Fence!;
        var admission = Admit(failed, admissionFence, 2);
        (await store.AppendAsync(admission, token)).IsSuccess.Should().BeTrue();
        await store.ReleaseDagRunLeaseAsync(admissionFence, token);
        var admitted = Snapshot(admission);
        var joinFence =
            (await store.AcquireDagRunLeaseAsync(Request(start.RunId) with { Purpose = DagRunLeasePurposeV1.ChildJoin },
                token)).Fence!;
        var joinedAt = admitted.UpdatedAt.AddTicks(1);
        var child = admitted.Nodes[2] with { State = DagNodeStateV1.Succeeded, CompletedAt = joinedAt };
        var end = admitted with
        {
            StreamVersion = new(5), State = DagRunStateV1.Failed, Nodes = [root, blocked, child],
            UpdatedAt = joinedAt, CompletedAt = joinedAt
        };
        var joinOp = Guid.NewGuid();
        var join = new DagRunRecordV1(1, DagRunRecordKindV1.ChildJoined, start.RunId, joinOp, new(4),
            DagRunStateV1.Running, null, [Change(child)], joinedAt, null);
        var terminal = new DagRunRecordV1(1, DagRunRecordKindV1.Terminal, start.RunId, joinOp, new(5),
            DagRunStateV1.Failed, null, [], joinedAt, joinedAt);
        var completion = await store.AppendAsync(Batch(end, [join, terminal], joinFence, []), token);
        completion.IsSuccess.Should()
            .BeTrue(completion.IsFailure ? completion.Error.ToString() : "valid terminal closure");
        (await store.AcquireDagRunLeaseAsync(Request(start.RunId), token)).Outcome.Should()
            .Be(DagRunLeaseOutcomeV1.Terminal);
        (await store.ListRecoverableDagRunsAsync(new(0, 1000), token)).Should().NotContain(r => r.RunId == start.RunId);
        (await store.LoadDagRunTailAsync(WorkflowStreamId.ForDagRun(start.RunId), StreamVersion.Empty, token)).Should()
            .HaveCount(5);
    }

    private static DagRunSnapshotV1 Snapshot(ProviderCommitBatch batch)
    {
        using var document = JsonDocument.Parse(batch.DagRun!.Checkpoint.Payload);
        return JsonSerializer.Deserialize<DagRunSnapshotV1>(document.RootElement.GetProperty("snapshot").GetRawText(),
            Json)!;
    }

    private static DagRunSnapshotV1 Initial()
    {
        var binding = new DagRunBindingV1(DefinitionId.Parse(Guid.NewGuid().ToString("D")), DefinitionVersion.Initial,
            new string('A', 64));
        var time = DateTimeOffset.UtcNow;
        return new(Guid.NewGuid(), new(1), new("key-" + Guid.NewGuid().ToString("N")),
            new(binding, "input",
                [new(0, "first", binding, "input", null, []), new(1, "second", binding, "input", null, [])]),
            Value(), DagRunStateV1.Running,
            [
                new(0, DagNodeStateV1.Ready, null, null, null, null, time, null, null),
                new(1, DagNodeStateV1.Ready, null, null, null, null, time, null, null)
            ], time, time, null);
    }

    private static DagRunValueV1 Value() => new(true, "input", "orcacore-json-v1", "null"u8.ToArray(),
        Convert.ToHexString(SHA256.HashData("null"u8)));

    private static ProviderCommitBatch Creation(DagRunSnapshotV1 snapshot) => Batch(snapshot,
    [
        new(1, DagRunRecordKindV1.Created, snapshot.RunId, Guid.NewGuid(), new(1), DagRunStateV1.Running, snapshot, [],
            snapshot.UpdatedAt, null)
    ], null, []);

    private static ProviderCommitBatch Admit(DagRunSnapshotV1 before, DagRunFenceV1 fence, int ordinal = 0)
    {
        var time = before.UpdatedAt.AddTicks(1);
        var input = Value();
        var child = Guid.ParseExact(
            Convert.ToHexStringLower(
                SHA256.HashData(Encoding.UTF8.GetBytes($"orcacore-dag-child-v1\n{before.RunId:D}\n{ordinal}\n")))[..32],
            "N");
        var node = before.Nodes[ordinal] with
        {
            State = DagNodeStateV1.StartAuthorized, ChildInstanceId = InstanceId.Parse(child.ToString("D")),
            Input = input, StartedAt = time
        };
        var nodes = before.Nodes.ToArray();
        nodes[ordinal] = node;
        var after = before with { StreamVersion = before.StreamVersion.Next(), Nodes = nodes, UpdatedAt = time };
        var record = new DagRunRecordV1(1, DagRunRecordKindV1.InputAdmitted, before.RunId, Guid.NewGuid(),
            after.StreamVersion, DagRunStateV1.Running, null,
            [new(ordinal, node.State, node.ChildInstanceId, input, null, null, node.ReadyAt, time, null)], time, null);
        return Batch(after, [record], fence,
            [new(OutboxRecordId.New(), "dag-child-start-v1", "child"u8.ToArray()) { DagRunOwner = before.RunId }]);
    }

    private static ProviderCommitBatch Batch(DagRunSnapshotV1 after, IReadOnlyList<DagRunRecordV1> records,
        DagRunFenceV1? fence, IReadOnlyList<OutboxWrite> outbox)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            new
            {
                aggregateKind = "dag-run-v1", version = 1, runId = after.RunId,
                streamVersion = after.StreamVersion.Value, snapshot = after
            }, Json);
        var checkpoint = new DagRunCheckpointV1(1, after.RunId, after.StreamVersion,
            "application/vnd.orcacore.dag-run+json;v=1", bytes, Convert.ToHexStringLower(SHA256.HashData(bytes)));
        return new()
        {
            StreamId = WorkflowStreamId.ForDagRun(after.RunId),
            ExpectedVersion = new(records[0].StreamVersion.Value - 1), OutboxRecords = outbox,
            DagRun = new(records[0].OperationId, Fingerprint(records, outbox), fence, records, checkpoint)
        };
    }

    private static string Fingerprint(IReadOnlyList<DagRunRecordV1> records, IReadOnlyList<OutboxWrite> outbox)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("records");
            foreach (var r in records)
            {
                using var doc = JsonDocument.Parse(DagRunRecordCodecV1.Serialize(r));
                Intent(writer, doc.RootElement);
            }

            writer.WriteEndArray();
            writer.WriteStartArray("outbox");
            foreach (var w in outbox.OrderBy(w => w.OutboxRecordId.ToString(), StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("id", w.OutboxRecordId.ToString());
                writer.WriteString("kind", w.Kind);
                writer.WriteBase64String("payload", w.Payload);
                writer.WriteString("owner", w.DagRunOwner?.ToString("D"));
                writer.WriteString("recipient", w.RecipientHostId?.ToString("D"));
                writer.WriteString("subscription", w.DagRunSubscriptionId?.ToString("D"));
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Convert.ToHexStringLower(SHA256.HashData(buffer.ToArray()));
    }

    private static void Intent(Utf8JsonWriter writer, JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var p in element.EnumerateObject())
            {
                if (p.Name is "streamVersion" or "createdAt" or "updatedAt" or "completedAt" or "readyAt"
                    or "startedAt") continue;
                writer.WritePropertyName(p.Name);
                Intent(writer, p.Value);
            }

            writer.WriteEndObject();
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var e in element.EnumerateArray()) Intent(writer, e);
            writer.WriteEndArray();
        }
        else element.WriteTo(writer);
    }
}
