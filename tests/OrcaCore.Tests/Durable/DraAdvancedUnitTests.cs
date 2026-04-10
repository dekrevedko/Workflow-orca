using System.Text.Json;
using OrcaCore.Runtime.Durable.Management;
using OrcaCore.Runtime.Durable.Persistence;

namespace OrcaCore.Tests.Durable;

/// <summary>
/// Unit tests for durable advanced acceptance criteria (DRA-AT-001 … DRA-AT-004) at the persistence layer.
/// </summary>
public sealed class DraAdvancedUnitTests
{
    [Fact]
    public async Task DraAt001_Two_parallel_commits_with_same_initial_token_only_one_succeeds()
    {
        var store = new InMemoryWorkflowStore();
        var instance = CreateMinimalInstance("inst-dra1", concurrencyToken: 0);
        await store.CreateAsync(instance, CancellationToken.None);

        var stale = (await store.LoadAsync("inst-dra1", CancellationToken.None))!;
        var next = stale with
        {
            RuntimeState = stale.RuntimeState with { Status = WorkflowStatus.Completed }
        };

        Exception? first = null;
        Exception? second = null;

        var t1 = Task.Run(async () =>
        {
            try
            {
                await store.CommitTransitionAsync(next, CancellationToken.None);
            }
            catch (Exception ex)
            {
                first = ex;
            }
        });

        var t2 = Task.Run(async () =>
        {
            try
            {
                await store.CommitTransitionAsync(next, CancellationToken.None);
            }
            catch (Exception ex)
            {
                second = ex;
            }
        });

        await Task.WhenAll(t1, t2);

        var concurrencyErrors = new[] { first, second }.Count(e => e is ConcurrencyException);
        Assert.Equal(1, concurrencyErrors);

        var final = await store.LoadAsync("inst-dra1", CancellationToken.None);
        Assert.NotNull(final);
        Assert.Equal(WorkflowStatus.Completed, final!.RuntimeState.Status);
        Assert.Equal(1, final.ConcurrencyToken);
    }

    [Fact]
    public async Task DraAt002_Appended_history_is_observable_and_grows_with_activity()
    {
        var store = new InMemoryWorkflowStore();
        await store.CreateAsync(CreateMinimalInstance("inst-dra2"), CancellationToken.None);

        Assert.Empty(store.GetHistory("inst-dra2"));

        for (var i = 0; i < 50; i++)
        {
            await store.AppendHistoryAsync(
                "inst-dra2",
                new HistoryRecord("Synthetic", DateTimeOffset.UtcNow.AddMilliseconds(i), $"n={i}"),
                CancellationToken.None);
        }

        Assert.Equal(50, store.GetHistory("inst-dra2").Count);
    }

