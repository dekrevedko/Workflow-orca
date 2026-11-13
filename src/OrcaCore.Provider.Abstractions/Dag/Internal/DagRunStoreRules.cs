using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Serialization;

namespace OrcaCore.Abstractions.Providers.Dag.Internal;

// Private source shared by the three owning providers, not an exported provider
// implementation, extra assembly, friend edge or separate outbox transport.
internal sealed class DagRunStoredState
{
    internal required DagRunSnapshotV1 Snapshot { get; set; }
    internal required DagRunCheckpointV1 Checkpoint { get; set; }
    internal long CreationSequence { get; set; }
    internal long Fence { get; set; }
    internal Guid? ActiveRequest { get; set; }
    internal DateTimeOffset LastOperationalAt { get; set; }
    internal bool IsStuck { get; set; }
    internal List<DagRunRecordV1> NewRecords { get; } = [];
    internal Dictionary<Guid, DagRunOperationReceipt> Operations { get; } = [];
    internal Dictionary<Guid, DagRunLeaseRow> Requests { get; } = [];
    internal Dictionary<Guid, DagRunObserverRow> Observers { get; } = [];
    internal List<OutboxWrite> NewOutbox { get; } = [];
}

internal sealed record DagRunOperationReceipt(
    Guid OperationId,
    string OperationFingerprint,
    long ResultVersion,
    string Outcome,
    int? Ordinal,
    Guid? ChildInstanceId,
    string? MappedInputFingerprint);

internal sealed record DagRunLeaseRow(
    Guid RequestId,
    Guid OwnerId,
    DagRunLeasePurposeV1 Purpose,
    long QueueSequence,
    string State,
    DateTimeOffset WaitExpiresAt,
    DateTimeOffset? GrantedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? HardDeadline,
    long? GrantedToken,
    bool AppendCommitted);

internal sealed record DagRunObserverRow(DagRunSubscriptionV1 Subscription, DateTimeOffset ExpiresAt);

internal sealed record DagRunCheckpointFrame(
    string AggregateKind,
    int Version,
    Guid RunId,
    long StreamVersion,
    DagRunSnapshotV1? Snapshot);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(DagRunSnapshotV1))]
[JsonSerializable(typeof(DagRunCheckpointFrame))]
[JsonSerializable(typeof(DagRunOperationReceipt))]
[JsonSerializable(typeof(DagRunGraphV1))]
[JsonSerializable(typeof(DagRunValueV1))]
[JsonSerializable(typeof(DagRunBindingV1))]
[JsonSerializable(typeof(int[]))]
internal sealed partial class DagRunStoreJsonContext : JsonSerializerContext;

internal static class DagRunStoreRules
{
    internal const string ReadyKind = "dag-run-ready-v1";
    internal const string Waiting = "Waiting";
    internal const string Granted = "Granted";
    internal const string Consumed = "Consumed";
    internal const string CheckpointContentType = "application/vnd.orcacore.dag-run+json;v=1";
    internal static readonly TimeSpan MaximumLease = TimeSpan.FromSeconds(5);
    internal static readonly TimeSpan MaximumTurn = TimeSpan.FromSeconds(15);
    internal static readonly TimeSpan WaiterLifetime = TimeSpan.FromSeconds(30);
    internal static readonly TimeSpan MaximumSubscription = TimeSpan.FromSeconds(30);

    internal static Guid RequireDagStream(WorkflowStreamId id)
    {
        if (id.AggregateKind != DurableAggregateKind.DagRun || id.DagRunId is not { } run || run == Guid.Empty)
            throw new InvalidOperationException("This port requires a nonempty DAG run stream.");
        return run;
    }

