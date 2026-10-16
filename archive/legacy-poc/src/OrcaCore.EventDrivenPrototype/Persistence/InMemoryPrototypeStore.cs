using OrcaCore.Abstractions.Enums;
using OrcaCore.Abstractions.Models;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.EventDrivenPrototype.Engine;
using OrcaCore.EventDrivenPrototype.Projections;

namespace OrcaCore.EventDrivenPrototype.Persistence;

public sealed class InMemoryPrototypeStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, List<PrototypeStreamRecord>> _streams = [];
    private readonly Dictionary<string, PrototypeCheckpointState> _checkpoints = [];
    private readonly Dictionary<string, List<PrototypeInboxRecord>> _inboxes = [];
    private readonly Dictionary<string, InstanceSummaryProjection> _summaries = [];
    private readonly Dictionary<string, List<ActiveWaitProjection>> _activeWaits = [];

    internal Task<PrototypeCheckpointState?> LoadCheckpointAsync(string instanceId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            return Task.FromResult(_checkpoints.TryGetValue(instanceId, out var checkpoint)
                ? CloneCheckpoint(checkpoint)
                : null);
        }
    }

    public Task<IReadOnlyList<PrototypeStreamRecord>> LoadStreamAsync(string instanceId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<PrototypeStreamRecord>>(
                _streams.TryGetValue(instanceId, out var stream)
                    ? stream.Select(CloneStreamRecord).ToArray()
                    : []);
        }
    }

    public Task<IReadOnlyList<PrototypeInboxRecord>> GetInboxAsync(string instanceId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<PrototypeInboxRecord>>(
                _inboxes.TryGetValue(instanceId, out var inbox)
                    ? inbox.ToArray()
                    : []);
        }
    }

    internal Task<InstanceSummaryProjection?> GetSummaryAsync(string instanceId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            return Task.FromResult(_summaries.TryGetValue(instanceId, out var summary) ? summary : null);
        }
    }

    internal Task<IReadOnlyList<ActiveWaitProjection>> GetActiveWaitsAsync(string instanceId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<ActiveWaitProjection>>(
                _activeWaits.TryGetValue(instanceId, out var waits)
                    ? waits.ToArray()
                    : []);
        }
    }

    internal Task<Option<string>> TryResolveByCorrelationAsync(string eventName, string correlationId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            var matches = _activeWaits.Values
                .SelectMany(static x => x)
                .Where(x => x.EventName == eventName && x.CorrelationId == correlationId)
                .Select(x => x.InstanceId)
                .Distinct()
                .ToArray();

            return matches.Length switch
            {
                0 => Task.FromResult(Option<string>.None),
                1 => Task.FromResult(Option<string>.Some(matches[0])),
                _ => throw new InvalidOperationException($"Ambiguous correlation for event '{eventName}' and correlation '{correlationId}'.")
            };
        }
    }

    internal async Task<string?> ResolveByCorrelationAsync(string eventName, string correlationId, CancellationToken cancellationToken)
    {
        var resolved = await TryResolveByCorrelationAsync(eventName, correlationId, cancellationToken);
        return resolved.HasValue ? resolved.Value : null;
    }

    internal Task CommitAsync(
        string instanceId,
        PrototypeCheckpointState newCheckpoint,
        IReadOnlyList<PrototypeStreamRecord> newEvents,
        IReadOnlyList<PrototypeInboxRecord> newInboxRecords,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (!_streams.TryGetValue(instanceId, out var stream))
            {
                stream = [];
                _streams[instanceId] = stream;
            }

            foreach (var record in newEvents)
            {
                stream.Add(CloneStreamRecord(record));
            }

            _checkpoints[instanceId] = CloneCheckpoint(newCheckpoint);

            if (!_inboxes.TryGetValue(instanceId, out var inbox))
            {
                inbox = [];
                _inboxes[instanceId] = inbox;
            }

            foreach (var record in newInboxRecords)
            {
                var existingIndex = inbox.FindIndex(x => x.EventId == record.EventId);

                if (existingIndex >= 0)
                {
                    inbox[existingIndex] = record;
                }
                else
                {
                    inbox.Add(record);
                }
            }

            _summaries[instanceId] = new InstanceSummaryProjection(
                newCheckpoint.InstanceId,
                newCheckpoint.DefinitionId,
                newCheckpoint.DefinitionVersion,
                newCheckpoint.Status,
                newCheckpoint.ActiveWaits.Count,
                newCheckpoint.StreamVersion,
                DateTimeOffset.UtcNow);

            _activeWaits[instanceId] = newCheckpoint.ActiveWaits
                .Where(x => x.Status == WaitStatus.Active)
                .Select(x => new ActiveWaitProjection(
                    instanceId,
                    newCheckpoint.DefinitionId,
                    newCheckpoint.DefinitionVersion,
                    x.WaitId,
                    x.EventName,
                    x.CorrelationId,
                    x.Mode,
                    x.RegisteredAt))
                .ToList();
        }

        return Task.CompletedTask;
    }

    private static PrototypeCheckpointState CloneCheckpoint(PrototypeCheckpointState checkpoint)
    {
        return checkpoint with
        {
            BusinessState = PrototypeCloner.CloneObject(checkpoint.BusinessState, checkpoint.BusinessStateType),
            ActiveWaits = checkpoint.ActiveWaits.ToArray(),
            PendingEvents = checkpoint.PendingEvents.ToArray(),
            ConsumedEventIds = checkpoint.ConsumedEventIds.ToHashSet(StringComparer.Ordinal)
        };
    }

    private static PrototypeStreamRecord CloneStreamRecord(PrototypeStreamRecord record)
        => record with { Data = new Dictionary<string, object?>(record.Data, StringComparer.Ordinal) };
}
