using OrcaCore.Abstractions.Instances;
using OrcaCore.Core.Compilation;

namespace OrcaCore.Core.Execution;

internal sealed record StructuredRuntimeDiagnostic(string Code, string Message);

internal sealed record StructuredExecutionFailure(string Code, string Message);

internal sealed record StructuredExecutionStatus(
    WorkflowStatus? Status,
    StructuredExecutionFailure? Failure);

internal static class ExecutionStatusDeriver
{
    internal static StructuredExecutionStatus Derive(
        WorkflowExecutionMode mode,
        StructuredExecutionState state,
        StructuredRuntimeDiagnostic? blockingDiagnostic = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (blockingDiagnostic is not null)
        {
            return ReportModeFailure(mode, blockingDiagnostic.Code, blockingDiagnostic.Message);
        }

        if (!state.Fibers.TryGetValue(state.RootFiberId, out var root))
        {
            return ReportModeFailure(
                mode,
                "SFE-RUN-STATE-001",
                $"Root fiber '{state.RootFiberId}' is missing.");
        }

        var terminal = root.Phase switch
        {
            FiberPhase.Completed => WorkflowStatus.Completed,
            FiberPhase.Failed => WorkflowStatus.Failed,
            FiberPhase.Cancelled => WorkflowStatus.Cancelled,
            _ => (WorkflowStatus?)null
        };
        if (terminal is not null)
        {
            return new StructuredExecutionStatus(terminal, null);
        }

        if (state.Scopes.Values.Any(scope =>
            scope.Phase is ExecutionScopePhase.Joinable or ExecutionScopePhase.Merging))
        {
            return new StructuredExecutionStatus(WorkflowStatus.Running, null);
        }

        var runnable = state.Fibers.Values
            .Where(fiber => fiber.Phase == FiberPhase.Runnable)
            .Select(fiber => fiber.Id)
            .ToHashSet();
        var scheduled = state.Scheduler.RunnableFiberIds.ToHashSet();
        FiberId? expectedNext = state.Scheduler.RunnableFiberIds.Count == 0
            ? null
            : state.Scheduler.RunnableFiberIds[0];
        if (!runnable.SetEquals(scheduled) ||
            state.Scheduler.NextFiberId != expectedNext)
        {
            return ReportModeFailure(
                mode,
                "SFE-RUN-STATE-002",
                "Runnable fiber membership and the persisted scheduler queue disagree.");
        }

        if (runnable.Count > 0)
        {
            return new StructuredExecutionStatus(WorkflowStatus.Running, null);
        }

        var nonterminal = state.Fibers.Values
            .Where(fiber => fiber.Phase is FiberPhase.Runnable or FiberPhase.Blocked)
            .ToArray();
        if (nonterminal.Length > 0 && nonterminal.All(fiber => fiber.Phase == FiberPhase.Blocked))
        {
            return new StructuredExecutionStatus(WorkflowStatus.Waiting, null);
        }

        return ReportModeFailure(
            mode,
            "SFE-RUN-STATE-003",
            "Execution has no runnable or blocked fiber and the root is not terminal.");
    }

    private static StructuredExecutionStatus ReportModeFailure(
        WorkflowExecutionMode mode,
        string code,
        string message)
    {
        return mode == WorkflowExecutionMode.Durable
            ? new StructuredExecutionStatus(WorkflowStatus.Parked, null)
            : new StructuredExecutionStatus(null, new StructuredExecutionFailure(code, message));
    }
}