    internal static void ValidateBatch(ProviderCommitBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        _ = RequireDagStream(batch.StreamId);
        if (batch.DagRun is null || batch.Events.Count != 0 || batch.Checkpoint is not null ||
            batch.InboxOperations.Count != 0 || batch.InboxRouteMutations.Count != 0 ||
            batch.InboxTargetPoisonOperations.Count != 0 || batch.ProjectionOperations.Count != 0 ||
            batch.TimerSchedules.Count != 0 || batch.StartIdempotencyOperations.Count != 0)
            throw new InvalidOperationException("DAG_PROTOCOL_INVALID: DAG append cannot contain workflow writes.");
        if (batch.DagRun.OperationId == Guid.Empty || !LowerHash(batch.DagRun.OperationFingerprint) ||
            batch.DagRun.Records.Count == 0)
            throw Invalid("operation identity or empty append");
        if (batch.DagRun.Records.Count > 2 || (batch.DagRun.Records.Count == 2 &&
            (batch.DagRun.Records[0].Kind == DagRunRecordKindV1.Terminal || batch.DagRun.Records[1].Kind != DagRunRecordKindV1.Terminal)))
            throw Invalid("one mutation fact plus optional terminal closure per turn");
        foreach (var record in batch.DagRun.Records)
        {
            if (record.RunId != batch.StreamId.DagRunId || record.OperationId != batch.DagRun.OperationId)
                throw Invalid("record/append coordinates");
            _ = DagRunRecordCodecV1.Serialize(record);
        }

        if (!string.Equals(OperationFingerprint(batch.DagRun.Records, batch.OutboxRecords),
                batch.DagRun.OperationFingerprint, StringComparison.Ordinal)) throw Invalid("operation fingerprint");
        foreach (var outbox in batch.OutboxRecords)
        {
            if (outbox.DagRunOwner != batch.StreamId.DagRunId || outbox.DispatchAttempt != 0 ||
                outbox.Kind is not ("dag-child-start-v1" or "dag-child-join-v1" or "dag-child-cancel-v1"
                    or ReadyKind) ||
                (outbox.RecipientHostId is not null &&
                 (outbox.Kind != ReadyKind || outbox.DagRunSubscriptionId is null)) ||
                (outbox.DagRunSubscriptionId is not null && outbox.RecipientHostId is null) ||
                outbox.RecipientHostId == Guid.Empty || outbox.DagRunSubscriptionId == Guid.Empty)
                throw Invalid("outbox ownership or kind");
        }
    }

