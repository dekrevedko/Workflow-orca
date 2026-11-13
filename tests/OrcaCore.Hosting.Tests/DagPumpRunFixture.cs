using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AwesomeAssertions;
using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Providers.Dag;
using OrcaCore.Abstractions.Serialization;

namespace OrcaCore.Hosting.Tests;

// Independent protocol writer: no engine reducer, framing helper or friend probe.
internal static class DagPumpRunFixture
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
        { Converters = { new JsonStringEnumConverter() } };

    internal static async Task<(DagRunSnapshotV1 Run, OutboxWrite Start)> CreateAsync(IWorkflowEventStore store,
        DurableWorkflowDefinition<int, int> workflow, bool cancelled, CancellationToken token, bool branches = false)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(typeof(int).AssemblyQualifiedName!)));
        var binding = new DagRunBindingV1(workflow.DefinitionId, workflow.DefinitionVersion, workflow.DefinitionFingerprint.Value);
        var value = new DagRunValueV1(true, key, "orcacore-json-v1", "41"u8.ToArray(), Convert.ToHexString(SHA256.HashData("41"u8)));
        var runId = Guid.NewGuid();
        var time = DateTimeOffset.UtcNow;
        var run = new DagRunSnapshotV1(runId, new(1), new("dag-pump-" + runId.ToString("N")),
            new(binding, key, [new(0, "node", binding, key, key, [])]), value, DagRunStateV1.Running,
            [new(0, DagNodeStateV1.Ready, null, null, null, null, time, null, null)], time, time, null);
        if (branches)
            run = run with
            {
                Graph = run.Graph with { Nodes = [run.Graph.Nodes[0], new(1, "dependent", binding, key, key, [0]), new(2, "independent", binding, key, key, [])] },
                Nodes = [run.Nodes[0], new(1, DagNodeStateV1.Pending, null, null, null, null, null, null, null),
                    new(2, DagNodeStateV1.Ready, null, null, null, null, time, null, null)]
            };
        var created = new DagRunRecordV1(1, DagRunRecordKindV1.Created, runId, Guid.NewGuid(), new(1),
            run.State, run, [], time, null);
        (await store.AppendAsync(Batch(run, [created], null, []), token)).IsSuccess.Should().BeTrue();
        var fence = (await store.AcquireDagRunLeaseAsync(new(runId, Guid.NewGuid(), Guid.NewGuid(),
            DagRunLeasePurposeV1.Admission, null, TimeSpan.FromSeconds(5)), token)).Fence!;
        var child = Guid.ParseExact(Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes($"orcacore-dag-child-v1\n{runId:D}\n0\n")))[..32], "N");
        var node = run.Nodes[0] with { State = DagNodeStateV1.StartAuthorized, ChildInstanceId = InstanceId.Parse(child.ToString("D")), Input = value, StartedAt = time };
        run = run with { StreamVersion = new(2), Nodes = new[] { node }.Concat(run.Nodes.Skip(1)).ToArray() };
        var start = ChildWrite("dag-child-start-v1", runId);
        var admitted = new DagRunRecordV1(1, DagRunRecordKindV1.InputAdmitted, runId, Guid.NewGuid(), new(2),
            run.State, null, [new(0, node.State, node.ChildInstanceId, value, null, null, time, time, null)], time, null);
        (await store.AppendAsync(Batch(run, [admitted], fence, [start]), token)).IsSuccess.Should().BeTrue();
        await store.ReleaseDagRunLeaseAsync(fence, token);
        if (cancelled)
        {
            fence = (await store.AcquireDagRunLeaseAsync(new(runId, Guid.NewGuid(), Guid.NewGuid(),
                DagRunLeasePurposeV1.RunCancellation, null, TimeSpan.FromSeconds(5)), token)).Fence!;
            node = node with { State = DagNodeStateV1.CancellationRequested };
            run = run with { StreamVersion = new(3), State = DagRunStateV1.CancellationRequested, Nodes = [node] };
            var cancellation = new DagRunRecordV1(1, DagRunRecordKindV1.CancellationRequested, runId, Guid.NewGuid(), new(3),
                run.State, null, [new(0, node.State, node.ChildInstanceId, null, null, null, time, time, null)], time, null);
            (await store.AppendAsync(Batch(run, [cancellation], fence, [ChildWrite("dag-child-cancel-v1", runId)]), token)).IsSuccess.Should().BeTrue();
            await store.ReleaseDagRunLeaseAsync(fence, token);
        }
        return (run, start);
    }

    internal static OutboxWrite ChildWrite(string kind, Guid runId) => new(OutboxRecordId.New(), kind,
        Encoding.UTF8.GetBytes($"{{\"version\":1,\"runId\":\"{runId:D}\",\"ordinal\":0}}")) { DagRunOwner = runId };

    internal static async Task<DagRunSnapshotV1> ReadAsync(IWorkflowEventStore store, Guid run, CancellationToken token)
    {
        var checkpoint = (await store.LoadDagRunCheckpointAsync(WorkflowStreamId.ForDagRun(run), token)).Value;
        using var document = JsonDocument.Parse(checkpoint.Payload);
        return JsonSerializer.Deserialize<DagRunSnapshotV1>(document.RootElement.GetProperty("snapshot"), Json)!;
    }

    private static ProviderCommitBatch Batch(DagRunSnapshotV1 after, IReadOnlyList<DagRunRecordV1> records,
        DagRunFenceV1? fence, IReadOnlyList<OutboxWrite> outbox)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { aggregateKind = "dag-run-v1", version = 1,
            runId = after.RunId, streamVersion = after.StreamVersion.Value, snapshot = after }, Json);
        return new()
        {
            StreamId = WorkflowStreamId.ForDagRun(after.RunId), ExpectedVersion = new(records[0].StreamVersion.Value - 1),
            OutboxRecords = outbox, DagRun = new(records[0].OperationId, Fingerprint(records, outbox), fence, records,
                new(1, after.RunId, after.StreamVersion, "application/vnd.orcacore.dag-run+json;v=1", bytes,
                    Convert.ToHexStringLower(SHA256.HashData(bytes))))
        };
    }

    private static string Fingerprint(IReadOnlyList<DagRunRecordV1> records, IReadOnlyList<OutboxWrite> outbox)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject(); writer.WriteStartArray("records");
            foreach (var record in records)
            {
                using var document = JsonDocument.Parse(DagRunRecordCodecV1.Serialize(record));
                Intent(writer, document.RootElement);
            }
            writer.WriteEndArray(); writer.WriteStartArray("outbox");
            foreach (var write in outbox.OrderBy(w => w.OutboxRecordId.ToString(), StringComparer.Ordinal))
            {
                writer.WriteStartObject(); writer.WriteString("id", write.OutboxRecordId.ToString());
                writer.WriteString("kind", write.Kind); writer.WriteBase64String("payload", write.Payload);
                writer.WriteString("owner", write.DagRunOwner?.ToString("D"));
                writer.WriteString("recipient", write.RecipientHostId?.ToString("D"));
                writer.WriteString("subscription", write.DagRunSubscriptionId?.ToString("D")); writer.WriteEndObject();
            }
            writer.WriteEndArray(); writer.WriteEndObject();
        }
        return Convert.ToHexStringLower(SHA256.HashData(buffer.ToArray()));
    }

    private static void Intent(Utf8JsonWriter writer, JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name is "streamVersion" or "createdAt" or "updatedAt" or "completedAt" or "readyAt" or "startedAt") continue;
                writer.WritePropertyName(property.Name); Intent(writer, property.Value);
            }
            writer.WriteEndObject();
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray(); foreach (var value in element.EnumerateArray()) Intent(writer, value); writer.WriteEndArray();
        }
        else element.WriteTo(writer);
    }
}
