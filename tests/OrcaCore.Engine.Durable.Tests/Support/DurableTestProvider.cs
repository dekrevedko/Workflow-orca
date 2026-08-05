using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Providers.InMemory;
using DurableWorkflowEvent = OrcaCore.Abstractions.Durable.WorkflowEvent;

namespace OrcaCore.Engine.Durable.Tests.Support;

/// <summary>
/// Test-local view over the public in-memory provider registration. The product implementation
/// remains internal; durable-engine tests consume only the provider ports they own.
/// </summary>
internal sealed class DurableTestStore :
    IWorkflowEventStore,
    IWorkflowInboxStore,
    IWorkflowStartIdempotencyStore,
    IWorkflowOutboxStore,
    IWorkflowProjectionStore,
    ITimerScheduler,
    IMessageDispatcher,
    IDisposable
{
    private readonly ServiceProvider services;
    private readonly IWorkflowEventStore eventStore;
    private readonly IWorkflowInboxStore inboxStore;
    private readonly IWorkflowStartIdempotencyStore startStore;
    private readonly IWorkflowOutboxStore outboxStore;
    private readonly IWorkflowProjectionStore projectionStore;
    private readonly ITimerScheduler timerScheduler;
    private readonly IMessageDispatcher dispatcher;

    internal DurableTestStore(TimeProvider? timeProvider = null)
    {
        var registrations = new ServiceCollection();
        if (timeProvider is not null)
        {
            registrations.AddSingleton(timeProvider);
        }

        registrations.AddOrcaCoreInMemoryDurableProvider();
        services = registrations.BuildServiceProvider();
        eventStore = services.GetRequiredService<IWorkflowEventStore>();
        inboxStore = services.GetRequiredService<IWorkflowInboxStore>();
        startStore = services.GetRequiredService<IWorkflowStartIdempotencyStore>();
        outboxStore = services.GetRequiredService<IWorkflowOutboxStore>();
        projectionStore = services.GetRequiredService<IWorkflowProjectionStore>();
        timerScheduler = services.GetRequiredService<ITimerScheduler>();
        dispatcher = services.GetRequiredService<IMessageDispatcher>();
    }

    public Task<Option<CheckpointWrite>> LoadCheckpointAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken) =>
        eventStore.LoadCheckpointAsync(instanceId, cancellationToken);

    public Task<Result<AppendEventsResult>> AppendAsync(
        ProviderCommitBatch batch,
        CancellationToken cancellationToken) =>
        eventStore.AppendAsync(batch, cancellationToken);

    public Task<IReadOnlyList<DurableWorkflowEvent>> LoadTailAsync(
        WorkflowStreamId streamId,
        StreamVersion afterVersion,
        CancellationToken cancellationToken) =>
        eventStore.LoadTailAsync(streamId, afterVersion, cancellationToken);

    public Task<Option<InboxRecord>> GetAsync(
        InstanceId instanceId,
        EventId eventId,
        CancellationToken cancellationToken) =>
        inboxStore.GetAsync(instanceId, eventId, cancellationToken);

    public Task<Option<StartedWorkflowIdempotencyRecord>> GetStartedAsync(
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        startStore.GetStartedAsync(idempotencyKey, cancellationToken);

    public Task<IReadOnlyList<OutboxWrite>> ClaimAsync(
        int maxCount,
        CancellationToken cancellationToken) =>
        outboxStore.ClaimAsync(maxCount, cancellationToken);

    public Task<IReadOnlyList<OutboxWrite>> ClaimAsync(
        OutboxClaimRequest request,
        CancellationToken cancellationToken) =>
        outboxStore.ClaimAsync(request, cancellationToken);

    public Task<Option<OutboxRecordState>> GetStateAsync(
        OutboxRecordId outboxRecordId,
        CancellationToken cancellationToken) =>
        outboxStore.GetStateAsync(outboxRecordId, cancellationToken);

    public Task MarkAsync(
        OutboxRecordId outboxRecordId,
        OutboxRecordState state,
        CancellationToken cancellationToken) =>
        outboxStore.MarkAsync(outboxRecordId, state, cancellationToken);

    public Task ReleaseAsync(
        OutboxRecordId outboxRecordId,
        CancellationToken cancellationToken) =>
        outboxStore.ReleaseAsync(outboxRecordId, cancellationToken);

    public Task ApplyAsync(
        IReadOnlyList<ProjectionWrite> operations,
        CancellationToken cancellationToken) =>
        projectionStore.ApplyAsync(operations, cancellationToken);

    public Task<Option<WorkflowProjectionSnapshot>> GetAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken) =>
        projectionStore.GetAsync(instanceId, cancellationToken);

    public Task<IReadOnlyList<WorkflowProjectionSnapshot>> FindActiveWaitsAsync(
        DefinitionId? definitionId,
        EventName eventName,
        CorrelationId correlationId,
        CancellationToken cancellationToken) =>
        projectionStore.FindActiveWaitsAsync(
            definitionId,
            eventName,
            correlationId,
            cancellationToken);

    public Task<IReadOnlyList<WorkflowProjectionSnapshot>> ListLeaseRecoveryCandidatesAsync(
        CancellationToken cancellationToken) =>
        projectionStore.ListLeaseRecoveryCandidatesAsync(cancellationToken);

    public Task ScheduleAsync(
        TimerScheduleRequest request,
        CancellationToken cancellationToken) =>
        timerScheduler.ScheduleAsync(request, cancellationToken);

    public Task<IReadOnlyList<FireTimerCommand>> ClaimDueAsync(
        DateTimeOffset dueAtOrBefore,
        int maxCount,
        CancellationToken cancellationToken) =>
        timerScheduler.ClaimDueAsync(dueAtOrBefore, maxCount, cancellationToken);

    public Task<IReadOnlyList<FireTimerCommand>> ClaimDueAsync(
        TimerClaimRequest request,
        CancellationToken cancellationToken) =>
        timerScheduler.ClaimDueAsync(request, cancellationToken);

    public Task CompleteAsync(TimerId timerId, CancellationToken cancellationToken) =>
        timerScheduler.CompleteAsync(timerId, cancellationToken);

    public Task ReleaseAsync(TimerId timerId, CancellationToken cancellationToken) =>
        timerScheduler.ReleaseAsync(timerId, cancellationToken);

    public Task<DispatchResult> DispatchAsync(
        OutboxWrite record,
        CancellationToken cancellationToken) =>
        dispatcher.DispatchAsync(record, cancellationToken);

    public void Dispose() => services.Dispose();
}

