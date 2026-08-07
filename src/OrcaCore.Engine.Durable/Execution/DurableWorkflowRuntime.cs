using System.Reflection;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Definitions;
using OrcaCore.Internal;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Driver;

namespace OrcaCore.Engine.Durable.Execution;

/// <summary>
/// Public durable workflow facade: definition registration, version-bound starts, and event
/// delivery. Registered definitions are driven by the durable driver — no caller ever issues
/// kernel commands by hand (DR-032/DR-061).
/// </summary>
internal sealed class DurableWorkflowRuntime
{
    private static readonly MethodInfo RegisterRuntimeDefinitionMethod = typeof(DurableWorkflowRuntime)
        .GetMethods(BindingFlags.Instance | BindingFlags.Public)
        .Single(method =>
            method.Name == nameof(RegisterDefinition) &&
            method.IsGenericMethodDefinition &&
            method.GetParameters() is [{ ParameterType.IsGenericType: true } parameter] &&
            parameter.ParameterType.GetGenericTypeDefinition() == typeof(WorkflowDefinition<>));

    private readonly DurableCommandProcessor commandProcessor;
    private readonly DurableDefinitionRegistry definitions;
    private readonly TimeProvider timeProvider;
    private readonly JsonWorkflowPayloadSerializer payloadSerializer;
    private readonly DurableStartService startService;
    private readonly IWorkflowProjectionStore? projectionStore;
    private readonly DurableDriverCatalog driverCatalog;
    private readonly DurableWorkflowDriver driver;

