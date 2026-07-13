namespace OrcaCore.Tests.Durable;

/// <summary>
/// Acceptance tests mapping to <c>DRA-AT-001</c> … <c>DRA-AT-004</c> (durable advanced acceptance criteria).
/// OrcaCore does not implement continue-as-new yet; <see cref="DraAt003_Logical_identity_stable_across_restart_and_resume"/> documents the
/// continuity guarantees that exist today (stable instance id and definition version binding).
/// </summary>
public sealed class DurableAdvancedAcceptanceTests
{
    private sealed class OrderState
    {
        public string Id { get; set; } = "order-1";
    }

    private sealed class NoOpStep : IStep<OrderState>
    {
        public string StepId => "NoOp";

        public Task<StepResult> ExecuteAsync(StepContext<OrderState> context) =>
            Task.FromResult<StepResult>(new StepResult.Completed());
    }

    /// <summary>
    /// DRA-AT-001: two logical “hosts” share one store; concurrent resume attempts must serialize to one durable outcome.
    /// Uses a gated load wrapper so both event deliveries observe a single load of the cold instance.
    /// </summary>
    [Fact]
    public async Task DraAt001_Concurrent_resume_attempts_against_cold_instance_serialize_single_load_and_completion()
    {
        var innerStore = new InMemoryWorkflowStore();
        var definition = new DurableWorkflowBuilder<OrderState>("DraMultiHost", "v1")
            .Init()
            .WaitLong("Approval", state => state.Id)
            .Then<NoOpStep>()
            .End()
            .Build();

        var engine1 = DurableWorkflowEngine.Create(innerStore);
        var started = await (await engine1.ForDefinitionAsync(definition)).Start(new OrderState());
        await engine1.DisposeAsync();

        var store = new LoadCountingStore(innerStore, started.InstanceId);
        await using var engine2 = DurableWorkflowEngine.Create(store);
        await engine2.ForDefinitionAsync(definition);

        var approvalTask = RaiseIgnoringTerminalStateAsync(
            engine2.Instance(started.InstanceId).RaiseEvent(
                new EventEnvelope("Approval", "order-1", "approved", "evt-approved")));
        await store.FirstLoadEntered.WaitAsync(TimeSpan.FromSeconds(2));

        var unexpectedTask = RaiseIgnoringTerminalStateAsync(
            engine2.Instance(started.InstanceId).RaiseEvent(
                new EventEnvelope("Unexpected", "order-1", "noise", "evt-unexpected")));

        await Task.Delay(100);
        store.ReleaseLoads();

        await Task.WhenAll(approvalTask, unexpectedTask);
        Assert.Equal(1, store.LoadCount);

        var final = await innerStore.LoadAsync(started.InstanceId, CancellationToken.None);
        Assert.NotNull(final);
        Assert.Equal(WorkflowStatus.Completed, final!.RuntimeState.Status);
        Assert.Contains(final.RuntimeState.ConsumedEventIds, x => x == "evt-approved");
    }

    [Fact]
    public async Task DraAt002_Long_running_workflow_accumulates_history_operator_can_inspect_store()
    {
        var store = new InMemoryWorkflowStore();
        var definition = new DurableWorkflowBuilder<OrderState>("DraHist", "v1")
            .Init()
            .Wait("E1", state => state.Id)
            .Then<NoOpStep>()
            .Wait("E2", state => state.Id)
            .End()
            .Build();

        await using var engine = DurableWorkflowEngine.Create(store);
        var started = await (await engine.ForDefinitionAsync(definition)).Start(new OrderState());

        await engine.Instance(started.InstanceId).RaiseEvent(
            new EventEnvelope("E1", "order-1", null, "evt-1"));
        await engine.Instance(started.InstanceId).RaiseEvent(
            new EventEnvelope("E2", "order-1", null, "evt-2"));

        var history = store.GetHistory(started.InstanceId);
        Assert.True(
            history.Count >= 3,
            "History should grow with multiple durable transitions so operators can observe pressure via the store (reference provider).");
    }

    [Fact]
    public async Task DraAt003_Logical_identity_stable_across_restart_and_resume()
    {
        var store = new InMemoryWorkflowStore();
        var definition = new DurableWorkflowBuilder<OrderState>("DraIdentity", "v1")
            .Init()
            .WaitLong("Approval", state => state.Id)
            .Then<NoOpStep>()
            .End()
            .Build();

        var engine1 = DurableWorkflowEngine.Create(store);
        var started = await (await engine1.ForDefinitionAsync(definition)).Start(new OrderState());
        var instanceId = started.InstanceId;
        await engine1.DisposeAsync();

        await using var engine2 = DurableWorkflowEngine.Create(store);
        await engine2.ForDefinitionAsync(definition);

        var mid = await engine2.Instance(instanceId).GetAsync();
        Assert.Equal(instanceId, mid.InstanceId);
        Assert.Equal("DraIdentity", mid.DefinitionId);
        Assert.Equal("v1", mid.DefinitionVersion);

        await engine2.Instance(instanceId).RaiseEvent(
            new EventEnvelope("Approval", "order-1", "x", "evt-one"));

        var final = await engine2.Instance(instanceId).GetAsync();
        Assert.Equal(instanceId, final.InstanceId);
        Assert.Equal("v1", final.DefinitionVersion);
        Assert.Equal(WorkflowStatus.Completed, final.Status);
    }

