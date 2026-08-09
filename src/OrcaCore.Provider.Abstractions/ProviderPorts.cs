using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using DurableWorkflowEvent = global::OrcaCore.Abstractions.Durable.WorkflowEvent;
using ProjectionWorkflowInstanceSnapshot = global::OrcaCore.Abstractions.Providers.WorkflowProjectionSnapshot;

namespace OrcaCore.Abstractions.Providers;

/// <summary>
/// Stores durable workflow event streams and checkpoints.
/// </summary>
public interface IWorkflowEventStore
{
    /// <summary>
    /// Loads the latest checkpoint for one workflow instance when a provider has one.
    /// </summary>
    Task<Option<CheckpointWrite>> LoadCheckpointAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Appends one accepted mutation with optimistic concurrency.
    /// </summary>
    Task<Result<AppendEventsResult>> AppendAsync(
        ProviderCommitBatch batch,
        CancellationToken cancellationToken);

    /// <summary>
    /// Loads stream events after the supplied version.
    /// </summary>
    Task<IReadOnlyList<DurableWorkflowEvent>> LoadTailAsync(
        WorkflowStreamId streamId,
        StreamVersion afterVersion,
        CancellationToken cancellationToken);
}

/// <summary>
/// Stores durable inbound event delivery records.
/// </summary>
public interface IWorkflowInboxStore
{
    /// <summary>
    /// Atomically owns a normalized inbound envelope and advances its exact route revision.
    /// </summary>
    Task<InboxAcceptanceCommitResult> AcceptAsync(
        InboxAcceptance acceptance,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("This provider does not implement durable pending-event acceptance.");

    /// <summary>
    /// Atomically owns one definition-fanout envelope together with the complete provider-visible
    /// snapshot of current nonterminal targets. A rejected limit check commits neither ownership
    /// nor a partial target set.
    /// </summary>
    Task<InboxAcceptanceCommitResult> AcceptDefinitionFanoutAsync(
        InboxDefinitionFanoutAcceptance acceptance,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("This provider does not implement durable definition fanout.");

    /// <summary>
    /// Atomically owns one start-or-deliver envelope and reserves or reuses its exact
    /// definition/version/fixed-codec-input binding.
    /// </summary>
    Task<InboxAcceptanceCommitResult> AcceptStartOrDeliverAsync(
        InboxStartOrDeliverAcceptance acceptance,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("This provider does not implement durable pending start intents.");

    /// <summary>Gets the durable intent bound to one start idempotency key.</summary>
    Task<Option<InboxStartIntentRecord>> GetStartIntentAsync(
        string startIdempotencyKey,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("This provider does not implement durable pending start intents.");

    /// <summary>
    /// Gets the globally owned envelope for one event identity before route or target-state evaluation.
    /// </summary>
    Task<Option<InboxRecord>> GetByEventIdAsync(
        EventId eventId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets an inbox record by its target instance and event identity.
    /// </summary>
    Task<Option<InboxRecord>> GetAsync(
        InstanceId instanceId,
        EventId eventId,
        CancellationToken cancellationToken);

    /// <summary>Lists the immutable per-instance target records captured for one accepted fanout event.</summary>
    Task<IReadOnlyList<InboxRecord>> ListDefinitionFanoutTargetsAsync(
        EventId eventId,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("This provider does not implement durable definition fanout.");

    /// <summary>
    /// Reads the oldest eligible pending event and the route revisions that make a later
    /// wait/match commit conditional on this exact view.
    /// </summary>
    Task<InboxMatchSnapshot> GetMatchSnapshotAsync(
        InboxMatchRequest request,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("This provider does not implement durable pending-event matching.");

    /// <summary>
    /// Reads one stable page of globally accepted <see cref="InboxRecordState.Received"/> records
    /// after the supplied acceptance sequence. The caller uses the durable acceptance order as a
    /// restart-safe scan cursor; consuming commits remain serialized by route revision and inbox
    /// expected-state checks.
    /// </summary>
    Task<IReadOnlyList<InboxRecord>> ListReceivedAsync(
        long afterAcceptanceSequence,
        int maxCount,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reads a stable page of failed handoffs that are eligible for another attempt. This retry
    /// lane is independent of the forward acceptance cursor so continuous new acceptance cannot
    /// starve an older failed record.
    /// </summary>
    Task<IReadOnlyList<InboxRecord>> ListHandoffRetriesAsync(
        DateTimeOffset eligibleAt,
        int maxCount,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records an operator-visible terminal disposition without deleting ownership when the
    /// current state still equals <paramref name="expectedState"/>.
    /// </summary>
    Task MarkPoisonedAsync(
        EventId eventId,
        InboxRecordState expectedState,
        string code,
        string? detail,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("This provider does not implement observable inbox poison state.");

    /// <summary>Marks one exact route or fanout-target inbox record as terminal poison.</summary>
    Task MarkPoisonedAsync(
        InboxRecordIdentity recordIdentity,
        InboxRecordState expectedState,
        string code,
        string? detail,
        CancellationToken cancellationToken) =>
        MarkPoisonedAsync(recordIdentity.EventId, expectedState, code, detail, cancellationToken);

    /// <summary>
    /// Conditionally records one failed autonomous handoff attempt. The update applies only while
    /// the record remains in <paramref name="expectedState"/> with the observed failure count.
    /// Reaching <paramref name="maxFailureCount"/> terminalizes the accepted record as poison;
    /// otherwise the record remains owned and becomes eligible again at
    /// <paramref name="retryNotBefore"/>.
    /// </summary>
    Task RecordHandoffFailureAsync(
        EventId eventId,
        InboxRecordState expectedState,
        int expectedFailureCount,
        int maxFailureCount,
        DateTimeOffset retryNotBefore,
        string code,
        string? detail,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("This provider does not implement durable inbox handoff retries.");

    /// <summary>Conditionally records one failed handoff for an exact route or fanout-target record.</summary>
    Task RecordHandoffFailureAsync(
        InboxRecordIdentity recordIdentity,
        InboxRecordState expectedState,
        int expectedFailureCount,
        int maxFailureCount,
        DateTimeOffset retryNotBefore,
        string code,
        string? detail,
        CancellationToken cancellationToken) =>
        RecordHandoffFailureAsync(
            recordIdentity.EventId,
            expectedState,
            expectedFailureCount,
            maxFailureCount,
            retryNotBefore,
            code,
            detail,
            cancellationToken);
}

/// <summary>Well-known durable inbox route discriminators.</summary>
public static class InboxRouteKinds
{
    /// <summary>Routes an event to one exact workflow instance.</summary>
    public const string Direct = "direct";

    /// <summary>Routes an event through an exact definition and correlation identity.</summary>
    public const string Correlation = "correlation";

    /// <summary>Owns one definition-wide fanout envelope root.</summary>
    public const string DefinitionFanout = "definition-fanout";

    /// <summary>Owns one per-instance member of a definition-wide fanout.</summary>
    public const string DefinitionFanoutTarget = "definition-fanout-target";

    /// <summary>Owns one exact-definition start-or-deliver intent.</summary>
    public const string StartOrDeliver = "start-or-deliver";
}

/// <summary>Identifies one serialized direct, correlation, or per-instance fanout inbox route.</summary>
public sealed record InboxRouteKey
{
    private InboxRouteKey(
        string kind,
        InstanceId? instanceId,
        DefinitionId? definitionId,
        EventName eventName,
        EventContractVersion eventContractVersion,
        CorrelationId correlationId)
    {
        Kind = kind;
        InstanceId = instanceId;
        DefinitionId = definitionId;
        EventName = eventName;
        EventContractVersion = eventContractVersion;
        CorrelationId = correlationId;
    }

    public string Kind { get; }

    public InstanceId? InstanceId { get; }

    public DefinitionId? DefinitionId { get; }

    public EventName EventName { get; }

    public EventContractVersion EventContractVersion { get; }

    public CorrelationId CorrelationId { get; }

    public static InboxRouteKey Direct(
        InstanceId instanceId,
        EventName eventName,
        EventContractVersion eventContractVersion,
        CorrelationId correlationId) =>
        new(InboxRouteKinds.Direct, instanceId, null, eventName, eventContractVersion, correlationId);

    public static InboxRouteKey Correlation(
        DefinitionId definitionId,
        EventName eventName,
        EventContractVersion eventContractVersion,
        CorrelationId correlationId) =>
        new(InboxRouteKinds.Correlation, null, definitionId, eventName, eventContractVersion, correlationId);

    public static InboxRouteKey DefinitionFanoutTarget(
        InstanceId instanceId,
        DefinitionId definitionId,
        EventName eventName,
        EventContractVersion eventContractVersion,
        CorrelationId correlationId) =>
        new(
            InboxRouteKinds.DefinitionFanoutTarget,
            instanceId,
            definitionId,
            eventName,
            eventContractVersion,
            correlationId);
}

/// <summary>Requests durable ownership of one normalized inbound envelope.</summary>
public sealed record InboxAcceptance(
    DurableEventEnvelope Envelope,
    string EnvelopeFingerprint,
    DateTimeOffset AcceptedAt);

/// <summary>Requests atomic ownership and target snapshotting for one definition-fanout envelope.</summary>
public sealed record InboxDefinitionFanoutAcceptance(
    InboxAcceptance Acceptance,
    DefinitionId DefinitionId,
    int MaximumTargetCount);

/// <summary>Requests atomic ownership of one exact-definition start-or-deliver envelope.</summary>
public sealed record InboxStartOrDeliverAcceptance(
    InboxAcceptance Acceptance,
    DefinitionId DefinitionId,
    DefinitionVersion DefinitionVersion,
    string StartIdempotencyKey,
    string WorkflowInputContentType,
    byte[] WorkflowInputPayload,
    string WorkflowInputFingerprint);

/// <summary>Describes the durable state of one input-bound pending start intent.</summary>
public enum InboxStartIntentState
{
    Pending,
    Materialized,
    Poisoned
}

/// <summary>Describes one durable start intent keyed independently of its retained events.</summary>
public sealed record InboxStartIntentRecord(
    string StartIdempotencyKey,
    DefinitionId DefinitionId,
    DefinitionVersion DefinitionVersion,
    string WorkflowInputContentType,
    byte[] WorkflowInputPayload,
    string WorkflowInputFingerprint,
    InboxStartIntentState State)
{
    public InstanceId? InstanceId { get; init; }

    public string? DefinitionFingerprint { get; init; }

    public string? PoisonCode { get; init; }

    public string? PoisonDetail { get; init; }
}

/// <summary>Captures a known incompatible reuse of a durable pending-start binding.</summary>
public sealed record InboxStartBindingConflict(
    DefinitionId ExistingDefinitionId,
    DefinitionVersion ExistingDefinitionVersion,
    string? ExistingDefinitionFingerprint,
    string ExistingInputFingerprint,
    DefinitionId AttemptedDefinitionId,
    DefinitionVersion AttemptedDefinitionVersion,
    string AttemptedInputFingerprint);

/// <summary>Describes how an inbox acceptance attempt resolved.</summary>
public sealed record InboxAcceptanceCommitResult(
    InboxAcceptanceCommitDisposition Disposition,
    InboxRecord? Record)
{
    /// <summary>Gets the immutable target membership committed for definition fanout.</summary>
    public IReadOnlyList<InstanceId> DefinitionFanoutTargets { get; init; } = [];

    /// <summary>Gets the incompatible start binding when the disposition is StartConflict.</summary>
    public InboxStartBindingConflict? StartConflict { get; init; }
}

/// <summary>Closed provider result for global inbound-event ownership.</summary>
public enum InboxAcceptanceCommitDisposition
{
    Accepted,
    Duplicate,
    Conflict,
    DirectInstanceNotFound,
    DirectInstanceTerminal,
    StartConflict,
    FanoutLimitExceeded
}

/// <summary>Describes the exact wait route whose pending event is being inspected.</summary>
public sealed record InboxMatchRequest(
    InstanceId InstanceId,
    DefinitionId DefinitionId,
    EventName EventName,
    EventContractVersion EventContractVersion,
    CorrelationId CorrelationId)
{
    public IReadOnlyList<InboxRouteKey> Routes { get; } =
    [
        InboxRouteKey.Direct(InstanceId, EventName, EventContractVersion, CorrelationId),
        InboxRouteKey.Correlation(DefinitionId, EventName, EventContractVersion, CorrelationId),
        InboxRouteKey.DefinitionFanoutTarget(
            InstanceId,
            DefinitionId,
            EventName,
            EventContractVersion,
            CorrelationId)
    ];
}

/// <summary>Captures one route revision observed while matching a pending event.</summary>
public sealed record InboxRouteRevision(InboxRouteKey Route, long Revision);

/// <summary>Captures the oldest pending event and the exact route revisions observed with it.</summary>
public sealed record InboxMatchSnapshot(
    InboxRecord? PendingEvent,
    IReadOnlyList<InboxRouteRevision> RouteRevisions);

/// <summary>
/// Describes the durable identity and state of one accepted inbound envelope.
/// </summary>
public sealed record InboxRecord(
    InstanceId? InstanceId,
    EventId EventId,
    string EnvelopeFingerprint,
    InboxRecordState State)
{
    /// <summary>Gets the complete normalized envelope and route persisted at first acceptance.</summary>
    public DurableEventEnvelope? Envelope { get; init; }

    /// <summary>Gets the serialized route boundary that owns this record.</summary>
    public InboxRouteKey? Route { get; init; }

    /// <summary>Gets the provider-assigned global durable acceptance order.</summary>
    public long AcceptanceSequence { get; init; }

    /// <summary>Gets when durable ownership completed.</summary>
    public DateTimeOffset AcceptedAt { get; init; }

    /// <summary>Gets the stable operator-facing poison/dead-letter code, when terminal.</summary>
    public string? PoisonCode { get; init; }

    /// <summary>Gets optional operator-facing poison/dead-letter detail.</summary>
    public string? PoisonDetail { get; init; }

    /// <summary>Gets the number of failed autonomous handoff attempts observed for this record.</summary>
    public int HandoffFailureCount { get; init; }

    /// <summary>Gets the earliest instant at which another autonomous handoff attempt is eligible.</summary>
    public DateTimeOffset? HandoffRetryNotBefore { get; init; }
}

/// <summary>Identifies one route-level record or one independent fanout target.</summary>
public sealed record InboxRecordIdentity(EventId EventId, InstanceId? TargetInstanceId = null);

/// <summary>
/// Stores durable start idempotency mappings.
/// </summary>
public interface IWorkflowStartIdempotencyStore
{
    /// <summary>
    /// Gets the instance bound to one start idempotency key, when present.
    /// </summary>
    Task<Option<StartedWorkflowIdempotencyRecord>> GetStartedAsync(
        string idempotencyKey,
        CancellationToken cancellationToken);
}

/// <summary>
/// Describes the workflow instance that won a durable start idempotency key.
/// </summary>
public sealed record StartedWorkflowIdempotencyRecord(
    string IdempotencyKey,
    InstanceId InstanceId,
    DefinitionId DefinitionId,
    DefinitionVersion DefinitionVersion,
    string DefinitionFingerprint,
    string InputFingerprint);

/// <summary>
/// Stores durable outbound dispatch records.
/// </summary>
public interface IWorkflowOutboxStore
{
    /// <summary>
    /// Claims records eligible for dispatch.
    /// </summary>
    Task<IReadOnlyList<OutboxWrite>> ClaimAsync(int maxCount, CancellationToken cancellationToken);

    /// <summary>
    /// Claims records eligible for dispatch with a recoverable lease.
    /// </summary>
    Task<IReadOnlyList<OutboxWrite>> ClaimAsync(
        OutboxClaimRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets the dispatch state for one outbox record.
    /// </summary>
    Task<Option<OutboxRecordState>> GetStateAsync(
        OutboxRecordId outboxRecordId,
        CancellationToken cancellationToken);

    /// <summary>Gets the dispatch state and immutable terminal poison detail for one record.</summary>
    async Task<Option<OutboxDispatchSnapshot>> GetDispatchSnapshotAsync(
        OutboxRecordId outboxRecordId,
        CancellationToken cancellationToken)
    {
        var state = await GetStateAsync(outboxRecordId, cancellationToken).ConfigureAwait(false);
        return state.HasValue
            ? Option<OutboxDispatchSnapshot>.Some(new OutboxDispatchSnapshot(state.Value))
            : Option<OutboxDispatchSnapshot>.None;
    }

    /// <summary>
    /// Marks one outbox record with a dispatch state.
    /// </summary>
    Task MarkAsync(
        OutboxRecordId outboxRecordId,
        OutboxRecordState state,
        CancellationToken cancellationToken);

    /// <summary>Atomically terminalizes one claimed record with immutable poison detail.</summary>
    Task MarkPoisonedAsync(
        OutboxRecordId outboxRecordId,
        string code,
        string? detail,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return MarkAsync(outboxRecordId, OutboxRecordState.Poisoned, cancellationToken);
    }

    /// <summary>
    /// Releases one claimed outbox record so it can be retried.
    /// </summary>
    Task ReleaseAsync(OutboxRecordId outboxRecordId, CancellationToken cancellationToken);
}

/// <summary>
/// Stores query and routing projections derived from workflow events.
/// </summary>
public interface IWorkflowProjectionStore
{
    /// <summary>
    /// Applies projection operations included in a commit batch.
    /// </summary>
    Task ApplyAsync(IReadOnlyList<ProjectionWrite> operations, CancellationToken cancellationToken);

    /// <summary>
    /// Gets one projected instance summary by its exact runtime identity.
    /// </summary>
    Task<Option<ProjectionWorkflowInstanceSnapshot>> GetAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves the exact active-wait candidates used by correlation delivery.
    /// </summary>
    Task<IReadOnlyList<ProjectionWorkflowInstanceSnapshot>> FindActiveWaitsAsync(
        DefinitionId? definitionId,
        EventName eventName,
        CorrelationId correlationId,
        CancellationToken cancellationToken);

    /// <summary>Resolves exact active-wait candidates including event-contract version identity.</summary>
    async Task<IReadOnlyList<ProjectionWorkflowInstanceSnapshot>> FindActiveWaitsAsync(
        DefinitionId? definitionId,
        EventName eventName,
        EventContractVersion eventContractVersion,
        CorrelationId correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventContractVersion);
        var candidates = await FindActiveWaitsAsync(
            definitionId,
            eventName,
            correlationId,
            cancellationToken).ConfigureAwait(false);
        return candidates
            .Where(snapshot => snapshot.ActiveWaits.Any(wait =>
                string.Equals(wait.EventName, eventName.Value, StringComparison.Ordinal) &&
                wait.EventContractVersion == eventContractVersion.Value &&
                wait.CorrelationId.Equals(correlationId)))
            .ToArray();
    }

    /// <summary>
    /// Lists the runtime candidates inspected by trusted lease recovery and diagnostics.
    /// </summary>
    Task<IReadOnlyList<ProjectionWorkflowInstanceSnapshot>> ListLeaseRecoveryCandidatesAsync(
        CancellationToken cancellationToken);
}

/// <summary>
/// Schedules durable timer wake-up commands.
/// </summary>
public interface ITimerScheduler
{
    /// <summary>
    /// Schedules a durable wake-up.
    /// </summary>
    Task ScheduleAsync(TimerScheduleRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Claims due wake-ups and returns timer-fire commands exactly once.
    /// </summary>
    Task<IReadOnlyList<FireTimerCommand>> ClaimDueAsync(
        DateTimeOffset dueAtOrBefore,
        int maxCount,
        CancellationToken cancellationToken);

    /// <summary>
    /// Claims due wake-ups with a recoverable lease and returns timer-fire commands.
    /// </summary>
    Task<IReadOnlyList<FireTimerCommand>> ClaimDueAsync(
        TimerClaimRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Completes one claimed timer after its fire command has been durably processed.
    /// </summary>
    Task CompleteAsync(TimerId timerId, CancellationToken cancellationToken);

    /// <summary>
    /// Releases one claimed timer so it can be retried.
    /// </summary>
    Task ReleaseAsync(TimerId timerId, CancellationToken cancellationToken);
}

/// <summary>
/// Dispatches normalized outbox records to an external transport.
/// </summary>
public interface IMessageDispatcher
{
    /// <summary>
    /// Dispatches one outbox record.
    /// </summary>
    Task<DispatchResult> DispatchAsync(OutboxWrite record, CancellationToken cancellationToken);
}

/// <summary>
/// Describes a durable timer schedule request.
/// </summary>
public sealed record TimerScheduleRequest
{
    /// <summary>
    /// Gets the timer identity.
    /// </summary>
    public required TimerId TimerId { get; init; }

    /// <summary>
    /// Gets the target workflow instance.
    /// </summary>
    public required InstanceId InstanceId { get; init; }

    /// <summary>
    /// Gets the command identity to use when the timer fires.
    /// </summary>
    public required CommandId CommandId { get; init; }

    /// <summary>
    /// Gets when the wake-up becomes due.
    /// </summary>
    public required DateTimeOffset FireAt { get; init; }

    /// <summary>
    /// Gets the logical wake-up name.
    /// </summary>
    public required string WakeupName { get; init; }
}

/// <summary>
/// Describes a recoverable durable outbox claim request. The optional kind selector partitions
/// the outbox so disjoint pumps consume disjoint record kinds (DR-037): the external message
/// dispatcher excludes the internal <c>continue</c> kind, and the continuation pump includes
/// only it. When no selector is set, all kinds are claimable (backward-compatible default).
/// </summary>
public sealed record OutboxClaimRequest(
    int MaxCount,
    DateTimeOffset ClaimedAt,
    TimeSpan LeaseDuration)
{
    /// <summary>
    /// Gets the kind selector. Null claims any kind; an include set claims only listed kinds;
    /// an exclude set claims any kind except the listed ones.
    /// </summary>
    public OutboxKindSelector? KindSelector { get; init; }
}

/// <summary>
/// Selects outbox record kinds for a partitioned claim (DR-037). Exactly one of
/// <see cref="Include"/> or <see cref="Exclude"/> is set.
/// </summary>
public sealed record OutboxKindSelector
{
    private OutboxKindSelector(IReadOnlySet<string>? include, IReadOnlySet<string>? exclude)
    {
        Include = include;
        Exclude = exclude;
    }

    /// <summary>
    /// Gets the kinds to claim exclusively, when this is an include selector.
    /// </summary>
    public IReadOnlySet<string>? Include { get; }

    /// <summary>
    /// Gets the kinds to skip, when this is an exclude selector.
    /// </summary>
    public IReadOnlySet<string>? Exclude { get; }

    /// <summary>
    /// Creates a selector that claims only the listed kinds.
    /// </summary>
    public static OutboxKindSelector Including(params string[] kinds)
    {
        ArgumentNullException.ThrowIfNull(kinds);
        return new OutboxKindSelector(new HashSet<string>(kinds, StringComparer.Ordinal), null);
    }

    /// <summary>
    /// Creates a selector that claims any kind except the listed ones.
    /// </summary>
    public static OutboxKindSelector Excluding(params string[] kinds)
    {
        ArgumentNullException.ThrowIfNull(kinds);
        return new OutboxKindSelector(null, new HashSet<string>(kinds, StringComparer.Ordinal));
    }

    /// <summary>
    /// Returns whether a record of the given kind matches this selector.
    /// </summary>
    public bool Matches(string kind)
    {
        ArgumentNullException.ThrowIfNull(kind);
        if (Include is not null)
        {
            return Include.Contains(kind);
        }

        return Exclude is null || !Exclude.Contains(kind);
    }
}

/// <summary>
/// Well-known durable outbox record kinds.
/// </summary>
public static class OutboxKinds
{
    /// <summary>Application-shaped workflow event dispatched through the public dispatcher.</summary>
    public const string WorkflowEvent = "workflow-event";

    /// <summary>
    /// Internal restart-safe continuation signal consumed only by the continuation pump (DR-034).
    /// External message dispatchers never receive this kind.
    /// </summary>
    public const string Continue = "continue";
}

/// <summary>
/// Describes a recoverable durable timer claim request.
/// </summary>
public sealed record TimerClaimRequest(
    DateTimeOffset DueAtOrBefore,
    int MaxCount,
    DateTimeOffset ClaimedAt,
    TimeSpan LeaseDuration);

/// <summary>
/// Describes dispatch outcome categories.
/// </summary>
public enum DispatchResult
{
    /// <summary>
    /// The record was dispatched successfully.
    /// </summary>
    Success,

    /// <summary>
    /// The dispatch failed and can be retried.
    /// </summary>
    RetryableFailure,

    /// <summary>
    /// The dispatch failed permanently.
    /// </summary>
    PermanentFailure
}
