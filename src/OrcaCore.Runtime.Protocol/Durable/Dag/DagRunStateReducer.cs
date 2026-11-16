using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Serialization;

namespace OrcaCore.Abstractions.Durable.Dag;

// Linked as private source into the provider implementations. No internal friend,
// public helper API, provider dependency or second business-value codec is introduced.
internal static class DagRunStateReducer
{
    internal static DagRunSnapshotV1 Apply(DagRunSnapshotV1? before, DagRunRecordV1 record)
    {
        _ = DagRunRecordCodecV1.Serialize(record);
        if (before is null)
        {
            if (record.Kind != DagRunRecordKindV1.Created) throw Invalid("creation must be first");
            return record.CreationSnapshot!;
        }

        if (record.RunId != before.RunId || record.StreamVersion.Value != before.StreamVersion.Value + 1 ||
            record.Kind == DagRunRecordKindV1.Created || record.UpdatedAt < before.UpdatedAt ||
            Terminal(before.State)) throw Invalid("run identity, version or terminal barrier");
        var nodes = before.Nodes.ToArray();
        var changed = record.ChangedNodes.ToDictionary(n => n.Ordinal);
        foreach (var change in record.ChangedNodes)
        {
            if (change.Ordinal < 0 || change.Ordinal >= nodes.Length) throw Invalid("node ordinal");
            var old = nodes[change.Ordinal];
            if ((old.ChildInstanceId is not null && old.ChildInstanceId.Value != change.ChildInstanceId?.Value) ||
                (old.ReadyAt is not null && old.ReadyAt != change.ReadyAt) ||
                (old.StartedAt is not null && old.StartedAt != change.StartedAt) ||
                (old.CompletedAt is not null && old.CompletedAt != change.CompletedAt) ||
                (old.Input is not null && change.InputWrite is not null) ||
                (old.Output is not null && change.OutputWrite is not null))
                throw Invalid("immutable node coordinates/value overwritten");
            switch (record.Kind)
            {
                case DagRunRecordKindV1.InputAdmitted:
                    if (before.State != DagRunStateV1.Running || old.State != DagNodeStateV1.Ready ||
                        before.Nodes.Any(n => n.Ordinal < old.Ordinal && n.State == DagNodeStateV1.Ready) ||
                        change.ChildInstanceId?.Value != ChildId(before.RunId, old.Ordinal) ||
                        change.InputWrite?.ContractKey != before.Graph.Nodes[old.Ordinal].InputContractKey ||
                        change.StartedAt != record.UpdatedAt || change.OutputWrite is not null ||
                        change.FailureCode is not null || change.CompletedAt is not null)
                        throw Invalid("input admission");
                    break;
                case DagRunRecordKindV1.MappingFailed:
                    if (before.State != DagRunStateV1.Running ||
                        old.State is not (DagNodeStateV1.Pending or DagNodeStateV1.Ready) ||
                        change.ChildInstanceId is not null || change.StartedAt is not null ||
                        change.CompletedAt != record.UpdatedAt ||
                        (change.State == DagNodeStateV1.Failed && old.State != DagNodeStateV1.Ready))
                        throw Invalid("mapping failure");
                    break;
                case DagRunRecordKindV1.ChildJoined:
                    if (change.State is DagNodeStateV1.Succeeded or DagNodeStateV1.Failed or DagNodeStateV1.Cancelled)
                    {
                        if (old.State is not (DagNodeStateV1.StartAuthorized or DagNodeStateV1.CancellationRequested) ||
                            change.CompletedAt != record.UpdatedAt ||
                            (change.State == DagNodeStateV1.Cancelled &&
                             before.State != DagRunStateV1.CancellationRequested) ||
                            (change.State == DagNodeStateV1.Failed && change.FailureCode is not
                                ("CHILD_FAILED" or "CHILD_TIMED_OUT" or "CHILD_TERMINATED" or "CHILD_CANCELLED")) ||
                            (change.State != DagNodeStateV1.Failed && change.FailureCode is not null) ||
                            (change.State == DagNodeStateV1.Succeeded &&
                             (before.Graph.Nodes[old.Ordinal].OutputContractKey is { } outputKey
                                 ? change.OutputWrite?.ContractKey != outputKey
                                 : change.OutputWrite is not null)))
                            throw Invalid("child terminal join");
                    }
                    else if (old.State is not (DagNodeStateV1.Pending or DagNodeStateV1.Ready))
                        throw Invalid("derived dependency transition");

                    break;
                case DagRunRecordKindV1.CancellationRequested:
                    if (before.State != DagRunStateV1.Running ||
                        change.FailureCode is not null || change.InputWrite is not null ||
                        change.OutputWrite is not null ||
                        (change.State == DagNodeStateV1.Cancelled
                            ? change.CompletedAt != record.UpdatedAt
                            : change.CompletedAt is not null) ||
                        (old.State is DagNodeStateV1.Pending or DagNodeStateV1.Ready
                            ? change.State != DagNodeStateV1.Cancelled
                            : old.State != DagNodeStateV1.StartAuthorized ||
                              change.State != DagNodeStateV1.CancellationRequested))
                        throw Invalid("cancellation transition");
                    break;
                default:
                    throw Invalid("this kind cannot change node business state");
            }

            nodes[change.Ordinal] = new DagRunNodeStateV1(change.Ordinal, change.State, change.ChildInstanceId,
                change.InputWrite ?? old.Input, change.OutputWrite ?? old.Output, change.FailureCode,
                change.ReadyAt, change.StartedAt, change.CompletedAt);
        }

        switch (record.Kind)
        {
            case DagRunRecordKindV1.MappingFailed:
                var failed = record.ChangedNodes.Single(n => n.State == DagNodeStateV1.Failed).Ordinal;
                RequireBlockedClosure(before, nodes, failed, changed);
                break;
            case DagRunRecordKindV1.ChildJoined:
                var joined = record.ChangedNodes.Where(n => n.State is DagNodeStateV1.Succeeded or
                    DagNodeStateV1.Failed or DagNodeStateV1.Cancelled).ToArray();
                if (joined.Length != 1) throw Invalid("one authoritative child per join fact");
                if (before.State == DagRunStateV1.CancellationRequested)
                {
                    if (record.ChangedNodes.Count != 1)
                        throw Invalid("a cancelling run cannot ready or re-block descendants");
                }
                else if (joined[0].State == DagNodeStateV1.Succeeded)
                {
                    for (var i = 0; i < nodes.Length; i++)
                    {
                        var ready = before.Nodes[i].State == DagNodeStateV1.Pending &&
                                    before.Graph.Nodes[i].DirectDependencies
                                        .All(d => nodes[d].State == DagNodeStateV1.Succeeded);
                        if (ready
                                ? nodes[i].State != DagNodeStateV1.Ready || nodes[i].ReadyAt != record.UpdatedAt
                                : i != joined[0].Ordinal && nodes[i] != before.Nodes[i])
                            throw Invalid("success readiness closure");
                    }
                }
                else RequireBlockedClosure(before, nodes, joined[0].Ordinal, changed);

                break;
            case DagRunRecordKindV1.CancellationRequested:
                if (record.State != DagRunStateV1.CancellationRequested ||
                    before.Nodes.Where(n =>
                            n.State is DagNodeStateV1.Pending or DagNodeStateV1.Ready or DagNodeStateV1.StartAuthorized)
                        .Any(n => !changed.ContainsKey(n.Ordinal))) throw Invalid("incomplete cancellation barrier");
                break;
            case DagRunRecordKindV1.Terminal:
                if (nodes.Any(n => n.State is DagNodeStateV1.Pending or DagNodeStateV1.Ready or
                        DagNodeStateV1.StartAuthorized or DagNodeStateV1.CancellationRequested))
                    throw Invalid("terminal with live work");
                var expected = before.State == DagRunStateV1.CancellationRequested ? DagRunStateV1.Cancelled :
                    nodes.All(n => n.State == DagNodeStateV1.Succeeded) ? DagRunStateV1.Succeeded :
                    DagRunStateV1.Failed;
                if (record.State != expected) throw Invalid("terminal outcome");
                break;
            case DagRunRecordKindV1.PoisonReconciled:
                if (before.State != DagRunStateV1.IntegrityFault || changed.Count != 0 ||
                    record.State != (nodes.Any(n =>
                        n.State is DagNodeStateV1.Cancelled or DagNodeStateV1.CancellationRequested)
                        ? DagRunStateV1.CancellationRequested
                        : DagRunStateV1.Running))
                    throw Invalid("poison reconciliation cannot rewrite business facts or forget cancellation");
                break;
        }

        if (record.Kind is DagRunRecordKindV1.InputAdmitted or DagRunRecordKindV1.MappingFailed
                or DagRunRecordKindV1.ChildJoined &&
            record.State != before.State) throw Invalid("implicit run-state change");
        return before with
        {
            StreamVersion = record.StreamVersion, State = record.State, Nodes = nodes,
            UpdatedAt = record.UpdatedAt, CompletedAt = record.CompletedAt
        };
    }

