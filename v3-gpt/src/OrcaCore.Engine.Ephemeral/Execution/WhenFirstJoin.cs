using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Definitions;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed class WhenFirstJoin<TState>(
    WhenFirstNode<TState> node,
    WorkflowInstance<TState> instance,
    Func<DateTimeOffset> getUtcNow,
    Func<CancellationToken, Task> continueAsync)
{
    private readonly object gate = new();
    private readonly HashSet<BranchId> completedBranches = [];
    private int continued;
    private BranchId? winner;

    internal bool ShouldStopScheduling
    {
        get
        {
            lock (gate)
            {
                return node.ResidualPolicy is not WhenFirstResidualPolicy.LetRemainingComplete &&
                    winner is not null;
            }
        }
    }

    internal async Task BranchCompletedAsync(
        ParallelBranch<TState> branch,
        CancellationToken cancellationToken)
    {
        var shouldContinue = false;
        lock (gate)
        {
            if (!completedBranches.Add(branch.BranchId))
            {
                return;
            }

            var recordedAt = getUtcNow();
            if (winner is null)
            {
                winner = branch.BranchId;
                instance.RecordCompositionBranchOutcome(node.NodeId, branch.BranchId, "Winner", recordedAt);
                switch (node.ResidualPolicy)
                {
                    case WhenFirstResidualPolicy.CancelRemaining:
                        RecordResidualBranches("Cancelled", recordedAt);
                        shouldContinue = true;
                        break;
                    case WhenFirstResidualPolicy.IgnoreRemaining:
                        RecordResidualBranches("Ignored", recordedAt);
                        shouldContinue = true;
                        break;
                    case WhenFirstResidualPolicy.LetRemainingComplete:
                        shouldContinue = completedBranches.Count == node.Branches.Count;
                        break;
                    default:
                        throw new NotSupportedException(
                            $"WhenFirst residual policy '{node.ResidualPolicy}' is not supported.");
                }
            }
            else
            {
                instance.RecordCompositionBranchOutcome(node.NodeId, branch.BranchId, "Completed", recordedAt);
                shouldContinue = node.ResidualPolicy is WhenFirstResidualPolicy.LetRemainingComplete &&
                    completedBranches.Count == node.Branches.Count;
            }
        }

        if (shouldContinue && Interlocked.Exchange(ref continued, 1) == 0)
        {
            await continueAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private void RecordResidualBranches(string status, DateTimeOffset recordedAt)
    {
        foreach (var residualBranch in node.Branches.Where(candidate => candidate.BranchId != winner))
        {
            instance.RecordCompositionBranchOutcome(node.NodeId, residualBranch.BranchId, status, recordedAt);
            instance.ResolveBranchRuntimeWork(residualBranch.BranchId);
        }
    }
}
