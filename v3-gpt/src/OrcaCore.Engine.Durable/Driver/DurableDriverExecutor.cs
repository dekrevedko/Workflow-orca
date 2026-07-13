using OrcaCore.Core.Definitions;

namespace OrcaCore.Engine.Durable.Driver;

/// <summary>
/// The durable interpreter (DR-010..015): runs one advancement segment for one definition
/// version. Given rehydrated facts, the persisted position, and the business state, it
/// executes step code to the next suspension point and emits kernel commands through the
/// processor seam. Decisions are deterministic functions of (definition version, committed
/// facts, persisted position, business state); wall-clock reads go through TimeProvider.
/// </summary>
internal sealed class DurableDriverExecutor<TState> : IDurableDriverExecutor
{
    private readonly Dictionary<string, SequenceNode<TState>> sequencesByPath;

    internal DurableDriverExecutor(WorkflowDefinition<TState> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        sequencesByPath = [];
        IndexSequences(definition.RootSequence);
    }

    public async Task<DurableSegmentResult> RunSegmentAsync(
        DurableDriverContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var run = new DurableDriverSegmentRun<TState>(sequencesByPath, context);
        if (!run.TryLoad(out var loadFailure, out var parkReason))
        {
            return await run.ParkAsync(parkReason, loadFailure!, cancellationToken).ConfigureAwait(false);
        }

        return await run.AdvanceAsync(cancellationToken).ConfigureAwait(false);
    }

    private void IndexSequences(SequenceNode<TState> sequence)
    {
        sequencesByPath[sequence.NodeId] = sequence;
        foreach (var node in sequence.Children)
        {
            switch (node)
            {
                case IfNode<TState> ifNode:
                    IndexSequences(ifNode.Then);
                    IndexSequences(ifNode.Else);
                    break;
                case WhileNode<TState> whileNode:
                    IndexSequences(whileNode.Body);
                    break;
                case ParallelNode<TState> parallelNode:
                    foreach (var branch in parallelNode.Branches)
                    {
                        IndexSequences(branch.Sequence);
                    }

                    break;
                case WhenFirstNode<TState> whenFirstNode:
                    foreach (var branch in whenFirstNode.Branches)
                    {
                        IndexSequences(branch.Sequence);
                    }

                    break;
            }
        }
    }
}
