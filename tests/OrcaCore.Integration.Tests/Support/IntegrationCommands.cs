using System.Text.Json;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Aggregates;

namespace OrcaCore.Integration.Tests.Support;

internal static class IntegrationIds
{
    internal static DateTimeOffset Timestamp(int index) =>
        new DateTimeOffset(2026, 7, 3, 12, 0, 0, TimeSpan.Zero).AddMinutes(index);

    internal static Guid GuidValue(int value) =>
        Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");

    internal static InstanceId Instance(int value) => new(GuidValue(value));
    internal static CommandId Command(int value) => new(GuidValue(value));
    internal static EventId Event(int value) => new(GuidValue(value));
    internal static WaitId Wait(int value) => new(GuidValue(value));
    internal static TimerId Timer(int value) => new(GuidValue(value));
    internal static DefinitionId Definition(int value) => new(GuidValue(value));
    internal static CausationId Causation(int value) => new(GuidValue(value));
    internal static OutboxRecordId Outbox(int value) => new(GuidValue(value));
}

internal static class IntegrationCommands
{
    internal static StartWorkflowCommand Start(
        int instance = 1,
        int command = 1,
        string? idempotencyKey = null) =>
        new()
        {
            CommandId = IntegrationIds.Command(command),
            InstanceId = IntegrationIds.Instance(instance),
            RequestedAt = IntegrationIds.Timestamp(command),
            DefinitionId = IntegrationIds.Definition(1),
            DefinitionVersion = DefinitionVersion.Initial,
            IdempotencyKey = idempotencyKey
        };

    internal static DurableWaitRegisteredCommand WaitRegistered(
        int instance,
        int wait,
        int command,
        string? branchId = null,
        WaitMode mode = WaitMode.Resident) =>
        new(
            IntegrationIds.Command(command),
            IntegrationIds.Instance(instance),
            IntegrationIds.Timestamp(command),
            IntegrationIds.Wait(wait),
            "Approved",
            new CorrelationId("order-1"),
            mode,
            branchId);

    internal static DeliverEventCommand Deliver(
        int instance,
        int eventId,
        int command,
        string? branchId = null) =>
        new()
        {
            CommandId = IntegrationIds.Command(command),
            InstanceId = IntegrationIds.Instance(instance),
            RequestedAt = IntegrationIds.Timestamp(command),
            Envelope = new EventEnvelope
            {
                EventId = IntegrationIds.Event(eventId),
                EventName = "Approved",
                CorrelationId = new CorrelationId("order-1"),
                OccurredAt = IntegrationIds.Timestamp(command),
                BranchId = branchId
            }
        };

    internal static DurableExecutionEnvelopeV2 Envelope(
        int instance = 1,
        string stateContentType = "application/octet-stream",
        byte[]? statePayload = null,
        int rootIndex = 1) =>
        new()
        {
            EnvelopeVersion = DurableExecutionEnvelopeV2.CurrentVersion,
            InstanceId = IntegrationIds.Instance(instance),
            ContinueAsNewGeneration = 0,
            RootFiberId = "root",
            PlanBinding = new DurablePlanBinding
            {
                DefinitionId = IntegrationIds.Definition(1),
                DefinitionVersion = DefinitionVersion.Initial,
                CompilerFormatVersion = 1,
                PlanFingerprint = "integration-test-plan"
            },
            StateContentType = stateContentType,
            StatePayload = statePayload ?? [1],
            Fibers =
            [
                new DurableFiberState
                {
                    FiberId = "root",
                    InstructionId = $"root/{rootIndex}",
                    Phase = DurableFiberPhase.Runnable,
                    LoopIteration = 0,
                    NextScopeEntrySequence = 0
                }
            ],
            Scopes = [],
            Scheduler = new DurableFiberSchedulerState
            {
                RunnableFiberIds = ["root"],
                NextFiberId = "root"
            }
        };

    internal static DurableStepCompletedCommand StepCompleted(
        int instance,
        int command,
        string stepPath = "root/1") =>
        new(
            IntegrationIds.Command(command),
            IntegrationIds.Instance(instance),
            IntegrationIds.Timestamp(command),
            stepPath,
            Envelope(instance, "application/octet-stream", [(byte)command]));

    internal static DurableCompleteCommand Complete(int instance, int command) =>
        new(IntegrationIds.Command(command), IntegrationIds.Instance(instance), IntegrationIds.Timestamp(command), null);

    internal static DurablePauseCommand Pause(int instance, int command) =>
        new(IntegrationIds.Command(command), IntegrationIds.Instance(instance), IntegrationIds.Timestamp(command));