    private static void RequireBlockedClosure(DagRunSnapshotV1 before, DagRunNodeStateV1[] after, int failed,
        IReadOnlyDictionary<int, DagRunNodeChangeV1> changed)
    {
        var descendants = new HashSet<int>();
        var dependants = Enumerable.Range(0, after.Length).Select(_ => new List<int>()).ToArray();
        foreach (var node in before.Graph.Nodes)
        foreach (var dependency in node.DirectDependencies)
            dependants[dependency].Add(node.Ordinal);
        var pending = new Queue<int>(dependants[failed]);
        while (pending.TryDequeue(out var ordinal))
            if (descendants.Add(ordinal))
                foreach (var dependant in dependants[ordinal])
                    pending.Enqueue(dependant);
        for (var i = 0; i < after.Length; i++)
        {
            if (i == failed) continue;
            var mustBlock = descendants.Contains(i) &&
                            before.Nodes[i].State is DagNodeStateV1.Pending or DagNodeStateV1.Ready;
            if (mustBlock
                    ? after[i].State != DagNodeStateV1.DependencyBlocked || !changed.ContainsKey(i)
                    : after[i] != before.Nodes[i])
                throw Invalid("failure must atomically block exactly its pending/ready descendants");
        }
    }

    internal static Guid ChildId(Guid runId, int ordinal)
    {
        var recipe = $"orcacore-dag-child-v1\n{runId:D}\n{ordinal.ToString(CultureInfo.InvariantCulture)}\n";
        var hex = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(recipe)))[..32];
        if (hex.All(c => c == '0')) hex = hex[..31] + "1";
        return Guid.ParseExact(hex, "N");
    }

    internal static bool Terminal(DagRunStateV1 state) =>
        state is DagRunStateV1.Succeeded or DagRunStateV1.Failed or DagRunStateV1.Cancelled;

    private static InvalidDataException Invalid(string reason) => new("DAG_PROTOCOL_INVALID: " + reason + ".");
}
