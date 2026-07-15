using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;

namespace OrcaCore.Core.Compilation;

internal static partial class DefinitionCompiler
{
    private static CompiledInstruction AddInstruction(
        List<CompiledInstruction> instructions,
        CompiledInstructionKind kind,
        string path,
        Delegate? operation = null,
        string? eventName = null,
        WaitMode? waitMode = null,
        TimeSpan? delayDuration = null,
        DefinitionId? childDefinitionId = null,
        DefinitionVersion? childDefinitionVersion = null,
        RunChildFailurePolicy? childFailurePolicy = null,
        int? maxConcurrency = null,
        RunChildrenJoinPolicy? childJoinPolicy = null,
        RunChildrenResidualPolicy? childResidualPolicy = null,
        CompiledPolicyPlan? policy = null)
    {
        var instruction = new CompiledInstruction(
            new InstructionId($"instruction:{path}:{kind}"),
            kind,
            path,
            policy ?? CompiledPolicyPlan.Empty)
        {
            Operation = operation,
            EventName = eventName,
            WaitMode = waitMode,
            DelayDuration = delayDuration,
            ChildDefinitionId = childDefinitionId,
            ChildDefinitionVersion = childDefinitionVersion,
            ChildFailurePolicy = childFailurePolicy,
            MaxConcurrency = maxConcurrency,
            ChildJoinPolicy = childJoinPolicy,
            ChildResidualPolicy = childResidualPolicy
        };
        instructions.Add(instruction);
        return instruction;
    }
}
