using System.Collections.Concurrent;
using System.Text.Json;

namespace OrcaCore.Runtime.Durable.Persistence;

internal sealed class InMemoryWorkflowStore(int concurrencyIncrement = 1) : IWorkflowStore
{
    private readonly object _gate = new();
    private readonly ConcurrentDictionary<string, PersistedInstance> _instances = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, List<InboxRecord>> _inbox = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, List<HistoryRecord>> _history = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, OutboxRecord> _outbox = new(StringComparer.Ordinal);

    public Task CreateAsync(PersistedInstance data, CancellationToken ct)
        => CreateAsync(data, [], [], ct);

    public Task CreateAsync(
        PersistedInstance data,
        IReadOnlyList<OutboxRecord> outboxRecords,
        IReadOnlyList<HistoryRecord> historyRecords,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var clone = CloneInstance(data);
            if (!_instances.TryAdd(data.InstanceId, clone))
            {
                throw new InvalidOperationException(
                    $"Durable instance '{data.InstanceId}' already exists.");
            }

            ValidateOutboxRecords(outboxRecords, _outbox);
            ApplyOutboxRecords(outboxRecords);
            ApplyHistoryRecords(data.InstanceId, historyRecords);
        }
        return Task.CompletedTask;
    }

    public Task<PersistedInstance?> LoadAsync(string instanceId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        return Task.FromResult(
            _instances.TryGetValue(instanceId, out var instance)
                ? CloneInstance(instance)
                : null);
    }

    public Task<IReadOnlyList<InboxRecord>> GetInboxAsync(string instanceId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(GetInbox(instanceId));
    }

    public Task<PersistedInstance> CommitTransitionAsync(PersistedInstance data, CancellationToken ct)
        => CommitTransitionAsync(data, [], [], [], [], ct);

    public Task<PersistedInstance> CommitTransitionAsync(
        PersistedInstance data,
        IReadOnlyList<InboxRecord> inboxRecords,
        IReadOnlyList<string> processedInboxEventIds,
        IReadOnlyList<OutboxRecord> outboxRecords,
        IReadOnlyList<HistoryRecord> historyRecords,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (!_instances.TryGetValue(data.InstanceId, out var current))
            {
                throw new KeyNotFoundException(
                    $"Durable instance '{data.InstanceId}' not found.");
            }

            if (current.ConcurrencyToken != data.ConcurrencyToken)
            {
                throw new ConcurrencyException(
                    $"Concurrency token mismatch for instance '{data.InstanceId}'. " +
                    $"Expected {current.ConcurrencyToken} but received {data.ConcurrencyToken}.");
            }

            var committed = CloneInstance(data with
            {
                ConcurrencyToken = current.ConcurrencyToken + concurrencyIncrement
            });
            var stagedInbox = StageInboxRecords(data.InstanceId, inboxRecords, processedInboxEventIds);
            var stagedHistory = StageHistoryRecords(data.InstanceId, historyRecords);
            ValidateOutboxRecords(outboxRecords, existingOutbox: null);
            var stagedOutbox = StageOutboxRecords(outboxRecords);

            _instances[data.InstanceId] = committed;
            ApplyInboxRecords(data.InstanceId, stagedInbox);
            ApplyHistoryRecords(data.InstanceId, stagedHistory);
            ApplyStagedOutboxRecords(stagedOutbox);
        }

        return Task.FromResult(CloneInstance(_instances[data.InstanceId]));
    }

    public Task<IReadOnlyList<PersistedInstance>> QueryAsync(
        WorkflowStatus? status = null,
        string? definitionId = null,
        string? definitionVersion = null,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var results = _instances.Values
            .Where(instance => status is null || instance.RuntimeState.Status == status)
            .Where(instance => definitionId is null || string.Equals(instance.DefinitionId, definitionId, StringComparison.Ordinal))
            .Where(instance => definitionVersion is null || string.Equals(instance.DefinitionVersion, definitionVersion, StringComparison.Ordinal))
            .OrderBy(instance => instance.InstanceId, StringComparer.Ordinal)
            .Select(CloneInstance)
            .ToArray();

        return Task.FromResult<IReadOnlyList<PersistedInstance>>(results);
    }

    public Task<CorrelationLookupResult> LookupByCorrelationAsync(
        string eventName,
        string correlationId,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var matches = _instances.Values
            .Where(instance => instance.RuntimeState.Status == WorkflowStatus.Waiting)
            .Select(instance => new
            {
                instance.InstanceId,
                instance.DefinitionId,
                DefinitionVersion = instance.DefinitionVersion ?? string.Empty,
                Wait = instance.RuntimeState.ActiveWaits.FirstOrDefault(wait =>
                    wait.Status == WaitStatus.Active
                    && string.Equals(wait.EventName, eventName, StringComparison.Ordinal)
                    && string.Equals(wait.CorrelationId, correlationId, StringComparison.Ordinal))
            })
            .Where(x => x.Wait is not null)
            .Select(x => new CorrelationMatch(
                x.InstanceId,
                x.DefinitionId,
                x.DefinitionVersion,
                x.Wait!.Mode))
            .OrderBy(x => x.InstanceId, StringComparer.Ordinal)
            .ToArray();

        var matchType = matches.Length switch
        {
            0 => CorrelationMatchType.NoMatch,
            1 => CorrelationMatchType.SingleMatch,
            _ => CorrelationMatchType.Ambiguous
        };

        return Task.FromResult(new CorrelationLookupResult(matchType, matches));
    }

    public Task<IReadOnlyList<OutboxRecord>> GetPendingOutboxAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var pending = _outbox.Values
            .Where(record => !record.Dispatched && !record.Poisoned)
            .OrderBy(record => record.CreatedAt)
            .Select(CloneOutboxRecord)
            .ToArray();

        return Task.FromResult<IReadOnlyList<OutboxRecord>>(pending);
    }

    public Task MarkOutboxDispatchedAsync(string outboxId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        while (true)
        {
            if (!_outbox.TryGetValue(outboxId, out var current))
            {
                throw new KeyNotFoundException(
                    $"Outbox record '{outboxId}' not found.");
            }

            if (current.Dispatched)
                return Task.CompletedTask;

            var updated = current with { Dispatched = true };
            if (_outbox.TryUpdate(outboxId, updated, current))
                return Task.CompletedTask;
        }
    }

    public Task<OutboxRecord> RecordOutboxDispatchFailureAsync(
        string outboxId,
        string? error,
        DateTimeOffset failedAt,
        bool poison,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        while (true)
        {
            if (!_outbox.TryGetValue(outboxId, out var current))
            {
                throw new KeyNotFoundException(
                    $"Outbox record '{outboxId}' not found.");
            }

            var updated = current with
            {
                FailureCount = current.FailureCount + 1,
                LastFailureAt = failedAt,
                LastFailure = error,
                Poisoned = current.Poisoned || poison
            };

            if (_outbox.TryUpdate(outboxId, updated, current))
                return Task.FromResult(CloneOutboxRecord(updated));
        }
    }

    public Task AppendHistoryAsync(string instanceId, HistoryRecord record, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var staged = StageHistoryRecords(instanceId, [record]);
            ApplyHistoryRecords(instanceId, staged);
        }

        return Task.CompletedTask;
    }

    public Task PurgeArtifactsAsync(string instanceId, DateTimeOffset olderThan, CancellationToken ct)
        => PurgeArtifactsAsync(
            instanceId,
            new DurableArtifactRetentionCutoffs(olderThan, olderThan, olderThan),
            ct);

    public Task PurgeArtifactsAsync(string instanceId, DurableArtifactRetentionCutoffs cutoffs, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (cutoffs.ProcessedInboxOlderThan is not null && _inbox.TryGetValue(instanceId, out var inbox))
            {
                lock (inbox)
                {
                    inbox.RemoveAll(record => record.Processed && record.ReceivedAt < cutoffs.ProcessedInboxOlderThan.Value);
                }
            }

            if (cutoffs.HistoryOlderThan is not null && _history.TryGetValue(instanceId, out var history))
            {
                lock (history)
                {
                    history.RemoveAll(record => record.Timestamp < cutoffs.HistoryOlderThan.Value);
                }
            }

            if (cutoffs.TerminalOutboxOlderThan is not null)
            {
                foreach (var record in _outbox.Values
                             .Where(x => x.InstanceId == instanceId)
                             .Where(x => (x.Dispatched || x.Poisoned) && x.CreatedAt < cutoffs.TerminalOutboxOlderThan.Value)
                             .ToArray())
                {
                    _outbox.TryRemove(record.OutboxId, out _);
                }
            }
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(string instanceId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (!_instances.TryRemove(instanceId, out _))
            {
                throw new KeyNotFoundException(
                    $"Durable instance '{instanceId}' not found.");
            }

            _inbox.TryRemove(instanceId, out _);
            _history.TryRemove(instanceId, out _);

            foreach (var record in _outbox.Values.Where(x => x.InstanceId == instanceId).ToArray())
                _outbox.TryRemove(record.OutboxId, out _);
        }

        return Task.CompletedTask;
    }

    internal Task AddOutboxAsync(OutboxRecord record, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var clone = CloneOutboxRecord(record);
        if (!_outbox.TryAdd(record.OutboxId, clone))
        {
            throw new InvalidOperationException(
                $"Outbox record '{record.OutboxId}' already exists.");
        }

        return Task.CompletedTask;
    }

    internal IReadOnlyList<HistoryRecord> GetHistory(string instanceId)
    {
        if (!_history.TryGetValue(instanceId, out var records))
            return [];

        lock (records)
        {
            return records.ToArray();
        }
    }

    internal IReadOnlyList<InboxRecord> GetInbox(string instanceId)
    {
        if (!_inbox.TryGetValue(instanceId, out var records))
            return [];

        lock (records)
        {
            return records.ToArray();
        }
    }

    internal OutboxRecord? GetOutbox(string outboxId)
    {
        if (!_outbox.TryGetValue(outboxId, out var record))
            return null;

        return CloneOutboxRecord(record);
    }

    private static PersistedInstance CloneInstance(PersistedInstance instance) =>
        new(
            instance.InstanceId,
            instance.DefinitionId,
            instance.DefinitionVersion,
            instance.ConcurrencyToken,
            instance.BusinessState.Clone(),
            CloneRuntimeState(instance.RuntimeState));

    private static PersistedRuntimeState CloneRuntimeState(PersistedRuntimeState state) =>
        new(
            state.Status,
            state.CreatedAt,
            state.LastTransitionAt,
            state.ActiveWaits.Select(CloneWaitRecord).ToArray(),
            state.PendingEvents.Select(ClonePendingEvent).ToArray(),
            state.ConsumedEventIds.ToArray(),
            state.Error is null ? null : CloneError(state.Error),
            CloneExecutionPath(state.MainPath),
            state.ActiveParallel is null ? null : CloneParallelGroup(state.ActiveParallel));

    private static PersistedWaitRecord CloneWaitRecord(PersistedWaitRecord wait) =>
        new(wait.WaitId, wait.EventName, wait.CorrelationId, wait.BranchId, wait.RegisteredAt, wait.Status, wait.Mode);

    private static PersistedPendingEvent ClonePendingEvent(PersistedPendingEvent pending) =>
        new(CloneEventEnvelope(pending.Envelope), pending.ReceivedAt, pending.Consumed);

    private static PersistedEventEnvelope CloneEventEnvelope(PersistedEventEnvelope envelope) =>
        new(
            envelope.EventName,
            envelope.CorrelationId,
            envelope.Payload?.Clone(),
            envelope.PayloadTypeKey,
            envelope.EventId);

    private static PersistedError CloneError(PersistedError error) =>
        new(error.ExceptionType, error.Message, error.StepId, error.Timestamp);

    private static PersistedExecutionPath CloneExecutionPath(PersistedExecutionPath path) =>
        new(path.BranchId, path.Frames.Select(CloneFrame).ToArray());

    private static PersistedFrame CloneFrame(PersistedFrame frame) =>
        new(frame.Kind, frame.NodePath, frame.Index, frame.ScopeId);

    private static PersistedParallelFrameGroup CloneParallelGroup(PersistedParallelFrameGroup group) =>
        new(group.BranchPaths.ToDictionary(
            entry => entry.Key,
            entry => CloneExecutionPath(entry.Value),
            StringComparer.Ordinal));

    private static OutboxRecord CloneOutboxRecord(OutboxRecord record) =>
        new(
            record.OutboxId,
            record.InstanceId,
            record.EventName,
            record.Payload?.Clone(),
            record.CreatedAt,
            record.Dispatched,
            record.FailureCount,
            record.LastFailureAt,
            record.LastFailure,
            record.Poisoned);

    private List<InboxRecord> StageInboxRecords(
        string instanceId,
        IReadOnlyList<InboxRecord> inboxRecords,
        IReadOnlyList<string> processedEventIds)
    {
        List<InboxRecord> staged;
        if (_inbox.TryGetValue(instanceId, out var existing))
        {
            lock (existing)
            {
                staged = existing.Select(CloneInboxRecord).ToList();
            }
        }
        else
        {
            staged = [];
        }

        foreach (var record in inboxRecords)
            staged.Add(CloneInboxRecord(record));

        if (processedEventIds.Count != 0)
        {
            var eventIdSet = new HashSet<string>(processedEventIds, StringComparer.Ordinal);
            for (var i = 0; i < staged.Count; i++)
            {
                var record = staged[i];
                if (!record.Processed && eventIdSet.Contains(record.EventId))
                    staged[i] = record with { Processed = true };
            }
        }

        return staged;
    }

    private List<HistoryRecord> StageHistoryRecords(string instanceId, IReadOnlyList<HistoryRecord> historyRecords)
    {
        List<HistoryRecord> staged;
        if (_history.TryGetValue(instanceId, out var existing))
        {
            lock (existing)
            {
                staged = existing.ToList();
            }
        }
        else
        {
            staged = [];
        }

        staged.AddRange(historyRecords);
        return staged;
    }

    private static void ValidateOutboxRecords(
        IReadOnlyList<OutboxRecord> outboxRecords,
        IDictionary<string, OutboxRecord>? existingOutbox)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var record in outboxRecords)
        {
            if (!seen.Add(record.OutboxId)
                || existingOutbox?.ContainsKey(record.OutboxId) == true)
            {
                throw new InvalidOperationException(
                    $"Outbox record '{record.OutboxId}' already exists.");
            }
        }
    }

    private Dictionary<string, OutboxRecord> StageOutboxRecords(IReadOnlyList<OutboxRecord> outboxRecords)
    {
        ValidateOutboxRecords(outboxRecords, _outbox);
        return outboxRecords.ToDictionary(
            record => record.OutboxId,
            CloneOutboxRecord,
            StringComparer.Ordinal);
    }

    private void ApplyInboxRecords(string instanceId, List<InboxRecord> stagedInbox)
    {
        if (stagedInbox.Count == 0)
        {
            _inbox.TryRemove(instanceId, out _);
            return;
        }

        _inbox[instanceId] = stagedInbox;
    }

    private void ApplyHistoryRecords(string instanceId, IReadOnlyList<HistoryRecord> stagedHistory)
    {
        if (stagedHistory.Count == 0)
        {
            _history.TryRemove(instanceId, out _);
            return;
        }

        _history[instanceId] = stagedHistory.ToList();
    }

    private void ApplyOutboxRecords(IReadOnlyList<OutboxRecord> outboxRecords)
    {
        foreach (var record in outboxRecords)
            _outbox[record.OutboxId] = CloneOutboxRecord(record);
    }

    private void ApplyStagedOutboxRecords(IReadOnlyDictionary<string, OutboxRecord> stagedOutbox)
    {
        foreach (var entry in stagedOutbox)
            _outbox[entry.Key] = entry.Value;
    }

    private static InboxRecord CloneInboxRecord(InboxRecord record) =>
        new(
            record.EventId,
            record.InstanceId,
            CloneEventEnvelope(record.Envelope),
            record.ReceivedAt,
            record.Processed);
}
