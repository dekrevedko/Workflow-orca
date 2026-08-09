using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Core.Building;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Definitions;
using OrcaCore.Core.Execution;
using OrcaCore.Engine.Durable.Execution;

namespace OrcaCore.Engine.Durable.Driver;

internal static class DurableFiberEnvelopeMapper
{
    internal static DurableExecutionEnvelopeV2 ToEnvelope(
        StructuredExecutionState state,
        CompiledWorkflowPlan plan,
        SerializedPayload parentState,
        IReadOnlyList<DurableOwnedObligationState>? ownedObligations = null,
        DurableWorkflowOutputState? output = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(parentState);

        return new DurableExecutionEnvelopeV2
        {
            EnvelopeVersion = DurableExecutionEnvelopeV2.CurrentVersion,
            InstanceId = state.InstanceId,
            ContinueAsNewGeneration = state.ContinueAsNewGeneration,
            RootFiberId = state.RootFiberId.Value,
            PlanBinding = new DurablePlanBinding
            {
                DefinitionId = plan.DefinitionId,
                DefinitionVersion = plan.DefinitionVersion,
                CompilerFormatVersion = plan.FormatVersion,
                CompilerProfileId = plan.CompilerProfileId,
                PlanFingerprint = plan.Fingerprint
            },
            StateContentType = parentState.ContentType,
            StatePayload = parentState.Payload.ToArray(),
            Fibers = state.Fibers.Values
                .OrderBy(fiber => fiber.Id.Value, StringComparer.Ordinal)
                .Select(ToEnvelopeFiber)
                .ToArray(),
            Scopes = state.Scopes.Values
                .OrderBy(scope => scope.ScopeEntrySequence)
                .ThenBy(scope => scope.Id.Value, StringComparer.Ordinal)
                .Select(ToEnvelopeScope)
                .ToArray(),
            Scheduler = new DurableFiberSchedulerState
            {
                RunnableFiberIds = state.Scheduler.RunnableFiberIds
                    .Select(fiberId => fiberId.Value)
                    .ToArray(),
                NextFiberId = state.Scheduler.NextFiberId?.Value
            },
            WorkflowDeadline = state.WorkflowDeadline,
            WorkflowDeadlineTimerId = state.WorkflowDeadlineTimerId,
            OwnedObligations = ownedObligations?.ToArray() ?? [],
            Output = output,
            NextRegistrationSequence = state.NextRegistrationSequence,
            Diagnostics = new DurableExecutionDiagnostics
            {
            TotalQuantumRotations = checked(
                state.CompletedQuantumRotationCount +
                state.Fibers.Values.Sum(fiber => fiber.QuantumRotationCount)),
                ForcedRotations = checked(
                    state.CompletedForcedRotationCount +
                    state.Fibers.Values.Sum(fiber => fiber.ForcedRotationCount))
            }
        };
    }

    internal static StructuredExecutionState FromEnvelope(DurableExecutionEnvelopeV2 envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var fibers = envelope.Fibers.ToDictionary(
            fiber => new FiberId(fiber.FiberId),
            FromEnvelopeFiber);
        var scopes = envelope.Scopes.ToDictionary(
            scope => new ScopeId(scope.ScopeId),
            FromEnvelopeScope);
        var activeQuantumRotations = fibers.Values.Sum(fiber => fiber.QuantumRotationCount);
        var activeRotations = fibers.Values.Sum(fiber => fiber.ForcedRotationCount);
        return new StructuredExecutionState(
            envelope.InstanceId,
            envelope.ContinueAsNewGeneration,
            new FiberId(envelope.RootFiberId),
            new FiberSchedulerState(
                envelope.Scheduler.RunnableFiberIds.Select(id => new FiberId(id)).ToArray(),
                envelope.Scheduler.NextFiberId is { } next ? new FiberId(next) : null),
            fibers,
            scopes)
        {
            WorkflowDeadline = envelope.WorkflowDeadline,
            WorkflowDeadlineTimerId = envelope.WorkflowDeadlineTimerId,
            CompletedQuantumRotationCount = Math.Max(
                0,
                envelope.Diagnostics.TotalQuantumRotations - activeQuantumRotations),
            CompletedForcedRotationCount = Math.Max(
                0,
                envelope.Diagnostics.ForcedRotations - activeRotations),
            NextRegistrationSequence = Math.Max(
                envelope.NextRegistrationSequence,
                envelope.OwnedObligations.Count == 0
                    ? 1
                    : checked(envelope.OwnedObligations.Max(obligation =>
                        obligation.RegistrationSequence) + 1))
        };
    }

