using System.Collections.Concurrent;

namespace OrcaCore.Runtime.Durable.Persistence;

internal sealed class InMemoryWorkflowStore(int concurrencyIncrement = 1) : IWorkflowStore
{
    private readonly object _gate = new();
    private readonly ConcurrentDictionary<string, PersistedInstance> _instances = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, List<InboxRecord>> _inbox = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, List<HistoryRecord>> _history = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, OutboxRecord> _outbox = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, List<ProjectionWorkItem>> _projectionWork = new(StringComparer.Ordinal);

    public Task<PersistedInstance> CreateAsync(WorkflowCommit commit, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var instance = CloneInstance(commit.Instance);
            var stagedInbox = StageInboxRecords(instance.InstanceId, commit.InboxRecords, commit.ProcessedInboxEventIds);
            var stagedHistory = StageHistoryRecords(instance.InstanceId, commit.HistoryRecords);
            var stagedProjectionWork = StageProjectionWorkItems(instance.InstanceId, commit.ProjectionWorkItems);
            var stagedOutbox = StageOutboxRecords(commit.OutboxRecords);

            if (!_instances.TryAdd(instance.InstanceId, instance))
                throw new InvalidOperationException($"Durable instance '{instance.InstanceId}' already exists.");

            ApplyInboxRecords(instance.InstanceId, stagedInbox);
            ApplyHistoryRecords(instance.InstanceId, stagedHistory);
            ApplyProjectionWorkItems(instance.InstanceId, stagedProjectionWork);
            ApplyStagedOutboxRecords(stagedOutbox);
            return Task.FromResult(CloneInstance(instance));
        }
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

    public Task<PersistedInstance> CommitAsync(WorkflowCommit commit, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var instance = commit.Instance;
            if (!_instances.TryGetValue(instance.InstanceId, out var current))
                throw new KeyNotFoundException($"Durable instance '{instance.InstanceId}' not found.");

            if (current.ConcurrencyToken != instance.ConcurrencyToken)
            {
                throw new ConcurrencyException(
                    $"Concurrency token mismatch for instance '{instance.InstanceId}'. " +
                    $"Expected {current.ConcurrencyToken} but received {instance.ConcurrencyToken}.");
            }

            var committed = CloneInstance(instance with
            {
                ConcurrencyToken = current.ConcurrencyToken + concurrencyIncrement
            });
            var stagedInbox = StageInboxRecords(instance.InstanceId, commit.InboxRecords, commit.ProcessedInboxEventIds);
            var stagedHistory = StageHistoryRecords(instance.InstanceId, commit.HistoryRecords);
            var stagedProjectionWork = StageProjectionWorkItems(instance.InstanceId, commit.ProjectionWorkItems);
            var stagedOutbox = StageOutboxRecords(commit.OutboxRecords);

            _instances[instance.InstanceId] = committed;
            ApplyInboxRecords(instance.InstanceId, stagedInbox);
            ApplyHistoryRecords(instance.InstanceId, stagedHistory);
            ApplyProjectionWorkItems(instance.InstanceId, stagedProjectionWork);
            ApplyStagedOutboxRecords(stagedOutbox);
            return Task.FromResult(CloneInstance(committed));
        }
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

