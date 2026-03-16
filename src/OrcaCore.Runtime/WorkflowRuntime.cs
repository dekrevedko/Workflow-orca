using OrcaCore.Abstractions;

namespace OrcaCore.Runtime;

internal static class WorkflowRuntime
{
    public static async Task ExecuteAsync<TState>(
        WorkflowInstance<TState> instance,
        WorkflowDefinition<TState> definition,
        CorrelationIndex correlationIndex,
        CancellationToken cancellationToken = default,
        EventEnvelope? resumedEvent = null)
    {
        var runtime = instance.RuntimeState;

        while (runtime.Status == WorkflowStatus.Running
               && runtime.ExecutionPointer < definition.Steps.Count)
        {
            var step = definition.Steps[runtime.ExecutionPointer];

            var result = await ExecuteStepAsync(instance, step, correlationIndex, cancellationToken, resumedEvent);
            resumedEvent = null; // consumed by first step

            if (result is null)
            {
                // Ensure workflow is in Waiting state (might already be set by parallel handler)
                if (runtime.Status == WorkflowStatus.Running)
                    InstanceLifecycle.TransitionTo(runtime, WorkflowStatus.Waiting);
                return;
            }

            switch (result)
            {
                case StepResult.Completed:
                    runtime.ExecutionPointer++;
                    break;

                case StepResult.Failed:
                    // Transition already happened in ExecuteStepAsync
                    if (runtime.Status != WorkflowStatus.Failed)
                        InstanceLifecycle.TransitionTo(runtime, WorkflowStatus.Failed);
                    return;

                default:
                    throw new InvalidOperationException(
                        $"Unexpected step result type: {result.GetType().Name}");
            }
        }

        if (runtime.Status == WorkflowStatus.Running)
        {
            InstanceLifecycle.TransitionTo(runtime, WorkflowStatus.Completed);
        }
    }

    /// <summary>
    /// Executes a single step. Returns null if execution was suspended (wait).
    /// </summary>
    private static async Task<StepResult?> ExecuteStepAsync<TState>(
        WorkflowInstance<TState> instance,
        IStep<TState> step,
        CorrelationIndex correlationIndex,
        CancellationToken cancellationToken,
        EventEnvelope? resumedEvent)
    {
        var runtime = instance.RuntimeState;

        // Infrastructure steps handled by interpreter
        switch (step)
        {
            case IfStep<TState> ifStep:
            {
                var branch = ifStep.Condition(instance.BusinessState) ? ifStep.ThenSteps : ifStep.ElseSteps;
                var suspended = await ExecuteStepListAsync(instance, branch, correlationIndex, cancellationToken, resumedEvent);
                return suspended ? null : new StepResult.Completed();
            }

            case ParallelStep<TState> parallelStep:
            {
                return await ExecuteParallelAsync(instance, parallelStep, correlationIndex, cancellationToken);
            }

            case WhileStep<TState> whileStep:
            {
                while (whileStep.Condition(instance.BusinessState))
                {
                    var suspended = await ExecuteStepListAsync(
                        instance, whileStep.BodySteps, correlationIndex, cancellationToken, resumedEvent);
                    resumedEvent = null;
                    if (suspended)
                        return null;
                    if (runtime.Status != WorkflowStatus.Running)
                        return runtime.Status == WorkflowStatus.Failed
                            ? new StepResult.Failed(new InvalidOperationException("Failed in while body"))
                            : null;
                }
                return new StepResult.Completed();
            }
        }

        // Business step
        var context = new StepContext<TState>(
            instance.BusinessState, instance.InstanceId, cancellationToken, resumedEvent);

        StepResult result;
        try
        {
            result = await step.ExecuteAsync(context);
        }
        catch (Exception ex)
        {
            runtime.Error = new WorkflowError(ex, step.StepId, DateTimeOffset.UtcNow);
            InstanceLifecycle.TransitionTo(runtime, WorkflowStatus.Failed);
            return new StepResult.Failed(ex);
        }

        if (result is StepResult.Failed failed)
        {
            runtime.Error = new WorkflowError(failed.Error, step.StepId, DateTimeOffset.UtcNow);
        }

        if (result is StepResult.WaitForEvent wait)
        {
            return HandleWait(instance, wait, correlationIndex);
        }

        if (result is StepResult.Yield)
        {
            return null; // suspend — yield control back to runtime
        }

        return result;
    }