    [Fact]
    public async Task DraAt003_Instance_identity_and_definition_binding_remain_after_commit_lineage()
    {
        var store = new InMemoryWorkflowStore();
        var instance = CreateMinimalInstance("inst-dra3", definitionId: "OrderFlow", definitionVersion: "2026-04-01", concurrencyToken: 0);
        await store.CreateAsync(instance, CancellationToken.None);

        var current = (await store.LoadAsync("inst-dra3", CancellationToken.None))!;
        for (var i = 0; i < 5; i++)
        {
            current = await store.CommitTransitionAsync(
                current with
                {
                    RuntimeState = current.RuntimeState with
                    {
                        LastTransitionAt = DateTimeOffset.UtcNow
                    }
                },
                CancellationToken.None);
        }

        var loaded = await store.LoadAsync("inst-dra3", CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal("inst-dra3", loaded!.InstanceId);
        Assert.Equal("OrderFlow", loaded.DefinitionId);
        Assert.Equal("2026-04-01", loaded.DefinitionVersion);
        Assert.Equal(5, loaded.ConcurrencyToken);
    }

    [Fact]
    public async Task DraAt003_History_purge_trims_old_records_but_preserves_instance_row()
    {
        var store = new InMemoryWorkflowStore();
        await store.CreateAsync(CreateMinimalInstance("inst-dra3b"), CancellationToken.None);

        var loaded = (await store.LoadAsync("inst-dra3b", CancellationToken.None))!;
        await store.CommitTransitionAsync(
            loaded,
            inboxRecords: [],
            processedInboxEventIds: [],
            outboxRecords: [],
            historyRecords:
            [
                new HistoryRecord("Old", DateTimeOffset.UtcNow.AddHours(-10), "a"),
                new HistoryRecord("Recent", DateTimeOffset.UtcNow.AddMinutes(-1), "b")
            ],
            CancellationToken.None);

        await store.PurgeArtifactsAsync(
            "inst-dra3b",
            new DurableArtifactRetentionCutoffs(
                ProcessedInboxOlderThan: null,
                TerminalOutboxOlderThan: null,
                HistoryOlderThan: DateTimeOffset.UtcNow.AddHours(-2)),
            CancellationToken.None);

        var after = await store.LoadAsync("inst-dra3b", CancellationToken.None);
        Assert.NotNull(after);
        Assert.Equal("inst-dra3b", after!.InstanceId);
        Assert.DoesNotContain(store.GetHistory("inst-dra3b"), h => h.TransitionType == "Old");
        Assert.Contains(store.GetHistory("inst-dra3b"), h => h.TransitionType == "Recent");
    }

    [Fact]
    public async Task DraAt004_PurgeArtifacts_never_removes_the_instance_row()
    {
        var store = new InMemoryWorkflowStore();
        await store.CreateAsync(CreateMinimalInstance("inst-dra4"), CancellationToken.None);

        await store.PurgeArtifactsAsync("inst-dra4", DateTimeOffset.UtcNow, CancellationToken.None);

        var loaded = await store.LoadAsync("inst-dra4", CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal("inst-dra4", loaded!.InstanceId);
    }

    [Fact]
    public async Task DraAt004_Purge_retention_only_removes_processed_inbox_matching_cutoff()
    {
        var store = new InMemoryWorkflowStore();
        await store.CreateAsync(CreateMinimalInstance("inst-dra4b"), CancellationToken.None);

        var loaded = (await store.LoadAsync("inst-dra4b", CancellationToken.None))!;
        await store.CommitTransitionAsync(
            loaded,
            inboxRecords:
            [
                new InboxRecord(
                    "evt-old",
                    "inst-dra4b",
                    new PersistedEventEnvelope("E", "c", null, null, "evt-old"),
                    DateTimeOffset.UtcNow.AddHours(-3),
                    Processed: true),
                new InboxRecord(
                    "evt-pending",
                    "inst-dra4b",
                    new PersistedEventEnvelope("E", "c", null, null, "evt-pending"),
                    DateTimeOffset.UtcNow.AddHours(-3),
                    Processed: false)
            ],
            processedInboxEventIds: [],
            outboxRecords: [],
            historyRecords: [],
            CancellationToken.None);

        await store.PurgeArtifactsAsync(
            "inst-dra4b",
            new DurableArtifactRetentionCutoffs(
                ProcessedInboxOlderThan: DateTimeOffset.UtcNow.AddHours(-1),
                TerminalOutboxOlderThan: null,
                HistoryOlderThan: null),
            CancellationToken.None);

        var inbox = store.GetInbox("inst-dra4b");
        Assert.DoesNotContain(inbox, r => r.EventId == "evt-old");
        Assert.Contains(inbox, r => r.EventId == "evt-pending");
    }

    private static PersistedInstance CreateMinimalInstance(
        string instanceId,
        string definitionId = "DefA",
        string definitionVersion = "v1",
        int concurrencyToken = 0) =>
        new(
            instanceId,
            definitionId,
            definitionVersion,
            concurrencyToken,
            JsonSerializer.SerializeToElement(new { value = "payload" }),
            new PersistedRuntimeState(
                WorkflowStatus.Waiting,
                DateTimeOffset.UtcNow.AddMinutes(-5),
                DateTimeOffset.UtcNow,
                [],
                [],
                [],
                Error: null,
                new PersistedExecutionPath(BranchId: null, [new PersistedFrame(PersistedFrameKind.Root, "", 0, ScopeId: null)]),
                ActiveParallel: null));
}
