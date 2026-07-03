using System.Security.Cryptography;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Engine.Durable.Aggregates;

internal sealed class DurableWorkflowAggregate
{
    private readonly List<DurableActiveTimer> activeTimers;
    private readonly List<DurableBufferedTimer> bufferedTimers;
    private readonly List<DurableActiveChild> activeChildren;
    private readonly List<DurableActiveChildGroup> activeChildGroups;
    private readonly List<DurableCompletedChild> completedChildren = [];
    private readonly List<ResourcePoolTicket> activeResourceTickets;
    private readonly DurableExternalJobState externalJobState;
    private readonly DurableWaitState waitState;
    private readonly DurableSagaState sagaState;
    private readonly HashSet<string> compensatedChildGroups = new(StringComparer.Ordinal);
    private readonly HashSet<EventId> recordedParentResumeTokens = [];
    private readonly HashSet<EventId> consumedParentResumeTokens = [];

    private DurableWorkflowAggregate(
        InstanceId instanceId,
        StreamVersion streamVersion,
        InstanceId? parentInstanceId,
        InstanceId? rootInstanceId,
        DefinitionId? definitionId,
        DefinitionVersion? definitionVersion,
        WorkflowStatus? status,
        DateTimeOffset? createdAt,
        DateTimeOffset? updatedAt,
        string? lastStepPath,
        string? errorSummary,
        string? outcomeName,
        int continueAsNewGeneration,
        IEnumerable<DurableActiveTimer> activeTimers,
        IEnumerable<DurableActiveWait> activeWaits,
        IEnumerable<DurableBufferedDelivery> bufferedDeliveries,
        IEnumerable<DurableBufferedTimer> bufferedTimers,
        IEnumerable<DurableActiveChild> activeChildren,
        IEnumerable<DurableActiveChildGroup> activeChildGroups,
        IEnumerable<ResourcePoolTicket> activeResourceTickets,
        IEnumerable<DurableActiveExternalJob> activeExternalJobs,
        IEnumerable<DurableSagaForwardAction> completedSagaForwardActions,
        IEnumerable<DurableSagaCompensationAction> sagaCompensationActions,
        IEnumerable<DurableSagaRecoveryIntervention> sagaRecoveryInterventions,
        IEnumerable<string> requestedSagaCompensationScopes,
        IEnumerable<EventId> recordedParentResumeTokens,
        IEnumerable<EventId> consumedParentResumeTokens)
    {
        InstanceId = instanceId;
        StreamVersion = streamVersion;
        ParentInstanceId = parentInstanceId;
        RootInstanceId = rootInstanceId;
        DefinitionId = definitionId;
        DefinitionVersion = definitionVersion;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        LastStepPath = lastStepPath;
        ErrorSummary = errorSummary;
        OutcomeName = outcomeName;
        ContinueAsNewGeneration = continueAsNewGeneration;
        this.activeTimers = [.. activeTimers];
        waitState = DurableWaitState.FromSnapshot(activeWaits, bufferedDeliveries);
        this.bufferedTimers = [.. bufferedTimers];
        this.activeChildren = [.. activeChildren];
        this.activeChildGroups = [.. activeChildGroups];
        this.activeResourceTickets = [.. activeResourceTickets];
        externalJobState = DurableExternalJobState.FromSnapshot(activeExternalJobs);
        sagaState = DurableSagaState.FromSnapshot(
            completedSagaForwardActions,
            sagaCompensationActions,
            sagaRecoveryInterventions,
            requestedSagaCompensationScopes);
        this.recordedParentResumeTokens = [.. recordedParentResumeTokens];
        this.consumedParentResumeTokens = [.. consumedParentResumeTokens];
    }

    internal InstanceId InstanceId { get; private set; }

    internal StreamVersion StreamVersion { get; private set; }

    internal InstanceId? ParentInstanceId { get; private set; }

    internal InstanceId? RootInstanceId { get; private set; }

    internal DefinitionId? DefinitionId { get; private set; }

    internal DefinitionVersion? DefinitionVersion { get; private set; }

    internal WorkflowStatus? Status { get; private set; }

    internal DateTimeOffset? CreatedAt { get; private set; }

    internal DateTimeOffset? UpdatedAt { get; private set; }

    internal string? LastStepPath { get; private set; }

    internal string? ErrorSummary { get; private set; }

    internal string? OutcomeName { get; private set; }

    internal int ContinueAsNewGeneration { get; private set; }

    internal DurableAggregateSnapshot Snapshot => new(
        InstanceId,
        DefinitionId,
        DefinitionVersion,
        ParentInstanceId,
        RootInstanceId,
        Status,
        CreatedAt,
        UpdatedAt,
        LastStepPath,
        ErrorSummary,
        OutcomeName,
        ContinueAsNewGeneration,
        [.. activeTimers],
        waitState.ActiveWaits,
        waitState.BufferedDeliveries,
        [.. bufferedTimers],
        [.. activeChildren],
        [.. activeChildGroups],
        [.. activeResourceTickets],
        externalJobState.ActiveJobs,
        sagaState.CompletedForwardActions,
        sagaState.CompensationActions,
        sagaState.RecoveryInterventions,
        sagaState.RequestedCompensationScopes);

    internal static DurableWorkflowAggregate Empty(InstanceId instanceId)
    {
        return new DurableWorkflowAggregate(
            instanceId,
            StreamVersion.Empty,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            0,
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            []);
    }

    internal static DurableWorkflowAggregate Rehydrate(
        DurableAggregateCheckpoint? checkpoint,
        IEnumerable<WorkflowEvent> tail)
    {
        ArgumentNullException.ThrowIfNull(tail);

        var aggregate = checkpoint is null
            ? Empty(default)
            : new DurableWorkflowAggregate(
                checkpoint.InstanceId,
                checkpoint.StreamVersion,
                checkpoint.ParentInstanceId,
                checkpoint.RootInstanceId,
                checkpoint.DefinitionId,
                checkpoint.DefinitionVersion,
                checkpoint.Status,
                checkpoint.CreatedAt,
                checkpoint.UpdatedAt,
                checkpoint.LastStepPath,
                checkpoint.ErrorSummary,
                checkpoint.OutcomeName,
                checkpoint.ContinueAsNewGeneration,
                checkpoint.ActiveTimers,
                checkpoint.ActiveWaits,
                checkpoint.BufferedDeliveries,
                checkpoint.BufferedTimers,
                checkpoint.ActiveChildren,
                checkpoint.ActiveChildGroups,
                checkpoint.ActiveResourceTickets,
                checkpoint.ActiveExternalJobs,
                [],
                [],
                [],
                [],
                [],
                []);

        foreach (var workflowEvent in tail)
        {
            aggregate.Apply(workflowEvent);
        }

        return aggregate;
    }

    internal DurableAggregateCheckpoint CreateCheckpoint(string contentType, byte[] payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentNullException.ThrowIfNull(payload);

        return new DurableAggregateCheckpoint(
            InstanceId,
            StreamVersion,
            ParentInstanceId,
            RootInstanceId,
            DefinitionId,
            DefinitionVersion,
            Status,
            CreatedAt,
            UpdatedAt,
            LastStepPath,
            ErrorSummary,
            OutcomeName,
            ContinueAsNewGeneration,
            [.. activeTimers],
            waitState.ActiveWaits,
            waitState.BufferedDeliveries,
            [.. bufferedTimers],
            [.. activeChildren],
            [.. activeChildGroups],
            [.. activeResourceTickets],
            externalJobState.ActiveJobs,
            contentType,
            [.. payload]);
    }

