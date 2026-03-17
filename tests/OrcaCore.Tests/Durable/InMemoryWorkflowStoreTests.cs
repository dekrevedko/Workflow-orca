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
}
