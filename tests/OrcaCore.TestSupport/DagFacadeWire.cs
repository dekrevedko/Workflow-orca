using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using AwesomeAssertions;
using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Providers.Dag;
using OrcaCore.Abstractions.Serialization;

namespace OrcaCore.TestSupport;

// Independent test-side framing shared by provider certification and real process restarts.
public static class DagFacadeWire
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { Converters = { new JsonStringEnumConverter() } };
    private static DagRunLeaseRequestV1 Request(Guid run) => new(run, Guid.NewGuid(), Guid.NewGuid(),
        DagRunLeasePurposeV1.Admission, null, TimeSpan.FromSeconds(5));
    public static async Task FacadeTransitionAsync(IWorkflowEventStore events, Guid id, bool fault, CancellationToken token)
    {
        var request = Request(id) with { Purpose = DagRunLeasePurposeV1.IntegrityReconciliation };
        DagRunLeaseResultV1? granted = null;
        try
        {
            // Providers cap a lease at five seconds and an owner's renewed turn at
            // fifteen seconds. Retry at lease-expiry boundaries, never a 25ms poll;
            // a fourth failed acquire is a test failure, not an unbounded wait.
            for (var attempt = 0; attempt < 4; attempt++)
            {
                granted = await events.AcquireDagRunLeaseAsync(request, token);
                if (granted.Fence is not null) break;
                granted.Outcome.Should().Be(DagRunLeaseOutcomeV1.Queued);
                if (attempt < 3) await Task.Delay(TimeSpan.FromSeconds(5), TimeProvider.System, token);
            }
            granted!.Fence.Should().NotBeNull("the queued test transition must acquire within the provider's maximum turn");
            var before = await FacadeReadAsync(events, id, token);
            var state = fault ? DagRunStateV1.IntegrityFault : before.Nodes.Any(node => node.State is
                DagNodeStateV1.Cancelled or DagNodeStateV1.CancellationRequested) ? DagRunStateV1.CancellationRequested : DagRunStateV1.Running;
            var at = DateTimeOffset.UtcNow > before.UpdatedAt ? DateTimeOffset.UtcNow : before.UpdatedAt;
            var after = before with { StreamVersion = before.StreamVersion.Next(), State = state, UpdatedAt = at, CompletedAt = null };
            var record = new DagRunRecordV1(1, fault ? DagRunRecordKindV1.IntegrityFaulted : DagRunRecordKindV1.PoisonReconciled,
                id, Guid.NewGuid(), after.StreamVersion, state, null, [], at, null);
            var result = await events.AppendAsync(Batch(after, [record], granted.Fence, []), token);
            result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.ToString() : "the injected transition is valid");
        }
        finally
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            if (granted?.Fence is { } fence) await events.ReleaseDagRunLeaseAsync(fence, cleanup.Token);
            else await events.CancelDagRunLeaseRequestAsync(id, request.OwnerId, request.RequestId, cleanup.Token);
        }
    }

    public static async Task<DagRunSnapshotV1> FacadeReadAsync(IWorkflowEventStore events, Guid id, CancellationToken token)
    {
        var checkpoint = (await events.LoadDagRunCheckpointAsync(WorkflowStreamId.ForDagRun(id), token)).Value;
        using var doc = JsonDocument.Parse(checkpoint.Payload);
        return JsonSerializer.Deserialize<DagRunSnapshotV1>(doc.RootElement.GetProperty("snapshot"), Json)!;
    }

    public static DagRunCheckpointV1 CorruptRunInput(DagRunCheckpointV1 checkpoint, string corruption)
    {
        using var doc = JsonDocument.Parse(checkpoint.Payload);
        var snapshot = JsonSerializer.Deserialize<DagRunSnapshotV1>(doc.RootElement.GetProperty("snapshot"), Json)!;
        // Prove this independent serializer exactly reproduces the canonical frame
        // before changing its business value. JsonNode reserialization cannot do that.
        byte[] Frame(DagRunSnapshotV1 value) => JsonSerializer.SerializeToUtf8Bytes(new
        {
            aggregateKind = "dag-run-v1", version = 1, runId = value.RunId,
            streamVersion = value.StreamVersion.Value, snapshot = value
        }, Json);
        Frame(snapshot).Should().Equal(checkpoint.Payload, "the corruption must pass the existing canonical framing check");
        var input = snapshot.RunInput;
        var malformed = "{"u8.ToArray();
        input = corruption switch
        {
            "key" => new(true, "wrong-recorded-key", input.CodecFormat, input.Bytes, input.Fingerprint),
            "json" => new(true, input.ContractKey, input.CodecFormat, malformed, Convert.ToHexString(SHA256.HashData(malformed))),
            _ => throw new ArgumentOutOfRangeException(nameof(corruption))
        };
        var bytes = Frame(snapshot with { RunInput = input });
        return checkpoint with { Payload = bytes, Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)) };
    }

    private static ProviderCommitBatch Batch(DagRunSnapshotV1 after, IReadOnlyList<DagRunRecordV1> records,
        DagRunFenceV1? fence, IReadOnlyList<OutboxWrite> outbox)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            new
            {
                aggregateKind = "dag-run-v1", version = 1, runId = after.RunId,
                streamVersion = after.StreamVersion.Value, snapshot = after
            }, Json);
        var checkpoint = new DagRunCheckpointV1(1, after.RunId, after.StreamVersion,
            "application/vnd.orcacore.dag-run+json;v=1", bytes, Convert.ToHexStringLower(SHA256.HashData(bytes)));
        return new()
        {
            StreamId = WorkflowStreamId.ForDagRun(after.RunId),
            ExpectedVersion = new(records[0].StreamVersion.Value - 1), OutboxRecords = outbox,
            DagRun = new(records[0].OperationId, Fingerprint(records, outbox), fence, records, checkpoint)
        };
    }

    private static string Fingerprint(IReadOnlyList<DagRunRecordV1> records, IReadOnlyList<OutboxWrite> outbox)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("records");
            foreach (var r in records)
            {
                using var doc = JsonDocument.Parse(DagRunRecordCodecV1.Serialize(r));
                Intent(writer, doc.RootElement);
            }

            writer.WriteEndArray();
            writer.WriteStartArray("outbox");
            foreach (var w in outbox.OrderBy(w => w.OutboxRecordId.ToString(), StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("id", w.OutboxRecordId.ToString());
                writer.WriteString("kind", w.Kind);
                writer.WriteBase64String("payload", w.Payload);
                writer.WriteString("owner", w.DagRunOwner?.ToString("D"));
                writer.WriteString("recipient", w.RecipientHostId?.ToString("D"));
                writer.WriteString("subscription", w.DagRunSubscriptionId?.ToString("D"));
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Convert.ToHexStringLower(SHA256.HashData(buffer.ToArray()));
    }

    private static void Intent(Utf8JsonWriter writer, JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var p in element.EnumerateObject())
            {
                if (p.Name is "streamVersion" or "createdAt" or "updatedAt" or "completedAt" or "readyAt"
                    or "startedAt") continue;
                writer.WritePropertyName(p.Name);
                Intent(writer, p.Value);
            }

            writer.WriteEndObject();
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var e in element.EnumerateArray()) Intent(writer, e);
            writer.WriteEndArray();
        }
        else element.WriteTo(writer);
    }
}
