using System.Text.Json;
using OrcaCore.Runtime.Durable.Persistence;

namespace OrcaCore.Tests.Durable;

public sealed class InMemoryWorkflowStoreTests
{
    [Fact]
    public async Task Create_and_load_round_trip_instance()
    {
        var store = new InMemoryWorkflowStore();
        var instance = CreateInstance("inst-1", "DefA", "v1", WorkflowStatus.Waiting, concurrencyToken: 0);

        await store.CreateAsync(instance, CancellationToken.None);
        var loaded = await store.LoadAsync("inst-1", CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal("inst-1", loaded!.InstanceId);
        Assert.Equal("DefA", loaded.DefinitionId);
        Assert.Equal("v1", loaded.DefinitionVersion);
        Assert.Equal(0, loaded.ConcurrencyToken);
        Assert.Equal(WorkflowStatus.Waiting, loaded.RuntimeState.Status);
        Assert.Equal("payload", loaded.BusinessState.GetProperty("value").GetString());
    }

    [Fact]
    public async Task Create_duplicate_instance_throws()
    {
        var store = new InMemoryWorkflowStore();
        var instance = CreateInstance("inst-1");

        await store.CreateAsync(instance, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.CreateAsync(instance, CancellationToken.None));
        Assert.Contains("already exists", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Create_is_atomic_when_outbox_append_fails()
    {
        var store = new InMemoryWorkflowStore();
        await store.AddOutboxAsync(CreateModernOutboxRecord("shared-outbox", "existing-inst", 0, 0));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.CreateAsync(
                new WorkflowCommit(
                    CreateInstance("inst-create-fail"),
                    [],
                    [],
                    [CreateModernOutboxRecord("shared-outbox", "inst-create-fail", 0, 0)],
                    [],
                    []),
                CancellationToken.None));

        Assert.Null(await store.LoadAsync("inst-create-fail", CancellationToken.None));
    }

    [Fact]
    public async Task CommitTransition_increments_concurrency_token()
    {
        var store = new InMemoryWorkflowStore();
        var instance = CreateInstance("inst-1", concurrencyToken: 0, status: WorkflowStatus.Waiting);
        await store.CreateAsync(instance, CancellationToken.None);

        await store.CommitTransitionAsync(instance with
        {
            RuntimeState = instance.RuntimeState with { Status = WorkflowStatus.Completed }
        }, CancellationToken.None);

        var loaded = await store.LoadAsync("inst-1", CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal(1, loaded!.ConcurrencyToken);
        Assert.Equal(WorkflowStatus.Completed, loaded.RuntimeState.Status);
    }

    [Fact]
    public async Task CommitTransition_rejects_stale_concurrency_token()
    {
        var store = new InMemoryWorkflowStore();
        var instance = CreateInstance("inst-1", concurrencyToken: 0);
        await store.CreateAsync(instance, CancellationToken.None);

        await store.CommitTransitionAsync(instance, CancellationToken.None);

        var ex = await Assert.ThrowsAsync<ConcurrencyException>(() =>
            store.CommitTransitionAsync(instance, CancellationToken.None));
        Assert.Contains("token mismatch", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CommitTransition_is_atomic_when_outbox_append_fails()
    {
        var store = new InMemoryWorkflowStore();
        var instance = CreateInstance("inst-1", concurrencyToken: 0, status: WorkflowStatus.Waiting);
        await store.CreateAsync(instance, CancellationToken.None);
        await store.AddOutboxAsync(new OutboxRecord(
            "out-1",
            "inst-1",
            "Existing",
            Payload: null,
            DateTimeOffset.UtcNow,
            Dispatched: false));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.CommitTransitionAsync(
                instance with
                {
                    RuntimeState = instance.RuntimeState with { Status = WorkflowStatus.Completed }
                },
                inboxRecords:
                [
                    new InboxRecord(
                        "evt-1",
                        "inst-1",
                        new PersistedEventEnvelope("Approval", "corr-1", null, null, "evt-1"),
                        DateTimeOffset.UtcNow,
                        Processed: true)
                ],
                processedInboxEventIds: ["evt-1"],
                outboxRecords:
                [
                    new OutboxRecord(
                        "out-1",
                        "inst-1",
                        "Duplicate",
                        Payload: null,
                        DateTimeOffset.UtcNow,
                        Dispatched: false)
                ],
                historyRecords:
                [
                    new HistoryRecord("Completed", DateTimeOffset.UtcNow, "details")
                ],
                CancellationToken.None));

        var loaded = await store.LoadAsync("inst-1", CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal(WorkflowStatus.Waiting, loaded!.RuntimeState.Status);
        Assert.Equal(0, loaded.ConcurrencyToken);
        Assert.Empty(store.GetInbox("inst-1"));
        Assert.Empty(store.GetHistory("inst-1"));

        var pendingOutbox = await store.GetPendingOutboxAsync(CancellationToken.None);
        Assert.Single(pendingOutbox);
        Assert.Equal("out-1", pendingOutbox[0].OutboxId);
        Assert.Equal("Existing", pendingOutbox[0].EventName);
    }

    [Fact]
    public async Task CommitTransition_suppresses_equivalent_duplicate_outbox_record_by_deterministic_key()
    {
        var store = new InMemoryWorkflowStore();
        var instance = CreateInstance("inst-1", concurrencyToken: 0, status: WorkflowStatus.Waiting);
        var originalRecord = CreateModernOutboxRecord("inst-1:0:0", "inst-1", 0, 0);
        await store.CreateAsync(
            new WorkflowCommit(
                instance,
                [],
                [],
                [originalRecord],
                [],
                []),
            CancellationToken.None);

        var loaded = await store.LoadAsync("inst-1", CancellationToken.None);
        Assert.NotNull(loaded);

        await store.CommitAsync(
            new WorkflowCommit(
                loaded!,
                [],
                [],
                [CreateModernOutboxRecord("inst-1:0:0", "inst-1", 0, 0)],
                [],
                []),
            CancellationToken.None);

        var records = store.GetOutboxRecords("inst-1");
        Assert.Single(records);
        Assert.Equal("inst-1:0:0", records[0].OutboxId);
    }

    [Fact]
    public async Task Query_filters_by_status_definition_and_version()
    {
        var store = new InMemoryWorkflowStore();
        await store.CreateAsync(CreateInstance("a", "DefA", "v1", WorkflowStatus.Waiting), CancellationToken.None);
        await store.CreateAsync(CreateInstance("b", "DefA", "v2", WorkflowStatus.Completed), CancellationToken.None);
        await store.CreateAsync(CreateInstance("c", "DefB", "v1", WorkflowStatus.Waiting), CancellationToken.None);

        var byStatus = await store.QueryAsync(status: WorkflowStatus.Waiting, ct: CancellationToken.None);
        var byDefinition = await store.QueryAsync(definitionId: "DefA", ct: CancellationToken.None);
        var byVersion = await store.QueryAsync(definitionId: "DefA", definitionVersion: "v2", ct: CancellationToken.None);

        Assert.Equal(["a", "c"], byStatus.Select(x => x.InstanceId).ToArray());
        Assert.Equal(["a", "b"], byDefinition.Select(x => x.InstanceId).ToArray());
        Assert.Equal(["b"], byVersion.Select(x => x.InstanceId).ToArray());
    }

    [Fact]
    public async Task GetPendingOutbox_and_mark_dispatched_round_trip()
    {
        var store = new InMemoryWorkflowStore();
        await store.AddOutboxAsync(new OutboxRecord(
            "out-1",
            "inst-1",
            "EvtA",
            JsonSerializer.SerializeToElement("payload-a"),
            DateTimeOffset.UtcNow.AddMinutes(-1),
            Dispatched: false));
        await store.AddOutboxAsync(new OutboxRecord(
            "out-2",
            "inst-1",
            "EvtB",
            JsonSerializer.SerializeToElement("payload-b"),
            DateTimeOffset.UtcNow,
            Dispatched: true));

        var pendingBefore = await store.GetPendingOutboxAsync(CancellationToken.None);
        await store.MarkOutboxDispatchedAsync("out-1", CancellationToken.None);
        var pendingAfter = await store.GetPendingOutboxAsync(CancellationToken.None);

        Assert.Single(pendingBefore);
        Assert.Equal("out-1", pendingBefore[0].OutboxId);
        Assert.Empty(pendingAfter);
    }

    [Fact]
    public async Task AppendHistory_and_delete_cleanup_state()
    {
        var store = new InMemoryWorkflowStore();
        await store.CreateAsync(CreateInstance("inst-1"), CancellationToken.None);
        await store.AddOutboxAsync(new OutboxRecord(
            "out-1",
            "inst-1",
            "EvtA",
            Payload: null,
            DateTimeOffset.UtcNow,
            Dispatched: false));

        await store.AppendHistoryAsync(
            "inst-1",
            new HistoryRecord("Started", DateTimeOffset.UtcNow, "details"),
            CancellationToken.None);

        Assert.Single(store.GetHistory("inst-1"));

        await store.DeleteAsync("inst-1", CancellationToken.None);

        Assert.Null(await store.LoadAsync("inst-1", CancellationToken.None));
        Assert.Empty(store.GetHistory("inst-1"));
        Assert.Empty(await store.GetPendingOutboxAsync(CancellationToken.None));
    }

    [Fact]
    public async Task PurgeArtifacts_removes_only_old_processed_or_terminal_records()
    {
        var store = new InMemoryWorkflowStore();
        await store.CreateAsync(CreateInstance("inst-1"), CancellationToken.None);

        await store.CommitTransitionAsync(
            (await store.LoadAsync("inst-1", CancellationToken.None))!,
            inboxRecords:
            [
                new InboxRecord(
                    "evt-old-processed",
                    "inst-1",
                    new PersistedEventEnvelope("Approval", "corr-1", null, null, "evt-old-processed"),
                    DateTimeOffset.UtcNow.AddHours(-2),
                    Processed: true),
                new InboxRecord(
                    "evt-keep-buffered",
                    "inst-1",
                    new PersistedEventEnvelope("Approval", "corr-1", null, null, "evt-keep-buffered"),
                    DateTimeOffset.UtcNow.AddHours(-2),
                    Processed: false)
            ],
            processedInboxEventIds: [],
            outboxRecords:
            [
                new OutboxRecord(
                    "out-dispatched-old",
                    "inst-1",
                    "EvtA",
                    Payload: null,
                    DateTimeOffset.UtcNow.AddHours(-2),
                    Dispatched: true),
                new OutboxRecord(
                    "out-pending-old",
                    "inst-1",
                    "EvtB",
                    Payload: null,
                    DateTimeOffset.UtcNow.AddHours(-2),
                    Dispatched: false),
                new OutboxRecord(
                    "out-poison-old",
                    "inst-1",
                    "EvtC",
                    Payload: null,
                    DateTimeOffset.UtcNow.AddHours(-2),
                    Dispatched: false,
                    FailureCount: 2,
                    LastFailureAt: DateTimeOffset.UtcNow.AddHours(-2),
                    LastFailure: "failed",
                    Poisoned: true)
            ],
            historyRecords:
            [
                new HistoryRecord("Old", DateTimeOffset.UtcNow.AddHours(-2), "old"),
                new HistoryRecord("Recent", DateTimeOffset.UtcNow.AddMinutes(-5), "recent")
            ],
            CancellationToken.None);

        await store.PurgeArtifactsAsync("inst-1", DateTimeOffset.UtcNow.AddHours(-1), CancellationToken.None);

        var inbox = store.GetInbox("inst-1");
        var history = store.GetHistory("inst-1");
        var pendingOutbox = await store.GetPendingOutboxAsync(CancellationToken.None);
        var dispatched = store.GetOutbox("out-dispatched-old");
        var poisoned = store.GetOutbox("out-poison-old");

        Assert.DoesNotContain(inbox, x => x.EventId == "evt-old-processed");
        Assert.Contains(inbox, x => x.EventId == "evt-keep-buffered");
        Assert.DoesNotContain(history, x => x.TransitionType == "Old");
        Assert.Contains(history, x => x.TransitionType == "Recent");
        Assert.Null(dispatched);
        Assert.Null(poisoned);
        Assert.Contains(pendingOutbox, x => x.OutboxId == "out-pending-old");
    }

    [Fact]
    public async Task PurgeArtifacts_with_cutoffs_applies_independent_retention_windows()
    {
        var store = new InMemoryWorkflowStore();
        await store.CreateAsync(CreateInstance("inst-1"), CancellationToken.None);

        await store.CommitTransitionAsync(
            (await store.LoadAsync("inst-1", CancellationToken.None))!,
            inboxRecords:
            [
                new InboxRecord(
                    "evt-old-processed",
                    "inst-1",
                    new PersistedEventEnvelope("Approval", "corr-1", null, null, "evt-old-processed"),
                    DateTimeOffset.UtcNow.AddHours(-3),
                    Processed: true)
            ],
            processedInboxEventIds: [],
            outboxRecords:
            [
                new OutboxRecord(
                    "out-dispatched-keep",
                    "inst-1",
                    "EvtKeep",
                    Payload: null,
                    DateTimeOffset.UtcNow.AddHours(-3),
                    Dispatched: true)
            ],
            historyRecords:
            [
                new HistoryRecord("Old", DateTimeOffset.UtcNow.AddHours(-3), "old")
            ],
            CancellationToken.None);

        await store.PurgeArtifactsAsync(
            "inst-1",
            new DurableArtifactRetentionCutoffs(
                ProcessedInboxOlderThan: DateTimeOffset.UtcNow.AddHours(-1),
                TerminalOutboxOlderThan: DateTimeOffset.UtcNow.AddHours(-10),
                HistoryOlderThan: DateTimeOffset.UtcNow.AddHours(-1)),
            CancellationToken.None);

        Assert.Empty(store.GetInbox("inst-1"));
        Assert.DoesNotContain(store.GetHistory("inst-1"), x => x.TransitionType == "Old");
        Assert.NotNull(store.GetOutbox("out-dispatched-keep"));
    }

    [Fact]
    public async Task LeaseDispatchableOutbox_orders_by_stream_version_and_sequence()
    {
        var store = new InMemoryWorkflowStore();
        await store.AddOutboxAsync(CreateModernOutboxRecord("inst-1:1:0", "inst-1", 1, 0));
        await store.AddOutboxAsync(CreateModernOutboxRecord("inst-1:1:1", "inst-1", 1, 1));

        var leased = await store.LeaseDispatchableOutboxAsync(
            new OutboxLeaseRequest("worker-1", 10, TimeSpan.FromMinutes(1)),
            CancellationToken.None);

        Assert.Single(leased);
        Assert.Equal("inst-1:1:0", leased[0].OutboxId);
    }

    [Fact]
    public async Task Expired_lease_can_be_reclaimed()
    {
        var store = new InMemoryWorkflowStore();
        await store.AddOutboxAsync(CreateModernOutboxRecord(
            "inst-1:1:0",
            "inst-1",
            1,
            0,
            status: OutboxStatus.Leased,
            leaseOwner: "worker-1",
            leaseExpiresAt: DateTimeOffset.UtcNow.AddSeconds(-1)));

        var leased = await store.LeaseDispatchableOutboxAsync(
            new OutboxLeaseRequest("worker-2", 10, TimeSpan.FromMinutes(1)),
            CancellationToken.None);

        Assert.Single(leased);
        Assert.Equal("worker-2", leased[0].LeaseOwner);
    }

    [Fact]
    public async Task LeaseDispatchableOutbox_blocks_higher_sequence_while_head_record_is_leased()
    {
        var store = new InMemoryWorkflowStore();
        await store.AddOutboxAsync(CreateModernOutboxRecord(
            "inst-1:1:0",
            "inst-1",
            1,
            0,
            status: OutboxStatus.Leased,
            leaseOwner: "worker-1",
            leaseExpiresAt: DateTimeOffset.UtcNow.AddMinutes(1)));
        await store.AddOutboxAsync(CreateModernOutboxRecord("inst-1:1:1", "inst-1", 1, 1));

        var leased = await store.LeaseDispatchableOutboxAsync(
            new OutboxLeaseRequest("worker-2", 10, TimeSpan.FromMinutes(1)),
            CancellationToken.None);

        Assert.Empty(leased);
    }

    [Fact]
    public async Task CompleteLeasedOutbox_rejects_lost_lease()
    {
        var store = new InMemoryWorkflowStore();
        await store.AddOutboxAsync(CreateModernOutboxRecord("inst-1:1:0", "inst-1", 1, 0));
        var leased = await store.LeaseDispatchableOutboxAsync(
            new OutboxLeaseRequest("worker-1", 1, TimeSpan.FromMinutes(1)),
            CancellationToken.None);

        await Assert.ThrowsAsync<ConcurrencyException>(() =>
            store.CompleteLeasedOutboxAsync(
                leased[0].OutboxId,
                "worker-2",
                DateTimeOffset.UtcNow,
                CancellationToken.None));
    }

    [Fact]
    public async Task WorkflowCommit_persists_projection_work_atomically()
    {
        var store = new InMemoryWorkflowStore();
        var instance = CreateInstance("inst-1");
        var projectionPayload = new DispatchPayload(new byte[] { 1, 2, 3 }, "application/json", "projection");

        await store.CreateAsync(
            new WorkflowCommit(
                instance,
                [],
                [],
                [],
                [new ProjectionWorkItem("Outbox", "Refresh", projectionPayload)],
                [new HistoryRecord("Started", DateTimeOffset.UtcNow, null)]),
            CancellationToken.None);

        var loaded = await store.LoadAsync("inst-1", CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Single(store.GetProjectionWork("inst-1"));
        Assert.Single(store.GetHistory("inst-1"));
    }

    [Fact]
    public async Task FailLeasedOutbox_retryable_returns_to_pending_with_next_attempt_and_clears_lease()
    {
        var store = new InMemoryWorkflowStore();
        await store.AddOutboxAsync(CreateModernOutboxRecord("inst-1:1:0", "inst-1", 1, 0));
        var leased = await store.LeaseDispatchableOutboxAsync(
            new OutboxLeaseRequest("worker-1", 1, TimeSpan.FromMinutes(1)),
            CancellationToken.None);

        var nextAttempt = DateTimeOffset.UtcNow.AddMinutes(5);
        var updated = await store.FailLeasedOutboxAsync(
            leased[0].OutboxId,
            "worker-1",
            new OutboxDispatchFailure("transient error", Poison: false, DateTimeOffset.UtcNow, nextAttempt),
            CancellationToken.None);

        Assert.Equal(OutboxStatus.Pending, updated.Status);
        Assert.Null(updated.LeaseOwner);
        Assert.Null(updated.LeaseExpiresAt);
        Assert.NotNull(updated.NextAttemptAt);
        Assert.Equal("transient error", updated.LastError);
    }

    [Fact]
    public async Task FailLeasedOutbox_poison_sets_poisoned_status_and_clears_lease_and_schedule()
    {
        var store = new InMemoryWorkflowStore();
        await store.AddOutboxAsync(CreateModernOutboxRecord("inst-1:1:0", "inst-1", 1, 0));
        var leased = await store.LeaseDispatchableOutboxAsync(
            new OutboxLeaseRequest("worker-1", 1, TimeSpan.FromMinutes(1)),
            CancellationToken.None);

        var updated = await store.FailLeasedOutboxAsync(
            leased[0].OutboxId,
            "worker-1",
            new OutboxDispatchFailure("fatal error", Poison: true, DateTimeOffset.UtcNow, null),
            CancellationToken.None);

        Assert.Equal(OutboxStatus.Poisoned, updated.Status);
        Assert.Null(updated.LeaseOwner);
        Assert.Null(updated.LeaseExpiresAt);
        Assert.Null(updated.NextAttemptAt);
        Assert.Equal("fatal error", updated.LastError);
    }

    [Fact]
    public async Task FailLeasedOutbox_rejects_wrong_lease_owner()
    {
        var store = new InMemoryWorkflowStore();
        await store.AddOutboxAsync(CreateModernOutboxRecord("inst-1:1:0", "inst-1", 1, 0));
        await store.LeaseDispatchableOutboxAsync(
            new OutboxLeaseRequest("worker-1", 1, TimeSpan.FromMinutes(1)),
            CancellationToken.None);

        await Assert.ThrowsAsync<ConcurrencyException>(() =>
            store.FailLeasedOutboxAsync(
                "inst-1:1:0",
                "worker-2",
                new OutboxDispatchFailure("error", Poison: false, DateTimeOffset.UtcNow, null),
                CancellationToken.None));
    }

    [Fact]
    public async Task FailLeasedOutbox_increments_attempt_count()
    {
        var store = new InMemoryWorkflowStore();
        await store.AddOutboxAsync(CreateModernOutboxRecord("inst-1:1:0", "inst-1", 1, 0));
        var leased = await store.LeaseDispatchableOutboxAsync(
            new OutboxLeaseRequest("worker-1", 1, TimeSpan.FromMinutes(1)),
            CancellationToken.None);
        Assert.Equal(0, leased[0].AttemptCount);

        var updated = await store.FailLeasedOutboxAsync(
            leased[0].OutboxId,
            "worker-1",
            new OutboxDispatchFailure(null, Poison: false, DateTimeOffset.UtcNow, null),
            CancellationToken.None);

        Assert.Equal(1, updated.AttemptCount);
    }

    [Fact]
    public async Task CompleteLeasedOutbox_marks_dispatched_and_clears_lease_metadata()
    {
        var store = new InMemoryWorkflowStore();
        await store.AddOutboxAsync(CreateModernOutboxRecord("inst-1:1:0", "inst-1", 1, 0));
        var leased = await store.LeaseDispatchableOutboxAsync(
            new OutboxLeaseRequest("worker-1", 1, TimeSpan.FromMinutes(1)),
            CancellationToken.None);

        var updated = await store.CompleteLeasedOutboxAsync(
            leased[0].OutboxId,
            "worker-1",
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.Equal(OutboxStatus.Dispatched, updated.Status);
        Assert.Null(updated.LeaseOwner);
        Assert.Null(updated.LeaseExpiresAt);
        Assert.Equal(1, updated.AttemptCount);
        Assert.NotNull(updated.LastAttemptAt);
        Assert.Null(updated.NextAttemptAt);
        Assert.Null(updated.LastError);
    }

    [Fact]
    public async Task LeaseDispatchableOutbox_blocks_higher_sequence_while_lower_is_pending_with_future_next_attempt()
    {
        var store = new InMemoryWorkflowStore();
        await store.AddOutboxAsync(
            CreateModernOutboxRecord("inst-1:1:0", "inst-1", 1, 0)
                with { NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(5) });
        await store.AddOutboxAsync(CreateModernOutboxRecord("inst-1:1:1", "inst-1", 1, 1));

        var leased = await store.LeaseDispatchableOutboxAsync(
            new OutboxLeaseRequest("worker-1", 10, TimeSpan.FromMinutes(1)),
            CancellationToken.None);

        Assert.Empty(leased);
    }

    [Fact]
    public async Task LeaseDispatchableOutbox_unblocks_sequence_after_lower_reaches_dispatched()
    {
        var store = new InMemoryWorkflowStore();
        await store.AddOutboxAsync(
            CreateModernOutboxRecord("inst-1:1:0", "inst-1", 1, 0, status: OutboxStatus.Dispatched));
        await store.AddOutboxAsync(CreateModernOutboxRecord("inst-1:1:1", "inst-1", 1, 1));

        var leased = await store.LeaseDispatchableOutboxAsync(
            new OutboxLeaseRequest("worker-1", 10, TimeSpan.FromMinutes(1)),
            CancellationToken.None);

        Assert.Single(leased);
        Assert.Equal("inst-1:1:1", leased[0].OutboxId);
    }

    [Fact]
    public async Task LeaseDispatchableOutbox_respects_max_count()
    {
        var store = new InMemoryWorkflowStore();
        for (var i = 0; i < 5; i++)
        {
            var instanceId = $"inst-{i}";
            await store.AddOutboxAsync(CreateModernOutboxRecord($"{instanceId}:1:0", instanceId, 1, 0));
        }

        var leased = await store.LeaseDispatchableOutboxAsync(
            new OutboxLeaseRequest("worker-1", 3, TimeSpan.FromMinutes(1)),
            CancellationToken.None);

        Assert.Equal(3, leased.Count);
    }

    [Fact]
    public async Task LeaseDispatchableOutbox_sets_lease_owner_and_expiry_on_acquired_record()
    {
        var store = new InMemoryWorkflowStore();
        await store.AddOutboxAsync(CreateModernOutboxRecord("inst-1:1:0", "inst-1", 1, 0));
        var before = DateTimeOffset.UtcNow;

        var leased = await store.LeaseDispatchableOutboxAsync(
            new OutboxLeaseRequest("my-worker", 1, TimeSpan.FromMinutes(5)),
            CancellationToken.None);

        Assert.Single(leased);
        Assert.Equal(OutboxStatus.Leased, leased[0].Status);
        Assert.Equal("my-worker", leased[0].LeaseOwner);
        Assert.NotNull(leased[0].LeaseExpiresAt);
        Assert.True(leased[0].LeaseExpiresAt >= before.AddMinutes(5).AddSeconds(-1));
        Assert.True(leased[0].LeaseExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(5).AddSeconds(1));
    }

    [Fact]
    public async Task LeaseDispatchableOutbox_returns_records_from_multiple_instances_in_parallel()
    {
        var store = new InMemoryWorkflowStore();
        await store.AddOutboxAsync(CreateModernOutboxRecord("inst-a:1:0", "inst-a", 1, 0));
        await store.AddOutboxAsync(CreateModernOutboxRecord("inst-b:1:0", "inst-b", 1, 0));
        await store.AddOutboxAsync(CreateModernOutboxRecord("inst-c:1:0", "inst-c", 1, 0));

        var leased = await store.LeaseDispatchableOutboxAsync(
            new OutboxLeaseRequest("worker-1", 10, TimeSpan.FromMinutes(1)),
            CancellationToken.None);

        Assert.Equal(3, leased.Count);
        Assert.Contains(leased, r => r.InstanceId == "inst-a");
        Assert.Contains(leased, r => r.InstanceId == "inst-b");
        Assert.Contains(leased, r => r.InstanceId == "inst-c");
    }

    [Fact]
    public async Task LeaseDispatchableOutbox_never_returns_dispatched_or_poisoned_records()
    {
        var store = new InMemoryWorkflowStore();
        await store.AddOutboxAsync(
            CreateModernOutboxRecord("inst-a:1:0", "inst-a", 1, 0, status: OutboxStatus.Dispatched));
        await store.AddOutboxAsync(
            CreateModernOutboxRecord("inst-b:1:0", "inst-b", 1, 0, status: OutboxStatus.Poisoned));

        var leased = await store.LeaseDispatchableOutboxAsync(
            new OutboxLeaseRequest("worker-1", 10, TimeSpan.FromMinutes(1)),
            CancellationToken.None);

        Assert.Empty(leased);
    }

    [Fact]
    public async Task CommitAsync_failure_rolls_back_all_artifacts_atomically()
    {
        var store = new InMemoryWorkflowStore();
        var instance = CreateInstance("inst-1", status: WorkflowStatus.Waiting, concurrencyToken: 0);
        await store.CreateAsync(instance, CancellationToken.None);

        // Seed an outbox record with channel "channel-original"
        var existing = CreateModernOutboxRecord("inst-1:1:0", "inst-1", 1, 0);
        await store.AddOutboxAsync(existing);

        // Attempt a commit that includes a conflicting non-equivalent version of the same record
        var conflicting = existing with { Channel = "channel-conflicting" };
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.CommitAsync(
                new WorkflowCommit(
                    instance with { RuntimeState = instance.RuntimeState with { Status = WorkflowStatus.Completed } },
                    [new InboxRecord("evt-1", "inst-1", new PersistedEventEnvelope("Approval", "corr-1", null, "evt-1"), DateTimeOffset.UtcNow, Processed: true)],
                    ["evt-1"],
                    [conflicting],
                    [],
                    [new HistoryRecord("Completed", DateTimeOffset.UtcNow, null)]),
                CancellationToken.None));

        var loaded = await store.LoadAsync("inst-1", CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal(WorkflowStatus.Waiting, loaded!.RuntimeState.Status);
        Assert.Equal(0, loaded.ConcurrencyToken);
        Assert.Empty(store.GetInbox("inst-1"));
        Assert.Empty(store.GetHistory("inst-1"));
        Assert.Equal("workflow-status", store.GetOutbox("inst-1:1:0")!.Channel);
    }

    [Fact]
    public async Task DeleteAsync_removes_all_outbox_records_including_leased()
    {
        var store = new InMemoryWorkflowStore();
        var instance = CreateInstance("inst-1");
        await store.CreateAsync(instance, CancellationToken.None);
        await store.AddOutboxAsync(CreateModernOutboxRecord("inst-1:1:0", "inst-1", 1, 0));
        await store.AddOutboxAsync(CreateModernOutboxRecord("inst-1:1:1", "inst-1", 1, 1));

        // Lease the first record
        await store.LeaseDispatchableOutboxAsync(
            new OutboxLeaseRequest("worker-1", 1, TimeSpan.FromMinutes(1)),
            CancellationToken.None);

        await store.DeleteAsync("inst-1", CancellationToken.None);

        Assert.Null(await store.LoadAsync("inst-1", CancellationToken.None));
        Assert.Empty(store.GetOutboxRecords("inst-1"));
    }

    private static PersistedInstance CreateInstance(
        string instanceId,
        string definitionId = "DefA",
        string definitionVersion = "v1",
        WorkflowStatus status = WorkflowStatus.Waiting,
        int concurrencyToken = 0)
    {
        return new PersistedInstance(
            instanceId,
            definitionId,
            definitionVersion,
            concurrencyToken,
            JsonSerializer.SerializeToElement(new { value = "payload" }),
            new PersistedRuntimeState(
                status,
                DateTimeOffset.UtcNow.AddMinutes(-5),
                DateTimeOffset.UtcNow,
                [
                    new PersistedWaitRecord(
                        "wait-1",
                        "Approval",
                        "corr-1",
                        BranchId: null,
                        DateTimeOffset.UtcNow.AddMinutes(-4),
                        WaitStatus.Active,
                        WaitMode.Resident)
                ],
                [
                    new PersistedPendingEvent(
                        new PersistedEventEnvelope(
                            "Buffered",
                            "corr-1",
                            JsonSerializer.SerializeToElement("payload"),
                            "string",
                            "evt-1"),
                        DateTimeOffset.UtcNow.AddMinutes(-3),
                        Consumed: false)
                ],
                ["evt-consumed"],
                Error: null,
                new PersistedExecutionPath(
                    BranchId: null,
                    [new PersistedFrame(PersistedFrameKind.Root, "", 1, ScopeId: null)]),
                ActiveParallel: null));
    }

    private static OutboxRecord CreateModernOutboxRecord(
        string outboxId,
        string instanceId,
        int streamVersion,
        int sequence,
        OutboxStatus status = OutboxStatus.Pending,
        string? leaseOwner = null,
        DateTimeOffset? leaseExpiresAt = null)
    {
        var payloadEnvelope = new JsonPayloadEnvelopeSerializer(DurablePayloadTypeRegistry.Default)
            .Serialize("payload", typeof(string))
            .Value!;

        return new OutboxRecord(
            outboxId,
            outboxId,
            instanceId,
            ParentInstanceId: null,
            RootInstanceId: instanceId,
            GroupId: null,
            StreamId: instanceId,
            StreamVersion: streamVersion,
            Sequence: sequence,
            MessageType: "WorkflowWaiting",
            Channel: "workflow-status",
            Destination: "WorkflowWaiting",
            PayloadEnvelope: payloadEnvelope,
            CorrelationId: null,
            CausationEventId: null,
            ResumeTokenId: null,
            Status: status,
            AttemptCount: 0,
            CreatedAt: DateTimeOffset.UtcNow,
            LastAttemptAt: null,
            NextAttemptAt: null,
            LeaseOwner: leaseOwner,
            LeaseExpiresAt: leaseExpiresAt,
            LastError: null);
    }
}