    internal static Result<AppendEventsResult> Append(DagRunStoredState? state, ProviderCommitBatch batch,
        DateTimeOffset now, long creationSequence, out DagRunStoredState? updated)
    {
        updated = state;
        var append = batch.DagRun!;
        if (state?.Operations.TryGetValue(append.OperationId, out var receipt) == true)
            return receipt.OperationFingerprint == append.OperationFingerprint
                ? Result<AppendEventsResult>.Success(new AppendEventsResult(new StreamVersion(receipt.ResultVersion)))
                : Failure("DAG_INTEGRITY_CONFLICT", "operation identity was reused with different content");
        if ((state?.Snapshot.StreamVersion ?? StreamVersion.Empty) != batch.ExpectedVersion)
            return Failure("DAG_VERSION_CONFLICT", "expected version differs from the committed head");
        DagRunLeaseRow? request = null;
        if (state is null)
        {
            if (append.Fence is not null || batch.ExpectedVersion != StreamVersion.Empty ||
                append.Records[0].Kind != DagRunRecordKindV1.Created)
                return Failure("DAG_PROTOCOL_INVALID", "invalid creation append");
        }
        else
        {
            now = Clamp(state, now);
            if (append.Fence is not { } fence || !MatchesLiveFence(state, fence, now, out request))
                return Failure("DAG_STALE_FENCE", "turn is absent, expired or replaced");
            if (request!.AppendCommitted)
                return Failure("DAG_TURN_ALREADY_COMMITTED", "turn already committed another operation");
            if (append.Records.Any(record => !PurposePermits(request.Purpose, record.Kind)))
                return Failure("DAG_PROTOCOL_INVALID", "turn purpose does not permit these records");
        }

        try
        {
            var snapshot = state?.Snapshot;
            foreach (var record in append.Records) snapshot = DagRunStateReducer.Apply(snapshot, record);
            var expected = snapshot!;
            if (expected.Nodes.All(n => n.State is DagNodeStateV1.Succeeded or DagNodeStateV1.Failed or
                    DagNodeStateV1.Cancelled or DagNodeStateV1.DependencyBlocked) &&
                !DagRunStateReducer.Terminal(expected.State))
                throw Invalid("terminal closure must be committed in the same append");
            ValidateCheckpoint(append.Checkpoint, expected);
            updated = state ?? new DagRunStoredState
            {
                Snapshot = expected, Checkpoint = Clone(append.Checkpoint), CreationSequence = creationSequence,
                LastOperationalAt = now
            };
            updated.Snapshot = expected;
            updated.Checkpoint = Clone(append.Checkpoint);
            updated.LastOperationalAt = now;
            updated.IsStuck = false;
            updated.NewRecords.AddRange(append.Records);
            var admitted = append.Records.SelectMany(r => r.ChangedNodes).FirstOrDefault(n => n.InputWrite is not null);
            updated.Operations.Add(append.OperationId, new DagRunOperationReceipt(append.OperationId,
                append.OperationFingerprint, expected.StreamVersion.Value, "Committed", admitted?.Ordinal,
                admitted?.ChildInstanceId?.Value, admitted?.InputWrite?.Fingerprint));
            if (request is not null) updated.Requests[request.RequestId] = request with { AppendCommitted = true };
            updated.NewOutbox.AddRange(batch.OutboxRecords.Select(Clone));
            AddNotifications(updated, now);
            return Result<AppendEventsResult>.Success(new AppendEventsResult(expected.StreamVersion));
        }
        catch (InvalidDataException exception)
        {
            return Failure("DAG_PROTOCOL_INVALID", exception.Message);
        }
    }

