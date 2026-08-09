using System.Collections.Concurrent;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using DurableWorkflowEvent = global::OrcaCore.Abstractions.Durable.WorkflowEvent;
using ProjectionWorkflowInstanceSnapshot = global::OrcaCore.Abstractions.Providers.WorkflowProjectionSnapshot;

namespace OrcaCore.TestSupport.Providers;

public sealed class FakeWorkflowEventStore :
    IWorkflowEventStore,
    IWorkflowInboxStore,
    IWorkflowStartIdempotencyStore,
    IWorkflowOutboxStore,
    IWorkflowProjectionStore
{
    private static readonly TimeSpan DefaultLeaseDuration = TimeSpan.FromMinutes(5);

    private readonly object gate = new();
    private readonly ConcurrentDictionary<EventId, InboxRecord> inbox = [];
    private readonly Dictionary<InboxRouteKey, long> inboxRouteRevisions = [];
    private long nextInboxAcceptanceSequence;
    private readonly ConcurrentDictionary<string, StartedWorkflowIdempotencyRecord> startIdempotency =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<OutboxRecordId, FakeOutboxRecord> outbox = [];
    private readonly ConcurrentDictionary<InstanceId, CheckpointWrite> checkpoints = [];
    private readonly ConcurrentDictionary<WorkflowStreamId, List<DurableWorkflowEvent>> streams = [];
    private readonly ConcurrentDictionary<InstanceId, ProjectionWorkflowInstanceSnapshot> projectedSummaries = [];
    private readonly List<ProviderCommitBatch> committedBatches = [];
    private int failNextCommitBeforeApply;

    public IReadOnlyList<ProviderCommitBatch> CommittedBatches
    {
        get
        {
            lock (gate)
            {
                return committedBatches.ToArray();
            }
        }
    }

    public Task<Option<CheckpointWrite>> LoadCheckpointAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(checkpoints.TryGetValue(instanceId, out var checkpoint)
            ? Option<CheckpointWrite>.Some(CloneCheckpointWrite(checkpoint))
            : Option<CheckpointWrite>.None);
    }

    public Task<Result<AppendEventsResult>> AppendAsync(
        ProviderCommitBatch batch,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(batch);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            if (Interlocked.Exchange(ref failNextCommitBeforeApply, 0) == 1)
            {
                return Task.FromResult(EventStoreConflict.ExpectedVersionMismatch(
                    batch.ExpectedVersion,
                    batch.ExpectedVersion));
            }

            var conflictingEventId = batch.InboxOperations
                .Where(operation => operation.EnvelopeFingerprint is not null)
                .Where(operation => inbox.TryGetValue(operation.EventId, out var existing) &&
                    (existing.InstanceId is null || !existing.InstanceId.Equals(batch.StreamId.InstanceId) ||
                     !string.Equals(
                         existing.EnvelopeFingerprint,
                         operation.EnvelopeFingerprint,
                         StringComparison.Ordinal)))
                .Select(operation => operation.EventId)
                .FirstOrDefault();
            if (conflictingEventId is not null)
            {
                return Task.FromResult(EventStoreConflict.EventIdAlreadyExists(conflictingEventId));
            }

            var changedRoute = batch.InboxRouteMutations.FirstOrDefault(mutation =>
                CurrentRouteRevision(mutation.Route) != mutation.ExpectedRevision);
            if (changedRoute is not null)
            {
                return Task.FromResult(EventStoreConflict.InboxRouteChanged(changedRoute.Route));
            }

            var stream = streams.GetOrAdd(batch.StreamId, _ => []);
            var actualVersion = new StreamVersion(stream.Count);
            if (actualVersion != batch.ExpectedVersion)
            {
                return Task.FromResult(EventStoreConflict.ExpectedVersionMismatch(
                    batch.ExpectedVersion,
                    actualVersion));
            }

            if (HasStartIdempotencyConflict(batch.StartIdempotencyOperations))
            {
                return Task.FromResult(EventStoreConflict.StartIdempotencyKeyAlreadyExists(
                    batch.StartIdempotencyOperations[0].IdempotencyKey));
            }

            stream.AddRange(batch.Events);
            foreach (var operation in batch.InboxOperations)
            {
                if (inbox.TryGetValue(operation.EventId, out var existing))
                {
                    inbox[operation.EventId] = existing with
                    {
                        State = existing.State == InboxRecordState.Applied
                            ? existing.State
                            : operation.State,
                        InstanceId = operation.TargetInstanceId ?? existing.InstanceId
                    };
                    continue;
                }

                if (string.IsNullOrWhiteSpace(operation.EnvelopeFingerprint))
                {
                    throw new InvalidOperationException(
                        $"Initial inbox write '{operation.EventId}' for instance " +
                        $"'{batch.StreamId.InstanceId}' must carry an envelope fingerprint.");
                }

                inbox[operation.EventId] = new InboxRecord(
                    batch.StreamId.InstanceId,
                    operation.EventId,
                    operation.EnvelopeFingerprint,
                    operation.State)
                {
                    Envelope = CloneEnvelope(operation.Envelope)
                };
            }


            foreach (var mutation in batch.InboxRouteMutations)
            {
                inboxRouteRevisions[mutation.Route] = mutation.ExpectedRevision + 1;
            }

            foreach (var operation in batch.InboxTargetPoisonOperations)
            {
                foreach (var record in inbox.Values.Where(record =>
                             record.State == InboxRecordState.Received &&
                             record.Route?.Kind == "direct" &&
                             record.Route.InstanceId?.Equals(operation.InstanceId) == true).ToArray())
                {
                    inbox[record.EventId] = record with
                    {
                        State = InboxRecordState.Poisoned,
                        PoisonCode = operation.Code,
                        PoisonDetail = operation.Detail
                    };
                }
            }

            ApplyProjectionOperations(batch.ProjectionOperations);

            foreach (var operation in batch.StartIdempotencyOperations)
            {
                startIdempotency[operation.IdempotencyKey] = new StartedWorkflowIdempotencyRecord(
                    operation.IdempotencyKey,
                    operation.InstanceId,
                    operation.DefinitionId,
                    operation.DefinitionVersion,
                    operation.DefinitionFingerprint,
                    operation.InputFingerprint);
            }

            foreach (var record in batch.OutboxRecords)
            {
                outbox[record.OutboxRecordId] = new FakeOutboxRecord(
                    record with { Payload = [.. record.Payload] },
                    OutboxRecordState.Pending);
            }

            if (batch.Checkpoint is { } checkpoint)
            {
                checkpoints[checkpoint.InstanceId] = CloneCheckpointWrite(checkpoint);
            }

            committedBatches.Add(batch);

            return Task.FromResult(Result<AppendEventsResult>.Success(
                new AppendEventsResult(new StreamVersion(stream.Count))));
        }
    }

    public Task<IReadOnlyList<DurableWorkflowEvent>> LoadTailAsync(
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
            return Task.FromResult<IReadOnlyList<DurableWorkflowEvent>>(events);
        }
    }

    public Task<Option<InboxRecord>> GetAsync(
        InstanceId instanceId,
        EventId eventId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(inbox.TryGetValue(eventId, out var record) && record.InstanceId?.Equals(instanceId) == true
            ? Option<InboxRecord>.Some(record)
            : Option<InboxRecord>.None);
    }

    public Task<Option<InboxRecord>> GetByEventIdAsync(
        EventId eventId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(inbox.TryGetValue(eventId, out var record)
            ? Option<InboxRecord>.Some(record)
            : Option<InboxRecord>.None);
    }

    public Task<InboxAcceptanceCommitResult> AcceptAsync(
        InboxAcceptance acceptance,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (inbox.TryGetValue(acceptance.Envelope.EventId, out var existing))
            {
                return Task.FromResult(new InboxAcceptanceCommitResult(
                    string.Equals(existing.EnvelopeFingerprint, acceptance.EnvelopeFingerprint, StringComparison.Ordinal)
                        ? InboxAcceptanceCommitDisposition.Duplicate
                        : InboxAcceptanceCommitDisposition.Conflict,
                    existing));
            }

            var route = CreateInboxRoute(acceptance.Envelope);
            if (route.Kind == "direct")
            {
                var instanceId = route.InstanceId ?? throw new InvalidOperationException(
                    "A direct inbox route requires an instance.");
                if (!projectedSummaries.TryGetValue(instanceId, out var target))
                {
                    return Task.FromResult(new InboxAcceptanceCommitResult(
                        InboxAcceptanceCommitDisposition.DirectInstanceNotFound,
                        null));
                }

                if (IsTerminal(target.Status))
                {
                    return Task.FromResult(new InboxAcceptanceCommitResult(
                        InboxAcceptanceCommitDisposition.DirectInstanceTerminal,
                        null));
                }
            }

            var record = new InboxRecord(
                acceptance.Envelope.Route.InstanceId,
                acceptance.Envelope.EventId,
                acceptance.EnvelopeFingerprint,
                InboxRecordState.Received)
            {
                Envelope = CloneEnvelope(acceptance.Envelope),
                Route = route,
                AcceptanceSequence = ++nextInboxAcceptanceSequence,
                AcceptedAt = acceptance.AcceptedAt
            };
            inbox[record.EventId] = record;
            inboxRouteRevisions[route] = CurrentRouteRevision(route) + 1;
            return Task.FromResult(new InboxAcceptanceCommitResult(InboxAcceptanceCommitDisposition.Accepted, record));
        }
    }

    public Task<InboxMatchSnapshot> GetMatchSnapshotAsync(
        InboxMatchRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            var routes = request.Routes.ToHashSet();
            var pending = inbox.Values
                .Where(record => record.State == InboxRecordState.Received &&
                    record.Route is not null && routes.Contains(record.Route))
                .OrderBy(record => record.AcceptanceSequence)
                .ThenBy(record => record.EventId.Value, StringComparer.Ordinal)
                .FirstOrDefault();
            return Task.FromResult(new InboxMatchSnapshot(
                pending,
                request.Routes.Select(route => new InboxRouteRevision(route, CurrentRouteRevision(route))).ToArray()));
        }
    }

    public Task<IReadOnlyList<InboxRecord>> ListReceivedAsync(
        long afterAcceptanceSequence,
        int maxCount,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(afterAcceptanceSequence);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCount);
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            return Task.FromResult<IReadOnlyList<InboxRecord>>(
                inbox.Values
                    .Where(record => record.State == InboxRecordState.Received &&
                        record.AcceptanceSequence > afterAcceptanceSequence)
                    .OrderBy(record => record.AcceptanceSequence)
                    .ThenBy(record => record.EventId.Value, StringComparer.Ordinal)
                    .Take(maxCount)
                    .ToArray());
        }
    }

    public Task<IReadOnlyList<InboxRecord>> ListHandoffRetriesAsync(
        DateTimeOffset eligibleAt,
        int maxCount,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCount);
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            return Task.FromResult<IReadOnlyList<InboxRecord>>(
                inbox.Values
                    .Where(record => record.State == InboxRecordState.Received &&
                        record.HandoffFailureCount > 0 &&
                        record.HandoffRetryNotBefore <= eligibleAt)
                    .OrderBy(record => record.HandoffRetryNotBefore)
                    .ThenBy(record => record.AcceptanceSequence)
                    .ThenBy(record => record.EventId.Value, StringComparer.Ordinal)
                    .Take(maxCount)
                    .ToArray());
        }
    }

    public Task MarkPoisonedAsync(
        EventId eventId,
        InboxRecordState expectedState,
        string code,
        string? detail,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (inbox.TryGetValue(eventId, out var record) && record.State == expectedState)
            {
                inbox[eventId] = record with
                {
                    State = InboxRecordState.Poisoned,
                    PoisonCode = code,
                    PoisonDetail = detail
                };
                if (record.Route is { } route)
                {
                    inboxRouteRevisions[route] = CurrentRouteRevision(route) + 1;
                }
            }
        }

        return Task.CompletedTask;
    }

    public Task RecordHandoffFailureAsync(
        EventId eventId,
        InboxRecordState expectedState,
        int expectedFailureCount,
        int maxFailureCount,
        DateTimeOffset retryNotBefore,
        string code,
        string? detail,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(expectedFailureCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFailureCount);
        if (expectedFailureCount >= maxFailureCount)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedFailureCount));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (!inbox.TryGetValue(eventId, out var record) ||
                record.State != expectedState ||
                record.HandoffFailureCount != expectedFailureCount)
            {
                return Task.CompletedTask;
            }

            var failureCount = expectedFailureCount + 1;
            var poisoned = failureCount >= maxFailureCount;
            inbox[eventId] = record with
            {
                State = poisoned ? InboxRecordState.Poisoned : record.State,
                HandoffFailureCount = failureCount,
                HandoffRetryNotBefore = poisoned ? null : retryNotBefore,
                PoisonCode = poisoned ? code : record.PoisonCode,
                PoisonDetail = poisoned ? detail : record.PoisonDetail
            };
            if (poisoned && record.Route is { } route)
            {
                inboxRouteRevisions[route] = CurrentRouteRevision(route) + 1;
            }
        }

        return Task.CompletedTask;
    }

    private long CurrentRouteRevision(InboxRouteKey route) =>
        inboxRouteRevisions.TryGetValue(route, out var revision) ? revision : 0;

    private static InboxRouteKey CreateInboxRoute(DurableEventEnvelope envelope) =>
        envelope.Route.Kind switch
        {
            "direct" => InboxRouteKey.Direct(
                envelope.Route.InstanceId!,
                EventName.Create(envelope.EventName),
                new EventContractVersion(envelope.EventContractVersion),
                envelope.CorrelationId),
            "correlation" => InboxRouteKey.Correlation(
                envelope.Route.DefinitionId!,
                EventName.Create(envelope.EventName),
                new EventContractVersion(envelope.EventContractVersion),
                envelope.CorrelationId),
            _ => throw new NotSupportedException()
        };

    public Task<Option<StartedWorkflowIdempotencyRecord>> GetStartedAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(startIdempotency.TryGetValue(idempotencyKey, out var record)
            ? Option<StartedWorkflowIdempotencyRecord>.Some(record)
            : Option<StartedWorkflowIdempotencyRecord>.None);
    }

    private bool HasStartIdempotencyConflict(IReadOnlyList<StartIdempotencyWrite> operations)
    {
        return operations
            .Select(operation => operation.IdempotencyKey)
            .GroupBy(key => key, StringComparer.Ordinal)
            .Any(group => group.Count() > 1 || startIdempotency.ContainsKey(group.Key));
    }

    public Task<IReadOnlyList<OutboxWrite>> ClaimAsync(int maxCount, CancellationToken cancellationToken)
    {
        return ClaimAsync(
            new OutboxClaimRequest(maxCount, DateTimeOffset.UtcNow, DefaultLeaseDuration),
            cancellationToken);
    }

    public Task<IReadOnlyList<OutboxWrite>> ClaimAsync(
        OutboxClaimRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegative(request.MaxCount);
        ThrowIfInvalidLease(request.LeaseDuration);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            var claimed = outbox.Values
                .Where(record => IsOutboxClaimable(record, request.ClaimedAt))
                .Where(record => request.KindSelector is null || request.KindSelector.Matches(record.Write.Kind))
                .Take(request.MaxCount)
                .Select(record => record.Write with { Payload = [.. record.Write.Payload] })
                .ToArray();
            foreach (var record in claimed)
            {
                outbox[record.OutboxRecordId] = outbox[record.OutboxRecordId] with
                {
                    State = OutboxRecordState.Claimed,
                    ClaimedUntil = request.ClaimedAt.Add(request.LeaseDuration)
                };
            }

            return Task.FromResult<IReadOnlyList<OutboxWrite>>(claimed);
        }
    }

    public Task<Option<OutboxRecordState>> GetStateAsync(
        OutboxRecordId outboxRecordId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(outbox.TryGetValue(outboxRecordId, out var record)
            ? Option<OutboxRecordState>.Some(record.State)
            : Option<OutboxRecordState>.None);
    }

    public Task<Option<OutboxDispatchSnapshot>> GetDispatchSnapshotAsync(
        OutboxRecordId outboxRecordId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(outbox.TryGetValue(outboxRecordId, out var record)
            ? Option<OutboxDispatchSnapshot>.Some(new OutboxDispatchSnapshot(record.State)
            {
                PoisonCode = record.PoisonCode,
                PoisonDetail = record.PoisonDetail
            })
            : Option<OutboxDispatchSnapshot>.None);
    }

    public Task MarkAsync(
        OutboxRecordId outboxRecordId,
        OutboxRecordState state,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (outbox.TryGetValue(outboxRecordId, out var record) &&
            record.State != OutboxRecordState.Poisoned)
        {
            outbox[outboxRecordId] = record with
            {
                State = state,
                ClaimedUntil = state == OutboxRecordState.Claimed ? record.ClaimedUntil : null
            };
        }

        return Task.CompletedTask;
    }

    public Task ReleaseAsync(OutboxRecordId outboxRecordId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (outbox.TryGetValue(outboxRecordId, out var record) &&
            record.State == OutboxRecordState.Claimed)
        {
            outbox[outboxRecordId] = record with
            {
                State = OutboxRecordState.Retryable,
                ClaimedUntil = null
            };
        }

        return Task.CompletedTask;
    }

    public Task ApplyAsync(IReadOnlyList<ProjectionWrite> operations, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            ApplyProjectionOperations(operations);
        }

        return Task.CompletedTask;
    }

    public Task MarkPoisonedAsync(
        OutboxRecordId outboxRecordId,
        string code,
        string? detail,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        cancellationToken.ThrowIfCancellationRequested();

        if (outbox.TryGetValue(outboxRecordId, out var record) &&
            record.State == OutboxRecordState.Claimed)
        {
            outbox[outboxRecordId] = record with
            {
                State = OutboxRecordState.Poisoned,
                ClaimedUntil = null,
                PoisonCode = code,
                PoisonDetail = detail
            };
        }

        return Task.CompletedTask;
    }

    private void ApplyProjectionOperations(IEnumerable<ProjectionWrite> operations)
    {
        foreach (var operation in operations)
        {
            if (operation is { Kind: ProjectionOperationKind.UpsertSummary, InstanceSnapshot: { } snapshot })
            {
                projectedSummaries[operation.InstanceId] = snapshot;
            }
        }
    }

    public Task<Option<ProjectionWorkflowInstanceSnapshot>> GetAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(projectedSummaries.TryGetValue(instanceId, out var snapshot)
            ? Option<ProjectionWorkflowInstanceSnapshot>.Some(snapshot)
            : Option<ProjectionWorkflowInstanceSnapshot>.None);
    }

    public Task<IReadOnlyList<ProjectionWorkflowInstanceSnapshot>> FindActiveWaitsAsync(
        DefinitionId? definitionId,
        EventName eventName,
        CorrelationId correlationId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<ProjectionWorkflowInstanceSnapshot>>(
            projectedSummaries.Values
                .Where(snapshot => definitionId is null || snapshot.DefinitionId.Equals(definitionId))
                .Where(snapshot => snapshot.ActiveWaits.Any(wait =>
                    string.Equals(wait.EventName, eventName.Value, StringComparison.Ordinal) &&
                    wait.CorrelationId.Equals(correlationId)))
                .OrderBy(snapshot => snapshot.InstanceId.Value)
                .ToArray());
    }

    public Task<IReadOnlyList<ProjectionWorkflowInstanceSnapshot>> ListLeaseRecoveryCandidatesAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<ProjectionWorkflowInstanceSnapshot>>(
            projectedSummaries.Values
                .OrderBy(snapshot => snapshot.InstanceId.Value)
                .ToArray());
    }

    public void FailNextCommitBeforeApply()
    {
        Interlocked.Exchange(ref failNextCommitBeforeApply, 1);
    }

    private static DurableEventEnvelope? CloneEnvelope(DurableEventEnvelope? envelope)
    {
        return envelope is null
            ? null
            : envelope with
            {
                Payload = envelope.Payload is null ? null : [.. envelope.Payload],
                Route = envelope.Route with
                {
                    WorkflowInputPayload = envelope.Route.WorkflowInputPayload is null
                        ? null
                        : [.. envelope.Route.WorkflowInputPayload]
                }
            };
    }

    private static bool IsTerminal(global::OrcaCore.WorkflowInstanceStatus status) =>
        status is global::OrcaCore.WorkflowInstanceStatus.Completed or
            global::OrcaCore.WorkflowInstanceStatus.Failed or
            global::OrcaCore.WorkflowInstanceStatus.TimedOut or
            global::OrcaCore.WorkflowInstanceStatus.Cancelled or
            global::OrcaCore.WorkflowInstanceStatus.Terminated;

    private static CheckpointWrite CloneCheckpointWrite(CheckpointWrite checkpoint)
    {
        return checkpoint with
        {
            Payload = [.. checkpoint.Payload],
            RuntimeState = new WorkflowRuntimeCheckpointState
            {
                ActiveTimers = checkpoint.RuntimeState.ActiveTimers.Select(timer => timer with { }).ToArray(),
                ActiveWaits = checkpoint.RuntimeState.ActiveWaits.Select(wait => wait with { }).ToArray(),
                ActiveResourceTickets = checkpoint.RuntimeState.ActiveResourceTickets.Select(ticket => ticket with { }).ToArray(),
                PendingResumes = checkpoint.RuntimeState.PendingResumes.Select(resume => resume with
                {
                    Payload = resume.Payload is null ? null : [.. resume.Payload]
                }).ToArray(),
                ContinuationFailureCount = checkpoint.RuntimeState.ContinuationFailureCount,
                ContinuationFailurePositionStreamVersion = checkpoint.RuntimeState.ContinuationFailurePositionStreamVersion,
                ContinuationRetryNotBefore = checkpoint.RuntimeState.ContinuationRetryNotBefore
            }
        };
    }

    private static bool IsOutboxClaimable(FakeOutboxRecord record, DateTimeOffset claimedAt)
    {
        return record.State is OutboxRecordState.Pending or OutboxRecordState.Retryable ||
            (record.State == OutboxRecordState.Claimed &&
                (record.ClaimedUntil is null || record.ClaimedUntil <= claimedAt));
    }

    private static void ThrowIfInvalidLease(TimeSpan leaseDuration)
    {
        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration), leaseDuration, "Lease duration must be positive.");
        }
    }

    private sealed record FakeOutboxRecord(
        OutboxWrite Write,
        OutboxRecordState State,
        DateTimeOffset? ClaimedUntil = null,
        string? PoisonCode = null,
        string? PoisonDetail = null);
}
