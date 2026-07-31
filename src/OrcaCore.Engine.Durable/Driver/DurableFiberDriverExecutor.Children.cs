using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Execution;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;

namespace OrcaCore.Engine.Durable.Driver;

internal sealed partial class DurableFiberDriverExecutor<TState>
{
    private async Task<ChildInstructionTransition> ExecuteChildInstructionAsync(
        DurableDriverContext context,
        StructuredExecutionState execution,
        FiberRecord fiber,
        CompiledInstruction instruction,
        TState state,
        List<DurableOwnedObligationState> ownedObligations,
        StreamVersion currentVersion,
        WaitId? consumedWaitId,
        CancellationToken cancellationToken)
    {
        var existing = ownedObligations.FirstOrDefault(obligation =>
            obligation.Kind == DurableOwnedObligationKind.ChildGroup &&
            obligation.FiberId == fiber.Id.Value);
        if (existing is not null)
        {
            return await ResumeChildGroupAsync(
                context,
                execution,
                fiber,
                instruction,
                state,
                ownedObligations,
                existing,
                currentVersion,
                cancellationToken).ConfigureAwait(false);
        }

        return instruction.Kind == CompiledInstructionKind.RunChild
            ? await DispatchChildAsync(
                context,
                execution,
                fiber,
                instruction,
                state,
                ownedObligations,
                currentVersion,
                consumedWaitId,
                cancellationToken).ConfigureAwait(false)
            : await DispatchChildrenAsync(
                context,
                execution,
                fiber,
                instruction,
                state,
                ownedObligations,
                currentVersion,
                consumedWaitId,
                cancellationToken).ConfigureAwait(false);
    }

    private async Task<ChildInstructionTransition> DispatchChildAsync(
        DurableDriverContext context,
        StructuredExecutionState execution,
        FiberRecord fiber,
        CompiledInstruction instruction,
        TState state,
        List<DurableOwnedObligationState> ownedObligations,
        StreamVersion currentVersion,
        WaitId? consumedWaitId,
        CancellationToken cancellationToken)
    {
        var commandId = CommandId.New();
        var childInstanceId = DurableChildWorkflowState.DeterministicChildId(
            context.InstanceId,
            commandId,
            0);
        var groupId = childInstanceId.Value.ToString("D");
        execution = BlockOnChildGroup(execution, fiber, groupId);
        AddChildGroupObligation(ref execution, ownedObligations, fiber, groupId, consumedWaitId);
        var command = new DurableRunChildCommand(
            commandId,
            context.InstanceId,
            context.TimeProvider.GetUtcNow(),
            childInstanceId,
            instruction.ChildDefinitionId ?? throw MissingChildMetadata(instruction),
            instruction.ChildDefinitionVersion ?? throw MissingChildMetadata(instruction),
            instruction.ChildFailurePolicy ?? throw MissingChildMetadata(instruction))
        {
            FiberId = fiber.Id,
            ScopeId = fiber.OwningScopeId,
            Envelope = BuildEnvelope(context, execution, state, ownedObligations),
            ExpectedStreamVersion = currentVersion,
            ConsumedResumeWaitIds = ConsumedWaitIds(consumedWaitId)
        };
        var commit = await context.Processor.ProcessAsync(command, cancellationToken).ConfigureAwait(false);
        return new ChildInstructionTransition(execution, commit);
    }

    private async Task<ChildInstructionTransition> DispatchChildrenAsync(
        DurableDriverContext context,
        StructuredExecutionState execution,
        FiberRecord fiber,
        CompiledInstruction instruction,
        TState state,
        List<DurableOwnedObligationState> ownedObligations,
        StreamVersion currentVersion,
        WaitId? consumedWaitId,
        CancellationToken cancellationToken)
    {
        if (instruction.Operation is not Func<TState, IReadOnlyList<string>> selector)
        {
            throw MissingChildMetadata(instruction);
        }

        var itemSnapshots = selector(state).ToArray();
        if (itemSnapshots.Length == 0)
        {
            return new ChildInstructionTransition(
                MoveTo(execution, fiber, RequiredNext(instruction)),
                Commit: null);
        }

        var commandId = CommandId.New();
        var groupId = commandId.Value.ToString("D");
        execution = BlockOnChildGroup(execution, fiber, groupId);
        AddChildGroupObligation(ref execution, ownedObligations, fiber, groupId, consumedWaitId);
        var command = new DurableRunChildrenCommand(
            commandId,
            context.InstanceId,
            context.TimeProvider.GetUtcNow(),
            instruction.ChildDefinitionId ?? throw MissingChildMetadata(instruction),
            instruction.ChildDefinitionVersion ?? throw MissingChildMetadata(instruction),
            itemSnapshots,
            instruction.ChildFailurePolicy ?? throw MissingChildMetadata(instruction),
            instruction.MaxConcurrency,
            instruction.ChildJoinPolicy ?? throw MissingChildMetadata(instruction),
            instruction.ChildResidualPolicy ?? throw MissingChildMetadata(instruction))
        {
            FiberId = fiber.Id,
            ScopeId = fiber.OwningScopeId,
            Envelope = BuildEnvelope(context, execution, state, ownedObligations),
            ExpectedStreamVersion = currentVersion,
            ConsumedResumeWaitIds = ConsumedWaitIds(consumedWaitId)
        };
        var commit = await context.Processor.ProcessAsync(command, cancellationToken).ConfigureAwait(false);
        return new ChildInstructionTransition(execution, commit);
    }

