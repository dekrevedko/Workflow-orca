using System.Globalization;
using System.Text.Json;
using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Abstractions.Serialization;

/// <summary>Encodes the separate closed DAG protocol, never workflow events or business values.</summary>
public static class DagRunRecordCodecV1
{
    private static readonly string[] Kinds =
    [
        "dag-run-created-v1", "dag-node-input-admitted-v1", "dag-node-mapping-failed-v1",
        "dag-child-terminal-joined-v1", "dag-run-cancellation-requested-v1", "dag-run-terminal-v1",
        "dag-run-integrity-faulted-v1", "dag-run-poison-reconciled-v1"
    ];

    public static byte[] Serialize(DagRunRecordV1 record)
    {
        ArgumentNullException.ThrowIfNull(record);
        Validate(record);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer)) WriteRecord(writer, record);
        return buffer.ToArray();
    }

    public static DagRunRecordV1 Deserialize(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        try
        {
            using var document = JsonDocument.Parse(payload);
            var record = ReadRecord(document.RootElement);
            // The wire contract fixes order, scalar spelling, UTF-8, base64 and timestamps.
            // Comparing the canonical projection also rejects whitespace/BOM/coercion aliases.
            if (!Serialize(record).AsSpan().SequenceEqual(payload))
                throw Invalid("noncanonical record encoding");
            return record;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or
                                              InvalidOperationException or FormatException or OverflowException
                                              or KeyNotFoundException)
        {
            throw new InvalidDataException("DAG_PROTOCOL_INVALID: malformed record.", exception);
        }
    }

    private static void Validate(DagRunRecordV1 record)
    {
        if (record.Version != 1 || record.RunId == Guid.Empty || record.OperationId == Guid.Empty ||
            record.StreamVersion.Value <= 0 || !Enum.IsDefined(record.Kind) || !Enum.IsDefined(record.State))
            throw Invalid("record identity, version, kind or state");
        Utc(record.UpdatedAt);
        if (record.CompletedAt is { } completed) Utc(completed);
        if (record.Kind == DagRunRecordKindV1.Created)
        {
            var snapshot = record.CreationSnapshot ?? throw Invalid("missing creation snapshot");
            if (record.StreamVersion.Value != 1 || record.ChangedNodes.Count != 0 ||
                snapshot.RunId != record.RunId || snapshot.StreamVersion != record.StreamVersion ||
                snapshot.State != DagRunStateV1.Running || record.State != snapshot.State ||
                snapshot.CreatedAt != record.UpdatedAt || snapshot.UpdatedAt != record.UpdatedAt ||
                snapshot.CompletedAt is not null || record.CompletedAt is not null)
                throw Invalid("inconsistent creation snapshot");
            _ = DagRunValidation.GraphNodes(snapshot.Graph.Definition, snapshot.Graph.RunInputContractKey,
                snapshot.Graph.Nodes);
            _ = DagRunValidation.StartKey(snapshot.StartKey.Value);
            if (snapshot.RunInput.ContractKey != snapshot.Graph.RunInputContractKey ||
                snapshot.Nodes.Count != snapshot.Graph.Nodes.Count)
                throw Invalid("snapshot input binding or node inventory");
            Value(snapshot.RunInput);
            for (var i = 0; i < snapshot.Nodes.Count; i++)
            {
                var node = snapshot.Nodes[i];
                var expected = snapshot.Graph.Nodes[i].DirectDependencies.Count == 0
                    ? DagNodeStateV1.Ready
                    : DagNodeStateV1.Pending;
                if (node.Ordinal != i || node.State != expected || node.ChildInstanceId is not null ||
                    node.Input is not null || node.Output is not null || node.FailureCode is not null ||
                    node.StartedAt is not null || node.CompletedAt is not null ||
                    (expected == DagNodeStateV1.Ready ? node.ReadyAt != snapshot.CreatedAt : node.ReadyAt is not null))
                    throw Invalid("creation node state");
            }

            return;
        }

        if (record.CreationSnapshot is not null) throw Invalid("non-created snapshot");
        if (record.Kind != DagRunRecordKindV1.Terminal && record.CompletedAt is not null)
            throw Invalid("nonterminal completion time");
        var previous = -1;
        foreach (var node in record.ChangedNodes)
        {
            if (node.FailureCode is { } code) DagRunValidation.StorageText(code);
            if (node.Ordinal <= previous || !Enum.IsDefined(node.State)) throw Invalid("changed-node order or state");
            previous = node.Ordinal;
            if (node.InputWrite is not null) Value(node.InputWrite);
            if (node.OutputWrite is not null) Value(node.OutputWrite);
            if (node.ReadyAt is { } ready) Utc(ready);
            if (node.StartedAt is { } started) Utc(started);
            if (node.CompletedAt is { } finished) Utc(finished);
            var permitted = record.Kind switch
            {
                DagRunRecordKindV1.InputAdmitted => node.State == DagNodeStateV1.StartAuthorized &&
                                                    node.ChildInstanceId is not null && node.InputWrite is not null &&
                                                    node.StartedAt is not null,
                DagRunRecordKindV1.MappingFailed => node.State is DagNodeStateV1.Failed
                    or DagNodeStateV1.DependencyBlocked,
                DagRunRecordKindV1.ChildJoined => node.State is DagNodeStateV1.Succeeded or DagNodeStateV1.Failed or
                    DagNodeStateV1.Cancelled or DagNodeStateV1.Ready or DagNodeStateV1.DependencyBlocked,
                DagRunRecordKindV1.CancellationRequested => node.State is DagNodeStateV1.Cancelled
                    or DagNodeStateV1.CancellationRequested,
                DagRunRecordKindV1.PoisonReconciled => node.InputWrite is null && node.OutputWrite is null,
                _ => false
            };
            if (!permitted || (node.InputWrite is not null && record.Kind != DagRunRecordKindV1.InputAdmitted) ||
                (node.OutputWrite is not null && (record.Kind != DagRunRecordKindV1.ChildJoined ||
                                                  node.State != DagNodeStateV1.Succeeded)))
                throw Invalid("record kind and node transition disagree");
        }

        if (record.Kind == DagRunRecordKindV1.InputAdmitted && record.ChangedNodes.Count != 1)
            throw Invalid("input admission must change exactly one node");
        if (record.Kind == DagRunRecordKindV1.MappingFailed &&
            (record.ChangedNodes.Count(node => node.State == DagNodeStateV1.Failed) != 1 ||
             record.ChangedNodes.Single(node => node.State == DagNodeStateV1.Failed).FailureCode !=
             "DAG_INPUT_MAPPING_INVALID"))
            throw Invalid("mapping failure must name one invalid mapper");
        if (record.Kind == DagRunRecordKindV1.Terminal &&
            (record.State is not (DagRunStateV1.Succeeded or DagRunStateV1.Failed or DagRunStateV1.Cancelled) ||
             record.CompletedAt is null || record.ChangedNodes.Count != 0))
            throw Invalid("terminal record");
        if (record.Kind == DagRunRecordKindV1.IntegrityFaulted &&
            (record.State != DagRunStateV1.IntegrityFault || record.CompletedAt is not null ||
             record.ChangedNodes.Count != 0))
            throw Invalid("integrity-fault record");
    }

    private static void Value(DagRunValueV1 value) =>
        _ = DagRunValidation.ValueBytes(value.IsPresent, value.ContractKey, value.CodecFormat, value.Bytes,
            value.Fingerprint);

    private static void Utc(DateTimeOffset time)
    {
        if (time.Offset != TimeSpan.Zero) throw Invalid("timestamp must be UTC");
    }

    private static InvalidDataException Invalid(string reason) => new("DAG_PROTOCOL_INVALID: " + reason + ".");

    private static void WriteRecord(Utf8JsonWriter w, DagRunRecordV1 r)
    {
        w.WriteStartObject();
        w.WriteString("aggregateKind", "dag-run-v1");
        w.WriteNumber("version", r.Version);
        w.WriteString("kind", Kinds[(int)r.Kind]);
        w.WriteString("runId", r.RunId.ToString("D"));
        w.WriteString("operationId", r.OperationId.ToString("D"));
        w.WriteNumber("streamVersion", r.StreamVersion.Value);
        w.WriteString("state", r.State.ToString());
        w.WritePropertyName("creationSnapshot");
        if (r.CreationSnapshot is { } s) WriteSnapshot(w, s);
        else w.WriteNullValue();
        w.WriteStartArray("changedNodes");
        foreach (var node in r.ChangedNodes) WriteChange(w, node);
        w.WriteEndArray();
        WriteTime(w, "updatedAt", r.UpdatedAt);
        WriteTime(w, "completedAt", r.CompletedAt);
        w.WriteEndObject();
    }

    private static void WriteSnapshot(Utf8JsonWriter w, DagRunSnapshotV1 s)
    {
        w.WriteStartObject();
        w.WriteString("runId", s.RunId.ToString("D"));
        w.WriteNumber("streamVersion", s.StreamVersion.Value);
        w.WriteStartObject("startKey");
        w.WriteString("value", s.StartKey.Value);
        w.WriteEndObject();
        w.WritePropertyName("graph");
        WriteGraph(w, s.Graph);
        w.WritePropertyName("runInput");
        WriteValue(w, s.RunInput);
        w.WriteString("state", s.State.ToString());
        w.WriteStartArray("nodes");
        foreach (var n in s.Nodes)
        {
            w.WriteStartObject();
            w.WriteNumber("ordinal", n.Ordinal);
            w.WriteString("state", n.State.ToString());
            WriteChild(w, n.ChildInstanceId);
            w.WritePropertyName("input");
            WriteValue(w, n.Input);
            w.WritePropertyName("output");
            WriteValue(w, n.Output);
            w.WriteString("failureCode", n.FailureCode);
            WriteTime(w, "readyAt", n.ReadyAt);
            WriteTime(w, "startedAt", n.StartedAt);
            WriteTime(w, "completedAt", n.CompletedAt);
            w.WriteEndObject();
        }

        w.WriteEndArray();
        WriteTime(w, "createdAt", s.CreatedAt);
        WriteTime(w, "updatedAt", s.UpdatedAt);
        WriteTime(w, "completedAt", s.CompletedAt);
        w.WriteEndObject();
    }

    private static void WriteGraph(Utf8JsonWriter w, DagRunGraphV1 g)
    {
        w.WriteStartObject();
        w.WritePropertyName("definition");
        WriteBinding(w, g.Definition);
        w.WriteString("runInputContractKey", g.RunInputContractKey);
        w.WriteStartArray("nodes");
        foreach (var n in g.Nodes)
        {
            w.WriteStartObject();
            w.WriteNumber("ordinal", n.Ordinal);
            w.WriteString("nodeId", n.NodeId);
            w.WritePropertyName("workflow");
            WriteBinding(w, n.Workflow);
            w.WriteString("inputContractKey", n.InputContractKey);
            w.WriteString("outputContractKey", n.OutputContractKey);
            w.WriteStartArray("directDependencies");
            foreach (var dependency in n.DirectDependencies) w.WriteNumberValue(dependency);
            w.WriteEndArray();
            w.WriteEndObject();
        }

        w.WriteEndArray();
        w.WriteEndObject();
    }

    private static void WriteBinding(Utf8JsonWriter w, DagRunBindingV1 b)
    {
        w.WriteStartObject();
        w.WriteString("definitionId", b.DefinitionId.ToString());
        w.WriteNumber("definitionVersion", b.DefinitionVersion.Value);
        w.WriteString("fingerprint", DagRunValidation.Fingerprint(b.Fingerprint));
        w.WriteEndObject();
    }

    private static void WriteValue(Utf8JsonWriter w, DagRunValueV1? v)
    {
        if (v is null)
        {
            w.WriteNullValue();
            return;
        }

        Value(v);
        w.WriteStartObject();
        w.WriteBoolean("isPresent", v.IsPresent);
        w.WriteString("contractKey", v.ContractKey);
        w.WriteString("codecFormat", v.CodecFormat);
        w.WriteBase64String("bytes", v.Bytes);
        w.WriteString("fingerprint", v.Fingerprint);
        w.WriteEndObject();
    }

    private static void WriteChange(Utf8JsonWriter w, DagRunNodeChangeV1 n)
    {
        w.WriteStartObject();
        w.WriteNumber("ordinal", n.Ordinal);
        w.WriteString("state", n.State.ToString());
        WriteChild(w, n.ChildInstanceId);
        w.WritePropertyName("inputWrite");
        WriteValue(w, n.InputWrite);
        w.WritePropertyName("outputWrite");
        WriteValue(w, n.OutputWrite);
        w.WriteString("failureCode", n.FailureCode);
        WriteTime(w, "readyAt", n.ReadyAt);
        WriteTime(w, "startedAt", n.StartedAt);
        WriteTime(w, "completedAt", n.CompletedAt);
        w.WriteEndObject();
    }

    private static void WriteChild(Utf8JsonWriter w, InstanceId? id) =>
        w.WriteString("childInstanceId", id?.Value.ToString("D"));

    private static void WriteTime(Utf8JsonWriter w, string key, DateTimeOffset? time)
    {
        if (time is { } value)
        {
            Utc(value);
            w.WriteString(key, value.ToString("O", CultureInfo.InvariantCulture));
        }
        else w.WriteNull(key);
    }

    private static DagRunRecordV1 ReadRecord(JsonElement e)
    {
        Object(e, "aggregateKind", "version", "kind", "runId", "operationId", "streamVersion", "state",
            "creationSnapshot", "changedNodes", "updatedAt", "completedAt");
        if (Text(e, "aggregateKind") != "dag-run-v1") throw Invalid("aggregate kind");
        var kind = Array.IndexOf(Kinds, Text(e, "kind"));
        if (kind < 0) throw Invalid("unknown record kind");
        return new DagRunRecordV1(e.GetProperty("version").GetInt32(), (DagRunRecordKindV1)kind,
            Id(e, "runId"), Id(e, "operationId"), new StreamVersion(e.GetProperty("streamVersion").GetInt64()),
            EnumValue<DagRunStateV1>(e, "state"),
            e.GetProperty("creationSnapshot").ValueKind == JsonValueKind.Null
                ? null
                : ReadSnapshot(e.GetProperty("creationSnapshot")),
            e.GetProperty("changedNodes").EnumerateArray().Select(ReadChange).ToArray(),
            Time(e, "updatedAt") ?? throw Invalid("updatedAt is required"), Time(e, "completedAt"));
    }

    private static DagRunSnapshotV1 ReadSnapshot(JsonElement e)
    {
        Object(e, "runId", "streamVersion", "startKey", "graph", "runInput", "state", "nodes", "createdAt", "updatedAt",
            "completedAt");
        var start = e.GetProperty("startKey");
        Object(start, "value");
        return new DagRunSnapshotV1(Id(e, "runId"), new StreamVersion(e.GetProperty("streamVersion").GetInt64()),
            new DagRunStartKeyV1(Text(start, "value")), ReadGraph(e.GetProperty("graph")),
            ReadValue(e.GetProperty("runInput")) ?? throw Invalid("run input is required"),
            EnumValue<DagRunStateV1>(e, "state"),
            e.GetProperty("nodes").EnumerateArray().Select(n =>
            {
                Object(n, "ordinal", "state", "childInstanceId", "input", "output", "failureCode", "readyAt",
                    "startedAt", "completedAt");
                return new DagRunNodeStateV1(n.GetProperty("ordinal").GetInt32(), EnumValue<DagNodeStateV1>(n, "state"),
                    Child(n), ReadValue(n.GetProperty("input")), ReadValue(n.GetProperty("output")),
                    NullableText(n, "failureCode"),
                    Time(n, "readyAt"), Time(n, "startedAt"), Time(n, "completedAt"));
            }).ToArray(), Time(e, "createdAt") ?? throw Invalid("createdAt is required"),
            Time(e, "updatedAt") ?? throw Invalid("updatedAt is required"), Time(e, "completedAt"));
    }

    private static DagRunGraphV1 ReadGraph(JsonElement e)
    {
        Object(e, "definition", "runInputContractKey", "nodes");
        return new DagRunGraphV1(ReadBinding(e.GetProperty("definition")), Text(e, "runInputContractKey"),
            e.GetProperty("nodes").EnumerateArray().Select(n =>
            {
                Object(n, "ordinal", "nodeId", "workflow", "inputContractKey", "outputContractKey",
                    "directDependencies");
                return new DagRunNodeV1(n.GetProperty("ordinal").GetInt32(), Text(n, "nodeId"),
                    ReadBinding(n.GetProperty("workflow")),
                    Text(n, "inputContractKey"), NullableText(n, "outputContractKey"),
                    n.GetProperty("directDependencies").EnumerateArray().Select(d => d.GetInt32()).ToArray());
            }).ToArray());
    }

    private static DagRunBindingV1 ReadBinding(JsonElement e)
    {
        Object(e, "definitionId", "definitionVersion", "fingerprint");
        return new DagRunBindingV1(DefinitionId.Parse(Id(e, "definitionId").ToString("D")),
            new DefinitionVersion(e.GetProperty("definitionVersion").GetInt32()), Text(e, "fingerprint"));
    }

    private static DagRunValueV1? ReadValue(JsonElement e)
    {
        if (e.ValueKind == JsonValueKind.Null) return null;
        Object(e, "isPresent", "contractKey", "codecFormat", "bytes", "fingerprint");
        return new DagRunValueV1(e.GetProperty("isPresent").GetBoolean(), Text(e, "contractKey"),
            Text(e, "codecFormat"),
            e.GetProperty("bytes").GetBytesFromBase64(), Text(e, "fingerprint"));
    }

    private static DagRunNodeChangeV1 ReadChange(JsonElement e)
    {
        Object(e, "ordinal", "state", "childInstanceId", "inputWrite", "outputWrite", "failureCode", "readyAt",
            "startedAt", "completedAt");
        return new DagRunNodeChangeV1(e.GetProperty("ordinal").GetInt32(), EnumValue<DagNodeStateV1>(e, "state"),
            Child(e),
            ReadValue(e.GetProperty("inputWrite")), ReadValue(e.GetProperty("outputWrite")),
            NullableText(e, "failureCode"),
            Time(e, "readyAt"), Time(e, "startedAt"), Time(e, "completedAt"));
    }

    private static void Object(JsonElement e, params string[] fields)
    {
        if (e.ValueKind != JsonValueKind.Object) throw Invalid("required object");
        var actual = e.EnumerateObject().Select(p => p.Name).ToArray();
        if (actual.Length != fields.Length || actual.Distinct(StringComparer.Ordinal).Count() != fields.Length ||
            actual.Except(fields, StringComparer.Ordinal).Any()) throw Invalid("extra, missing or duplicate field");
    }

    private static string Text(JsonElement e, string key) =>
        e.GetProperty(key).GetString() ?? throw Invalid(key + " is required");

    private static string? NullableText(JsonElement e, string key) =>
        e.GetProperty(key).ValueKind == JsonValueKind.Null ? null : Text(e, key);

    private static Guid Id(JsonElement e, string key)
    {
        var text = Text(e, key);
        if (!Guid.TryParseExact(text, "D", out var id) || id == Guid.Empty || id.ToString("D") != text)
            throw Invalid(key + " is not a canonical GUID");
        return id;
    }

    private static InstanceId? Child(JsonElement e) => e.GetProperty("childInstanceId").ValueKind == JsonValueKind.Null
        ? null
        : InstanceId.Parse(Id(e, "childInstanceId").ToString("D"));

    private static DateTimeOffset? Time(JsonElement e, string key)
    {
        if (e.GetProperty(key).ValueKind == JsonValueKind.Null) return null;
        var text = Text(e, key);
        if (!DateTimeOffset.TryParseExact(text, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ||
            time.Offset != TimeSpan.Zero ||
            time.ToString("O", CultureInfo.InvariantCulture) != text) throw Invalid(key + " is not canonical UTC");
        return time;
    }

    private static T EnumValue<T>(JsonElement e, string key) where T : struct, Enum
    {
        var text = Text(e, key);
        if (!Enum.TryParse<T>(text, out var value) || !Enum.IsDefined(value) || value.ToString() != text)
            throw Invalid(key + " enum");
        return value;
    }
}
