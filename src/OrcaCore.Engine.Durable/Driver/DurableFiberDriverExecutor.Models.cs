using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Execution;
using OrcaCore.Core.Internal;

namespace OrcaCore.Engine.Durable.Driver;

internal sealed partial class DurableFiberDriverExecutor<TState>
{
    private sealed class DurableStructuredValueCodec : IStructuredValueCodec
    {
        public StructuredSerializedValue Serialize(
            object? value,
            Type declaredType,
            string schemaIdentity)
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

    private sealed record ExecutedStep(
        StructuredExecutionState Execution,
        FiberRecord Fiber,
        StepResult Result,
        TState State);

    private sealed record BranchTerminalTransition(
        StructuredExecutionState State,
        ScopeId ScopeId,
        bool ScopeBecameJoinable);

    private sealed record NoRunnableResolution(
        StructuredExecutionState Execution,
        TState State,
        StreamVersion StreamVersion,
        int Commands,
        DurableSegmentResult? Result = null);

    private readonly record struct StepThrottleOwner(
        InstanceId InstanceId,
        FiberId FiberId);
}
