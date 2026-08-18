using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Diagnostics;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Serialization;
using DurableWorkflowEvent = global::OrcaCore.Abstractions.Durable.WorkflowEvent;

namespace OrcaCore.Providers.SqlServer.Internal;

/// <summary>
/// Applies the certified workflow-port transitions to one transactionally loaded SQL Server state document.
/// </summary>
internal sealed class SqlServerWorkflowStateEngine :
    IWorkflowEventStore,
    IWorkflowInboxStore,
    IWorkflowStartIdempotencyStore,
    IWorkflowOutboxStore,
    IWorkflowProjectionStore,
    IWorkflowOperationalStore,
    IWorkflowProviderMaintenanceStore,
    ITimerScheduler,
    IMessageDispatcher
{
    private static readonly TimeSpan DefaultLeaseDuration = TimeSpan.FromMinutes(5);

    private readonly Lock gate = new();
    private readonly Dictionary<EventId, InboxRecord> inbox = [];
    private readonly Dictionary<(EventId EventId, InstanceId InstanceId), InboxRecord> definitionFanoutTargets = [];
    private readonly Dictionary<InboxRouteKey, long> inboxRouteRevisions = [];
    private long nextInboxAcceptanceSequence;
    private readonly Dictionary<string, StartedWorkflowIdempotencyRecord> startIdempotency = new(StringComparer.Ordinal);
    private readonly Dictionary<string, InboxStartIntentRecord> startIntents = new(StringComparer.Ordinal);
    private readonly Dictionary<OutboxRecordId, InMemoryOutboxRecord> outbox = [];
    private readonly List<OutboxWrite> dispatched = [];
    private readonly List<ProjectionWrite> projections = [];
    private readonly List<ProjectionHistoryWrite> history = [];
    private readonly Dictionary<InstanceId, WorkflowProjectionSnapshot> summaries = [];
    private readonly Dictionary<InstanceId, CheckpointWrite> checkpoints = [];
    private readonly Dictionary<WorkflowStreamId, List<DurableWorkflowEvent>> streams = [];
    private readonly Dictionary<TimerId, InMemoryTimerSchedule> timers = [];
    private readonly TimeProvider timeProvider;

    /// <summary>
    /// Initializes an empty SQL Server workflow transition state.
    /// </summary>
    public SqlServerWorkflowStateEngine(TimeProvider? timeProvider = null)
    {
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    internal SqlServerWorkflowStateEngine(StateSnapshot snapshot, TimeProvider? timeProvider = null)
        : this(timeProvider)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        foreach (var record in snapshot.Inbox)
        {
            inbox.Add(record.EventId, record);
        }

        foreach (var record in snapshot.DefinitionFanoutTargets)
        {
            var instanceId = record.InstanceId ?? throw new InvalidOperationException(
                "A persisted fanout target requires an instance identity.");
            definitionFanoutTargets.Add((record.EventId, instanceId), record);
        }

        foreach (var route in snapshot.InboxRouteRevisions)
        {
            inboxRouteRevisions.Add(route.Route, route.Revision);
        }

        nextInboxAcceptanceSequence = snapshot.NextInboxAcceptanceSequence;
        foreach (var record in snapshot.StartIdempotency)
        {
            startIdempotency.Add(record.IdempotencyKey, record);
        }

        foreach (var record in snapshot.StartIntents)
        {
            startIntents.Add(record.StartIdempotencyKey, record);
        }

        foreach (var record in snapshot.Outbox)
        {
            outbox.Add(record.Write.OutboxRecordId, record);
        }

        dispatched.AddRange(snapshot.Dispatched);
        projections.AddRange(snapshot.Projections);
        history.AddRange(snapshot.History);
        foreach (var summary in snapshot.Summaries)
        {
            summaries.Add(summary.InstanceId, summary);
        }

        foreach (var checkpoint in snapshot.Checkpoints)
        {
            checkpoints.Add(checkpoint.InstanceId, checkpoint);
        }

        foreach (var stream in snapshot.Streams)
        {
            streams.Add(
                new WorkflowStreamId(stream.InstanceId),
                stream.Events.Select(workflowEvent => workflowEvent.Restore()).ToList());
        }

        foreach (var timer in snapshot.Timers)
        {
            timers.Add(timer.Request.TimerId, timer);
        }
    }

    internal StateSnapshot Capture()
    {
        lock (gate)
        {
            return new StateSnapshot(
                inbox.Values.Select(CloneInboxRecord).ToArray(),
                definitionFanoutTargets.Values.Select(CloneInboxRecord).ToArray(),
                inboxRouteRevisions
                    .Select(pair => new RouteRevisionState(pair.Key, pair.Value))
                    .ToArray(),
                nextInboxAcceptanceSequence,
                startIdempotency.Values.ToArray(),
                startIntents.Values.Select(CloneStartIntent).ToArray(),
                outbox.Values
                    .Select(record => record with { Write = CloneOutboxWrite(record.Write) })
                    .ToArray(),
                dispatched.Select(CloneOutboxWrite).ToArray(),
                projections.ToArray(),
                history.ToArray(),
                summaries.Values.Select(CloneSnapshot).ToArray(),
                checkpoints.Values.Select(CloneCheckpointWrite).ToArray(),
                streams
                    .Select(pair => new WorkflowStreamState(
                        pair.Key.InstanceId,
                        pair.Value.Select(PersistedWorkflowEvent.Capture).ToArray()))
                    .ToArray(),
                timers.Values.Select(timer => timer with { }).ToArray());
        }
    }

    internal sealed record StateSnapshot(
        IReadOnlyList<InboxRecord> Inbox,
        IReadOnlyList<InboxRecord> DefinitionFanoutTargets,
        IReadOnlyList<RouteRevisionState> InboxRouteRevisions,
        long NextInboxAcceptanceSequence,
        IReadOnlyList<StartedWorkflowIdempotencyRecord> StartIdempotency,
        IReadOnlyList<InboxStartIntentRecord> StartIntents,
        IReadOnlyList<InMemoryOutboxRecord> Outbox,
        IReadOnlyList<OutboxWrite> Dispatched,
        IReadOnlyList<ProjectionWrite> Projections,
        IReadOnlyList<ProjectionHistoryWrite> History,
        IReadOnlyList<WorkflowProjectionSnapshot> Summaries,
        IReadOnlyList<CheckpointWrite> Checkpoints,
        IReadOnlyList<WorkflowStreamState> Streams,
        IReadOnlyList<InMemoryTimerSchedule> Timers);

    internal sealed record RouteRevisionState(InboxRouteKey Route, long Revision);

    internal sealed record WorkflowStreamState(
        InstanceId InstanceId,
        IReadOnlyList<PersistedWorkflowEvent> Events);

    internal sealed record PersistedWorkflowEvent(string EventType, string Payload)
    {
        internal static PersistedWorkflowEvent Capture(DurableWorkflowEvent workflowEvent) =>
            new(WorkflowEventCodec.ToEventType(workflowEvent), WorkflowEventCodec.Serialize(workflowEvent));

        internal DurableWorkflowEvent Restore() => WorkflowEventCodec.Deserialize(EventType, Payload);
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

            var invalidInboxTransition = batch.InboxOperations.FirstOrDefault(operation =>
                operation.ExpectedState is { } expected &&
                (!TryGetInboxOperationRecord(operation, out var existing) || existing.State != expected));
            if (invalidInboxTransition is not null)
            {
                var route = batch.InboxRouteMutations.FirstOrDefault()?.Route ??
                    InboxRouteKey.Direct(
                        batch.StreamId.InstanceId,
                        EventName.Create("inbox-transition"),
                        EventContractVersion.Initial,
                        CorrelationId.Create("inbox-transition"));
                return Task.FromResult(EventStoreConflict.InboxRouteChanged(route));
            }

            var stream = GetStream(batch.StreamId);
            var actualVersion = new StreamVersion(stream.Count);
            if (actualVersion != batch.ExpectedVersion)
            {
                return Task.FromResult(EventStoreConflict.ExpectedVersionMismatch(
                    batch.ExpectedVersion,
                    actualVersion));
            }

            var conflictingStartKey = FindConflictingStartIdempotencyKey(batch.StartIdempotencyOperations);
            if (conflictingStartKey is not null)
            {
                return Task.FromResult(EventStoreConflict.StartIdempotencyKeyAlreadyExists(conflictingStartKey));
            }

            var incompatiblePendingStart = FindIncompatiblePendingStart(batch.StartIdempotencyOperations);
            if (incompatiblePendingStart is not null)
            {
                return Task.FromResult(EventStoreConflict.StartIdempotencyKeyAlreadyExists(incompatiblePendingStart));
            }
            stream.AddRange(batch.Events);
            ApplyInboxOperations(batch.StreamId.InstanceId, batch.InboxOperations);
            AdvanceInboxRoutes(batch.InboxRouteMutations);
            ApplyInboxTargetPoisonOperations(batch.InboxTargetPoisonOperations);
            ApplyStartIdempotencyOperations(batch.StartIdempotencyOperations);
            MaterializePendingStarts(batch.StartIdempotencyOperations);
            foreach (var record in batch.OutboxRecords)
            {
                outbox[record.OutboxRecordId] = new InMemoryOutboxRecord(
                    batch.StreamId.InstanceId,
                    CloneOutboxWrite(record) with { DispatchAttempt = 0 },
                    OutboxRecordState.Pending);
            }

            ApplyProjectionOperations(batch.ProjectionOperations);
            foreach (var timer in batch.TimerSchedules)
            {
                timers[timer.TimerId] = new InMemoryTimerSchedule(timer, ClaimedUntil: null);
            }

            if (batch.Checkpoint is { } checkpoint)
            {
                checkpoints[checkpoint.InstanceId] = CloneCheckpointWrite(checkpoint);
            }

            return Task.FromResult(Result<AppendEventsResult>.Success(
                new AppendEventsResult(new StreamVersion(stream.Count))));
        }
    }

    /// <inheritdoc />
    public Task<InboxAcceptanceCommitResult> AcceptAsync(
        InboxAcceptance acceptance,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(acceptance);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            if (inbox.TryGetValue(acceptance.Envelope.EventId, out var existing))
            {
                var disposition = string.Equals(
                    existing.EnvelopeFingerprint,
                    acceptance.EnvelopeFingerprint,
                    StringComparison.Ordinal)
                    ? InboxAcceptanceCommitDisposition.Duplicate
                    : InboxAcceptanceCommitDisposition.Conflict;
                return Task.FromResult(new InboxAcceptanceCommitResult(disposition, existing));
            }

            var route = CreateInboxRoute(acceptance.Envelope);
            if (route.Kind == InboxRouteKinds.Direct)
            {
                var instanceId = route.InstanceId ?? throw new InvalidOperationException(
                    "A direct inbox route requires an instance.");
                if (!summaries.TryGetValue(instanceId, out var target))
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
                acceptance.Envelope.Route.Kind == InboxRouteKinds.Direct
                    ? acceptance.Envelope.Route.InstanceId
                    : null,
                acceptance.Envelope.EventId,
                acceptance.EnvelopeFingerprint,
                InboxRecordState.Received)
            {
                Envelope = CloneEnvelope(acceptance.Envelope),
                Route = route,
                AcceptanceSequence = ++nextInboxAcceptanceSequence,
                AcceptedAt = acceptance.AcceptedAt
            };
            inbox.Add(record.EventId, record);
            inboxRouteRevisions[route] = CurrentRouteRevision(route) + 1;
            return Task.FromResult(new InboxAcceptanceCommitResult(
                InboxAcceptanceCommitDisposition.Accepted,
                record));
        }
    }

    /// <inheritdoc />
    public Task<InboxAcceptanceCommitResult> AcceptDefinitionFanoutAsync(
        InboxDefinitionFanoutAcceptance request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegative(request.MaximumTargetCount);
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Acceptance.Envelope.Route.Kind != InboxRouteKinds.DefinitionFanout ||
            !Equals(request.Acceptance.Envelope.Route.DefinitionId, request.DefinitionId))
        {
            throw new ArgumentException(
                "Definition fanout requires a matching definition-fanout envelope route.",
                nameof(request));
        }

        lock (gate)
        {
            var acceptance = request.Acceptance;
            if (inbox.TryGetValue(acceptance.Envelope.EventId, out var existing))
            {
                var disposition = string.Equals(
                    existing.EnvelopeFingerprint,
                    acceptance.EnvelopeFingerprint,
                    StringComparison.Ordinal)
                    ? InboxAcceptanceCommitDisposition.Duplicate
                    : InboxAcceptanceCommitDisposition.Conflict;
                return Task.FromResult(new InboxAcceptanceCommitResult(disposition, CloneInboxRecord(existing))
                {
                    DefinitionFanoutTargets = LoadDefinitionFanoutTargetIds(existing.EventId)
                });
            }

            var targetIds = summaries.Values
                .Where(snapshot => snapshot.DefinitionId.Equals(request.DefinitionId) && !IsTerminal(snapshot.Status))
                .Select(snapshot => snapshot.InstanceId)
                .OrderBy(instanceId => instanceId.Value)
                .ToArray();
            if (targetIds.Length > request.MaximumTargetCount)
            {
                return Task.FromResult(new InboxAcceptanceCommitResult(
                    InboxAcceptanceCommitDisposition.FanoutLimitExceeded,
                    null));
            }

            var root = new InboxRecord(
                null,
                acceptance.Envelope.EventId,
                acceptance.EnvelopeFingerprint,
                InboxRecordState.Received)
            {
                Envelope = CloneEnvelope(acceptance.Envelope),
                AcceptedAt = acceptance.AcceptedAt
            };
            inbox.Add(root.EventId, root);

            foreach (var instanceId in targetIds)
            {
                var route = InboxRouteKey.DefinitionFanoutTarget(
                    instanceId,
                    request.DefinitionId,
                    EventName.Create(acceptance.Envelope.EventName),
                    new EventContractVersion(acceptance.Envelope.EventContractVersion),
                    acceptance.Envelope.CorrelationId);
                definitionFanoutTargets.Add(
                    (root.EventId, instanceId),
                    root with
                    {
                        InstanceId = instanceId,
                        Route = route,
                        AcceptanceSequence = ++nextInboxAcceptanceSequence
                    });
                inboxRouteRevisions[route] = CurrentRouteRevision(route) + 1;
            }

            return Task.FromResult(new InboxAcceptanceCommitResult(
                InboxAcceptanceCommitDisposition.Accepted,
                CloneInboxRecord(root))
            {
                DefinitionFanoutTargets = targetIds
            });
        }
    }

    /// <inheritdoc />
    public Task<InboxAcceptanceCommitResult> AcceptStartOrDeliverAsync(
        InboxStartOrDeliverAcceptance request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.StartIdempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.WorkflowInputContentType);
        ArgumentNullException.ThrowIfNull(request.WorkflowInputPayload);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.WorkflowInputFingerprint);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateStartOrDeliverRoute(request);

        lock (gate)
        {
            var acceptance = request.Acceptance;
            if (inbox.TryGetValue(acceptance.Envelope.EventId, out var existingEvent))
            {
                return Task.FromResult(ClassifyInboxAcceptance(existingEvent, acceptance.EnvelopeFingerprint));
            }

            if (TryCreateStartConflict(request, out var conflict))
            {
                return Task.FromResult(new InboxAcceptanceCommitResult(
                    InboxAcceptanceCommitDisposition.StartConflict,
                    null)
                {
                    StartConflict = conflict
                });
            }

            startIdempotency.TryGetValue(request.StartIdempotencyKey, out var started);
            if (!startIntents.TryGetValue(request.StartIdempotencyKey, out var intent))
            {
                intent = new InboxStartIntentRecord(
                    request.StartIdempotencyKey,
                    request.DefinitionId,
                    request.DefinitionVersion,
                    request.WorkflowInputContentType,
                    [.. request.WorkflowInputPayload],
                    request.WorkflowInputFingerprint,
                    started is null ? InboxStartIntentState.Pending : InboxStartIntentState.Materialized)
                {
                    InstanceId = started?.InstanceId,
                    DefinitionFingerprint = started?.DefinitionFingerprint
                };
                startIntents.Add(intent.StartIdempotencyKey, intent);
            }

            var route = started is null
                ? null
                : InboxRouteKey.Direct(
                    started.InstanceId,
                    EventName.Create(acceptance.Envelope.EventName),
                    new EventContractVersion(acceptance.Envelope.EventContractVersion),
                    acceptance.Envelope.CorrelationId);
            var record = new InboxRecord(
                started?.InstanceId,
                acceptance.Envelope.EventId,
                acceptance.EnvelopeFingerprint,
                InboxRecordState.Received)
            {
                Envelope = CloneEnvelope(acceptance.Envelope),
                Route = route,
                AcceptanceSequence = ++nextInboxAcceptanceSequence,
                AcceptedAt = acceptance.AcceptedAt
            };
            inbox.Add(record.EventId, record);
            if (route is not null)
            {
                inboxRouteRevisions[route] = CurrentRouteRevision(route) + 1;
            }

            return Task.FromResult(new InboxAcceptanceCommitResult(
                InboxAcceptanceCommitDisposition.Accepted,
                CloneInboxRecord(record)));
        }
    }

    /// <inheritdoc />
    public Task<Option<InboxStartIntentRecord>> GetStartIntentAsync(
        string startIdempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startIdempotencyKey);
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            return Task.FromResult(startIntents.TryGetValue(startIdempotencyKey, out var intent)
                ? Option<InboxStartIntentRecord>.Some(CloneStartIntent(intent))
                : Option<InboxStartIntentRecord>.None);
        }
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public Task<Option<InboxRecord>> GetAsync(
        InstanceId instanceId,
        EventId eventId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instanceId);
        ArgumentNullException.ThrowIfNull(eventId);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            if (definitionFanoutTargets.TryGetValue((eventId, instanceId), out var target))
            {
                return Task.FromResult(Option<InboxRecord>.Some(CloneInboxRecord(target)));
            }

            return Task.FromResult(inbox.TryGetValue(eventId, out var record) && record.InstanceId?.Equals(instanceId) == true
                ? Option<InboxRecord>.Some(record)
                : Option<InboxRecord>.None);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<InboxRecord>> ListDefinitionFanoutTargetsAsync(
        EventId eventId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventId);
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            return Task.FromResult<IReadOnlyList<InboxRecord>>(
                definitionFanoutTargets.Values
                    .Where(record => record.EventId.Equals(eventId))
                    .OrderBy(record => record.InstanceId!.Value)
                    .Select(CloneInboxRecord)
                    .ToArray());
        }
    }

    /// <inheritdoc />
    public Task<Option<InboxRecord>> GetByEventIdAsync(
        EventId eventId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventId);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            return Task.FromResult(inbox.TryGetValue(eventId, out var record)
                ? Option<InboxRecord>.Some(record)
                : Option<InboxRecord>.None);
        }
    }

    /// <inheritdoc />
    public Task<InboxMatchSnapshot> GetMatchSnapshotAsync(
        InboxMatchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            var routes = request.Routes.ToHashSet();
            var pending = DeliveryRecords()
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

    /// <inheritdoc />
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
                DeliveryRecords()
                    .Where(record => record.State == InboxRecordState.Received &&
                        record.AcceptanceSequence > afterAcceptanceSequence)
                    .OrderBy(record => record.AcceptanceSequence)
                    .ThenBy(record => record.EventId.Value, StringComparer.Ordinal)
                    .Take(maxCount)
                    .Select(CloneInboxRecord)
                    .ToArray());
        }
    }

    Task<IReadOnlyList<InboxRecord>> IWorkflowInboxStore.ListReceivedAsync(
        long afterAcceptanceSequence,
        int maxCount,
        CancellationToken cancellationToken) =>
        ListReceivedAsync(afterAcceptanceSequence, maxCount, cancellationToken);

    /// <inheritdoc />
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
                DeliveryRecords()
                    .Where(record => record.State == InboxRecordState.Received &&
                        record.HandoffFailureCount > 0 &&
                        record.HandoffRetryNotBefore <= eligibleAt)
                    .OrderBy(record => record.HandoffRetryNotBefore)
                    .ThenBy(record => record.AcceptanceSequence)
                    .ThenBy(record => record.EventId.Value, StringComparer.Ordinal)
                    .Take(maxCount)
                    .Select(CloneInboxRecord)
                    .ToArray());
        }
    }

    /// <inheritdoc />
    public Task MarkPoisonedAsync(
        EventId eventId,
        InboxRecordState expectedState,
        string code,
        string? detail,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            if (inbox.TryGetValue(eventId, out var record) && record.State == expectedState)
            {
                if (record.Envelope?.Route is
                        { Kind: InboxRouteKinds.StartOrDeliver, StartIdempotencyKey: { } startKey } &&
                    startIntents.TryGetValue(startKey, out var intent) &&
                    intent.State == InboxStartIntentState.Pending)
                {
                    startIntents[startKey] = intent with
                    {
                        State = InboxStartIntentState.Poisoned,
                        PoisonCode = code,
                        PoisonDetail = detail
                    };
                    foreach (var pending in inbox
                                 .Where(pair =>
                                     pair.Value.State == expectedState &&
                                     string.Equals(
                                         pair.Value.Envelope?.Route.StartIdempotencyKey,
                                         startKey,
                                         StringComparison.Ordinal))
                                 .ToArray())
                    {
                        inbox[pending.Key] = pending.Value with
                        {
                            State = InboxRecordState.Poisoned,
                            PoisonCode = code,
                            PoisonDetail = detail
                        };
                    }

                    return Task.CompletedTask;
                }

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

    /// <inheritdoc />
    public Task MarkPoisonedAsync(
        InboxRecordIdentity recordIdentity,
        InboxRecordState expectedState,
        string code,
        string? detail,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recordIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        cancellationToken.ThrowIfCancellationRequested();
        if (recordIdentity.TargetInstanceId is null)
        {
            return MarkPoisonedAsync(
                recordIdentity.EventId,
                expectedState,
                code,
                detail,
                cancellationToken);
        }

        var identityKind = ResolveInboxIdentityKind(recordIdentity);
        if (identityKind == InboxIdentityKind.Missing)
        {
            return Task.CompletedTask;
        }
        if (identityKind == InboxIdentityKind.Root)
        {
            return MarkPoisonedAsync(
                recordIdentity.EventId,
                expectedState,
                code,
                detail,
                cancellationToken);
        }

        lock (gate)
        {
            var key = (recordIdentity.EventId, recordIdentity.TargetInstanceId!);
            if (definitionFanoutTargets.TryGetValue(key, out var record) && record.State == expectedState)
            {
                definitionFanoutTargets[key] = record with
                {
                    State = InboxRecordState.Poisoned,
                    PoisonCode = code,
                    PoisonDetail = detail
                };
                inboxRouteRevisions[record.Route!] = CurrentRouteRevision(record.Route!) + 1;
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
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
        ArgumentNullException.ThrowIfNull(eventId);
        ValidateHandoffFailureArguments(expectedFailureCount, maxFailureCount, code);
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

    /// <inheritdoc />
    public Task RecordHandoffFailureAsync(
        InboxRecordIdentity recordIdentity,
        InboxRecordState expectedState,
        int expectedFailureCount,
        int maxFailureCount,
        DateTimeOffset retryNotBefore,
        string code,
        string? detail,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recordIdentity);
        ValidateHandoffFailureArguments(expectedFailureCount, maxFailureCount, code);
        cancellationToken.ThrowIfCancellationRequested();
        if (recordIdentity.TargetInstanceId is null)
        {
            return RecordHandoffFailureAsync(
                recordIdentity.EventId,
                expectedState,
                expectedFailureCount,
                maxFailureCount,
                retryNotBefore,
                code,
                detail,
                cancellationToken);
        }

        var identityKind = ResolveInboxIdentityKind(recordIdentity);
        if (identityKind == InboxIdentityKind.Missing)
        {
            return Task.CompletedTask;
        }
        if (identityKind == InboxIdentityKind.Root)
        {
            return RecordHandoffFailureAsync(
                recordIdentity.EventId,
                expectedState,
                expectedFailureCount,
                maxFailureCount,
                retryNotBefore,
                code,
                detail,
                cancellationToken);
        }

        lock (gate)
        {
            var key = (recordIdentity.EventId, recordIdentity.TargetInstanceId!);
            if (!definitionFanoutTargets.TryGetValue(key, out var record) ||
                record.State != expectedState ||
                record.HandoffFailureCount != expectedFailureCount)
            {
                return Task.CompletedTask;
            }

            var failureCount = expectedFailureCount + 1;
            var poisoned = failureCount >= maxFailureCount;
            definitionFanoutTargets[key] = record with
            {
                State = poisoned ? InboxRecordState.Poisoned : record.State,
                HandoffFailureCount = failureCount,
                HandoffRetryNotBefore = poisoned ? null : retryNotBefore,
                PoisonCode = poisoned ? code : record.PoisonCode,
                PoisonDetail = poisoned ? detail : record.PoisonDetail
            };
            if (poisoned)
            {
                inboxRouteRevisions[record.Route!] = CurrentRouteRevision(record.Route!) + 1;
            }
        }

        return Task.CompletedTask;
    }

    private InboxIdentityKind ResolveInboxIdentityKind(InboxRecordIdentity recordIdentity)
    {
        if (recordIdentity.TargetInstanceId is null)
        {
            return InboxIdentityKind.Root;
        }

        lock (gate)
        {
            if (definitionFanoutTargets.ContainsKey(
                    (recordIdentity.EventId, recordIdentity.TargetInstanceId)))
            {
                return InboxIdentityKind.FanoutTarget;
            }

            return inbox.TryGetValue(recordIdentity.EventId, out var directRecord) &&
                   directRecord.InstanceId?.Equals(recordIdentity.TargetInstanceId) == true
                ? InboxIdentityKind.Root
                : InboxIdentityKind.Missing;
        }
    }

    private static void ValidateHandoffFailureArguments(
        int expectedFailureCount,
        int maxFailureCount,
        string code)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(expectedFailureCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFailureCount);
        if (expectedFailureCount >= maxFailureCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedFailureCount),
                expectedFailureCount,
                "The observed failure count must be below the terminal failure count.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(code);
    }

    private enum InboxIdentityKind
    {
        Missing,
        Root,
        FanoutTarget
    }

    /// <inheritdoc />
    public Task<Option<StartedWorkflowIdempotencyRecord>> GetStartedAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            return Task.FromResult(startIdempotency.TryGetValue(idempotencyKey, out var record)
                ? Option<StartedWorkflowIdempotencyRecord>.Some(record)
                : Option<StartedWorkflowIdempotencyRecord>.None);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<OutboxWrite>> ClaimAsync(int maxCount, CancellationToken cancellationToken)
    {
        return ClaimAsync(
            new OutboxClaimRequest(maxCount, timeProvider.GetUtcNow(), DefaultLeaseDuration),
            cancellationToken);
    }

    /// <inheritdoc />
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
                .Select(record => CloneOutboxWrite(record.Write) with
                {
                    DispatchAttempt = checked(record.Write.DispatchAttempt + 1)
                })
                .ToArray();
            foreach (var record in claimed)
            {
                outbox[record.OutboxRecordId] = outbox[record.OutboxRecordId] with
                {
                    Write = record,
                    State = OutboxRecordState.Claimed,
                    ClaimedUntil = request.ClaimedAt.Add(request.LeaseDuration)
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
    public Task<Option<OutboxDispatchSnapshot>> GetDispatchSnapshotAsync(
        OutboxRecordId outboxRecordId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            return Task.FromResult(outbox.TryGetValue(outboxRecordId, out var record)
                ? Option<OutboxDispatchSnapshot>.Some(new OutboxDispatchSnapshot(record.State)
                {
                    PoisonCode = record.PoisonCode,
                    PoisonDetail = record.PoisonDetail
                })
                : Option<OutboxDispatchSnapshot>.None);
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
            if (outbox.TryGetValue(outboxRecordId, out var record) &&
                record.State != OutboxRecordState.Poisoned)
            {
                outbox[outboxRecordId] = record with
                {
                    State = state,
                    ClaimedUntil = state == OutboxRecordState.Claimed ? record.ClaimedUntil : null
                };
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task MarkPoisonedAsync(
        OutboxRecordId outboxRecordId,
        string code,
        string? detail,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
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
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ReleaseAsync(OutboxRecordId outboxRecordId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            if (outbox.TryGetValue(outboxRecordId, out var record) &&
                record.State == OutboxRecordState.Claimed)
            {
                outbox[outboxRecordId] = record with
                {
                    State = OutboxRecordState.Retryable,
                    ClaimedUntil = null
                };
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
    public Task<Option<WorkflowProjectionSnapshot>> GetAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instanceId);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            return Task.FromResult(summaries.TryGetValue(instanceId, out var snapshot)
                ? Option<WorkflowProjectionSnapshot>.Some(CloneSnapshot(snapshot))
                : Option<WorkflowProjectionSnapshot>.None);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkflowProjectionSnapshot>> FindActiveWaitsAsync(
        DefinitionId? definitionId,
        EventName eventName,
        CorrelationId correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventName);
        ArgumentNullException.ThrowIfNull(correlationId);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            return Task.FromResult<IReadOnlyList<WorkflowProjectionSnapshot>>(
                summaries.Values
                    .Where(snapshot => definitionId is null || snapshot.DefinitionId.Equals(definitionId))
                    .Where(snapshot => snapshot.ActiveWaits.Any(wait =>
                        string.Equals(wait.EventName, eventName.Value, StringComparison.Ordinal) &&
                        wait.CorrelationId.Equals(correlationId)))
                    .Select(CloneSnapshot)
                    .ToArray());
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkflowProjectionSnapshot>> FindActiveWaitsAsync(
        DefinitionId? definitionId,
        EventName eventName,
        EventContractVersion eventContractVersion,
        CorrelationId correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventName);
        ArgumentNullException.ThrowIfNull(eventContractVersion);
        ArgumentNullException.ThrowIfNull(correlationId);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            return Task.FromResult<IReadOnlyList<WorkflowProjectionSnapshot>>(
                summaries.Values
                    .Where(snapshot => definitionId is null || snapshot.DefinitionId.Equals(definitionId))
                    .Where(snapshot => snapshot.ActiveWaits.Any(wait =>
                        string.Equals(wait.EventName, eventName.Value, StringComparison.Ordinal) &&
                        wait.EventContractVersion == eventContractVersion.Value &&
                        wait.CorrelationId.Equals(correlationId)))
                    .Select(CloneSnapshot)
                    .ToArray());
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<WorkflowProjectionSnapshot>> ListLeaseRecoveryCandidatesAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            return Task.FromResult<IReadOnlyList<WorkflowProjectionSnapshot>>(
                summaries.Values.Select(CloneSnapshot).ToArray());
        }
    }

    /// <inheritdoc />
    public Task ScheduleAsync(TimerScheduleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            timers[request.TimerId] = new InMemoryTimerSchedule(request, ClaimedUntil: null);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<FireTimerCommand>> ClaimDueAsync(
        DateTimeOffset dueAtOrBefore,
        int maxCount,
        CancellationToken cancellationToken)
    {
        return ClaimDueAsync(
            new TimerClaimRequest(dueAtOrBefore, maxCount, dueAtOrBefore, DefaultLeaseDuration),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<FireTimerCommand>> ClaimDueAsync(
        TimerClaimRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegative(request.MaxCount);
        ThrowIfInvalidLease(request.LeaseDuration);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            var due = timers.Values
                .Where(timer => timer.Request.FireAt <= request.DueAtOrBefore &&
                    (timer.ClaimedUntil is null || timer.ClaimedUntil <= request.ClaimedAt))
                .OrderBy(timer => timer.Request.FireAt)
                .ThenBy(timer => timer.Request.TimerId.Value)
                .Take(request.MaxCount)
                .ToArray();
            foreach (var timer in due)
            {
                timers[timer.Request.TimerId] = timer with
                {
                    ClaimedUntil = request.ClaimedAt.Add(request.LeaseDuration)
                };
            }

            return Task.FromResult<IReadOnlyList<FireTimerCommand>>(due.Select(timer => new FireTimerCommand
            {
                CommandId = timer.Request.CommandId,
                InstanceId = timer.Request.InstanceId,
                RequestedAt = request.ClaimedAt,
                TimerId = timer.Request.TimerId
            }).ToArray());
        }
    }

    /// <inheritdoc />
    public Task CompleteAsync(TimerId timerId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            timers.Remove(timerId);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ReleaseAsync(TimerId timerId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            if (timers.TryGetValue(timerId, out var timer))
            {
                timers[timerId] = timer with { ClaimedUntil = null };
            }
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

    public Task RefreshStuckStateAsync(
        WorkflowOperatorStatisticsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            MarkStuckInstances(request);
        }

        return Task.CompletedTask;
    }

    public Task<WorkflowOperatorStatistics> GetOperatorStatisticsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            var groups = summaries.Values
                .GroupBy(snapshot => new
                {
                    snapshot.DefinitionId,
                    snapshot.DefinitionVersion,
                    snapshot.Status
                })
                .OrderBy(group => group.Key.DefinitionId.Value)
                .ThenBy(group => group.Key.DefinitionVersion.Value)
                .ThenBy(group => group.Key.Status)
                .Select(group => new WorkflowOperatorStatisticsGroup(
                    group.Key.DefinitionId,
                    group.Key.DefinitionVersion,
                    group.Key.Status,
                    group.LongCount()))
                .ToArray();
            var pressure = new WorkflowOperationalPressure
            {
                ActiveInstanceCount = summaries.Values.LongCount(snapshot => IsActive(snapshot.InstanceId)),
                StuckInstanceCount = summaries.Values.LongCount(snapshot => snapshot.IsStuck),
                ActiveWaitCount = summaries.Values.Sum(snapshot => (long)snapshot.ActiveWaits.Count),
                StreamEventCount = streams.Values.Sum(stream => (long)stream.Count),
                CheckpointCount = checkpoints.Count,
                CheckpointLag = streams
                    .Select(stream => Math.Max(
                        0,
                        stream.Value.Count - (checkpoints.TryGetValue(stream.Key.InstanceId, out var checkpoint)
                            ? checkpoint.StreamVersion.Value
                            : 0)))
                    .DefaultIfEmpty()
                    .Max(),
                ContinuationPendingCount = CountOutbox(OutboxKinds.Continue, OutboxRecordState.Pending),
                ContinuationRetryableCount = CountOutbox(OutboxKinds.Continue, OutboxRecordState.Retryable),
                ContinuationClaimedCount = CountOutbox(OutboxKinds.Continue, OutboxRecordState.Claimed),
                ContinuationPoisonedCount = CountOutbox(OutboxKinds.Continue, OutboxRecordState.Poisoned),
                ExternalOutboxPendingCount = CountExternalOutbox(OutboxRecordState.Pending),
                ExternalOutboxRetryableCount = CountExternalOutbox(OutboxRecordState.Retryable),
                ExternalOutboxClaimedCount = CountExternalOutbox(OutboxRecordState.Claimed),
                ExternalOutboxPoisonedCount = CountExternalOutbox(OutboxRecordState.Poisoned)
            };
            return Task.FromResult(new WorkflowOperatorStatistics
            {
                ProviderName = OrcaCoreDiagnostics.SqlServerProviderName,
                Groups = groups,
                StuckGroups = summaries.Values
                    .Where(snapshot => snapshot.IsStuck)
                    .GroupBy(snapshot => snapshot.DefinitionId)
                    .OrderBy(group => group.Key.Value)
                    .Select(group => new WorkflowOperatorStuckGroup(group.Key, group.LongCount()))
                    .ToArray(),
                ActiveWaitGroups = summaries.Values
                    .SelectMany(snapshot => snapshot.ActiveWaits.Select(wait => new
                    {
                        snapshot.DefinitionId,
                        EventName = global::OrcaCore.EventName.Create(wait.EventName)
                    }))
                    .GroupBy(wait => new { wait.DefinitionId, wait.EventName })
                    .OrderBy(group => group.Key.DefinitionId.Value)
                    .ThenBy(group => group.Key.EventName.Value, StringComparer.Ordinal)
                    .Select(group => new WorkflowOperatorActiveWaitGroup(
                        group.Key.DefinitionId,
                        group.Key.EventName,
                        group.LongCount()))
                    .ToArray(),
                Pressure = pressure
            });
        }
    }

    public Task<WorkflowProviderMaintenanceResult> ArchiveForMaintenanceAsync(
        WorkflowProviderMaintenanceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            if (!summaries.ContainsKey(request.InstanceId))
            {
                return Task.FromResult(new WorkflowProviderMaintenanceResult(
                    WorkflowProviderMaintenanceDisposition.NotFound));
            }

            if (IsActive(request.InstanceId))
            {
                return Task.FromResult(Rejected(WorkflowProviderMaintenanceBlocker.ActiveInstance));
            }

            summaries[request.InstanceId] = summaries[request.InstanceId] with
            {
                ArchivedAt = request.RequestedAt
            };
            return Task.FromResult(new WorkflowProviderMaintenanceResult(
                WorkflowProviderMaintenanceDisposition.Archived));
        }
    }

    public Task<WorkflowProviderMaintenanceResult> PurgeForMaintenanceAsync(
        WorkflowProviderMaintenanceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            if (!summaries.ContainsKey(request.InstanceId) &&
                !streams.ContainsKey(new WorkflowStreamId(request.InstanceId)))
            {
                return Task.FromResult(new WorkflowProviderMaintenanceResult(
                    WorkflowProviderMaintenanceDisposition.NotFound));
            }

            if (FindMaintenanceBlocker(request.InstanceId) is { } blocker)
            {
                return Task.FromResult(Rejected(blocker));
            }

            DeleteInstanceData(request.InstanceId);
            return Task.FromResult(new WorkflowProviderMaintenanceResult(
                WorkflowProviderMaintenanceDisposition.Purged));
        }
    }

    public Task<WorkflowProviderMaintenanceInspection> InspectForMaintenanceAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instanceId);
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            var exists = summaries.TryGetValue(instanceId, out var snapshot) ||
                streams.ContainsKey(new WorkflowStreamId(instanceId));
            return Task.FromResult(new WorkflowProviderMaintenanceInspection(
                exists,
                snapshot?.ArchivedAt,
                exists ? FindMaintenanceBlocker(instanceId) : null));
        }
    }

    private long CountOutbox(string kind, OutboxRecordState state) =>
        outbox.Values.LongCount(record =>
            string.Equals(record.Write.Kind, kind, StringComparison.Ordinal) && record.State == state);

    private long CountExternalOutbox(OutboxRecordState state) =>
        outbox.Values.LongCount(record =>
            !string.Equals(record.Write.Kind, OutboxKinds.Continue, StringComparison.Ordinal) && record.State == state);

    private void MarkStuckInstances(WorkflowOperatorStatisticsRequest request)
    {
        var cutoff = request.ObservedAt.Subtract(request.StuckThreshold);
        foreach (var (instanceId, snapshot) in summaries.ToArray())
        {
            if (snapshot.IsStuck ||
                snapshot.Status is not (global::OrcaCore.WorkflowInstanceStatus.Pending or
                    global::OrcaCore.WorkflowInstanceStatus.Running or
                    global::OrcaCore.WorkflowInstanceStatus.CancellationRequested) ||
                (snapshot.LastActiveAt ?? snapshot.UpdatedAt) > cutoff)
            {
                continue;
            }

            summaries[instanceId] = snapshot with
            {
                IsStuck = true,
                StuckDetectedAt = snapshot.StuckDetectedAt ?? request.ObservedAt
            };
        }
    }

    private WorkflowProviderMaintenanceBlocker? FindMaintenanceBlocker(InstanceId instanceId)
    {
        if (IsActive(instanceId))
        {
            return WorkflowProviderMaintenanceBlocker.ActiveInstance;
        }

        if (DeliveryRecords().Any(record =>
                record.InstanceId?.Equals(instanceId) == true && record.State == InboxRecordState.Received))
        {
            return WorkflowProviderMaintenanceBlocker.PendingInboxDelivery;
        }

        var states = outbox.Values
            .Where(record => record.InstanceId.Equals(instanceId))
            .Select(record => record.State)
            .ToHashSet();
        if (states.Contains(OutboxRecordState.Claimed))
        {
            return WorkflowProviderMaintenanceBlocker.ClaimedOutboxDispatch;
        }

        if (states.Contains(OutboxRecordState.Pending) || states.Contains(OutboxRecordState.Retryable))
        {
            return WorkflowProviderMaintenanceBlocker.PendingOutboxDispatch;
        }

        return states.Contains(OutboxRecordState.Poisoned)
            ? WorkflowProviderMaintenanceBlocker.PoisonedOutboxDispatch
            : null;
    }

    private static WorkflowProviderMaintenanceResult Rejected(WorkflowProviderMaintenanceBlocker blocker) =>
        new(WorkflowProviderMaintenanceDisposition.Rejected, blocker);

    private List<DurableWorkflowEvent> GetStream(WorkflowStreamId streamId)
    {
        if (!streams.TryGetValue(streamId, out var stream))
        {
            stream = [];
            streams.Add(streamId, stream);
        }

        return stream;
    }

    private void ApplyInboxOperations(InstanceId instanceId, IEnumerable<InboxWrite> operations)
    {
        foreach (var operation in operations)
        {
            if (operation.TargetInstanceId is { } fanoutTarget &&
                definitionFanoutTargets.TryGetValue((operation.EventId, fanoutTarget), out var fanoutRecord))
            {
                if (operation.ExpectedState is null || fanoutRecord.State == operation.ExpectedState)
                {
                    definitionFanoutTargets[(operation.EventId, fanoutTarget)] = fanoutRecord with
                    {
                        State = fanoutRecord.State == InboxRecordState.Applied
                            ? fanoutRecord.State
                            : operation.State
                    };
                }

                continue;
            }

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
                    $"Initial inbox write '{operation.EventId}' for instance '{instanceId}' " +
                    "must carry an envelope fingerprint.");
            }

            inbox[operation.EventId] = new InboxRecord(
                instanceId,
                operation.EventId,
                operation.EnvelopeFingerprint,
                operation.State)
            {
                Envelope = CloneEnvelope(operation.Envelope)
            };
        }
    }

    private bool TryGetInboxOperationRecord(InboxWrite operation, out InboxRecord record)
    {
        if (operation.TargetInstanceId is { } targetInstanceId &&
            definitionFanoutTargets.TryGetValue((operation.EventId, targetInstanceId), out record!))
        {
            return true;
        }

        if (!inbox.TryGetValue(operation.EventId, out record!))
        {
            record = null!;
            return false;
        }

        if (string.Equals(
                record.Envelope?.Route.Kind,
                InboxRouteKinds.DefinitionFanout,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Fanout inbox transition '{operation.EventId}' for instance " +
                $"'{operation.TargetInstanceId}' is outside the committed target snapshot.");
        }

        return true;
    }

    private long CurrentRouteRevision(InboxRouteKey route) =>
        inboxRouteRevisions.TryGetValue(route, out var revision) ? revision : 0;

    private void AdvanceInboxRoutes(IEnumerable<InboxRouteMutation> mutations)
    {
        foreach (var mutation in mutations)
        {
            inboxRouteRevisions[mutation.Route] = mutation.ExpectedRevision + 1;
        }
    }

    private void ApplyInboxTargetPoisonOperations(IEnumerable<InboxTargetPoisonWrite> operations)
    {
        foreach (var operation in operations)
        {
            foreach (var record in inbox.Values
                         .Where(record => record.State == InboxRecordState.Received &&
                             record.Route?.Kind == InboxRouteKinds.Direct &&
                             record.Route.InstanceId?.Equals(operation.InstanceId) == true)
                         .ToArray())
            {
                inbox[record.EventId] = record with
                {
                    State = InboxRecordState.Poisoned,
                    PoisonCode = operation.Code,
                    PoisonDetail = operation.Detail
                };
            }
        }
    }

    private static InboxRouteKey CreateInboxRoute(DurableEventEnvelope envelope) =>
        envelope.Route.Kind switch
        {
            InboxRouteKinds.Direct => InboxRouteKey.Direct(
                envelope.Route.InstanceId ?? throw new InvalidOperationException("A direct inbox route requires an instance."),
                EventName.Create(envelope.EventName),
                new EventContractVersion(envelope.EventContractVersion),
                envelope.CorrelationId),
            InboxRouteKinds.Correlation => InboxRouteKey.Correlation(
                envelope.Route.DefinitionId ?? throw new InvalidOperationException("A correlation inbox route requires a definition."),
                EventName.Create(envelope.EventName),
                new EventContractVersion(envelope.EventContractVersion),
                envelope.CorrelationId),
            _ => throw new NotSupportedException(
                $"Inbox buffering for route '{envelope.Route.Kind}' is owned by a later Section 7B task.")
        };

    private IEnumerable<InboxRecord> DeliveryRecords() =>
        inbox.Values
            .Where(record => !string.Equals(
                record.Envelope?.Route.Kind,
                InboxRouteKinds.DefinitionFanout,
                StringComparison.Ordinal))
            .Concat(definitionFanoutTargets.Values);

    private IReadOnlyList<InstanceId> LoadDefinitionFanoutTargetIds(EventId eventId) =>
        definitionFanoutTargets.Keys
            .Where(key => key.EventId.Equals(eventId))
            .Select(key => key.InstanceId)
            .OrderBy(instanceId => instanceId.Value)
            .ToArray();

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

    private static InboxAcceptanceCommitResult ClassifyInboxAcceptance(
        InboxRecord existing,
        string envelopeFingerprint) =>
        new(
            string.Equals(existing.EnvelopeFingerprint, envelopeFingerprint, StringComparison.Ordinal)
                ? InboxAcceptanceCommitDisposition.Duplicate
                : InboxAcceptanceCommitDisposition.Conflict,
            CloneInboxRecord(existing));

    private static void ValidateStartOrDeliverRoute(InboxStartOrDeliverAcceptance request)
    {
        var route = request.Acceptance.Envelope.Route;
        if (!string.Equals(route.Kind, InboxRouteKinds.StartOrDeliver, StringComparison.Ordinal) ||
            !Equals(route.DefinitionId, request.DefinitionId) ||
            !Equals(route.DefinitionVersion, request.DefinitionVersion) ||
            !string.Equals(route.StartIdempotencyKey, request.StartIdempotencyKey, StringComparison.Ordinal) ||
            !string.Equals(route.WorkflowInputContentType, request.WorkflowInputContentType, StringComparison.Ordinal) ||
            route.WorkflowInputPayload is null ||
            !route.WorkflowInputPayload.AsSpan().SequenceEqual(request.WorkflowInputPayload))
        {
            throw new ArgumentException(
                "Start-or-deliver acceptance must match the normalized envelope route.",
                nameof(request));
        }
    }

    private bool TryCreateStartConflict(
        InboxStartOrDeliverAcceptance request,
        out InboxStartBindingConflict? conflict)
    {
        if (startIdempotency.TryGetValue(request.StartIdempotencyKey, out var started) &&
            !MatchesStartBinding(
                started.DefinitionId,
                started.DefinitionVersion,
                started.InputFingerprint,
                request))
        {
            conflict = new InboxStartBindingConflict(
                started.DefinitionId,
                started.DefinitionVersion,
                started.DefinitionFingerprint,
                started.InputFingerprint,
                request.DefinitionId,
                request.DefinitionVersion,
                request.WorkflowInputFingerprint);
            return true;
        }

        if (startIntents.TryGetValue(request.StartIdempotencyKey, out var intent) &&
            (!MatchesStartBinding(
                 intent.DefinitionId,
                 intent.DefinitionVersion,
                 intent.WorkflowInputFingerprint,
                 request) ||
             intent.State == InboxStartIntentState.Poisoned))
        {
            conflict = new InboxStartBindingConflict(
                intent.DefinitionId,
                intent.DefinitionVersion,
                intent.DefinitionFingerprint,
                intent.WorkflowInputFingerprint,
                request.DefinitionId,
                request.DefinitionVersion,
                request.WorkflowInputFingerprint);
            return true;
        }

        conflict = null;
        return false;
    }

    private static bool MatchesStartBinding(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        string inputFingerprint,
        InboxStartOrDeliverAcceptance request) =>
        definitionId.Equals(request.DefinitionId) &&
        definitionVersion.Equals(request.DefinitionVersion) &&
        string.Equals(inputFingerprint, request.WorkflowInputFingerprint, StringComparison.Ordinal);

    private string? FindIncompatiblePendingStart(IReadOnlyList<StartIdempotencyWrite> operations)
    {
        foreach (var operation in operations)
        {
            if (!startIntents.TryGetValue(operation.IdempotencyKey, out var intent))
            {
                continue;
            }

            if (intent.State == InboxStartIntentState.Poisoned ||
                !intent.DefinitionId.Equals(operation.DefinitionId) ||
                !intent.DefinitionVersion.Equals(operation.DefinitionVersion) ||
                !string.Equals(intent.WorkflowInputFingerprint, operation.InputFingerprint, StringComparison.Ordinal))
            {
                return operation.IdempotencyKey;
            }
        }

        return null;
    }

    private void MaterializePendingStarts(IEnumerable<StartIdempotencyWrite> operations)
    {
        foreach (var operation in operations)
        {
            if (!startIntents.TryGetValue(operation.IdempotencyKey, out var intent) ||
                intent.State == InboxStartIntentState.Materialized)
            {
                continue;
            }

            startIntents[operation.IdempotencyKey] = intent with
            {
                State = InboxStartIntentState.Materialized,
                InstanceId = operation.InstanceId,
                DefinitionFingerprint = operation.DefinitionFingerprint
            };
            foreach (var pair in inbox
                         .Where(pair =>
                             pair.Value.State == InboxRecordState.Received &&
                             pair.Value.InstanceId is null &&
                             string.Equals(
                                 pair.Value.Envelope?.Route.StartIdempotencyKey,
                                 operation.IdempotencyKey,
                                 StringComparison.Ordinal))
                         .ToArray())
            {
                var envelope = pair.Value.Envelope ?? throw new InvalidOperationException(
                    "A pending start event must retain its normalized envelope.");
                var route = InboxRouteKey.Direct(
                    operation.InstanceId,
                    EventName.Create(envelope.EventName),
                    new EventContractVersion(envelope.EventContractVersion),
                    envelope.CorrelationId);
                inbox[pair.Key] = pair.Value with
                {
                    InstanceId = operation.InstanceId,
                    Route = route
                };
                inboxRouteRevisions[route] = CurrentRouteRevision(route) + 1;
            }
        }
    }

    private static InboxStartIntentRecord CloneStartIntent(InboxStartIntentRecord intent) =>
        intent with { WorkflowInputPayload = [.. intent.WorkflowInputPayload] };

    private void ApplyStartIdempotencyOperations(IEnumerable<StartIdempotencyWrite> operations)
    {
        foreach (var operation in operations)
        {
            startIdempotency.TryAdd(
                operation.IdempotencyKey,
                new StartedWorkflowIdempotencyRecord(
                    operation.IdempotencyKey,
                     operation.InstanceId,
                     operation.DefinitionId,
                     operation.DefinitionVersion,
                     operation.DefinitionFingerprint,
                     operation.InputFingerprint));
        }
    }

    private string? FindConflictingStartIdempotencyKey(IReadOnlyList<StartIdempotencyWrite> operations)
    {
        return operations
            .Select(operation => operation.IdempotencyKey)
            .GroupBy(key => key, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1 || startIdempotency.ContainsKey(group.Key))
            ?.Key;
    }

    private void ApplyProjectionOperations(IEnumerable<ProjectionWrite> operations)
    {
        foreach (var operation in operations)
        {
            projections.Add(operation);
            if (operation.InstanceSnapshot is { } snapshot)
            {
                var archivedAt = summaries.TryGetValue(operation.InstanceId, out var existing)
                    ? existing.ArchivedAt
                    : snapshot.ArchivedAt;
                summaries[operation.InstanceId] = CloneSnapshot(snapshot with { ArchivedAt = archivedAt });
            }

            if (operation.Kind == ProjectionOperationKind.AppendHistory && operation.History is { } entry)
            {
                history.Add(entry);
            }
        }
    }

    private static WorkflowProjectionSnapshot CloneSnapshot(WorkflowProjectionSnapshot snapshot)
    {
        return snapshot with
        {
            ActiveWaits = snapshot.ActiveWaits.Select(CloneActiveWait).ToArray()
        };
    }

    private static InboxRecord CloneInboxRecord(InboxRecord record)
    {
        return record with
        {
            Envelope = record.Envelope is null ? null : CloneEnvelope(record.Envelope),
            Route = record.Route
        };
    }

    private static WorkflowProjectionActiveWaitSnapshot CloneActiveWait(
        WorkflowProjectionActiveWaitSnapshot snapshot)
    {
        return snapshot with { };
    }

    private static OutboxWrite CloneOutboxWrite(OutboxWrite record)
    {
        return record with { Payload = [.. record.Payload] };
    }

    private static CheckpointWrite CloneCheckpointWrite(CheckpointWrite checkpoint)
    {
        return checkpoint with
        {
            Payload = [.. checkpoint.Payload],
            RuntimeState = CloneRuntimeState(checkpoint.RuntimeState)
        };
    }

    private static WorkflowRuntimeCheckpointState CloneRuntimeState(WorkflowRuntimeCheckpointState state)
    {
        return new WorkflowRuntimeCheckpointState
        {
            ActiveTimers = state.ActiveTimers.Select(timer => timer with { }).ToArray(),
            ActiveWaits = state.ActiveWaits.Select(wait => wait with { }).ToArray(),
            ActiveResourceTickets = state.ActiveResourceTickets.Select(ticket => ticket with { }).ToArray(),
            PendingResumes = state.PendingResumes.Select(pending => pending with
            {
                Payload = pending.Payload?.ToArray()
            }).ToArray(),
            ContinuationFailureCount = state.ContinuationFailureCount,
            ContinuationFailurePositionStreamVersion = state.ContinuationFailurePositionStreamVersion,
            ContinuationRetryNotBefore = state.ContinuationRetryNotBefore
        };
    }

    private bool IsActive(InstanceId instanceId)
    {
        return summaries.TryGetValue(instanceId, out var snapshot) && snapshot.Status is
            global::OrcaCore.WorkflowInstanceStatus.Pending or
            global::OrcaCore.WorkflowInstanceStatus.Running or
            global::OrcaCore.WorkflowInstanceStatus.Waiting or
            global::OrcaCore.WorkflowInstanceStatus.CancellationRequested;
    }

    private static bool IsTerminal(global::OrcaCore.WorkflowInstanceStatus status) =>
        status is global::OrcaCore.WorkflowInstanceStatus.Completed or
            global::OrcaCore.WorkflowInstanceStatus.Failed or
            global::OrcaCore.WorkflowInstanceStatus.TimedOut or
            global::OrcaCore.WorkflowInstanceStatus.Cancelled or
            global::OrcaCore.WorkflowInstanceStatus.Terminated;

    private bool HasClaimedOutbox(InstanceId instanceId)
    {
        return outbox.Values.Any(record =>
            record.InstanceId.Equals(instanceId) &&
            record.State == OutboxRecordState.Claimed);
    }

    private void DeleteInstanceData(InstanceId instanceId)
    {
        summaries.Remove(instanceId);
        checkpoints.Remove(instanceId);
        streams.Remove(new WorkflowStreamId(instanceId));
        foreach (var timerId in timers.Values
            .Where(timer => timer.Request.InstanceId.Equals(instanceId))
            .Select(timer => timer.Request.TimerId)
            .ToArray())
        {
            timers.Remove(timerId);
        }

        var purgedHistoryIds = projections
            .Where(operation => operation.InstanceId.Equals(instanceId) && operation.History is not null)
            .Select(operation => operation.History!.HistoryId)
            .ToHashSet();
        history.RemoveAll(entry => purgedHistoryIds.Contains(entry.HistoryId));
        foreach (var recordId in outbox.Values
            .Where(record => record.InstanceId.Equals(instanceId))
            .Select(record => record.Write.OutboxRecordId)
            .ToArray())
        {
            outbox.Remove(recordId);
        }
    }

    private static bool IsOutboxClaimable(InMemoryOutboxRecord record, DateTimeOffset claimedAt)
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

    internal sealed record InMemoryOutboxRecord(
        InstanceId InstanceId,
        OutboxWrite Write,
        OutboxRecordState State,
        DateTimeOffset? ClaimedUntil = null,
        string? PoisonCode = null,
        string? PoisonDetail = null);

    internal sealed record InMemoryTimerSchedule(TimerScheduleRequest Request, DateTimeOffset? ClaimedUntil);
}
