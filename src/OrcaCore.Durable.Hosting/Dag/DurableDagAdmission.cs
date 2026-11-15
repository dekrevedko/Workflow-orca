using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Providers.Dag;
using OrcaCore.Engine.Durable.Dag;

namespace OrcaCore.Durable.Hosting.Dag;

internal enum DurableDagMutationOutcomeV1
{
    Committed, Existing, CapacityDeferred, AdmissionStopped, StaleFence, VersionConflict,
    MappingInvalid, IntegrityConflict, NotFound
}

internal sealed record DurableDagMutationResultV1(
    DurableDagMutationOutcomeV1 Outcome, StreamVersion? Version, DagRunSnapshotV1? Snapshot);

internal sealed record DurableDagMappedInputV1(object? Value, Type DeclaredType);

internal sealed record DurableDagPreparedMappingV1(
    DurableDagNodeContractV1 Node, string ImmutableInputsFingerprint,
    DurableDagMappedInputV1? SourceInput, DagRunValueV1? NormalizedInput, bool IsMappingInvalid);

internal sealed class DurableDagAdmission(IWorkflowEventStore events, DurableDagRunStore store,
    DurableDagMappingReader reader, TimeProvider time)
{
    private readonly ConditionalWeakTable<DurableDagPreparedMappingV1, object> receipts = new();
    private static readonly object ReceiptMarker = new();

    internal async ValueTask<DurableDagPreparedMappingV1> PrepareMappingAsync(
        DurableDagNodeContractV1 node, DurableDagMappedInputV1? input, bool mappingInvalid, CancellationToken token)
    {
        var run = await store.ReadAsync(node.Identity.RunId, token).ConfigureAwait(false)
            ?? throw new InvalidOperationException("DAG run does not exist.");
        var frozenNode = node with { DirectDependencies = Array.AsReadOnly(node.DirectDependencies.ToArray()) };
        var immutable = reader.Inspect(run, frozenNode);
        var fingerprint = Fingerprint(run, frozenNode.Identity.AuthoredOrdinal, immutable);
        DagRunValueV1? normalized = null;
        if (!mappingInvalid)
        {
            if (input is null || input.DeclaredType != frozenNode.Workflow.InputType)
                mappingInvalid = true;
            else
            {
                try
                {
                    normalized = DurableDagValues.Normalize(DurableDagWire.ChildInputKey(run.Graph.Nodes[node.Identity.AuthoredOrdinal].Workflow),
                        input.Value, input.DeclaredType);
                }
                catch (Exception error) when (error is not (OutOfMemoryException or StackOverflowException or AccessViolationException))
                {
                    mappingInvalid = true;
                }
            }
        }
        token.ThrowIfCancellationRequested();
        var receipt = new DurableDagPreparedMappingV1(frozenNode, fingerprint, input, normalized, mappingInvalid);
        receipts.Add(receipt, ReceiptMarker);
        return receipt;
    }

    internal async ValueTask<DurableDagMutationResultV1> AdmitPreparedMappingAsync(
        DurableDagPreparedMappingV1 prepared, DagRunFenceV1 fence, StreamVersion expectedVersion,
        int maxConcurrentNodes, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(fence);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrentNodes, 1);
        if (!receipts.TryGetValue(prepared, out _) || prepared.Node.Identity.RunId != fence.RunId)
            return new(DurableDagMutationOutcomeV1.IntegrityConflict, null, null);
        var run = await store.ReadAsync(fence.RunId, token).ConfigureAwait(false);
        if (run is null) return new(DurableDagMutationOutcomeV1.NotFound, null, null);
        var ordinal = prepared.Node.Identity.AuthoredOrdinal;
        var state = run.Nodes[ordinal];
        if (state.Input is not null) return Result(DurableDagMutationOutcomeV1.Existing);
        if (run.State == DagRunStateV1.IntegrityFault) return Result(DurableDagMutationOutcomeV1.IntegrityConflict);
        if (run.State != DagRunStateV1.Running || state.State != DagNodeStateV1.Ready)
            return Result(DurableDagMutationOutcomeV1.AdmissionStopped);
        if (run.StreamVersion != expectedVersion) return Result(DurableDagMutationOutcomeV1.VersionConflict);
        try
        {
            if (Fingerprint(run, ordinal, reader.Inspect(run, prepared.Node)) != prepared.ImmutableInputsFingerprint)
                return Result(DurableDagMutationOutcomeV1.IntegrityConflict);
        }
        catch (InvalidDataException) { return Result(DurableDagMutationOutcomeV1.IntegrityConflict); }

        if (run.Nodes.Any(node => node.State == DagNodeStateV1.Ready && node.Ordinal < ordinal) ||
            (!prepared.IsMappingInvalid && run.Nodes.Count(node => node.State is
                DagNodeStateV1.StartAuthorized or DagNodeStateV1.CancellationRequested) >= maxConcurrentNodes))
            return Result(DurableDagMutationOutcomeV1.CapacityDeferred);
        var now = time.GetUtcNow().ToUniversalTime();
        if (now < run.UpdatedAt) now = run.UpdatedAt;
        var operation = Guid.CreateVersion7();
        ProviderCommitBatch batch;
        if (prepared.IsMappingInvalid)
        {
            batch = DurableDagWire.Commit(run, MappingFailure(run, ordinal, now, operation), fence);
        }
        else
        {
            var child = InstanceId.Parse(DurableDagWire.ChildId(run.RunId, ordinal).ToString("D"));
            var admitted = new DagRunNodeChangeV1(ordinal, DagNodeStateV1.StartAuthorized, child,
                prepared.NormalizedInput!, null, null, state.ReadyAt, now, null);
            var record = new DagRunRecordV1(1, DagRunRecordKindV1.InputAdmitted, run.RunId, operation,
                run.StreamVersion.Next(), run.State, null, [admitted], now, null);
            batch = DurableDagWire.Commit(run, [record], fence,
                [DurableDagWire.ChildWrite(DurableDagKinds.Start, run.RunId, ordinal)]);
        }
        var append = await events.AppendAsync(batch, token).ConfigureAwait(false);
        if (append.IsFailure)
            return Result(append.Error.Code switch
            {
                "DAG_VERSION_CONFLICT" => DurableDagMutationOutcomeV1.VersionConflict,
                "DAG_STALE_FENCE" or "DAG_TURN_ALREADY_COMMITTED" => DurableDagMutationOutcomeV1.StaleFence,
                "DAG_INTEGRITY_CONFLICT" => DurableDagMutationOutcomeV1.IntegrityConflict,
                _ => throw append.Error
            });
        return new(prepared.IsMappingInvalid ? DurableDagMutationOutcomeV1.MappingInvalid : DurableDagMutationOutcomeV1.Committed,
            append.Value.NewVersion, await store.ReadAsync(run.RunId, token).ConfigureAwait(false));

        DurableDagMutationResultV1 Result(DurableDagMutationOutcomeV1 outcome) => new(outcome, run.StreamVersion, run);
    }

    private static DagRunRecordV1[] MappingFailure(DagRunSnapshotV1 run, int ordinal, DateTimeOffset now, Guid operation)
    {
        var dependants = Enumerable.Range(0, run.Nodes.Count).Select(_ => new List<int>()).ToArray();
        foreach (var node in run.Graph.Nodes)
        foreach (var dependency in node.DirectDependencies)
            dependants[dependency].Add(node.Ordinal);
        var descendants = new HashSet<int> { ordinal };
        var pending = new Queue<int>(dependants[ordinal]);
        while (pending.TryDequeue(out var next))
            if (descendants.Add(next))
                foreach (var dependant in dependants[next]) pending.Enqueue(dependant);
        var changed = descendants.Order().Where(index => run.Nodes[index].State is DagNodeStateV1.Ready or DagNodeStateV1.Pending)
            .Select(index => new DagRunNodeChangeV1(index,
                index == ordinal ? DagNodeStateV1.Failed : DagNodeStateV1.DependencyBlocked, null, null, null,
                index == ordinal ? "DAG_INPUT_MAPPING_INVALID" : "DEPENDENCY_BLOCKED", run.Nodes[index].ReadyAt, null, now))
            .ToArray();
        var failed = new DagRunRecordV1(1, DagRunRecordKindV1.MappingFailed, run.RunId, operation,
            run.StreamVersion.Next(), run.State, null, changed, now, null);
        var changedOrdinals = changed.Select(node => node.Ordinal).ToHashSet();
        if (run.Nodes.Any(node => !changedOrdinals.Contains(node.Ordinal) && node.State is
                DagNodeStateV1.Ready or DagNodeStateV1.Pending or DagNodeStateV1.StartAuthorized or DagNodeStateV1.CancellationRequested))
            return [failed];
        return [failed, new(1, DagRunRecordKindV1.Terminal, run.RunId, operation, failed.StreamVersion.Next(),
            DagRunStateV1.Failed, null, [], now, now)];
    }

    private static string Fingerprint(DagRunSnapshotV1 run, int ordinal, DurableDagMappingReader.MappingBytes input)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Text("orcacore-dag-mapping-input-v1");
        Text(run.RunId.ToString("D"));
        Number(ordinal);
        Binding(run.Graph.Definition);
        Text(run.Graph.RunInputContractKey);
        Number(run.Graph.Nodes.Count);
        foreach (var node in run.Graph.Nodes)
        {
            Number(node.Ordinal);
            Text(node.NodeId);
            Binding(node.Workflow);
            Text(node.InputContractKey);
            Text(node.OutputContractKey ?? "");
            Number(node.DirectDependencies.Count);
            foreach (var dependency in node.DirectDependencies) Number(dependency);
        }
        Bytes(input.RunBytes);
        Number(input.Outputs.Count);
        foreach (var output in input.Outputs)
        {
            Number(output.Identity.AuthoredOrdinal);
            Bytes(output.Bytes);
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());

        void Binding(DagRunBindingV1 binding)
        {
            Text(binding.DefinitionId.ToString());
            Number(binding.DefinitionVersion.Value);
            Text(binding.Fingerprint);
        }
        void Text(string value) => Bytes(Encoding.UTF8.GetBytes(value));
        void Number(int value)
        {
            Span<byte> bytes = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32BigEndian(bytes, value);
            hash.AppendData(bytes);
        }
        void Bytes(byte[] value)
        {
            Number(value.Length);
            hash.AppendData(value);
        }
    }
}
