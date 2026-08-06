using System.Diagnostics;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Execution;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;

namespace OrcaCore.Engine.Durable.Driver;

internal sealed partial class DurableFiberDriverExecutor<TState>
{
    private static EventEnvelope? TakeResumedEvent(
        FiberRecord fiber,
        IReadOnlyList<DurablePendingResume> pendingResumes,
        out WaitId? consumedWaitId)
    {
        consumedWaitId = null;
        if (fiber.ResumeFromWaitId is not { } waitId ||
            pendingResumes.FirstOrDefault(pending => pending.WaitId.ToString() == waitId) is not { } pending)
        {
            return null;
        }

        consumedWaitId = pending.WaitId;
        return RuntimeStepContextFactory.CreateResumedEvent(
            pending.MatchedEventId,
            WorkflowEventContract.Create(
                EventName.Create(pending.EventName ?? "(unnamed)"),
                new EventContractVersion(pending.EventContractVersion ?? 1)),
            pending.CorrelationId ?? CorrelationId.Create("(uncorrelated)"),
            pending.MatchedAt,
            pending.Payload ?? []);
    }

    private static FiberRecord ClearResume(FiberRecord fiber)
    {
        return fiber with { ResumeFromWaitId = null };
    }

    private static FiberRecord ClearStepPolicyState(FiberRecord fiber)
    {
        return fiber with
        {
            RetryAttempt = 0,
            RetryNotBefore = null,
            LogicalOperationKey = null,
            AttemptInFlight = false,
            TimeoutDeadline = null
        };
    }

    private static string LogicalOperationKey(
        StructuredExecutionState execution,
        FiberRecord fiber,
        CompiledInstruction instruction)
    {
        return $"{execution.ContinueAsNewGeneration}:{fiber.Id.Value}:{instruction.Id.Value}:{fiber.LoopIteration}";
    }

    private static IReadOnlyList<WaitId> ConsumedWaitIds(WaitId? waitId)
    {
        return waitId is { } consumed ? [consumed] : [];
    }

    private static bool IsRetryEligible(OrcaCoreException error)
    {
        ArgumentNullException.ThrowIfNull(error);
        if (error is WorkflowDefinitionException or
            WorkflowDeadlineExceededException or
            WorkflowWaitTimeoutException)
        {
            return false;
        }

        return !error.Code.StartsWith("SFE-AUTH-", StringComparison.Ordinal) &&
            !error.Code.StartsWith("SFE-TYPE-", StringComparison.Ordinal) &&
            !error.Code.StartsWith("WF-CANCEL", StringComparison.Ordinal) &&
            !error.Code.StartsWith("LEASE-", StringComparison.Ordinal);
    }

    private static void RemoveConsumedObligation(
        IList<DurableOwnedObligationState> ownedObligations,
        WaitId? waitId)
    {
        if (waitId is not { } consumed)
        {
            return;
        }

        var obligation = ownedObligations.FirstOrDefault(candidate =>
            candidate.ObligationId == consumed.ToString());
        if (obligation is not null)
        {
            ownedObligations.Remove(obligation);
        }
    }

    private static long AllocateRegistrationSequence(ref StructuredExecutionState execution)
    {
        var allocated = execution.NextRegistrationSequence;
        execution = execution with
        {
            NextRegistrationSequence = checked(allocated + 1)
        };
        return allocated;
    }

    private static TerminalFiberCleanup RemoveTerminalFiberObligations(
        StructuredExecutionState execution,
        IList<DurableOwnedObligationState> ownedObligations)
    {
        var terminalFiberIds = execution.Fibers.Values
            .Where(fiber => fiber.Phase is FiberPhase.Completed or FiberPhase.Failed or FiberPhase.Cancelled)
            .Select(fiber => fiber.Id)
            .ToHashSet();
        var cancelled = ownedObligations
            .Where(obligation =>
                terminalFiberIds.Contains(new FiberId(obligation.FiberId)) &&
                !(obligation.Kind == DurableOwnedObligationKind.Resource &&
                  obligation.LeasePhase is
                      nameof(DurableLeaseObligationPhase.Quarantined) or
                      nameof(DurableLeaseObligationPhase.Released) or
                      nameof(DurableLeaseObligationPhase.LeaseLost)))
            .ToArray();
        foreach (var obligation in cancelled)
        {
            ownedObligations.Remove(obligation);
        }

        return new TerminalFiberCleanup(
            cancelled
                .Where(obligation => obligation.Kind == DurableOwnedObligationKind.Wait)
                .Select(obligation => WaitId.Parse(obligation.ObligationId))
                .ToArray(),
            cancelled
                .Where(obligation => obligation.Kind == DurableOwnedObligationKind.Timer)
                .Select(obligation => new TimerId(Guid.Parse(obligation.ObligationId)))
                .ToArray(),
            terminalFiberIds.ToArray());
    }

    private sealed record TerminalFiberCleanup(
        IReadOnlyList<WaitId> WaitIds,
        IReadOnlyList<TimerId> TimerIds,
        IReadOnlyList<FiberId> TerminalFiberIds);

    private static bool BudgetReached(
        DurableDriverContext context,
        int commands,
        Stopwatch elapsed)
    {
        return commands >= context.Budget.MaxCommandsPerSegment ||
            elapsed.Elapsed >= context.Budget.MaxSegmentDuration;
    }

    private static DurableSegmentResult Conflict(DurableCommandResult result)
    {
        return new DurableSegmentResult(
            DurableSegmentOutcome.Conflict,
            result.Message ?? $"Kernel command outcome {result.Outcome}.");
    }

    private static Task<DurableWorkflowAggregate> ReloadAggregateAsync(
        DurableDriverContext context,
        CancellationToken cancellationToken)
    {
        return new DurableAggregateLoader(context.Processor.EventStore)
            .LoadAsync(context.InstanceId, cancellationToken);
    }

    private static async Task<DurableSegmentResult> ParkAsync(
        DurableDriverContext context,
        DurableParkReason reason,
        string diagnostic,
        CancellationToken cancellationToken)
    {
        var result = await context.Processor.ProcessAsync(
            new DurableParkCommand(
                CommandId.New(),
                context.InstanceId,
                context.TimeProvider.GetUtcNow(),
                reason,
                diagnostic,
                FailedAttemptCount: 1,
                context.Aggregate.StreamVersion)
            {
                ExpectedStreamVersion = context.Aggregate.StreamVersion
            },
            cancellationToken).ConfigureAwait(false);
        return result.Outcome == DurableCommandOutcome.Committed
            ? new DurableSegmentResult(DurableSegmentOutcome.Parked, diagnostic)
            : Conflict(result);
    }
}