    [Fact]
    public async Task DraAt004_Selection_purge_does_not_remove_active_waiting_instance()
    {
        var store = new InMemoryWorkflowStore();
        var waitingDef = new DurableWorkflowBuilder<OrderState>("DraPurgeWait", "v1")
            .Init()
            .Wait("Never", state => state.Id)
            .End()
            .Build();

        var doneDef = new DurableWorkflowBuilder<OrderState>("DraPurgeDone", "v1")
            .Init()
            .End()
            .Build();

        await using var engine = DurableWorkflowEngine.Create(store);

        var waiting = await (await engine.ForDefinitionAsync(waitingDef)).Start(new OrderState());
        var completed = await (await engine.ForDefinitionAsync(doneDef)).Start(new OrderState());

        var loaded = await store.LoadAsync(completed.InstanceId, CancellationToken.None);
        Assert.NotNull(loaded);
        await store.CommitTransitionAsync(
            loaded!,
            inboxRecords:
            [
                new InboxRecord(
                    "old-processed",
                    completed.InstanceId,
                    new PersistedEventEnvelope("x", "c", null, null, "old-processed"),
                    DateTimeOffset.UtcNow.AddHours(-5),
                    Processed: true)
            ],
            processedInboxEventIds: [],
            outboxRecords: [],
            historyRecords:
            [
                new HistoryRecord("Stale", DateTimeOffset.UtcNow.AddHours(-5), "old")
            ],
            CancellationToken.None);

        await engine
            .Where(s => s.InstanceId == completed.InstanceId)
            .PurgeArtifactsAsync(DurableArtifactRetentionPolicy.Uniform(TimeSpan.FromHours(1)));

        Assert.NotNull(await store.LoadAsync(waiting.InstanceId, CancellationToken.None));
        Assert.Equal(WorkflowStatus.Waiting, (await store.LoadAsync(waiting.InstanceId, CancellationToken.None))!.RuntimeState.Status);

        Assert.Empty(store.GetInbox(completed.InstanceId));
        Assert.DoesNotContain(store.GetHistory(completed.InstanceId), h => h.TransitionType == "Stale");
    }

    private static async Task RaiseIgnoringTerminalStateAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (InvalidOperationException)
        {
            // Concurrent completion may make a later non-matching direct raise terminal.
        }
    }

    private sealed class LoadCountingStore(IWorkflowStore innerStore, string targetInstanceId) : IWorkflowStore
    {
        private readonly TaskCompletionSource<bool> _firstLoadEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _continueLoads = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _loadCount;

        public int LoadCount => Volatile.Read(ref _loadCount);

        public Task FirstLoadEntered => _firstLoadEntered.Task;

        public void ReleaseLoads() => _continueLoads.TrySetResult(true);

        public Task<PersistedInstance> CreateAsync(WorkflowCommit commit, CancellationToken ct) =>
            innerStore.CreateAsync(commit, ct);

        public async Task<PersistedInstance?> LoadAsync(string instanceId, CancellationToken ct)
        {
            if (string.Equals(instanceId, targetInstanceId, StringComparison.Ordinal))
            {
                var count = Interlocked.Increment(ref _loadCount);
                if (count == 1)
                {
                    _firstLoadEntered.TrySetResult(true);
                    await _continueLoads.Task.WaitAsync(ct);
                }
            }

            return await innerStore.LoadAsync(instanceId, ct);
        }

        public Task<IReadOnlyList<InboxRecord>> GetInboxAsync(string instanceId, CancellationToken ct) =>
            innerStore.GetInboxAsync(instanceId, ct);

        public Task<PersistedInstance> CommitAsync(WorkflowCommit commit, CancellationToken ct) =>
            innerStore.CommitAsync(commit, ct);

        public Task<IReadOnlyList<PersistedInstance>> QueryAsync(
            WorkflowStatus? status = null,
            string? definitionId = null,
            string? definitionVersion = null,
            CancellationToken ct = default) =>
            innerStore.QueryAsync(status, definitionId, definitionVersion, ct);

        public Task<CorrelationLookupResult> LookupByCorrelationAsync(string eventName, string correlationId, CancellationToken ct) =>
            innerStore.LookupByCorrelationAsync(eventName, correlationId, ct);

        public Task<IReadOnlyList<OutboxRecord>> LeaseDispatchableOutboxAsync(OutboxLeaseRequest request, CancellationToken ct) =>
            innerStore.LeaseDispatchableOutboxAsync(request, ct);

        public Task<OutboxRecord> CompleteLeasedOutboxAsync(string outboxId, string leaseOwner, DateTimeOffset dispatchedAt, CancellationToken ct) =>
            innerStore.CompleteLeasedOutboxAsync(outboxId, leaseOwner, dispatchedAt, ct);

        public Task<OutboxRecord> FailLeasedOutboxAsync(string outboxId, string leaseOwner, OutboxDispatchFailure failure, CancellationToken ct) =>
            innerStore.FailLeasedOutboxAsync(outboxId, leaseOwner, failure, ct);

        public Task AppendHistoryAsync(string instanceId, HistoryRecord record, CancellationToken ct) =>
            innerStore.AppendHistoryAsync(instanceId, record, ct);

        public Task PurgeArtifactsAsync(string instanceId, DateTimeOffset olderThan, CancellationToken ct) =>
            innerStore.PurgeArtifactsAsync(instanceId, olderThan, ct);

        public Task PurgeArtifactsAsync(string instanceId, DurableArtifactRetentionCutoffs cutoffs, CancellationToken ct) =>
            innerStore.PurgeArtifactsAsync(instanceId, cutoffs, ct);

        public Task DeleteAsync(string instanceId, CancellationToken ct) =>
            innerStore.DeleteAsync(instanceId, ct);
    }
}
