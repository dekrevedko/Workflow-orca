using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Providers.Dag;
using OrcaCore.Abstractions.Serialization;

namespace OrcaCore.Engine.Durable.Dag;

internal static class DurableDagKinds
{
    internal const string Start = "dag-child-start-v1";
    internal const string Join = "dag-child-join-v1";
    internal const string Cancel = "dag-child-cancel-v1";
    internal const string Ready = "dag-run-ready-v1";
    internal static bool IsChild(string kind) => kind is Start or Join or Cancel;
}

internal sealed record DurableDagChildSignal(int Version, Guid RunId, int Ordinal);
internal sealed record DurableDagCheckpointFrame(string AggregateKind, int Version, Guid RunId,
    long StreamVersion, DagRunSnapshotV1 Snapshot);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(DurableDagChildSignal))]
[JsonSerializable(typeof(DurableDagCheckpointFrame))]
internal sealed partial class DurableDagJsonContext : JsonSerializerContext;

// Engine-side framing of the approved provider protocol, not a business-value codec.
// Provider certification independently reconstructs these bytes and validates the commit.
internal static class DurableDagWire
{
    internal const string CheckpointContentType = "application/vnd.orcacore.dag-run+json;v=1";

    internal static DagRunSnapshotV1 Read(DagRunCheckpointV1 checkpoint)
    {
        var bytes = checkpoint.Payload;
        if (checkpoint.Version != 1 || checkpoint.ContentType != CheckpointContentType ||
            checkpoint.Sha256 != Hash(bytes)) throw new InvalidDataException("DAG checkpoint framing/checksum.");
        var frame = JsonSerializer.Deserialize(bytes, DurableDagJsonContext.Default.DurableDagCheckpointFrame)
            ?? throw new InvalidDataException("DAG checkpoint is null.");
        if (frame.AggregateKind != "dag-run-v1" || frame.Version != 1 || frame.Snapshot is null || frame.RunId != checkpoint.RunId ||
            frame.StreamVersion != checkpoint.StreamVersion.Value || frame.Snapshot.RunId != frame.RunId ||
            frame.Snapshot.StreamVersion != checkpoint.StreamVersion ||
            !JsonSerializer.SerializeToUtf8Bytes(frame, DurableDagJsonContext.Default.DurableDagCheckpointFrame).AsSpan().SequenceEqual(bytes))
            throw new InvalidDataException("DAG checkpoint coordinates or noncanonical bytes.");
        return frame.Snapshot;
    }

    internal static DurableDagChildSignal ReadSignal(OutboxWrite write)
    {
        var signal = JsonSerializer.Deserialize(write.Payload, DurableDagJsonContext.Default.DurableDagChildSignal)
            ?? throw new InvalidDataException("DAG child signal is null.");
        if (signal.Version != 1 || signal.RunId == Guid.Empty || signal.RunId != write.DagRunOwner ||
            signal.Ordinal < 0 || write.RecipientHostId is not null || write.DagRunSubscriptionId is not null ||
            !JsonSerializer.SerializeToUtf8Bytes(signal, DurableDagJsonContext.Default.DurableDagChildSignal).AsSpan().SequenceEqual(write.Payload))
            throw new InvalidDataException("DAG child signal coordinates or noncanonical bytes.");
        return signal;
    }

    internal static OutboxWrite ChildWrite(string kind, Guid runId, int ordinal) =>
        new(OutboxRecordId.New(), kind, JsonSerializer.SerializeToUtf8Bytes(
            new DurableDagChildSignal(1, runId, ordinal), DurableDagJsonContext.Default.DurableDagChildSignal))
        { DagRunOwner = runId };

    internal static string ContractKey(Type type) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(type.AssemblyQualifiedName ?? type.FullName ?? type.Name)));

    internal static Guid ChildId(Guid runId, int ordinal)
    {
        var recipe = $"orcacore-dag-child-v1\n{runId:D}\n{ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture)}\n";
        var hex = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(recipe)))[..32];
        if (hex.All(c => c == '0')) hex = hex[..31] + "1";
        return Guid.ParseExact(hex, "N");
    }

    internal static ProviderCommitBatch Commit(DagRunSnapshotV1 before, IReadOnlyList<DagRunRecordV1> records,
        DagRunFenceV1 fence, IReadOnlyList<OutboxWrite>? outbox = null)
    {
        var after = before;
        // This is a write projection, not a second authority/reducer. The provider
        // independently validates every transition and compares this checkpoint to
        // its own reduction before it commits anything.
        foreach (var record in records)
        {
            var nodes = after.Nodes.ToArray();
            foreach (var change in record.ChangedNodes)
            {
                var old = nodes[change.Ordinal];
                nodes[change.Ordinal] = new(change.Ordinal, change.State, change.ChildInstanceId,
                    change.InputWrite ?? old.Input, change.OutputWrite ?? old.Output, change.FailureCode,
                    change.ReadyAt, change.StartedAt, change.CompletedAt);
            }
            after = after with { Nodes = nodes, StreamVersion = record.StreamVersion, State = record.State,
                UpdatedAt = record.UpdatedAt, CompletedAt = record.CompletedAt };
        }
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new DurableDagCheckpointFrame("dag-run-v1", 1,
            after.RunId, after.StreamVersion.Value, after), DurableDagJsonContext.Default.DurableDagCheckpointFrame);
        outbox ??= [];
        return new ProviderCommitBatch
        {
            StreamId = WorkflowStreamId.ForDagRun(before.RunId), ExpectedVersion = before.StreamVersion,
            OutboxRecords = outbox,
            DagRun = new(records[0].OperationId, OperationFingerprint(records, outbox), fence, records,
                new(1, after.RunId, after.StreamVersion, CheckpointContentType, bytes, Hash(bytes)))
        };
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static string OperationFingerprint(IReadOnlyList<DagRunRecordV1> records, IReadOnlyList<OutboxWrite> outbox)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("records");
            foreach (var record in records)
            {
                using var document = JsonDocument.Parse(DagRunRecordCodecV1.Serialize(record));
                Intent(writer, document.RootElement);
            }
            writer.WriteEndArray();
            writer.WriteStartArray("outbox");
            foreach (var write in outbox.OrderBy(w => w.OutboxRecordId.ToString(), StringComparer.Ordinal))
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

    private static void Intent(Utf8JsonWriter writer, JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name is "streamVersion" or "createdAt" or "updatedAt" or "completedAt" or "readyAt" or "startedAt") continue;
                writer.WritePropertyName(property.Name);
                Intent(writer, property.Value);
            }
            writer.WriteEndObject();
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var value in element.EnumerateArray()) Intent(writer, value);
            writer.WriteEndArray();
        }
        else element.WriteTo(writer);
    }
}
