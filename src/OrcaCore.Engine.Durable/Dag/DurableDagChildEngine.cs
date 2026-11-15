using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Core.Internal;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Internal;

namespace OrcaCore.Engine.Durable.Dag;

internal sealed record DurableDagChildFact(WorkflowInstanceStatus Status, DagRunValueV1? Output);
internal sealed record DurableDagChildObservation(InstanceId ChildId, InstanceId RootId, InstanceId ParentId,
    WorkflowInstanceStatus Status, bool HasOutput, WorkflowFailure? Failure, bool IsTerminal);

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
        var graph = run.Graph.Nodes[ordinal];
        if (graph.InputContractKey != DurableDagWire.ChildInputKey(graph.Workflow) ||
            (graph.OutputContractKey is { } outputKey && outputKey != DurableDagWire.ChildOutputKey(graph.Workflow)) ||
            input.ContractKey != graph.InputContractKey || input.CodecFormat != CoreWorkflowValueCodec.Format ||
            input.Fingerprint != DurableDagWire.ValueFingerprint(input.Bytes))
            throw new InvalidDataException("DAG child input or logical workflow role is invalid.");
        if (await ReadBoundStartAsync(run, ordinal, token).ConfigureAwait(false) is not null) return true;
        var installed = definitions.ResolveDagBinding(graph.Workflow.DefinitionId, graph.Workflow.DefinitionVersion);
        if (installed is null) return false; // An exact definition may be installed on another host.
        if (installed.Fingerprint != graph.Workflow.Fingerprint ||
            (graph.OutputContractKey is null) != (installed.OutputType is null))
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
        if (await ReadBoundStartAsync(run, ordinal, token).ConfigureAwait(false) is null)
            return false;
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
        var graph = run.Graph.Nodes[ordinal];
        if (aggregate.Status == WorkflowInstanceStatus.Completed && graph.OutputContractKey is { } key)
        {
            if (key != DurableDagWire.ChildOutputKey(graph.Workflow))
                throw new InvalidDataException("DAG child output has the wrong logical workflow role.");
            var installed = definitions.ResolveDagBinding(graph.Workflow.DefinitionId, graph.Workflow.DefinitionVersion);
            if (installed is null) return null; // Another durable host can validate the exact output schema.
            if (installed.Fingerprint != graph.Workflow.Fingerprint || installed.OutputSchemaIdentity is null)
                throw new InvalidDataException("DAG child output binding differs from the installed definition.");
            var checkpoint = await store.LoadCheckpointAsync(started.InstanceId, token).ConfigureAwait(false);
            if (!checkpoint.HasValue || checkpoint.Value.ContentType != DurableExecutionEnvelopeV2.ContentType)
                throw new InvalidDataException("A resultful DAG child has no terminal output checkpoint.");
            var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint.Value.Payload);
            if (envelope.Output is not { } value) throw new InvalidDataException("A resultful DAG child has no output.");
            if (value.SchemaIdentity != installed.OutputSchemaIdentity)
                throw new InvalidDataException("DAG output contract differs from its terminal checkpoint.");
            // These are already fixed-codec committed bytes. Decoding belongs to the
            // later mapping bridge; a join never executes the mapper or reserializes output.
            output = new(true, key, CoreWorkflowValueCodec.Format, value.Payload,
                DurableDagWire.ValueFingerprint(value.Payload));
        }
        return new(aggregate.Status!.Value, output);
    }

    internal async Task<bool> CancelAsync(DagRunSnapshotV1 run, int ordinal, CancellationToken token)
        => await TryCancelAsync(run, ordinal, token).ConfigureAwait(false) is not null;

    internal async Task<WorkflowCancellationRequestStatus?> TryCancelAsync(DagRunSnapshotV1 run, int ordinal, CancellationToken token)
    {
        if (!await StartOrReattachAsync(run, ordinal, token).ConfigureAwait(false)) return null;
        var result = await commands.ProcessAsync(new CancelWorkflowCommand
        {
            CommandId = CommandId.New(), InstanceId = run.Nodes[ordinal].ChildInstanceId!,
            RequestedAt = timeProvider.GetUtcNow()
        }, token).ConfigureAwait(false);
        if (result.Outcome == DurableCommandOutcome.Conflict) return null;
        return result.LifecycleDisposition switch
        {
            DurableLifecycleDisposition.CancellationRequested => WorkflowCancellationRequestStatus.Requested,
            DurableLifecycleDisposition.CancellationAlreadyRequested => WorkflowCancellationRequestStatus.AlreadyRequested,
            DurableLifecycleDisposition.AlreadyTerminal => WorkflowCancellationRequestStatus.AlreadyTerminal,
            _ => throw new InvalidDataException("DAG child cancellation did not produce a lifecycle disposition.")
        };
    }

    internal async Task<DurableDagChildObservation?> ObserveAsync(DagRunSnapshotV1 run, int ordinal, CancellationToken token)
    {
        var started = await ReadBoundStartAsync(run, ordinal, token).ConfigureAwait(false);
        if (started is null) return null;
        var aggregate = await new DurableAggregateLoader(store).LoadAsync(started.InstanceId, token).ConfigureAwait(false);
        var hasOutput = aggregate.Status == WorkflowInstanceStatus.Completed &&
            run.Graph.Nodes[ordinal].OutputContractKey is not null;
        WorkflowFailure? failure = null;
        if (aggregate.ErrorSummary is { Length: > 0 } summary && !string.IsNullOrWhiteSpace(summary))
        {
            var separator = summary.IndexOf(':', StringComparison.Ordinal);
            failure = DurableContractAdapter.WorkflowFailure(separator > 0 ? summary[..separator] : "WF-RUNTIME-FAILED",
                separator > 0 ? summary[(separator + 1)..].Trim() : summary, DurableContractAdapter.AuthoredLocation("workflow:$"),
                DurableContractAdapter.RootFailureOccurrence(), []);
        }
        return new(started.InstanceId, started.RootInstanceId!, started.ParentInstanceId!, aggregate.Status!.Value,
            hasOutput, failure, aggregate.IsTerminal);
    }

    internal static PayloadFingerprint RehydrateFingerprint(string fingerprint) => new(fingerprint);

    internal async Task<WorkflowStartedEvent?> ReadBoundStartAsync(DagRunSnapshotV1 run, int ordinal, CancellationToken token)
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
        if (input.ContractKey != DurableDagWire.ChildInputKey(binding) ||
            input.ContractKey != run.Graph.Nodes[ordinal].InputContractKey ||
            input.CodecFormat != CoreWorkflowValueCodec.Format ||
            input.Fingerprint != DurableDagWire.ValueFingerprint(input.Bytes))
            throw new InvalidDataException("DAG child input binding is invalid.");
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
