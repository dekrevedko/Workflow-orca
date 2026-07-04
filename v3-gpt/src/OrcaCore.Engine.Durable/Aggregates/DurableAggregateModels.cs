using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Engine.Durable.Aggregates;
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

public sealed record DurableYieldCommand(
    CommandId CommandId,
    InstanceId InstanceId,
    DateTimeOffset RequestedAt,
    string StepPath,
    string StateContentType,
    byte[] StatePayload);

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
