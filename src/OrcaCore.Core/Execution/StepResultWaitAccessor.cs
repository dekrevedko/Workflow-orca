namespace OrcaCore.Core.Execution;

internal static class StepResultWaitAccessor
{
    internal static bool TryGetWait(
        global::OrcaCore.StepResult result,
        out global::OrcaCore.WorkflowEventContract eventContract,
        out global::OrcaCore.CorrelationId correlationId) =>
        result.TryReadWait(out eventContract, out correlationId);
}
