using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Definitions;
using OrcaCore.Core.Execution;
using OrcaCore.Core.Internal;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;

namespace OrcaCore.Engine.Durable.Driver;

internal sealed partial class DurableFiberDriverExecutor<TState>
{
    private async ValueTask<InitializationResult> ExecuteInitInstructionAsync(
        DurableDriverContext context,
        StructuredExecutionState execution,
        FiberRecord fiber,
        CompiledInstruction instruction,
        List<DurableOwnedObligationState> ownedObligations,
        StreamVersion currentVersion,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (DeadlineExpired())
        {
            return await TimeoutBeforeStateAsync().ConfigureAwait(false);
        }

        object? input = null;
        if (context.Aggregate.StartInputPayload is { } payload &&
            context.Aggregate.StartInputContentType is { } contentType)
        {
            var inputType = instruction.InputType ??
                throw new InvalidOperationException($"Init at '{instruction.Path}' has no input type.");
            input = context.Serializer.Deserialize(new SerializedPayload(contentType, payload), inputType);
            cancellationToken.ThrowIfCancellationRequested();
            if (DeadlineExpired())
            {
                return await TimeoutBeforeStateAsync().ConfigureAwait(false);
            }
        }

        TState state = default!;
        Exception? failure = null;
        try
        {
            state = (TState)CoreWorkflowValueCodec.Detach(instruction.CreateInitialState<TState>(input), typeof(TState))!;
        }
        catch (Exception error) when (error is not (OutOfMemoryException or StackOverflowException or AccessViolationException))
        {
            failure = error;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var now = context.TimeProvider.GetUtcNow();
        if (execution.WorkflowDeadline <= now)
        {
            return await TimeoutBeforeStateAsync().ConfigureAwait(false);
        }
        if (failure is null)
        {
            return new InitializationResult(MoveTo(execution, fiber, RequiredNext(instruction)), state, ownedObligations, null);
        }

        var failed = await context.Processor.ProcessAsync(
            new DurableFailCommand(CommandId.New(), context.InstanceId, now,
                $"WF-RUNTIME-FAILED: Init at '{instruction.Path}' failed: {failure.Message}")
            {
                ExpectedStreamVersion = currentVersion
            }, cancellationToken).ConfigureAwait(false);
        return new InitializationResult(null, default!, ownedObligations,
            failed.Outcome == DurableCommandOutcome.Committed ? DurableSegmentResult.Terminal : Conflict(failed));

        bool DeadlineExpired() => execution.WorkflowDeadline is { } deadline && deadline <= context.TimeProvider.GetUtcNow();

        async ValueTask<InitializationResult> TimeoutBeforeStateAsync()
        {
            var timedOut = await TimeoutWorkflowAsync(context, execution.WorkflowDeadline!.Value, currentVersion, envelope: null,
                preserveOwnership: false).ConfigureAwait(false);
            return new InitializationResult(null, default!, ownedObligations, timedOut);
        }
    }

    private StructuredExecutionState ExecuteConditionInstruction(
        StructuredExecutionState execution,
        FiberRecord fiber,
        CompiledInstruction instruction,
        TState state)
    {
        if (instruction.Operation is not { } condition)
        {
            throw global::OrcaCore.Engine.Durable.Internal.DurableContractAdapter.DefinitionException(
                $"Compiled condition '{instruction.Path}' has no typed binding.");
        }

        var conditionState = ResolveFiberState(execution, fiber, state);
        var matched = StructuredInvocationCache.Invoke(condition, conditionState) as bool? ??
            throw global::OrcaCore.Engine.Durable.Internal.DurableContractAdapter.DefinitionException(
                $"Compiled condition '{instruction.Path}' did not return a Boolean value.");
        return MoveTo(
            execution,
            fiber,
            matched
                ? RequiredNext(instruction)
                : instruction.AlternateInstructionId ??
                    throw global::OrcaCore.Engine.Durable.Internal.DurableContractAdapter.DefinitionException(
                        $"Condition '{instruction.Path}' has no alternate target."));
    }

    private static StructuredExecutionState AdvanceStructuralInstruction(
        StructuredExecutionState execution,
        FiberRecord fiber,
        CompiledInstruction instruction)
    {
        if (instruction.Kind == CompiledInstructionKind.LoopBack)
        {
            fiber = fiber with { LoopIteration = checked(fiber.LoopIteration + 1) };
        }

        return MoveTo(execution, fiber, RequiredNext(instruction));
    }
}
