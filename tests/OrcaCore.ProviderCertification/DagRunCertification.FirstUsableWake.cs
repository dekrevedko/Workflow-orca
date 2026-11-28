using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Providers.Dag;

namespace OrcaCore.ProviderCertification;

public static partial class DagRunCertification
{
    public static DateTimeOffset WakeTestTime() =>
        DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600);

    private sealed class WakeRun(IWorkflowEventStore store, DagRunSnapshotV1 initial)
    {
        internal DagRunSnapshotV1 Head { get; private set; } = initial;
        internal DagRunSubscriptionV1 Subscription { get; } =
            new(Guid.NewGuid(), Guid.NewGuid(), initial.RunId, new(1), TimeSpan.FromSeconds(30));

        internal static async Task<WakeRun> CreateAsync(IWorkflowEventStore store, int admissions, CancellationToken token)
        {
            var initial = Initial();
            var nodes = Enumerable.Range(0, admissions)
                .Select(i => initial.Graph.Nodes[0] with { Ordinal = i, NodeId = $"node-{i}" }).ToArray();
            initial = initial with { Graph = initial.Graph with { Nodes = nodes },
                Nodes = nodes.Select(n => initial.Nodes[0] with { Ordinal = n.Ordinal }).ToArray() };
            var run = new WakeRun(store, initial);
            (await store.AppendAsync(Creation(initial), token)).IsSuccess.Should().BeTrue();
            (await store.SubscribeDagRunAsync(run.Subscription, token)).Outcome.Should().Be(DagRunSubscribeOutcomeV1.Subscribed);
            return run;
        }

        internal async Task<ProviderCommitBatch> PrepareAsync(CancellationToken token, OutboxWrite? extra = null)
        {
            var turn = await store.AcquireDagRunLeaseAsync(Request(Head.RunId), token);
            var fence = turn.Fence ?? throw new InvalidOperationException($"Wake certification turn was denied: {turn.Outcome}");
            var admission = Admit(Head, fence, checked((int)Head.StreamVersion.Value - 1));
            return extra is null ? admission :
                Batch(Snapshot(admission), admission.DagRun!.Records, fence, [.. admission.OutboxRecords, extra]);
        }

        internal async Task CommitAsync(ProviderCommitBatch batch, CancellationToken token)
        {
            var result = await store.AppendAsync(batch, token);
            result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Message : "the admission must commit");
            (await store.ReleaseDagRunLeaseAsync(batch.DagRun!.Fence!, token)).Outcome.Should().Be(DagRunLeaseOutcomeV1.Released);
            Head = Snapshot(batch);
        }

        internal async Task AdvanceAsync(CancellationToken token, OutboxWrite? extra = null) =>
            await CommitAsync(await PrepareAsync(token, extra), token);

        internal OutboxClaimRequest Claim(DateTimeOffset now) => new(1000, now, TimeSpan.FromSeconds(10))
        {
            KindSelector = OutboxKindSelector.Including(["dag-run-ready-v1"]), RecipientHostId = Subscription.RecipientHostId
        };

        internal async Task<OutboxWrite> ClaimAsync(IWorkflowOutboxStore outbox, DateTimeOffset now, long expected, CancellationToken token)
        {
            var writes = await outbox.ClaimAsync(Claim(now), token);
            var wake = writes.Single(w => w.DagRunSubscriptionId == Subscription.SubscriptionId);
            wake.DagRunOwner.Should().Be(Head.RunId);
            wake.RecipientHostId.Should().Be(Subscription.RecipientHostId);
            using var payload = JsonDocument.Parse(wake.Payload);
            payload.RootElement.GetProperty("version").GetInt64().Should().Be(expected);
            return wake;
        }
    }

    private static async Task AssertFencedAsync(IWorkflowOutboxStore outbox, OutboxWrite stale, CancellationToken token)
    {
        (await outbox.MarkAsync(stale.OutboxRecordId, stale.DispatchAttempt, OutboxRecordState.Dispatched, token)).Should().BeFalse();
        (await outbox.ReleaseAsync(stale.OutboxRecordId, stale.DispatchAttempt, token)).Should().BeFalse();
        (await outbox.MarkPoisonedAsync(stale.OutboxRecordId, stale.DispatchAttempt, "stale", "stale delivery", token)).Should().BeFalse();
    }

    public static async Task WakeCursorRaceAsync(IWorkflowEventStore writer, IWorkflowEventStore reader, bool cursorFirst,
        Func<DateTimeOffset> now, Action<TimeSpan> advance,
        Func<DagRunSubscriptionV1, Func<Task>, Func<Task>, CancellationToken, Task> exerciseOrder, CancellationToken caller)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(caller);
        deadline.CancelAfter(TimeSpan.FromSeconds(45));
        var token = deadline.Token;
        var run = await WakeRun.CreateAsync(writer, 4, token);
        var sub = run.Subscription;
        var outbox = (IWorkflowOutboxStore)reader;
        var ops = (IWorkflowOperationalStore)reader;
        await run.AdvanceAsync(token);
        var first = await run.ClaimAsync(outbox, now(), 2, token);
        await run.AdvanceAsync(token);
        (await outbox.GetStateAsync(first.OutboxRecordId, token)).Value.Should().Be(OutboxRecordState.Claimed);
        var admission = await run.PrepareAsync(token);
        var renewal = sub with { AfterVersion = new(3), LeaseDuration = TimeSpan.FromSeconds(9) };
        DagRunSubscribeResultV1? result = null;
        async Task Refresh() { result = await reader.SubscribeDagRunAsync(renewal, token); }
        async Task Append() { (await writer.AppendAsync(admission, token)).IsSuccess.Should().BeTrue(); }
        await exerciseOrder(sub, cursorFirst ? Refresh : Append, cursorFirst ? Append : Refresh, token);
        // A committed retry returns the receipt without creating a second wake.
        await run.CommitAsync(admission, token);
        var head = await reader.LoadDagRunCheckpointAsync(admission.StreamId, token);
        head.Value.StreamVersion.Value.Should().Be(4);
        var registered = result ?? throw new InvalidOperationException("The cursor operation did not finish.");
        var pressure = (await ops.GetOperatorStatisticsAsync(token)).Pressure;
        if (cursorFirst)
        {
            registered.Outcome.Should().Be(DagRunSubscribeOutcomeV1.Subscribed);
            registered.CurrentVersion.Should().Be(new StreamVersion(3));
            pressure.DagObserverPendingCount.Should().Be(1);
            pressure.DagObserverClaimedCount.Should().Be(0);
            await AssertFencedAsync(outbox, first, token);
            var replacement = await run.ClaimAsync(outbox, now(), 4, token);
            replacement.OutboxRecordId.Should().Be(first.OutboxRecordId);
            replacement.DispatchAttempt.Should().Be(first.DispatchAttempt + 1);
            await AssertFencedAsync(outbox, first, token);
            (await outbox.MarkAsync(replacement.OutboxRecordId, replacement.DispatchAttempt, OutboxRecordState.Dispatched, token)).Should().BeTrue();
        }
        else
        {
            registered.Outcome.Should().Be(DagRunSubscribeOutcomeV1.AlreadyChanged);
            registered.CurrentVersion.Should().Be(new StreamVersion(4));
            pressure.DagObserverPendingCount.Should().Be(0);
            pressure.DagObserverClaimedCount.Should().Be(1);
            (await outbox.MarkAsync(first.OutboxRecordId, first.DispatchAttempt, OutboxRecordState.Dispatched, token)).Should().BeTrue();
        }
        advance(TimeSpan.FromSeconds(10));
        (await ops.GetOperatorStatisticsAsync(token)).Pressure.ActiveDagObserverCount.Should().Be(cursorFirst ? 0 : 1);
        (await reader.SubscribeDagRunAsync(sub with { AfterVersion = new(4) }, token)).Outcome.Should().Be(DagRunSubscribeOutcomeV1.Subscribed);
        await run.AdvanceAsync(token);
        var next = await run.ClaimAsync(outbox, now(), 5, token);
        next.OutboxRecordId.Should().NotBe(first.OutboxRecordId);
        (await outbox.GetStateAsync(first.OutboxRecordId, token)).Value.Should().Be(OutboxRecordState.Dispatched);
    }

    public static async Task WakeDeliveryAsync(IWorkflowEventStore store, Func<DateTimeOffset> now, Action<TimeSpan> advance, CancellationToken token)
    {
        var run = await WakeRun.CreateAsync(store, 6, token);
        var sub = run.Subscription;
        var outbox = (IWorkflowOutboxStore)store;
        // A stale version comparison must not hide an attempt to bind this subscription ID to another run.
        var foreign = Initial();
        (await store.AppendAsync(Creation(foreign), token)).IsSuccess.Should().BeTrue();
        await new WakeRun(store, foreign).AdvanceAsync(token);
        Func<Task> steal = () => store.SubscribeDagRunAsync(sub with
        {
            RunId = foreign.RunId, AfterVersion = new(1), LeaseDuration = TimeSpan.FromSeconds(1)
        }, token);
        await steal.Should().ThrowAsync<InvalidDataException>().WithMessage("*DAG_INTEGRITY_CONFLICT*");
        advance(TimeSpan.FromSeconds(2));
        (await ((IWorkflowOperationalStore)store).GetOperatorStatisticsAsync(token)).Pressure.ActiveDagObserverCount.Should().Be(1);
        await run.AdvanceAsync(token);
        var first = await run.ClaimAsync(outbox, now(), 2, token);
        var originalPayload = first.Payload.ToArray();
        advance(TimeSpan.FromSeconds(5));
        await run.AdvanceAsync(token);
        (await outbox.ClaimAsync(run.Claim(now()), token)).Should().NotContain(w => w.DagRunSubscriptionId.HasValue);
        advance(TimeSpan.FromSeconds(5) + TimeSpan.FromMilliseconds(1));
        var reclaimed = await run.ClaimAsync(outbox, now(), 2, token);
        reclaimed.OutboxRecordId.Should().Be(first.OutboxRecordId);
        reclaimed.Payload.Should().Equal(first.Payload);
        reclaimed.DispatchAttempt.Should().Be(first.DispatchAttempt + 1);
        await AssertFencedAsync(outbox, first, token);
        // Bytes given to a consumer must be detached from the pending durable record.
        reclaimed.Payload.AsSpan().Fill((byte)'!');
        (await outbox.ReleaseAsync(reclaimed.OutboxRecordId, reclaimed.DispatchAttempt, token)).Should().BeTrue();
        await run.AdvanceAsync(token);
        (await outbox.GetStateAsync(first.OutboxRecordId, token)).Value.Should().Be(OutboxRecordState.Retryable);
        var retried = await run.ClaimAsync(outbox, now(), 2, token);
        retried.OutboxRecordId.Should().Be(first.OutboxRecordId);
        retried.Payload.Should().Equal(originalPayload);
        (await outbox.MarkPoisonedAsync(retried.OutboxRecordId, retried.DispatchAttempt, "test", "evidence", token)).Should().BeTrue();
        var explicitWake = first with { OutboxRecordId = OutboxRecordId.New(), DispatchAttempt = 0 };
        await run.AdvanceAsync(token, explicitWake);
        await run.AdvanceAsync(token);
        var claimed = (await outbox.ClaimAsync(run.Claim(now()), token)).Where(w => w.DagRunSubscriptionId == sub.SubscriptionId).ToArray();
        claimed.Should().HaveCount(2);
        var separate = claimed.Single(w => w.OutboxRecordId == explicitWake.OutboxRecordId);
        separate.Payload.Should().Equal(explicitWake.Payload);
        var fresh = claimed.Single(w => w.OutboxRecordId != separate.OutboxRecordId);
        fresh.OutboxRecordId.Should().NotBe(first.OutboxRecordId);
        using var bytes = JsonDocument.Parse(fresh.Payload);
        bytes.RootElement.GetProperty("version").GetInt64().Should().Be(5);
        (await outbox.GetDispatchSnapshotAsync(first.OutboxRecordId, token)).Value.PoisonCode.Should().Be("test");
        (await outbox.MarkAsync(fresh.OutboxRecordId, fresh.DispatchAttempt, OutboxRecordState.Dispatched, token)).Should().BeTrue();
        (await outbox.MarkAsync(separate.OutboxRecordId, separate.DispatchAttempt, OutboxRecordState.Dispatched, token)).Should().BeTrue();
        advance(TimeSpan.FromSeconds(31));
        await run.AdvanceAsync(token);
        (await outbox.ClaimAsync(run.Claim(now()), token)).Should().NotContain(w => w.DagRunSubscriptionId.HasValue);
    }

    public static async Task WakeCollisionAsync(IWorkflowEventStore store, bool previouslyStored, CancellationToken token)
    {
        var run = await WakeRun.CreateAsync(store, 2, token);
        var sub = run.Subscription;
        var key = $"dag-notice-v1\n{sub.RunId:D}\n3\n{sub.SubscriptionId:D}\n";
        var id = new OutboxRecordId(Guid.ParseExact(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..32], "N"));
        var explicitWrite = new OutboxWrite(id, "dag-child-cancel-v1", "explicit"u8.ToArray()) { DagRunOwner = sub.RunId };
        await run.AdvanceAsync(token, previouslyStored ? explicitWrite : null);
        var batch = await run.PrepareAsync(token, previouslyStored ? null : explicitWrite);
        var rejected = await store.AppendAsync(batch, token);
        rejected.IsFailure.Should().BeTrue();
        rejected.Error.Message.Should().Contain("collision");
        (await store.LoadDagRunCheckpointAsync(batch.StreamId, token)).Value.StreamVersion.Value.Should().Be(2);
        (await store.LoadDagRunTailAsync(batch.StreamId, new(2), token)).Should().BeEmpty();
        var outbox = (IWorkflowOutboxStore)store;
        (await outbox.GetStateAsync(id, token)).HasValue.Should().Be(previouslyStored);
        (await ((IWorkflowOperationalStore)store).GetOperatorStatisticsAsync(token)).Pressure.DagObserverPendingCount.Should().Be(1);
    }

    public enum WakeOrder { CursorFirst, AppendBeforeCursor, AckFirst, AppendBeforeAck }

    public static async Task WakeSettlementRaceAsync(IWorkflowEventStore writer, IWorkflowEventStore reader,
        bool ackFirst, DateTimeOffset now, Func<Guid, Guid, Func<Task>, Func<Task>, CancellationToken, Task> exerciseOrder,
        CancellationToken caller)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(caller);
        deadline.CancelAfter(TimeSpan.FromSeconds(45));
        var token = deadline.Token;
        var run = await WakeRun.CreateAsync(writer, 2, token);
        var outbox = (IWorkflowOutboxStore)reader;
        await run.AdvanceAsync(token);
        var old = await run.ClaimAsync(outbox, now, 2, token);
        var batch = await run.PrepareAsync(token);
        async Task Ack() => (await outbox.MarkAsync(old.OutboxRecordId, old.DispatchAttempt,
            OutboxRecordState.Dispatched, token)).Should().BeTrue();
        async Task Append() => (await writer.AppendAsync(batch, token)).IsSuccess.Should().BeTrue();
        await exerciseOrder(old.OutboxRecordId.Value, batch.OutboxRecords.Single().OutboxRecordId.Value,
            ackFirst ? Ack : Append, ackFirst ? Append : Ack, token);
        await run.CommitAsync(batch, token);
        (await reader.LoadDagRunCheckpointAsync(batch.StreamId, token)).Value.StreamVersion.Value.Should().Be(3);
        (await outbox.GetStateAsync(old.OutboxRecordId, token)).Value.Should().Be(OutboxRecordState.Dispatched);
        var pressure = (await ((IWorkflowOperationalStore)reader).GetOperatorStatisticsAsync(token)).Pressure;
        pressure.DagObserverClaimedCount.Should().Be(0);
        pressure.DagObserverPendingCount.Should().Be(ackFirst ? 1 : 0);
        if (ackFirst)
        {
            var fresh = await run.ClaimAsync(outbox, now, 3, token);
            fresh.OutboxRecordId.Should().NotBe(old.OutboxRecordId);
            (await outbox.MarkAsync(fresh.OutboxRecordId, fresh.DispatchAttempt, OutboxRecordState.Dispatched, token)).Should().BeTrue();
        }
    }

    private static void Parameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    // Rollback removes the test blocker without committing any provider state. Both
    // production calls must be seen waiting on their designated backend session.
    public static async Task ExerciseRelationalWakeOrderAsync(
        DbConnection barrier, DbConnection monitor, string lockRow, Guid id, string detectBlock,
        string barrierName, string firstName, string secondName, Func<Task> firstOperation, Func<Task> secondOperation,
        CancellationToken token)
    {
        async Task WaitForBlockAsync(string waiter, string holder, Task pending)
        {
            await using var command = monitor.CreateCommand();
            command.CommandText = detectBlock;
            Parameter(command, "waiter", waiter);
            Parameter(command, "holder", holder);
            while (!pending.IsCompleted)
            {
                if (await command.ExecuteScalarAsync(token) is not null) return;
                await Task.Yield();
            }
            await pending;
            throw new InvalidOperationException($"{waiter} completed before blocking on {holder}.");
        }

        await barrier.OpenAsync(token);
        await monitor.OpenAsync(token);
        await using var transaction = await barrier.BeginTransactionAsync(token);
        await using (var command = barrier.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = lockRow;
            Parameter(command, "id", id);
            (await command.ExecuteScalarAsync(token)).Should().Be(id);
        }
        var first = firstOperation();
        Task? second = null;
        try
        {
            await WaitForBlockAsync(firstName, barrierName, first);
            second = secondOperation();
            await WaitForBlockAsync(secondName, firstName, second);
        }
        finally
        {
            await transaction.RollbackAsync(CancellationToken.None);
            await Task.WhenAll(first, second ?? Task.CompletedTask);
        }
    }

    public static async Task AssertWakeCursorStoredAsync(DbConnection monitor, string subscriptions,
        Guid id, long expectedCursor, DateTimeOffset expectedExpiry, CancellationToken token)
    {
        await using var verify = monitor.CreateCommand();
        verify.CommandText = $"select after_version from {subscriptions} where subscription_id=@id and lease_expires_at=@expiry";
        Parameter(verify, "id", id);
        Parameter(verify, "expiry", expectedExpiry);
        (await verify.ExecuteScalarAsync(token)).Should().Be(expectedCursor, "cursor and expiry commit together");
    }
}