    internal static DurableResumeCommand Resume(
        int instance,
        int command,
        ResumeBufferedDeliveries bufferedDeliveries = ResumeBufferedDeliveries.Replay) =>
        new(
            IntegrationIds.Command(command),
            IntegrationIds.Instance(instance),
            IntegrationIds.Timestamp(command),
            bufferedDeliveries);

    internal static ScheduleTimerCommand ScheduleTimer(
        int instance,
        int timer,
        int command,
        DateTimeOffset fireAt,
        string wakeup = "timeout") =>
        new()
        {
            CommandId = IntegrationIds.Command(command),
            InstanceId = IntegrationIds.Instance(instance),
            RequestedAt = IntegrationIds.Timestamp(command),
            TimerId = IntegrationIds.Timer(timer),
            FireAt = fireAt,
            WakeupName = wakeup
        };

    internal static AcquireResourcePoolCommand Acquire(
        int instance,
        int command,
        string holderKey,
        params ResourcePoolRequirement[] requirements) =>
        new()
        {
            CommandId = IntegrationIds.Command(command),
            InstanceId = IntegrationIds.Instance(instance),
            RequestedAt = IntegrationIds.Timestamp(command),
            HolderKey = holderKey,
            Requirements = requirements,
            ExpiresAt = IntegrationIds.Timestamp(command).AddMinutes(30)
        };

    internal static RunExternalJobCommand RunExternalJob(
        int instance,
        int command,
        string jobId,
        params ResourcePoolRequirement[] requirements) =>
        new()
        {
            CommandId = IntegrationIds.Command(command),
            InstanceId = IntegrationIds.Instance(instance),
            RequestedAt = IntegrationIds.Timestamp(command),
            ExternalJobId = jobId,
            Payload = JsonSerializer.SerializeToUtf8Bytes(new { externalJobId = jobId }),
            Requirements = requirements,
            TimeoutAt = IntegrationIds.Timestamp(command).AddMinutes(30)
        };

    internal static CompleteExternalJobCommand CompleteExternalJob(
        int instance,
        int command,
        string jobId,
        int eventId) =>
        new()
        {
            CommandId = IntegrationIds.Command(command),
            InstanceId = IntegrationIds.Instance(instance),
            RequestedAt = IntegrationIds.Timestamp(command),
            ExternalJobId = jobId,
            CompletionEventId = IntegrationIds.Event(eventId)
        };

    internal static ContinueAsNewCommand ContinueAsNew(int instance, int command) =>
        new()
        {
            CommandId = IntegrationIds.Command(command),
            InstanceId = IntegrationIds.Instance(instance),
            RequestedAt = IntegrationIds.Timestamp(command),
            StateContentType = "application/json",
            StatePayload = [(byte)command]
        };

    internal static DurableRunChildrenCommand RunChildren(
        int instance,
        int command,
        params string[] items) =>
        new(
            IntegrationIds.Command(command),
            IntegrationIds.Instance(instance),
            IntegrationIds.Timestamp(command),
            IntegrationIds.Definition(2),
            DefinitionVersion.Initial,
            items,
            RunChildFailurePolicy.PropagateFailure,
            items.Length);

    internal static DurableRunChildrenCommand RunChildrenThrottled(
        int instance,
        int command,
        int maxConcurrency,
        params string[] items) =>
        new(
            IntegrationIds.Command(command),
            IntegrationIds.Instance(instance),
            IntegrationIds.Timestamp(command),
            IntegrationIds.Definition(2),
            DefinitionVersion.Initial,
            items,
            RunChildFailurePolicy.PropagateFailure,
            maxConcurrency);

    internal static DurableChildCompletedCommand ChildCompleted(
        int parentInstance,
        int command,
        InstanceId childInstanceId,
        WorkflowStatus status = WorkflowStatus.Completed) =>
        new(
            IntegrationIds.Command(command),
            IntegrationIds.Instance(parentInstance),
            IntegrationIds.Timestamp(command),
            childInstanceId,
            status,
            null);

    internal static TimeoutExternalJobCommand TimeoutExternalJob(
        int instance,
        int command,
        string jobId) =>
        new()
        {
            CommandId = IntegrationIds.Command(command),
            InstanceId = IntegrationIds.Instance(instance),
            RequestedAt = IntegrationIds.Timestamp(command),
            ExternalJobId = jobId
        };

    internal static ResourcePoolDefinition Pool(string name, int capacity) =>
        new(name, capacity, TimeSpan.FromMinutes(30));

    internal static ResourcePoolRequirement Requirement(string poolName, int count = 1) =>
        new(poolName, count);

    internal static ProviderCommitBatch OutboxOnlyBatch(
        int instance,
        params OutboxWrite[] records) =>
        new()
        {
            StreamId = new WorkflowStreamId(IntegrationIds.Instance(instance)),
            ExpectedVersion = StreamVersion.Empty,
            OutboxRecords = records
        };
}