    internal static void ValidateLeaseRequest(DagRunLeaseRequestV1 request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.RunId == Guid.Empty || request.OwnerId == Guid.Empty || request.RequestId == Guid.Empty ||
            !Enum.IsDefined(request.Purpose) || request.LeaseDuration <= TimeSpan.Zero ||
            request.LeaseDuration > MaximumLease)
            throw new ArgumentException("Invalid DAG lease identity, purpose or duration.", nameof(request));
    }

    internal static DagRunLeaseResultV1 Acquire(DagRunStoredState? state, DagRunLeaseRequestV1 request,
        DateTimeOffset now, Func<long> nextSequence)
    {
        ValidateLeaseRequest(request);
        if (state is null) return Lease(DagRunLeaseOutcomeV1.NotFound);
        if (DagRunStateReducer.Terminal(state.Snapshot.State)) return Lease(DagRunLeaseOutcomeV1.Terminal, state);
        now = Clamp(state, now);
        Expire(state, now);
        if (state.Requests.TryGetValue(request.RequestId, out var row))
        {
            if (row.OwnerId != request.OwnerId || row.Purpose != request.Purpose)
                throw new InvalidDataException("DAG_INTEGRITY_CONFLICT: lease request identity changed.");
            if (row.State == Consumed) return Lease(DagRunLeaseOutcomeV1.StaleFence, state);
            if (row.State == Granted) return Lease(DagRunLeaseOutcomeV1.Granted, state, row);
            row = row with { WaitExpiresAt = now + WaiterLifetime };
        }
        else
        {
            if (state.Requests.Values.Any(r => r.OwnerId == request.OwnerId && r.State is Waiting or Granted))
                return Lease(DagRunLeaseOutcomeV1.OwnerBusy, state);
            row = new DagRunLeaseRow(request.RequestId, request.OwnerId, request.Purpose, nextSequence(),
                Waiting, now + WaiterLifetime, null, null, null, null, false);
        }

        state.Requests[row.RequestId] = row;
        if (state.ActiveRequest is null && state.Requests.Values.Where(r => r.State == Waiting)
                .MinBy(r => r.QueueSequence)?.RequestId == row.RequestId)
        {
            state.Fence = checked(state.Fence + 1);
            row = row with
            {
                State = Granted, GrantedAt = now, ExpiresAt = now + request.LeaseDuration,
                HardDeadline = now + MaximumTurn, GrantedToken = state.Fence
            };
            state.Requests[row.RequestId] = row;
            state.ActiveRequest = row.RequestId;
            return Lease(DagRunLeaseOutcomeV1.Granted, state, row);
        }

        return Lease(DagRunLeaseOutcomeV1.Queued, state, row);
    }

    internal static DagRunLeaseResultV1 Renew(DagRunStoredState? state, DagRunLeaseRequestV1 request,
        DateTimeOffset now)
    {
        ValidateLeaseRequest(request);
        if (state is null) return Lease(DagRunLeaseOutcomeV1.NotFound);
        now = Clamp(state, now);
        var fence = new DagRunFenceV1(request.RunId, request.OwnerId, request.ExpectedFence ?? 0, request.RequestId);
        if (!MatchesLiveFence(state, fence, now, out var row) || row!.Purpose != request.Purpose)
            return Lease(DagRunLeaseOutcomeV1.StaleFence, state);
        var expires = now + request.LeaseDuration;
        row = row with { ExpiresAt = expires < row.HardDeadline ? expires : row.HardDeadline };
        state.Requests[row.RequestId] = row;
        return Lease(DagRunLeaseOutcomeV1.Granted, state, row);
    }

    internal static DagRunLeaseResultV1 Release(DagRunStoredState? state, DagRunFenceV1 fence, DateTimeOffset now)
    {
        ValidateFence(fence);
        if (state is null) return Lease(DagRunLeaseOutcomeV1.NotFound);
        now = Clamp(state, now);
        if (fence.RunId != state.Snapshot.RunId || !state.Requests.TryGetValue(fence.RequestId, out var row) ||
            row.OwnerId != fence.OwnerId || row.GrantedToken != fence.Token)
            return Lease(DagRunLeaseOutcomeV1.StaleFence, state);
        // An old exact receipt may acknowledge release again, but never revoke a newer owner.
        if (state.ActiveRequest == row.RequestId) state.ActiveRequest = null;
        state.Requests[row.RequestId] = row with { State = Consumed, ExpiresAt = null };
        return new DagRunLeaseResultV1(DagRunLeaseOutcomeV1.Released, fence, null, null,
            row.QueueSequence, state.Snapshot.StreamVersion);
    }

    internal static void ValidateFence(DagRunFenceV1 fence)
    {
        ArgumentNullException.ThrowIfNull(fence);
        ValidateRequestCoordinates(fence.RunId, fence.OwnerId, fence.RequestId);
        if (fence.Token <= 0) throw new ArgumentOutOfRangeException(nameof(fence));
    }

    internal static void ValidateRequestCoordinates(Guid run, Guid owner, Guid request)
    {
        if (run == Guid.Empty || owner == Guid.Empty || request == Guid.Empty)
            throw new ArgumentException("DAG request coordinates must be nonempty.");
    }

    internal static void CancelRequest(DagRunStoredState? state, Guid owner, Guid requestId, DateTimeOffset now)
    {
        if (state is null) return;
        Clamp(state, now);
        if (state.Requests.TryGetValue(requestId, out var row) && row.OwnerId == owner && row.State == Waiting)
            state.Requests[requestId] = row with { State = Consumed };
    }

    internal static DagRunSubscribeResultV1 Subscribe(DagRunStoredState? state, DagRunSubscriptionV1 subscription,
        DateTimeOffset now)
    {
        if (subscription.SubscriptionId == Guid.Empty || subscription.RecipientHostId == Guid.Empty ||
            subscription.RunId == Guid.Empty ||
            subscription.LeaseDuration <= TimeSpan.Zero || subscription.LeaseDuration > MaximumSubscription)
            throw new ArgumentException("Invalid DAG subscription identity or duration.", nameof(subscription));
        if (state is null) return new(DagRunSubscribeOutcomeV1.NotFound, null);
        now = Clamp(state, now);
        if (state.Observers.TryGetValue(subscription.SubscriptionId, out var old) &&
            (old.Subscription.RunId != subscription.RunId ||
             old.Subscription.RecipientHostId != subscription.RecipientHostId))
            throw new InvalidDataException("DAG_INTEGRITY_CONFLICT: subscription identity changed.");
        if (state.Snapshot.StreamVersion.Value > subscription.AfterVersion.Value)
            return new(DagRunSubscribeOutcomeV1.AlreadyChanged, state.Snapshot.StreamVersion);
        if (subscription.AfterVersion != state.Snapshot.StreamVersion)
            throw Invalid("subscription is ahead of the run");
        state.Observers[subscription.SubscriptionId] =
            new DagRunObserverRow(subscription, now + subscription.LeaseDuration);
        return new(DagRunSubscribeOutcomeV1.Subscribed, state.Snapshot.StreamVersion);
    }

    internal static DagRunCheckpointV1 Clone(DagRunCheckpointV1 checkpoint) => new(checkpoint.Version,
        checkpoint.RunId, checkpoint.StreamVersion, checkpoint.ContentType, checkpoint.Payload, checkpoint.Sha256);

    internal static OutboxWrite Clone(OutboxWrite record) => record with { Payload = record.Payload.ToArray() };

    internal static DagRunDiscoveryV1 Discovery(DagRunStoredState state) => new(state.CreationSequence,
        state.Snapshot.RunId, state.Snapshot.StreamVersion, state.Snapshot.Graph.Definition, state.Snapshot.State);

    internal static void ValidateDiscovery(DagRunDiscoveryRequestV1 request)
    {
        if (request.AfterCreationSequence < 0 || request.MaxCount is < 1 or > 1000)
            throw new ArgumentOutOfRangeException(nameof(request));
    }

    internal static DagRunSnapshotV1 ReadCheckpoint(DagRunCheckpointV1 checkpoint)
    {
        if (checkpoint.Version != 1 || checkpoint.ContentType != CheckpointContentType ||
            !LowerHash(checkpoint.Sha256) ||
            Hash(checkpoint.Payload) != checkpoint.Sha256) throw Invalid("checkpoint framing/hash");
        DagRunCheckpointFrame frame;
        try
        {
            using var doc = JsonDocument.Parse(checkpoint.Payload);
            RejectDuplicateFields(doc.RootElement);
            frame = JsonSerializer.Deserialize(checkpoint.Payload, DagRunStoreJsonContext.Default.DagRunCheckpointFrame)
                    ?? throw Invalid("null checkpoint");
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException
                                              or NullReferenceException)
        {
            throw new InvalidDataException("DAG_PROTOCOL_INVALID: checkpoint JSON.", exception);
        }

        if (frame.AggregateKind != "dag-run-v1" || frame.Version != 1 || frame.RunId != checkpoint.RunId ||
            frame.StreamVersion != checkpoint.StreamVersion.Value || frame.Snapshot is null ||
            frame.Snapshot.RunId != frame.RunId ||
            frame.Snapshot.StreamVersion.Value != frame.StreamVersion ||
            !JsonSerializer.SerializeToUtf8Bytes(frame, DagRunStoreJsonContext.Default.DagRunCheckpointFrame).AsSpan()
                .SequenceEqual(checkpoint.Payload)) throw Invalid("checkpoint coordinates/canonical bytes");
        ValidateSnapshot(frame.Snapshot);
        return frame.Snapshot;
    }

    private static void ValidateSnapshot(DagRunSnapshotV1 snapshot)
    {
        if (snapshot.RunId == Guid.Empty || snapshot.StreamVersion.Value < 1 || !Enum.IsDefined(snapshot.State) ||
            snapshot.CreatedAt.Offset != TimeSpan.Zero || snapshot.UpdatedAt.Offset != TimeSpan.Zero ||
            snapshot.UpdatedAt < snapshot.CreatedAt ||
            (DagRunStateReducer.Terminal(snapshot.State)
                ? snapshot.CompletedAt != snapshot.UpdatedAt
                : snapshot.CompletedAt is not null))
            throw Invalid("checkpoint run coordinates/state/times");
        _ = new DagRunStartKeyV1(snapshot.StartKey.Value);
        _ = new DagRunGraphV1(snapshot.Graph.Definition, snapshot.Graph.RunInputContractKey, snapshot.Graph.Nodes);
        ValidateValue(snapshot.RunInput);
        if (snapshot.RunInput.ContractKey != snapshot.Graph.RunInputContractKey ||
            snapshot.Nodes.Count != snapshot.Graph.Nodes.Count)
            throw Invalid("checkpoint graph/input inventory");
        for (var ordinal = 0; ordinal < snapshot.Nodes.Count; ordinal++)
        {
            var node = snapshot.Nodes[ordinal];
            var definition = snapshot.Graph.Nodes[ordinal];
            if (node.Ordinal != ordinal || !Enum.IsDefined(node.State) ||
                new[] { node.ReadyAt, node.StartedAt, node.CompletedAt }.Any(t => t is { } time &&
                    (time.Offset != TimeSpan.Zero || time < snapshot.CreatedAt || time > snapshot.UpdatedAt)) ||
                (node.ChildInstanceId is { } child &&
                 child.Value != DagRunStateReducer.ChildId(snapshot.RunId, ordinal)))
                throw Invalid("checkpoint node coordinates/state/times");
            if (node.Input is { } input)
            {
                ValidateValue(input);
                if (input.ContractKey != definition.InputContractKey) throw Invalid("checkpoint node input type");
            }

            if (node.Output is { } output)
            {
                ValidateValue(output);
                if (output.ContractKey != definition.OutputContractKey || node.State != DagNodeStateV1.Succeeded)
                    throw Invalid("checkpoint node output type/state");
            }

            if (node.State is DagNodeStateV1.StartAuthorized or DagNodeStateV1.CancellationRequested
                or DagNodeStateV1.Succeeded)
            {
                if (node.Input is null || node.ChildInstanceId is null || node.StartedAt is null ||
                    node.ReadyAt is null)
                    throw Invalid("checkpoint admitted coordinates");
            }

            if (node.State is DagNodeStateV1.Pending or DagNodeStateV1.Ready or DagNodeStateV1.DependencyBlocked &&
                (node.Input is not null || node.ChildInstanceId is not null || node.StartedAt is not null))
                throw Invalid("checkpoint unadmitted coordinates");
            if (node.State is DagNodeStateV1.Succeeded or DagNodeStateV1.Failed or DagNodeStateV1.Cancelled
                    or DagNodeStateV1.DependencyBlocked
                    ? node.CompletedAt is null
                    : node.CompletedAt is not null)
                throw Invalid("checkpoint node completion time");
            if (node.State == DagNodeStateV1.Ready && node.ReadyAt is null) throw Invalid("checkpoint readiness time");
        }
    }

    private static void ValidateValue(DagRunValueV1 value) =>
        _ = new DagRunValueV1(value.IsPresent, value.ContractKey, value.CodecFormat, value.Bytes, value.Fingerprint);

    internal static byte[] CheckpointBytes(DagRunSnapshotV1 snapshot) => JsonSerializer.SerializeToUtf8Bytes(
        new DagRunCheckpointFrame("dag-run-v1", 1, snapshot.RunId, snapshot.StreamVersion.Value, snapshot),
        DagRunStoreJsonContext.Default.DagRunCheckpointFrame);

    private static void ValidateCheckpoint(DagRunCheckpointV1 checkpoint, DagRunSnapshotV1 expected)
    {
        _ = ReadCheckpoint(checkpoint);
        if (!CheckpointBytes(expected).AsSpan().SequenceEqual(checkpoint.Payload))
            throw Invalid("checkpoint does not equal the reduced records");
    }

    internal static string OperationFingerprint(IReadOnlyList<DagRunRecordV1> records,
        IReadOnlyList<OutboxWrite> outbox)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("records");
            foreach (var record in records)
            {
                using var document = JsonDocument.Parse(DagRunRecordCodecV1.Serialize(record));
                WriteIntent(writer, document.RootElement);
            }

            writer.WriteEndArray();
            writer.WriteStartArray("outbox");
            foreach (var write in outbox.OrderBy(o => o.OutboxRecordId.ToString(), StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("id", write.OutboxRecordId.ToString());
                writer.WriteString("kind", write.Kind);
                writer.WriteBase64String("payload", write.Payload);
                writer.WriteString("owner", write.DagRunOwner?.ToString("D"));
                writer.WriteString("recipient", write.RecipientHostId?.ToString("D"));
                writer.WriteString("subscription", write.DagRunSubscriptionId?.ToString("D"));
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Hash(buffer.ToArray());
    }

    private static void WriteIntent(Utf8JsonWriter writer, JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name is "streamVersion" or "createdAt" or "updatedAt" or "completedAt" or "readyAt"
                    or "startedAt") continue;
                writer.WritePropertyName(property.Name);
                WriteIntent(writer, property.Value);
            }

            writer.WriteEndObject();
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var item in element.EnumerateArray()) WriteIntent(writer, item);
            writer.WriteEndArray();
        }
        else element.WriteTo(writer);
    }

    private static void AddNotifications(DagRunStoredState state, DateTimeOffset now)
    {
        foreach (var observer in state.Observers.Values.Where(o => o.ExpiresAt > now &&
                                                                   o.Subscription.AfterVersion.Value <
                                                                   state.Snapshot.StreamVersion.Value))
            state.NewOutbox.Add(Notice(state.Snapshot, observer.Subscription));
        if (state.Snapshot.State == DagRunStateV1.Running &&
            state.Snapshot.Nodes.Any(n => n.State == DagNodeStateV1.Ready))
            state.NewOutbox.Add(Notice(state.Snapshot, null));
    }

    private static OutboxWrite Notice(DagRunSnapshotV1 snapshot, DagRunSubscriptionV1? subscription)
    {
        var key =
            $"dag-notice-v1\n{snapshot.RunId:D}\n{snapshot.StreamVersion.Value.ToString(CultureInfo.InvariantCulture)}\n{subscription?.SubscriptionId.ToString("D") ?? "progression"}\n";
        var id = Guid.ParseExact(Hash(Encoding.UTF8.GetBytes(key))[..32], "N");
        var payload = Encoding.UTF8.GetBytes(
            $"{{\"runId\":\"{snapshot.RunId:D}\",\"version\":{snapshot.StreamVersion.Value.ToString(CultureInfo.InvariantCulture)},\"subscriptionId\":{(subscription is null ? "null" : "\"" + subscription.SubscriptionId.ToString("D") + "\"")}}}");
        return new OutboxWrite(new OutboxRecordId(id), ReadyKind, payload)
        {
            DagRunOwner = snapshot.RunId, RecipientHostId = subscription?.RecipientHostId,
            DagRunSubscriptionId = subscription?.SubscriptionId
        };
    }

    internal static DateTimeOffset Clamp(DagRunStoredState state, DateTimeOffset sample)
    {
        var now = sample.ToUniversalTime();
        if (now < state.LastOperationalAt) now = state.LastOperationalAt;
        state.LastOperationalAt = now;
        return now;
    }

    internal static DagRunStoredState CopyForWrite(DagRunStoredState source)
    {
        var copy = new DagRunStoredState
        {
            Snapshot = source.Snapshot, Checkpoint = Clone(source.Checkpoint),
            CreationSequence = source.CreationSequence,
            Fence = source.Fence, ActiveRequest = source.ActiveRequest, LastOperationalAt = source.LastOperationalAt,
            IsStuck = source.IsStuck
        };
        copy.NewRecords.AddRange(source.NewRecords);
        foreach (var entry in source.Operations) copy.Operations.Add(entry.Key, entry.Value);
        foreach (var entry in source.Requests) copy.Requests.Add(entry.Key, entry.Value);
        foreach (var entry in source.Observers) copy.Observers.Add(entry.Key, entry.Value);
        return copy;
    }

    private static void Expire(DagRunStoredState state, DateTimeOffset now)
    {
        foreach (var row in state.Requests.Values.ToArray())
            if ((row.State == Waiting && row.WaitExpiresAt <= now) ||
                (row.State == Granted && (row.ExpiresAt <= now || row.HardDeadline <= now)))
            {
                state.Requests[row.RequestId] = row with { State = Consumed, ExpiresAt = null };
                if (state.ActiveRequest == row.RequestId) state.ActiveRequest = null;
            }
    }

    private static bool MatchesLiveFence(DagRunStoredState state, DagRunFenceV1 fence, DateTimeOffset now,
        out DagRunLeaseRow? row)
    {
        row = state.Requests.GetValueOrDefault(fence.RequestId);
        return fence.RunId == state.Snapshot.RunId && state.ActiveRequest == fence.RequestId &&
               state.Fence == fence.Token &&
               row is { State: Granted } && row.OwnerId == fence.OwnerId && row.GrantedToken == fence.Token &&
               row.ExpiresAt > now && row.HardDeadline > now;
    }

    private static DagRunLeaseResultV1 Lease(DagRunLeaseOutcomeV1 outcome, DagRunStoredState? state = null,
        DagRunLeaseRow? row = null) =>
        new(outcome, outcome == DagRunLeaseOutcomeV1.Granted && state is not null && row is not null
                ? new DagRunFenceV1(state.Snapshot.RunId, row.OwnerId, row.GrantedToken!.Value, row.RequestId)
                : null,
            outcome == DagRunLeaseOutcomeV1.Granted ? row?.ExpiresAt : null,
            outcome == DagRunLeaseOutcomeV1.Granted ? row?.HardDeadline : null, row?.QueueSequence,
            state?.Snapshot.StreamVersion);

    private static bool PurposePermits(DagRunLeasePurposeV1 purpose, DagRunRecordKindV1 kind) =>
        kind == DagRunRecordKindV1.Terminal || purpose switch
        {
            DagRunLeasePurposeV1.Admission => kind is DagRunRecordKindV1.InputAdmitted
                or DagRunRecordKindV1.MappingFailed,
            DagRunLeasePurposeV1.ChildJoin => kind == DagRunRecordKindV1.ChildJoined,
            DagRunLeasePurposeV1.RunCancellation => kind == DagRunRecordKindV1.CancellationRequested,
            DagRunLeasePurposeV1.IntegrityReconciliation => kind is DagRunRecordKindV1.IntegrityFaulted
                or DagRunRecordKindV1.PoisonReconciled,
            _ => false
        };

    internal static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static bool LowerHash(string? value) =>
        value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    internal static Result<AppendEventsResult> Failure(string code, string message) =>
        Result<AppendEventsResult>.Failure(new WorkflowConcurrencyException(code + ": " + message));

    private static InvalidDataException Invalid(string message) => new("DAG_PROTOCOL_INVALID: " + message + ".");

    private static void RejectDuplicateFields(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var properties = element.EnumerateObject().ToArray();
            if (properties.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != properties.Length)
                throw Invalid("duplicate checkpoint field");
            foreach (var property in properties) RejectDuplicateFields(property.Value);
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray())
                RejectDuplicateFields(item);
    }
}
