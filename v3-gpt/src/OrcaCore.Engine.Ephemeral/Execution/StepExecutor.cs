using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Definitions;
using OrcaCore.Core.Lifecycle;
using OrcaCore.Engine.Ephemeral.Governance;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed class StepExecutor<TState>
{
    private readonly TimeProvider timeProvider;
    private readonly ResourceGovernanceCoordinator governance;
    private readonly TimeSpan? stuckStepThreshold;

    internal StepExecutor(
        TimeProvider timeProvider,
        ResourceGovernanceCoordinator governance,
        TimeSpan? stuckStepThreshold)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(governance);

        this.timeProvider = timeProvider;
        this.governance = governance;
        this.stuckStepThreshold = stuckStepThreshold;
    }

    internal async Task<StepExecutionResult> ExecuteAsync(
        WorkflowInstance<TState> instance,
        BusinessStepNode<TState> stepNode,
        string stepPath,
        EventEnvelope? resumedEvent,
        CancellationToken cancellationToken,
        bool deferFailures)
    {
        var timedOut = false;
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var timeoutTimer = stepNode.Policies.Timeout is { } timeout
            ? timeProvider.CreateTimer(
                _ =>
                {
                    timedOut = true;
                    timeoutCancellation.Cancel();
                },
                null,
                timeout.Duration,
                Timeout.InfiniteTimeSpan)
            : null;
        var executionToken = timeoutTimer is null
            ? cancellationToken
            : timeoutCancellation.Token;
        await using var governanceLease = await governance
            .EnterStepAsync(stepNode.Policies.PoolKey, executionToken)
            .ConfigureAwait(false);
        var maxAttempts = stepNode.Policies.Retry?.MaxAttempts ?? 1;
        var step = stepNode.StepFactory();

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var stepStartedAt = timeProvider.GetUtcNow();
            instance.StartStep(stepPath, stepStartedAt, stepNode.Policies.Timeout?.Duration);
            try
            {
                var context = new StepContext<TState>(instance.State, resumedEvent, timeProvider);
                var result = await step.ExecuteAsync(context, executionToken).ConfigureAwait(false);
                instance.CompleteStep(stepPath, timeProvider.GetUtcNow());
                RecordStuckStepIfNeeded(instance, stepPath, stepStartedAt);
                if (result is StepResult.Failed && attempt < maxAttempts)
                {
                    continue;
                }

                return ApplyResult(instance, result, stepPath, deferFailures);
            }
            catch (OperationCanceledException) when (timedOut && !cancellationToken.IsCancellationRequested)
            {
                instance.CompleteStep(stepPath, timeProvider.GetUtcNow());
                var timeoutException = new TimeoutException(
                    $"Step '{stepPath}' timed out after {stepNode.Policies.Timeout!.Duration}.");
                if (deferFailures)
                {
                    return StepExecutionResult.Failed(timeoutException);
                }

                Fail(instance, timeoutException, stepPath);
                return StepExecutionResult.Stop();
            }
            catch (Exception exception) when (exception is not OperationCanceledException and not NotSupportedException)
            {
                instance.CompleteStep(stepPath, timeProvider.GetUtcNow());
                RecordStuckStepIfNeeded(instance, stepPath, stepStartedAt);
                if (attempt < maxAttempts)
                {
                    continue;
                }

                if (deferFailures)
                {
                    return StepExecutionResult.Failed(exception);
                }

                Fail(instance, exception, stepPath);
                return StepExecutionResult.Stop();
            }
        }

        return StepExecutionResult.Stop();
    }

    private void RecordStuckStepIfNeeded(
        WorkflowInstance<TState> instance,
        string stepPath,
        DateTimeOffset stepStartedAt)
    {
        if (stuckStepThreshold is not { } threshold)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        if (now - stepStartedAt > threshold)
        {
            instance.MarkStuckStep(stepPath, now);
        }
    }

    private StepExecutionResult ApplyResult(
        WorkflowInstance<TState> instance,
        StepResult result,
        string stepPath,
        bool deferFailures)
    {
        switch (result)
        {
            case StepResult.Completed:
                instance.RecordLifecycleEvent("StepCompleted", stepPath, WorkflowStatus.Running, timeProvider.GetUtcNow());
                return StepExecutionResult.Continue();
            case StepResult.Failed failed:
                if (deferFailures)
                {
                    return StepExecutionResult.Failed(failed.Error);
                }

                Fail(instance, failed.Error, stepPath);
                return StepExecutionResult.Stop();
            case StepResult.WaitForEvent wait:
                return StepExecutionResult.Wait(wait.EventName, wait.CorrelationId);
            case StepResult.Yield:
                return StepExecutionResult.Yield();
            default:
                throw new NotSupportedException($"Step result '{result.GetType().Name}' is not supported.");
        }
    }

    private void Fail(WorkflowInstance<TState> instance, Exception exception, string stepPath)
    {
        var occurredAt = timeProvider.GetUtcNow();
        instance.RecordLifecycleEvent("StepFailed", stepPath, WorkflowStatus.Failed, occurredAt);
        FireOrThrow(instance, LifecycleTrigger.Fail);
        instance.Fail(new WorkflowErrorDetails(
            exception.GetType().Name,
            exception.Message,
            stepPath,
            occurredAt));
    }

    private static void FireOrThrow(WorkflowInstance<TState> instance, LifecycleTrigger trigger)
    {
        var result = LifecycleMachine.Fire(instance.Status, trigger);
        if (result.IsFailure)
        {
            throw result.Error;
        }
    }
}
