using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Core.Building;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Definitions;
using OrcaCore.Core.Execution;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Outbox;
using OrcaCore.Engine.Durable.ResourceGovernance;

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
        var leaseReconciliation = await ReconcileLeaseTicketsAsync(
            context,
            execution,
            state,
            ownedObligations,
            currentVersion,
            cancellationToken).ConfigureAwait(false);
        if (leaseReconciliation is not null)
        {
            return leaseReconciliation;
        }

        if (execution.WorkflowDeadline is { } workflowDeadline)
        {
            var now = context.TimeProvider.GetUtcNow();
            if (workflowDeadline <= now)
            {
                return await TimeoutWorkflowAsync(
                    context,
                    execution,
                    state,
                    ownedObligations,
                    currentVersion,
                    cancellationToken).ConfigureAwait(false);
            }

            if (execution.WorkflowDeadlineTimerId is null)
            {
                var timerId = TimerId.New();
                execution = execution with { WorkflowDeadlineTimerId = timerId };
                var scheduled = await context.Processor.ProcessAsync(
                    new ScheduleTimerCommand
                    {
                        CommandId = CommandId.New(),
                        InstanceId = context.InstanceId,
                        RequestedAt = now,
                        TimerId = timerId,
                        FireAt = workflowDeadline,
                        WakeupName = "workflow-deadline",
                        Envelope = BuildEnvelope(context, execution, state, ownedObligations),
                        ExpectedStreamVersion = currentVersion
                    },
                    cancellationToken).ConfigureAwait(false);
                return scheduled.Outcome == DurableCommandOutcome.Committed
                    ? DurableSegmentResult.PolicyBoundary
                    : Conflict(scheduled);
            }
        }

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
            execution = ReconcileAdmission(execution);
            if (BudgetReached(context, commands, elapsed))
            {
                return new DurableSegmentResult(
                    DurableSegmentOutcome.BudgetExhausted,
                    CommittedProgress: commands > 0);
            }
            var selected = FiberScheduler.SelectNext(execution.Scheduler);
            if (selected is null)
            {
                var resolution = await ResolveNoRunnableFiberAsync(
                    context,
                    execution,
                    state,
                    ownedObligations,
                    currentVersion,
                    commands,
                    elapsed,
                    quantumBudget,
                    cancellationToken).ConfigureAwait(false);
                if (resolution.Result is { } result)
                {
                    return result;
                }
                execution = resolution.Execution;
                state = resolution.State;
                currentVersion = resolution.StreamVersion;
                commands = resolution.Commands;
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
            if (instruction.Kind is CompiledInstructionKind.Step or CompiledInstructionKind.Publish)
            {
                quantumBudget.EndTurn();
            }

            var resumedEvent = TakeResumedEvent(
                plan,
                fiber,
                ownedObligations,
                context.Aggregate.WaitState.PendingResumes,
                out var consumedWaitId);
            if (resumedEvent is not null)
            {
                fiber = fiber with { CurrentCausationEventId = resumedEvent.EventId.Value };
                execution = execution with
                {
                    Fibers = new Dictionary<FiberId, FiberRecord>(execution.Fibers)
                    {
                        [fiber.Id] = fiber
                    }
                };
            }
            switch (instruction.Kind)
            {
                case CompiledInstructionKind.Init:
                    (execution, state) = ExecuteInitInstruction(context, execution, fiber, instruction);
                    break;
                case CompiledInstructionKind.Wait:
                {
                    if (instruction.Operation is null ||
                        instruction.EventContract is null ||
                        instruction.WaitMode is not { } waitMode)
                    {
                        throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException(
                            $"Compiled wait '{instruction.Path}' has no typed executable binding.");
                    }

                    var registration = await RegisterOwnedWaitAsync(
                        context,
                        execution,
                        fiber,
                        instruction,
                        state,
                        ownedObligations,
                        instruction.EventContract,
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
                    if (registration.MatchedPendingEvent)
                    {
                        return new DurableSegmentResult(
                            DurableSegmentOutcome.Yielded,
                            CommittedProgress: true);
                    }

                    if (BudgetReached(context, commands, elapsed))
                    {
                        return new DurableSegmentResult(
                            DurableSegmentOutcome.BudgetExhausted,
                            CommittedProgress: true);
                    }

                    break;
                }
                case CompiledInstructionKind.Publish:
                {
                    var eventContract = instruction.EventContract ??
                        throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException(
                            $"Compiled publish '{instruction.Path}' has no event contract.");
                    var definitionId = context.Aggregate.DefinitionId ??
                        throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException(
                            "A durable publish cannot execute before its definition identity is available.");
                    var definitionVersion = context.Aggregate.DefinitionVersion ??
                        throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException(
                            "A durable publish cannot execute before its definition version is available.");
                    var resolved = ResolvePublish(execution, fiber, state, instruction);
                    var now = context.TimeProvider.GetUtcNow();
                    var outboundEventId = EventId.Create(Guid.CreateVersion7().ToString());
                    var outbound = new DurableWorkflowOutboundEventData(
                        eventContract.EventName.Value,
                        eventContract.Version.Value,
                        instruction.PublishPayloadType?.AssemblyQualifiedName,
                        instruction.PublishPayloadSchemaIdentity,
                        outboundEventId.Value,
                        resolved.CorrelationId.Value,
                        fiber.CurrentCausationEventId,
                        now,
                        context.InstanceId.Value.ToString(),
                        definitionId.Value.ToString(),
                        definitionVersion.Value,
                        resolved.Payload);
                    fiber = ClearResume(fiber);
                    execution = CompleteStepTurn(execution, fiber, instruction);
                    RemoveConsumedObligation(ownedObligations, consumedWaitId);
                    var published = await context.Processor.ProcessAsync(
                        new DurableStepCompletedCommand(
                            CommandId.New(),
                            context.InstanceId,
                            now,
                            instruction.Path,
                            BuildEnvelope(context, execution, state, ownedObligations))
                        {
                            ExpectedStreamVersion = currentVersion,
                            ConsumedResumeWaitIds = ConsumedWaitIds(consumedWaitId),
                            OutboxRecords =
                            [
                                new OutboxWrite(
                                    OutboxRecordId.New(),
                                    OutboxKinds.WorkflowEvent,
                                    DurableWorkflowOutboundEventCodec.Encode(outbound))
                            ]
                        },
                        CancellationToken.None).ConfigureAwait(false);
                    if (published.Outcome != DurableCommandOutcome.Committed)
                    {
                        return Conflict(published);
                    }

                    currentVersion = published.StreamVersion;
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
                        throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException(
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
                case CompiledInstructionKind.AcquireResources:
                {
                    var lease = FindScopedLease(fiber, ownedObligations);
                    if (lease is null)
                    {
                        if (HasCapacityReservingLeaseAncestor(execution, fiber, ownedObligations))
                        {
                            var rejected = await context.Processor.ProcessAsync(
                                new DurableStepFailedCommand(
                                    CommandId.New(),
                                    context.InstanceId,
                                    context.TimeProvider.GetUtcNow(),
                                    instruction.Path,
                                    "SFE-RUN-002: LeaseAncestryViolation.",
                                    BuildEnvelope(context, execution, state, ownedObligations))
                                {
                                    ExpectedStreamVersion = currentVersion,
                                    PreserveOwnership = true
                                },
                                CancellationToken.None).ConfigureAwait(false);
                            return rejected.Outcome == DurableCommandOutcome.Committed
                                ? DurableSegmentResult.Terminal
                                : Conflict(rejected);
                        }

                        var request = NormalizeLeaseRequest(
                            ResolveLeaseRequest(execution, fiber, instruction, state));
                        await context.Processor.ValidateResourcePoolsAsync(
                            request,
                            cancellationToken).ConfigureAwait(false);
                        var occurrenceKey = LeaseOccurrenceKey(execution, fiber, instruction);
                        var waitId = LeaseWaitId(occurrenceKey);
                        lease = new DurableOwnedObligationState
                        {
                            Kind = DurableOwnedObligationKind.Resource,
                            ObligationId = waitId.ToString(),
                            FiberId = fiber.Id.Value,
                            ScopeId = fiber.OwningScopeId?.Value,
                            InstructionId = instruction.Id.Value,
                            AuthoredPath = instruction.Path,
                            LeasePhase = nameof(DurableLeaseObligationPhase.Queued),
                            HolderKey = LeaseHolderKey(occurrenceKey),
                            ProtectionToken = LeaseProtectionKey(occurrenceKey),
                            LeaseRequirements = request,
                            RegistrationSequence = AllocateRegistrationSequence(ref execution)
                        };
                        ownedObligations.Add(lease);
                        execution = execution with
                        {
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
                                $"{instruction.Path}:lease-request-committed",
                                BuildEnvelope(context, execution, state, ownedObligations))
                            {
                                ExpectedStreamVersion = currentVersion
                            },
                            cancellationToken).ConfigureAwait(false);
                        if (admitted.Outcome != DurableCommandOutcome.Committed)
                        {
                            return Conflict(admitted);
                        }

                        await context.Processor.ReportLeaseBarrierAsync(
                            DurableResourceLeaseCommitBarrier.WorkflowPendingObligationCommitted,
                            context.InstanceId,
                            checked((int)execution.ContinueAsNewGeneration),
                            lease.ObligationId,
                            lease.FiberId,
                            lease.ScopeId ?? "root",
                            lease.ProtectionToken!,
                            admitted.StreamVersion.Value,
                            [],
                            cancellationToken).ConfigureAwait(false);
                        currentVersion = admitted.StreamVersion;
                        commands++;
                        continue;
                    }

                    var wait = WaitId.Parse(lease.ObligationId);
                    var blocked = FiberReducer.Block(
                        ClearResume(fiber),
                        FiberBlockedReason.Resource,
                        wait.ToString());
                    execution = execution with
                    {
                        Fibers = new Dictionary<FiberId, FiberRecord>(execution.Fibers)
                        {
                            [fiber.Id] = blocked
                        },
                        Scheduler = FiberScheduler.RemoveRunnable(execution.Scheduler, [fiber.Id])
                    };
                    var leaseIndex = ownedObligations.IndexOf(lease);
                    lease = lease with
                    {
                        LeasePhase = nameof(DurableLeaseObligationPhase.PendingCommit)
                    };
                    ownedObligations[leaseIndex] = lease;
                    var now = context.TimeProvider.GetUtcNow();
                    var acquired = await context.Processor.ProcessAsync(
                        new AcquireResourcePoolCommand
                        {
                            CommandId = CommandId.New(),
                            InstanceId = context.InstanceId,
                            RequestedAt = now,
                            HolderKey = lease.HolderKey!,
                            Requirements = lease.LeaseRequirements,
                            ExpiresAt = null,
                            WaitId = wait,
                            WaitSequence = lease.RegistrationSequence,
                            FiberId = fiber.Id,
                            ScopeId = fiber.OwningScopeId,
                            Envelope = BuildEnvelope(context, execution, state, ownedObligations),
                            ExpectedStreamVersion = currentVersion,
                            ConsumedResumeWaitIds = ConsumedWaitIds(consumedWaitId),
                            LeaseObligationId = lease.ObligationId,
                            LeaseProtectionToken = lease.ProtectionToken,
                            LeaseGeneration = checked((int)execution.ContinueAsNewGeneration),
                            LeaseFiberOccurrence = lease.FiberId,
                            LeaseScopeOccurrence = lease.ScopeId ?? "root"
                        },
                        CancellationToken.None).ConfigureAwait(false);
                    if (acquired.Outcome != DurableCommandOutcome.Committed)
                    {
                        return acquired.Outcome == DurableCommandOutcome.NoOp
                            ? await ParkAsync(
                                context,
                                DurableParkReason.Poison,
                                $"Scoped resource acquisition for '{lease.HolderKey}' was rejected.",
                                CancellationToken.None).ConfigureAwait(false)
                            : Conflict(acquired);
                    }

                    currentVersion = acquired.StreamVersion;
                    commands++;
                    var reloaded = await ReloadAggregateAsync(context, cancellationToken)
                        .ConfigureAwait(false);
                    if (reloaded.StreamVersion != currentVersion)
                    {
                        return new DurableSegmentResult(
                            DurableSegmentOutcome.Conflict,
                            "Scoped resource acquisition stream moved before result inspection.");
                    }

                    if (reloaded.WaitState.HasWait(wait))
                    {
                        ownedObligations[leaseIndex] = lease with
                        {
                            LeasePhase = nameof(DurableLeaseObligationPhase.Queued)
                        };
                        var queued = await context.Processor.ProcessAsync(
                            new DurableYieldCommand(
                                CommandId.New(),
                                context.InstanceId,
                                context.TimeProvider.GetUtcNow(),
                                $"{instruction.Path}:lease-queued",
                                BuildEnvelope(context, execution, state, ownedObligations))
                            {
                                ExpectedStreamVersion = currentVersion
                            },
                            CancellationToken.None).ConfigureAwait(false);
                        if (queued.Outcome != DurableCommandOutcome.Committed)
                        {
                            return Conflict(queued);
                        }

                        currentVersion = queued.StreamVersion;
                        commands++;
                        if (BudgetReached(context, commands, elapsed))
                        {
                            return new DurableSegmentResult(
                                DurableSegmentOutcome.BudgetExhausted,
                                CommittedProgress: true);
                        }

                        // A resource miss parks only its exact fiber. Other roots, branches, or
                        // items that remain runnable continue within this segment. Once none
                        // remain, ResolveNoRunnableFiberAsync returns the suspended boundary.
                        continue;
                    }

                    var acquiredLeaseTickets = reloaded.ResourcePoolState.ActiveTickets
                        .Where(ticket =>
                            string.Equals(
                                ticket.HolderKey,
                                lease.HolderKey,
                                StringComparison.Ordinal))
                        .OrderBy(ticket => ticket.PoolName, StringComparer.Ordinal)
                        .ThenBy(ticket => ticket.ProviderGeneration)
                        .ToArray();
                    ownedObligations[leaseIndex] = lease with
                    {
                        LeasePhase = nameof(DurableLeaseObligationPhase.Held),
                        LeaseTickets = acquiredLeaseTickets
                            .Select(ticket => new DurableLeaseTicketState
                            {
                                TicketId = ticket.TicketId.ToString("N"),
                                PoolName = ticket.PoolName,
                                Units = ticket.Count,
                                ProviderGeneration = ticket.ProviderGeneration,
                                ReviewDeadline = ticket.ReviewDeadline,
                                ReviewMarked = ticket.ReviewMarked
                            })
                            .ToArray()
                    };
                    RemoveConsumedObligation(ownedObligations, consumedWaitId);
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
                        Scheduler = FiberScheduler.EnqueueResumed(execution.Scheduler, [fiber.Id])
                    };
                    var activated = await context.Processor.ProcessAsync(
                        new DurableStepCompletedCommand(
                            CommandId.New(),
                            context.InstanceId,
                            context.TimeProvider.GetUtcNow(),
                            $"{instruction.Path}:lease-activated",
                            BuildEnvelope(context, execution, state, ownedObligations))
                        {
                            ExpectedStreamVersion = currentVersion
                        },
                        CancellationToken.None).ConfigureAwait(false);
                    if (activated.Outcome != DurableCommandOutcome.Committed)
                    {
                        return Conflict(activated);
                    }

                    currentVersion = activated.StreamVersion;
                    commands++;
                    await context.Processor.ReportLeaseBarrierAsync(
                        DurableResourceLeaseCommitBarrier.WorkflowActivationCommitted,
                        context.InstanceId,
                        checked((int)execution.ContinueAsNewGeneration),
                        lease.ObligationId,
                        lease.FiberId,
                        lease.ScopeId ?? "root",
                        lease.ProtectionToken!,
                        activated.StreamVersion.Value,
                        acquiredLeaseTickets,
                        cancellationToken).ConfigureAwait(false);
                    await context.Processor.ReportLeaseBarrierAsync(
                        DurableResourceLeaseCommitBarrier.GovernanceOwnershipConfirmed,
                        context.InstanceId,
                        checked((int)execution.ContinueAsNewGeneration),
                        lease.ObligationId,
                        lease.FiberId,
                        lease.ScopeId ?? "root",
                        lease.ProtectionToken!,
                        activated.StreamVersion.Value,
                        acquiredLeaseTickets,
                        cancellationToken).ConfigureAwait(false);
                    break;
                }
                case CompiledInstructionKind.ReleaseResources:
                {
                    var lease = FindScopedLease(fiber, ownedObligations);
                    if (lease is null ||
                        lease.LeasePhase is not (
                            nameof(DurableLeaseObligationPhase.Held) or
                            nameof(DurableLeaseObligationPhase.ReviewMarked) or
                            nameof(DurableLeaseObligationPhase.AmbiguousHeld)) ||
                        string.IsNullOrWhiteSpace(lease.HolderKey))
                    {
                        var rejected = await context.Processor.ProcessAsync(
                            new DurableStepFailedCommand(
                                CommandId.New(),
                                context.InstanceId,
                                context.TimeProvider.GetUtcNow(),
                                instruction.Path,
                                "SFE-RUN-002: LeaseAncestryViolation.",
                                BuildEnvelope(context, execution, state, ownedObligations))
                            {
                                ExpectedStreamVersion = currentVersion,
                                PreserveOwnership = true
                            },
                            CancellationToken.None).ConfigureAwait(false);
                        return rejected.Outcome == DurableCommandOutcome.Committed
                            ? DurableSegmentResult.Terminal
                            : Conflict(rejected);
                    }

                    var quarantine = lease.LeasePhase ==
                        nameof(DurableLeaseObligationPhase.AmbiguousHeld);
                    if (quarantine)
                    {
                        var leaseIndex = ownedObligations.IndexOf(lease);
                        ownedObligations[leaseIndex] = lease with
                        {
                            LeasePhase = nameof(DurableLeaseObligationPhase.Quarantined)
                        };
                    }
                    else
                    {
                        var leaseIndex = ownedObligations.IndexOf(lease);
                        ownedObligations[leaseIndex] = lease with
                        {
                            LeasePhase = nameof(DurableLeaseObligationPhase.Released),
                            AcceptedConfirmationId = null
                        };
                    }

                    if (fiber.ResultPayload is not null && fiber.OwningScopeId is not null)
                    {
                        execution = ReturnBranch(execution, fiber).State;
                    }
                    else
                    {
                        execution = MoveTo(execution, fiber, RequiredNext(instruction));
                    }

                    var cleanup = RemoveTerminalFiberObligations(execution, ownedObligations);
                    var released = await context.Processor.ProcessAsync(
                        new DurableStepCompletedCommand(
                            CommandId.New(),
                            context.InstanceId,
                            context.TimeProvider.GetUtcNow(),
                            instruction.Path,
                            BuildEnvelope(context, execution, state, ownedObligations))
                        {
                            ExpectedStreamVersion = currentVersion,
                            ReleaseResourceHolderKeys = quarantine ? [] : [lease.HolderKey],
                            CancelWaitIds = cleanup.WaitIds,
                            CancelTimerIds = cleanup.TimerIds,
                            TerminalFiberIds = cleanup.TerminalFiberIds
                        },
                        CancellationToken.None).ConfigureAwait(false);
                    if (released.Outcome != DurableCommandOutcome.Committed)
                    {
                        return Conflict(released);
                    }

                    currentVersion = released.StreamVersion;
                    commands++;
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
                    if (!fiber.AttemptInFlight)
                    {
                        var now = context.TimeProvider.GetUtcNow();
                        var admittedFiber = fiber with
                        {
                            RetryAttempt = fiber.RetryAttempt > 0 ? fiber.RetryAttempt : 1,
                            TimeoutDeadline = instruction.Policy.Timeout is { } stepTimeout
                                ? now.Add(stepTimeout)
                                : null,
                            LogicalOperationKey = fiber.LogicalOperationKey ??
                                LogicalOperationKey(execution, fiber, instruction),
                            AttemptInFlight = true
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
                                now,
                                $"{instruction.Path}:attempt-admission",
                                BuildEnvelope(context, execution, state, ownedObligations))
                            {
                                ExpectedStreamVersion = currentVersion
                            },
                            cancellationToken).ConfigureAwait(false);
                        if (admitted.Outcome != DurableCommandOutcome.Committed)
                        {
                            return Conflict(admitted);
                        }

                        if (instruction.Policy.Timeout is not null)
                        {
                            return DurableSegmentResult.PolicyBoundary;
                        }

                        currentVersion = admitted.StreamVersion;
                        commands++;
                        continue;
                    }

                    var stepThrottleOwner = new StepThrottleOwner(context.InstanceId, fiber.Id);
                    if (!grantedStepThrottles.TryRemove(stepThrottleOwner, out var stepThrottleLease) &&
                        !stepThrottles.TryEnter(instruction.StepType, out stepThrottleLease))
                    {
                        var obligation = StepThrottleObligation(fiber, instruction);
                        var blockedFiber = FiberReducer.Block(
                            fiber,
                            FiberBlockedReason.Resource,
                            obligation);
                        execution = execution with
                        {
                            Fibers = new Dictionary<FiberId, FiberRecord>(execution.Fibers)
                            {
                                [fiber.Id] = blockedFiber
                            },
                            Scheduler = FiberScheduler.CompleteTurn(
                                execution.Scheduler,
                                fiber.Id,
                                requeueSelected: false)
                        };
                        var blocked = await context.Processor.ProcessAsync(
                            new DurableYieldCommand(
                                CommandId.New(),
                                context.InstanceId,
                                context.TimeProvider.GetUtcNow(),
                                $"{instruction.Path}:step-throttle-wait",
                                BuildEnvelope(context, execution, state, ownedObligations))
                            {
                                ExpectedStreamVersion = currentVersion
                            },
                            cancellationToken).ConfigureAwait(false);
                        if (blocked.Outcome != DurableCommandOutcome.Committed)
                        {
                            return Conflict(blocked);
                        }

                        currentVersion = blocked.StreamVersion;
                        commands++;
                        continue;
                    }

                    await using var ownedStepThrottleLease = stepThrottleLease;
                    var executed = await ExecutePolicyStepAsync(
                        context,
                        execution,
                        fiber,
                        instruction,
                        state,
                        resumedEvent,
                        ownedObligations,
                        ownedStepThrottleLease,
                        cancellationToken).ConfigureAwait(false);
                    execution = executed.Execution;
                    fiber = executed.Fiber;
                    state = executed.State;
                    if (executed.OperatorCancelled)
                    {
                        var cancelled = await context.Processor.FinalizeCancellationAsync(
                            context.InstanceId,
                            context.TimeProvider.GetUtcNow(),
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
                        case StepResult.Failed failed:
                        {
                            var scopedLease = FindScopedLease(fiber, ownedObligations);
                            if (scopedLease is not null &&
                                string.Equals(failed.Error.Code, "WF-STEP-TIMEOUT", StringComparison.Ordinal) &&
                                scopedLease.LeasePhase is
                                    nameof(DurableLeaseObligationPhase.Held) or
                                    nameof(DurableLeaseObligationPhase.ReviewMarked))
                            {
                                var scopedLeaseIndex = ownedObligations.IndexOf(scopedLease);
                                ownedObligations[scopedLeaseIndex] = scopedLease with
                                {
                                    LeasePhase = nameof(DurableLeaseObligationPhase.AmbiguousHeld)
                                };
                            }

                            var attempt = fiber.RetryAttempt > 0 ? fiber.RetryAttempt : 1;
                            if (instruction.Policy.Retry is { } retry &&
                                attempt < retry.MaxAttempts &&
                                IsRetryEligible(failed.Error))
                            {
                                var nextAttempt = checked(attempt + 1);
                                var retryAdmissionAt = context.TimeProvider.GetUtcNow()
                                    .Add(retry.Backoff);
                                var retryFiber = ClearResume(fiber) with
                                {
                                    RetryAttempt = nextAttempt,
                                    RetryNotBefore = retry.Backoff > TimeSpan.Zero
                                        ? retryAdmissionAt
                                        : null,
                                    LogicalOperationKey = fiber.LogicalOperationKey ??
                                        LogicalOperationKey(execution, fiber, instruction),
                                    AttemptInFlight = true,
                                    TimeoutDeadline = instruction.Policy.Timeout is { } retryTimeout
                                        ? retryAdmissionAt.Add(retryTimeout)
                                        : null
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

                            var failure = FailureProvenance.Create(
                                plan,
                                execution,
                                fiber,
                                instruction,
                                failed.Error.Code,
                                failed.Error.Message);
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

                            RemoveConsumedObligation(ownedObligations, consumedWaitId);
                            var cleanup = RemoveTerminalFiberObligations(
                                execution,
                                ownedObligations);
                            if (!RootFailed(execution))
                            {
                                var fiberFailed = await context.Processor.ProcessAsync(
                                    new DurableFiberFailedCommand(
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
                                        TerminalFiberIds = cleanup.TerminalFiberIds
                                    },
                                    commitCancellationToken).ConfigureAwait(false);
                                return fiberFailed.Outcome == DurableCommandOutcome.Committed
                                    ? DurableSegmentResult.Yielded
                                    : Conflict(fiberFailed);
                            }

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
                                    TerminalFiberIds = cleanup.TerminalFiberIds
                                },
                                commitCancellationToken).ConfigureAwait(false);
                            return failedResult.Outcome == DurableCommandOutcome.Committed
                                ? DurableSegmentResult.Terminal
                                : Conflict(failedResult);
                        }
                        case var waitResult when StepResultWaitAccessor.TryGetWait(
                            waitResult,
                            out var waitEventContract,
                            out var waitCorrelationId):
                        {
                            fiber = ClearStepPolicyState(ClearResume(fiber));
                            execution = execution with
                            {
                                Fibers = new Dictionary<FiberId, FiberRecord>(execution.Fibers)
                                {
                                    [fiber.Id] = fiber
                                }
                            };
                            var registration = await RegisterOwnedWaitAsync(
                                context,
                                execution,
                                fiber,
                                instruction,
                                state,
                                ownedObligations,
                                waitEventContract,
                                waitCorrelationId,
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
                            if (registration.MatchedPendingEvent)
                            {
                                return new DurableSegmentResult(
                                    DurableSegmentOutcome.Yielded,
                                    CommittedProgress: true);
                            }

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