    private static DurableFiberState ToEnvelopeFiber(FiberRecord fiber)
    {
        return new DurableFiberState
        {
            FiberId = fiber.Id.Value,
            OwningScopeId = fiber.OwningScopeId?.Value,
            InstructionId = fiber.InstructionId.Value,
            Phase = ToEnvelopePhase(fiber.Phase),
            LoopIteration = fiber.LoopIteration,
            NextScopeEntrySequence = fiber.NextScopeEntrySequence,
            LocalStatePayload = fiber.LocalStatePayload?.ToArray(),
            ResultPayload = fiber.ResultPayload?.ToArray(),
            Blocked = fiber.Blocked is null
                ? null
                : new DurableFiberBlock
                {
                    Reason = ToEnvelopeBlockedReason(fiber.Blocked.Reason),
                    ObligationId = fiber.Blocked.ObligationId
                },
            Failure = fiber.Failure is null
                ? null
                : ToEnvelopeFailure(fiber.Failure),
            CancellationReason = fiber.CancellationReason,
            QuantumRotationCount = fiber.QuantumRotationCount,
            ForcedRotationCount = fiber.ForcedRotationCount,
            RetryAttempt = fiber.RetryAttempt,
            RetryNotBefore = fiber.RetryNotBefore,
            LogicalOperationKey = fiber.LogicalOperationKey,
            AttemptInFlight = fiber.AttemptInFlight,
            TimeoutDeadline = fiber.TimeoutDeadline,
            ResumeFromWaitId = fiber.ResumeFromWaitId,
            CurrentCausationEventId = fiber.CurrentCausationEventId
        };
    }

    private static FiberRecord FromEnvelopeFiber(DurableFiberState fiber)
    {
        return new FiberRecord(
            new FiberId(fiber.FiberId),
            fiber.OwningScopeId is { } scopeId ? new ScopeId(scopeId) : null,
            new InstructionId(fiber.InstructionId),
            FromEnvelopePhase(fiber.Phase),
            fiber.LoopIteration,
            fiber.NextScopeEntrySequence,
            fiber.LocalStatePayload?.ToArray(),
            fiber.ResultPayload?.ToArray(),
            fiber.Blocked is null
                ? null
                : new FiberBlock(
                    FromEnvelopeBlockedReason(fiber.Blocked.Reason),
                    fiber.Blocked.ObligationId),
            fiber.Failure is null
                ? null
                : FromEnvelopeFailure(fiber.Failure),
            fiber.CancellationReason)
        {
            QuantumRotationCount = fiber.QuantumRotationCount,
            ForcedRotationCount = fiber.ForcedRotationCount,
            RetryAttempt = fiber.RetryAttempt,
            RetryNotBefore = fiber.RetryNotBefore,
            LogicalOperationKey = fiber.LogicalOperationKey,
            AttemptInFlight = fiber.AttemptInFlight,
            TimeoutDeadline = fiber.TimeoutDeadline,
            ResumeFromWaitId = fiber.ResumeFromWaitId,
            CurrentCausationEventId = fiber.CurrentCausationEventId
        };
    }

