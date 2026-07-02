using System.Text.Json;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Providers.InMemory;

/// <summary>
/// Provides in-memory durable port implementations for tests and local execution.
/// </summary>
public sealed class InMemoryWorkflowProvider :
    IWorkflowEventStore,
    IWorkflowInboxStore,
    IWorkflowOutboxStore,
    IWorkflowProjectionStore,
    ITimerScheduler,
    IMessageDispatcher,
    IWorkflowPayloadSerializer
{
    private const string JsonContentType = "application/json";

    private readonly object gate = new();
    private readonly Dictionary<EventId, InboxRecordState> inbox = [];
    private readonly Dictionary<OutboxRecordId, InMemoryOutboxRecord> outbox = [];
    private readonly List<OutboxWrite> dispatched = [];
    private readonly List<ProjectionWrite> projections = [];
    private readonly Dictionary<InstanceId, WorkflowInstanceSnapshot> summaries = [];
    private readonly Dictionary<InstanceId, CheckpointWrite> checkpoints = [];
    private readonly Dictionary<WorkflowStreamId, List<WorkflowEvent>> streams = [];
    private readonly Dictionary<TimerId, TimerScheduleRequest> timers = [];

    /// <inheritdoc />
    public Task<Option<CheckpointWrite>> LoadCheckpointAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            return Task.FromResult(checkpoints.TryGetValue(instanceId, out var checkpoint)
                ? Option<CheckpointWrite>.Some(CloneCheckpointWrite(checkpoint))
                : Option<CheckpointWrite>.None);
        }
    }

    /// <inheritdoc />
    public Task<Result<AppendEventsResult>> AppendAsync(
        ProviderCommitBatch batch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            var stream = GetStream(batch.StreamId);
            var actualVersion = new StreamVersion(stream.Count);
            if (actualVersion != batch.ExpectedVersion)
            {
                return Task.FromResult(EventStoreConflict.ExpectedVersionMismatch(
                    batch.ExpectedVersion,
                    actualVersion));
            }

            stream.AddRange(batch.Events);
            ApplyInboxOperations(batch.InboxOperations);
            foreach (var record in batch.OutboxRecords)
            {
                outbox[record.OutboxRecordId] = new InMemoryOutboxRecord(
                    CloneOutboxWrite(record),
                    OutboxRecordState.Pending);
            }

            ApplyProjectionOperations(batch.ProjectionOperations);
            if (batch.Checkpoint is { } checkpoint)
            {
                checkpoints[checkpoint.InstanceId] = CloneCheckpointWrite(checkpoint);
            }

            return Task.FromResult(Result<AppendEventsResult>.Success(
                new AppendEventsResult(new StreamVersion(stream.Count))));
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkflowEvent>> LoadTailAsync(
        WorkflowStreamId streamId,
        StreamVersion afterVersion,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            var events = streams.TryGetValue(streamId, out var stream)
                ? stream.Skip((int)afterVersion.Value).ToArray()
                : [];
            return Task.FromResult<IReadOnlyList<WorkflowEvent>>(events);
        }
    }

    /// <inheritdoc />
    public Task<Option<InboxRecordState>> GetAsync(EventId eventId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            return Task.FromResult(inbox.TryGetValue(eventId, out var state)
                ? Option<InboxRecordState>.Some(state)
                : Option<InboxRecordState>.None);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<OutboxWrite>> ClaimAsync(int maxCount, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxCount);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            var claimed = outbox.Values
                .Where(record => record.State is OutboxRecordState.Pending or OutboxRecordState.Retryable)
                .Take(maxCount)
                .Select(record => CloneOutboxWrite(record.Write))
                .ToArray();
            foreach (var record in claimed)
            {
                outbox[record.OutboxRecordId] = outbox[record.OutboxRecordId] with { State = OutboxRecordState.Claimed };
            }

            return Task.FromResult<IReadOnlyList<OutboxWrite>>(claimed);
        }
    }

    /// <inheritdoc />
    public Task<Option<OutboxRecordState>> GetStateAsync(
        OutboxRecordId outboxRecordId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            return Task.FromResult(outbox.TryGetValue(outboxRecordId, out var record)
                ? Option<OutboxRecordState>.Some(record.State)
                : Option<OutboxRecordState>.None);
        }
    }

    /// <inheritdoc />
    public Task MarkAsync(
        OutboxRecordId outboxRecordId,
        OutboxRecordState state,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            if (outbox.TryGetValue(outboxRecordId, out var record))
            {
                outbox[outboxRecordId] = record with { State = state };
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ApplyAsync(IReadOnlyList<ProjectionWrite> operations, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operations);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            ApplyProjectionOperations(operations);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkflowInstanceSnapshot>> ListAsync(
        WorkflowProjectionQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            return Task.FromResult<IReadOnlyList<WorkflowInstanceSnapshot>>(
                summaries.Values
                    .Where(snapshot => Matches(snapshot, query))
                    .Select(CloneSnapshot)
                    .ToArray());
        }
    }

    /// <inheritdoc />
    public Task<int> CountAsync(WorkflowProjectionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            return Task.FromResult(summaries.Values.Count(snapshot => Matches(snapshot, query)));
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ActiveWaitSnapshot>> ListActiveWaitsAsync(
        WorkflowProjectionQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            return Task.FromResult<IReadOnlyList<ActiveWaitSnapshot>>(
                summaries.Values
                    .Where(snapshot => Matches(snapshot, query))
                    .SelectMany(snapshot => snapshot.ActiveWaits)
                    .Select(CloneActiveWait)
                    .ToArray());
        }
    }

    /// <inheritdoc />
    public Task<WorkflowStatistics> GetStatisticsAsync(
        WorkflowProjectionQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            var groups = summaries.Values
                .Where(snapshot => Matches(snapshot, query))
                .GroupBy(snapshot => new
                {
                    snapshot.DefinitionId,
                    snapshot.DefinitionVersion,
                    snapshot.Status
                })
                .Select(group => new WorkflowStatisticsGroup
                {
                    DefinitionId = group.Key.DefinitionId,
                    DefinitionVersion = group.Key.DefinitionVersion,
                    Status = group.Key.Status,
                    Count = group.Count()
                })
                .ToArray();

            return Task.FromResult(new WorkflowStatistics { Groups = groups });
        }
    }

    /// <inheritdoc />
    public Task ScheduleAsync(TimerScheduleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            timers[request.TimerId] = request;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<DispatchResult> DispatchAsync(OutboxWrite record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            dispatched.Add(CloneOutboxWrite(record));
        }

        return Task.FromResult(DispatchResult.Success);
    }

    /// <inheritdoc />
    public SerializedPayload Serialize<TPayload>(TPayload payload)
    {
        return new SerializedPayload(
            JsonContentType,
            JsonSerializer.SerializeToUtf8Bytes(payload));
    }

    /// <inheritdoc />
    public TPayload Deserialize<TPayload>(SerializedPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (!string.Equals(payload.ContentType, JsonContentType, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Payload content type '{payload.ContentType}' is not supported.",
                nameof(payload));
        }

        return JsonSerializer.Deserialize<TPayload>(payload.Payload)
            ?? throw new JsonException($"Payload could not be deserialized as '{typeof(TPayload).Name}'.");
    }

    private List<WorkflowEvent> GetStream(WorkflowStreamId streamId)
    {
        if (!streams.TryGetValue(streamId, out var stream))
        {
            stream = [];
            streams.Add(streamId, stream);
        }

        return stream;
    }

    private void ApplyInboxOperations(IEnumerable<InboxWrite> operations)
    {
        foreach (var operation in operations)
        {
            if (inbox.TryGetValue(operation.EventId, out var existingState)
                && existingState == InboxRecordState.Applied)
            {
                continue;
            }

            inbox[operation.EventId] = operation.State;
        }
    }

    private void ApplyProjectionOperations(IEnumerable<ProjectionWrite> operations)
    {
        foreach (var operation in operations)
        {
            projections.Add(operation);
            if (operation.InstanceSnapshot is { } snapshot)
            {
                summaries[operation.InstanceId] = CloneSnapshot(snapshot);
            }
        }
    }

    private static bool Matches(WorkflowInstanceSnapshot snapshot, WorkflowProjectionQuery query)
    {
        return (query.InstanceId is null || snapshot.InstanceId == query.InstanceId) &&
            (query.DefinitionId is null || snapshot.DefinitionId == query.DefinitionId) &&
            (query.DefinitionVersion is null || snapshot.DefinitionVersion == query.DefinitionVersion) &&
            (query.Status is null || snapshot.Status == query.Status) &&
            (query.ActiveWaitEventName is null || snapshot.ActiveWaits.Any(wait =>
                string.Equals(wait.EventName, query.ActiveWaitEventName, StringComparison.Ordinal))) &&
            (query.ActiveWaitCorrelationId is null || snapshot.ActiveWaits.Any(wait =>
                wait.CorrelationId == query.ActiveWaitCorrelationId));
    }

    private static WorkflowInstanceSnapshot CloneSnapshot(WorkflowInstanceSnapshot snapshot)
    {
        return snapshot with { ActiveWaits = snapshot.ActiveWaits.Select(CloneActiveWait).ToArray() };
    }

    private static ActiveWaitSnapshot CloneActiveWait(ActiveWaitSnapshot snapshot)
    {
        return snapshot with { };
    }

    private static OutboxWrite CloneOutboxWrite(OutboxWrite record)
    {
        return record with { Payload = [.. record.Payload] };
    }

    private static CheckpointWrite CloneCheckpointWrite(CheckpointWrite checkpoint)
    {
        return checkpoint with { Payload = [.. checkpoint.Payload] };
    }

    private sealed record InMemoryOutboxRecord(OutboxWrite Write, OutboxRecordState State);
}