    internal DurableDecision DecideStart(StartWorkflowCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (Status is not null)
        {
            return DurableDecision.Empty;
        }

        return new DurableDecision([
            new WorkflowStartedEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                ParentInstanceId = command.ParentInstanceId,
                RootInstanceId = command.RootInstanceId ?? command.InstanceId,
                DefinitionId = command.DefinitionId,
                DefinitionVersion = command.DefinitionVersion,
                IdempotencyKey = command.IdempotencyKey
            }
        ]);
    }

    internal DurableDecision DecideStepCompleted(DurableStepCompletedCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (IsTerminal)
        {
            return DurableDecision.Empty;
        }

        var checkpoint = new CheckpointWrite(
            command.InstanceId,
            StreamVersion.Next(),
            command.StateContentType,
            [.. command.StatePayload])
        {
            DefinitionId = DefinitionId,
            ParentInstanceId = ParentInstanceId,
            RootInstanceId = RootInstanceId,
            DefinitionVersion = DefinitionVersion,
            Status = WorkflowStatus.Running,
            LastStepPath = command.StepPath,
            ErrorSummary = null,
            OutcomeName = null,
            ContinueAsNewGeneration = ContinueAsNewGeneration,
            RuntimeState = ToCheckpointRuntimeState()
        };

        return new DurableDecision(
            [
                new WorkflowStepCompletedEvent
                {
                    EventId = EventId.New(),
                    InstanceId = command.InstanceId,
                    CommandId = command.CommandId,
                    CausationId = ToCausationId(command.CommandId),
                    OccurredAt = command.RequestedAt,
                    StepPath = command.StepPath
                }
            ],
            checkpoint);
    }

    internal DurableDecision DecideContinueAsNew(ContinueAsNewCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.StateContentType);
        ArgumentNullException.ThrowIfNull(command.StatePayload);
        if (Status is null || IsTerminal)
        {
            return DurableDecision.Empty;
        }

        var generation = ContinueAsNewGeneration + 1;
        var checkpoint = new CheckpointWrite(
            command.InstanceId,
            StreamVersion.Next(),
            command.StateContentType,
            [.. command.StatePayload])
        {
            DefinitionId = DefinitionId,
            ParentInstanceId = ParentInstanceId,
            RootInstanceId = RootInstanceId,
            DefinitionVersion = DefinitionVersion,
            Status = WorkflowStatus.Running,
            LastStepPath = LastStepPath,
            ErrorSummary = null,
            OutcomeName = null,
            ContinueAsNewGeneration = generation
        };

        return new DurableDecision(
            [
                new WorkflowContinuedAsNewEvent
                {
                    EventId = EventId.New(),
                    InstanceId = command.InstanceId,
                    CommandId = command.CommandId,
                    CausationId = ToCausationId(command.CommandId),
                    OccurredAt = command.RequestedAt,
                    ParentInstanceId = ParentInstanceId,
                    RootInstanceId = RootInstanceId ?? InstanceId,
                    PreviousStreamVersion = StreamVersion,
                    Generation = generation
                }
            ],
            checkpoint);
    }

    internal DurableDecision DecideRunChild(DurableRunChildCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (IsTerminal)
        {
            return DurableDecision.Empty;
        }

        var waitId = WaitId.New();
        return new DurableDecision([
            new WorkflowChildScheduledEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                ParentInstanceId = ParentInstanceId,
                RootInstanceId = RootInstanceId ?? InstanceId,
                ChildInstanceId = command.ChildInstanceId,
                ChildDefinitionId = command.ChildDefinitionId,
                ChildDefinitionVersion = command.ChildDefinitionVersion,
                WaitId = waitId,
                FailurePolicy = command.FailurePolicy
            }
        ]);
    }

    internal DurableDecision DecideChildCompleted(DurableChildCompletedCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var activeChild = activeChildren.FirstOrDefault(child => child.ChildInstanceId == command.ChildInstanceId);
        if (activeChild is null || IsTerminal)
        {
            return DurableDecision.Empty;
        }

        var childCompleted = new WorkflowChildCompletedEvent
        {
            EventId = EventId.New(),
            InstanceId = command.InstanceId,
            CommandId = command.CommandId,
            CausationId = ToCausationId(command.CommandId),
            OccurredAt = command.RequestedAt,
            ParentInstanceId = ParentInstanceId,
            RootInstanceId = RootInstanceId ?? InstanceId,
            ChildInstanceId = command.ChildInstanceId,
            ChildStatus = command.ChildStatus,
            ErrorSummary = command.ErrorSummary
        };
        var waitMatched = new WorkflowWaitMatchedEvent
        {
            EventId = EventId.New(),
            InstanceId = command.InstanceId,
            CommandId = command.CommandId,
            CausationId = ToCausationId(command.CommandId),
            OccurredAt = command.RequestedAt,
            ParentInstanceId = ParentInstanceId,
            RootInstanceId = RootInstanceId ?? InstanceId,
            WaitId = activeChild.WaitId,
            MatchedEventId = childCompleted.EventId
        };
        var childDispatch = DispatchChildrenIfCapacity(command, activeChild);
        WorkflowEvent[] childDispatchEvents = childDispatch is null ? [] : [childDispatch];
        var residualIntent = ResidualIntentIfNeeded(command, activeChild);
        var resumeToken = ResumeTokenIfGroupComplete(command, activeChild, childDispatch);

        if (command.ChildStatus == WorkflowStatus.Failed &&
            activeChild.FailurePolicy is RunChildFailurePolicy.PropagateFailure)
        {
            return new DurableDecision([
                childCompleted,
                .. residualIntent,
                .. resumeToken,
                waitMatched,
                new WorkflowTerminalEvent
                {
                    EventId = EventId.New(),
                    InstanceId = command.InstanceId,
                    CommandId = command.CommandId,
                    CausationId = ToCausationId(command.CommandId),
                    OccurredAt = command.RequestedAt,
                    ParentInstanceId = ParentInstanceId,
                    RootInstanceId = RootInstanceId ?? InstanceId,
                    Status = WorkflowStatus.Failed
                }
            ]);
        }

        return new DurableDecision([
            childCompleted,
            .. childDispatchEvents,
            .. residualIntent,
            .. resumeToken,
            waitMatched
        ]);
    }

    internal DurableDecision DecideRunChildren(DurableRunChildrenCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (IsTerminal)
        {
            return DurableDecision.Empty;
        }

        var groupId = command.CommandId.Value.ToString("D");
        if (activeChildren.Any(child => string.Equals(child.GroupId, groupId, StringComparison.Ordinal)))
        {
            return DurableDecision.Empty;
        }

        var children = command.ItemSnapshots
            .Select((itemSnapshot, index) => new WorkflowChildMaterialization
            {
                Index = index,
                ChildInstanceId = DeterministicChildId(command.InstanceId, command.CommandId, index),
                ChildDefinitionId = command.ChildDefinitionId,
                ChildDefinitionVersion = command.ChildDefinitionVersion,
                ItemSnapshot = itemSnapshot
            })
            .ToArray();
        var maxConcurrency = Math.Min(command.MaxConcurrency ?? children.Length, children.Length);
        var initialDispatchCount = Math.Min(maxConcurrency, children.Length);
        return new DurableDecision([
            new WorkflowChildrenScheduledEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                ParentInstanceId = ParentInstanceId,
                RootInstanceId = RootInstanceId ?? InstanceId,
                GroupId = groupId,
                ChildDefinitionId = command.ChildDefinitionId,
                ChildDefinitionVersion = command.ChildDefinitionVersion,
                FailurePolicy = command.FailurePolicy,
                JoinPolicy = command.JoinPolicy,
                ResidualPolicy = command.ResidualPolicy,
                TotalItemCount = children.Length,
                InitialDispatchCount = initialDispatchCount,
                NextDispatchIndex = initialDispatchCount,
                MaxConcurrency = maxConcurrency,
                Children = children
            }
        ]);
    }

    internal DurableDecision DecideCompensateChildGroup(CompensateChildGroupCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (IsTerminal || compensatedChildGroups.Contains(command.GroupId))
        {
            return DurableDecision.Empty;
        }

        var eligibleChildren = completedChildren
            .Where(child => string.Equals(child.GroupId, command.GroupId, StringComparison.Ordinal))
            .OrderBy(child => child.CompletedAt)
            .ThenBy(child => child.ChildInstanceId.Value)
            .ToArray();
        if (eligibleChildren.Length == 0)
        {
            return DurableDecision.Empty;
        }

        var compensations = eligibleChildren
            .Select((child, index) => new WorkflowChildCompensationMaterialization
            {
                Index = index,
                SourceChildInstanceId = child.ChildInstanceId,
                CompensationInstanceId = DeterministicChildId(command.InstanceId, command.CommandId, index),
                ItemSnapshot = child.ItemSnapshot ?? string.Empty
            })
            .ToArray();

        return new DurableDecision([
            new WorkflowChildCompensationScheduledEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                ParentInstanceId = ParentInstanceId,
                RootInstanceId = RootInstanceId ?? InstanceId,
                GroupId = command.GroupId,
                CompensationDefinitionId = command.CompensationDefinitionId,
                CompensationDefinitionVersion = command.CompensationDefinitionVersion,
                Compensations = compensations
            }
        ]);
    }

    internal DurableDecision DecideStepFailed(DurableStepFailedCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (IsTerminal)
        {
            return DurableDecision.Empty;
        }

        return new DurableDecision([
            new WorkflowStepFailedEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                StepPath = command.StepPath,
                ErrorSummary = command.ErrorSummary
            },
            new WorkflowTerminalEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                Status = WorkflowStatus.Failed
            }
        ], null, true);
    }

    internal DurableDecision DecideWaitRegistered(DurableWaitRegisteredCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (IsTerminal)
        {
            return DurableDecision.Empty;
        }

        var decision = new DurableDecision([
            new WorkflowWaitRegisteredEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                WaitId = command.WaitId,
                EventName = command.EventName,
                CorrelationId = command.CorrelationId,
                Mode = command.Mode,
                BranchId = command.BranchId
            }
        ]);

        var matched = waitState.FindBufferedDelivery(command.EventName, command.CorrelationId, command.BranchId);
        if (matched is null)
        {
            return new DurableDecision(
                decision.Events,
                null,
                command.Mode == WaitMode.Cold);
        }

        return new DurableDecision(
            [
                .. decision.Events,
                new WorkflowWaitMatchedEvent
                {
                    EventId = EventId.New(),
                    InstanceId = command.InstanceId,
                    CommandId = command.CommandId,
                    CausationId = ToCausationId(command.CommandId),
                    OccurredAt = command.RequestedAt,
                    WaitId = command.WaitId,
                    MatchedEventId = matched.EventId
                }
            ],
            null,
            false,
            [new InboxWrite(matched.EventId, InboxRecordState.Applied)]);
    }

    internal DurableDecision DecideResourcePoolAcquire(
        AcquireResourcePoolCommand command,
        ResourcePoolAcquireResult acquireResult)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(acquireResult);
        if (IsTerminal)
        {
            return DurableDecision.Empty;
        }

        if (acquireResult.Status == ResourcePoolAcquireStatus.Granted)
        {
            return new DurableDecision([
                new WorkflowResourcePoolAcquiredEvent
                {
                    EventId = EventId.New(),
                    InstanceId = command.InstanceId,
                    CommandId = command.CommandId,
                    CausationId = ToCausationId(command.CommandId),
                    OccurredAt = command.RequestedAt,
                    ParentInstanceId = ParentInstanceId,
                    RootInstanceId = RootInstanceId ?? InstanceId,
                    HolderKey = command.HolderKey,
                    Tickets = acquireResult.Tickets
                }
            ]);
        }

        if (acquireResult.Status == ResourcePoolAcquireStatus.Queued)
        {
            return new DurableDecision([
                new WorkflowResourcePoolQueuedEvent
                {
                    EventId = EventId.New(),
                    InstanceId = command.InstanceId,
                    CommandId = command.CommandId,
                    CausationId = ToCausationId(command.CommandId),
                    OccurredAt = command.RequestedAt,
                    ParentInstanceId = ParentInstanceId,
                    RootInstanceId = RootInstanceId ?? InstanceId,
                    WaitId = WaitId.New(),
                    HolderKey = command.HolderKey,
                    Requirements = command.Requirements,
                    ExpiresAt = command.ExpiresAt
                }
            ], null, true);
        }

        return DurableDecision.Empty;
    }

    internal DurableDecision DecideRunExternalJob(
        RunExternalJobCommand command,
        ResourcePoolAcquireResult? acquireResult)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (IsTerminal || externalJobState.Find(command.ExternalJobId) is not null)
        {
            return DurableDecision.Empty;
        }

        if (acquireResult is not null && acquireResult.Status == ResourcePoolAcquireStatus.Queued)
        {
            return new DurableDecision([
                new WorkflowResourcePoolQueuedEvent
                {
                    EventId = EventId.New(),
                    InstanceId = command.InstanceId,
                    CommandId = command.CommandId,
                    CausationId = ToCausationId(command.CommandId),
                    OccurredAt = command.RequestedAt,
                    ParentInstanceId = ParentInstanceId,
                    RootInstanceId = RootInstanceId ?? InstanceId,
                    WaitId = WaitId.New(),
                    HolderKey = command.ExternalJobId,
                    Requirements = command.Requirements,
                    ExpiresAt = command.TimeoutAt
                }
            ], null, true);
        }

        if (acquireResult is { Status: ResourcePoolAcquireStatus.Rejected })
        {
            return DurableDecision.Empty;
        }

        var waitId = WaitId.New();
        TimerId? timeoutTimerId = command.TimeoutAt is null ? null : TimerId.New();
        var events = new List<WorkflowEvent>();
        if (acquireResult is { Status: ResourcePoolAcquireStatus.Granted })
        {
            events.Add(new WorkflowResourcePoolAcquiredEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                ParentInstanceId = ParentInstanceId,
                RootInstanceId = RootInstanceId ?? InstanceId,
                HolderKey = command.ExternalJobId,
                Tickets = acquireResult.Tickets
            });
        }

        events.Add(new WorkflowExternalJobStartedEvent
        {
            EventId = EventId.New(),
            InstanceId = command.InstanceId,
            CommandId = command.CommandId,
            CausationId = ToCausationId(command.CommandId),
            OccurredAt = command.RequestedAt,
            ParentInstanceId = ParentInstanceId,
            RootInstanceId = RootInstanceId ?? InstanceId,
            ExternalJobId = command.ExternalJobId,
            Payload = [.. command.Payload],
            WaitId = waitId,
            TimeoutTimerId = timeoutTimerId,
            TimeoutAt = command.TimeoutAt
        });
        events.Add(new WorkflowWaitRegisteredEvent
        {
            EventId = EventId.New(),
            InstanceId = command.InstanceId,
            CommandId = command.CommandId,
            CausationId = ToCausationId(command.CommandId),
            OccurredAt = command.RequestedAt,
            ParentInstanceId = ParentInstanceId,
            RootInstanceId = RootInstanceId ?? InstanceId,
            WaitId = waitId,
            EventName = "ExternalJobCompleted",
            CorrelationId = new CorrelationId(command.ExternalJobId),
            Mode = WaitMode.Cold
        });

        if (timeoutTimerId is { } timerId && command.TimeoutAt is { } timeoutAt)
        {
            events.Add(new WorkflowTimerScheduledEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                ParentInstanceId = ParentInstanceId,
                RootInstanceId = RootInstanceId ?? InstanceId,
                TimerId = timerId,
                FireAt = timeoutAt,
                WakeupName = $"ExternalJobTimeout:{command.ExternalJobId}"
            });
        }

        return new DurableDecision(events, null, true);
    }

    internal DurableDecision DecideExternalJobCompleted(CompleteExternalJobCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var job = externalJobState.Find(command.ExternalJobId);
        if (IsTerminal || job is null)
        {
            return DurableDecision.Empty;
        }

        return new DurableDecision([
            new WorkflowExternalJobCompletedEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                ParentInstanceId = ParentInstanceId,
                RootInstanceId = RootInstanceId ?? InstanceId,
                ExternalJobId = command.ExternalJobId,
                CompletionEventId = command.CompletionEventId
            },
            .. ReleaseEvents(command.CommandId, command.InstanceId, command.RequestedAt, command.ExternalJobId),
            new WorkflowWaitMatchedEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                ParentInstanceId = ParentInstanceId,
                RootInstanceId = RootInstanceId ?? InstanceId,
                WaitId = job.WaitId,
                MatchedEventId = command.CompletionEventId
            }
        ]);
    }

    internal DurableDecision DecideExternalJobTimedOut(TimeoutExternalJobCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var job = externalJobState.Find(command.ExternalJobId);
        if (IsTerminal || job is null)
        {
            return DurableDecision.Empty;
        }

        return new DurableDecision([
            new WorkflowExternalJobTimedOutEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                ParentInstanceId = ParentInstanceId,
                RootInstanceId = RootInstanceId ?? InstanceId,
                ExternalJobId = command.ExternalJobId
            },
            new WorkflowExternalJobStopRequestedEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                ParentInstanceId = ParentInstanceId,
                RootInstanceId = RootInstanceId ?? InstanceId,
                ExternalJobId = command.ExternalJobId
            },
            .. ReleaseEvents(command.CommandId, command.InstanceId, command.RequestedAt, command.ExternalJobId),
            new WorkflowTerminalEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                ParentInstanceId = ParentInstanceId,
                RootInstanceId = RootInstanceId ?? InstanceId,
                Status = WorkflowStatus.Failed
            }
        ], null, true);
    }

    internal DurableDecision DecideCancel(CancelWorkflowCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (IsTerminal)
        {
            return DurableDecision.Empty;
        }

        var events = new List<WorkflowEvent>(externalJobState.CreateStopRequestedEvents(
            CreateExternalJobEventContext(command.CommandId, command.InstanceId, command.RequestedAt)));

        events.AddRange(ReleaseEvents(command.CommandId, command.InstanceId, command.RequestedAt));
        events.Add(new WorkflowTerminalEvent
        {
            EventId = EventId.New(),
            InstanceId = command.InstanceId,
            CommandId = command.CommandId,
            CausationId = ToCausationId(command.CommandId),
            OccurredAt = command.RequestedAt,
            ParentInstanceId = ParentInstanceId,
            RootInstanceId = RootInstanceId ?? InstanceId,
            Status = WorkflowStatus.Cancelled
        });
        return new DurableDecision(events, null, true);
    }

    internal DurableDecision DecideConsumeParentResumeToken(ConsumeParentResumeTokenCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.GroupId);
        if (!recordedParentResumeTokens.Contains(command.ResumeTokenId) ||
            consumedParentResumeTokens.Contains(command.ResumeTokenId))
        {
            return DurableDecision.Empty;
        }

        return new DurableDecision([
            new WorkflowParentResumeTokenConsumedEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                ParentInstanceId = ParentInstanceId,
                RootInstanceId = RootInstanceId ?? InstanceId,
                GroupId = command.GroupId,
                ResumeTokenId = command.ResumeTokenId
            }
        ]);
    }

    internal DurableDecision DecideRecordSagaForwardActionCompleted(
        RecordSagaForwardActionCompletedCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (IsTerminal || sagaState.HasForwardAction(command.ScopeId, command.ActionKey))
        {
            return DurableDecision.Empty;
        }

        return new DurableDecision([
            new SagaForwardActionCompletedEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                ParentInstanceId = ParentInstanceId,
                RootInstanceId = RootInstanceId ?? InstanceId,
                ScopeId = command.ScopeId,
                ActionKey = command.ActionKey,
                CompensationKey = command.CompensationKey
            }
        ]);
    }

    internal DurableDecision DecideRequestSagaCompensation(RequestSagaCompensationCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (IsTerminal || sagaState.HasRequestedCompensation(command.ScopeId))
        {
            return DurableDecision.Empty;
        }

        return new DurableDecision(sagaState.PlanCompensation(
            CreateSagaEventContext(command.CommandId, command.InstanceId, command.RequestedAt),
            command.ScopeId,
            command.Reason));
    }

    internal DurableDecision DecideSagaForwardActionTimedOut(SagaForwardActionTimedOutCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (IsTerminal)
        {
            return DurableDecision.Empty;
        }

        var events = new List<WorkflowEvent>
        {
            new SagaForwardActionTimedOutEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                ParentInstanceId = ParentInstanceId,
                RootInstanceId = RootInstanceId ?? InstanceId,
                ScopeId = command.ScopeId,
                ActionKey = command.ActionKey,
                CompensateScope = command.CompensateScope
            }
        };

        if (command.CompensateScope && !sagaState.HasRequestedCompensation(command.ScopeId))
        {
            events.AddRange(sagaState.PlanCompensation(
                CreateSagaEventContext(command.CommandId, command.InstanceId, command.RequestedAt),
                command.ScopeId,
                "timeout"));
        }

        return new DurableDecision(events);
    }

    internal DurableDecision DecideCompleteSagaCompensation(CompleteSagaCompensationCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (IsTerminal)
        {
            return DurableDecision.Empty;
        }

        var events = new List<WorkflowEvent>
        {
            new SagaCompensationCompletedEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                ParentInstanceId = ParentInstanceId,
                RootInstanceId = RootInstanceId ?? InstanceId,
                ScopeId = command.ScopeId,
                ActionKey = command.ActionKey
            }
        };

        if (sagaState.AllCompensationsCompleteAfter(command.ScopeId, command.ActionKey))
        {
            events.Add(new WorkflowTerminalEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                ParentInstanceId = ParentInstanceId,
                RootInstanceId = RootInstanceId ?? InstanceId,
                Status = WorkflowStatus.Compensated
            });
        }

        return new DurableDecision(events, null, events.Any(workflowEvent => workflowEvent is WorkflowTerminalEvent));
    }

    internal DurableDecision DecideFailSagaCompensation(FailSagaCompensationCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (IsTerminal)
        {
            return DurableDecision.Empty;
        }

        return new DurableDecision([
            new SagaCompensationFailedEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                ParentInstanceId = ParentInstanceId,
                RootInstanceId = RootInstanceId ?? InstanceId,
                ScopeId = command.ScopeId,
                ActionKey = command.ActionKey,
                ErrorSummary = command.ErrorSummary
            },
            new WorkflowTerminalEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                ParentInstanceId = ParentInstanceId,
                RootInstanceId = RootInstanceId ?? InstanceId,
                Status = WorkflowStatus.CompensationFailed
            }
        ], null, true);
    }

    internal DurableDecision DecideRecordSagaManualRecovery(RecordSagaManualRecoveryCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (Status is not WorkflowStatus.CompensationFailed)
        {
            return DurableDecision.Empty;
        }

        return new DurableDecision([
            new SagaManualRecoveryRecordedEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                ParentInstanceId = ParentInstanceId,
                RootInstanceId = RootInstanceId ?? InstanceId,
                ScopeId = command.ScopeId,
                ActionKey = command.ActionKey,
                OperatorId = command.OperatorId,
                RecoveryAction = command.RecoveryAction,
                Reason = command.Reason,
                TargetStatus = command.TargetStatus
            },
            new WorkflowTerminalEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                ParentInstanceId = ParentInstanceId,
                RootInstanceId = RootInstanceId ?? InstanceId,
                Status = command.TargetStatus
            }
        ], null, true);
    }

    internal DurableDecision DecideWaitMatched(DurableWaitMatchedCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (IsTerminal || !waitState.HasWait(command.WaitId))
        {
            return DurableDecision.Empty;
        }

        return new DurableDecision([
            new WorkflowWaitMatchedEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                WaitId = command.WaitId,
                MatchedEventId = command.MatchedEventId
            }
        ]);
    }

    internal DurableDecision DecideTimerScheduled(ScheduleTimerCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (IsTerminal)
        {
            return DurableDecision.Empty;
        }

        return new DurableDecision([
            new WorkflowTimerScheduledEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                TimerId = command.TimerId,
                FireAt = command.FireAt,
                WakeupName = command.WakeupName
            }
        ]);
    }

    internal DurableDecision DecideTimerFired(FireTimerCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var timer = activeTimers.FirstOrDefault(candidate => candidate.TimerId == command.TimerId);
        if (IsTerminal || timer is null)
        {
            return DurableDecision.Empty;
        }

        if (Status == WorkflowStatus.Paused)
        {
            return new DurableDecision([
                new WorkflowTimerBufferedEvent
                {
                    EventId = EventId.New(),
                    InstanceId = command.InstanceId,
                    CommandId = command.CommandId,
                    CausationId = ToCausationId(command.CommandId),
                    OccurredAt = command.RequestedAt,
                    TimerId = command.TimerId,
                    WakeupName = timer.WakeupName
                }
            ]);
        }

        return new DurableDecision([
            new WorkflowTimerFiredEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                TimerId = command.TimerId
            }
        ]);
    }

    internal DurableDecision DecideDeliverEvent(DeliverEventCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (IsTerminal)
        {
            return DurableDecision.Empty;
        }

        if (Status == WorkflowStatus.Paused)
        {
            return new DurableDecision([
                new WorkflowDeliveryBufferedEvent
                {
                    EventId = EventId.New(),
                    InstanceId = command.InstanceId,
                    CommandId = command.CommandId,
                    CausationId = ToCausationId(command.CommandId),
                    OccurredAt = command.RequestedAt,
                    BufferedEventId = command.Envelope.EventId,
                    EventName = command.Envelope.EventName,
                    CorrelationId = command.Envelope.CorrelationId,
                    BranchId = command.Envelope.BranchId
                }
            ]);
        }

        var wait = waitState.FindActiveWait(command.Envelope);
        if (wait is null)
        {
            return new DurableDecision([
                new WorkflowDeliveryBufferedEvent
                {
                    EventId = EventId.New(),
                    InstanceId = command.InstanceId,
                    CommandId = command.CommandId,
                    CausationId = ToCausationId(command.CommandId),
                    OccurredAt = command.RequestedAt,
                    BufferedEventId = command.Envelope.EventId,
                    EventName = command.Envelope.EventName,
                    CorrelationId = command.Envelope.CorrelationId,
                    BranchId = command.Envelope.BranchId
                }
            ]);
        }

        return new DurableDecision([
            new WorkflowWaitMatchedEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                WaitId = wait.WaitId,
                MatchedEventId = command.Envelope.EventId
            }
        ]);
    }

    internal DurableDecision DecidePause(DurablePauseCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (IsTerminal || Status is null or WorkflowStatus.Paused)
        {
            return DurableDecision.Empty;
        }

        return new DurableDecision([
            new WorkflowPausedEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt
            }
        ]);
    }

    internal DurableDecision DecideResume(DurableResumeCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (Status != WorkflowStatus.Paused)
        {
            return DurableDecision.Empty;
        }

        var events = new List<WorkflowEvent>
        {
            new WorkflowResumedEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                BufferHandling = command.BufferedDeliveries.ToString()
            }
        };

        foreach (var bufferedTimer in bufferedTimers)
        {
            if (command.BufferedDeliveries == ResumeBufferedDeliveries.Discard)
            {
                continue;
            }

            events.Add(new WorkflowTimerFiredEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                TimerId = bufferedTimer.TimerId
            });
        }

        var replayPlan = waitState.PlanBufferedDeliveryReplay(
            CreateWaitEventContext(command.CommandId, command.InstanceId, command.RequestedAt),
            command.BufferedDeliveries);
        events.AddRange(replayPlan.Events);

        return new DurableDecision(events, null, false, replayPlan.InboxWrites);
    }

    internal DurableDecision DecideComplete(DurableCompleteCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (IsTerminal)
        {
            return DurableDecision.Empty;
        }

        return new DurableDecision([
            .. ReleaseEvents(command.CommandId, command.InstanceId, command.RequestedAt),
            new WorkflowCompletedEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                OutcomeName = command.OutcomeName
            },
            new WorkflowTerminalEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                Status = WorkflowStatus.Completed
            }
        ], null, true);
    }

    internal DurableDecision DecideFail(DurableFailCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (IsTerminal)
        {
            return DurableDecision.Empty;
        }

        return new DurableDecision([
            .. ReleaseEvents(command.CommandId, command.InstanceId, command.RequestedAt),
            new WorkflowTerminalEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                Status = WorkflowStatus.Failed
            }
        ], null, true);
    }

    internal DurableDecision DecideTerminate(TerminateWorkflowCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (IsTerminal)
        {
            return DurableDecision.Empty;
        }

        return new DurableDecision([
            .. ReleaseEvents(command.CommandId, command.InstanceId, command.RequestedAt),
            new WorkflowTerminalEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                Status = WorkflowStatus.Terminated
            }
        ], null, true);
    }

    internal IReadOnlyList<ProjectionWrite> CreateProjectionWrites(IReadOnlyList<WorkflowEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (events.Count == 0)
        {
            return [];
        }

        var projected = new DurableWorkflowAggregate(
            InstanceId,
            StreamVersion,
            ParentInstanceId,
            RootInstanceId,
            DefinitionId,
            DefinitionVersion,
            Status,
            CreatedAt,
            UpdatedAt,
            LastStepPath,
            ErrorSummary,
            OutcomeName,
            ContinueAsNewGeneration,
            activeTimers,
            waitState.ActiveWaits,
            waitState.BufferedDeliveries,
            bufferedTimers,
            activeChildren,
            activeChildGroups,
            activeResourceTickets,
            externalJobState.ActiveJobs,
            sagaState.CompletedForwardActions,
            sagaState.CompensationActions,
            sagaState.RecoveryInterventions,
            sagaState.RequestedCompensationScopes,
            recordedParentResumeTokens,
            consumedParentResumeTokens);

        foreach (var workflowEvent in events)
        {
            projected.Apply(workflowEvent);
        }

        var snapshot = projected.ToInstanceSnapshot();
        return snapshot is null
            ? []
            : [new ProjectionWrite(projected.InstanceId, ProjectionOperationKind.UpsertSummary)
            {
                InstanceSnapshot = snapshot
            }];
    }

    private bool IsTerminal =>
        Status is WorkflowStatus.Completed
            or WorkflowStatus.Failed
            or WorkflowStatus.Cancelled
            or WorkflowStatus.Terminated
            or WorkflowStatus.Compensated
            or WorkflowStatus.CompensationFailed;

    private void Apply(WorkflowEvent workflowEvent)
    {
        ArgumentNullException.ThrowIfNull(workflowEvent);

        if (InstanceId == default)
        {
            InstanceId = workflowEvent.InstanceId;
        }

        CreatedAt ??= workflowEvent.OccurredAt;
        UpdatedAt = workflowEvent.OccurredAt;
        StreamVersion = StreamVersion.Next();
        switch (workflowEvent)
        {
            case WorkflowStartedEvent started:
                ParentInstanceId = started.ParentInstanceId;
                RootInstanceId = started.RootInstanceId ?? started.InstanceId;
                DefinitionId = started.DefinitionId;
                DefinitionVersion = started.DefinitionVersion;
                Status = WorkflowStatus.Running;
                break;
            case WorkflowContinuedAsNewEvent continuedAsNew:
                ContinueAsNewGeneration = continuedAsNew.Generation;
                Status = WorkflowStatus.Running;
                ErrorSummary = null;
                OutcomeName = null;
                activeTimers.Clear();
                waitState.Clear();
                bufferedTimers.Clear();
                activeChildren.Clear();
                activeResourceTickets.Clear();
                externalJobState.Clear();
                break;
            case WorkflowStepCompletedEvent stepCompleted:
                LastStepPath = stepCompleted.StepPath;
                if (Status is not WorkflowStatus.Waiting)
                {
                    Status = WorkflowStatus.Running;
                }

                break;
            case WorkflowStepFailedEvent stepFailed:
                LastStepPath = stepFailed.StepPath;
                ErrorSummary = stepFailed.ErrorSummary;
                Status = WorkflowStatus.Failed;
                activeTimers.Clear();
                waitState.Clear();
                activeChildren.Clear();
                break;
            case WorkflowWaitRegisteredEvent waitRegistered:
                waitState.Apply(waitRegistered);
                Status = WorkflowStatus.Waiting;
                break;
            case WorkflowWaitMatchedEvent waitMatched:
                waitState.Apply(waitMatched);
                Status = waitState.HasActiveWaits ? WorkflowStatus.Waiting : WorkflowStatus.Running;
                break;
            case WorkflowTimerScheduledEvent timerScheduled:
                activeTimers.Add(new DurableActiveTimer(
                    timerScheduled.TimerId,
                    timerScheduled.FireAt,
                    timerScheduled.WakeupName,
                    timerScheduled.OccurredAt));
                Status = WorkflowStatus.Waiting;
                break;
            case WorkflowTimerFiredEvent timerFired:
                activeTimers.RemoveAll(timer => timer.TimerId == timerFired.TimerId);
                bufferedTimers.RemoveAll(timer => timer.TimerId == timerFired.TimerId);
                Status = !waitState.HasActiveWaits && activeTimers.Count == 0
                    ? WorkflowStatus.Running
                    : WorkflowStatus.Waiting;
                break;
            case WorkflowChildScheduledEvent childScheduled:
                activeChildren.Add(new DurableActiveChild(
                    childScheduled.ChildInstanceId.Value.ToString("D"),
                    childScheduled.ChildInstanceId,
                    childScheduled.WaitId,
                    childScheduled.FailurePolicy,
                    RunChildrenJoinPolicy.WhenAll,
                    RunChildrenResidualPolicy.CancelRemaining,
                    null));
                waitState.Register(new DurableActiveWait(
                    childScheduled.WaitId,
                    "ChildCompleted",
                    ChildCorrelation(childScheduled.ChildInstanceId),
                    childScheduled.OccurredAt));
                Status = WorkflowStatus.Waiting;
                break;
            case WorkflowChildrenScheduledEvent childrenScheduled:
                activeChildGroups.RemoveAll(group =>
                    string.Equals(group.GroupId, childrenScheduled.GroupId, StringComparison.Ordinal));
                activeChildGroups.Add(new DurableActiveChildGroup(
                    childrenScheduled.GroupId,
                    childrenScheduled.FailurePolicy,
                    childrenScheduled.JoinPolicy,
                    childrenScheduled.ResidualPolicy,
                    childrenScheduled.MaxConcurrency,
                    childrenScheduled.NextDispatchIndex,
                    childrenScheduled.Children));
                foreach (var child in childrenScheduled.Children.Take(childrenScheduled.InitialDispatchCount))
                {
                    var waitId = new WaitId(child.ChildInstanceId.Value);
                    activeChildren.Add(new DurableActiveChild(
                        childrenScheduled.GroupId,
                        child.ChildInstanceId,
                        waitId,
                        childrenScheduled.FailurePolicy,
                        childrenScheduled.JoinPolicy,
                        childrenScheduled.ResidualPolicy,
                        child.ItemSnapshot));
                    waitState.Register(new DurableActiveWait(
                        waitId,
                        "ChildCompleted",
                        ChildCorrelation(child.ChildInstanceId),
                        childrenScheduled.OccurredAt));
                }

                Status = WorkflowStatus.Waiting;
                break;
            case WorkflowChildrenDispatchedEvent childrenDispatched:
                var activeGroup = activeChildGroups.FirstOrDefault(group =>
                    string.Equals(group.GroupId, childrenDispatched.GroupId, StringComparison.Ordinal));
                if (activeGroup is not null)
                {
                    activeChildGroups.Remove(activeGroup);
                    activeChildGroups.Add(activeGroup with { NextDispatchIndex = childrenDispatched.NextDispatchIndex });
                    foreach (var child in childrenDispatched.Children)
                    {
                        var waitId = new WaitId(child.ChildInstanceId.Value);
                        activeChildren.Add(new DurableActiveChild(
                            activeGroup.GroupId,
                            child.ChildInstanceId,
                            waitId,
                            activeGroup.FailurePolicy,
                            activeGroup.JoinPolicy,
                            activeGroup.ResidualPolicy,
                            child.ItemSnapshot));
                        waitState.Register(new DurableActiveWait(
                            waitId,
                            "ChildCompleted",
                            ChildCorrelation(child.ChildInstanceId),
                            childrenDispatched.OccurredAt));
                    }
                }

                Status = WorkflowStatus.Waiting;
                break;
            case WorkflowChildCompletedEvent childCompleted:
                var completedChild = activeChildren.FirstOrDefault(
                    child => child.ChildInstanceId == childCompleted.ChildInstanceId);
                if (completedChild is not null && childCompleted.ChildStatus is WorkflowStatus.Completed)
                {
                    completedChildren.RemoveAll(child =>
                        string.Equals(child.GroupId, completedChild.GroupId, StringComparison.Ordinal) &&
                        child.ChildInstanceId == childCompleted.ChildInstanceId);
                    completedChildren.Add(new DurableCompletedChild(
                        completedChild.GroupId,
                        childCompleted.ChildInstanceId,
                        completedChild.ItemSnapshot,
                        childCompleted.OccurredAt));
                }

                activeChildren.RemoveAll(child => child.ChildInstanceId == childCompleted.ChildInstanceId);
                ErrorSummary = childCompleted.ChildStatus == WorkflowStatus.Failed &&
                    completedChild?.FailurePolicy is RunChildFailurePolicy.PropagateFailure
                    ? childCompleted.ErrorSummary
                    : ErrorSummary;
                break;
            case WorkflowChildCompensationScheduledEvent childCompensationScheduled:
                compensatedChildGroups.Add(childCompensationScheduled.GroupId);
                break;
            case WorkflowChildResidualIntentRecordedEvent residualIntent:
                foreach (var residualChildId in residualIntent.ResidualChildInstanceIds)
                {
                    var residualChild = activeChildren.FirstOrDefault(
                        child => child.ChildInstanceId == residualChildId);
                    if (residualChild is not null)
                    {
                        waitState.Remove(residualChild.WaitId);
                    }

                    activeChildren.RemoveAll(child => child.ChildInstanceId == residualChildId);
                }

                break;
            case WorkflowParentResumeTokenRecordedEvent parentResumeToken:
                recordedParentResumeTokens.Add(parentResumeToken.ResumeTokenId);
                activeChildGroups.RemoveAll(group =>
                    string.Equals(group.GroupId, parentResumeToken.GroupId, StringComparison.Ordinal));
                break;
            case WorkflowParentResumeTokenConsumedEvent parentResumeTokenConsumed:
                consumedParentResumeTokens.Add(parentResumeTokenConsumed.ResumeTokenId);
                break;
            case WorkflowResourcePoolAcquiredEvent resourcePoolAcquired:
                activeResourceTickets.RemoveAll(ticket =>
                    string.Equals(ticket.HolderKey, resourcePoolAcquired.HolderKey, StringComparison.Ordinal));
                activeResourceTickets.AddRange(resourcePoolAcquired.Tickets);
                Status = WorkflowStatus.Running;
                break;
            case WorkflowResourcePoolQueuedEvent resourcePoolQueued:
                waitState.Register(new DurableActiveWait(
                    resourcePoolQueued.WaitId,
                    "ResourcePoolGranted",
                    new CorrelationId(resourcePoolQueued.HolderKey),
                    resourcePoolQueued.OccurredAt,
                    WaitMode.Cold));
                Status = WorkflowStatus.Waiting;
                break;
            case WorkflowResourcePoolReleasedEvent resourcePoolReleased:
                activeResourceTickets.RemoveAll(ticket =>
                    string.Equals(ticket.HolderKey, resourcePoolReleased.HolderKey, StringComparison.Ordinal));
                break;
            case WorkflowExternalJobStartedEvent externalJobStarted:
                externalJobState.Apply(externalJobStarted);
                break;
            case WorkflowExternalJobCompletedEvent externalJobCompleted:
                externalJobState.Apply(externalJobCompleted);
                break;
            case WorkflowExternalJobTimedOutEvent externalJobTimedOut:
                externalJobState.Apply(externalJobTimedOut);
                break;
            case WorkflowExternalJobStopRequestedEvent:
                break;
            case SagaForwardActionCompletedEvent:
            case SagaForwardActionTimedOutEvent:
            case SagaCompensationRequestedEvent:
            case SagaCompensationStartedEvent:
            case SagaCompensationCompletedEvent:
            case SagaCompensationFailedEvent:
            case SagaManualRecoveryRecordedEvent:
                sagaState.Apply(workflowEvent, UpdatedAt);
                break;
            case WorkflowTimerBufferedEvent timerBuffered:
                activeTimers.RemoveAll(timer => timer.TimerId == timerBuffered.TimerId);
                bufferedTimers.RemoveAll(timer => timer.TimerId == timerBuffered.TimerId);
                bufferedTimers.Add(new DurableBufferedTimer(
                    timerBuffered.TimerId,
                    timerBuffered.WakeupName,
                    timerBuffered.OccurredAt));
                Status = WorkflowStatus.Paused;
                break;
            case WorkflowPausedEvent:
                Status = WorkflowStatus.Paused;
                break;
            case WorkflowResumedEvent:
                Status = waitState.HasActiveWaits ? WorkflowStatus.Waiting : WorkflowStatus.Running;
                break;
            case WorkflowDeliveryBufferedEvent deliveryBuffered:
                waitState.Apply(deliveryBuffered);
                break;
            case WorkflowDeliveryDiscardedEvent deliveryDiscarded:
                waitState.Apply(deliveryDiscarded);
                break;
            case WorkflowCompletedEvent completed:
                OutcomeName = completed.OutcomeName;
                Status = WorkflowStatus.Completed;
                activeTimers.Clear();
                waitState.Clear();
                bufferedTimers.Clear();
                activeChildren.Clear();
                activeResourceTickets.Clear();
                externalJobState.Clear();
                break;
            case WorkflowTerminalEvent terminal:
                Status = terminal.Status;
                if (IsTerminal)
                {
                    activeTimers.Clear();
                    waitState.Clear();
                    bufferedTimers.Clear();
                    activeChildren.Clear();
                    activeResourceTickets.Clear();
                    externalJobState.Clear();
                }

                break;
            default:
                throw new InvalidOperationException(
                    $"Workflow event '{workflowEvent.GetType().Name}' is not supported by durable aggregate replay.");
        }
    }

    private static CausationId ToCausationId(CommandId commandId)
    {
        return new CausationId(commandId.Value);
    }

    private static DurableWaitEventContext CreateWaitEventContext(
        CommandId commandId,
        InstanceId instanceId,
        DateTimeOffset requestedAt)
    {
        return new DurableWaitEventContext(commandId, instanceId, requestedAt);
    }

    private DurableExternalJobEventContext CreateExternalJobEventContext(
        CommandId commandId,
        InstanceId instanceId,
        DateTimeOffset requestedAt)
    {
        return new DurableExternalJobEventContext(
            commandId,
            instanceId,
            requestedAt,
            ParentInstanceId,
            RootInstanceId ?? InstanceId);
    }

    private DurableSagaEventContext CreateSagaEventContext(
        CommandId commandId,
        InstanceId instanceId,
        DateTimeOffset requestedAt)
    {
        return new DurableSagaEventContext(
            commandId,
            instanceId,
            requestedAt,
            ParentInstanceId,
            RootInstanceId ?? InstanceId);
    }

    private IReadOnlyList<WorkflowResourcePoolReleasedEvent> ReleaseEvents(
        CommandId commandId,
        InstanceId instanceId,
        DateTimeOffset occurredAt,
        string? holderKey = null)
    {
        return activeResourceTickets
            .Where(ticket => holderKey is null || string.Equals(ticket.HolderKey, holderKey, StringComparison.Ordinal))
            .GroupBy(ticket => ticket.HolderKey, StringComparer.Ordinal)
            .Select(group => new WorkflowResourcePoolReleasedEvent
            {
                EventId = EventId.New(),
                InstanceId = instanceId,
                CommandId = commandId,
                CausationId = ToCausationId(commandId),
                OccurredAt = occurredAt,
                ParentInstanceId = ParentInstanceId,
                RootInstanceId = RootInstanceId ?? InstanceId,
                HolderKey = group.Key,
                Tickets = group.ToArray()
            })
            .ToArray();
    }

    private IReadOnlyList<WorkflowParentResumeTokenRecordedEvent> ResumeTokenIfGroupComplete(
        DurableChildCompletedCommand command,
        DurableActiveChild activeChild,
        WorkflowChildrenDispatchedEvent? childDispatch)
    {
        if (activeChild.JoinPolicy is RunChildrenJoinPolicy.WhenAny)
        {
            return [ResumeToken(command, activeChild)];
        }

        var remainingInGroup = activeChildren.Count(child =>
            string.Equals(child.GroupId, activeChild.GroupId, StringComparison.Ordinal) &&
            child.ChildInstanceId != activeChild.ChildInstanceId) +
            (childDispatch?.Children.Count ?? 0);
        var group = activeChildGroups.FirstOrDefault(candidate =>
            string.Equals(candidate.GroupId, activeChild.GroupId, StringComparison.Ordinal));
        var nextDispatchIndex = childDispatch?.NextDispatchIndex ?? group?.NextDispatchIndex ?? 0;
        if (remainingInGroup > 0 || (group is not null && nextDispatchIndex < group.Children.Count))
        {
            return [];
        }

        return
        [
            ResumeToken(command, activeChild)
        ];
    }

    private WorkflowChildrenDispatchedEvent? DispatchChildrenIfCapacity(
        DurableChildCompletedCommand command,
        DurableActiveChild activeChild)
    {
        if (activeChild.JoinPolicy is not RunChildrenJoinPolicy.WhenAll)
        {
            return null;
        }

        var group = activeChildGroups.FirstOrDefault(candidate =>
            string.Equals(candidate.GroupId, activeChild.GroupId, StringComparison.Ordinal));
        if (group is null || group.NextDispatchIndex >= group.Children.Count)
        {
            return null;
        }

        var activeAfterCompletion = activeChildren.Count(child =>
            string.Equals(child.GroupId, activeChild.GroupId, StringComparison.Ordinal) &&
            child.ChildInstanceId != activeChild.ChildInstanceId);
        var availableSlots = group.MaxConcurrency - activeAfterCompletion;
        if (availableSlots <= 0)
        {
            return null;
        }

        var children = group.Children
            .Skip(group.NextDispatchIndex)
            .Take(availableSlots)
            .ToArray();
        if (children.Length == 0)
        {
            return null;
        }

        return new WorkflowChildrenDispatchedEvent
        {
            EventId = EventId.New(),
            InstanceId = command.InstanceId,
            CommandId = command.CommandId,
            CausationId = ToCausationId(command.CommandId),
            OccurredAt = command.RequestedAt,
            ParentInstanceId = ParentInstanceId,
            RootInstanceId = RootInstanceId ?? InstanceId,
            GroupId = activeChild.GroupId,
            PreviousDispatchIndex = group.NextDispatchIndex,
            NextDispatchIndex = group.NextDispatchIndex + children.Length,
            Children = children
        };
    }

    private IReadOnlyList<WorkflowChildResidualIntentRecordedEvent> ResidualIntentIfNeeded(
        DurableChildCompletedCommand command,
        DurableActiveChild activeChild)
    {
        if (activeChild.JoinPolicy is not RunChildrenJoinPolicy.WhenAny ||
            activeChild.ResidualPolicy is not RunChildrenResidualPolicy.CancelRemaining)
        {
            return [];
        }

        var residualChildIds = activeChildren
            .Where(child => string.Equals(child.GroupId, activeChild.GroupId, StringComparison.Ordinal) &&
                child.ChildInstanceId != activeChild.ChildInstanceId)
            .Select(child => child.ChildInstanceId)
            .ToArray();
        return residualChildIds.Length == 0
            ? []
            :
            [
                new WorkflowChildResidualIntentRecordedEvent
                {
                    EventId = EventId.New(),
                    InstanceId = command.InstanceId,
                    CommandId = command.CommandId,
                    CausationId = ToCausationId(command.CommandId),
                    OccurredAt = command.RequestedAt,
                    ParentInstanceId = ParentInstanceId,
                    RootInstanceId = RootInstanceId ?? InstanceId,
                    GroupId = activeChild.GroupId,
                    ResidualPolicy = activeChild.ResidualPolicy,
                    ResidualChildInstanceIds = residualChildIds
                }
            ];
    }

    private WorkflowParentResumeTokenRecordedEvent ResumeToken(
        DurableChildCompletedCommand command,
        DurableActiveChild activeChild)
    {
        return new WorkflowParentResumeTokenRecordedEvent
        {
            EventId = EventId.New(),
            InstanceId = command.InstanceId,
            CommandId = command.CommandId,
            CausationId = ToCausationId(command.CommandId),
            OccurredAt = command.RequestedAt,
            ParentInstanceId = ParentInstanceId,
            RootInstanceId = RootInstanceId ?? InstanceId,
            GroupId = activeChild.GroupId,
            ResumeTokenId = new EventId(Guid.Parse(activeChild.GroupId))
        };
    }

    private static CorrelationId ChildCorrelation(InstanceId childInstanceId)
    {
        return new CorrelationId(childInstanceId.Value.ToString("D"));
    }

    private static InstanceId DeterministicChildId(
        InstanceId parentInstanceId,
        CommandId commandId,
        int index)
    {
        Span<byte> input = stackalloc byte[36];
        parentInstanceId.Value.TryWriteBytes(input[..16]);
        commandId.Value.TryWriteBytes(input.Slice(16, 16));
        BitConverter.TryWriteBytes(input.Slice(32, 4), index);
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(input, hash);
        return new InstanceId(new Guid(hash[..16]));
    }

    private WorkflowInstanceSnapshot? ToInstanceSnapshot()
    {
        if (DefinitionId is not { } definitionId ||
            DefinitionVersion is not { } definitionVersion ||
            Status is not { } status ||
            CreatedAt is not { } createdAt ||
            UpdatedAt is not { } updatedAt)
        {
            return null;
        }

        return new WorkflowInstanceSnapshot
        {
            InstanceId = InstanceId,
            ParentInstanceId = ParentInstanceId,
            RootInstanceId = RootInstanceId,
            DefinitionId = definitionId,
            DefinitionVersion = definitionVersion,
            Status = status,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            ErrorSummary = ErrorSummary,
            EndOutcomeName = OutcomeName,
            ContinueAsNewGeneration = ContinueAsNewGeneration,
            ActiveWaits = waitState.CreateActiveWaitSnapshots(),
            SagaAudits = sagaState.CreateAuditScopes(Status)
        };
    }

    private WorkflowRuntimeCheckpointState ToCheckpointRuntimeState()
    {
        return new WorkflowRuntimeCheckpointState
        {
            ActiveTimers = activeTimers
                .Select(timer => new CheckpointActiveTimer(
                    timer.TimerId,
                    timer.FireAt,
                    timer.WakeupName,
                    timer.RegisteredAt))
                .ToArray(),
            ActiveWaits = waitState.CreateCheckpointActiveWaits(),
            BufferedDeliveries = waitState.CreateCheckpointBufferedDeliveries(),
            BufferedTimers = bufferedTimers
                .Select(timer => new CheckpointBufferedTimer(
                    timer.TimerId,
                    timer.WakeupName,
                    timer.BufferedAt))
                .ToArray(),
            ActiveChildren = activeChildren
                .Select(child => new CheckpointActiveChild(
                    child.GroupId,
                    child.ChildInstanceId,
                    child.WaitId,
                    child.FailurePolicy,
                    child.JoinPolicy,
                    child.ResidualPolicy,
                    child.ItemSnapshot))
                .ToArray(),
            ActiveChildGroups = activeChildGroups
                .Select(group => new CheckpointActiveChildGroup(
                    group.GroupId,
                    group.FailurePolicy,
                    group.JoinPolicy,
                    group.ResidualPolicy,
                    group.MaxConcurrency,
                    group.NextDispatchIndex,
                    group.Children))
                .ToArray(),
            ActiveResourceTickets = [.. activeResourceTickets],
            ActiveExternalJobs = externalJobState.CreateCheckpointActiveExternalJobs()
        };
    }
}