    private static DurableExecutionScopeState ToEnvelopeScope(ExecutionScopeRecord scope)
    {
        return new DurableExecutionScopeState
        {
            ScopeId = scope.Id.Value,
            ScopePlanId = scope.ScopePlanId.Value,
            ScopeEntrySequence = scope.ScopeEntrySequence,
            ParentScopeId = scope.ParentScopeId?.Value,
            ParentFiberId = scope.ParentFiberId.Value,
            Kind = (DurableExecutionScopeKind)(int)scope.Kind,
            Phase = (DurableExecutionScopePhase)(int)scope.Phase,
            ChildFiberIds = scope.ChildFiberIds.Select(id => id.Value).ToArray(),
            WinnerFiberId = scope.WinnerFiberId?.Value,
            CommittedResults = scope.CommittedResults
                .OrderBy(result => result.Key.Value, StringComparer.Ordinal)
                .Select(result => new DurableCommittedResult
                {
                    FiberId = result.Key.Value,
                    Payload = result.Value?.ToArray()
                })
                .ToArray(),
            ForEach = scope.ForEach is null ? null : ToEnvelopeForEach(scope.ForEach)
        };
    }

    private static ExecutionScopeRecord FromEnvelopeScope(DurableExecutionScopeState scope)
    {
        return new ExecutionScopeRecord(
            new ScopeId(scope.ScopeId),
            new ScopePlanId(scope.ScopePlanId),
            scope.ScopeEntrySequence,
            scope.ParentScopeId is { } parentScopeId ? new ScopeId(parentScopeId) : null,
            new FiberId(scope.ParentFiberId),
            (CompiledScopeKind)(int)scope.Kind,
            (ExecutionScopePhase)(int)scope.Phase,
            scope.ChildFiberIds.Select(id => new FiberId(id)).ToArray(),
            scope.WinnerFiberId is { } winner ? new FiberId(winner) : null,
            scope.CommittedResults.ToDictionary(
                result => new FiberId(result.FiberId),
                result => result.Payload?.ToArray()))
        {
            ForEach = scope.ForEach is null ? null : FromEnvelopeForEach(scope.ForEach)
        };
    }

    private static DurableForEachScopeState ToEnvelopeForEach(ForEachRuntimeState state)
    {
        return new DurableForEachScopeState
        {
            Descriptors = state.Descriptors
                .OrderBy(descriptor => descriptor.Index)
                .Select(descriptor => new DurableForEachItemDescriptor
                {
                    Index = descriptor.Index,
                    LocalStatePayload = descriptor.LocalStatePayload.ToArray()
                })
                .ToArray(),
            NextAdmissionOffset = state.NextAdmissionOffset,
            MaxConcurrency = state.MaxConcurrency,
            JoinPolicy = state.JoinPolicy.ToString(),
            FailurePolicy = state.FailurePolicy.ToString(),
            ItemFibers = state.ItemIndexByFiber
                .OrderBy(binding => binding.Value)
                .Select(binding => new DurableForEachFiberBinding
                {
                    FiberId = binding.Key.Value,
                    ItemIndex = binding.Value
                })
                .ToArray(),
            Outcomes = state.Outcomes.Values
                .OrderBy(outcome => outcome.Index)
                .Select(outcome => new DurableForEachItemOutcomeState
                {
                    Index = outcome.Index,
                    Status = outcome.Status.ToString(),
                    ResultPayload = outcome.ResultPayload?.ToArray(),
                    Failure = outcome.Failure is null
                        ? null
                        : ToEnvelopeFailure(outcome.Failure)
                })
                .ToArray()
        };
    }