    /// <summary>
    /// Executes a list of steps sequentially. Returns true if suspended.
    /// </summary>
    internal static async Task<bool> ExecuteStepListAsync<TState>(
        WorkflowInstance<TState> instance,
        IReadOnlyList<IStep<TState>> steps,
        CorrelationIndex correlationIndex,
        CancellationToken cancellationToken,
        EventEnvelope? resumedEvent)
    {
        var runtime = instance.RuntimeState;

        // If resuming into a nested structure, skip to the saved position
        var startIndex = runtime.NestedPointers.Count > 0 ? runtime.NestedPointers.Pop() : 0;

        for (var i = startIndex; i < steps.Count; i++)
        {
            if (runtime.Status != WorkflowStatus.Running)
                return runtime.Status == WorkflowStatus.Waiting;

            var result = await ExecuteStepAsync(instance, steps[i], correlationIndex, cancellationToken, resumedEvent);
            resumedEvent = null;

            if (result is null)
            {
                // Suspended — save position so we can resume here
                runtime.NestedPointers.Push(i);
                return true;
            }

            switch (result)
            {
                case StepResult.Completed:
                    break;
                case StepResult.Failed:
                    if (runtime.Status != WorkflowStatus.Failed)
                        InstanceLifecycle.TransitionTo(runtime, WorkflowStatus.Failed);
                    return false;
                default:
                    throw new InvalidOperationException(
                        $"Unexpected step result type: {result.GetType().Name}");
            }
        }

        return false;
    }

    private static StepResult? HandleWait<TState>(
        WorkflowInstance<TState> instance,
        StepResult.WaitForEvent wait,
        CorrelationIndex correlationIndex,
        string? branchId = null)
    {
        var runtime = instance.RuntimeState;
        var waitRecord = new WaitRecord(
            WaitId: Guid.NewGuid().ToString("N"),
            EventName: wait.EventName,
            CorrelationId: wait.CorrelationId,
            BranchId: branchId,
            RegisteredAt: DateTimeOffset.UtcNow,
            Status: WaitStatus.Active);

        // Check mailbox for a buffered event before suspending
        var buffered = runtime.PendingEvents.FirstOrDefault(p =>
            !p.Consumed
            && p.Envelope.EventName == wait.EventName
            && p.Envelope.CorrelationId == wait.CorrelationId);

        if (buffered is not null)
        {
            var idx = runtime.PendingEvents.IndexOf(buffered);
            runtime.PendingEvents[idx] = buffered with { Consumed = true };
            runtime.ConsumedEventIds.Add(buffered.Envelope.EventId);
            runtime.ActiveWaits.Add(waitRecord with { Status = WaitStatus.Matched });
            // Return Completed so caller continues
            return new StepResult.Completed();
        }

        runtime.ActiveWaits.Add(waitRecord);
        correlationIndex.Add(wait.EventName, wait.CorrelationId, instance.InstanceId);
        // Don't transition to Waiting here — caller controls that
        return null; // suspended
    }

    public static async Task ResumeParallelBranchAsync<TState>(
        WorkflowInstance<TState> instance,
        WorkflowDefinition<TState> definition,
        CorrelationIndex correlationIndex,
        string branchId,
        EventEnvelope resumedEvent,
        CancellationToken cancellationToken)
    {
        var runtime = instance.RuntimeState;
        var parallel = runtime.ActiveParallel
            ?? throw new InvalidOperationException("No active parallel execution to resume.");

        // Find the parallel step at the current execution pointer
        var parallelStep = definition.Steps[runtime.ExecutionPointer] as ParallelStep<TState>
            ?? throw new InvalidOperationException("Current step is not a ParallelStep.");

        var branch = parallelStep.Branches.First(b => b.BranchId == branchId);

        // Advance branch pointer past the wait step
        parallel.BranchPointers[branchId]++;
        parallel.BranchStatuses[branchId] = BranchStatus.Running;

        // Continue executing the branch
        var suspended = await ExecuteBranchAsync(
            instance, branch, parallel, correlationIndex, cancellationToken, resumedEvent);

        if (!suspended)
        {
            if (runtime.Status == WorkflowStatus.Failed)
            {
                runtime.ActiveParallel = null;
                return;
            }
            parallel.BranchStatuses[branchId] = BranchStatus.Completed;
        }
        else
        {
            parallel.BranchStatuses[branchId] = BranchStatus.Waiting;
        }

        // Check if all branches completed
        if (parallel.BranchStatuses.Values.All(s => s == BranchStatus.Completed))
        {
            runtime.ActiveParallel = null;
            runtime.ExecutionPointer++;
            // Continue main execution after the parallel block
            await ExecuteAsync(instance, definition, correlationIndex, cancellationToken);
        }
        else if (parallel.BranchStatuses.Values.Any(s => s == BranchStatus.Waiting))
        {
            // Still have waiting branches — go back to Waiting
            if (runtime.Status == WorkflowStatus.Running)
                InstanceLifecycle.TransitionTo(runtime, WorkflowStatus.Waiting);
        }
    }

