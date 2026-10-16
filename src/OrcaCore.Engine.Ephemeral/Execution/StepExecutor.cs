using System.Diagnostics;
using OrcaCore.Abstractions.Diagnostics;
using OrcaCore.Core.Definitions;
using OrcaCore.Core.Execution;
using OrcaCore.Engine.Ephemeral.Diagnostics;
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
        ForEachItemContext? forEachItem,
        CancellationToken cancellationToken,
        bool deferFailures)
    {
        await using var governanceLease = await governance
            .EnterStepAsync(stepNode.StepType, stepNode.Policies.PoolKey, cancellationToken)
            .ConfigureAwait(false);
        using var stateAccess = await instance.EnterStateAccessAsync(cancellationToken).ConfigureAwait(false);
        var timedOut = 0;
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var timeoutTimer = stepNode.Policies.Timeout is { } timeout
            ? timeProvider.CreateTimer(
                _ =>
                {
                    Interlocked.Exchange(ref timedOut, 1);
                    timeoutCancellation.Cancel();
                },
                null,
                timeout.Duration,
                Timeout.InfiniteTimeSpan)
            : null;
        var executionToken = timeoutTimer is null
            ? cancellationToken
            : timeoutCancellation.Token;
        var maxAttempts = stepNode.Policies.Retry?.MaxAttempts ?? 1;
        var operationId = instance.BeginStepOperation(stepPath);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var stepStartedAt = timeProvider.GetUtcNow();
            using var activity = OrcaCoreEphemeralDiagnostics.StartStep(instance.DefinitionId, stepPath);
            instance.StartStep(stepPath, stepStartedAt, stepNode.Policies.Timeout?.Duration);
            using var stuckTimer = stuckStepThreshold is { } threshold
                ? timeProvider.CreateTimer(
                    _ => instance.MarkStuckStep(stepPath, timeProvider.GetUtcNow()),
                    null,
                    threshold,
                    Timeout.InfiniteTimeSpan)
                : null;
            try
            {
                var step = stepNode.StepFactory();
                var context = StepContextContracts.Create(
                    instance.State,
                    StepContextContracts.CreateExecution(instance.InstanceId, operationId, attempt),
                    resumedEvent,
                    timeProvider,
                    forEachItem);
                var result = await step.ExecuteAsync(context, executionToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                var stepCompletedAt = timeProvider.GetUtcNow();
                instance.CompleteStep(stepPath, stepCompletedAt);
                var errorKind = result is StepResult.Failed failed ? failed.Error.GetType().Name : null;
                OrcaCoreEphemeralDiagnostics.RecordStep(
                    instance.DefinitionId,
                    stepPath,
                    stepCompletedAt - stepStartedAt,
                    errorKind);
                if (errorKind is not null)
                {
                    activity?.SetTag(OrcaCoreDiagnostics.ErrorKindKey, errorKind);
                    activity?.SetStatus(ActivityStatusCode.Error, errorKind);
                }
                RecordStuckStepIfNeeded(instance, stepPath, stepStartedAt);
                if (result is StepResult.Failed && attempt < maxAttempts)
                {
                    if (stepNode.Policies.Retry is { } retry && retry.Backoff > TimeSpan.Zero)
                    {
                        await Task.Delay(retry.Backoff, timeProvider, executionToken).ConfigureAwait(false);
                    }

                    continue;
                }

                if (result is not StepResult.Failed)
                {
                    instance.ReplaceState(context.State);
                }

                return ApplyResult(instance, result, stepPath, deferFailures);
            }
            catch (OperationCanceledException) when (
                Volatile.Read(ref timedOut) == 1 &&
                !cancellationToken.IsCancellationRequested)
            {
                var stepCompletedAt = timeProvider.GetUtcNow();
                instance.CompleteStep(stepPath, stepCompletedAt);
                var timeoutException = new TimeoutException(
                    $"Step '{stepPath}' timed out after {stepNode.Policies.Timeout!.Duration}.");
                OrcaCoreEphemeralDiagnostics.RecordStep(
                    instance.DefinitionId,
                    stepPath,
                    stepCompletedAt - stepStartedAt,
                    timeoutException.GetType().Name);
                activity?.SetStatus(ActivityStatusCode.Error, timeoutException.Message);
                activity?.AddException(timeoutException);
                if (deferFailures)
                {
                    return StepExecutionResult.Failed(timeoutException);
                }

                Fail(instance, timeoutException, stepPath);
                return StepExecutionResult.Stop();
            }
            catch (Exception exception) when (exception is not OperationCanceledException and not NotSupportedException)
            {
                var stepCompletedAt = timeProvider.GetUtcNow();
                instance.CompleteStep(stepPath, stepCompletedAt);
                OrcaCoreEphemeralDiagnostics.RecordStep(
                    instance.DefinitionId,
                    stepPath,
                    stepCompletedAt - stepStartedAt,
                    exception.GetType().Name);
                activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
                activity?.AddException(exception);
                RecordStuckStepIfNeeded(instance, stepPath, stepStartedAt);
                if (attempt < maxAttempts)
                {
                    if (stepNode.Policies.Retry is { } retry && retry.Backoff > TimeSpan.Zero)
                    {
                        await Task.Delay(retry.Backoff, timeProvider, executionToken).ConfigureAwait(false);
                    }

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
                instance.RecordLifecycleEvent("StepCompleted", stepPath, global::OrcaCore.WorkflowInstanceStatus.Running, timeProvider.GetUtcNow());
                return StepExecutionResult.Continue();
            case StepResult.Failed failed:
                if (deferFailures)
                {
                    return StepExecutionResult.Failed(failed.Error);
                }

                Fail(instance, failed.Error, stepPath);
                return StepExecutionResult.Stop();
            default:
                if (StepResultWaitAccessor.TryGetWait(result, out var eventContract, out var correlationId))
                {
                    return StepExecutionResult.Wait(eventContract, correlationId);
                }

                throw new NotSupportedException(
                    $"Step result '{result.GetType().Name}' is not supported by the ephemeral engine.");
        }
    }

    private void Fail(WorkflowInstance<TState> instance, Exception exception, string stepPath)
    {
        var occurredAt = timeProvider.GetUtcNow();
        instance.RecordLifecycleEvent("StepFailed", stepPath, global::OrcaCore.WorkflowInstanceStatus.Failed, occurredAt);
        instance.Fail(new WorkflowErrorDetails(
            exception.GetType().Name,
            exception.Message,
            stepPath,
            occurredAt));
    }
}