    private async Task<ChildInstructionTransition> ResumeChildGroupAsync(
        DurableDriverContext context,
        StructuredExecutionState execution,
        FiberRecord fiber,
        CompiledInstruction instruction,
        TState state,
        List<DurableOwnedObligationState> ownedObligations,
        DurableOwnedObligationState obligation,
        StreamVersion currentVersion,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(obligation.ObligationId, out var groupGuid))
        {
            throw new InvalidOperationException(
                $"Child-group obligation '{obligation.ObligationId}' is not a valid identity.");
        }

        var token = EventId.Create(groupGuid.ToString());
        if (!context.Aggregate.ChildState.RecordedParentResumeTokens.Contains(token) ||
            context.Aggregate.ChildState.ConsumedParentResumeTokens.Contains(token))
        {
            throw new InvalidOperationException(
                $"Child-group '{obligation.ObligationId}' resumed without a committed parent token.");
        }

        var consumedChildWaits = context.Aggregate.WaitState.PendingResumes
            .Where(pending =>
                pending.FiberId == fiber.Id &&
                string.Equals(pending.EventName, "ChildCompleted", StringComparison.Ordinal))
            .Select(pending => pending.WaitId)
            .ToArray();
        ownedObligations.Remove(obligation);
        execution = MoveTo(execution, ClearResume(fiber), RequiredNext(instruction));
        var command = new ConsumeParentResumeTokenCommand
        {
            CommandId = CommandId.New(),
            InstanceId = context.InstanceId,
            RequestedAt = context.TimeProvider.GetUtcNow(),
            GroupId = obligation.ObligationId,
            ResumeTokenId = token,
            Envelope = BuildEnvelope(context, execution, state, ownedObligations),
            ConsumedResumeWaitIds = consumedChildWaits,
            ExpectedStreamVersion = currentVersion
        };
        var commit = await context.Processor.ProcessAsync(command, cancellationToken).ConfigureAwait(false);
        return new ChildInstructionTransition(execution, commit);
    }

    private static StructuredExecutionState BlockOnChildGroup(
        StructuredExecutionState execution,
        FiberRecord fiber,
        string groupId)
    {
        var blocked = FiberReducer.Block(
            ClearResume(fiber),
            FiberBlockedReason.ChildGroup,
            groupId);
        return execution with
        {
            Fibers = new Dictionary<FiberId, FiberRecord>(execution.Fibers)
            {
                [fiber.Id] = blocked
            },
            Scheduler = FiberScheduler.RemoveRunnable(execution.Scheduler, [fiber.Id])
        };
    }

    private static void AddChildGroupObligation(
        ref StructuredExecutionState execution,
        List<DurableOwnedObligationState> ownedObligations,
        FiberRecord fiber,
        string groupId,
        WaitId? consumedWaitId)
    {
        RemoveConsumedObligation(ownedObligations, consumedWaitId);
        ownedObligations.Add(new DurableOwnedObligationState
        {
            Kind = DurableOwnedObligationKind.ChildGroup,
            ObligationId = groupId,
            FiberId = fiber.Id.Value,
            ScopeId = fiber.OwningScopeId?.Value,
            RegistrationSequence = AllocateRegistrationSequence(ref execution)
        });
    }

    private static InvalidOperationException MissingChildMetadata(CompiledInstruction instruction)
    {
        return new InvalidOperationException(
            $"Compiled child instruction '{instruction.Path}' is missing required metadata.");
    }

    private sealed record ChildInstructionTransition(
        StructuredExecutionState Execution,
        DurableCommandResult? Commit);
}