    /// <summary>
    /// Initializes the durable workflow facade.
    /// </summary>
    public DurableWorkflowRuntime(
        DurableCommandProcessor commandProcessor,
        DurableDefinitionRegistry definitions,
        TimeProvider timeProvider,
        DurableDriverBudget? driverBudget = null,
        IWorkflowProjectionStore? projectionStore = null,
        IDurableDriverObserver? driverObserver = null)
    {
        ArgumentNullException.ThrowIfNull(commandProcessor);
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(timeProvider);

        this.commandProcessor = commandProcessor;
        this.definitions = definitions;
        this.timeProvider = timeProvider;
        payloadSerializer = new JsonWorkflowPayloadSerializer();
        this.projectionStore =
            projectionStore ??
            commandProcessor.EventStore as IWorkflowProjectionStore;
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
    /// Registers one durable workflow definition version. Definition shapes the durable driver
    /// cannot execute fail fast here with a capability diagnostic (DR-010).
    /// </summary>
    public void RegisterDefinition<TState>(WorkflowDefinition<TState> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        driverCatalog.Register(definition);
    }

    internal void RegisterDefinition(object applicationDefinition)
    {
        ArgumentNullException.ThrowIfNull(applicationDefinition);
        var runtimeDefinition =
            global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.RuntimeDefinition(
                applicationDefinition);
        var stateType =
            global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.RuntimeStateType(
                applicationDefinition);
        RegisterRuntimeDefinitionMethod
            .MakeGenericMethod(stateType)
            .Invoke(this, [runtimeDefinition]);
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
        ArgumentNullException.ThrowIfNull(definitionId);
        ArgumentNullException.ThrowIfNull(definitionVersion);
        var definition = definitions.Resolve<TState>(definitionId, definitionVersion);
        var plan = (CompiledWorkflowPlan)WorkflowDefinitionRuntime.GetPlan(definition);
        var result = await StartOrGetCoreAsync(
                idempotencyKey,
                definitionId,
                definitionVersion,
                plan.Fingerprint,
                global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.PayloadFingerprint(input).Value,
                input,
                cancellationToken)
            .ConfigureAwait(false);
        if (result.ConflictingBinding is { } conflict)
        {
            throw new WorkflowVersionException(
                $"Start idempotency key '{idempotencyKey}' is already bound to definition " +
                $"'{conflict.DefinitionId}' version '{conflict.DefinitionVersion}' with different " +
                "definition or fixed-codec input bytes.");
        }

        return new DurableWorkflowStartResult(
            result.InstanceId,
            definitionId,
            definitionVersion,
            result.Created);
    }

    internal async Task<DurableFacadeStartResult> StartOrGetForFacadeAsync<TInput, TState>(
        string idempotencyKey,
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint definitionFingerprint,
        TInput input,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentNullException.ThrowIfNull(definitionId);
        ArgumentNullException.ThrowIfNull(definitionVersion);
        ArgumentNullException.ThrowIfNull(definitionFingerprint);
        definitions.Resolve<TState>(definitionId, definitionVersion);

        var result = await StartOrGetCoreAsync(
                idempotencyKey,
                definitionId,
                definitionVersion,
                definitionFingerprint.Value,
                global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.PayloadFingerprint(input).Value,
                input,
                cancellationToken)
            .ConfigureAwait(false);
        return new DurableFacadeStartResult(
            result.InstanceId,
            result.Created,
            result.ConflictingBinding);
    }

    private async Task<StartOrGetResult> StartOrGetCoreAsync<TInput>(
        string idempotencyKey,
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        string definitionFingerprint,
        string inputFingerprint,
        TInput input,
        CancellationToken cancellationToken)
    {
        var serializedInput = input is null ? null : payloadSerializer.Serialize(input);
        var result = await startService
            .StartOrGetAsync(
                new StartOrGetRequest(
                    idempotencyKey,
                    definitionId,
                    definitionVersion,
                    definitionFingerprint,
                    inputFingerprint,
                    serializedInput,
                    timeProvider.GetUtcNow()),
                cancellationToken)
            .ConfigureAwait(false);
        if (result.ConflictingBinding is not null)
        {
            return result;
        }

        if (projectionStore is not null)
        {
            _ = await new DurableResourceLeaseRecovery(
                    commandProcessor,
                    projectionStore,
                    timeProvider)
                .ReconcileReleaseGapsAsync(result.InstanceId, cancellationToken)
                .ConfigureAwait(false);
        }

        await driver.DriveAsync(result.InstanceId, cancellationToken).ConfigureAwait(false);
        return result;
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

        return await RaiseFacadeEventAsync(
            instanceId,
            eventId ?? EventId.Create(Guid.CreateVersion7().ToString()),
            EventName.Create(eventName),
            correlationId,
            timeProvider.GetUtcNow(),
            payload,
            cancellationToken).ConfigureAwait(false);
    }

    internal async Task<DurableCommandResult> RaiseFacadeEventAsync<TPayload>(
        InstanceId instanceId,
        EventId eventId,
        EventName eventName,
        CorrelationId correlationId,
        DateTimeOffset occurredAt,
        TPayload payload,
        CancellationToken cancellationToken,
        bool driveAfterDelivery = true,
        string? envelopeFingerprint = null)
    {
        var serialized = payload is null ? null : payloadSerializer.Serialize(payload);
        var envelope = new DurableEventEnvelope
        {
            EventId = eventId,
            EventName = eventName.Value,
            EventContractVersion = EventContractVersion.Initial.Value,
            CorrelationId = correlationId,
            Payload = serialized?.Payload,
            PayloadContentType = serialized?.ContentType,
            OccurredAt = occurredAt,
            Route = new DurableEventRouteEnvelope
            {
                Kind = "direct",
                InstanceId = instanceId
            }
        };
        return await RaiseFacadeEventAsync(
            instanceId,
            envelope,
            cancellationToken,
            driveAfterDelivery,
            envelopeFingerprint).ConfigureAwait(false);
    }

    internal async Task<DurableCommandResult> RaiseFacadeEventAsync(
        InstanceId instanceId,
        DurableEventEnvelope envelope,
        CancellationToken cancellationToken,
        bool driveAfterDelivery = true,
        string? envelopeFingerprint = null)
    {
        ArgumentNullException.ThrowIfNull(instanceId);
        ArgumentNullException.ThrowIfNull(envelope);
        var command = new Abstractions.Durable.DeliverEventCommand
        {
            CommandId = CommandId.New(),
            InstanceId = instanceId,
            RequestedAt = timeProvider.GetUtcNow(),
            Envelope = envelope,
            EnvelopeFingerprint = envelopeFingerprint ?? DurableEventEnvelopeFingerprint.Create(envelope)
        };
        var result = await commandProcessor.ProcessAsync(command, cancellationToken).ConfigureAwait(false);
        if (driveAfterDelivery && result.Outcome == DurableCommandOutcome.Committed)
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
    public async Task<DurableCorrelationRouteResult> RaiseEventByCorrelationAsync<TPayload>(
        string eventName,
        CorrelationId correlationId,
        TPayload payload,
        EventId? eventId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        var matches = await RequiredProjectionStore()
            .FindActiveWaitsAsync(
                definitionId: null,
                EventName.Create(eventName),
                correlationId,
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
                "the correlation route requires exactly one active wait.");
        }

        var instanceId = matches[0].InstanceId;
        var result = await RaiseEventAsync(
            instanceId,
            eventName,
            correlationId,
            payload,
            eventId,
            cancellationToken).ConfigureAwait(false);
        return new DurableCorrelationRouteResult(instanceId, result);
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
internal sealed record DurableWorkflowStartResult(
    InstanceId InstanceId,
    DefinitionId DefinitionId,
    DefinitionVersion DefinitionVersion,
    bool Created);

internal sealed record DurableFacadeStartResult(
    InstanceId InstanceId,
    bool Created,
    StartedWorkflowIdempotencyRecord? ConflictingBinding);

/// <summary>
/// Preconditions supplied by an operator when explicitly re-arming a parked durable instance.
/// </summary>
internal sealed record DurableRearmRequest(
    StreamVersion ExpectedStreamVersion,
    bool AcknowledgePoison = false);

/// <summary>
/// Result of routing one durable event to a resolved instance.
/// </summary>
internal sealed record DurableCorrelationRouteResult(
    InstanceId InstanceId,
    DurableCommandResult Result);
