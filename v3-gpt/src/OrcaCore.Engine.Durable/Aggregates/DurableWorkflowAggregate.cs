using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Engine.Durable.Aggregates;

internal sealed class DurableWorkflowAggregate
{
    private readonly List<DurableActiveWait> activeWaits;

    private DurableWorkflowAggregate(
        InstanceId instanceId,
        StreamVersion streamVersion,
        DefinitionId? definitionId,
        DefinitionVersion? definitionVersion,
        WorkflowStatus? status,
        string? lastStepPath,
        string? errorSummary,
        string? outcomeName,
        IEnumerable<DurableActiveWait> activeWaits)
    {
        InstanceId = instanceId;
        StreamVersion = streamVersion;
        DefinitionId = definitionId;
        DefinitionVersion = definitionVersion;
        Status = status;
        LastStepPath = lastStepPath;
        ErrorSummary = errorSummary;
        OutcomeName = outcomeName;
        this.activeWaits = [.. activeWaits];
    }

    internal InstanceId InstanceId { get; private set; }

    internal StreamVersion StreamVersion { get; private set; }

    internal DefinitionId? DefinitionId { get; private set; }

    internal DefinitionVersion? DefinitionVersion { get; private set; }

    internal WorkflowStatus? Status { get; private set; }

    internal string? LastStepPath { get; private set; }

    internal string? ErrorSummary { get; private set; }

    internal string? OutcomeName { get; private set; }

    internal DurableAggregateSnapshot Snapshot => new(
        InstanceId,
        DefinitionId,
        DefinitionVersion,
        Status,
        LastStepPath,
        ErrorSummary,
        OutcomeName,
        [.. activeWaits]);

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
                checkpoint.LastStepPath,
                checkpoint.ErrorSummary,
                checkpoint.OutcomeName,
                checkpoint.ActiveWaits);

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
            LastStepPath,
            ErrorSummary,
            OutcomeName,
            [.. activeWaits],
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
                    waitRegistered.Mode));
                Status = WorkflowStatus.Waiting;
                break;
            case WorkflowWaitMatchedEvent waitMatched:
                activeWaits.RemoveAll(wait => wait.WaitId == waitMatched.WaitId);
                Status = activeWaits.Count == 0 ? WorkflowStatus.Running : WorkflowStatus.Waiting;
                break;
            case WorkflowCompletedEvent completed:
                OutcomeName = completed.OutcomeName;
                Status = WorkflowStatus.Completed;
                activeWaits.Clear();
                break;
            case WorkflowTerminalEvent terminal:
                Status = terminal.Status;
                if (IsTerminal)
                {
                    activeWaits.Clear();
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
}

internal sealed record DurableDecision(
    IReadOnlyList<WorkflowEvent> Events,
    CheckpointWrite? Checkpoint = null,
    bool EvictAfterCommit = false)
{
    internal static DurableDecision Empty { get; } = new([]);
}

internal sealed record DurableAggregateSnapshot(
    InstanceId InstanceId,
    DefinitionId? DefinitionId,
    DefinitionVersion? DefinitionVersion,
    WorkflowStatus? Status,
    string? LastStepPath,
    string? ErrorSummary,
    string? OutcomeName,
    IReadOnlyList<DurableActiveWait> ActiveWaits);

internal sealed record DurableAggregateCheckpoint(
    InstanceId InstanceId,
    StreamVersion StreamVersion,
    DefinitionId? DefinitionId,
    DefinitionVersion? DefinitionVersion,
    WorkflowStatus? Status,
    string? LastStepPath,
    string? ErrorSummary,
    string? OutcomeName,
    IReadOnlyList<DurableActiveWait> ActiveWaits,
    string ContentType,
    byte[] Payload);

internal sealed record DurableActiveWait(
    WaitId WaitId,
    string EventName,
    CorrelationId CorrelationId,
    WaitMode Mode = WaitMode.Resident);

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
