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
    private readonly List<DurableActiveWait> activeWaits;
    private readonly List<DurableBufferedDelivery> bufferedDeliveries;
    private readonly List<DurableActiveChild> activeChildren;
    private readonly List<DurableCompletedChild> completedChildren = [];
    private readonly List<ResourcePoolTicket> activeResourceTickets;
    private readonly List<DurableActiveExternalJob> activeExternalJobs;
    private readonly List<DurableSagaForwardAction> completedSagaForwardActions = [];
    private readonly List<DurableSagaCompensationAction> sagaCompensationActions = [];
    private readonly List<DurableSagaRecoveryIntervention> sagaRecoveryInterventions = [];
    private readonly HashSet<string> requestedSagaCompensationScopes = new(StringComparer.Ordinal);
    private readonly HashSet<string> compensatedChildGroups = new(StringComparer.Ordinal);

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
        IEnumerable<DurableActiveChild> activeChildren,
        IEnumerable<ResourcePoolTicket> activeResourceTickets,
        IEnumerable<DurableActiveExternalJob> activeExternalJobs,
        IEnumerable<DurableSagaForwardAction> completedSagaForwardActions,
        IEnumerable<DurableSagaCompensationAction> sagaCompensationActions,
        IEnumerable<DurableSagaRecoveryIntervention> sagaRecoveryInterventions,
        IEnumerable<string> requestedSagaCompensationScopes)
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
        this.activeWaits = [.. activeWaits];
        this.bufferedDeliveries = [.. bufferedDeliveries];
        this.activeChildren = [.. activeChildren];
        this.activeResourceTickets = [.. activeResourceTickets];
        this.activeExternalJobs = [.. activeExternalJobs];
        this.completedSagaForwardActions = [.. completedSagaForwardActions];
        this.sagaCompensationActions = [.. sagaCompensationActions];
        this.sagaRecoveryInterventions = [.. sagaRecoveryInterventions];
        this.requestedSagaCompensationScopes = new HashSet<string>(requestedSagaCompensationScopes, StringComparer.Ordinal);
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
        [.. activeWaits],
        [.. bufferedDeliveries],
        [.. activeChildren],
        [.. activeResourceTickets],
        [.. activeExternalJobs],
        [.. completedSagaForwardActions],
        [.. sagaCompensationActions],
        [.. sagaRecoveryInterventions],
        [.. requestedSagaCompensationScopes]);

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
                checkpoint.ActiveChildren,
                checkpoint.ActiveResourceTickets,
                checkpoint.ActiveExternalJobs,
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
            [.. activeWaits],
            [.. bufferedDeliveries],
            [.. activeChildren],
            [.. activeResourceTickets],
            [.. activeExternalJobs],
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
                DefinitionVersion = command.DefinitionVersion
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
            ContinueAsNewGeneration = ContinueAsNewGeneration
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

        if (command.ChildStatus == WorkflowStatus.Failed &&
            activeChild.FailurePolicy is RunChildFailurePolicy.PropagateFailure)
        {
            return new DurableDecision([
                childCompleted,
                .. ResidualIntentIfNeeded(command, activeChild),
                .. ResumeTokenIfGroupComplete(command, activeChild),
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
            .. ResidualIntentIfNeeded(command, activeChild),
            .. ResumeTokenIfGroupComplete(command, activeChild),
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
                ItemSnapshot = itemSnapshot
            })
            .ToArray();
        var initialDispatchCount = Math.Min(command.MaxConcurrency ?? children.Length, children.Length);
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

        return new DurableDecision([
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
                Mode = command.Mode
            }
        ], null, command.Mode == WaitMode.Cold);
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
        if (IsTerminal || activeExternalJobs.Any(job =>
                string.Equals(job.ExternalJobId, command.ExternalJobId, StringComparison.Ordinal)))
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
        var job = activeExternalJobs.FirstOrDefault(candidate =>
            string.Equals(candidate.ExternalJobId, command.ExternalJobId, StringComparison.Ordinal));
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
        var job = activeExternalJobs.FirstOrDefault(candidate =>
            string.Equals(candidate.ExternalJobId, command.ExternalJobId, StringComparison.Ordinal));
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

        var events = new List<WorkflowEvent>();
        foreach (var externalJob in activeExternalJobs)
        {
            events.Add(new WorkflowExternalJobStopRequestedEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                ParentInstanceId = ParentInstanceId,
                RootInstanceId = RootInstanceId ?? InstanceId,
                ExternalJobId = externalJob.ExternalJobId
            });
        }

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

    internal DurableDecision DecideRecordSagaForwardActionCompleted(
        RecordSagaForwardActionCompletedCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (IsTerminal || completedSagaForwardActions.Any(action =>
                string.Equals(action.ScopeId, command.ScopeId, StringComparison.Ordinal) &&
                string.Equals(action.ActionKey, command.ActionKey, StringComparison.Ordinal)))
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
        if (IsTerminal || requestedSagaCompensationScopes.Contains(command.ScopeId))
        {
            return DurableDecision.Empty;
        }

        return new DurableDecision(CreateSagaCompensationPlanEvents(
            command.CommandId,
            command.InstanceId,
            command.RequestedAt,
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

        if (command.CompensateScope && !requestedSagaCompensationScopes.Contains(command.ScopeId))
        {
            events.AddRange(CreateSagaCompensationPlanEvents(
                command.CommandId,
                command.InstanceId,
                command.RequestedAt,
                command.ScopeId,
                "timeout"));
        }

        return new DurableDecision(events);
    }

    private IReadOnlyList<WorkflowEvent> CreateSagaCompensationPlanEvents(
        CommandId commandId,
        InstanceId instanceId,
        DateTimeOffset requestedAt,
        string scopeId,
        string? reason)
    {
        var events = new List<WorkflowEvent>
        {
            new SagaCompensationRequestedEvent
            {
                EventId = EventId.New(),
                InstanceId = instanceId,
                CommandId = commandId,
                CausationId = ToCausationId(commandId),
                OccurredAt = requestedAt,
                ParentInstanceId = ParentInstanceId,
                RootInstanceId = RootInstanceId ?? InstanceId,
                ScopeId = scopeId,
                Reason = reason
            }
        };
        var eligibleActions = completedSagaForwardActions
            .Where(action => string.Equals(action.ScopeId, scopeId, StringComparison.Ordinal))
            .Where(action => !string.IsNullOrWhiteSpace(action.CompensationKey))
            .Reverse()
            .ToArray();
        for (var index = 0; index < eligibleActions.Length; index++)
        {
            events.Add(new SagaCompensationStartedEvent
            {
                EventId = EventId.New(),
                InstanceId = instanceId,
                CommandId = commandId,
                CausationId = ToCausationId(commandId),
                OccurredAt = requestedAt,
                ParentInstanceId = ParentInstanceId,
                RootInstanceId = RootInstanceId ?? InstanceId,
                ScopeId = scopeId,
                ActionKey = eligibleActions[index].CompensationKey,
                Order = index
            });
        }

        return events;
    }

    internal DurableDecision DecideCompleteSagaCompensation(CompleteSagaCompensationCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (IsTerminal)
        {
            return DurableDecision.Empty;
        }

        return new DurableDecision([
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
                Status = WorkflowStatus.Compensated
            }
        ], null, true);
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
        if (IsTerminal || activeWaits.All(wait => wait.WaitId != command.WaitId))
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
                    CorrelationId = command.Envelope.CorrelationId
                }
            ]);
        }

        var wait = FindActiveWait(command.Envelope);
        if (wait is null)
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
        var inboxOperations = new List<InboxWrite>();
        var replayWaits = activeWaits.ToList();

        foreach (var bufferedDelivery in bufferedDeliveries)
        {
            if (command.BufferedDeliveries == ResumeBufferedDeliveries.Discard)
            {
                events.Add(new WorkflowDeliveryDiscardedEvent
                {
                    EventId = EventId.New(),
                    InstanceId = command.InstanceId,
                    CommandId = command.CommandId,
                    CausationId = ToCausationId(command.CommandId),
                    OccurredAt = command.RequestedAt,
                    DiscardedEventId = bufferedDelivery.EventId
                });
                inboxOperations.Add(new InboxWrite(bufferedDelivery.EventId, InboxRecordState.DiscardedOnResume));
                continue;
            }

            var wait = replayWaits.FirstOrDefault(candidate =>
                candidate.EventName == bufferedDelivery.EventName &&
                candidate.CorrelationId == bufferedDelivery.CorrelationId);
            if (wait is null)
            {
                inboxOperations.Add(new InboxWrite(bufferedDelivery.EventId, InboxRecordState.Poisoned));
                continue;
            }

            replayWaits.RemoveAll(candidate => candidate.WaitId == wait.WaitId);
            events.Add(new WorkflowWaitMatchedEvent
            {
                EventId = EventId.New(),
                InstanceId = command.InstanceId,
                CommandId = command.CommandId,
                CausationId = ToCausationId(command.CommandId),
                OccurredAt = command.RequestedAt,
                WaitId = wait.WaitId,
                MatchedEventId = bufferedDelivery.EventId
            });
            inboxOperations.Add(new InboxWrite(bufferedDelivery.EventId, InboxRecordState.Applied));
        }

        return new DurableDecision(events, null, false, inboxOperations);
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
            activeWaits,
            bufferedDeliveries,
            activeChildren,
            activeResourceTickets,
            activeExternalJobs,
            completedSagaForwardActions,
            sagaCompensationActions,
            sagaRecoveryInterventions,
            requestedSagaCompensationScopes);

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
                activeWaits.Clear();
                bufferedDeliveries.Clear();
                activeChildren.Clear();
                activeResourceTickets.Clear();
                activeExternalJobs.Clear();
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
                activeWaits.Clear();
                activeChildren.Clear();
                break;
            case WorkflowWaitRegisteredEvent waitRegistered:
                activeWaits.Add(new DurableActiveWait(
                    waitRegistered.WaitId,
                    waitRegistered.EventName,
                    waitRegistered.CorrelationId,
                    waitRegistered.OccurredAt,
                    waitRegistered.Mode));
                Status = WorkflowStatus.Waiting;
                break;
            case WorkflowWaitMatchedEvent waitMatched:
                activeWaits.RemoveAll(wait => wait.WaitId == waitMatched.WaitId);
                bufferedDeliveries.RemoveAll(delivery => delivery.EventId == waitMatched.MatchedEventId);
                Status = activeWaits.Count == 0 ? WorkflowStatus.Running : WorkflowStatus.Waiting;
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
                Status = activeWaits.Count == 0 && activeTimers.Count == 0
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
                activeWaits.Add(new DurableActiveWait(
                    childScheduled.WaitId,
                    "ChildCompleted",
                    ChildCorrelation(childScheduled.ChildInstanceId),
                    childScheduled.OccurredAt));
                Status = WorkflowStatus.Waiting;
                break;
            case WorkflowChildrenScheduledEvent childrenScheduled:
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
                    activeWaits.Add(new DurableActiveWait(
                        waitId,
                        "ChildCompleted",
                        ChildCorrelation(child.ChildInstanceId),
                        childrenScheduled.OccurredAt));
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
                        activeWaits.RemoveAll(wait => wait.WaitId == residualChild.WaitId);
                    }

                    activeChildren.RemoveAll(child => child.ChildInstanceId == residualChildId);
                }

                break;
            case WorkflowParentResumeTokenRecordedEvent:
                break;
            case WorkflowResourcePoolAcquiredEvent resourcePoolAcquired:
                activeResourceTickets.RemoveAll(ticket =>
                    string.Equals(ticket.HolderKey, resourcePoolAcquired.HolderKey, StringComparison.Ordinal));
                activeResourceTickets.AddRange(resourcePoolAcquired.Tickets);
                Status = WorkflowStatus.Running;
                break;
            case WorkflowResourcePoolQueuedEvent resourcePoolQueued:
                activeWaits.Add(new DurableActiveWait(
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
                activeExternalJobs.Add(new DurableActiveExternalJob(
                    externalJobStarted.ExternalJobId,
                    externalJobStarted.WaitId,
                    externalJobStarted.TimeoutTimerId));
                break;
            case WorkflowExternalJobCompletedEvent externalJobCompleted:
                activeExternalJobs.RemoveAll(job =>
                    string.Equals(job.ExternalJobId, externalJobCompleted.ExternalJobId, StringComparison.Ordinal));
                break;
            case WorkflowExternalJobTimedOutEvent externalJobTimedOut:
                activeExternalJobs.RemoveAll(job =>
                    string.Equals(job.ExternalJobId, externalJobTimedOut.ExternalJobId, StringComparison.Ordinal));
                break;
            case WorkflowExternalJobStopRequestedEvent:
                break;
            case SagaForwardActionCompletedEvent sagaForwardActionCompleted:
                completedSagaForwardActions.RemoveAll(action =>
                    string.Equals(action.ScopeId, sagaForwardActionCompleted.ScopeId, StringComparison.Ordinal) &&
                    string.Equals(action.ActionKey, sagaForwardActionCompleted.ActionKey, StringComparison.Ordinal));
                completedSagaForwardActions.Add(new DurableSagaForwardAction(
                    sagaForwardActionCompleted.ScopeId,
                    sagaForwardActionCompleted.ActionKey,
                    sagaForwardActionCompleted.CompensationKey,
                    sagaForwardActionCompleted.OccurredAt));
                break;
            case SagaForwardActionTimedOutEvent:
                break;
            case SagaCompensationRequestedEvent sagaCompensationRequested:
                requestedSagaCompensationScopes.Add(sagaCompensationRequested.ScopeId);
                break;
            case SagaCompensationStartedEvent sagaCompensationStarted:
                sagaCompensationActions.RemoveAll(action =>
                    string.Equals(action.ScopeId, sagaCompensationStarted.ScopeId, StringComparison.Ordinal) &&
                    string.Equals(action.ActionKey, sagaCompensationStarted.ActionKey, StringComparison.Ordinal));
                sagaCompensationActions.Add(new DurableSagaCompensationAction(
                    sagaCompensationStarted.ScopeId,
                    sagaCompensationStarted.ActionKey,
                    sagaCompensationStarted.Order,
                    sagaCompensationStarted.OccurredAt,
                    null,
                    null,
                    null,
                    SagaCompensationActionStatus.Started));
                break;
            case SagaCompensationCompletedEvent sagaCompensationCompleted:
                UpdateSagaCompensationAction(
                    sagaCompensationCompleted.ScopeId,
                    sagaCompensationCompleted.ActionKey,
                    sagaCompensationCompleted.OccurredAt,
                    null,
                    null,
                    SagaCompensationActionStatus.Completed);
                break;
            case SagaCompensationFailedEvent sagaCompensationFailed:
                UpdateSagaCompensationAction(
                    sagaCompensationFailed.ScopeId,
                    sagaCompensationFailed.ActionKey,
                    null,
                    sagaCompensationFailed.OccurredAt,
                    sagaCompensationFailed.ErrorSummary,
                    SagaCompensationActionStatus.Failed);
                break;
            case SagaManualRecoveryRecordedEvent sagaManualRecoveryRecorded:
                sagaRecoveryInterventions.Add(new DurableSagaRecoveryIntervention(
                    sagaManualRecoveryRecorded.ScopeId,
                    sagaManualRecoveryRecorded.ActionKey,
                    sagaManualRecoveryRecorded.OperatorId,
                    sagaManualRecoveryRecorded.RecoveryAction,
                    sagaManualRecoveryRecorded.Reason,
                    sagaManualRecoveryRecorded.OccurredAt,
                    sagaManualRecoveryRecorded.TargetStatus));
                break;
            case WorkflowTimerBufferedEvent timerBuffered:
                activeTimers.RemoveAll(timer => timer.TimerId == timerBuffered.TimerId);
                Status = WorkflowStatus.Paused;
                break;
            case WorkflowPausedEvent:
                Status = WorkflowStatus.Paused;
                break;
            case WorkflowResumedEvent:
                Status = activeWaits.Count == 0 ? WorkflowStatus.Running : WorkflowStatus.Waiting;
                break;
            case WorkflowDeliveryBufferedEvent deliveryBuffered:
                bufferedDeliveries.Add(new DurableBufferedDelivery(
                    deliveryBuffered.BufferedEventId,
                    deliveryBuffered.EventName,
                    deliveryBuffered.CorrelationId));
                Status = WorkflowStatus.Paused;
                break;
            case WorkflowDeliveryDiscardedEvent deliveryDiscarded:
                bufferedDeliveries.RemoveAll(delivery => delivery.EventId == deliveryDiscarded.DiscardedEventId);
                break;
            case WorkflowCompletedEvent completed:
                OutcomeName = completed.OutcomeName;
                Status = WorkflowStatus.Completed;
                activeTimers.Clear();
                activeWaits.Clear();
                bufferedDeliveries.Clear();
                activeChildren.Clear();
                activeResourceTickets.Clear();
                activeExternalJobs.Clear();
                break;
            case WorkflowTerminalEvent terminal:
                Status = terminal.Status;
                if (IsTerminal)
                {
                    activeTimers.Clear();
                    activeWaits.Clear();
                    bufferedDeliveries.Clear();
                    activeChildren.Clear();
                    activeResourceTickets.Clear();
                    activeExternalJobs.Clear();
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
        DurableActiveChild activeChild)
    {
        if (activeChild.JoinPolicy is RunChildrenJoinPolicy.WhenAny)
        {
            return [ResumeToken(command, activeChild)];
        }

        var remainingInGroup = activeChildren.Count(child =>
            string.Equals(child.GroupId, activeChild.GroupId, StringComparison.Ordinal) &&
            child.ChildInstanceId != activeChild.ChildInstanceId);
        if (remainingInGroup > 0)
        {
            return [];
        }

        return
        [
            ResumeToken(command, activeChild)
        ];
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

    private void UpdateSagaCompensationAction(
        string scopeId,
        string actionKey,
        DateTimeOffset? completedAt,
        DateTimeOffset? failedAt,
        string? errorSummary,
        SagaCompensationActionStatus status)
    {
        var existing = sagaCompensationActions.FirstOrDefault(action =>
            string.Equals(action.ScopeId, scopeId, StringComparison.Ordinal) &&
            string.Equals(action.ActionKey, actionKey, StringComparison.Ordinal));
        if (existing is null)
        {
            sagaCompensationActions.Add(new DurableSagaCompensationAction(
                scopeId,
                actionKey,
                0,
                completedAt ?? failedAt ?? UpdatedAt ?? DateTimeOffset.MinValue,
                completedAt,
                failedAt,
                errorSummary,
                status));
            return;
        }

        sagaCompensationActions.Remove(existing);
        sagaCompensationActions.Add(existing with
        {
            CompletedAt = completedAt ?? existing.CompletedAt,
            FailedAt = failedAt ?? existing.FailedAt,
            ErrorSummary = errorSummary ?? existing.ErrorSummary,
            Status = status
        });
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
            ActiveWaits = activeWaits
                .Select(wait => new ActiveWaitSnapshot
                {
                    WaitId = wait.WaitId,
                    EventName = wait.EventName,
                    CorrelationId = wait.CorrelationId,
                    RegisteredAt = wait.RegisteredAt,
                    Status = "Active",
                    Mode = wait.Mode.ToString()
                })
                .ToArray(),
            SagaAudits = CreateSagaAuditScopes()
        };
    }

    private IReadOnlyList<SagaAuditScopeSnapshot> CreateSagaAuditScopes()
    {
        var scopeIds = completedSagaForwardActions.Select(action => action.ScopeId)
            .Concat(sagaCompensationActions.Select(action => action.ScopeId))
            .Concat(sagaRecoveryInterventions.Select(intervention => intervention.ScopeId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(scopeId => scopeId, StringComparer.Ordinal)
            .ToArray();

        return scopeIds.Select(scopeId => new SagaAuditScopeSnapshot
        {
            ScopeId = scopeId,
            Outcome = Status is WorkflowStatus.Compensated or WorkflowStatus.CompensationFailed ? Status : null,
            ForwardActions = completedSagaForwardActions
                .Where(action => string.Equals(action.ScopeId, scopeId, StringComparison.Ordinal))
                .OrderBy(action => action.CompletedAt)
                .ThenBy(action => action.ActionKey, StringComparer.Ordinal)
                .Select(action => new SagaForwardActionSnapshot
                {
                    ScopeId = action.ScopeId,
                    ActionKey = action.ActionKey,
                    CompensationKey = action.CompensationKey,
                    CompletedAt = action.CompletedAt
                })
                .ToArray(),
            CompensationActions = sagaCompensationActions
                .Where(action => string.Equals(action.ScopeId, scopeId, StringComparison.Ordinal))
                .OrderBy(action => action.Order)
                .ThenBy(action => action.ActionKey, StringComparer.Ordinal)
                .Select(action => new SagaCompensationActionSnapshot
                {
                    ScopeId = action.ScopeId,
                    ActionKey = action.ActionKey,
                    Order = action.Order,
                    StartedAt = action.StartedAt,
                    CompletedAt = action.CompletedAt,
                    FailedAt = action.FailedAt,
                    ErrorSummary = action.ErrorSummary,
                    Status = action.Status
                })
                .ToArray(),
            RecoveryInterventions = sagaRecoveryInterventions
                .Where(intervention => string.Equals(intervention.ScopeId, scopeId, StringComparison.Ordinal))
                .OrderBy(intervention => intervention.RecordedAt)
                .ThenBy(intervention => intervention.ActionKey, StringComparer.Ordinal)
                .Select(intervention => new SagaRecoveryInterventionSnapshot
                {
                    ScopeId = intervention.ScopeId,
                    ActionKey = intervention.ActionKey,
                    OperatorId = intervention.OperatorId,
                    RecoveryAction = intervention.RecoveryAction,
                    Reason = intervention.Reason,
                    RecordedAt = intervention.RecordedAt,
                    TargetStatus = intervention.TargetStatus
                })
                .ToArray()
        }).ToArray();
    }

    private DurableActiveWait? FindActiveWait(EventEnvelope envelope)
    {
        return activeWaits.FirstOrDefault(wait =>
            wait.EventName == envelope.EventName &&
            wait.CorrelationId == envelope.CorrelationId);
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
    IReadOnlyList<DurableActiveChild> ActiveChildren,
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
    IReadOnlyList<DurableActiveChild> ActiveChildren,
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
    WaitMode Mode = WaitMode.Resident);

internal sealed record DurableBufferedDelivery(
    EventId EventId,
    string EventName,
    CorrelationId CorrelationId);

internal sealed record DurableActiveChild(
    string GroupId,
    InstanceId ChildInstanceId,
    WaitId WaitId,
    RunChildFailurePolicy FailurePolicy,
    RunChildrenJoinPolicy JoinPolicy,
    RunChildrenResidualPolicy ResidualPolicy,
    string? ItemSnapshot);

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
    WaitMode Mode = WaitMode.Resident);

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
