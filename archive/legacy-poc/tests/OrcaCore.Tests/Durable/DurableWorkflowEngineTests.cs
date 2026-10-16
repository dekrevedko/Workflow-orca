using OrcaCore.Runtime.Durable.Persistence;

namespace OrcaCore.Tests.Durable;

public sealed class DurableWorkflowEngineTests
{
    private sealed class OrderState
    {
        public string Id { get; set; } = "order-1";
        public string? Result { get; set; }
        public string? LastDefinitionId { get; set; }
        public string? LastNodePath { get; set; }
        public bool SawResumedContext { get; set; }
    }

    private record BasePayload(string Value);

    private sealed record DerivedPayload(string Value, string Extra) : BasePayload(Value);

    private sealed class CapturePayloadStep : IStep<OrderState>
    {
        public string StepId => "CapturePayload";

        public Task<StepResult> ExecuteAsync(StepContext<OrderState> context)
        {
            context.State.Result = context.ResumedEvent?.Payload as string;
            return Task.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class CaptureContextStep : IStep<OrderState>
    {
        public string StepId => "CaptureContext";

        public Task<StepResult> ExecuteAsync(StepContext<OrderState> context)
        {
            context.State.LastDefinitionId = context.DefinitionId;
            context.State.LastNodePath = context.CurrentNodePath;
            context.State.SawResumedContext = context.IsResumed;
            return Task.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class CallbackOutboxDispatcher(Action<DispatchMessage> onDispatch) : IMessageDispatcher
    {
        public Task<Result<DispatchOutcome>> DispatchAsync(DispatchMessage message, CancellationToken cancellationToken)
        {
            onDispatch(message);
            return Task.FromResult(Result<DispatchOutcome>.Success(new DispatchOutcome(true, false, null)));
        }
    }

    private sealed class FailingOutboxDispatcher(Func<DispatchMessage, bool> shouldFail) : IMessageDispatcher
    {
        public Task<Result<DispatchOutcome>> DispatchAsync(DispatchMessage message, CancellationToken cancellationToken)
        {
            if (shouldFail(message))
                throw new InvalidOperationException("dispatch failed");

            return Task.FromResult(Result<DispatchOutcome>.Success(new DispatchOutcome(true, false, null)));
        }
    }

    private sealed class RecordingOutboxPumpObserver : IOutboxPumpObserver
    {
        public List<string> SucceededEvents { get; } = [];

        public List<string> FailedEvents { get; } = [];

        public List<OutboxPumpCycleResult> Cycles { get; } = [];

        public void OnDispatchSucceeded(OutboxRecord record) => SucceededEvents.Add(record.EventName);

        public void OnDispatchFailed(OutboxRecord record, Exception exception) => FailedEvents.Add(record.EventName);

        public void OnCycleCompleted(OutboxPumpCycleResult result) => Cycles.Add(result);
    }

    private sealed class ThrowingOutboxPumpObserver(
        bool throwOnDispatchSucceeded = false,
        bool throwOnDispatchFailed = false,
        bool throwOnCycleCompleted = false) : IOutboxPumpObserver
    {
        public void OnDispatchSucceeded(OutboxRecord record)
        {
            if (throwOnDispatchSucceeded)
                throw new InvalidOperationException("observer success failure");
        }

        public void OnDispatchFailed(OutboxRecord record, Exception exception)
        {
            if (throwOnDispatchFailed)
                throw new InvalidOperationException("observer failure failure");
        }

        public void OnCycleCompleted(OutboxPumpCycleResult result)
        {
            if (throwOnCycleCompleted)
                throw new InvalidOperationException("observer cycle failure");
        }
    }

    private sealed class SequenceOutboxDispatcher(params bool[] failSequence) : IMessageDispatcher
    {
        private readonly Queue<bool> _failures = new(failSequence);

        public Task<Result<DispatchOutcome>> DispatchAsync(DispatchMessage message, CancellationToken cancellationToken)
        {
            if (_failures.Count != 0 && _failures.Dequeue())
                throw new InvalidOperationException("dispatch failed");

            return Task.FromResult(Result<DispatchOutcome>.Success(new DispatchOutcome(true, false, null)));
        }
    }

    private sealed class RecordingDelayStrategy(Func<OutboxPumpDelayContext, TimeSpan> resolveDelay) : IOutboxPumpDelayStrategy
    {
        public List<OutboxPumpDelayContext> Contexts { get; } = [];

        public TimeSpan GetDelay(OutboxPumpDelayContext context)
        {
            Contexts.Add(context);
            return resolveDelay(context);
        }
    }

    private sealed class RecordingOutboxPoisonHandler : IOutboxPoisonHandler
    {
        public List<string> PoisonedOutboxIds { get; } = [];

        public Task HandleAsync(OutboxRecord record, Exception exception, CancellationToken cancellationToken)
        {
            PoisonedOutboxIds.Add(record.OutboxId);
            return Task.CompletedTask;
        }
    }

    private sealed class BlockingOutboxDispatcher : IMessageDispatcher
    {
        private readonly TaskCompletionSource<bool> _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Entered => _entered.Task;

        public async Task<Result<DispatchOutcome>> DispatchAsync(DispatchMessage message, CancellationToken cancellationToken)
        {
            _entered.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return Result<DispatchOutcome>.Success(new DispatchOutcome(true, false, null));
        }
    }

    private sealed class BlockingResumeStep : IStep<OrderState>
    {
        private static TaskCompletionSource<bool> _entered = NewSignal();
        private static TaskCompletionSource<bool> _continue = NewSignal();

        public string StepId => "BlockingResume";

        public static Task Entered => _entered.Task;

        public static void Reset()
        {
            _entered = NewSignal();
            _continue = NewSignal();
        }

        public static void AllowContinue() => _continue.TrySetResult(true);

        public async Task<StepResult> ExecuteAsync(StepContext<OrderState> context)
        {
            _entered.TrySetResult(true);
            await _continue.Task;
            return new StepResult.Completed();
        }

        private static TaskCompletionSource<bool> NewSignal() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
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

    private sealed class FailingCommitStore(
        IWorkflowStore innerStore,
        bool failNextProcessedCommit = true,
        bool failNextBufferedCommit = false) : IWorkflowStore
    {
        private bool _failNextProcessedCommit = failNextProcessedCommit;
        private bool _failNextBufferedCommit = failNextBufferedCommit;

        public Task<PersistedInstance> CreateAsync(WorkflowCommit commit, CancellationToken ct) =>
            innerStore.CreateAsync(commit, ct);

        public Task<PersistedInstance?> LoadAsync(string instanceId, CancellationToken ct) =>
            innerStore.LoadAsync(instanceId, ct);

        public Task<IReadOnlyList<InboxRecord>> GetInboxAsync(string instanceId, CancellationToken ct) =>
            innerStore.GetInboxAsync(instanceId, ct);

        public Task<PersistedInstance> CommitAsync(WorkflowCommit commit, CancellationToken ct)
        {
            if (_failNextProcessedCommit && commit.InboxRecords.Any(x => x.Processed))
            {
                _failNextProcessedCommit = false;
                throw new ConcurrencyException("Injected commit failure.");
            }

            if (_failNextBufferedCommit && commit.InboxRecords.Any(x => !x.Processed))
            {
                _failNextBufferedCommit = false;
                throw new ConcurrencyException("Injected buffered commit failure.");
            }

            return innerStore.CommitAsync(commit, ct);
        }

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

    private sealed class FailingCreateStore(IWorkflowStore innerStore) : IWorkflowStore
    {
        private bool _failNextCreate = true;

        public Task<PersistedInstance> CreateAsync(WorkflowCommit commit, CancellationToken ct)
        {
            if (_failNextCreate)
            {
                _failNextCreate = false;
                throw new InvalidOperationException("Injected create failure.");
            }

            return innerStore.CreateAsync(commit, ct);
        }

        public Task<PersistedInstance?> LoadAsync(string instanceId, CancellationToken ct) =>
            innerStore.LoadAsync(instanceId, ct);

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

    private sealed class AuthoritativeCreateTokenStore(
        IWorkflowStore innerStore,
        int initialConcurrencyToken) : IWorkflowStore
    {
        public Task<PersistedInstance> CreateAsync(WorkflowCommit commit, CancellationToken ct)
        {
            var rewrittenOutbox = commit.OutboxRecords
                .Select(record => Rewrite(record, initialConcurrencyToken))
                .ToArray();

            return innerStore.CreateAsync(
                commit with
                {
                    Instance = commit.Instance with { ConcurrencyToken = initialConcurrencyToken },
                    OutboxRecords = rewrittenOutbox
                },
                ct);
        }

        public Task<PersistedInstance?> LoadAsync(string instanceId, CancellationToken ct) =>
            innerStore.LoadAsync(instanceId, ct);

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

        private static OutboxRecord Rewrite(OutboxRecord record, int streamVersion)
        {
            var outboxId = OutboxRecord.CreateDeterministicId(record.InstanceId, streamVersion, record.Sequence);
            return record with
            {
                OutboxId = outboxId,
                IdempotencyKey = outboxId,
                StreamVersion = streamVersion
            };
        }
    }

    private sealed class FailingCommitWithoutReloadStore(IWorkflowStore innerStore) : IWorkflowStore
    {
        private bool _failNextProcessedCommit = true;
        private bool _blockLoadAfterFailure;

        public Task<PersistedInstance> CreateAsync(WorkflowCommit commit, CancellationToken ct) =>
            innerStore.CreateAsync(commit, ct);

        public Task<PersistedInstance?> LoadAsync(string instanceId, CancellationToken ct)
        {
            if (_blockLoadAfterFailure)
                throw new InvalidOperationException("LoadAsync should not be used after commit failure for resident wait recovery.");

            return innerStore.LoadAsync(instanceId, ct);
        }

        public Task<IReadOnlyList<InboxRecord>> GetInboxAsync(string instanceId, CancellationToken ct) =>
            innerStore.GetInboxAsync(instanceId, ct);

        public Task<PersistedInstance> CommitAsync(WorkflowCommit commit, CancellationToken ct)
        {
            if (_failNextProcessedCommit && commit.InboxRecords.Any(x => x.Processed))
            {
                _failNextProcessedCommit = false;
                _blockLoadAfterFailure = true;
                throw new ConcurrencyException("Injected commit failure.");
            }

            return innerStore.CommitAsync(commit, ct);
        }

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

    [Fact]
    public async Task Durable_waitlong_survives_restart_and_resumes_to_completion()
    {
        var store = new InMemoryWorkflowStore();
        var definition = new DurableWorkflowBuilder<OrderState>("DurableOrderFlow", "v1")
            .Init()
            .WaitLong("OrderApproved", state => state.Id)
            .Then<CapturePayloadStep>()
            .End()
            .Build();

        var engine1 = DurableWorkflowEngine.Create(store);
        var typed1 = await engine1.ForDefinitionAsync(definition);

        var started = await typed1.Start(new OrderState());

        Assert.Equal(WorkflowStatus.Waiting, started.Status);
        Assert.Equal("v1", started.DefinitionVersion);
        Assert.Equal(1, started.ActiveWaitCount);
        Assert.Equal(0, started.ConcurrencyToken);
        Assert.False(engine1.IsInstanceLoaded(started.InstanceId));

        await engine1.DisposeAsync();

        var engine2 = DurableWorkflowEngine.Create(store);
        await engine2.ForDefinitionAsync(definition);

        await engine2.RaiseEvent(new EventEnvelope("OrderApproved", "order-1", "approved", "evt-1"));

        var final = await engine2.Instance(started.InstanceId).GetAsync();
        var state = await engine2.Instance(started.InstanceId).GetStateAsync<OrderState>();
        var persisted = await store.LoadAsync(started.InstanceId, CancellationToken.None);

        Assert.Equal(WorkflowStatus.Completed, final.Status);
        Assert.Equal(0, final.ActiveWaitCount);
        Assert.Equal(1, final.ConcurrencyToken);
        Assert.Equal("approved", state.Result);
        Assert.NotNull(persisted);
        Assert.Equal(WorkflowStatus.Completed, persisted!.RuntimeState.Status);
        Assert.Equal(1, persisted.ConcurrencyToken);
    }

    [Fact]
    public async Task Start_failure_does_not_publish_hot_correlation_before_persisted_create_succeeds()
    {
        var innerStore = new InMemoryWorkflowStore();
        var store = new FailingCreateStore(innerStore);
        var definition = new DurableWorkflowBuilder<OrderState>("StartFailureFlow", "v1")
            .Init()
            .Wait("Approval", state => state.Id)
            .End()
            .Build();

        await using var engine = DurableWorkflowEngine.Create(store);
        var typed = await engine.ForDefinitionAsync(definition);

        await Assert.ThrowsAsync<InvalidOperationException>(() => typed.Start(new OrderState()));

        await Assert.ThrowsAsync<NoActiveWaitException>(() =>
            engine.RaiseEvent(new EventEnvelope("Approval", "order-1", null, "evt-1")));

        var started = await typed.Start(new OrderState());

        Assert.Equal(WorkflowStatus.Waiting, started.Status);
        Assert.True(engine.IsInstanceLoaded(started.InstanceId));
    }

    [Fact]
    public async Task Registering_definition_does_not_eagerly_load_cold_waitlong_instances_after_restart()
    {
        var store = new InMemoryWorkflowStore();
        var definition = new DurableWorkflowBuilder<OrderState>("ColdRegistrationFlow", "v1")
            .Init()
            .WaitLong("Approval", state => state.Id)
            .End()
            .Build();

        var engine1 = DurableWorkflowEngine.Create(store);
        var started = await (await engine1.ForDefinitionAsync(definition)).Start(new OrderState());
        await engine1.DisposeAsync();

        await using var engine2 = DurableWorkflowEngine.Create(store);

        Assert.False(engine2.IsInstanceLoaded(started.InstanceId));

        await engine2.ForDefinitionAsync(definition);

        Assert.False(engine2.IsInstanceLoaded(started.InstanceId));
    }

    [Fact]
    public async Task Registering_definition_eagerly_loads_regular_wait_instances_after_restart()
    {
        var store = new InMemoryWorkflowStore();
        var definition = new DurableWorkflowBuilder<OrderState>("ResidentRegistrationFlow", "v1")
            .Init()
            .Wait("Approval", state => state.Id)
            .End()
            .Build();

        var engine1 = DurableWorkflowEngine.Create(store);
        var started = await (await engine1.ForDefinitionAsync(definition)).Start(new OrderState());
        await engine1.DisposeAsync();

        await using var engine2 = DurableWorkflowEngine.Create(store);

        Assert.False(engine2.IsInstanceLoaded(started.InstanceId));

        await engine2.ForDefinitionAsync(definition);

        Assert.True(engine2.IsInstanceLoaded(started.InstanceId));
    }

    [Fact]
    public async Task Correlation_targeted_raise_throws_explicit_ambiguous_routing_exception_for_hot_matches()
    {
        var store = new InMemoryWorkflowStore();
        var definition = new DurableWorkflowBuilder<OrderState>("AmbiguousHotRoutingFlow", "v1")
            .Init()
            .Wait("Approval", state => state.Id)
            .End()
            .Build();

        await using var engine = DurableWorkflowEngine.Create(store);
        await (await engine.ForDefinitionAsync(definition)).Start(new OrderState { Id = "shared" });
        await (await engine.ForDefinitionAsync(definition)).Start(new OrderState { Id = "shared" });

        var ex = await Assert.ThrowsAsync<AmbiguousCorrelationException>(() =>
            engine.RaiseEvent(new EventEnvelope("Approval", "shared", null, "evt-1")));

        Assert.Equal("Approval", ex.EventName);
        Assert.Equal("shared", ex.CorrelationId);
        Assert.Equal(2, ex.MatchCount);
    }

    [Fact]
    public async Task Definition_scoped_fanout_returns_mixed_per_instance_results_without_aborting_later_instances()
    {
        var store = new InMemoryWorkflowStore();
        var definition = new DurableWorkflowBuilder<OrderState>("FanoutFlow", "v1")
            .Init()
            .Wait("Approval", state => state.Id)
            .Then<CapturePayloadStep>()
            .End()
            .Build();

        await using var engine = DurableWorkflowEngine.Create(store);
        var typed = await engine.ForDefinitionAsync(definition);

        var waiting = await typed.Start(new OrderState { Id = "shared" });
        var completed = await typed.Start(new OrderState { Id = "other" });
        await engine.Instance(completed.InstanceId).RaiseEvent(new EventEnvelope("Approval", "other", "done", "evt-complete"));

        var result = await typed.RaiseEvent(new EventEnvelope("Approval", "shared", "approved", "evt-fanout"));

        var waitingSnapshot = await engine.Instance(waiting.InstanceId).GetAsync();
        var completedSnapshot = await engine.Instance(completed.InstanceId).GetAsync();

        Assert.Equal(2, result.AttemptedCount);
        Assert.Equal(1, result.SucceededCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Contains(result.InstanceResults, x => x.InstanceId == waiting.InstanceId && x.Succeeded);
        Assert.Contains(result.InstanceResults, x => x.InstanceId == completed.InstanceId && !x.Succeeded);
        Assert.IsType<InvalidOperationException>(result.InstanceResults.Single(x => x.InstanceId == completed.InstanceId).Error);
        Assert.Equal(WorkflowStatus.Completed, waitingSnapshot.Status);
        Assert.Equal(WorkflowStatus.Completed, completedSnapshot.Status);
    }

    [Fact]
    public async Task Durable_waitlong_eviction_is_immediate_but_regular_wait_stays_loaded()
    {
        var store = new InMemoryWorkflowStore();
        var longDefinition = new DurableWorkflowBuilder<OrderState>("LongFlow", "v1")
            .Init()
            .WaitLong("LongEvt", state => state.Id)
            .End()
            .Build();
        var shortDefinition = new DurableWorkflowBuilder<OrderState>("ShortFlow", "v1")
            .Init()
            .Wait("ShortEvt", state => state.Id)
            .End()
            .Build();

        await using var engine = DurableWorkflowEngine.Create(store);

        var longStarted = await (await engine.ForDefinitionAsync(longDefinition)).Start(new OrderState { Id = "long-1" });
        var shortStarted = await (await engine.ForDefinitionAsync(shortDefinition)).Start(new OrderState { Id = "short-1" });

        Assert.False(engine.IsInstanceLoaded(longStarted.InstanceId));
        Assert.True(engine.IsInstanceLoaded(shortStarted.InstanceId));

        var loadedSnapshot = await engine.Instance(longStarted.InstanceId).GetAsync();
        Assert.True(engine.IsInstanceLoaded(longStarted.InstanceId));
        Assert.Equal(WorkflowStatus.Waiting, loadedSnapshot.Status);
        Assert.Equal(1, loadedSnapshot.ActiveWaitCount);
    }

    [Fact]
    public async Task Loaded_waitlong_can_be_evicted_again_after_buffering_and_reloaded_for_resume()
    {
        var store = new InMemoryWorkflowStore();
        var definition = new DurableWorkflowBuilder<OrderState>("ReloadableLongFlow", "v1")
            .Init()
            .WaitLong("Approval", state => state.Id)
            .Then<CapturePayloadStep>()
            .End()
            .Build();

        await using var engine = DurableWorkflowEngine.Create(store);
        var typed = await engine.ForDefinitionAsync(definition);
        var started = await typed.Start(new OrderState());

        await engine.Instance(started.InstanceId).GetAsync();
        Assert.True(engine.IsInstanceLoaded(started.InstanceId));

        await engine.Instance(started.InstanceId).RaiseEvent(
            new EventEnvelope("Unexpected", "order-1", "buffered", "evt-buffered"));

        Assert.False(engine.IsInstanceLoaded(started.InstanceId));

        var reloaded = await engine.Instance(started.InstanceId).GetAsync();
        Assert.True(engine.IsInstanceLoaded(started.InstanceId));
        Assert.Equal(WorkflowStatus.Waiting, reloaded.Status);

        await engine.Instance(started.InstanceId).RaiseEvent(
            new EventEnvelope("Approval", "order-1", "approved", "evt-approved"));

        var completed = await engine.Instance(started.InstanceId).GetAsync();
        Assert.Equal(WorkflowStatus.Completed, completed.Status);
    }

    [Fact]
    public async Task Unmatched_event_is_buffered_and_checkpointed()
    {
        var store = new InMemoryWorkflowStore();
        var definition = new DurableWorkflowBuilder<OrderState>("BufferedEventFlow", "v1")
            .Init()
            .WaitLong("Expected", state => state.Id)
            .End()
            .Build();

        await using var engine = DurableWorkflowEngine.Create(store);
        var typed = await engine.ForDefinitionAsync(definition);

        var started = await typed.Start(new OrderState());
        await engine.Instance(started.InstanceId).RaiseEvent(new EventEnvelope("Unexpected", "order-1", "payload", "evt-buffered"));

        var persisted = await store.LoadAsync(started.InstanceId, CancellationToken.None);
        Assert.NotNull(persisted);
        Assert.Equal(1, persisted!.ConcurrencyToken);
        Assert.Single(persisted.RuntimeState.PendingEvents);
        Assert.Equal("Unexpected", persisted.RuntimeState.PendingEvents[0].Envelope.EventName);
        Assert.Equal(WaitMode.Cold, persisted.RuntimeState.ActiveWaits[0].Mode);
        Assert.False(engine.IsInstanceLoaded(started.InstanceId));
    }

    [Fact]
    public async Task Durable_engine_writes_inbox_outbox_and_history_records()
    {
        var store = new InMemoryWorkflowStore();
        var definition = new DurableWorkflowBuilder<OrderState>("ArtifactFlow", "v1")
            .Init()
            .WaitLong("Approval", state => state.Id)
            .Then<CapturePayloadStep>()
            .End()
            .Build();

        await using var engine = DurableWorkflowEngine.Create(store);
        var typed = await engine.ForDefinitionAsync(definition);

        var started = await typed.Start(new OrderState());
        await engine.Instance(started.InstanceId).RaiseEvent(new EventEnvelope("Approval", "order-1", "ok", "evt-1"));

        var inbox = store.GetInbox(started.InstanceId);
        var history = store.GetHistory(started.InstanceId);
        var pendingOutbox = await store.GetPendingOutboxAsync(CancellationToken.None);

        Assert.Contains(inbox, x => x.EventId == "evt-1" && x.Processed);
        Assert.Contains(history, x => x.TransitionType == "Started");
        Assert.Contains(history, x => x.TransitionType == nameof(WorkflowStatus.Waiting));
        Assert.Contains(history, x => x.TransitionType == "EventResumed");
        Assert.Contains(history, x => x.TransitionType == nameof(WorkflowStatus.Completed));
        Assert.Contains(pendingOutbox, x => x.InstanceId == started.InstanceId && x.EventName == "WorkflowWaiting");
        Assert.Contains(pendingOutbox, x => x.InstanceId == started.InstanceId && x.EventName == "WorkflowCompleted");
    }

    [Fact]
    public async Task Processed_inbox_record_deduplicates_duplicate_event_after_restart_even_without_consumed_set()
    {
        var store = new InMemoryWorkflowStore();
        var definition = new DurableWorkflowBuilder<OrderState>("InboxDedupFlow", "v1")
            .Init()
            .WaitLong("Approval", state => state.Id)
            .Then<CapturePayloadStep>()
            .End()
            .Build();

        var engine1 = DurableWorkflowEngine.Create(store);
        var started = await (await engine1.ForDefinitionAsync(definition)).Start(new OrderState());
        await engine1.Instance(started.InstanceId).RaiseEvent(new EventEnvelope("Approval", "order-1", "ok", "evt-1"));
        await engine1.DisposeAsync();

        var persisted = await store.LoadAsync(started.InstanceId, CancellationToken.None);
        Assert.NotNull(persisted);

        await store.CommitTransitionAsync(
            persisted! with
            {
                RuntimeState = persisted.RuntimeState with
                {
                    ConsumedEventIds = Array.Empty<string>()
                }
            },
            inboxRecords: [],
            processedInboxEventIds: [],
            outboxRecords: [],
            historyRecords: [],
            CancellationToken.None);

        var engine2 = DurableWorkflowEngine.Create(store);
        await engine2.ForDefinitionAsync(definition);

        await engine2.Instance(started.InstanceId).RaiseEvent(new EventEnvelope("Approval", "order-1", "ok-duplicate", "evt-1"));

        var final = await engine2.Instance(started.InstanceId).GetAsync();
        var state = await engine2.Instance(started.InstanceId).GetStateAsync<OrderState>();

        Assert.Equal(WorkflowStatus.Completed, final.Status);
        Assert.Equal("ok", state.Result);
    }

    [Fact]
    public async Task Buffered_inbox_record_deduplicates_duplicate_unmatched_event_after_restart_even_without_pending_events()
    {
        var store = new InMemoryWorkflowStore();
        var definition = new DurableWorkflowBuilder<OrderState>("BufferedInboxDedupFlow", "v1")
            .Init()
            .WaitLong("Expected", state => state.Id)
            .End()
            .Build();

        var engine1 = DurableWorkflowEngine.Create(store);
        var started = await (await engine1.ForDefinitionAsync(definition)).Start(new OrderState());
        await engine1.Instance(started.InstanceId).RaiseEvent(new EventEnvelope("Unexpected", "order-1", "payload", "evt-buffered"));
        await engine1.DisposeAsync();

        var persisted = await store.LoadAsync(started.InstanceId, CancellationToken.None);
        Assert.NotNull(persisted);

        await store.CommitTransitionAsync(
            persisted! with
            {
                RuntimeState = persisted.RuntimeState with
                {
                    PendingEvents = Array.Empty<PersistedPendingEvent>(),
                    ConsumedEventIds = Array.Empty<string>()
                }
            },
            inboxRecords: [],
            processedInboxEventIds: [],
            outboxRecords: [],
            historyRecords: [],
            CancellationToken.None);

        var engine2 = DurableWorkflowEngine.Create(store);
        await engine2.ForDefinitionAsync(definition);

        await engine2.Instance(started.InstanceId).RaiseEvent(new EventEnvelope("Unexpected", "order-1", "payload-duplicate", "evt-buffered"));

        var inbox = store.GetInbox(started.InstanceId);
        Assert.Single(inbox, x => x.EventId == "evt-buffered");
    }

    [Fact]
    public async Task Buffered_event_consumption_marks_existing_inbox_record_processed_for_restart_dedup()
    {
        var store = new InMemoryWorkflowStore();
        var definition = new DurableWorkflowBuilder<OrderState>("BufferedPromotionFlow", "v1")
            .Init()
            .WaitLong("First", state => state.Id)
            .WaitLong("Second", state => state.Id)
            .Then<CapturePayloadStep>()
            .End()
            .Build();

        var engine1 = DurableWorkflowEngine.Create(store);
        var started = await (await engine1.ForDefinitionAsync(definition)).Start(new OrderState());
        await engine1.Instance(started.InstanceId).RaiseEvent(new EventEnvelope("Second", "order-1", "buffered-payload", "evt-second"));
        await engine1.DisposeAsync();

        var engine2 = DurableWorkflowEngine.Create(store);
        await engine2.ForDefinitionAsync(definition);
        await engine2.Instance(started.InstanceId).RaiseEvent(new EventEnvelope("First", "order-1", null, "evt-first"));
        await engine2.DisposeAsync();

        var inboxAfterResume = store.GetInbox(started.InstanceId);
        Assert.Contains(inboxAfterResume, x => x.EventId == "evt-second" && x.Processed);

        var persisted = await store.LoadAsync(started.InstanceId, CancellationToken.None);
        Assert.NotNull(persisted);

        await store.CommitTransitionAsync(
            persisted! with
            {
                RuntimeState = persisted.RuntimeState with
                {
                    PendingEvents = Array.Empty<PersistedPendingEvent>(),
                    ConsumedEventIds = Array.Empty<string>()
                }
            },
            inboxRecords: [],
            processedInboxEventIds: [],
            outboxRecords: [],
            historyRecords: [],
            CancellationToken.None);

        var engine3 = DurableWorkflowEngine.Create(store);
        await engine3.ForDefinitionAsync(definition);
        await engine3.Instance(started.InstanceId).RaiseEvent(new EventEnvelope("Second", "order-1", "duplicate", "evt-second"));

        var final = await engine3.Instance(started.InstanceId).GetAsync();
        var state = await engine3.Instance(started.InstanceId).GetStateAsync<OrderState>();

        Assert.Equal(WorkflowStatus.Completed, final.Status);
        Assert.Equal("buffered-payload", state.Result);
    }

    [Fact]
    public async Task Durable_state_snapshot_is_detached_from_live_instance()
    {
        var store = new InMemoryWorkflowStore();
        var definition = new DurableWorkflowBuilder<OrderState>("StateSnapshotFlow", "v1")
            .Init()
            .WaitLong("Approval", state => state.Id)
            .End()
            .Build();

        await using var engine = DurableWorkflowEngine.Create(store);
        var started = await (await engine.ForDefinitionAsync(definition)).Start(new OrderState { Result = "original" });

        var first = await engine.Instance(started.InstanceId).GetStateAsync<OrderState>();
        first.Result = "mutated";

        var second = await engine.Instance(started.InstanceId).GetStateAsync<OrderState>();

        Assert.Equal("original", second.Result);
    }

    [Fact]
    public async Task Durable_resumed_step_context_exposes_definition_node_path_and_resume_flag()
    {
        var store = new InMemoryWorkflowStore();
        var definition = new DurableWorkflowBuilder<OrderState>("ContextFlow", "v1")
            .Init()
            .WaitLong("Approval", state => state.Id)
            .Then<CaptureContextStep>()
            .End()
            .Build();

        await using var engine = DurableWorkflowEngine.Create(store);
        var started = await (await engine.ForDefinitionAsync(definition)).Start(new OrderState());

        await engine.Instance(started.InstanceId).RaiseEvent(
            new EventEnvelope("Approval", "order-1", null, "evt-ctx"));

        var state = await engine.Instance(started.InstanceId).GetStateAsync<OrderState>();

        Assert.Equal("ContextFlow", state.LastDefinitionId);
        Assert.Equal("1", state.LastNodePath);
        Assert.True(state.SawResumedContext);
    }

    [Fact]
    public async Task Commit_failure_during_resume_does_not_leave_instance_stuck_in_mutated_memory_state()
    {
        var innerStore = new InMemoryWorkflowStore();
        var store = new FailingCommitStore(innerStore);
        var definition = new DurableWorkflowBuilder<OrderState>("CommitRecoveryFlow", "v1")
            .Init()
            .WaitLong("Approval", state => state.Id)
            .Then<CapturePayloadStep>()
            .End()
            .Build();

        var engine1 = DurableWorkflowEngine.Create(store);
        var started = await (await engine1.ForDefinitionAsync(definition)).Start(new OrderState());
        await engine1.DisposeAsync();

        await using var engine2 = DurableWorkflowEngine.Create(store);
        await engine2.ForDefinitionAsync(definition);

        await Assert.ThrowsAsync<ConcurrencyException>(() =>
            engine2.RaiseEvent(new EventEnvelope("Approval", "order-1", "approved", "evt-1")));

        var persistedAfterFailure = await innerStore.LoadAsync(started.InstanceId, CancellationToken.None);
        Assert.NotNull(persistedAfterFailure);
        Assert.Equal(WorkflowStatus.Waiting, persistedAfterFailure!.RuntimeState.Status);

        await engine2.RaiseEvent(new EventEnvelope("Approval", "order-1", "approved", "evt-1"));

        var persistedAfterRetry = await innerStore.LoadAsync(started.InstanceId, CancellationToken.None);

        Assert.NotNull(persistedAfterRetry);
        Assert.Equal(WorkflowStatus.Completed, persistedAfterRetry!.RuntimeState.Status);
    }

    [Fact]
    public async Task Commit_failure_during_buffering_requires_retry_to_durably_accept_event()
    {
        var innerStore = new InMemoryWorkflowStore();
        var store = new FailingCommitStore(innerStore, failNextProcessedCommit: false, failNextBufferedCommit: true);
        var definition = new DurableWorkflowBuilder<OrderState>("BufferedCommitRecoveryFlow", "v1")
            .Init()
            .WaitLong("Approval", state => state.Id)
            .Then<CapturePayloadStep>()
            .End()
            .Build();

        var engine1 = DurableWorkflowEngine.Create(store);
        var started = await (await engine1.ForDefinitionAsync(definition)).Start(new OrderState());
        await engine1.DisposeAsync();

        await using var engine2 = DurableWorkflowEngine.Create(store);
        await engine2.ForDefinitionAsync(definition);

        await Assert.ThrowsAsync<ConcurrencyException>(() =>
            engine2.Instance(started.InstanceId).RaiseEvent(new EventEnvelope("Unexpected", "order-1", "payload", "evt-buffered")));

        var persistedAfterFailure = await innerStore.LoadAsync(started.InstanceId, CancellationToken.None);
        Assert.NotNull(persistedAfterFailure);
        Assert.Empty(persistedAfterFailure!.RuntimeState.PendingEvents);

        await engine2.Instance(started.InstanceId).RaiseEvent(new EventEnvelope("Unexpected", "order-1", "payload", "evt-buffered"));

        var persistedAfterRetry = await innerStore.LoadAsync(started.InstanceId, CancellationToken.None);
        Assert.NotNull(persistedAfterRetry);
        Assert.Single(persistedAfterRetry!.RuntimeState.PendingEvents);
        Assert.Equal("evt-buffered", persistedAfterRetry.RuntimeState.PendingEvents[0].Envelope.EventId);
    }

    [Fact]
    public async Task Buffered_event_persists_declared_payload_type_through_router()
    {
        var store = new InMemoryWorkflowStore();
        var registry = DurablePayloadTypeRegistry.Default
            .Register<BasePayload>("base", "schema-base")
            .Register<DerivedPayload>("derived", "schema-derived");
        var serializer = new JsonPayloadEnvelopeSerializer(registry);
        var definition = new DurableWorkflowBuilder<OrderState>("DeclaredPayloadBufferFlow", "v1")
            .Init()
            .WaitLong("Expected", state => state.Id)
            .End()
            .Build();

        await using var engine = DurableWorkflowEngine.Create(
            store,
            new DurableWorkflowEngineOptions
            {
                PayloadSchemaResolver = registry,
                PayloadEnvelopeSerializer = serializer
            });
        var started = await (await engine.ForDefinitionAsync(definition)).Start(new OrderState());

        await engine.Instance(started.InstanceId).RaiseEvent(
            new EventEnvelope(
                "Unexpected",
                "order-1",
                new DerivedPayload("payload", "extra"),
                "evt-buffered",
                typeof(BasePayload)));

        var persisted = await store.LoadAsync(started.InstanceId, CancellationToken.None);

        Assert.NotNull(persisted);
        Assert.Equal("base", persisted!.RuntimeState.PendingEvents[0].Envelope.PayloadTypeKey);
        Assert.Equal("base", store.GetInbox(started.InstanceId)[0].Envelope.PayloadTypeKey);
    }

    [Fact]
    public async Task Durable_engine_uses_store_returned_concurrency_token_for_follow_up_commits()
    {
        var store = new InMemoryWorkflowStore(concurrencyIncrement: 5);
        var definition = new DurableWorkflowBuilder<OrderState>("AuthoritativeTokenFlow", "v1")
            .Init()
            .Wait("Approval", state => state.Id)
            .Then<CapturePayloadStep>()
            .End()
            .Build();

        await using var engine = DurableWorkflowEngine.Create(store);
        var started = await (await engine.ForDefinitionAsync(definition)).Start(new OrderState());

        await engine.Instance(started.InstanceId).RaiseEvent(
            new EventEnvelope("Unexpected", "order-1", "buffered", "evt-buffered"));

        var buffered = await engine.Instance(started.InstanceId).GetAsync();
        Assert.Equal(5, buffered.ConcurrencyToken);

        await engine.Instance(started.InstanceId).RaiseEvent(
            new EventEnvelope("Approval", "order-1", "approved", "evt-approved"));

        var completed = await engine.Instance(started.InstanceId).GetAsync();
        var persisted = await store.LoadAsync(started.InstanceId, CancellationToken.None);

        Assert.Equal(10, completed.ConcurrencyToken);
        Assert.NotNull(persisted);
        Assert.Equal(10, persisted!.ConcurrencyToken);
        Assert.Equal(WorkflowStatus.Completed, persisted.RuntimeState.Status);
    }

    [Fact]
    public async Task Start_uses_store_returned_concurrency_token_and_authoritative_initial_outbox_version()
    {
        var innerStore = new InMemoryWorkflowStore(concurrencyIncrement: 5);
        var store = new AuthoritativeCreateTokenStore(innerStore, initialConcurrencyToken: 5);
        var definition = new DurableWorkflowBuilder<OrderState>("AuthoritativeCreateFlow", "v1")
            .Init()
            .Wait("Approval", state => state.Id)
            .Then<CapturePayloadStep>()
            .End()
            .Build();

        await using var engine = DurableWorkflowEngine.Create(store);
        var started = await (await engine.ForDefinitionAsync(definition)).Start(new OrderState());

        var pendingOutbox = await innerStore.GetPendingOutboxAsync(CancellationToken.None);
        var waitingOutbox = Assert.Single(
            pendingOutbox,
            record => record.InstanceId == started.InstanceId && record.EventName == "WorkflowWaiting");

        Assert.Equal(5, started.ConcurrencyToken);
        Assert.Equal(5, waitingOutbox.StreamVersion);
        Assert.Equal($"{started.InstanceId}:5:0", waitingOutbox.OutboxId);

        await engine.Instance(started.InstanceId).RaiseEvent(
            new EventEnvelope("Approval", "order-1", "approved", "evt-approved"));

        var completed = await engine.Instance(started.InstanceId).GetAsync();
        Assert.Equal(10, completed.ConcurrencyToken);
    }

    [Fact]
    public async Task Resident_wait_resume_commit_failure_restores_in_memory_state_without_store_reload()
    {
        var innerStore = new InMemoryWorkflowStore();
        var store = new FailingCommitWithoutReloadStore(innerStore);
        var definition = new DurableWorkflowBuilder<OrderState>("ResidentRollbackFlow", "v1")
            .Init()
            .Wait("Approval", state => state.Id)
            .Then<CapturePayloadStep>()
            .End()
            .Build();

        await using var engine = DurableWorkflowEngine.Create(store);
        var started = await (await engine.ForDefinitionAsync(definition)).Start(new OrderState());

        await Assert.ThrowsAsync<ConcurrencyException>(() =>
            engine.RaiseEvent(new EventEnvelope("Approval", "order-1", "approved", "evt-1")));

        var afterFailure = await engine.Instance(started.InstanceId).GetAsync();
        Assert.Equal(WorkflowStatus.Waiting, afterFailure.Status);
        Assert.Equal(1, afterFailure.ActiveWaitCount);

        await engine.RaiseEvent(new EventEnvelope("Approval", "order-1", "approved", "evt-1"));

        var final = await engine.Instance(started.InstanceId).GetAsync();
        var persisted = await innerStore.LoadAsync(started.InstanceId, CancellationToken.None);

        Assert.Equal(WorkflowStatus.Completed, final.Status);
        Assert.NotNull(persisted);
        Assert.Equal(WorkflowStatus.Completed, persisted!.RuntimeState.Status);
    }

    [Fact]
    public async Task Pending_outbox_records_are_dispatched_after_restart_and_marked_dispatched()
    {
        var store = new InMemoryWorkflowStore();
        var definition = new DurableWorkflowBuilder<OrderState>("OutboxFlow", "v1")
            .Init()
            .WaitLong("Approval", state => state.Id)
            .End()
            .Build();

        var engine1 = DurableWorkflowEngine.Create(store);
        var started = await (await engine1.ForDefinitionAsync(definition)).Start(new OrderState());
        await engine1.DisposeAsync();

        var before = await store.GetPendingOutboxAsync(CancellationToken.None);
        Assert.Contains(before, x => x.InstanceId == started.InstanceId && x.EventName == "WorkflowWaiting");

        var engine2 = DurableWorkflowEngine.Create(store);
        var delivered = new List<string>();

        var result = await engine2.DispatchPendingOutboxAsync(
            new CallbackOutboxDispatcher(record => delivered.Add(record.EventName)));

        var after = await store.GetPendingOutboxAsync(CancellationToken.None);

        Assert.Equal(before.Count, result.SucceededCount);
        Assert.Equal(before.Count, result.AttemptedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Contains("WorkflowWaiting", delivered);
        Assert.Empty(after);
    }

    [Fact]
    public async Task Failed_outbox_dispatch_reports_failure_and_leaves_record_pending()
    {
        var store = new InMemoryWorkflowStore();
        var definition = new DurableWorkflowBuilder<OrderState>("OutboxFailureFlow", "v1")
            .Init()
            .WaitLong("Approval", state => state.Id)
            .End()
            .Build();

        await using var engine = DurableWorkflowEngine.Create(store);
        var started = await (await engine.ForDefinitionAsync(definition)).Start(new OrderState());

        var result = await engine.DispatchPendingOutboxAsync(
            new FailingOutboxDispatcher(record => record.InstanceId == started.InstanceId));

        var pending = await store.GetPendingOutboxAsync(CancellationToken.None);
        Assert.Equal(1, result.AttemptedCount);
        Assert.Equal(0, result.SucceededCount);
        Assert.Equal(1, result.FailedCount);
        Assert.False(result.Results[0].Succeeded);
        Assert.IsType<InvalidOperationException>(result.Results[0].Error);
        Assert.Contains(pending, x => x.InstanceId == started.InstanceId && x.EventName == "WorkflowWaiting");
    }

    [Fact]
    public async Task Manual_outbox_dispatch_continues_after_individual_failure_and_reports_all_results()
    {
        var store = new InMemoryWorkflowStore();
        var definition = new DurableWorkflowBuilder<OrderState>("OutboxBatchFlow", "v1")
            .Init()
            .WaitLong("Approval", state => state.Id)
            .End()
            .Build();

        var engine1 = DurableWorkflowEngine.Create(store);
        var first = await (await engine1.ForDefinitionAsync(definition)).Start(new OrderState { Id = "order-1" });
        var second = await (await engine1.ForDefinitionAsync(definition)).Start(new OrderState { Id = "order-2" });
        await engine1.DisposeAsync();

        await using var engine2 = DurableWorkflowEngine.Create(store);
        var failedOutboxId = (await store.GetPendingOutboxAsync(CancellationToken.None))
            .First(x => x.InstanceId == first.InstanceId)
            .OutboxId;

        var result = await engine2.DispatchPendingOutboxAsync(
            new FailingOutboxDispatcher(record => record.OutboxId == failedOutboxId));

        var pending = await store.GetPendingOutboxAsync(CancellationToken.None);

        Assert.Equal(2, result.AttemptedCount);
        Assert.Equal(1, result.SucceededCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Contains(result.Results, x => x.Record.OutboxId == failedOutboxId && !x.Succeeded);
        Assert.Contains(result.Results, x => x.Record.InstanceId == second.InstanceId && x.Succeeded);
        Assert.Single(pending, x => x.OutboxId == failedOutboxId);
    }

    [Fact]
    public async Task Observer_failure_on_dispatch_success_does_not_change_manual_dispatch_outcome()
    {
        var store = new InMemoryWorkflowStore();
        var definition = new DurableWorkflowBuilder<OrderState>("OutboxObserverSuccessFlow", "v1")
            .Init()
            .WaitLong("Approval", state => state.Id)
            .End()
            .Build();

        var engine1 = DurableWorkflowEngine.Create(store);
        await (await engine1.ForDefinitionAsync(definition)).Start(new OrderState());
        await engine1.DisposeAsync();

        await using var engine2 = DurableWorkflowEngine.Create(
            store,
            new DurableWorkflowEngineOptions
            {
                AutoDispatchOutbox = false,
                OutboxPumpObserver = new ThrowingOutboxPumpObserver(throwOnDispatchSucceeded: true)
            });

        var result = await engine2.DispatchPendingOutboxAsync(new CallbackOutboxDispatcher(_ => { }));
        var pending = await store.GetPendingOutboxAsync(CancellationToken.None);

        Assert.True(result.SucceededCount > 0);
        Assert.Empty(pending);
    }

    [Fact]
    public async Task Automatic_outbox_dispatch_runs_on_restart_when_dispatcher_is_configured()
    {
        var store = new InMemoryWorkflowStore();
        var definition = new DurableWorkflowBuilder<OrderState>("AutoOutboxFlow", "v1")
            .Init()
            .WaitLong("Approval", state => state.Id)
            .End()
            .Build();

        var engine1 = DurableWorkflowEngine.Create(store);
        var started = await (await engine1.ForDefinitionAsync(definition)).Start(new OrderState());
        await engine1.DisposeAsync();

        var delivered = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var engine2 = DurableWorkflowEngine.Create(
            store,
            new DurableWorkflowEngineOptions
            {
                OutboxDispatcher = new CallbackOutboxDispatcher(
                    record => delivered.TrySetResult(record.EventName)),
                OutboxDispatchPollingInterval = TimeSpan.FromMilliseconds(25)
            });

        var eventName = await delivered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var pending = await store.GetPendingOutboxAsync(CancellationToken.None);

        Assert.Equal("WorkflowWaiting", eventName);
        Assert.Empty(pending);
    }

    [Fact]
    public async Task Automatic_outbox_dispatch_can_be_disabled()
    {
        var store = new InMemoryWorkflowStore();
        var definition = new DurableWorkflowBuilder<OrderState>("ManualOutboxFlow", "v1")
            .Init()
            .WaitLong("Approval", state => state.Id)
            .End()
            .Build();

        var engine1 = DurableWorkflowEngine.Create(store);
        await (await engine1.ForDefinitionAsync(definition)).Start(new OrderState());
        await engine1.DisposeAsync();

        var delivered = false;
        await using var engine2 = DurableWorkflowEngine.Create(
            store,
            new DurableWorkflowEngineOptions
            {
                AutoDispatchOutbox = false,
                OutboxDispatcher = new CallbackOutboxDispatcher(_ => delivered = true),
                OutboxDispatchPollingInterval = TimeSpan.FromMilliseconds(25)
            });

        await Task.Delay(150);

        var pending = await store.GetPendingOutboxAsync(CancellationToken.None);
        Assert.False(delivered);
        Assert.NotEmpty(pending);
    }

    [Fact]
    public async Task Automatic_outbox_dispatch_observes_failures_and_applies_configured_backoff()
    {
        var store = new InMemoryWorkflowStore();
        var definition = new DurableWorkflowBuilder<OrderState>("ObservedOutboxFlow", "v1")
            .Init()
            .WaitLong("Approval", state => state.Id)
            .End()
            .Build();

        var engine1 = DurableWorkflowEngine.Create(store);
        await (await engine1.ForDefinitionAsync(definition)).Start(new OrderState());
        await engine1.DisposeAsync();

        var observer = new RecordingOutboxPumpObserver();
        var delayStrategy = new RecordingDelayStrategy(context =>
            context.ConsecutiveFailures > 0
                ? TimeSpan.FromMilliseconds(10)
                : TimeSpan.FromMilliseconds(5));

        await using var engine2 = DurableWorkflowEngine.Create(
            store,
            new DurableWorkflowEngineOptions
            {
                OutboxDispatcher = new SequenceOutboxDispatcher(true, false),
                OutboxDispatchPollingInterval = TimeSpan.FromMilliseconds(5),
                OutboxPumpDelayStrategy = delayStrategy,
                OutboxPumpObserver = observer
            });

        await AssertEventuallyAsync(
            async () => store.GetOutboxRecords().All(record =>
                record.Status is OutboxStatus.Dispatched or OutboxStatus.Poisoned),
            TimeSpan.FromSeconds(2));

        Assert.Contains(observer.FailedEvents, x => x == "WorkflowWaiting");
        Assert.Contains(observer.SucceededEvents, x => x == "WorkflowWaiting");
        Assert.Contains(observer.Cycles, x => x.Failure is not null && x.ConsecutiveFailures == 1 && x.NextDelay == TimeSpan.FromMilliseconds(10));
        Assert.Contains(observer.Cycles, x => x.Failure is null && x.ConsecutiveFailures == 0 && x.NextDelay == TimeSpan.FromMilliseconds(5));
        Assert.Contains(delayStrategy.Contexts, x => x.ConsecutiveFailures == 1 && x.LastError is not null);
    }

    [Fact]
    public async Task Observer_failure_on_cycle_completion_does_not_break_automatic_outbox_dispatch()
    {
        var store = new InMemoryWorkflowStore();
        var definition = new DurableWorkflowBuilder<OrderState>("OutboxObserverCycleFlow", "v1")
            .Init()
            .WaitLong("Approval", state => state.Id)
            .End()
            .Build();

        var engine1 = DurableWorkflowEngine.Create(store);
        await (await engine1.ForDefinitionAsync(definition)).Start(new OrderState());
        await engine1.DisposeAsync();

        await using var engine2 = DurableWorkflowEngine.Create(
            store,
            new DurableWorkflowEngineOptions
            {
                OutboxDispatcher = new CallbackOutboxDispatcher(_ => { }),
                OutboxPumpObserver = new ThrowingOutboxPumpObserver(throwOnCycleCompleted: true),
                OutboxDispatchPollingInterval = TimeSpan.FromMilliseconds(25)
            });

        await AssertEventuallyAsync(
            async () => (await store.GetPendingOutboxAsync(CancellationToken.None)).Count == 0,
            TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task Manual_outbox_dispatch_poison_marks_record_after_max_attempts_and_stops_retrying()
    {
        var store = new InMemoryWorkflowStore();
        var definition = new DurableWorkflowBuilder<OrderState>("PoisonOutboxFlow", "v1")
            .Init()
            .WaitLong("Approval", state => state.Id)
            .End()
            .Build();

        await using var engine = DurableWorkflowEngine.Create(
            store,
            new DurableWorkflowEngineOptions
            {
                AutoDispatchOutbox = false,
                OutboxDispatchPollingInterval = TimeSpan.FromMilliseconds(10),
                MaxOutboxDispatchAttempts = 2
            });
        await (await engine.ForDefinitionAsync(definition)).Start(new OrderState());

        var first = await engine.DispatchPendingOutboxAsync(new FailingOutboxDispatcher(_ => true));
        await Task.Delay(TimeSpan.FromMilliseconds(25));
        var second = await engine.DispatchPendingOutboxAsync(new FailingOutboxDispatcher(_ => true));
        var pendingAfterPoison = await store.GetPendingOutboxAsync(CancellationToken.None);
        var poisoned = store.GetOutbox(first.Results[0].Record.OutboxId);

        Assert.Equal(1, first.FailedCount);
        Assert.Equal(1, second.FailedCount);
        Assert.NotNull(poisoned);
        Assert.Equal(2, poisoned!.FailureCount);
        Assert.True(poisoned.Poisoned);
        Assert.Empty(pendingAfterPoison);
    }

    [Fact]
    public async Task Automatic_outbox_dispatch_invokes_poison_handler_when_record_crosses_threshold()
    {
        var store = new InMemoryWorkflowStore();
        var definition = new DurableWorkflowBuilder<OrderState>("PoisonHandlerOutboxFlow", "v1")
            .Init()
            .WaitLong("Approval", state => state.Id)
            .End()
            .Build();

        var poisonHandler = new RecordingOutboxPoisonHandler();

        var engine1 = DurableWorkflowEngine.Create(store);
        await (await engine1.ForDefinitionAsync(definition)).Start(new OrderState());
        await engine1.DisposeAsync();

        await using var engine2 = DurableWorkflowEngine.Create(
            store,
            new DurableWorkflowEngineOptions
            {
                OutboxDispatcher = new FailingOutboxDispatcher(_ => true),
                OutboxDispatchPollingInterval = TimeSpan.FromMilliseconds(10),
                MaxOutboxDispatchAttempts = 2,
                OutboxPoisonHandler = poisonHandler
            });

        await AssertEventuallyAsync(
            async () => poisonHandler.PoisonedOutboxIds.Count == 1,
            TimeSpan.FromSeconds(2));

        var pending = await store.GetPendingOutboxAsync(CancellationToken.None);
        var outboxId = poisonHandler.PoisonedOutboxIds[0];
        var poisoned = store.GetOutbox(outboxId);

        Assert.Empty(pending);
        Assert.NotNull(poisoned);
        Assert.True(poisoned!.Poisoned);
        Assert.Equal(2, poisoned.FailureCount);
    }

    [Fact]
    public async Task DisposeAsync_called_twice_does_not_throw()
    {
        var store = new InMemoryWorkflowStore();
        await using var engine = DurableWorkflowEngine.Create(store);

        await engine.DisposeAsync();
        await engine.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_called_twice_with_loaded_resident_instance_does_not_throw()
    {
        var store = new InMemoryWorkflowStore();
        var definition = new DurableWorkflowBuilder<OrderState>("DisposeResidentFlow", "v1")
            .Init()
            .Wait("Approval", state => state.Id)
            .End()
            .Build();

        var engine = DurableWorkflowEngine.Create(store);
        await (await engine.ForDefinitionAsync(definition)).Start(new OrderState());

        await engine.DisposeAsync();
        await engine.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_during_active_outbox_pump_completes_cleanly()
    {
        var store = new InMemoryWorkflowStore();
        var definition = new DurableWorkflowBuilder<OrderState>("DisposeOutboxFlow", "v1")
            .Init()
            .WaitLong("Approval", state => state.Id)
            .End()
            .Build();

        var engine1 = DurableWorkflowEngine.Create(store);
        await (await engine1.ForDefinitionAsync(definition)).Start(new OrderState());
        await engine1.DisposeAsync();

        var dispatcher = new BlockingOutboxDispatcher();
        var engine2 = DurableWorkflowEngine.Create(
            store,
            new DurableWorkflowEngineOptions
            {
                OutboxDispatcher = dispatcher,
                OutboxDispatchPollingInterval = TimeSpan.FromMilliseconds(10)
            });

        await dispatcher.Entered.WaitAsync(TimeSpan.FromSeconds(2));
        await engine2.DisposeAsync();
    }

    [Fact]
    public async Task DeleteAsync_removes_loaded_instance_from_store_and_hot_runtime()
    {
        var store = new InMemoryWorkflowStore();
        var definition = new DurableWorkflowBuilder<OrderState>("DeleteFlow", "v1")
            .Init()
            .Wait("Approval", state => state.Id)
            .End()
            .Build();

        await using var engine = DurableWorkflowEngine.Create(store);
        var started = await (await engine.ForDefinitionAsync(definition)).Start(new OrderState());

        Assert.True(engine.IsInstanceLoaded(started.InstanceId));

        await engine.Instance(started.InstanceId).DeleteAsync();

        Assert.False(engine.IsInstanceLoaded(started.InstanceId));
        Assert.Null(await store.LoadAsync(started.InstanceId, CancellationToken.None));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => engine.Instance(started.InstanceId).GetAsync());
        await Assert.ThrowsAsync<NoActiveWaitException>(() =>
            engine.RaiseEvent(new EventEnvelope("Approval", "order-1", null, "evt-deleted")));
    }

    [Fact]
    public async Task Concurrent_events_against_same_cold_instance_share_a_single_load_and_do_not_drop_resume()
    {
        var innerStore = new InMemoryWorkflowStore();
        var definition = new DurableWorkflowBuilder<OrderState>("ConcurrentColdLoadFlow", "v1")
            .Init()
            .WaitLong("Approval", state => state.Id)
            .Then<CapturePayloadStep>()
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
    public async Task DeleteAsync_waits_for_inflight_resume_without_disposing_lock_under_the_event_path()
    {
        var store = new InMemoryWorkflowStore();
        var definition = new DurableWorkflowBuilder<OrderState>("DeleteDuringResumeFlow", "v1")
            .Init()
            .Wait("Approval", state => state.Id)
            .Then<BlockingResumeStep>()
            .End()
            .Build();

        BlockingResumeStep.Reset();

        await using var engine = DurableWorkflowEngine.Create(store);
        var started = await (await engine.ForDefinitionAsync(definition)).Start(new OrderState());

        var raiseTask = engine.Instance(started.InstanceId).RaiseEvent(
            new EventEnvelope("Approval", "order-1", null, "evt-delete-race"));

        await BlockingResumeStep.Entered.WaitAsync(TimeSpan.FromSeconds(2));

        var deleteTask = engine.Instance(started.InstanceId).DeleteAsync();
        BlockingResumeStep.AllowContinue();

        await raiseTask;
        await deleteTask;

        Assert.Null(await store.LoadAsync(started.InstanceId, CancellationToken.None));
        Assert.False(engine.IsInstanceLoaded(started.InstanceId));
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

    [Fact]
    public async Task PurgeArtifactsAsync_removes_old_processed_artifacts_for_instance_scope()
    {
        var store = new InMemoryWorkflowStore();
        var definition = new DurableWorkflowBuilder<OrderState>("PurgeFlow", "v1")
            .Init()
            .Wait("Approval", state => state.Id)
            .End()
            .Build();

        await using var engine = DurableWorkflowEngine.Create(store);
        var started = await (await engine.ForDefinitionAsync(definition)).Start(new OrderState());
        var loaded = await store.LoadAsync(started.InstanceId, CancellationToken.None);
        Assert.NotNull(loaded);

        await store.CommitTransitionAsync(
            loaded!,
            inboxRecords:
            [
                new InboxRecord(
                    "evt-old-processed",
                    started.InstanceId,
                    new PersistedEventEnvelope("Approval", "order-1", null, null, "evt-old-processed"),
                    DateTimeOffset.UtcNow.AddHours(-2),
                    Processed: true)
            ],
            processedInboxEventIds: [],
            outboxRecords:
            [
                new OutboxRecord(
                    "out-old-dispatched",
                    started.InstanceId,
                    "WorkflowWaiting",
                    Payload: null,
                    DateTimeOffset.UtcNow.AddHours(-2),
                    Dispatched: true)
            ],
            historyRecords:
            [
                new HistoryRecord("Old", DateTimeOffset.UtcNow.AddHours(-2), "old")
            ],
            CancellationToken.None);

        await engine.Instance(started.InstanceId).PurgeArtifactsAsync(
            DurableArtifactRetentionPolicy.Uniform(TimeSpan.FromHours(1)));

        Assert.Empty(store.GetInbox(started.InstanceId));
        Assert.DoesNotContain(store.GetHistory(started.InstanceId), x => x.TransitionType == "Old");
        Assert.NotEmpty(store.GetHistory(started.InstanceId));
        Assert.Null(store.GetOutbox("out-old-dispatched"));
    }

    [Fact]
    public async Task PurgeArtifactsAsync_with_policy_uses_independent_cutoffs()
    {
        var store = new InMemoryWorkflowStore();
        var definition = new DurableWorkflowBuilder<OrderState>("PolicyPurgeFlow", "v1")
            .Init()
            .Wait("Approval", state => state.Id)
            .End()
            .Build();

        await using var engine = DurableWorkflowEngine.Create(store);
        var started = await (await engine.ForDefinitionAsync(definition)).Start(new OrderState());
        var loaded = await store.LoadAsync(started.InstanceId, CancellationToken.None);
        Assert.NotNull(loaded);

        await store.CommitTransitionAsync(
            loaded!,
            inboxRecords:
            [
                new InboxRecord(
                    "evt-old-processed",
                    started.InstanceId,
                    new PersistedEventEnvelope("Approval", "order-1", null, null, "evt-old-processed"),
                    DateTimeOffset.UtcNow.AddHours(-3),
                    Processed: true)
            ],
            processedInboxEventIds: [],
            outboxRecords:
            [
                new OutboxRecord(
                    "out-dispatched-keep",
                    started.InstanceId,
                    "WorkflowWaiting",
                    Payload: null,
                    DateTimeOffset.UtcNow.AddHours(-3),
                    Dispatched: true)
            ],
            historyRecords:
            [
                new HistoryRecord("Old", DateTimeOffset.UtcNow.AddHours(-3), "old")
            ],
            CancellationToken.None);

        await engine.Instance(started.InstanceId).PurgeArtifactsAsync(
            new DurableArtifactRetentionPolicy(
                ProcessedInboxRetention: TimeSpan.FromHours(1),
                TerminalOutboxRetention: TimeSpan.FromHours(10),
                HistoryRetention: TimeSpan.FromHours(1)));

        Assert.Empty(store.GetInbox(started.InstanceId));
        Assert.DoesNotContain(store.GetHistory(started.InstanceId), x => x.TransitionType == "Old");
        Assert.NotNull(store.GetOutbox("out-dispatched-keep"));
    }

    [Fact]
    public async Task Durable_outbox_record_has_deterministic_id_format_and_correct_message_type()
    {
        var store = new InMemoryWorkflowStore();
        var definition = new DurableWorkflowBuilder<OrderState>("DeterministicIdFlow", "v1")
            .Init()
            .WaitLong("Approval", state => state.Id)
            .End()
            .Build();

        await using var engine = DurableWorkflowEngine.Create(store);
        var started = await (await engine.ForDefinitionAsync(definition)).Start(new OrderState());

        var outboxRecords = store.GetOutboxRecords(started.InstanceId);
        var waitingRecord = Assert.Single(outboxRecords, r => r.MessageType == "WorkflowWaiting");

        // Deterministic ID: {instanceId}:{streamVersion}:{sequence}
        // Start commits use streamVersion=0; resume commits use instance.ConcurrencyToken + 1
        Assert.Equal($"{started.InstanceId}:0:0", waitingRecord.OutboxId);
        Assert.Equal(0, waitingRecord.StreamVersion);
        Assert.Equal(0, waitingRecord.Sequence);
        Assert.Equal(started.InstanceId, waitingRecord.InstanceId);
    }

    [Fact]
    public async Task Durable_outbox_payload_envelope_has_type_key_and_content_type()
    {
        var store = new InMemoryWorkflowStore();
        var definition = new DurableWorkflowBuilder<OrderState>("PayloadEnvelopeFlow", "v1")
            .Init()
            .WaitLong("Approval", state => state.Id)
            .End()
            .Build();

        await using var engine = DurableWorkflowEngine.Create(store);
        var started = await (await engine.ForDefinitionAsync(definition)).Start(new OrderState());

        var outboxRecords = store.GetOutboxRecords(started.InstanceId);
        var waitingRecord = Assert.Single(outboxRecords, r => r.MessageType == "WorkflowWaiting");

        Assert.NotNull(waitingRecord.PayloadEnvelope.TypeKey);
        Assert.False(string.IsNullOrWhiteSpace(waitingRecord.PayloadEnvelope.TypeKey));
        Assert.Equal("application/json", waitingRecord.PayloadEnvelope.Payload.ContentType);
        Assert.NotNull(waitingRecord.PayloadEnvelope.Payload.SchemaId);
        Assert.False(string.IsNullOrWhiteSpace(waitingRecord.PayloadEnvelope.Payload.SchemaId));
    }

    [Fact]
    public async Task Durable_replay_of_same_decision_produces_identical_outbox_ids()
    {
        // PP-AT-002: deterministic outbox IDs prevent duplicate delivery on replay.
        // Re-committing the same outbox record (replay/retry scenario) is idempotent.
        var store = new InMemoryWorkflowStore();
        var definition = new DurableWorkflowBuilder<OrderState>("ReplayIdentityFlow", "v1")
            .Init()
            .WaitLong("Approval", state => state.Id)
            .End()
            .Build();

        await using var engine = DurableWorkflowEngine.Create(store);
        var started = await (await engine.ForDefinitionAsync(definition)).Start(new OrderState());

        var outboxBefore = store.GetOutboxRecords(started.InstanceId);
        var originalId = Assert.Single(outboxBefore, r => r.MessageType == "WorkflowWaiting").OutboxId;

        // Simulate replay: re-commit the same outbox record; the store must suppress the duplicate.
        var persisted = (await store.LoadAsync(started.InstanceId, CancellationToken.None))!;
        await store.CommitAsync(
            new WorkflowCommit(persisted, [], [], outboxBefore, [], []),
            CancellationToken.None);

        var outboxAfter = store.GetOutboxRecords(started.InstanceId);
        Assert.Single(outboxAfter, r => r.MessageType == "WorkflowWaiting");
        Assert.Equal(originalId, outboxAfter[0].OutboxId);
    }

    private static async Task AssertEventuallyAsync(Func<Task<bool>> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
                return;

            await Task.Delay(20);
        }

        Assert.Fail("Condition was not satisfied before timeout.");
    }
}
