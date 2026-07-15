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

    private static StructuredExecutionState ExecuteConditionInstruction(
        StructuredExecutionState execution,
        FiberRecord fiber,
        CompiledInstruction instruction,
        TState state)
    {
        if (instruction.Operation is not Func<TState, bool> condition)
        {
            throw new WorkflowDefinitionException(
                $"Compiled condition '{instruction.Path}' has no typed binding.");
        }

        return MoveTo(
            execution,
            fiber,
            condition(state)
                ? RequiredNext(instruction)
                : instruction.AlternateInstructionId ??
                    throw new WorkflowDefinitionException(
                        $"Condition '{instruction.Path}' has no alternate target."));
    }

    private static StructuredExecutionState AdvanceStructuralInstruction(
        StructuredExecutionState execution,
        FiberRecord fiber,
        CompiledInstruction instruction)
    {
        return MoveTo(execution, fiber, RequiredNext(instruction));
    }
}