/// <summary>Test-local view over the public in-memory resource-pool provider port.</summary>
internal sealed class DurableTestResourcePoolStore : IResourcePoolStore, IDisposable
{
    private readonly ServiceProvider services;
    private readonly IResourcePoolStore inner;

    internal DurableTestResourcePoolStore()
    {
        var registrations = new ServiceCollection();
        registrations.AddOrcaCoreInMemoryDurableProvider();
        services = registrations.BuildServiceProvider();
        inner = services.GetRequiredService<IResourcePoolStore>();
    }

    public Task UpsertPoolAsync(
        ResourcePoolDefinition definition,
        CancellationToken cancellationToken) =>
        inner.UpsertPoolAsync(definition, cancellationToken);

    public Task<ResourcePoolAcquireResult> AcquireAsync(
        ResourcePoolAcquireRequest request,
        CancellationToken cancellationToken) =>
        inner.AcquireAsync(request, cancellationToken);

    public Task<ResourcePoolReleaseResult> ReleaseAsync(
        ResourcePoolReleaseRequest request,
        CancellationToken cancellationToken) =>
        inner.ReleaseAsync(request, cancellationToken);

    public Task<Option<ResourcePoolSnapshot>> GetPoolAsync(
        string poolName,
        CancellationToken cancellationToken) =>
        inner.GetPoolAsync(poolName, cancellationToken);

    public Task<IReadOnlyList<ResourcePoolSnapshot>> ListPoolsAsync(
        CancellationToken cancellationToken) =>
        inner.ListPoolsAsync(cancellationToken);

    public Task<ResourcePoolExpiryResult> ExpireTicketsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        inner.ExpireTicketsAsync(now, cancellationToken);

    public void Dispose() => services.Dispose();
}
