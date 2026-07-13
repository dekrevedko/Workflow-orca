using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Durable.Aggregates;

namespace OrcaCore.Engine.Durable.Driver;

internal sealed partial class DurableDriverSegmentRun<TState>
{
    private bool HasPendingChildResume(DurableDriverCursor cursor)
    {
        if (cursor.ChildGroupId is not { } groupId ||
            !Guid.TryParse(groupId, out var groupGuid))
        {
            return false;
        }

        var resumeTokenId = new EventId(groupGuid);
        return context.Aggregate.ChildState.RecordedParentResumeTokens.Contains(resumeTokenId) &&
            !context.Aggregate.ChildState.ConsumedParentResumeTokens.Contains(resumeTokenId);
    }

    private async Task<DurableSegmentResult?> RunChildAsync(
        DurableDriverCursor cursor,
        RunChildNode<TState> node,
        CancellationToken cancellationToken)
    {
        var commandId = CommandId.New();
        var childInstanceId = DurableChildWorkflowState.DeterministicChildId(
            context.InstanceId,
            commandId,
            0);
        var waitId = new WaitId(childInstanceId.Value);

        cursor.Phase = DurableCursorPhase.SuspendedOnChildren;
        cursor.ChildGroupId = childInstanceId.Value.ToString("D");
        cursor.ChildWaitIds = [waitId];

        var command = new DurableRunChildCommand(
            commandId,
            context.InstanceId,
            context.TimeProvider.GetUtcNow(),
            childInstanceId,
            node.ChildDefinitionId,
            node.ChildDefinitionVersion,
            node.FailurePolicy)
        {
            Envelope = BuildEnvelope(),
            ExpectedStreamVersion = currentVersion
        };
        return await CommitAsync(
            () => context.Processor.ProcessAsync(command, CancellationToken.None),
            [],
            [],
            []).ConfigureAwait(false);
    }

    private async Task<DurableSegmentResult?> RunChildrenAsync(
        DurableDriverCursor cursor,
        RunChildrenNode<TState> node,
        CancellationToken cancellationToken)
    {
        var itemSnapshots = node.ItemSnapshotSelector(state).ToArray();
        if (itemSnapshots.Length == 0)
        {
            cursor.AdvanceIndex();
            return null;
        }

        var commandId = CommandId.New();
        cursor.Phase = DurableCursorPhase.SuspendedOnChildren;
        cursor.ChildGroupId = commandId.Value.ToString("D");
        cursor.ChildWaitIds = Enumerable.Range(0, itemSnapshots.Length)
            .Select(index => new WaitId(
                DurableChildWorkflowState.DeterministicChildId(context.InstanceId, commandId, index).Value))
            .ToList();

        var command = new DurableRunChildrenCommand(
            commandId,
            context.InstanceId,
            context.TimeProvider.GetUtcNow(),
            node.ChildDefinitionId,
            node.ChildDefinitionVersion,
            itemSnapshots,
            node.FailurePolicy,
            node.MaxConcurrency,
            node.JoinPolicy,
            node.ResidualPolicy)
        {
            Envelope = BuildEnvelope(),
            ExpectedStreamVersion = currentVersion
        };
        return await CommitAsync(
            () => context.Processor.ProcessAsync(command, CancellationToken.None),
            [],
            [],
            []).ConfigureAwait(false);
    }

    private async Task<DurableSegmentResult?> ResolveChildSuspensionAsync(
        DurableDriverCursor cursor,
        CancellationToken cancellationToken)
    {
        var groupId = cursor.ChildGroupId!;
        var resumeTokenId = new EventId(Guid.Parse(groupId));
        var consumedWaitIds = cursor.ChildWaitIds
            .Where(pendingResumes.ContainsKey)
            .ToArray();

        cursor.Phase = DurableCursorPhase.AtNode;
        cursor.ChildGroupId = null;
        cursor.ChildWaitIds = [];
        cursor.AdvanceIndex();

        var command = new ConsumeParentResumeTokenCommand
        {
            CommandId = CommandId.New(),
            InstanceId = context.InstanceId,
            RequestedAt = context.TimeProvider.GetUtcNow(),
            GroupId = groupId,
            ResumeTokenId = resumeTokenId,
            Envelope = BuildEnvelope(),
            ConsumedResumeWaitIds = consumedWaitIds,
            ExpectedStreamVersion = currentVersion
        };
        return await CommitAsync(
            () => context.Processor.ProcessAsync(command, CancellationToken.None),
            consumedWaitIds,
            [],
            []).ConfigureAwait(false);
    }
}