internal sealed record DurableDecision
{
    internal DurableDecision(
        IReadOnlyList<WorkflowEvent> events,
        CheckpointWrite? checkpoint = null,
        bool evictAfterCommit = false,
        IReadOnlyList<InboxWrite>? inboxOperations = null)
    {
        Events = events;
        Checkpoint = checkpoint;
        EvictAfterCommit = evictAfterCommit;
        InboxOperations = inboxOperations ?? [];
    }

    internal IReadOnlyList<WorkflowEvent> Events { get; }

    internal CheckpointWrite? Checkpoint { get; }

    internal bool EvictAfterCommit { get; }

    internal IReadOnlyList<InboxWrite> InboxOperations { get; }

    internal static DurableDecision Empty { get; } = new([]);
}

internal sealed record DurableAggregateSnapshot(
    InstanceId InstanceId,
    DefinitionId? DefinitionId,
    DefinitionVersion? DefinitionVersion,
    InstanceId? ParentInstanceId,
    InstanceId? RootInstanceId,
    WorkflowStatus? Status,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? LastStepPath,
    string? ErrorSummary,
    string? OutcomeName,
    int ContinueAsNewGeneration,
    IReadOnlyList<DurableActiveTimer> ActiveTimers,
    IReadOnlyList<DurableActiveWait> ActiveWaits,
    IReadOnlyList<DurableBufferedDelivery> BufferedDeliveries,
    IReadOnlyList<DurableBufferedTimer> BufferedTimers,
    IReadOnlyList<DurableActiveChild> ActiveChildren,
    IReadOnlyList<DurableActiveChildGroup> ActiveChildGroups,
    IReadOnlyList<ResourcePoolTicket> ActiveResourceTickets,
    IReadOnlyList<DurableActiveExternalJob> ActiveExternalJobs,
    IReadOnlyList<DurableSagaForwardAction> CompletedSagaForwardActions,
    IReadOnlyList<DurableSagaCompensationAction> SagaCompensationActions,
    IReadOnlyList<DurableSagaRecoveryIntervention> SagaRecoveryInterventions,
    IReadOnlyList<string> RequestedSagaCompensationScopes);

