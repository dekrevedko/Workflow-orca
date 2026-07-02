using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Engine.Durable.Aggregates;

internal sealed class DurableWorkflowAggregate
{
    private readonly List<DurableActiveWait> activeWaits;
    private readonly List<DurableBufferedDelivery> bufferedDeliveries;

    private DurableWorkflowAggregate(
        InstanceId instanceId,
        StreamVersion streamVersion,
        DefinitionId? definitionId,
        DefinitionVersion? definitionVersion,
        WorkflowStatus? status,
        DateTimeOffset? createdAt,
        DateTimeOffset? updatedAt,
        string? lastStepPath,
        string? errorSummary,
        string? outcomeName,
        IEnumerable<DurableActiveWait> activeWaits,
        IEnumerable<DurableBufferedDelivery> bufferedDeliveries)
    {
        InstanceId = instanceId;
        StreamVersion = streamVersion;
        DefinitionId = definitionId;
        DefinitionVersion = definitionVersion;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        LastStepPath = lastStepPath;
        ErrorSummary = errorSummary;
        OutcomeName = outcomeName;
        this.activeWaits = [.. activeWaits];
        this.bufferedDeliveries = [.. bufferedDeliveries];
    }

    internal InstanceId InstanceId { get; private set; }

    internal StreamVersion StreamVersion { get; private set; }

    internal DefinitionId? DefinitionId { get; private set; }

    internal DefinitionVersion? DefinitionVersion { get; private set; }

    internal WorkflowStatus? Status { get; private set; }

    internal DateTimeOffset? CreatedAt { get; private set; }

    internal DateTimeOffset? UpdatedAt { get; private set; }

    internal string? LastStepPath { get; private set; }

    internal string? ErrorSummary { get; private set; }

    internal string? OutcomeName { get; private set; }

    internal DurableAggregateSnapshot Snapshot => new(
        InstanceId,
        DefinitionId,
        DefinitionVersion,
        Status,
        CreatedAt,
        UpdatedAt,
        LastStepPath,
        ErrorSummary,
        OutcomeName,
        [.. activeWaits],
        [.. bufferedDeliveries]);

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
                checkpoint.DefinitionId,
                checkpoint.DefinitionVersion,
                checkpoint.Status,
                checkpoint.CreatedAt,
                checkpoint.UpdatedAt,
                checkpoint.LastStepPath,
                checkpoint.ErrorSummary,
                checkpoint.OutcomeName,
                checkpoint.ActiveWaits,
                checkpoint.BufferedDeliveries);

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
            DefinitionId,
            DefinitionVersion,
            Status,
            CreatedAt,
            UpdatedAt,
            LastStepPath,
            ErrorSummary,
            OutcomeName,
            [.. activeWaits],
            [.. bufferedDeliveries],
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
            DefinitionVersion = DefinitionVersion,
            Status = WorkflowStatus.Running,
            LastStepPath = command.StepPath,
            ErrorSummary = null,
            OutcomeName = null
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
            DefinitionId,
            DefinitionVersion,
            Status,
            CreatedAt,
            UpdatedAt,
            LastStepPath,
            ErrorSummary,
            OutcomeName,
            activeWaits,
            bufferedDeliveries);

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
            or WorkflowStatus.Terminated;

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
                DefinitionId = started.DefinitionId;
                DefinitionVersion = started.DefinitionVersion;
                Status = WorkflowStatus.Running;
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
                activeWaits.Clear();
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
                activeWaits.Clear();
                bufferedDeliveries.Clear();
                break;
            case WorkflowTerminalEvent terminal:
                Status = terminal.Status;
                if (IsTerminal)
                {
                    activeWaits.Clear();
                    bufferedDeliveries.Clear();
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
            DefinitionId = definitionId,
            DefinitionVersion = definitionVersion,
            Status = status,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            ErrorSummary = ErrorSummary,
            EndOutcomeName = OutcomeName,
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
                .ToArray()
        };
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
    WorkflowStatus? Status,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? LastStepPath,
    string? ErrorSummary,
    string? OutcomeName,
    IReadOnlyList<DurableActiveWait> ActiveWaits,
    IReadOnlyList<DurableBufferedDelivery> BufferedDeliveries);

internal sealed record DurableAggregateCheckpoint(
    InstanceId InstanceId,
    StreamVersion StreamVersion,
    DefinitionId? DefinitionId,
    DefinitionVersion? DefinitionVersion,
    WorkflowStatus? Status,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? LastStepPath,
    string? ErrorSummary,
    string? OutcomeName,
    IReadOnlyList<DurableActiveWait> ActiveWaits,
    IReadOnlyList<DurableBufferedDelivery> BufferedDeliveries,
    string ContentType,
    byte[] Payload);

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
