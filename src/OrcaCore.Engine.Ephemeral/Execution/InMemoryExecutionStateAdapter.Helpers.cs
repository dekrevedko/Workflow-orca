using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Execution;
using OrcaCore.Core.Internal;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed partial class InMemoryExecutionStateAdapter<TState>
{
    private static StructuredExecutionState Advance(
        CompiledWorkflowPlan plan,
        StructuredExecutionState state,
        FiberRecord fiber,
        CompiledInstruction instruction)
    {
        var next = instruction.NextInstructionId;
        if (next is null)
        {
            next = plan.GetSequentialSuccessor(instruction.Id);
        }

        return MoveTo(state, fiber, next, instruction);
    }

    private static StructuredExecutionState MoveTo(
        StructuredExecutionState state,
        FiberRecord fiber,
        InstructionId? target,
        CompiledInstruction source)
    {
        if (target is null)
        {
            throw global::OrcaCore.Core.Authoring.PublicAuthoringContracts.DefinitionException(
                $"Instruction '{source.Id}' has no continuation.");
        }

        var fibers = new Dictionary<FiberId, FiberRecord>(state.Fibers)
        {
            [fiber.Id] = fiber with { InstructionId = target.Value }
        };
        return state with { Fibers = fibers };
    }
}

internal sealed class StructuredEphemeralValueCodec : IStructuredValueCodec
{
    public StructuredSerializedValue Serialize(object? value, Type declaredType, string schemaIdentity)
    {
        return new StructuredSerializedValue(
            declaredType,
            schemaIdentity,
                CoreWorkflowValueCodec.Serialize(value, declaredType));
    }

    public object? Deserialize(StructuredSerializedValue value)
    {
            return CoreWorkflowValueCodec.Deserialize(value.Payload, value.DeclaredType);
    }
}