internal sealed record DurableAggregateCheckpoint(
    InstanceId InstanceId,
    StreamVersion StreamVersion,
    InstanceId? ParentInstanceId,
    InstanceId? RootInstanceId,
    DefinitionId? DefinitionId,
    DefinitionVersion? DefinitionVersion,
    WorkflowStatus? Status,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? LastStepPath,
    string? ErrorSummary,
    string? OutcomeName,
    int ContinueAsNewGeneration,
    IReadOnlyList<DurableActiveTimer> ActiveTimers,
    IReadOnlyList<DurableActiveWait> ActiveWaits,
    IReadOnlyList<DurableBufferedDelivery> BufferedDeliveries,
    IReadOnlyList<DurableBufferedTimer> BufferedTimers,
    IReadOnlyList<DurableActiveChild> ActiveChildren,
    IReadOnlyList<DurableActiveChildGroup> ActiveChildGroups,
    IReadOnlyList<ResourcePoolTicket> ActiveResourceTickets,
    IReadOnlyList<DurableActiveExternalJob> ActiveExternalJobs,
    string ContentType,
    byte[] Payload);

internal sealed record DurableActiveTimer(
    TimerId TimerId,
    DateTimeOffset FireAt,
    string WakeupName,
    DateTimeOffset RegisteredAt);

