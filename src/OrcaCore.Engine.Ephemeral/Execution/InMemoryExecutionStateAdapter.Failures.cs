using System.Reflection;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Execution;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed partial class InMemoryExecutionStateAdapter<TState>
{
    private void FailExecutionBoundary(
        StructuredExecutionState execution,
        FiberRecord fiber,
        CompiledInstruction instruction,
        WorkflowInstance<TState> instance,
        Exception exception)
    {
        var failure = CreateFiberFailure(execution, fiber, instruction, exception);
        activeExecution = FailFiberAndAncestors(execution, fiber, failure);
        CancelAllStructuredWaits();
        FailInstance(instance, failure, instruction.Path);
    }

    private StructuredExecutionState FailMergeBoundary(
        StructuredExecutionState execution,
        ExecutionScopeRecord scope,
        WorkflowInstance<TState> instance,
        Exception exception)
    {
        var scopePlan = FailurePlan.GetScope(scope.ScopePlanId);
        var mergeInstruction = FailurePlan.GetInstruction(scopePlan.JoinInstructionId);
        var parent = execution.Fibers[scope.ParentFiberId];
        var failure = CreateFiberFailure(execution, parent, mergeInstruction, exception);
        var currentScope = execution.Scopes[scope.Id];
        if (currentScope.Phase is not (
                ExecutionScopePhase.Completed or
                ExecutionScopePhase.Failed or
                ExecutionScopePhase.Cancelled))
        {
            var scopes = new Dictionary<ScopeId, ExecutionScopeRecord>(execution.Scopes)
            {
                [scope.Id] = ScopeReducer.Transition(currentScope, ExecutionScopePhase.Failed)
            };
            execution = execution with { Scopes = scopes };
        }

        execution = FailFiberAndAncestors(
            execution,
            execution.Fibers[scope.ParentFiberId],
            failure);
        activeExecution = execution;
        CancelAllStructuredWaits();
        FailInstance(instance, failure, $"{scope.ScopePlanId.Value}:merge");
        return execution;
    }

    private static StructuredExecutionState FailFiberAndAncestors(
        StructuredExecutionState execution,
        FiberRecord failedFiber,
        FiberFailure failure)
    {
        var current = failedFiber;
        while (current.OwningScopeId is { } scopeId)
        {
            execution = ScopeReducer.RecordChildTerminals(
                execution,
                scopeId,
                [ChildTerminalOutcome.Failed(current.Id, failure)]).State;
            var scope = execution.Scopes[scopeId];
            if (scope.Phase != ExecutionScopePhase.Failed)
            {
                return execution;
            }

            current = execution.Fibers[scope.ParentFiberId];
        }

        if (current.Phase is not (FiberPhase.Runnable or FiberPhase.Blocked))
        {
            return execution;
        }

        var fibers = new Dictionary<FiberId, FiberRecord>(execution.Fibers)
        {
            [current.Id] = FiberReducer.Fail(current, failure)
        };
        return execution with
        {
            Fibers = fibers,
            Scheduler = FiberScheduler.RemoveRunnable(execution.Scheduler, [current.Id])
        };
    }

    private void HandleBackgroundResumeFailure(
        WorkflowInstance<TState> instance,
        FiberId fiberId,
        string boundary,
        Exception exception)
    {
        if (instance.Status is not (LegacyWorkflowStatus.Running or LegacyWorkflowStatus.Waiting))
        {
            return;
        }

        var execution = activeExecution;
        var failure = execution is not null &&
                      execution.Fibers.TryGetValue(fiberId, out var candidate)
            ? CreateFiberFailure(
                execution,
                candidate,
                FailurePlan.GetInstruction(candidate.InstructionId),
                exception)
            : new FiberFailure(
                "WF-STEP-UNHANDLED",
                exception.Message,
                authoredLocation: FailureProvenance.LocationFromCompilerPath(boundary));
        try
        {
            if (execution is not null &&
                execution.Fibers.TryGetValue(fiberId, out var fiber) &&
                fiber.Phase is FiberPhase.Runnable or FiberPhase.Blocked)
            {
                activeExecution = FailFiberAndAncestors(execution, fiber, failure);
            }
        }
        catch (Exception)
        {
            // This is the final fire-and-forget boundary. Preserve the original failure and
            // still make the instance terminal if partially mutated execution cannot reduce.
        }

        try
        {
            CancelAllStructuredWaits();
        }
        catch (Exception)
        {
            // WorkflowInstance.Fail below also clears instance-owned runtime work.
        }

        try
        {
            FailInstance(instance, failure, boundary);
        }
        catch (WorkflowLifecycleException)
        {
            // A racing terminal transition already owns the observable lifecycle result.
        }

        try
        {
            onSnapshotCommitted(instance.ToSnapshot());
        }
        catch (Exception)
        {
            // Snapshot publication must not fault a detached continuation after failure.
        }
    }

    private void FailInstance(
        WorkflowInstance<TState> instance,
        FiberFailure failure,
        string path)
    {
        if (instance.Status is not (LegacyWorkflowStatus.Running or LegacyWorkflowStatus.Waiting))
        {
            return;
        }

        instance.Fail(new WorkflowErrorDetails(
            failure.Code,
            failure.Message,
            path,
            timeProvider.GetUtcNow()));
    }

    private FiberFailure CreateFiberFailure(
        StructuredExecutionState execution,
        FiberRecord fiber,
        CompiledInstruction instruction,
        Exception exception)
    {
        var failure = exception is TargetInvocationException { InnerException: { } inner }
            ? inner
            : exception;
        if (failure is StructuredExecutionLimitException limit)
        {
            return FailureProvenance.Create(
                FailurePlan,
                execution,
                fiber,
                instruction,
                limit.Code,
                limit.Message);
        }

        return failure is OrcaCoreException known
            ? FailureProvenance.Create(
                FailurePlan,
                execution,
                fiber,
                instruction,
                known.Code,
                known.Message)
            : FailureProvenance.Create(
                FailurePlan,
                execution,
                fiber,
                instruction,
                "WF-STEP-UNHANDLED",
                failure.Message);
    }

    private CompiledWorkflowPlan FailurePlan => activePlan ??
        throw new InvalidOperationException("The structured execution plan is not initialized.");
}