    public Task<IReadOnlyList<OutboxRecord>> LeaseDispatchableOutboxAsync(
        OutboxLeaseRequest request,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(request.LeaseOwner);

        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            NormalizeExpiredLeases(now);

            var leased = new List<OutboxRecord>();
            foreach (var group in _outbox.Values
                         .GroupBy(record => record.InstanceId, StringComparer.Ordinal)
                         .OrderBy(group => group.Key, StringComparer.Ordinal))
            {
                if (leased.Count >= request.MaxCount)
                    break;

                var next = group
                    .OrderBy(record => record.StreamVersion)
                    .ThenBy(record => record.Sequence)
                    .FirstOrDefault(record =>
                        record.Status is OutboxStatus.Pending or OutboxStatus.Leased);

                if (next is null || next.Status != OutboxStatus.Pending)
                    continue;

                if (next.NextAttemptAt is not null && next.NextAttemptAt > now)
                    continue;

                var leasedRecord = next with
                {
                    Status = OutboxStatus.Leased,
                    LeaseOwner = request.LeaseOwner,
                    LeaseExpiresAt = now.Add(request.LeaseDuration)
                };
                _outbox[next.OutboxId] = leasedRecord;
                leased.Add(CloneOutboxRecord(leasedRecord));
            }

            return Task.FromResult<IReadOnlyList<OutboxRecord>>(leased);
        }
    }

    public Task<OutboxRecord> CompleteLeasedOutboxAsync(
        string outboxId,
        string leaseOwner,
        DateTimeOffset dispatchedAt,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var current = GetRequiredOutbox(outboxId);
            EnsureLeaseOwnership(current, leaseOwner, dispatchedAt);

            var updated = current with
            {
                Status = OutboxStatus.Dispatched,
                AttemptCount = current.AttemptCount + 1,
                LastAttemptAt = dispatchedAt,
                NextAttemptAt = null,
                LeaseOwner = null,
                LeaseExpiresAt = null,
                LastError = null
            };

            _outbox[outboxId] = updated;
            return Task.FromResult(CloneOutboxRecord(updated));
        }
    }

    public Task<OutboxRecord> FailLeasedOutboxAsync(
        string outboxId,
        string leaseOwner,
        OutboxDispatchFailure failure,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var current = GetRequiredOutbox(outboxId);
            EnsureLeaseOwnership(current, leaseOwner, failure.FailedAt);

            var updated = current with
            {
                Status = failure.Poison ? OutboxStatus.Poisoned : OutboxStatus.Pending,
                AttemptCount = current.AttemptCount + 1,
                LastAttemptAt = failure.FailedAt,
                NextAttemptAt = failure.Poison ? null : failure.NextAttemptAt,
                LeaseOwner = null,
                LeaseExpiresAt = null,
                LastError = failure.Error
            };

            _outbox[outboxId] = updated;
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
                             .Where(x => x.Status is OutboxStatus.Dispatched or OutboxStatus.Poisoned)
                             .Where(x => x.CreatedAt < cutoffs.TerminalOutboxOlderThan.Value)
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
                throw new KeyNotFoundException($"Durable instance '{instanceId}' not found.");

            _inbox.TryRemove(instanceId, out _);
            _history.TryRemove(instanceId, out _);
            _projectionWork.TryRemove(instanceId, out _);

            foreach (var record in _outbox.Values.Where(x => x.InstanceId == instanceId).ToArray())
                _outbox.TryRemove(record.OutboxId, out _);
        }

        return Task.CompletedTask;
    }

    internal Task AddOutboxAsync(OutboxRecord record, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (_outbox.TryGetValue(record.OutboxId, out var existing))
            {
                if (!AreEquivalent(existing, record))
                    throw new InvalidOperationException($"Outbox record '{record.OutboxId}' already exists.");

                return Task.CompletedTask;
            }

            _outbox[record.OutboxId] = CloneOutboxRecord(record);
        }

        return Task.CompletedTask;
    }

    internal Task MarkOutboxDispatchedAsync(string outboxId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var current = GetRequiredOutbox(outboxId);
            _outbox[outboxId] = current with
            {
                Status = OutboxStatus.Dispatched,
                LastAttemptAt = DateTimeOffset.UtcNow,
                NextAttemptAt = null,
                LeaseOwner = null,
                LeaseExpiresAt = null,
                LastError = null
            };
        }

        return Task.CompletedTask;
    }

    internal Task<OutboxRecord> RecordOutboxDispatchFailureAsync(
        string outboxId,
        string? error,
        DateTimeOffset failedAt,
        bool poison,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var current = GetRequiredOutbox(outboxId);
            var updated = current with
            {
                Status = poison ? OutboxStatus.Poisoned : OutboxStatus.Pending,
                AttemptCount = current.AttemptCount + 1,
                LastAttemptAt = failedAt,
                NextAttemptAt = poison ? null : failedAt,
                LeaseOwner = null,
                LeaseExpiresAt = null,
                LastError = error
            };
            _outbox[outboxId] = updated;
            return Task.FromResult(CloneOutboxRecord(updated));
        }
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

    internal IReadOnlyList<OutboxRecord> GetOutboxRecords(string? instanceId = null)
    {
        var records = _outbox.Values
            .Where(record => instanceId is null || string.Equals(record.InstanceId, instanceId, StringComparison.Ordinal))
            .OrderBy(record => record.InstanceId, StringComparer.Ordinal)
            .ThenBy(record => record.StreamVersion)
            .ThenBy(record => record.Sequence)
            .Select(CloneOutboxRecord)
            .ToArray();

        return records;
    }

    internal IReadOnlyList<ProjectionWorkItem> GetProjectionWork(string instanceId)
    {
        if (!_projectionWork.TryGetValue(instanceId, out var workItems))
            return [];

        lock (workItems)
        {
            return workItems.Select(CloneProjectionWorkItem).ToArray();
        }
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
            envelope.PayloadEnvelope is null ? null : CloneSerializedPayloadEnvelope(envelope.PayloadEnvelope),
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
        record with
        {
            PayloadEnvelope = CloneSerializedPayloadEnvelope(record.PayloadEnvelope)
        };

    private static SerializedPayloadEnvelope CloneSerializedPayloadEnvelope(SerializedPayloadEnvelope envelope)
    {
        var body = envelope.Payload.Body.ToArray();
        return new SerializedPayloadEnvelope(
            new DispatchPayload(body, envelope.Payload.ContentType, envelope.Payload.SchemaId),
            envelope.TypeKey);
    }

    private static bool AreEquivalent(OutboxRecord left, OutboxRecord right)
    {
        if (left == right)
            return true;

        return string.Equals(left.OutboxId, right.OutboxId, StringComparison.Ordinal)
            && string.Equals(left.IdempotencyKey, right.IdempotencyKey, StringComparison.Ordinal)
            && string.Equals(left.InstanceId, right.InstanceId, StringComparison.Ordinal)
            && string.Equals(left.ParentInstanceId, right.ParentInstanceId, StringComparison.Ordinal)
            && string.Equals(left.RootInstanceId, right.RootInstanceId, StringComparison.Ordinal)
            && string.Equals(left.GroupId, right.GroupId, StringComparison.Ordinal)
            && string.Equals(left.StreamId, right.StreamId, StringComparison.Ordinal)
            && left.StreamVersion == right.StreamVersion
            && left.Sequence == right.Sequence
            && string.Equals(left.MessageType, right.MessageType, StringComparison.Ordinal)
            && string.Equals(left.Channel, right.Channel, StringComparison.Ordinal)
            && string.Equals(left.Destination, right.Destination, StringComparison.Ordinal)
            && AreEquivalent(left.PayloadEnvelope, right.PayloadEnvelope)
            && string.Equals(left.CorrelationId, right.CorrelationId, StringComparison.Ordinal)
            && string.Equals(left.CausationEventId, right.CausationEventId, StringComparison.Ordinal)
            && string.Equals(left.ResumeTokenId, right.ResumeTokenId, StringComparison.Ordinal);
    }

    private static bool AreEquivalent(SerializedPayloadEnvelope left, SerializedPayloadEnvelope right) =>
        string.Equals(left.TypeKey, right.TypeKey, StringComparison.Ordinal)
        && AreEquivalent(left.Payload, right.Payload);

    private static bool AreEquivalent(DispatchPayload left, DispatchPayload right) =>
        string.Equals(left.ContentType, right.ContentType, StringComparison.Ordinal)
        && string.Equals(left.SchemaId, right.SchemaId, StringComparison.Ordinal)
        && left.Body.Span.SequenceEqual(right.Body.Span);

    private static ProjectionWorkItem CloneProjectionWorkItem(ProjectionWorkItem item) =>
        new(
            item.ProjectionName,
            item.WorkKind,
            new DispatchPayload(item.Payload.Body.ToArray(), item.Payload.ContentType, item.Payload.SchemaId));

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
        {
            var duplicateIndex = staged.FindIndex(existing => string.Equals(existing.EventId, record.EventId, StringComparison.Ordinal));
            if (duplicateIndex >= 0)
            {
                staged[duplicateIndex] = CloneInboxRecord(record);
            }
            else
            {
                staged.Add(CloneInboxRecord(record));
            }
        }

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

    private List<ProjectionWorkItem> StageProjectionWorkItems(string instanceId, IReadOnlyList<ProjectionWorkItem> workItems)
    {
        List<ProjectionWorkItem> staged;
        if (_projectionWork.TryGetValue(instanceId, out var existing))
        {
            lock (existing)
            {
                staged = existing.Select(CloneProjectionWorkItem).ToList();
            }
        }
        else
        {
            staged = [];
        }

        staged.AddRange(workItems.Select(CloneProjectionWorkItem));
        return staged;
    }

    private Dictionary<string, OutboxRecord> StageOutboxRecords(IReadOnlyList<OutboxRecord> outboxRecords)
    {
        var staged = new Dictionary<string, OutboxRecord>(StringComparer.Ordinal);
        foreach (var record in outboxRecords)
        {
            if (!staged.TryAdd(record.OutboxId, CloneOutboxRecord(record)))
                throw new InvalidOperationException($"Outbox record '{record.OutboxId}' already exists.");

            if (_outbox.TryGetValue(record.OutboxId, out var existing))
            {
                if (!AreEquivalent(existing, record))
                    throw new InvalidOperationException($"Outbox record '{record.OutboxId}' already exists.");

                staged.Remove(record.OutboxId);
            }
        }

        return staged;
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

    private void ApplyProjectionWorkItems(string instanceId, IReadOnlyList<ProjectionWorkItem> stagedProjectionWork)
    {
        if (stagedProjectionWork.Count == 0)
        {
            _projectionWork.TryRemove(instanceId, out _);
            return;
        }

        _projectionWork[instanceId] = stagedProjectionWork.Select(CloneProjectionWorkItem).ToList();
    }

    private void ApplyStagedOutboxRecords(IReadOnlyDictionary<string, OutboxRecord> stagedOutbox)
    {
        foreach (var entry in stagedOutbox)
            _outbox[entry.Key] = entry.Value;
    }

    private void NormalizeExpiredLeases(DateTimeOffset now)
    {
        foreach (var record in _outbox.Values
                     .Where(record => record.Status == OutboxStatus.Leased)
                     .Where(record => record.LeaseExpiresAt is not null && record.LeaseExpiresAt <= now)
                     .ToArray())
        {
            _outbox[record.OutboxId] = record with
            {
                Status = OutboxStatus.Pending,
                LeaseOwner = null,
                LeaseExpiresAt = null
            };
        }
    }

    private OutboxRecord GetRequiredOutbox(string outboxId)
    {
        if (!_outbox.TryGetValue(outboxId, out var current))
            throw new KeyNotFoundException($"Outbox record '{outboxId}' not found.");

        return current;
    }

    private static void EnsureLeaseOwnership(OutboxRecord record, string leaseOwner, DateTimeOffset transitionTime)
    {
        if (record.Status != OutboxStatus.Leased
            || !string.Equals(record.LeaseOwner, leaseOwner, StringComparison.Ordinal)
            || record.LeaseExpiresAt is null
            || record.LeaseExpiresAt <= transitionTime)
        {
            throw new ConcurrencyException($"Lease ownership lost for outbox record '{record.OutboxId}'.");
        }
    }

    private static InboxRecord CloneInboxRecord(InboxRecord record) =>
        new(
            record.EventId,
            record.InstanceId,
            CloneEventEnvelope(record.Envelope),
            record.ReceivedAt,
            record.Processed);
}
