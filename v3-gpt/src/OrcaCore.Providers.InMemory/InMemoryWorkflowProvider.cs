using System.Text.Json;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
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
    private readonly List<OutboxWrite> outbox = [];
    private readonly List<OutboxWrite> dispatched = [];
    private readonly List<ProjectionWrite> projections = [];
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
            outbox.AddRange(batch.OutboxRecords.Select(CloneOutboxWrite));
            projections.AddRange(batch.ProjectionOperations);
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
            return Task.FromResult<IReadOnlyList<OutboxWrite>>(
                outbox.Take(maxCount).Select(CloneOutboxWrite).ToArray());
        }
    }

    /// <inheritdoc />
    public Task ApplyAsync(IReadOnlyList<ProjectionWrite> operations, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operations);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            projections.AddRange(operations);
        }

        return Task.CompletedTask;
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

    private static OutboxWrite CloneOutboxWrite(OutboxWrite record)
    {
        return record with { Payload = [.. record.Payload] };
    }

    private static CheckpointWrite CloneCheckpointWrite(CheckpointWrite checkpoint)
    {
        return checkpoint with { Payload = [.. checkpoint.Payload] };
    }
}
