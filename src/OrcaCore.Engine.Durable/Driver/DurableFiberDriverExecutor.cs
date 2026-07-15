using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Definitions;
using OrcaCore.Core.Execution;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;

namespace OrcaCore.Engine.Durable.Driver;

internal sealed partial class DurableFiberDriverExecutor<TState> : IDurableDriverExecutor
{
    public async Task<DurableSegmentResult> RunSegmentAsync(
        DurableDriverContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var initialization = await InitializeAsync(context, cancellationToken).ConfigureAwait(false);
        if (initialization.Terminal is { } initializationFailure)
        {
            return initializationFailure;
        }

        var execution = initialization.Execution!;
        var state = initialization.State;
        var ownedObligations = initialization.OwnedObligations;
        var currentVersion = context.Aggregate.StreamVersion;
        var commands = 0;
        var elapsed = Stopwatch.StartNew();
        var recovery = await RecoverSuspensionsAsync(
            context,
            execution,
            state,
            ownedObligations,
            currentVersion,
            commands,
            elapsed,
            cancellationToken).ConfigureAwait(false);
        if (recovery.Terminal is { } recoveryResult)
        {
            return recoveryResult;
        }

        execution = recovery.Execution;
        currentVersion = recovery.StreamVersion;
        commands = recovery.Commands;
        var quantumBudget = new FiberQuantumBudget(
            plan.CompilerOptions.MaxInternalInstructionsPerQuantum);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (BudgetReached(context, commands, elapsed))
            {
                return new DurableSegmentResult(
                    DurableSegmentOutcome.BudgetExhausted,
                    CommittedProgress: commands > 0);
            }
            var selected = FiberScheduler.SelectNext(execution.Scheduler);
            if (selected is null)
            {
                var joinable = execution.Scopes.Values
                    .Where(scope => scope.Phase == ExecutionScopePhase.Joinable)
                    .OrderBy(scope => scope.Id.Value, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (joinable is null)
                {
                    return DurableSegmentResult.Suspended;
                }
                try
                {
                    (execution, state) = MergeAndResume(execution, joinable, state);
                    quantumBudget.EndTurn();
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    var failure = new FiberFailure(exception.GetType().Name, exception.Message);
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
                    return failedMerge.Outcome == DurableCommandOutcome.Committed
                        ? DurableSegmentResult.Terminal
                        : Conflict(failedMerge);
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
                    return Conflict(merge);
                }

                currentVersion = merge.StreamVersion;
                commands++;
                if (BudgetReached(context, commands, elapsed))
                {
                    return new DurableSegmentResult(
                        DurableSegmentOutcome.BudgetExhausted,
                        CommittedProgress: true);
                }
                continue;
            }

            var fiber = execution.Fibers[selected.Value];
            var instruction = plan.GetInstruction(fiber.InstructionId);
            if (quantumBudget.ShouldRotate(fiber.Id, instruction.Kind))
            {
                return await CommitForcedRotationAsync(
                    context,
                    execution,
                    fiber,
                    instruction,
                    state,
                    ownedObligations,
                    currentVersion,
                    cancellationToken).ConfigureAwait(false);
            }

            quantumBudget.Record(fiber.Id, instruction.Kind);
            if (instruction.Kind == CompiledInstructionKind.Step)
            {
                quantumBudget.EndTurn();
            }

            var resumedEvent = TakeResumedEvent(
                fiber,
                context.Aggregate.WaitState.PendingResumes,
                out var consumedWaitId);
            switch (instruction.Kind)
            {
                case CompiledInstructionKind.Init:
                    (execution, state) = ExecuteInitInstruction(context, execution, fiber, instruction);
                    break;
                case CompiledInstructionKind.Wait:
                {
                    if (instruction.Operation is null ||
                        string.IsNullOrWhiteSpace(instruction.EventName) ||
                        instruction.WaitMode is not { } waitMode)
                    {
                        throw new WorkflowDefinitionException(
                            $"Compiled wait '{instruction.Path}' has no typed executable binding.");
                    }

                    var registration = await RegisterOwnedWaitAsync(
                        context,
                        execution,
                        fiber,
                        instruction,
                        state,
                        ownedObligations,
                        instruction.EventName,
                        ResolveWaitCorrelation(execution, fiber, state, instruction),
                        waitMode,
                        currentVersion,
                        consumedWaitId,
                        cancellationToken).ConfigureAwait(false);
                    execution = registration.Execution;
                    if (registration.Commit.Outcome != DurableCommandOutcome.Committed)
                    {
                        return Conflict(registration.Commit);
                    }

                    currentVersion = registration.Commit.StreamVersion;
                    commands++;
                    if (BudgetReached(context, commands, elapsed))
                    {
                        return new DurableSegmentResult(
                            DurableSegmentOutcome.BudgetExhausted,
                            CommittedProgress: true);
                    }

                    break;
                }
                case CompiledInstructionKind.Delay:
                {
                    var duration = instruction.DelayDuration ??
                        throw new WorkflowDefinitionException(
                            $"Compiled delay '{instruction.Path}' has no duration.");
                    var timerId = TimerId.New();
                    var blocked = FiberReducer.Block(
                        fiber,
                        FiberBlockedReason.Timer,
                        timerId.ToString());
                    execution = execution with
                    {
                        Fibers = new Dictionary<FiberId, FiberRecord>(execution.Fibers)
                        {
                            [fiber.Id] = blocked
                        },
                        Scheduler = FiberScheduler.RemoveRunnable(execution.Scheduler, [fiber.Id])
                    };
                    ownedObligations.Add(new DurableOwnedObligationState
                    {
                        Kind = DurableOwnedObligationKind.Timer,
                        ObligationId = timerId.ToString(),
                        FiberId = fiber.Id.Value,
                        ScopeId = fiber.OwningScopeId?.Value,
                        RegistrationSequence = AllocateRegistrationSequence(ref execution)
                    });
                    var now = context.TimeProvider.GetUtcNow();
                    var scheduled = await context.Processor.ProcessAsync(
                        new ScheduleTimerCommand
                        {
                            CommandId = CommandId.New(),
                            InstanceId = context.InstanceId,
                            RequestedAt = now,
                            TimerId = timerId,
                            FireAt = now.Add(duration),
                            WakeupName = instruction.Path,
                            FiberId = fiber.Id,
                            ScopeId = fiber.OwningScopeId,
                            Envelope = BuildEnvelope(context, execution, state, ownedObligations),
                            ExpectedStreamVersion = currentVersion
                        },
                        cancellationToken).ConfigureAwait(false);
                    if (scheduled.Outcome != DurableCommandOutcome.Committed)
                    {
                        return Conflict(scheduled);
                    }

                    currentVersion = scheduled.StreamVersion;
                    commands++;
                    if (BudgetReached(context, commands, elapsed))
                    {
                        return new DurableSegmentResult(
                            DurableSegmentOutcome.BudgetExhausted,
                            CommittedProgress: true);
                    }

                    break;
                }
                case CompiledInstructionKind.RunChild:
                case CompiledInstructionKind.RunChildren:
                {
                    var child = await ExecuteChildInstructionAsync(
                        context,
                        execution,
                        fiber,
                        instruction,
                        state,
                        ownedObligations,
                        currentVersion,
                        consumedWaitId,
                        cancellationToken).ConfigureAwait(false);
                    execution = child.Execution;
                    if (child.Commit is not { } childCommit)
                    {
                        break;
                    }

                    if (childCommit.Outcome != DurableCommandOutcome.Committed)
                    {
                        return Conflict(childCommit);
                    }

                    currentVersion = childCommit.StreamVersion;
                    commands++;
                    if (BudgetReached(context, commands, elapsed))
                    {
                        return new DurableSegmentResult(
                            DurableSegmentOutcome.BudgetExhausted,
                            CommittedProgress: true);
                    }

                    break;
                }
                case CompiledInstructionKind.If:
                case CompiledInstructionKind.LoopCheck:
                    execution = ExecuteConditionInstruction(execution, fiber, instruction, state);
                    break;
                case CompiledInstructionKind.IfJoin:
                case CompiledInstructionKind.LoopBack:
                case CompiledInstructionKind.LoopExit:
                    execution = AdvanceStructuralInstruction(execution, fiber, instruction);
                    break;
                case CompiledInstructionKind.Step:
                {
                    if (instruction.Policy.Timeout is { } stepTimeout && fiber.TimeoutDeadline is null)
                    {
                        var admittedFiber = fiber with
                        {
                            TimeoutDeadline = context.TimeProvider.GetUtcNow().Add(stepTimeout),
                            LogicalOperationKey = LogicalOperationKey(execution, fiber, instruction)
                        };
                        execution = execution with
                        {
                            Fibers = new Dictionary<FiberId, FiberRecord>(execution.Fibers)
                            {
                                [fiber.Id] = admittedFiber
                            },
                            Scheduler = FiberScheduler.CompleteTurn(
                                execution.Scheduler,
                                fiber.Id,
                                requeueSelected: true)
                        };
                        var admitted = await context.Processor.ProcessAsync(
                            new DurableYieldCommand(
                                CommandId.New(),
                                context.InstanceId,
                                context.TimeProvider.GetUtcNow(),
                                $"{instruction.Path}:timeout-admission",
                                BuildEnvelope(context, execution, state, ownedObligations))
                            {
                                ExpectedStreamVersion = currentVersion
                            },
                            cancellationToken).ConfigureAwait(false);
                        return admitted.Outcome == DurableCommandOutcome.Committed
                            ? DurableSegmentResult.PolicyBoundary
                            : Conflict(admitted);
                    }

                    var resumedObligation = consumedWaitId is { } resumedWaitId
                        ? ownedObligations.FirstOrDefault(obligation =>
                            obligation.ObligationId == resumedWaitId.ToString())
                        : null;
                    if (resumedEvent?.EventName == DurableRuntimeEventNames.ExternalJobCompleted &&
                        resumedObligation?.Kind == DurableOwnedObligationKind.ExternalJob)
                    {
                        var obligationIndex = ownedObligations.IndexOf(resumedObligation);
                        ownedObligations[obligationIndex] = resumedObligation with
                        {
                            Kind = DurableOwnedObligationKind.PendingResume
                        };
                        execution = MoveTo(execution, fiber, RequiredNext(instruction));
                        break;
                    }

                    var executed = await ExecutePolicyStepAsync(
                        context,
                        execution,
                        fiber,
                        instruction,
                        state,
                        resumedEvent,
                        cancellationToken).ConfigureAwait(false);
                    execution = executed.Execution;
                    fiber = executed.Fiber;
                    state = executed.State;
                    if (executed.OperatorCancelled)
                    {
                        var cancelled = await context.Processor.ProcessAsync(
                            new CancelWorkflowCommand
                            {
                                CommandId = CommandId.New(),
                                InstanceId = context.InstanceId,
                                RequestedAt = context.TimeProvider.GetUtcNow()
                            },
                            CancellationToken.None).ConfigureAwait(false);
                        return cancelled.Outcome is DurableCommandOutcome.Committed or DurableCommandOutcome.NoOp
                            ? DurableSegmentResult.Terminal
                            : Conflict(cancelled);
                    }

                    var result = executed.Result;
                    // Shutdown may cancel the step body, but once it returns a result the current
                    // mutation must finish so a surviving host can resume from committed state.
                    var commitCancellationToken = CancellationToken.None;
                    switch (result)
                    {
                        case StepResult.Completed:
                            fiber = ClearStepPolicyState(ClearResume(fiber));
                            execution = CompleteStepTurn(
                                execution,
                                fiber,
                                instruction);
                            RemoveConsumedObligation(ownedObligations, consumedWaitId);
                            var completedEnvelope = BuildEnvelope(
                                context,
                                execution,
                                state,
                                ownedObligations);
                            var completed = await context.Processor.ProcessAsync(
                                new DurableStepCompletedCommand(
                                    CommandId.New(),
                                    context.InstanceId,
                                    context.TimeProvider.GetUtcNow(),
                                    instruction.Path,
                                    completedEnvelope)
                                {
                                    ExpectedStreamVersion = currentVersion,
                                    ConsumedResumeWaitIds = ConsumedWaitIds(consumedWaitId)
                                },
                                commitCancellationToken).ConfigureAwait(false);
                            if (completed.Outcome != DurableCommandOutcome.Committed)
                            {
                                return Conflict(completed);
                            }

                            currentVersion = completed.StreamVersion;
                            commands++;
                            if (BudgetReached(context, commands, elapsed))
                            {
                                return new DurableSegmentResult(
                                    DurableSegmentOutcome.BudgetExhausted,
                                    CommittedProgress: true);
                            }

                            break;
                        case StepResult.Yield:
                        {
                            var fibers = new Dictionary<FiberId, FiberRecord>(execution.Fibers)
                            {
                                [fiber.Id] = ClearResume(fiber) with
                                {
                                    YieldCount = checked(fiber.YieldCount + 1)
                                }
                            };
                            RemoveConsumedObligation(ownedObligations, consumedWaitId);
                            execution = execution with
                            {
                                Fibers = fibers,
                                Scheduler = FiberScheduler.CompleteTurn(
                                    execution.Scheduler,
                                    fiber.Id,
                                    requeueSelected: true)
                            };
                            var yielded = await context.Processor.ProcessAsync(
                                new DurableYieldCommand(
                                    CommandId.New(),
                                    context.InstanceId,
                                    context.TimeProvider.GetUtcNow(),
                                    instruction.Path,
                                    BuildEnvelope(context, execution, state, ownedObligations))
                                {
                                    ExpectedStreamVersion = currentVersion,
                                    ConsumedResumeWaitIds = ConsumedWaitIds(consumedWaitId)
                                },
                                commitCancellationToken).ConfigureAwait(false);
                            return yielded.Outcome == DurableCommandOutcome.Committed
                                ? DurableSegmentResult.Yielded
                                : Conflict(yielded);
                        }
                        case StepResult.Failed failed:
                        {
                            var attempt = fiber.RetryAttempt > 0 ? fiber.RetryAttempt : 1;
                            if (instruction.Policy.Retry is { } retry && attempt < retry.MaxAttempts)
                            {
                                var nextAttempt = checked(attempt + 1);
                                var retryFiber = ClearResume(fiber) with
                                {
                                    RetryAttempt = nextAttempt,
                                    RetryNotBefore = retry.Backoff > TimeSpan.Zero
                                        ? context.TimeProvider.GetUtcNow().Add(retry.Backoff)
                                        : null,
                                    LogicalOperationKey = fiber.LogicalOperationKey ??
                                        LogicalOperationKey(execution, fiber, instruction)
                                };
                                RemoveConsumedObligation(ownedObligations, consumedWaitId);
                                if (retry.Backoff > TimeSpan.Zero)
                                {
                                    var timerId = TimerId.New();
                                    retryFiber = FiberReducer.Block(
                                        retryFiber,
                                        FiberBlockedReason.Retry,
                                        timerId.ToString());
                                    execution = execution with
                                    {
                                        Fibers = new Dictionary<FiberId, FiberRecord>(execution.Fibers)
                                        {
                                            [fiber.Id] = retryFiber
                                        },
                                        Scheduler = FiberScheduler.RemoveRunnable(
                                            execution.Scheduler,
                                            [fiber.Id])
                                    };
                                    ownedObligations.Add(new DurableOwnedObligationState
                                    {
                                        Kind = DurableOwnedObligationKind.Timer,
                                        ObligationId = timerId.ToString(),
                                        FiberId = fiber.Id.Value,
                                        ScopeId = fiber.OwningScopeId?.Value,
                                        RegistrationSequence = AllocateRegistrationSequence(ref execution)
                                    });
                                    var retryAt = retryFiber.RetryNotBefore!.Value;
                                    var scheduled = await context.Processor.ProcessAsync(
                                        new ScheduleTimerCommand
                                        {
                                            CommandId = CommandId.New(),
                                            InstanceId = context.InstanceId,
                                            RequestedAt = context.TimeProvider.GetUtcNow(),
                                            TimerId = timerId,
                                            FireAt = retryAt,
                                            WakeupName = $"{instruction.Path}:retry:{nextAttempt}",
                                            FiberId = fiber.Id,
                                            ScopeId = fiber.OwningScopeId,
                                            Envelope = BuildEnvelope(
                                                context,
                                                execution,
                                                state,
                                                ownedObligations),
                                            ExpectedStreamVersion = currentVersion
                                        },
                                        commitCancellationToken).ConfigureAwait(false);
                                    return scheduled.Outcome == DurableCommandOutcome.Committed
                                        ? DurableSegmentResult.Suspended
                                        : Conflict(scheduled);
                                }

                                execution = execution with
                                {
                                    Fibers = new Dictionary<FiberId, FiberRecord>(execution.Fibers)
                                    {
                                        [fiber.Id] = retryFiber
                                    },
                                    Scheduler = FiberScheduler.CompleteTurn(
                                        execution.Scheduler,
                                        fiber.Id,
                                        requeueSelected: true)
                                };
                                var retryCommitted = await context.Processor.ProcessAsync(
                                    new DurableYieldCommand(
                                        CommandId.New(),
                                        context.InstanceId,
                                        context.TimeProvider.GetUtcNow(),
                                        $"{instruction.Path}:retry:{nextAttempt}",
                                        BuildEnvelope(context, execution, state, ownedObligations))
                                    {
                                        ExpectedStreamVersion = currentVersion,
                                        ConsumedResumeWaitIds = ConsumedWaitIds(consumedWaitId)
                                    },
                                    commitCancellationToken).ConfigureAwait(false);
                                return retryCommitted.Outcome == DurableCommandOutcome.Committed
                                    ? DurableSegmentResult.PolicyBoundary
                                    : Conflict(retryCommitted);
                            }

                            var failure = new FiberFailure(
                                failed.Error.GetType().Name,
                                failed.Error.Message);
                            execution = FailFiberAndAncestors(execution, fiber, failure);
                            RemoveConsumedObligation(ownedObligations, consumedWaitId);
                            var cleanup = RemoveTerminalFiberObligations(
                                execution,
                                ownedObligations);
                            var failedResult = await context.Processor.ProcessAsync(
                                new DurableStepFailedCommand(
                                    CommandId.New(),
                                    context.InstanceId,
                                    context.TimeProvider.GetUtcNow(),
                                    instruction.Path,
                                    $"{failure.Code}: {failure.Message}",
                                    BuildEnvelope(context, execution, state, ownedObligations))
                                {
                                    ExpectedStreamVersion = currentVersion,
                                    ConsumedResumeWaitIds = ConsumedWaitIds(consumedWaitId),
                                    CancelWaitIds = cleanup.WaitIds,
                                    CancelTimerIds = cleanup.TimerIds,
                                    TerminalFiberIds = cleanup.TerminalFiberIds,
                                    FailedSagaScopeIds = FailedSagaScopes(execution),
                                    CoversRootSagaEligibility = RootFailed(execution)
                                },
                                commitCancellationToken).ConfigureAwait(false);
                            return failedResult.Outcome == DurableCommandOutcome.Committed
                                ? DurableSegmentResult.Terminal
                                : Conflict(failedResult);
                        }
                        case StepResult.WaitForEvent wait:
                        {
                            var registration = await RegisterOwnedWaitAsync(
                                context,
                                execution,
                                fiber,
                                instruction,
                                state,
                                ownedObligations,
                                wait.EventName,
                                wait.CorrelationId,
                                WaitMode.Resident,
                                currentVersion,
                                consumedWaitId,
                                commitCancellationToken).ConfigureAwait(false);
                            execution = registration.Execution;
                            if (registration.Commit.Outcome != DurableCommandOutcome.Committed)
                            {
                                return Conflict(registration.Commit);
                            }

                            currentVersion = registration.Commit.StreamVersion;
                            commands++;
                            if (BudgetReached(context, commands, elapsed))
                            {
                                return new DurableSegmentResult(
                                    DurableSegmentOutcome.BudgetExhausted,
                                    CommittedProgress: true);
                            }

                            break;
                        }
                        case StepResult.RunExternalJob externalJob:
                        {
                            var waitId = WaitId.New();
                            var blocked = FiberReducer.Block(
                                ClearResume(fiber),
                                FiberBlockedReason.ExternalJob,
                                waitId.ToString());
                            var fibers = new Dictionary<FiberId, FiberRecord>(execution.Fibers)
                            {
                                [fiber.Id] = blocked
                            };
                            execution = execution with
                            {
                                Fibers = fibers,
                                Scheduler = FiberScheduler.RemoveRunnable(
                                    execution.Scheduler,
                                    [fiber.Id])
                            };
                            RemoveConsumedObligation(ownedObligations, consumedWaitId);
                            var waitSequence = AllocateRegistrationSequence(ref execution);
                            ownedObligations.Add(new DurableOwnedObligationState
                            {
                                Kind = DurableOwnedObligationKind.ExternalJob,
                                ObligationId = waitId.ToString(),
                                FiberId = fiber.Id.Value,
                                ScopeId = fiber.OwningScopeId?.Value,
                                RegistrationSequence = waitSequence
                            });
                            var now = context.TimeProvider.GetUtcNow();
                            var dispatched = await context.Processor.ProcessAsync(
                                new RunExternalJobCommand
                                {
                                    CommandId = CommandId.New(),
                                    InstanceId = context.InstanceId,
                                    RequestedAt = now,
                                    ExternalJobId = externalJob.ExternalJobId,
                                    Payload = externalJob.Payload,
                                    Requirements = externalJob.Requirements ?? [],
                                    TimeoutAt = externalJob.Timeout is { } timeout
                                        ? now.Add(timeout)
                                        : null,
                                    WaitId = waitId,
                                    WaitSequence = waitSequence,
                                    FiberId = fiber.Id,
                                    ScopeId = fiber.OwningScopeId,
                                    Envelope = BuildEnvelope(
                                        context,
                                        execution,
                                        state,
                                        ownedObligations),
                                    ExpectedStreamVersion = currentVersion,
                                    ConsumedResumeWaitIds = ConsumedWaitIds(consumedWaitId)
                                },
                                commitCancellationToken).ConfigureAwait(false);
                            if (dispatched.Outcome != DurableCommandOutcome.Committed)
                            {
                                if (dispatched.Outcome != DurableCommandOutcome.NoOp)
                                {
                                    return Conflict(dispatched);
                                }

                                var reloaded = await ReloadAggregateAsync(context, cancellationToken)
                                    .ConfigureAwait(false);
                                var existing = reloaded.ExternalJobState.Find(externalJob.ExternalJobId);
                                var existingWait = existing is null
                                    ? null
                                    : reloaded.WaitState.ActiveWaits.FirstOrDefault(candidate =>
                                        candidate.WaitId == existing.WaitId);
                                if (existing is null ||
                                    existingWait is null ||
                                    existing.FiberId != fiber.Id ||
                                    existing.ScopeId != fiber.OwningScopeId)
                                {
                                    return await ParkAsync(
                                        context,
                                        DurableParkReason.Poison,
                                        $"External job '{externalJob.ExternalJobId}' is not owned by the selected fiber.",
                                        cancellationToken).ConfigureAwait(false);
                                }

                                RemoveConsumedObligation(ownedObligations, waitId);
                                ownedObligations.Add(new DurableOwnedObligationState
                                {
                                    Kind = DurableOwnedObligationKind.ExternalJob,
                                    ObligationId = existing.WaitId.ToString(),
                                    FiberId = fiber.Id.Value,
                                    ScopeId = fiber.OwningScopeId?.Value,
                                    RegistrationSequence = existingWait.WaitSequence
                                });
                                var rebound = FiberReducer.Block(
                                    ClearResume(fiber),
                                    FiberBlockedReason.ExternalJob,
                                    existing.WaitId.ToString());
                                execution = execution with
                                {
                                    Fibers = new Dictionary<FiberId, FiberRecord>(execution.Fibers)
                                    {
                                        [fiber.Id] = rebound
                                    },
                                    Scheduler = FiberScheduler.RemoveRunnable(
                                        execution.Scheduler,
                                        [fiber.Id])
                                };
                                var repaired = await context.Processor.ProcessAsync(
                                    new DurableStepCompletedCommand(
                                        CommandId.New(),
                                        context.InstanceId,
                                        context.TimeProvider.GetUtcNow(),
                                        $"{instruction.Path}:external-job-rebind",
                                        BuildEnvelope(context, execution, state, ownedObligations))
                                    {
                                        ExpectedStreamVersion = reloaded.StreamVersion
                                    },
                                    commitCancellationToken).ConfigureAwait(false);
                                return repaired.Outcome == DurableCommandOutcome.Committed
                                    ? DurableSegmentResult.Suspended
                                    : Conflict(repaired);
                            }

                            currentVersion = dispatched.StreamVersion;
                            commands++;
                            if (BudgetReached(context, commands, elapsed))
                            {
                                return new DurableSegmentResult(
                                    DurableSegmentOutcome.BudgetExhausted,
                                    CommittedProgress: true);
                            }

                            break;
                        }
                        case StepResult.AcquireResources acquire:
                        {
                            var waitId = WaitId.New();
                            var blocked = FiberReducer.Block(
                                ClearResume(fiber),
                                FiberBlockedReason.Resource,
                                waitId.ToString());
                            var fibers = new Dictionary<FiberId, FiberRecord>(execution.Fibers)
                            {
                                [fiber.Id] = blocked
                            };
                            execution = execution with
                            {
                                Fibers = fibers,
                                Scheduler = FiberScheduler.RemoveRunnable(
                                    execution.Scheduler,
                                    [fiber.Id])
                            };
                            RemoveConsumedObligation(ownedObligations, consumedWaitId);
                            var waitSequence = AllocateRegistrationSequence(ref execution);
                            ownedObligations.Add(new DurableOwnedObligationState
                            {
                                Kind = DurableOwnedObligationKind.Resource,
                                ObligationId = waitId.ToString(),
                                FiberId = fiber.Id.Value,
                                ScopeId = fiber.OwningScopeId?.Value,
                                RegistrationSequence = waitSequence
                            });
                            var now = context.TimeProvider.GetUtcNow();
                            var acquired = await context.Processor.ProcessAsync(
                                new AcquireResourcePoolCommand
                                {
                                    CommandId = CommandId.New(),
                                    InstanceId = context.InstanceId,
                                    RequestedAt = now,
                                    HolderKey = acquire.HolderKey,
                                    Requirements = acquire.Requirements,
                                    ExpiresAt = acquire.LeaseDuration is { } lease
                                        ? now.Add(lease)
                                        : null,
                                    WaitId = waitId,
                                    WaitSequence = waitSequence,
                                    FiberId = fiber.Id,
                                    ScopeId = fiber.OwningScopeId,
                                    Envelope = BuildEnvelope(
                                        context,
                                        execution,
                                        state,
                                        ownedObligations),
                                    ExpectedStreamVersion = currentVersion,
                                    ConsumedResumeWaitIds = ConsumedWaitIds(consumedWaitId)
                                },
                                commitCancellationToken).ConfigureAwait(false);
                            if (acquired.Outcome != DurableCommandOutcome.Committed)
                            {
                                return acquired.Outcome == DurableCommandOutcome.NoOp
                                    ? await ParkAsync(
                                        context,
                                        DurableParkReason.Poison,
                                        $"Resource acquisition for '{acquire.HolderKey}' was rejected.",
                                        commitCancellationToken).ConfigureAwait(false)
                                    : Conflict(acquired);
                            }

                            currentVersion = acquired.StreamVersion;
                            commands++;
                            if (BudgetReached(context, commands, elapsed))
                            {
                                return new DurableSegmentResult(
                                    DurableSegmentOutcome.BudgetExhausted,
                                    CommittedProgress: true);
                            }

                            var reloaded = await ReloadAggregateAsync(context, cancellationToken)
                                .ConfigureAwait(false);
                            if (reloaded.StreamVersion != currentVersion)
                            {
                                return new DurableSegmentResult(
                                    DurableSegmentOutcome.Conflict,
                                    "Resource acquisition stream moved before result inspection.");
                            }

                            if (reloaded.WaitState.HasWait(waitId))
                            {
                                break;
                            }

                            RemoveConsumedObligation(ownedObligations, waitId);
                            var advanced = FiberReducer.Resume(blocked) with
                            {
                                InstructionId = RequiredNext(instruction)
                            };
                            execution = execution with
                            {
                                Fibers = new Dictionary<FiberId, FiberRecord>(execution.Fibers)
                                {
                                    [fiber.Id] = advanced
                                },
                                Scheduler = FiberScheduler.EnqueueResumed(
                                    execution.Scheduler,
                                    [fiber.Id])
                            };
                            var advancedCommit = await context.Processor.ProcessAsync(
                                new DurableStepCompletedCommand(
                                    CommandId.New(),
                                    context.InstanceId,
                                    context.TimeProvider.GetUtcNow(),
                                    instruction.Path,
                                    BuildEnvelope(context, execution, state, ownedObligations))
                                {
                                    ExpectedStreamVersion = currentVersion
                                },
                                commitCancellationToken).ConfigureAwait(false);
                            if (advancedCommit.Outcome != DurableCommandOutcome.Committed)
                            {
                                return Conflict(advancedCommit);
                            }

                            currentVersion = advancedCommit.StreamVersion;
                            commands++;
                            if (BudgetReached(context, commands, elapsed))
                            {
                                return new DurableSegmentResult(
                                    DurableSegmentOutcome.BudgetExhausted,
                                    CommittedProgress: true);
                            }

                            break;
                        }
                        default:
                            throw new NotSupportedException(
                                $"Step result '{result.GetType().Name}' is not yet supported by the fiber driver.");
                    }

                    break;
                }
                case CompiledInstructionKind.StartScope:
                {
                    execution = StartScope(execution, fiber, instruction, state);
                    var started = await CommitScopeStartAsync(
                        context, execution, state, ownedObligations, instruction, currentVersion, cancellationToken)
                        .ConfigureAwait(false);
                    if (started.Outcome != DurableCommandOutcome.Committed)
                    {
                        return Conflict(started);
                    }

                    currentVersion = started.StreamVersion;
                    commands++;
                    if (BudgetReached(context, commands, elapsed))
                    {
                        return new DurableSegmentResult(
                            DurableSegmentOutcome.BudgetExhausted,
                            CommittedProgress: true);
                    }

                    break;
                }
                case CompiledInstructionKind.BranchReturn:
                {
                    var branchReturn = await CommitBranchReturnAsync(
                        context,
                        execution,
                        state,
                        fiber,
                        instruction,
                        ownedObligations,
                        consumedWaitId,
                        currentVersion,
                        cancellationToken).ConfigureAwait(false);
                    if (branchReturn.Terminal is { } terminal)
                    {
                        return terminal;
                    }

                    execution = branchReturn.Execution;
                    currentVersion = branchReturn.StreamVersion;
                    commands++;
                    if (BudgetReached(context, commands, elapsed))
                    {
                        return new DurableSegmentResult(
                            DurableSegmentOutcome.BudgetExhausted,
                            CommittedProgress: true);
                    }

                    break;
                }
                case CompiledInstructionKind.ContinueAsNew:
                    return await ContinueAsNewAsync(
                        context,
                        execution,
                        state,
                        fiber,
                        instruction,
                        ownedObligations,
                        currentVersion,
                        cancellationToken).ConfigureAwait(false);
                case CompiledInstructionKind.End:
                    return await CompleteAsync(
                        context,
                        execution,
                        state,
                        fiber,
                        instruction,
                        ownedObligations,
                        consumedWaitId,
                        currentVersion,
                        cancellationToken).ConfigureAwait(false);
                default:
                    throw new NotSupportedException(
                        $"Compiled instruction '{instruction.Kind}' is not yet supported by the fiber driver.");
            }
        }
    }

}
