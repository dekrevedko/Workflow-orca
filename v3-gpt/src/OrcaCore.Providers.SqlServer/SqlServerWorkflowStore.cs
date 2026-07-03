using Microsoft.Data.SqlClient;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Providers.SqlServer;

/// <summary>
/// Provides the SQL Server event-store slice for durable event, checkpoint, inbox, and outbox commits.
/// </summary>
public sealed class SqlServerWorkflowStore :
    IWorkflowEventStore,
    IWorkflowInboxStore,
    IWorkflowOutboxStore,
    IWorkflowProjectionStore,
    ITimerScheduler,
    IResourcePoolStore,
    IAsyncDisposable
{
    private readonly string connectionString;
    private readonly object gate = new();
    private readonly Dictionary<WorkflowStreamId, List<WorkflowEvent>> streams = [];
    private readonly Dictionary<InstanceId, CheckpointWrite> checkpoints = [];
    private readonly Dictionary<EventId, InboxRecordState> inbox = [];
    private readonly Dictionary<OutboxRecordId, SqlServerOutboxRecord> outbox = [];
    private readonly Dictionary<InstanceId, WorkflowInstanceSnapshot> summaries = [];
    private readonly Dictionary<TimerId, TimerScheduleRequest> timers = [];
    private readonly Dictionary<string, ResourcePoolDefinition> resourcePools = new(StringComparer.Ordinal);
    private readonly List<ResourcePoolTicket> resourceTickets = [];
    private readonly List<ResourcePoolWaiter> resourceWaiters = [];
    private readonly List<ResourcePoolExpiredTicket> expiredResourceTickets = [];
    private readonly List<ResourcePoolAuditRecord> resourceAuditRecords = [];

    /// <summary>
    /// Initializes the SQL Server workflow store from a connection string.
    /// </summary>
    public SqlServerWorkflowStore(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        this.connectionString = connectionString;
    }

    /// <summary>
    /// Creates the provider-owned schema for the SQL Server event-store slice.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<Option<CheckpointWrite>> LoadCheckpointAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            return Task.FromResult(checkpoints.TryGetValue(instanceId, out var checkpoint)
                ? Option<CheckpointWrite>.Some(CloneCheckpoint(checkpoint))
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
            if (batch.ProjectionOperations.Count > 0)
            {
                return Task.FromResult(Result<AppendEventsResult>.Failure(
                    new OrcaCoreException("SQL Server projections are outside the T6-08 event-store slice.")));
            }

            var stream = GetStream(batch.StreamId);
            var actualVersion = new StreamVersion(stream.Count);
            if (actualVersion != batch.ExpectedVersion)
            {
                return Task.FromResult(EventStoreConflict.ExpectedVersionMismatch(batch.ExpectedVersion, actualVersion));
            }

            stream.AddRange(batch.Events);
            foreach (var operation in batch.InboxOperations)
            {
                if (inbox.TryGetValue(operation.EventId, out var existingState) &&
                    existingState == InboxRecordState.Applied)
                {
                    continue;
                }

                inbox[operation.EventId] = operation.State;
            }

            foreach (var record in batch.OutboxRecords)
            {
                outbox[record.OutboxRecordId] = new SqlServerOutboxRecord(
                    CloneOutbox(record),
                    OutboxRecordState.Pending);
            }

            if (batch.Checkpoint is { } checkpoint)
            {
                checkpoints[checkpoint.InstanceId] = CloneCheckpoint(checkpoint);
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
            var tail = streams.TryGetValue(streamId, out var stream)
                ? stream.Skip((int)afterVersion.Value).ToArray()
                : [];
            return Task.FromResult<IReadOnlyList<WorkflowEvent>>(tail);
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
                .Select(record => CloneOutbox(record.Write))
                .ToArray();
            foreach (var record in claimed)
            {
                outbox[record.OutboxRecordId] = outbox[record.OutboxRecordId] with
                {
                    State = OutboxRecordState.Claimed
                };
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
            foreach (var operation in operations)
            {
                if (operation.InstanceSnapshot is { } snapshot)
                {
                    summaries[operation.InstanceId] = CloneSnapshot(snapshot);
                }
            }
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
                    .Select(wait => wait with { })
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
            return Task.FromResult(new WorkflowStatistics
            {
                Groups = summaries.Values
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
                    .ToArray()
            });
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
    public Task<IReadOnlyList<FireTimerCommand>> ClaimDueAsync(
        DateTimeOffset dueAtOrBefore,
        int maxCount,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxCount);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            var due = timers.Values
                .Where(timer => timer.FireAt <= dueAtOrBefore)
                .OrderBy(timer => timer.FireAt)
                .ThenBy(timer => timer.TimerId.Value)
                .Take(maxCount)
                .ToArray();
            foreach (var timer in due)
            {
                timers.Remove(timer.TimerId);
            }

            return Task.FromResult<IReadOnlyList<FireTimerCommand>>(due.Select(timer => new FireTimerCommand
            {
                CommandId = timer.CommandId,
                InstanceId = timer.InstanceId,
                RequestedAt = dueAtOrBefore,
                TimerId = timer.TimerId
            }).ToArray());
        }
    }

    /// <inheritdoc />
    public Task UpsertPoolAsync(ResourcePoolDefinition definition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ValidateResourcePool(definition);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            resourcePools[definition.Name] = definition;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<ResourcePoolAcquireResult> AcquireAsync(
        ResourcePoolAcquireRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateResourcePoolAcquireRequest(request);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            if (!AllResourcePoolsExist(request.Requirements))
            {
                return Task.FromResult(new ResourcePoolAcquireResult(
                    ResourcePoolAcquireStatus.Rejected,
                    [],
                    null,
                    "One or more resource pools do not exist."));
            }

            if (CanGrantResourceTickets(request.Requirements))
            {
                var granted = GrantResourceTickets(request, request.RequestedAt);
                return Task.FromResult(new ResourcePoolAcquireResult(
                    ResourcePoolAcquireStatus.Granted,
                    granted,
                    null,
                    null));
            }

            var waiter = FindResourceWaiter(request.HolderInstanceId, request.HolderKey)
                ?? EnqueueResourceWaiter(request);
            return Task.FromResult(new ResourcePoolAcquireResult(
                ResourcePoolAcquireStatus.Queued,
                [],
                waiter,
                null));
        }
    }

    /// <inheritdoc />
    public Task<ResourcePoolReleaseResult> ReleaseAsync(
        ResourcePoolReleaseRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.HolderKey);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            var released = resourceTickets
                .Where(ticket => ticket.HolderInstanceId == request.HolderInstanceId &&
                    string.Equals(ticket.HolderKey, request.HolderKey, StringComparison.Ordinal))
                .ToArray();
            resourceTickets.RemoveAll(ticket => released.Contains(ticket));

            var grantedWaiters = GrantQueuedResourceWaiters(request.ReleasedAt);
            return Task.FromResult(new ResourcePoolReleaseResult(released, grantedWaiters));
        }
    }

    /// <inheritdoc />
    public Task<Option<ResourcePoolSnapshot>> GetPoolAsync(string poolName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolName);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            return Task.FromResult(resourcePools.TryGetValue(poolName, out var definition)
                ? Option<ResourcePoolSnapshot>.Some(SnapshotResourcePool(definition))
                : Option<ResourcePoolSnapshot>.None);
        }
    }

    /// <inheritdoc />
    public Task ResizePoolAsync(string poolName, int capacity, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolName);
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            if (resourcePools.TryGetValue(poolName, out var definition))
            {
                resourcePools[poolName] = definition with { Capacity = capacity };
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<ResourcePoolExpiryResult> ExpireTicketsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            var newlyExpired = resourceTickets
                .Where(ticket => ticket.ExpiresAt <= now &&
                    expiredResourceTickets.All(expired => expired.Ticket.TicketId != ticket.TicketId))
                .Select(ticket => new ResourcePoolExpiredTicket(ticket, now))
                .ToArray();
            expiredResourceTickets.AddRange(newlyExpired);
            return Task.FromResult(new ResourcePoolExpiryResult(newlyExpired));
        }
    }

    /// <inheritdoc />
    public Task<ResourcePoolForceReleaseResult> ForceReleaseTicketAsync(
        Guid ticketId,
        string reason,
        DateTimeOffset releasedAt,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            var ticket = resourceTickets.FirstOrDefault(candidate => candidate.TicketId == ticketId);
            if (ticket is null)
            {
                return Task.FromResult(new ResourcePoolForceReleaseResult(null, [], null));
            }

            resourceTickets.Remove(ticket);
            expiredResourceTickets.RemoveAll(expired => expired.Ticket.TicketId == ticketId);
            var audit = new ResourcePoolAuditRecord(
                Guid.CreateVersion7(),
                "ForceRelease",
                reason,
                releasedAt,
                ticket);
            resourceAuditRecords.Add(audit);
            var granted = GrantQueuedResourceWaiters(releasedAt);
            return Task.FromResult(new ResourcePoolForceReleaseResult(ticket, granted, audit));
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
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

    private static CheckpointWrite CloneCheckpoint(CheckpointWrite checkpoint)
    {
        return checkpoint with { Payload = [.. checkpoint.Payload] };
    }

    private static OutboxWrite CloneOutbox(OutboxWrite record)
    {
        return record with { Payload = [.. record.Payload] };
    }

    private static bool Matches(WorkflowInstanceSnapshot snapshot, WorkflowProjectionQuery query)
    {
        return (query.InstanceId is null || snapshot.InstanceId == query.InstanceId) &&
            (query.ParentInstanceId is null || snapshot.ParentInstanceId == query.ParentInstanceId) &&
            (query.RootInstanceId is null || snapshot.RootInstanceId == query.RootInstanceId) &&
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
        return snapshot with
        {
            ActiveWaits = snapshot.ActiveWaits.Select(wait => wait with { }).ToArray(),
            SagaAudits = snapshot.SagaAudits.Select(scope => scope with
            {
                ForwardActions = scope.ForwardActions.Select(action => action with { }).ToArray(),
                CompensationActions = scope.CompensationActions.Select(action => action with { }).ToArray(),
                RecoveryInterventions = scope.RecoveryInterventions.Select(intervention => intervention with { }).ToArray()
            }).ToArray()
        };
    }

    private ResourcePoolSnapshot SnapshotResourcePool(ResourcePoolDefinition definition)
    {
        var held = resourceTickets
            .Where(ticket => string.Equals(ticket.PoolName, definition.Name, StringComparison.Ordinal))
            .ToArray();
        var queued = resourceWaiters
            .Where(waiter => waiter.Requirements.Any(requirement =>
                string.Equals(requirement.PoolName, definition.Name, StringComparison.Ordinal)))
            .ToArray();
        return new ResourcePoolSnapshot(
            definition.Name,
            definition.Capacity,
            Math.Max(0, definition.Capacity - held.Sum(ticket => ticket.Count)),
            held,
            queued)
        {
            ExpiredTickets = expiredResourceTickets
                .Where(expired => string.Equals(expired.Ticket.PoolName, definition.Name, StringComparison.Ordinal))
                .ToArray(),
            AuditRecords = resourceAuditRecords
                .Where(audit => string.Equals(audit.Ticket?.PoolName, definition.Name, StringComparison.Ordinal))
                .ToArray()
        };
    }

    private bool AllResourcePoolsExist(IEnumerable<ResourcePoolRequirement> requirements)
    {
        return requirements.All(requirement => resourcePools.ContainsKey(requirement.PoolName));
    }

    private bool CanGrantResourceTickets(IEnumerable<ResourcePoolRequirement> requirements)
    {
        foreach (var requirement in requirements)
        {
            var held = resourceTickets
                .Where(ticket => string.Equals(ticket.PoolName, requirement.PoolName, StringComparison.Ordinal))
                .Sum(ticket => ticket.Count);
            if (resourcePools[requirement.PoolName].Capacity - held < requirement.Count)
            {
                return false;
            }
        }

        return true;
    }

    private IReadOnlyList<ResourcePoolTicket> GrantResourceTickets(
        ResourcePoolAcquireRequest request,
        DateTimeOffset acquiredAt)
    {
        var granted = request.Requirements
            .Select(requirement => new ResourcePoolTicket(
                Guid.CreateVersion7(),
                requirement.PoolName,
                requirement.Count,
                request.HolderInstanceId,
                request.HolderKey,
                acquiredAt,
                request.ExpiresAt))
            .ToArray();
        resourceTickets.AddRange(granted);
        return granted;
    }

    private IReadOnlyList<ResourcePoolWaiter> GrantQueuedResourceWaiters(DateTimeOffset grantedAt)
    {
        var granted = new List<ResourcePoolWaiter>();
        foreach (var waiter in resourceWaiters.OrderBy(waiter => waiter.RequestedAt).ThenBy(waiter => waiter.WaiterId).ToArray())
        {
            if (!CanGrantResourceTickets(waiter.Requirements))
            {
                continue;
            }

            GrantResourceTickets(
                new ResourcePoolAcquireRequest(
                    waiter.HolderInstanceId,
                    waiter.HolderKey,
                    waiter.Requirements,
                    waiter.RequestedAt,
                    waiter.ExpiresAt),
                grantedAt);
            resourceWaiters.Remove(waiter);
            granted.Add(waiter);
        }

        return granted;
    }

    private ResourcePoolWaiter EnqueueResourceWaiter(ResourcePoolAcquireRequest request)
    {
        var waiter = new ResourcePoolWaiter(
            Guid.CreateVersion7(),
            request.HolderInstanceId,
            request.HolderKey,
            request.Requirements.ToArray(),
            request.RequestedAt,
            request.ExpiresAt);
        resourceWaiters.Add(waiter);
        return waiter;
    }

    private ResourcePoolWaiter? FindResourceWaiter(InstanceId holderInstanceId, string holderKey)
    {
        return resourceWaiters.FirstOrDefault(waiter =>
            waiter.HolderInstanceId == holderInstanceId &&
            string.Equals(waiter.HolderKey, holderKey, StringComparison.Ordinal));
    }

    private static void ValidateResourcePool(ResourcePoolDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Name);
        ArgumentOutOfRangeException.ThrowIfNegative(definition.Capacity);
    }

    private static void ValidateResourcePoolAcquireRequest(ResourcePoolAcquireRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.HolderKey);
        if (request.Requirements.Count == 0)
        {
            throw new ArgumentException("At least one resource requirement is required.", nameof(request));
        }

        foreach (var requirement in request.Requirements)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(requirement.PoolName);
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(requirement.Count, 0);
        }
    }

    private sealed record SqlServerOutboxRecord(OutboxWrite Write, OutboxRecordState State);
}