    private static ForEachRuntimeState FromEnvelopeForEach(DurableForEachScopeState state)
    {
        return new ForEachRuntimeState(
            state.Descriptors
                .Select(descriptor => new ForEachItemDescriptor(
                    descriptor.Index,
                    descriptor.LocalStatePayload.ToArray()))
                .ToArray(),
            state.NextAdmissionOffset,
            state.MaxConcurrency,
            Enum.Parse<ForEachJoinPolicy>(state.JoinPolicy),
            Enum.Parse<ForEachFailurePolicy>(state.FailurePolicy),
            state.ItemFibers.ToDictionary(
                binding => new FiberId(binding.FiberId),
                binding => binding.ItemIndex),
            state.Outcomes.ToDictionary(
                outcome => outcome.Index,
                outcome => new ForEachTerminalOutcome(
                    outcome.Index,
                    Enum.Parse<ForEachItemTerminalStatus>(outcome.Status),
                    outcome.ResultPayload?.ToArray(),
                    outcome.Failure is null
                        ? null
                        : FromEnvelopeFailure(outcome.Failure))));
    }

    private static DurableFiberFailure ToEnvelopeFailure(FiberFailure failure) =>
        new()
        {
            Code = failure.Code,
            Message = failure.Message,
            AuthoredLocation = failure.AuthoredLocation.Value,
            OccurrenceKind = OccurrenceKind(failure.Occurrence),
            BranchId = failure.Occurrence is FailureOccurrence.Branch branch
                ? branch.BranchId.Value
                : null,
            ItemIndex = failure.Occurrence is FailureOccurrence.Item item
                ? item.Index
                : null,
            Causes = failure.Causes.Select(ToEnvelopeFailure).ToArray()
        };

    private static FiberFailure FromEnvelopeFailure(DurableFiberFailure failure) =>
        new(
            failure.Code,
            failure.Message,
            failure.Causes.Select(FromEnvelopeFailure).ToArray(),
            FailureProvenance.Location(failure.AuthoredLocation),
            Occurrence(failure));

    private static string OccurrenceKind(FailureOccurrence occurrence) => occurrence switch
    {
        FailureOccurrence.Root => "root",
        FailureOccurrence.Branch => "branch",
        FailureOccurrence.Item => "item",
        _ => throw new InvalidOperationException(
            $"Unsupported failure occurrence '{occurrence.GetType().FullName}'.")
    };

    private static FailureOccurrence Occurrence(DurableFiberFailure failure) =>
        failure.OccurrenceKind switch
        {
            "root" when failure.BranchId is null && failure.ItemIndex is null =>
                FailureProvenance.RootOccurrence(),
            "branch" when !string.IsNullOrWhiteSpace(failure.BranchId) &&
                          failure.ItemIndex is null =>
                FailureProvenance.BranchOccurrence(failure.BranchId),
            "item" when failure.BranchId is null && failure.ItemIndex is >= 0 =>
                FailureProvenance.ItemOccurrence(failure.ItemIndex.Value),
            _ => throw new InvalidOperationException(
                $"Invalid durable failure occurrence '{failure.OccurrenceKind}'.")
        };

    private static DurableFiberPhase ToEnvelopePhase(FiberPhase phase) => phase switch
    {
        FiberPhase.Runnable => DurableFiberPhase.Runnable,
        FiberPhase.Blocked => DurableFiberPhase.Blocked,
        FiberPhase.Completed => DurableFiberPhase.Completed,
        FiberPhase.Failed => DurableFiberPhase.Failed,
        FiberPhase.Cancelled => DurableFiberPhase.Cancelled,
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null)
    };

    private static FiberPhase FromEnvelopePhase(DurableFiberPhase phase) => phase switch
    {
        DurableFiberPhase.Runnable => FiberPhase.Runnable,
        DurableFiberPhase.Blocked => FiberPhase.Blocked,
        DurableFiberPhase.Completed => FiberPhase.Completed,
        DurableFiberPhase.Failed => FiberPhase.Failed,
        DurableFiberPhase.Cancelled => FiberPhase.Cancelled,
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null)
    };

    private static DurableFiberBlockedReason ToEnvelopeBlockedReason(FiberBlockedReason reason) =>
        (DurableFiberBlockedReason)(int)reason;

    private static FiberBlockedReason FromEnvelopeBlockedReason(DurableFiberBlockedReason reason) =>
        (FiberBlockedReason)(int)reason;
}