    private static async Task<StepResult?> ExecuteParallelAsync<TState>(
        WorkflowInstance<TState> instance,
        ParallelStep<TState> parallelStep,
        CorrelationIndex correlationIndex,
        CancellationToken cancellationToken)
    {
        var runtime = instance.RuntimeState;
        var parallel = new ParallelExecutionState();
        runtime.ActiveParallel = parallel;

        // Initialize all branches
        foreach (var branch in parallelStep.Branches)
        {
            parallel.BranchStatuses[branch.BranchId] = BranchStatus.Running;
            parallel.BranchPointers[branch.BranchId] = 0;
        }

        // Execute each branch
        foreach (var branch in parallelStep.Branches)
        {
            if (parallel.BranchStatuses[branch.BranchId] != BranchStatus.Running)
                continue;

            var suspended = await ExecuteBranchAsync(
                instance, branch, parallel, correlationIndex, cancellationToken, resumedEvent: null);

            if (!suspended)
            {
                if (runtime.Status == WorkflowStatus.Failed)
                {
                    runtime.ActiveParallel = null;
                    return new StepResult.Failed(runtime.Error?.Exception
                        ?? new InvalidOperationException("Branch failed"));
                }
                parallel.BranchStatuses[branch.BranchId] = BranchStatus.Completed;
            }
            else
            {
                parallel.BranchStatuses[branch.BranchId] = BranchStatus.Waiting;
            }
        }

        // Check if all branches completed
        if (parallel.BranchStatuses.Values.All(s => s == BranchStatus.Completed))
        {
            runtime.ActiveParallel = null;
            return new StepResult.Completed();
        }

        // Some branches are waiting — suspend the workflow
        InstanceLifecycle.TransitionTo(runtime, WorkflowStatus.Waiting);
        return null;
    }

    internal static async Task<bool> ExecuteBranchAsync<TState>(
        WorkflowInstance<TState> instance,
        ParallelBranch<TState> branch,
        ParallelExecutionState parallel,
        CorrelationIndex correlationIndex,
        CancellationToken cancellationToken,
        EventEnvelope? resumedEvent)
    {
        var runtime = instance.RuntimeState;
        var pointer = parallel.BranchPointers[branch.BranchId];

        for (var i = pointer; i < branch.Steps.Count; i++)
        {
            var step = branch.Steps[i];
            var result = await ExecuteStepInBranchAsync(
                instance, step, branch.BranchId, correlationIndex, cancellationToken, resumedEvent);
            resumedEvent = null;

            if (result is null)
            {
                parallel.BranchPointers[branch.BranchId] = i; // save position
                return true; // suspended
            }

            switch (result)
            {
                case StepResult.Completed:
                    parallel.BranchPointers[branch.BranchId] = i + 1;
                    break;
                case StepResult.Failed:
                    if (runtime.Status != WorkflowStatus.Failed)
                        InstanceLifecycle.TransitionTo(runtime, WorkflowStatus.Failed);
                    return false;
                default:
                    throw new InvalidOperationException(
                        $"Unexpected step result type: {result.GetType().Name}");
            }
        }

        return false; // completed
    }

    private static async Task<StepResult?> ExecuteStepInBranchAsync<TState>(
        WorkflowInstance<TState> instance,
        IStep<TState> step,
        string branchId,
        CorrelationIndex correlationIndex,
        CancellationToken cancellationToken,
        EventEnvelope? resumedEvent)
    {
        var runtime = instance.RuntimeState;

        // Infrastructure steps
        switch (step)
        {
            case IfStep<TState> ifStep:
            {
                var ifBranch = ifStep.Condition(instance.BusinessState) ? ifStep.ThenSteps : ifStep.ElseSteps;
                var suspended = await ExecuteStepListAsync(instance, ifBranch, correlationIndex, cancellationToken, resumedEvent);
                return suspended ? null : new StepResult.Completed();
            }

            case WhileStep<TState> whileStep:
            {
                while (whileStep.Condition(instance.BusinessState))
                {
                    var suspended = await ExecuteStepListAsync(
                        instance, whileStep.BodySteps, correlationIndex, cancellationToken, resumedEvent);
                    resumedEvent = null;
                    if (suspended)
                        return null;
                    if (runtime.Status != WorkflowStatus.Running)
                        return runtime.Status == WorkflowStatus.Failed
                            ? new StepResult.Failed(new InvalidOperationException("Failed in while body"))
                            : null;
                }
                return new StepResult.Completed();
            }
        }

        // Business step
        var context = new StepContext<TState>(
            instance.BusinessState, instance.InstanceId, cancellationToken, resumedEvent);

        StepResult result;
        try
        {
            result = await step.ExecuteAsync(context);
        }
        catch (Exception ex)
        {
            runtime.Error = new WorkflowError(ex, step.StepId, DateTimeOffset.UtcNow);
            InstanceLifecycle.TransitionTo(runtime, WorkflowStatus.Failed);
            return new StepResult.Failed(ex);
        }

        if (result is StepResult.Failed failed)
        {
            runtime.Error = new WorkflowError(failed.Error, step.StepId, DateTimeOffset.UtcNow);
        }

        if (result is StepResult.WaitForEvent wait)
        {
            return HandleWait(instance, wait, correlationIndex, branchId);
        }

        if (result is StepResult.Yield)
        {
            return null; // suspend — yield control back to runtime
        }

        return result;
    }
}
