using ProtocolWaitMode = global::OrcaCore.Abstractions.Durable.WaitMode;
using WorkflowWaitMode = global::OrcaCore.Core.Compilation.WorkflowWaitMode;

namespace OrcaCore.Engine.Durable;

/// <summary>
/// Maps the compiler's non-authored wait-residency hint to its persisted protocol value.
/// The two owners remain separate so Runtime.Protocol never depends on Core.
/// </summary>
internal static class DurableWaitResidency
{
    internal static ProtocolWaitMode ToProtocol(WorkflowWaitMode mode) => mode switch
    {
        WorkflowWaitMode.Resident => ProtocolWaitMode.Resident,
        WorkflowWaitMode.Cold => ProtocolWaitMode.Cold,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown workflow wait residency.")
    };

    internal static WorkflowWaitMode FromProtocol(ProtocolWaitMode mode) => mode switch
    {
        ProtocolWaitMode.Resident => WorkflowWaitMode.Resident,
        ProtocolWaitMode.Cold => WorkflowWaitMode.Cold,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown protocol wait residency.")
    };
}
