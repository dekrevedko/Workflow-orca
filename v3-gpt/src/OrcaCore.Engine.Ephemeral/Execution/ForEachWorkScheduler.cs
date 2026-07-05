using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Definitions;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed class ForEachWorkScheduler<TState, TInput>(
    ForEachNode<TState> forEachNode,
    SequenceExecutionContext<TState, TInput> parentContext,
    WorkflowInstance<TState>.ForEachGroupRecord group,
    IReadOnlyList<ForEachWorkItemSnapshot> workItems,
    int forEachIndex,
    ISequenceExecutionEngine<TState> sequenceExecution,
    TimeProvider timeProvider)
{
    private readonly object gate = new();
    private readonly HashSet<int> finishedItems = [];
    private readonly int maxConcurrency = forEachNode.MaxConcurrency ?? workItems.Count;
    private int activeCount;
    private int nextOrdinal;
    private int continued;
    private Exception? firstFailure;

    internal async Task DispatchAvailableAsync(CancellationToken dispatchToken)
    {
        while (TryReserveNext(out var workItemIndex, out var partitionItems))
        {
            var itemContext = parentContext.CreateNested(
                forEachNode.Body,
                new BranchId(workItemIndex, $"item-{workItemIndex}"),
                parentContext.ResumeEvent,
                itemToken => ItemCompletedAsync(workItemIndex, itemToken)) with
            {
                ForEachItem = new ForEachItemContext(workItemIndex, partitionItems)
            };
            var completed = await sequenceExecution.RunSequenceAsync(
                itemContext,
                startIndex: 0,
                dispatchToken,
                deferStepFailures: true).ConfigureAwait(false);

            var itemFailure = parentContext.RunState.TakeDeferredFailure();
            if (itemFailure is not null)
            {
                await ItemFailedAsync(workItemIndex, itemFailure, dispatchToken).ConfigureAwait(false);
                if (parentContext.RunState.Instance!.Status == WorkflowStatus.Failed)
                {
                    return;
                }

                continue;
            }

            if (parentContext.RunState.Instance!.Status == WorkflowStatus.Failed)
            {
                return;
            }

            if (completed)
            {
                await ItemCompletedAsync(workItemIndex, dispatchToken).ConfigureAwait(false);
            }
        }
    }

    private bool TryReserveNext(out int workItemIndex, out IReadOnlyList<object?> partitionItems)
    {
        lock (gate)
        {
            if (activeCount >= maxConcurrency ||
                nextOrdinal >= workItems.Count ||
                parentContext.RunState.Instance!.Status == WorkflowStatus.Failed ||
                (forEachNode.JoinPolicy is ForEachJoinPolicy.WhenAny &&
                    Interlocked.CompareExchange(ref continued, 0, 0) == 1))
            {
                workItemIndex = -1;
                partitionItems = [];
                return false;
            }

            var reserved = workItems[nextOrdinal];
            workItemIndex = reserved.Index;
            partitionItems = reserved.Items;
            nextOrdinal++;
            activeCount++;
            parentContext.RunState.Instance.StartForEachWorkItem(
                group,
                workItemIndex,
                timeProvider.GetUtcNow());
            return true;
        }
    }

    private async Task ItemCompletedAsync(int workItemIndex, CancellationToken itemToken)
    {
        var shouldContinueParent = false;
        var shouldFailParent = false;
        lock (gate)
        {
            if (!finishedItems.Add(workItemIndex))
            {
                return;
            }

            activeCount--;
            parentContext.RunState.Instance!.CompleteForEachWorkItem(
                group,
                workItemIndex,
                timeProvider.GetUtcNow());
            if (forEachNode.JoinPolicy is ForEachJoinPolicy.WhenAny)
            {
                if (forEachNode.ResidualPolicy is ForEachResidualPolicy.CancelRemaining)
                {
                    var cancelled = parentContext.RunState.Instance.CancelForEachResidualWork(
                        group,
                        workItemIndex,
                        timeProvider.GetUtcNow());
                    foreach (var cancelledIndex in cancelled)
                    {
                        parentContext.RunState.Instance.ResolveBranchRuntimeWork(
                            new BranchId(cancelledIndex, $"item-{cancelledIndex}"));
                    }

                    shouldContinueParent = true;
                }
                else
                {
                    shouldContinueParent = finishedItems.Count == workItems.Count;
                }
            }
            else if (finishedItems.Count == workItems.Count)
            {
                shouldFailParent = firstFailure is not null &&
                    forEachNode.FailurePolicy is ForEachFailurePolicy.WaitAllThenFail;
                shouldContinueParent = !shouldFailParent;
            }
        }

        if (shouldFailParent)
        {
            sequenceExecution.Fail(parentContext.RunState.Instance!, firstFailure!, forEachNode.NodeId);
            return;
        }

        if (shouldContinueParent)
        {
            await ContinueParentOnceAsync(itemToken).ConfigureAwait(false);
            return;
        }

        await DispatchAvailableAsync(itemToken).ConfigureAwait(false);
    }

    private async Task ItemFailedAsync(
        int workItemIndex,
        Exception exception,
        CancellationToken itemToken)
    {
        var shouldContinueParent = false;
        var shouldFailParent = false;
        lock (gate)
        {
            if (!finishedItems.Add(workItemIndex))
            {
                return;
            }

            firstFailure ??= exception;
            activeCount--;
            parentContext.RunState.Instance!.FailForEachWorkItem(
                group,
                workItemIndex,
                exception.Message,
                timeProvider.GetUtcNow());
            shouldFailParent = forEachNode.FailurePolicy is ForEachFailurePolicy.FailFast ||
                (forEachNode.FailurePolicy is ForEachFailurePolicy.WaitAllThenFail &&
                    finishedItems.Count == workItems.Count);
            shouldContinueParent = forEachNode.FailurePolicy is ForEachFailurePolicy.ContinueWithPartialFailures &&
                finishedItems.Count == workItems.Count;
        }

        if (shouldFailParent)
        {
            sequenceExecution.Fail(parentContext.RunState.Instance!, firstFailure!, forEachNode.NodeId);
            return;
        }

        if (shouldContinueParent)
        {
            await ContinueParentOnceAsync(itemToken).ConfigureAwait(false);
            return;
        }

        await DispatchAvailableAsync(itemToken).ConfigureAwait(false);
    }

    private async Task ContinueParentOnceAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref continued, 1) == 0)
        {
            await sequenceExecution.ContinueSequenceAsync(
                parentContext,
                forEachIndex + 1,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
