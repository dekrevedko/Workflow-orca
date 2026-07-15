using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Driver;
using OrcaCore.Engine.Durable.Management;

namespace OrcaCore.Engine.Durable.Execution;

/// <summary>
/// Public durable workflow facade: definition registration, version-bound starts, and event
/// delivery. Registered definitions are driven by the durable driver — no caller ever issues
/// kernel commands by hand (DR-032/DR-061).
/// </summary>
public sealed class DurableWorkflowRuntime
{
    private readonly DurableCommandProcessor commandProcessor;
    private readonly DurableDefinitionRegistry definitions;
    private readonly TimeProvider timeProvider;
    private readonly IWorkflowPayloadSerializer payloadSerializer;
    private readonly DurableStartService startService;
    private readonly IWorkflowProjectionStore? projectionStore;
    private readonly DurableManagement? management;
    private readonly DurableDriverCatalog driverCatalog;
    private readonly DurableWorkflowDriver driver;

    /// <summary>
    /// Initializes the durable workflow facade.
    /// </summary>
    public DurableWorkflowRuntime(
        DurableCommandProcessor commandProcessor,
        DurableDefinitionRegistry definitions,
        TimeProvider timeProvider,
        IWorkflowPayloadSerializer payloadSerializer,
        DurableDriverBudget? driverBudget = null,
        IWorkflowProjectionStore? projectionStore = null,
        DurableManagement? management = null,
        IDurableDriverObserver? driverObserver = null)
    {
        ArgumentNullException.ThrowIfNull(commandProcessor);
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(payloadSerializer);

        this.commandProcessor = commandProcessor;
        this.definitions = definitions;
        this.timeProvider = timeProvider;
        this.payloadSerializer = payloadSerializer;
        this.projectionStore = projectionStore;
        this.management = management;
        driverCatalog = new DurableDriverCatalog(definitions);
        startService = new DurableStartService(commandProcessor);
        driver = new DurableWorkflowDriver(
            commandProcessor,
            driverCatalog,
            payloadSerializer,
            timeProvider,
            driverBudget,
            driverObserver);
    }

    /// <summary>
    /// Gets the durable management surface configured for this runtime.
    /// </summary>
    public DurableManagement Management => management ?? throw new InvalidOperationException(
        "Durable management is unavailable because no management surface was configured.");

    /// <summary>
    /// Registers one durable workflow definition version. Definition shapes the durable driver
    /// cannot execute fail fast here with a capability diagnostic (DR-010).
    /// </summary>
    public void RegisterDefinition<TState>(WorkflowDefinition<TState> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        driverCatalog.Register(definition);
    }

    /// <summary>
    /// Starts a registered workflow definition or returns the instance previously started with
    /// the same key, then drives the instance to its next suspension point.
    /// </summary>
    public async Task<DurableWorkflowStartResult> StartOrGetAsync<TInput, TState>(
        string idempotencyKey,
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        TInput input,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        definitions.Resolve<TState>(definitionId, definitionVersion);

        var serializedInput = input is null ? null : payloadSerializer.Serialize(input);
        var result = await startService
            .StartOrGetAsync(
                new StartOrGetRequest(
                    idempotencyKey,
                    definitionId,
                    definitionVersion,
                    serializedInput,
                    timeProvider.GetUtcNow()),
                cancellationToken)
            .ConfigureAwait(false);
        await driver.DriveAsync(result.InstanceId, cancellationToken).ConfigureAwait(false);
        return new DurableWorkflowStartResult(
            result.InstanceId,
            definitionId,
            definitionVersion,
            result.Created);
    }