internal sealed record DurableActiveWait(
    WaitId WaitId,
    string EventName,
    CorrelationId CorrelationId,
    DateTimeOffset RegisteredAt,
    WaitMode Mode = WaitMode.Resident,
    string? BranchId = null);

internal sealed record DurableBufferedDelivery(
    EventId EventId,
    string EventName,
    CorrelationId CorrelationId,
    string? BranchId);

internal sealed record DurableBufferedTimer(
    TimerId TimerId,
    string WakeupName,
    DateTimeOffset BufferedAt);

internal sealed record DurableActiveChild(
    string GroupId,
    InstanceId ChildInstanceId,
    WaitId WaitId,
    RunChildFailurePolicy FailurePolicy,
    RunChildrenJoinPolicy JoinPolicy,
    RunChildrenResidualPolicy ResidualPolicy,
    string? ItemSnapshot);

internal sealed record DurableActiveChildGroup(
    string GroupId,
    RunChildFailurePolicy FailurePolicy,
    RunChildrenJoinPolicy JoinPolicy,
    RunChildrenResidualPolicy ResidualPolicy,
    int MaxConcurrency,
    int NextDispatchIndex,
    IReadOnlyList<WorkflowChildMaterialization> Children);

internal sealed record DurableCompletedChild(
    string GroupId,
    InstanceId ChildInstanceId,
    string? ItemSnapshot,
    DateTimeOffset CompletedAt);

