namespace OrcaCore.Tests.Durable;

public sealed class DurableOutboxPumpTests
{
    private sealed class OutcomeDispatcher(Func<DispatchMessage, Result<DispatchOutcome>> dispatch) : IMessageDispatcher
    {
        public Task<Result<DispatchOutcome>> DispatchAsync(DispatchMessage message, CancellationToken cancellationToken) =>
            Task.FromResult(dispatch(message));
    }

    [Fact]
    public async Task Successful_dispatch_marks_record_dispatched()
    {
        var store = new InMemoryWorkflowStore();
        await store.AddOutboxAsync(CreateRecord("out-1"));
        var pump = new DurableOutboxPump(store, new DurableWorkflowEngineOptions { AutoDispatchOutbox = false });

        var result = await pump.DispatchPendingAsync(
            new OutcomeDispatcher(_ => Result<DispatchOutcome>.Success(new DispatchOutcome(true, false, null))));

        Assert.Equal(1, result.SucceededCount);
        Assert.Equal(OutboxStatus.Dispatched, store.GetOutbox("out-1")!.Status);
    }

    [Fact]
    public async Task Retryable_failure_returns_record_to_pending()
    {
        var store = new InMemoryWorkflowStore();
        await store.AddOutboxAsync(CreateRecord("out-1"));
        var pump = new DurableOutboxPump(store, new DurableWorkflowEngineOptions { AutoDispatchOutbox = false });

        var result = await pump.DispatchPendingAsync(
            new OutcomeDispatcher(_ => Result<DispatchOutcome>.Success(new DispatchOutcome(false, true, "retry"))));

        Assert.Equal(1, result.FailedCount);
        Assert.Equal(OutboxStatus.Pending, store.GetOutbox("out-1")!.Status);
    }

    [Fact]
    public async Task Retryable_failure_schedules_future_retry_and_blocks_immediate_releasing()
    {
        var store = new InMemoryWorkflowStore();
        await store.AddOutboxAsync(CreateRecord("out-1"));
        var pump = new DurableOutboxPump(
            store,
            new DurableWorkflowEngineOptions
            {
                AutoDispatchOutbox = false,
                OutboxDispatchPollingInterval = TimeSpan.FromSeconds(5)
            });

        await pump.DispatchPendingAsync(
            new OutcomeDispatcher(_ => Result<DispatchOutcome>.Success(new DispatchOutcome(false, true, "retry"))));

        var record = store.GetOutbox("out-1")!;
        Assert.Equal(OutboxStatus.Pending, record.Status);
        Assert.NotNull(record.LastAttemptAt);
        Assert.NotNull(record.NextAttemptAt);
        Assert.True(record.NextAttemptAt!.Value > record.LastAttemptAt!.Value);

        var leased = await store.LeaseDispatchableOutboxAsync(
            new OutboxLeaseRequest("worker-2", 1, TimeSpan.FromMinutes(1)),
            CancellationToken.None);

        Assert.Empty(leased);
    }

    [Fact]
    public async Task Terminal_failure_poisons_record()
    {
        var store = new InMemoryWorkflowStore();
        await store.AddOutboxAsync(CreateRecord("out-1"));
        var pump = new DurableOutboxPump(
            store,
            new DurableWorkflowEngineOptions
            {
                AutoDispatchOutbox = false,
                MaxOutboxDispatchAttempts = 2
            });

        var result = await pump.DispatchPendingAsync(
            new OutcomeDispatcher(_ => Result<DispatchOutcome>.Success(new DispatchOutcome(false, false, "fatal"))));

        Assert.Equal(1, result.FailedCount);
        Assert.Equal(OutboxStatus.Poisoned, store.GetOutbox("out-1")!.Status);
    }

    [Fact]
    public async Task Retryable_failure_poisons_record_when_max_attempts_is_exceeded()
    {
        var store = new InMemoryWorkflowStore();
        await store.AddOutboxAsync(CreateRecord("out-1", attemptCount: 1));
        var pump = new DurableOutboxPump(
            store,
            new DurableWorkflowEngineOptions
            {
                AutoDispatchOutbox = false,
                MaxOutboxDispatchAttempts = 2
            });

        var result = await pump.DispatchPendingAsync(
            new OutcomeDispatcher(_ => Result<DispatchOutcome>.Success(new DispatchOutcome(false, true, "retry"))));

        Assert.Equal(1, result.FailedCount);
        Assert.Equal(OutboxStatus.Poisoned, store.GetOutbox("out-1")!.Status);
    }

    private sealed class RecordingPoisonHandler : IOutboxPoisonHandler
    {
        public List<string> PoisonedOutboxIds { get; } = [];

