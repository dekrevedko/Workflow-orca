
namespace OrcaCore.Core.Compilation;

internal static partial class DefinitionCompiler
{
    private static CompiledInstruction AddInstruction(
        List<CompiledInstruction> instructions,
        CompiledInstructionKind kind,
        string path,
        Delegate? operation = null,
        global::OrcaCore.WorkflowEventContract? eventContract = null,
        WaitMode? waitMode = null,
        TimeSpan? delayDuration = null,
        int? maxConcurrency = null,
        CompiledPolicyPlan? policy = null,
        Type? stepType = null,
        Type? outputType = null,
        string? outputSchemaIdentity = null,
        Delegate? outputSelector = null,
        string? fixedOutcomeName = null,
        TimeSpan? waitTimeout = null,
        Delegate? publishCorrelationSelector = null,
        Type? publishPayloadType = null,
        string? publishPayloadSchemaIdentity = null,
        Delegate? publishPayloadSelector = null,
        global::OrcaCore.ResourceLeaseRequest? staticLeaseRequest = null,
        Delegate? leaseRequestSelector = null)
    {
        var instruction = new CompiledInstruction(
            new InstructionId($"instruction:{path}:{kind}"),
            kind,
            path,
            policy ?? CompiledPolicyPlan.Empty)
        {
            Operation = operation,
            StepType = stepType,
            OutputType = outputType,
            OutputSchemaIdentity = outputSchemaIdentity,
            OutputSelector = outputSelector,
            FixedOutcomeName = fixedOutcomeName,
            EventContract = eventContract,
            WaitMode = waitMode,
            WaitTimeout = waitTimeout,
            PublishCorrelationSelector = publishCorrelationSelector,
            PublishPayloadType = publishPayloadType,
            PublishPayloadSchemaIdentity = publishPayloadSchemaIdentity,
            PublishPayloadSelector = publishPayloadSelector,
            StaticLeaseRequest = staticLeaseRequest,
            LeaseRequestSelector = leaseRequestSelector,
            DelayDuration = delayDuration,
            MaxConcurrency = maxConcurrency
        };
        instructions.Add(instruction);
        return instruction;
    }
}
