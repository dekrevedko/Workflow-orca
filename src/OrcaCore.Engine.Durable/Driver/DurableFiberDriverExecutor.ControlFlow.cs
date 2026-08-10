using OrcaCore.Abstractions.Errors;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Execution;

namespace OrcaCore.Engine.Durable.Driver;

internal sealed partial class DurableFiberDriverExecutor<TState>
{
    private (StructuredExecutionState Execution, TState State) ExecuteInitInstruction(
        DurableDriverContext context,
        StructuredExecutionState execution,
        FiberRecord fiber,
        CompiledInstruction instruction)
    {
        return (
            MoveTo(execution, fiber, RequiredNext(instruction)),
            Initialize(context));
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
