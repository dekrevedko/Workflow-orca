using System.Diagnostics;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Core.Execution;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;

namespace OrcaCore.Engine.Durable.Driver;

internal sealed partial class DurableFiberDriverExecutor<TState>
{
    private async Task<NoRunnableResolution> ResolveNoRunnableFiberAsync(
        DurableDriverContext context,
        StructuredExecutionState execution,
        TState state,
        List<DurableOwnedObligationState> ownedObligations,
        StreamVersion currentVersion,
        int commands,
        Stopwatch elapsed,
        FiberQuantumBudget quantumBudget,
        CancellationToken cancellationToken)
    {
        var joinable = execution.Scopes.Values
            .Where(scope => scope.Phase == ExecutionScopePhase.Joinable)
            .OrderBy(scope => scope.Id.Value, StringComparer.Ordinal)
            .FirstOrDefault();
        if (joinable is null)
        {
            var failedScope = execution.Scopes.Values
                .Where(scope => scope.Phase == ExecutionScopePhase.Failed)
                .OrderBy(scope => scope.Id.Value, StringComparer.Ordinal)
                .FirstOrDefault();
            if (failedScope is null)
            {
                if (TryFindBlockedStepThrottle(execution, out var blockedFiber, out var blockedInstruction))
                {
                    return await ReadmitBlockedStepThrottleAsync(
                        context,
                        execution,
                        state,
                        ownedObligations,
                        currentVersion,
                        commands,
                        blockedFiber,
                        blockedInstruction,
                        cancellationToken).ConfigureAwait(false);
                }

                return new NoRunnableResolution(
                    execution,
                    state,
                    currentVersion,
                    commands,
                    DurableSegmentResult.Suspended);
            }

            var childFailures = failedScope.ChildFiberIds
                .Select(childId => execution.Fibers[childId].Failure)
                .Where(failure => failure is not null)
                .Cast<FiberFailure>()
                .ToArray();
            var provenance = FailureProvenance.ForScope(plan, execution, failedScope);
            var joinFailure = ScopeReducer.AggregateFailures(
                childFailures,
                provenance.Location,
                provenance.Occurrence);
            execution = FailFiberAndAncestors(
                execution,
                execution.Fibers[failedScope.ParentFiberId],
                joinFailure);
            var cleanup = RemoveTerminalFiberObligations(
                execution,
                ownedObligations);
            var failedJoin = await context.Processor.ProcessAsync(
                new DurableStepFailedCommand(
                    CommandId.New(),
                    context.InstanceId,
                    context.TimeProvider.GetUtcNow(),
                    $"{failedScope.ScopePlanId.Value}:join",
                    $"{joinFailure.Code}: {joinFailure.Message}",
                    BuildEnvelope(context, execution, state, ownedObligations))
                {
                    ExpectedStreamVersion = currentVersion,
                    CancelWaitIds = cleanup.WaitIds,
                    CancelTimerIds = cleanup.TimerIds,
                    TerminalFiberIds = cleanup.TerminalFiberIds,
                    FailedSagaScopeIds = FailedSagaScopes(execution),
                    CoversRootSagaEligibility = RootFailed(execution)
                },
                cancellationToken).ConfigureAwait(false);
            return new NoRunnableResolution(
                execution,
                state,
                currentVersion,
                commands,
                failedJoin.Outcome == DurableCommandOutcome.Committed
                    ? DurableSegmentResult.Terminal
                    : Conflict(failedJoin));
        }

        try
        {
            (execution, state) = MergeAndResume(execution, joinable, state);
            quantumBudget.EndTurn();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var provenance = FailureProvenance.ForScope(plan, execution, joinable);
            var failure = new FiberFailure(
                "WF-STEP-UNHANDLED",
                exception.Message,
                authoredLocation: provenance.Location,
                occurrence: provenance.Occurrence);
            var scopes = new Dictionary<ScopeId, ExecutionScopeRecord>(execution.Scopes)
            {
                [joinable.Id] = ScopeReducer.Transition(
                    joinable,
                    ExecutionScopePhase.Failed)
            };
            execution = execution with { Scopes = scopes };
            execution = FailFiberAndAncestors(
                execution,
                execution.Fibers[joinable.ParentFiberId],
                failure);
            var cleanup = RemoveTerminalFiberObligations(
                execution,
                ownedObligations);
            var failedMerge = await context.Processor.ProcessAsync(
                new DurableStepFailedCommand(
                    CommandId.New(),
                    context.InstanceId,
                    context.TimeProvider.GetUtcNow(),
                    $"{joinable.ScopePlanId.Value}:merge",
                    $"{failure.Code}: {failure.Message}",
                    BuildEnvelope(context, execution, state, ownedObligations))
                {
                    ExpectedStreamVersion = currentVersion,
                    CancelWaitIds = cleanup.WaitIds,
                    CancelTimerIds = cleanup.TimerIds,
                    TerminalFiberIds = cleanup.TerminalFiberIds,
                    FailedSagaScopeIds = FailedSagaScopes(execution),
                    CoversRootSagaEligibility = RootFailed(execution)
                },
                cancellationToken).ConfigureAwait(false);
            return new NoRunnableResolution(
                execution,
                state,
                currentVersion,
                commands,
                failedMerge.Outcome == DurableCommandOutcome.Committed
                    ? DurableSegmentResult.Terminal
                    : Conflict(failedMerge));
        }

        var merge = await context.Processor.ProcessAsync(
            new DurableStepCompletedCommand(
                CommandId.New(),
                context.InstanceId,
                context.TimeProvider.GetUtcNow(),
                $"{joinable.ScopePlanId.Value}:merge",
                BuildEnvelope(context, execution, state, ownedObligations))
            {
                ExpectedStreamVersion = currentVersion,
                SagaScopeTransfers =
                [
                    new DurableSagaScopeTransfer(
                        joinable.Id,
                        execution.Fibers[joinable.ParentFiberId].OwningScopeId)
                ]
            },
            cancellationToken).ConfigureAwait(false);
        if (merge.Outcome != DurableCommandOutcome.Committed)
        {
            return new NoRunnableResolution(
                execution,
                state,
                currentVersion,
                commands,
                Conflict(merge));
        }

        currentVersion = merge.StreamVersion;
        commands++;
        return new NoRunnableResolution(
            execution,
            state,
            currentVersion,
            commands,
            BudgetReached(context, commands, elapsed)
                ? new DurableSegmentResult(
                    DurableSegmentOutcome.BudgetExhausted,
                    CommittedProgress: true)
                : null);
    }
}