internal sealed record DurableActiveExternalJob(
    string ExternalJobId,
    WaitId WaitId,
    TimerId? TimeoutTimerId);

internal sealed record DurableSagaForwardAction(
    string ScopeId,
    string ActionKey,
    string CompensationKey,
    DateTimeOffset CompletedAt);

internal sealed record DurableSagaCompensationAction(
    string ScopeId,
    string ActionKey,
    int Order,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? FailedAt,
    string? ErrorSummary,
    SagaCompensationActionStatus Status);

internal sealed record DurableSagaRecoveryIntervention(
    string ScopeId,
    string ActionKey,
    string OperatorId,
    string RecoveryAction,
    string? Reason,
    DateTimeOffset RecordedAt,
    WorkflowStatus TargetStatus);

internal sealed record DurableStepCompletedCommand(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt,
    string StepPath,
    string StateContentType,
    byte[] StatePayload);

internal sealed record DurableStepFailedCommand(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt,
    string StepPath,
    string ErrorSummary);

public sealed record DurableRunChildCommand(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt,
    InstanceId ChildInstanceId,
    DefinitionId ChildDefinitionId,
    DefinitionVersion ChildDefinitionVersion,
    RunChildFailurePolicy FailurePolicy);

public sealed record DurableChildCompletedCommand(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt,
    InstanceId ChildInstanceId,
    WorkflowStatus ChildStatus,
    string? ErrorSummary);

