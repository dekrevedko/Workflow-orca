namespace ProductForbiddenLegacySurface;

public static class ForbiddenLegacySurface
{
    public static Type RuntimeBridge() =>
        typeof(global::OrcaCore.Internal.WorkflowRuntimeBridge);

    public static Type PublicAuthoringFactory() =>
        typeof(global::OrcaCore.Core.Authoring.PublicAuthoringContracts);

    public static object LegacyProjection(
        global::OrcaCore.Abstractions.Instances.WorkflowInstanceSnapshot snapshot) => snapshot;

    public static object PausedStatus() =>
        global::OrcaCore.Abstractions.Instances.WorkflowStatus.Paused;

    public static object SagaProjection(
        global::OrcaCore.Abstractions.Instances.SagaAuditSnapshot snapshot) => snapshot;

    public static object ChildPolicy() =>
        global::OrcaCore.Abstractions.Durable.RunChildrenJoinPolicy.WhenAll;

    public static object RawChildCommand(
        global::OrcaCore.Abstractions.Durable.CompensateChildGroupCommand command) => command;

    public static object DurableManagement(
        global::OrcaCore.Engine.Durable.Execution.DurableWorkflowRuntime runtime) => runtime.Management;

    public static async Task DurableDefinitionTargetedEventFanout(
        global::OrcaCore.Engine.Durable.Execution.DurableWorkflowRuntime runtime,
        global::OrcaCore.DefinitionId definitionId,
        global::OrcaCore.CorrelationId correlationId)
    {
        _ = await runtime.RaiseEventToDefinitionAsync(definitionId, "legacy", correlationId, new object());
    }

    public static object EphemeralManagement(
        global::OrcaCore.Engine.Ephemeral.EphemeralWorkflowEngine engine) => engine.Management;

    public static async Task EphemeralDefinitionTargetedEventFanout(
        global::OrcaCore.Engine.Ephemeral.EphemeralWorkflowEngine engine,
        global::OrcaCore.DefinitionId definitionId,
        global::OrcaCore.Abstractions.Events.EventEnvelope envelope)
    {
        _ = await engine.RaiseEventByDefinitionAsync<object>(definitionId, envelope, CancellationToken.None);
    }

    public static object RetentionPort(
        global::OrcaCore.Abstractions.Providers.IWorkflowRetentionStore store) => store;

    public static object RetentionPolicy(
        global::OrcaCore.Abstractions.Providers.RetentionPolicy policy) => policy;

    public static object ArchiveResult(
        global::OrcaCore.Abstractions.Providers.ArchiveResult result) => result;

    public static object PurgeResult(
        global::OrcaCore.Abstractions.Providers.PurgeResult result) => result;

    public static object BroadProjectionQuery(
        global::OrcaCore.Abstractions.Providers.WorkflowProjectionQuery query) => query;

    public static object ProjectionStatistics(
        global::OrcaCore.Abstractions.Providers.WorkflowProjectionStatistics statistics) => statistics;

    public static object ProjectionPolicy() =>
        global::OrcaCore.Abstractions.Providers.ProviderCommitPolicy.ProjectionMode;

    public static object ProviderSerializedPayload(
        global::OrcaCore.Abstractions.Providers.SerializedPayload payload) => payload;

    public static Type ProviderSerializerContext() =>
        typeof(global::OrcaCore.Abstractions.Providers.ProviderJsonSerializerContext);

    public static object SupersededLeaseGovernance(
        global::OrcaCore.Provider.Abstractions.ResourceGovernance.IResourceLeaseGovernanceStore store) => store;

    public static object LegacyForEachItemContext(
        global::OrcaCore.Abstractions.Steps.ForEachItemContext context) => context;

    public static object ExternalJobCommand(
        global::OrcaCore.Abstractions.Durable.RunExternalJobCommand command) => command;

