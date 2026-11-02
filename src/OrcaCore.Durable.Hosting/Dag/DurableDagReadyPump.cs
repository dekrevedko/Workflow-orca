using System.Text.Json;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Dag;

namespace OrcaCore.Durable.Hosting.Dag;

internal sealed record DurableDagReadyNoticeV1(Guid RunId, StreamVersion Version, Guid? SubscriptionId);

internal interface IDurableDagReadySinkV1
{
    ValueTask HandleAsync(DurableDagReadyNoticeV1 notice, CancellationToken cancellationToken);
}

internal sealed class DurableDagReadyPump(IWorkflowEventStore store, IWorkflowOutboxStore outbox,
    DurableDagHostIdentity host, IDurableDagReadySinkV1? sink)
{
    internal async Task<int> PumpOnceAsync(OutboxClaimRequest request, CancellationToken token)
    {
        // No capable DAG role means no claim, not claim/release churn. Task 2.3
        // installs the real coalescing scheduler/hub; there is deliberately no no-op sink.
        if (sink is null) return 0;
        var writes = await outbox.ClaimAsync(request with
        {
            KindSelector = OutboxKindSelector.Including(DurableDagKinds.Ready), RecipientHostId = host.HostId
        }, token).ConfigureAwait(false);
        var count = 0;
        foreach (var write in writes)
        {
            if (write.Kind != DurableDagKinds.Ready ||
                (write.RecipientHostId is { } recipient && recipient != host.HostId))
            {
                await outbox.ReleaseAsync(write.OutboxRecordId, token).ConfigureAwait(false);
                continue;
            }
            DurableDagReadyNoticeV1 notice;
            try
            {
                notice = Read(write);
                var checkpoint = await store.LoadDagRunCheckpointAsync(WorkflowStreamId.ForDagRun(notice.RunId), token).ConfigureAwait(false);
                if (!checkpoint.HasValue || DurableDagWire.Read(checkpoint.Value).StreamVersion.Value < notice.Version.Value)
                    throw new InvalidDataException("DAG ready hint precedes authoritative run state.");
            }
            catch (Exception exception) when (exception is InvalidDataException or JsonException or FormatException)
            {
                await outbox.MarkPoisonedAsync(write.OutboxRecordId, "DAG_READY_HINT_INVALID", exception.Message, token).ConfigureAwait(false);
                continue;
            }
            catch
            {
                await outbox.ReleaseAsync(write.OutboxRecordId, CancellationToken.None).ConfigureAwait(false);
                throw;
            }
            try
            {
                // Sink failure is infrastructure failure, not malformed transport data.
                // Successful acceptance means a bounded coalesced wake, never mapper completion.
                await sink.HandleAsync(notice, token).ConfigureAwait(false);
                await outbox.MarkAsync(write.OutboxRecordId, OutboxRecordState.Dispatched, token).ConfigureAwait(false);
                count++;
            }
            catch
            {
                await outbox.ReleaseAsync(write.OutboxRecordId, CancellationToken.None).ConfigureAwait(false);
                throw;
            }
        }
        return count;
    }

    private static DurableDagReadyNoticeV1 Read(OutboxWrite write)
    {
        try { return ReadFields(write); }
        catch (InvalidOperationException exception) { throw new InvalidDataException("DAG ready hint field types.", exception); }
    }

    private static DurableDagReadyNoticeV1 ReadFields(OutboxWrite write)
    {
        using var document = JsonDocument.Parse(write.Payload);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).SequenceEqual(
                new[] { "runId", "subscriptionId", "version" }))
            throw new InvalidDataException("DAG ready hint field set.");
        var run = root.GetProperty("runId").GetGuid();
        var version = root.GetProperty("version").GetInt64();
        var subscriptionElement = root.GetProperty("subscriptionId");
        Guid? subscription = subscriptionElement.ValueKind == JsonValueKind.Null ? null : subscriptionElement.GetGuid();
        if (run == Guid.Empty || run != write.DagRunOwner || version <= 0 || subscription == Guid.Empty ||
            subscription != write.DagRunSubscriptionId || (subscription is null) != (write.RecipientHostId is null))
            throw new InvalidDataException("DAG ready hint trusted coordinates.");
        return new(run, new(version), subscription);
    }
}
