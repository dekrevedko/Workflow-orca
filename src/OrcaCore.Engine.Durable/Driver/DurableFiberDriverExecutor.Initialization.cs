using System.Collections.Concurrent;
using System.Diagnostics;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Definitions;
using OrcaCore.Core.Execution;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;

namespace OrcaCore.Engine.Durable.Driver;

internal sealed partial class DurableFiberDriverExecutor<TState>
{
    private readonly WorkflowDefinition<TState> definition;
    private readonly CompiledWorkflowPlan plan;
    private readonly DurableStructuredValueCodec codec;
    private readonly IServiceProvider? serviceProvider;
    private readonly int maxConcurrentExecutionPathsPerInstance;
    private readonly DurableStepThrottleCoordinator stepThrottles;
    private readonly ConcurrentDictionary<StepThrottleOwner, DurableStepThrottleLease> grantedStepThrottles = [];

    internal DurableFiberDriverExecutor(
        WorkflowDefinition<TState> definition,
        IServiceProvider? serviceProvider = null,
        int maxConcurrentExecutionPathsPerInstance = int.MaxValue,
        DurableStepThrottleCoordinator? stepThrottles = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        this.definition = definition;
        this.serviceProvider = serviceProvider;
        if (maxConcurrentExecutionPathsPerInstance <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxConcurrentExecutionPathsPerInstance),
                maxConcurrentExecutionPathsPerInstance,
                "MaxConcurrentExecutionPathsPerInstance must be positive.");
        }

        this.maxConcurrentExecutionPathsPerInstance = maxConcurrentExecutionPathsPerInstance;
        this.stepThrottles = stepThrottles ?? new DurableStepThrottleCoordinator();
        plan = (CompiledWorkflowPlan)WorkflowDefinitionRuntime.GetPlan(definition);
        codec = new DurableStructuredValueCodec();
    }

    private async Task<InitializationResult> InitializeAsync(
        DurableDriverContext context,
        CancellationToken cancellationToken)
    {
        var ownedObligations = context.FiberEnvelope?.OwnedObligations.ToList() ?? [];
        if (context.FiberEnvelope is not { } persisted)
        {
            DateTimeOffset? workflowDeadline = plan.WorkflowTimeout is { } timeout
                ? (context.Aggregate.CreatedAt ??
                    throw global::OrcaCore.Engine.Durable.Internal.DurableContractAdapter.DefinitionException(
                        "A started durable workflow has no creation timestamp.")).Add(timeout)
                : null;
            var initialExecution = StructuredExecutionState.Create(
                context.InstanceId,
                context.Aggregate.ContinueAsNewGeneration,
                plan.Instructions[0].Id) with
            {
                WorkflowDeadline = workflowDeadline
            };
            TState initialState = default!;
            if (workflowDeadline is not null &&
                plan.Instructions[0] is { Kind: CompiledInstructionKind.Init } init)
            {
                (initialExecution, initialState) = ExecuteInitInstruction(
                    context,
                    initialExecution,
                    initialExecution.Fibers[initialExecution.RootFiberId],
                    init);
            }

            return new InitializationResult(
                initialExecution,
                initialState,
                ownedObligations,
                null);
        }

        var validation = DurableFiberEnvelopeValidator.Validate(persisted, plan, context.InstanceId);
        if (!validation.IsValid)
        {
            var parked = await ParkAsync(
                context,
                validation.Code == "SFE-BIND-001"
                    ? DurableParkReason.RuntimeStateVersion
                    : DurableParkReason.VersionBinding,
                $"{validation.Code}: {validation.Diagnostic}",
                cancellationToken).ConfigureAwait(false);
            return new InitializationResult(null, default!, ownedObligations, parked);
        }

        var state = context.Serializer.Deserialize<TState>(new SerializedPayload(
            persisted.StateContentType,
            persisted.StatePayload));
        var execution = ReconcilePendingResumes(
            DurableFiberEnvelopeMapper.FromEnvelope(persisted),
            ownedObligations,
            context.Aggregate.WaitState.PendingResumes);
        return new InitializationResult(execution, state, ownedObligations, null);
    }

    private async Task<SuspensionRecoveryResult> RecoverSuspensionsAsync(
        DurableDriverContext context,
        StructuredExecutionState execution,
        TState state,
        List<DurableOwnedObligationState> ownedObligations,
        StreamVersion currentVersion,
        int commands,
        Stopwatch elapsed,
        CancellationToken cancellationToken)
    {
        var timedOutWait = await RecoverTimedOutWaitAsync(
            context,
            execution,
            state,
            ownedObligations,
            currentVersion,
            commands,
            cancellationToken).ConfigureAwait(false);
        if (timedOutWait is not null)
        {
            return timedOutWait;
        }

        var recoveredAggregate = context.Aggregate;
        foreach (var queuedLease in ownedObligations.Where(obligation =>
                     obligation.Kind == DurableOwnedObligationKind.Resource &&
                     obligation.ProtectionToken is not null &&
                     string.Equals(
                         obligation.LeasePhase,
                         nameof(DurableLeaseObligationPhase.Queued),
                         StringComparison.Ordinal) &&
                     obligation.HolderKey is not null &&
                     recoveredAggregate.WaitState.HasWait(WaitId.Parse(obligation.ObligationId))))
        {
            if (!await context.Processor.HasGrantedResourceTicketsAsync(
                    context.InstanceId,
                    queuedLease.HolderKey!,
                    queuedLease.LeaseRequirements,
                    cancellationToken)
                .ConfigureAwait(false))
            {
                continue;
            }

            var waitId = WaitId.Parse(queuedLease.ObligationId);
            var promoted = await context.Processor.ProcessAsync(
                new AcquireResourcePoolCommand
                {
                    CommandId = CommandId.New(),
                    InstanceId = context.InstanceId,
                    RequestedAt = context.TimeProvider.GetUtcNow(),
                    HolderKey = queuedLease.HolderKey!,
                    Requirements = queuedLease.LeaseRequirements,
                    ExpiresAt = null,
                    WaitId = waitId,
                    WaitSequence = queuedLease.RegistrationSequence,
                    FiberId = new FiberId(queuedLease.FiberId),
                    ScopeId = queuedLease.ScopeId is null
                        ? null
                        : new ScopeId(queuedLease.ScopeId),
                    Envelope = BuildEnvelope(context, execution, state, ownedObligations),
                    ExpectedStreamVersion = currentVersion
                },
                cancellationToken).ConfigureAwait(false);
            if (promoted.Outcome != DurableCommandOutcome.Committed)
            {
                return new SuspensionRecoveryResult(
                    execution,
                    currentVersion,
                    commands,
                    Conflict(promoted));
            }

            currentVersion = promoted.StreamVersion;
            commands++;
            recoveredAggregate = await ReloadAggregateAsync(context, cancellationToken)
                .ConfigureAwait(false);
        }

        execution = RecoverCommittedSuspensions(
            execution,
            ownedObligations,
            recoveredAggregate.WaitState,
            recoveredAggregate.TimerState,
            plan,
            out var recoveredSuspension);
        if (!recoveredSuspension)
        {
            return new SuspensionRecoveryResult(execution, currentVersion, commands, null);
        }

        var recovered = await context.Processor.ProcessAsync(
            new DurableStepCompletedCommand(
                CommandId.New(),
                context.InstanceId,
                context.TimeProvider.GetUtcNow(),
                "owned-suspension-recovery",
                BuildEnvelope(context, execution, state, ownedObligations))
            {
                ExpectedStreamVersion = currentVersion
            },
            cancellationToken).ConfigureAwait(false);
        if (recovered.Outcome != DurableCommandOutcome.Committed)
        {
            return new SuspensionRecoveryResult(
                execution,
                currentVersion,
                commands,
                Conflict(recovered));
        }

        currentVersion = recovered.StreamVersion;
        commands++;
        var terminal = BudgetReached(context, commands, elapsed)
            ? new DurableSegmentResult(
                DurableSegmentOutcome.BudgetExhausted,
                CommittedProgress: true)
            : null;
        return new SuspensionRecoveryResult(execution, currentVersion, commands, terminal);
    }

    private async Task<SuspensionRecoveryResult?> RecoverTimedOutWaitAsync(
        DurableDriverContext context,
        StructuredExecutionState execution,
        TState state,
        List<DurableOwnedObligationState> ownedObligations,
        StreamVersion currentVersion,
        int commands,
        CancellationToken cancellationToken)
    {
        var pendingResumeIds = context.Aggregate.WaitState.PendingResumes
            .Select(resume => resume.WaitId.ToString())
            .ToHashSet(StringComparer.Ordinal);
        var obligation = ownedObligations.FirstOrDefault(candidate =>
            candidate.Kind == DurableOwnedObligationKind.Wait &&
            !context.Aggregate.WaitState.HasWait(WaitId.Parse(candidate.ObligationId)) &&
            !pendingResumeIds.Contains(candidate.ObligationId));
        if (obligation is null)
        {
            return null;
        }

        var fiberId = new FiberId(obligation.FiberId);
        if (!execution.Fibers.TryGetValue(fiberId, out var fiber) ||
            fiber.Phase != FiberPhase.Blocked ||
            fiber.Blocked is not { Reason: FiberBlockedReason.Wait } blocked ||
            blocked.ObligationId != obligation.ObligationId ||
            obligation.InstructionId is not { } instructionId)
        {
            return null;
        }

        var instruction = plan.GetInstruction(new InstructionId(instructionId));
        if (instruction.Kind != CompiledInstructionKind.Wait ||
            instruction.WaitTimeout is null ||
            instruction.EventContract is null)
        {
            return null;
        }

        var correlation = ResolveWaitCorrelation(execution, fiber, state, instruction);
        var exception = global::OrcaCore.Engine.Durable.Internal.DurableContractAdapter.WaitTimeout(
            instruction.EventContract,
            correlation);
        var failure = FailureProvenance.Create(
            plan,
            execution,
            fiber,
            instruction,
            exception.Code,
            exception.Message);
        if (fiber.OwningScopeId is { } scopeId &&
            execution.Scopes[scopeId] is { Kind: CompiledScopeKind.ForEach } forEachScope)
        {
            execution = ScopeReducer.RecordForEachTerminal(
                execution,
                plan.GetScope(forEachScope.ScopePlanId),
                scopeId,
                fiber.Id,
                resultPayload: null,
                failure,
                maxConcurrentExecutionPathsPerInstance).State;
        }
        else
        {
            execution = FailFiberAndAncestors(execution, fiber, failure);
        }

        ownedObligations.Remove(obligation);
        var cleanup = RemoveTerminalFiberObligations(execution, ownedObligations);
        DurableCommandResult commit;
        DurableSegmentResult result;
        if (RootFailed(execution))
        {
            commit = await context.Processor.ProcessAsync(
                new DurableStepFailedCommand(
                    CommandId.New(),
                    context.InstanceId,
                    context.TimeProvider.GetUtcNow(),
                    instruction.Path,
                    $"{failure.Code}: {failure.Message}",
                    BuildEnvelope(context, execution, state, ownedObligations))
                {
                    ExpectedStreamVersion = currentVersion,
                    CancelWaitIds = cleanup.WaitIds,
                    CancelTimerIds = cleanup.TimerIds,
                    TerminalFiberIds = cleanup.TerminalFiberIds
                },
                CancellationToken.None).ConfigureAwait(false);
            result = DurableSegmentResult.Terminal;
        }
        else
        {
            commit = await context.Processor.ProcessAsync(
                new DurableFiberFailedCommand(
                    CommandId.New(),
                    context.InstanceId,
                    context.TimeProvider.GetUtcNow(),
                    instruction.Path,
                    $"{failure.Code}: {failure.Message}",
                    BuildEnvelope(context, execution, state, ownedObligations))
                {
                    ExpectedStreamVersion = currentVersion,
                    CancelWaitIds = cleanup.WaitIds,
                    CancelTimerIds = cleanup.TimerIds,
                    TerminalFiberIds = cleanup.TerminalFiberIds
                },
                CancellationToken.None).ConfigureAwait(false);
            result = DurableSegmentResult.Yielded;
        }

        return new SuspensionRecoveryResult(
            execution,
            commit.StreamVersion,
            checked(commands + 1),
            commit.Outcome == DurableCommandOutcome.Committed ? result : Conflict(commit));
    }

    private Task<DurableCommandResult> CommitScopeStartAsync(
        DurableDriverContext context,
        StructuredExecutionState execution,
        TState state,
        IReadOnlyList<DurableOwnedObligationState> ownedObligations,
        CompiledInstruction instruction,
        StreamVersion currentVersion,
        CancellationToken cancellationToken)
    {
        return context.Processor.ProcessAsync(
            new DurableStepCompletedCommand(
                CommandId.New(),
                context.InstanceId,
                context.TimeProvider.GetUtcNow(),
                $"{instruction.Path}:scope-start",
                BuildEnvelope(context, execution, state, ownedObligations))
            {
                ExpectedStreamVersion = currentVersion
            },
            cancellationToken);
    }

    private sealed record InitializationResult(
        StructuredExecutionState? Execution,
        TState State,
        List<DurableOwnedObligationState> OwnedObligations,
        DurableSegmentResult? Terminal);

    private sealed record SuspensionRecoveryResult(
        StructuredExecutionState Execution,
        StreamVersion StreamVersion,
        int Commands,
        DurableSegmentResult? Terminal);
}