        public Task HandleAsync(OutboxRecord record, Exception exception, CancellationToken cancellationToken)
        {
            PoisonedOutboxIds.Add(record.OutboxId);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Pump_maps_outbox_record_fields_to_dispatch_message_correctly()
    {
        var store = new InMemoryWorkflowStore();
        var payloadEnvelope = new JsonPayloadEnvelopeSerializer(DurablePayloadTypeRegistry.Default)
            .Serialize("payload", typeof(string)).Value!;

        var record = new OutboxRecord(
            "out-1", "idem-key-1", "inst-1",
            ParentInstanceId: "parent-1",
            RootInstanceId: "root-1",
            GroupId: null,
            StreamId: "inst-1",
            StreamVersion: 1,
            Sequence: 0,
            MessageType: "OrderShipped",
            Channel: "orders-channel",
            Destination: "OrderShippedHandler",
            PayloadEnvelope: payloadEnvelope,
            CorrelationId: "corr-1",
            CausationEventId: "cause-1",
            ResumeTokenId: "token-1",
            Status: OutboxStatus.Pending,
            AttemptCount: 0,
            CreatedAt: DateTimeOffset.UtcNow,
            LastAttemptAt: null,
            NextAttemptAt: null,
            LeaseOwner: null,
            LeaseExpiresAt: null,
            LastError: null);
        await store.AddOutboxAsync(record);

        var pump = new DurableOutboxPump(store, new DurableWorkflowEngineOptions { AutoDispatchOutbox = false });
        DispatchMessage? captured = null;

        await pump.DispatchPendingAsync(new OutcomeDispatcher(msg =>
        {
            captured = msg;
            return Result<DispatchOutcome>.Success(new DispatchOutcome(true, false, null));
        }));

        Assert.NotNull(captured);
        Assert.Equal("out-1", captured!.MessageId);
        Assert.Equal("idem-key-1", captured.IdempotencyKey);
        Assert.Equal("OrderShipped", captured.MessageType);
        Assert.Equal("orders-channel", captured.Channel);
        Assert.Equal("OrderShippedHandler", captured.Destination);
        Assert.Equal("inst-1", captured.InstanceId);
        Assert.Equal("parent-1", captured.ParentInstanceId);
        Assert.Equal("root-1", captured.RootInstanceId);
        Assert.Equal("corr-1", captured.CorrelationId);
        Assert.Equal("cause-1", captured.CausationId);
        Assert.Equal("token-1", captured.ResumeTokenId);
    }

    [Fact]
    public async Task Pump_dispatch_message_headers_are_non_null()
    {
        var store = new InMemoryWorkflowStore();
        await store.AddOutboxAsync(CreateRecord("out-1"));
        var pump = new DurableOutboxPump(store, new DurableWorkflowEngineOptions { AutoDispatchOutbox = false });
        IReadOnlyDictionary<string, string>? capturedHeaders = null;

        await pump.DispatchPendingAsync(new OutcomeDispatcher(msg =>
        {
            capturedHeaders = msg.Headers;
            return Result<DispatchOutcome>.Success(new DispatchOutcome(true, false, null));
        }));

        Assert.NotNull(capturedHeaders);
        Assert.Empty(capturedHeaders);
    }

    [Fact]
    public async Task Pump_catches_dispatcher_exception_and_treats_as_retryable()
    {
        var store = new InMemoryWorkflowStore();
        await store.AddOutboxAsync(CreateRecord("out-1"));
        var pump = new DurableOutboxPump(store, new DurableWorkflowEngineOptions
        {
            AutoDispatchOutbox = false,
            MaxOutboxDispatchAttempts = 5
        });

        var result = await pump.DispatchPendingAsync(
            new OutcomeDispatcher(_ => throw new InvalidOperationException("unexpected dispatch failure")));

        Assert.Equal(1, result.FailedCount);
        Assert.Equal(OutboxStatus.Pending, store.GetOutbox("out-1")!.Status);
        Assert.Equal(1, store.GetOutbox("out-1")!.AttemptCount);
    }

    [Fact]
    public async Task Pump_invokes_poison_handler_only_when_record_becomes_poisoned()
    {
        var store = new InMemoryWorkflowStore();
        await store.AddOutboxAsync(CreateRecord("out-terminal", "inst-a"));
        await store.AddOutboxAsync(CreateRecord("out-success", "inst-b"));
        var poisonHandler = new RecordingPoisonHandler();
        var pump = new DurableOutboxPump(store, new DurableWorkflowEngineOptions
        {
            AutoDispatchOutbox = false,
            OutboxPoisonHandler = poisonHandler
        });

        await pump.DispatchPendingAsync(new OutcomeDispatcher(msg =>
            msg.MessageId == "out-terminal"
                ? Result<DispatchOutcome>.Success(new DispatchOutcome(false, false, "fatal"))
                : Result<DispatchOutcome>.Success(new DispatchOutcome(true, false, null))));

        Assert.Equal(["out-terminal"], poisonHandler.PoisonedOutboxIds);
    }

    [Fact]
    public async Task Pump_reports_succeeded_and_failed_counts_in_result()
    {
        var store = new InMemoryWorkflowStore();
        await store.AddOutboxAsync(CreateRecord("out-success", "inst-a"));
        await store.AddOutboxAsync(CreateRecord("out-fail", "inst-b"));
        var pump = new DurableOutboxPump(store, new DurableWorkflowEngineOptions
        {
            AutoDispatchOutbox = false,
            MaxOutboxDispatchAttempts = 5
        });

        var result = await pump.DispatchPendingAsync(new OutcomeDispatcher(msg =>
            msg.MessageId == "out-success"
                ? Result<DispatchOutcome>.Success(new DispatchOutcome(true, false, null))
                : Result<DispatchOutcome>.Success(new DispatchOutcome(false, true, "retry"))));

        Assert.Equal(2, result.AttemptedCount);
        Assert.Equal(1, result.SucceededCount);
        Assert.Equal(1, result.FailedCount);
    }

    private static OutboxRecord CreateRecord(string outboxId, string instanceId = "inst-1", int attemptCount = 0)
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
            StreamVersion: 0,
            Sequence: 0,
            MessageType: "WorkflowWaiting",
            Channel: "workflow-status",
            Destination: "WorkflowWaiting",
            PayloadEnvelope: payloadEnvelope,
            CorrelationId: null,
            CausationEventId: null,
            ResumeTokenId: null,
            Status: OutboxStatus.Pending,
            AttemptCount: attemptCount,
            CreatedAt: DateTimeOffset.UtcNow,
            LastAttemptAt: null,
            NextAttemptAt: null,
            LeaseOwner: null,
            LeaseExpiresAt: null,
            LastError: null);
    }
}
