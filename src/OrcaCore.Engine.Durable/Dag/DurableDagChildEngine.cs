using System.Security.Cryptography;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Core.Internal;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Execution;

namespace OrcaCore.Engine.Durable.Dag;

internal sealed record DurableDagChildFact(WorkflowInstanceStatus Status, DagRunValueV1? Output);

// Only the existing engine friend reaches compiled definitions/fixed-codec data. Hosting
// supplies persisted coordinates, never reflection-built definitions or live mapper values.
internal sealed class DurableDagChildEngine(IWorkflowEventStore store, DurableDefinitionRegistry definitions,
    DurableCommandProcessor commands, TimeProvider timeProvider)
{
    internal async Task<bool> StartOrReattachAsync(DagRunSnapshotV1 run, int ordinal, CancellationToken token)
    {
        var node = run.Nodes[ordinal];
        if (node.Input is not { IsPresent: true } input || node.ChildInstanceId is not { } child ||
            child.Value != DurableDagWire.ChildId(run.RunId, ordinal))
            throw new InvalidDataException("DAG child lacks its authorized input/identity.");
        if (await ReadBoundStartAsync(run, ordinal, token).ConfigureAwait(false) is not null) return true;
        var graph = run.Graph.Nodes[ordinal];
        var installed = definitions.ResolveDagBinding(graph.Workflow.DefinitionId, graph.Workflow.DefinitionVersion);
        if (installed is null) return false; // An exact definition may be installed on another host.
        if (installed.Fingerprint != graph.Workflow.Fingerprint ||
            graph.InputContractKey != DurableDagWire.ContractKey(installed.InputType) ||
            graph.OutputContractKey != (installed.OutputType is null ? null : DurableDagWire.ContractKey(installed.OutputType)) ||
            input.ContractKey != graph.InputContractKey || input.CodecFormat != CoreWorkflowValueCodec.Format)
            throw new InvalidDataException("DAG child definition/type binding differs from the installed definition.");
        var anchor = InstanceId.Parse(run.RunId.ToString("D"));
        var result = await commands.ProcessAsync(new StartWorkflowCommand
        {
            CommandId = CommandId.New(), InstanceId = child, RequestedAt = timeProvider.GetUtcNow(),
            DefinitionId = graph.Workflow.DefinitionId, DefinitionVersion = graph.Workflow.DefinitionVersion,
            DefinitionFingerprint = graph.Workflow.Fingerprint, InputFingerprint = input.Fingerprint,
            InputContentType = input.CodecFormat, InputPayload = input.Bytes,
            ParentInstanceId = anchor, RootInstanceId = anchor
            // No caller start key: the deterministic workflow stream itself owns the identity.
        }, token).ConfigureAwait(false);
        if (result.Outcome == DurableCommandOutcome.Conflict) return false;
        await ReadBoundStartAsync(run, ordinal, token).ConfigureAwait(false);
        // The start transaction already emitted an ordinary continuation. Do not execute
        // arbitrary user steps under this outbox claim or an owning run turn.
        return true;
    }

    internal async Task<DurableDagChildFact?> ReadTerminalAsync(DagRunSnapshotV1 run, int ordinal, CancellationToken token)
    {
        var started = await ReadBoundStartAsync(run, ordinal, token).ConfigureAwait(false);
        if (started is null) return null;
        var aggregate = await new DurableAggregateLoader(store).LoadAsync(started.InstanceId, token).ConfigureAwait(false);
        if (!aggregate.IsTerminal) return null;
        DagRunValueV1? output = null;
        if (aggregate.Status == WorkflowInstanceStatus.Completed && run.Graph.Nodes[ordinal].OutputContractKey is { } key)
        {
            var checkpoint = await store.LoadCheckpointAsync(started.InstanceId, token).ConfigureAwait(false);
            if (!checkpoint.HasValue || checkpoint.Value.ContentType != DurableExecutionEnvelopeV2.ContentType)
                throw new InvalidDataException("A resultful DAG child has no terminal output checkpoint.");
            var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint.Value.Payload);
            if (envelope.Output is not { } value) throw new InvalidDataException("A resultful DAG child has no output.");
            if (Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value.SchemaIdentity))) != key)
                throw new InvalidDataException("DAG output contract differs from its terminal checkpoint.");
            // These are already fixed-codec committed bytes. Decoding belongs to the
            // later mapping bridge; a join never executes the mapper or reserializes output.
            output = new(true, key, CoreWorkflowValueCodec.Format, value.Payload,
                Convert.ToHexString(SHA256.HashData(value.Payload)));
        }
        return new(aggregate.Status!.Value, output);
    }

    internal async Task<bool> CancelAsync(DagRunSnapshotV1 run, int ordinal, CancellationToken token)
    {
        if (!await StartOrReattachAsync(run, ordinal, token).ConfigureAwait(false)) return false;
        var result = await commands.ProcessAsync(new CancelWorkflowCommand
        {
            CommandId = CommandId.New(), InstanceId = run.Nodes[ordinal].ChildInstanceId!,
            RequestedAt = timeProvider.GetUtcNow()
        }, token).ConfigureAwait(false);
        return result.Outcome != DurableCommandOutcome.Conflict;
    }

    private async Task<WorkflowStartedEvent?> ReadBoundStartAsync(DagRunSnapshotV1 run, int ordinal, CancellationToken token)
    {
        var node = run.Nodes[ordinal];
        var child = node.ChildInstanceId ?? throw new InvalidDataException("Missing DAG child coordinate.");
        if (child.Value != DurableDagWire.ChildId(run.RunId, ordinal))
            throw new InvalidDataException("DAG deterministic child identity mismatch.");
        var facts = await store.LoadTailAsync(new(child), StreamVersion.Empty, token).ConfigureAwait(false);
        var started = facts.OfType<WorkflowStartedEvent>().SingleOrDefault();
        if (started is null)
        {
            if (facts.Count != 0 || (await store.LoadCheckpointAsync(child, token).ConfigureAwait(false)).HasValue)
                throw new InvalidDataException("DAG child binding evidence is unavailable.");
            return null;
        }
        var binding = run.Graph.Nodes[ordinal].Workflow;
        var input = node.Input ?? throw new InvalidDataException("Missing committed DAG input.");
        var anchor = InstanceId.Parse(run.RunId.ToString("D"));
        if (started.DefinitionId != binding.DefinitionId || started.DefinitionVersion != binding.DefinitionVersion ||
            started.DefinitionFingerprint != binding.Fingerprint || started.InputFingerprint != input.Fingerprint ||
            started.InputContentType != input.CodecFormat || started.InputPayload is not { } bytes ||
            !bytes.AsSpan().SequenceEqual(input.Bytes) || started.ParentInstanceId?.Value != anchor.Value ||
            started.RootInstanceId?.Value != anchor.Value || started.IdempotencyKey is not null)
            throw new InvalidDataException("DAG deterministic child collision: complete binding differs.");
        return started;
    }
}