public sealed record DurableRunChildrenCommand(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt,
    DefinitionId ChildDefinitionId,
    DefinitionVersion ChildDefinitionVersion,
    IReadOnlyList<string> ItemSnapshots,
    RunChildFailurePolicy FailurePolicy,
    int? MaxConcurrency = null,
    RunChildrenJoinPolicy JoinPolicy = RunChildrenJoinPolicy.WhenAll,
    RunChildrenResidualPolicy ResidualPolicy = RunChildrenResidualPolicy.CancelRemaining);

internal sealed record DurableWaitRegisteredCommand(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt,
    WaitId WaitId,
    string EventName,
    CorrelationId CorrelationId,
    WaitMode Mode = WaitMode.Resident,
    string? BranchId = null);

internal sealed record DurableWaitMatchedCommand(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt,
    WaitId WaitId,
    EventId MatchedEventId);

internal sealed record DurablePauseCommand(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt);

internal sealed record DurableResumeCommand(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt,
    ResumeBufferedDeliveries BufferedDeliveries = ResumeBufferedDeliveries.Replay);

internal enum ResumeBufferedDeliveries
{
    Replay,
    Discard
}

internal sealed record DurableCompleteCommand(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt,
    string? OutcomeName);

internal sealed record DurableFailCommand(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt,
    string ErrorSummary);