    /// <summary>
    /// Starts the supplied definition version or returns the instance previously started with the same key.
    /// </summary>
    public async Task<DurableWorkflowStartResult> StartOrGetAsync<TInput, TState>(
        string idempotencyKey,
        WorkflowDefinition<TState> definition,
        TInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        RegisterDefinition(definition);

        return await StartOrGetAsync<TInput, TState>(
            idempotencyKey,
            definition.DefinitionId,
            definition.DefinitionVersion,
            input,
            cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Delivers an instance-targeted event with durable inbox deduplication, then drives the
    /// instance if the delivery unblocked it (DR-031/DR-032). The payload is serialized at
    /// this boundary so the matched fact survives restarts.
    /// </summary>
    public async Task<DurableCommandResult> RaiseEventAsync<TPayload>(
        InstanceId instanceId,
        string eventName,
        CorrelationId correlationId,
        TPayload payload,
        EventId? eventId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);

        var serialized = payload is null ? null : payloadSerializer.Serialize(payload);
        var command = new Abstractions.Durable.DeliverEventCommand
        {
            CommandId = CommandId.New(),
            InstanceId = instanceId,
            RequestedAt = timeProvider.GetUtcNow(),
            Envelope = new EventEnvelope
            {
                EventId = eventId ?? EventId.New(),
                EventName = eventName,
                CorrelationId = correlationId,
                Payload = serialized?.Payload,
                PayloadContentType = serialized?.ContentType,
                OccurredAt = timeProvider.GetUtcNow()
            }
        };
        var result = await commandProcessor.ProcessAsync(command, cancellationToken).ConfigureAwait(false);
        if (result.Outcome == DurableCommandOutcome.Committed)
        {
            await driver.DriveAsync(instanceId, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>
    /// Delivers an instance-targeted event without a payload.
    /// </summary>
    public Task<DurableCommandResult> RaiseEventAsync(
        InstanceId instanceId,
        string eventName,
        CorrelationId correlationId,
        EventId? eventId = null,
        CancellationToken cancellationToken = default)
    {
        return RaiseEventAsync<object?>(instanceId, eventName, correlationId, null, eventId, cancellationToken);
    }

    /// <summary>
    /// Resolves exactly one active wait by event name and correlation, then delivers through the
    /// normal instance-targeted durable inbox path (EV-010/012).
    /// </summary>
    public async Task<DurableEventDeliveryResult> RaiseEventByCorrelationAsync<TPayload>(
        string eventName,
        CorrelationId correlationId,
        TPayload payload,
        EventId? eventId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        var matches = await RequiredProjectionStore()
            .ListAsync(
                new WorkflowProjectionQuery
                {
                    ActiveWaitEventName = eventName,
                    ActiveWaitCorrelationId = correlationId
                },
                cancellationToken)
            .ConfigureAwait(false);
        if (matches.Count == 0)
        {
            throw new WorkflowRoutingException(
                $"No active wait matches event '{eventName}' and correlation '{correlationId}'.");
        }

        if (matches.Count > 1)
        {
            throw new WorkflowRoutingException(
                $"Event '{eventName}' and correlation '{correlationId}' match {matches.Count} instances; " +
                "use instance-targeted or definition-targeted delivery.");
        }

        var instanceId = matches[0].InstanceId;
        var result = await RaiseEventAsync(
            instanceId,
            eventName,
            correlationId,
            payload,
            eventId,
            cancellationToken).ConfigureAwait(false);
        return new DurableEventDeliveryResult(instanceId, result);
    }

    /// <summary>
    /// Delivers one logical event to every projected instance of a definition. Each target gets
    /// its own event identity so durable inbox deduplication remains instance-local (EV-010).
    /// </summary>
    public async Task<IReadOnlyList<DurableEventDeliveryResult>> RaiseEventToDefinitionAsync<TPayload>(
        DefinitionId definitionId,
        string eventName,
        CorrelationId correlationId,
        TPayload payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        var instances = await RequiredProjectionStore()
            .ListAsync(
                new WorkflowProjectionQuery
                {
                    DefinitionId = definitionId,
                    ActiveWaitEventName = eventName,
                    ActiveWaitCorrelationId = correlationId
                },
                cancellationToken)
            .ConfigureAwait(false);
        var results = new List<DurableEventDeliveryResult>(instances.Count);
        foreach (var instance in instances)
        {
            var result = await RaiseEventAsync(
                instance.InstanceId,
                eventName,
                correlationId,
                payload,
                EventId.New(),
                cancellationToken).ConfigureAwait(false);
            results.Add(new DurableEventDeliveryResult(instance.InstanceId, result));
        }

        return results;
    }

    /// <summary>
    /// Explicitly re-arms a parked workflow after its reason-specific precondition is satisfied
    /// (DR-017). Definition registration or checkpoint migration alone never mutates the instance.
    /// </summary>
    public Task<DurableCommandResult> RearmAsync(
        InstanceId instanceId,
        DurableRearmRequest request,
        CancellationToken cancellationToken = default)
    {
        return driver.RearmAsync(instanceId, request, cancellationToken);
    }

    /// <summary>
    /// Drives one instance's pending advancement (start commit, matched wait, fired timer,
    /// committed yield) to its next suspension point. Exposed for driver hosts and tests;
    /// duplicate or stale drive requests are harmless (DR-034 idempotence).
    /// </summary>
    internal Task<DurableSegmentResult> DriveAsync(InstanceId instanceId, CancellationToken cancellationToken)
    {
        return driver.DriveAsync(instanceId, cancellationToken);
    }

    /// <summary>
    /// Drives one instance in an explicit mode. Claimed continuations use
    /// <see cref="DurableDriveMode.Required"/>, which parks driver-owned instances whose bound
    /// definition version is not registered on this host (DR-016).
    /// </summary>
    internal Task<DurableSegmentResult> DriveAsync(
        InstanceId instanceId,
        DurableDriveMode mode,
        CancellationToken cancellationToken)
    {
        return driver.DriveAsync(instanceId, mode, cancellationToken);
    }

    private IWorkflowProjectionStore RequiredProjectionStore()
    {
        return projectionStore ?? throw new InvalidOperationException(
            "Correlation and definition routing require a durable projection store.");
    }
}

/// <summary>
/// Result of a durable start-or-get request.
/// </summary>
public sealed record DurableWorkflowStartResult(
    InstanceId InstanceId,
    DefinitionId DefinitionId,
    DefinitionVersion DefinitionVersion,
    bool Created);

/// <summary>
/// Preconditions supplied by an operator when explicitly re-arming a parked durable instance.
/// </summary>
public sealed record DurableRearmRequest(
    StreamVersion ExpectedStreamVersion,
    bool AcknowledgePoison = false);

/// <summary>
/// Result of routing one durable event to a resolved instance.
/// </summary>
public sealed record DurableEventDeliveryResult(
    InstanceId InstanceId,
    DurableCommandResult Result);