    public static object DeferredRuntimePlaceholders() => new object[]
    {
        global::OrcaCore.Core.Execution.FiberBlockedReason.ExternalJob,
        global::OrcaCore.Core.Execution.FiberBlockedReason.ChildGroup,
        global::OrcaCore.Abstractions.Durable.DurableFiberBlockedReason.ExternalJob,
        global::OrcaCore.Abstractions.Durable.DurableFiberBlockedReason.ChildGroup,
        global::OrcaCore.Abstractions.Durable.DurableOwnedObligationKind.ExternalJob,
        global::OrcaCore.Abstractions.Durable.DurableOwnedObligationKind.ChildGroup
    };

    public static object BufferedDeliveryCheckpoint(
        global::OrcaCore.Abstractions.Providers.CheckpointBufferedDelivery checkpoint) => checkpoint;

    public static object BufferedTimerCheckpoint(
        global::OrcaCore.Abstractions.Providers.CheckpointBufferedTimer checkpoint) => checkpoint;

    public static object ChildCheckpoint(
        global::OrcaCore.Abstractions.Providers.CheckpointActiveChild checkpoint) => checkpoint;

    public static object ChildGroupCheckpoint(
        global::OrcaCore.Abstractions.Providers.CheckpointActiveChildGroup checkpoint) => checkpoint;

    public static object ExternalJobCheckpoint(
        global::OrcaCore.Abstractions.Providers.CheckpointActiveExternalJob checkpoint) => checkpoint;

    public static object SagaForwardCheckpoint(
        global::OrcaCore.Abstractions.Providers.CheckpointSagaForwardAction checkpoint) => checkpoint;

    public static object SagaCompensationCheckpoint(
        global::OrcaCore.Abstractions.Providers.CheckpointSagaCompensationAction checkpoint) => checkpoint;

    public static object SagaRecoveryCheckpoint(
        global::OrcaCore.Abstractions.Providers.CheckpointSagaRecoveryIntervention checkpoint) => checkpoint;

    public static object BufferedDeliveryEvent(
        global::OrcaCore.Abstractions.Durable.WorkflowDeliveryBufferedEvent workflowEvent) => workflowEvent;

    public static object BufferedTimerEvent(
        global::OrcaCore.Abstractions.Durable.WorkflowTimerBufferedEvent workflowEvent) => workflowEvent;

    public static object DiscardedDeliveryEvent(
        global::OrcaCore.Abstractions.Durable.WorkflowDeliveryDiscardedEvent workflowEvent) => workflowEvent;

    public static object PayloadSerializer(
        global::OrcaCore.Abstractions.Providers.IWorkflowPayloadSerializer serializer) => serializer;

    public static object PayloadCodec(
        global::OrcaCore.Abstractions.Providers.IWorkflowPayloadCodec codec) => codec;

    public static object StructuredValueCodec(
        global::OrcaCore.Core.Execution.IStructuredValueCodec codec) => codec;

    public static object EphemeralSnapshotter(
        global::OrcaCore.Engine.Ephemeral.IEphemeralStateSnapshotter snapshotter) => snapshotter;

    public static object EphemeralSnapshotterOption(
        global::OrcaCore.Engine.Ephemeral.EphemeralWorkflowEngineOptions options) => options.StateSnapshotter;

    public static object HostingCodecOptions(
        global::OrcaCore.Hosting.WorkflowPayloadSerializationOptions options) => options;

    public static Type CatchAllHosting() =>
        typeof(global::OrcaCore.Hosting.OrcaCoreServiceCollectionExtensions);

    public static Type OpenTelemetryHosting() =>
        typeof(global::OrcaCore.Hosting.OrcaCoreOpenTelemetryServiceCollectionExtensions);

    public static object RemovedYieldResult(global::OrcaCore.EngineYieldStepResult result) => result;

    public static object RuntimeOwnedYieldCommand(
        global::OrcaCore.Engine.Durable.Aggregates.DurableYieldCommand command) => command;

    public static object RemovedContinueAsNewResult<TState>(
        global::OrcaCore.EngineContinueAsNewStepResult<TState> result) => result;

    public static object RemovedExternalJobResult(global::OrcaCore.EngineExternalJobStepResult result) => result;

    public static object RemovedAcquireResourcesResult(global::OrcaCore.EngineAcquireResourcesStepResult result) => result;
}
