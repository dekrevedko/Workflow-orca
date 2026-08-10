using System.Reflection;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Core.Definitions;
using OrcaCore.Core.Execution;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Diagnostics;
using OrcaCore.Core.Internal;
using OrcaCore.Internal;
using ProjectionSnapshot = OrcaCore.Abstractions.Providers.WorkflowProjectionSnapshot;
using ProjectionActiveWaitSnapshot = OrcaCore.Abstractions.Providers.WorkflowProjectionActiveWaitSnapshot;

namespace OrcaCore.Engine.Durable;

internal sealed class DurableFacadeNotificationHub : IWorkflowRuntimeObserver
{
    internal event Action<InstanceId>? InstanceCommitted;

    public ValueTask OnCommandCompletedAsync(
        WorkflowRuntimeObservation observation,
        CancellationToken cancellationToken)
    {
        if (observation.Outcome == DurableCommandOutcome.Committed)
        {
            InstanceCommitted?.Invoke(observation.InstanceId);
        }

        return ValueTask.CompletedTask;
    }
}

internal sealed partial class DurableWorkflowDefinitionRegistry : IWorkflowDefinitionRegistry
{
    private static readonly MethodInfo StartRuntimeMethod = typeof(DurableWorkflowRuntime)
        .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
        .Single(method =>
            method.Name == "StartOrGetForFacadeAsync" &&
            method.IsGenericMethodDefinition &&
            method.GetParameters().Length == 6 &&
            method.GetParameters()[1].ParameterType == typeof(DefinitionId));

    private readonly object gate = new();
    private readonly SemaphoreSlim startGate = new(1, 1);
    private readonly Dictionary<DefinitionKey, Registration> registrations = [];
    private readonly Dictionary<string, StartBinding> starts = new(StringComparer.Ordinal);
    private readonly DurableWorkflowRuntime runtime;
    private readonly IWorkflowProjectionStore projectionStore;
    private readonly IWorkflowEventStore eventStore;
    private readonly DurableCommandProcessor commandProcessor;
    private readonly DurableFacadeNotificationHub notifications;
    private readonly TimeProvider timeProvider;
    private readonly IReadOnlySet<string> configuredResourcePools;
    private readonly bool hasWorkflowEventDispatcher;

    public DurableWorkflowDefinitionRegistry(
        DurableWorkflowRuntime runtime,
        IWorkflowProjectionStore projectionStore,
        IWorkflowEventStore eventStore,
        DurableCommandProcessor commandProcessor,
        DurableFacadeNotificationHub notifications,
        TimeProvider timeProvider,
        IEnumerable<ResourcePoolName>? configuredResourcePools = null,
        bool hasWorkflowEventDispatcher = false)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        this.projectionStore = projectionStore ?? throw new ArgumentNullException(nameof(projectionStore));
        this.eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
        this.commandProcessor = commandProcessor ?? throw new ArgumentNullException(nameof(commandProcessor));
        this.notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.configuredResourcePools = (configuredResourcePools ?? [])
            .Select(pool => pool.Value)
            .ToHashSet(StringComparer.Ordinal);
        this.hasWorkflowEventDispatcher = hasWorkflowEventDispatcher;
    }

    public WorkflowRegistrationResult<EphemeralDefinitionHandle<TInput>> Register<TInput>(
        EphemeralWorkflowDefinition<TInput> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new WorkflowRegistrationResult<EphemeralDefinitionHandle<TInput>>.HostIncompatible(
            new DefinitionHostCompatibilityFailure.EngineModeMismatch(
                WorkflowMode.Durable,
                WorkflowMode.Ephemeral));
    }

    public WorkflowRegistrationResult<EphemeralDefinitionHandle<TInput, TOutput>> Register<TInput, TOutput>(
        EphemeralWorkflowDefinition<TInput, TOutput> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new WorkflowRegistrationResult<EphemeralDefinitionHandle<TInput, TOutput>>.HostIncompatible(
            new DefinitionHostCompatibilityFailure.EngineModeMismatch(
                WorkflowMode.Durable,
                WorkflowMode.Ephemeral));
    }

    public WorkflowRegistrationResult<DurableDefinitionHandle<TInput>> Register<TInput>(
        DurableWorkflowDefinition<TInput> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return RegisterCore(
            definition,
            global::OrcaCore.Engine.Durable.Internal.DurableContractAdapter.RuntimeDefinition(definition),
            global::OrcaCore.Engine.Durable.Internal.DurableContractAdapter.RuntimeStateType(definition),
            () => global::OrcaCore.Engine.Durable.Internal.DurableContractAdapter.DurableDefinitionHandle<TInput>(
                definition.DefinitionId,
                definition.DefinitionVersion,
                definition.DefinitionFingerprint,
                (input, key, token) => StartAsync(definition, input, key, token),
                (instanceId, token) => GetInstanceAsync(
                    definition.DefinitionId,
                    definition.DefinitionVersion,
                    instanceId,
                    token)));
    }

    public WorkflowRegistrationResult<DurableDefinitionHandle<TInput, TOutput>> Register<TInput, TOutput>(
        DurableWorkflowDefinition<TInput, TOutput> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return RegisterCore(
            definition,
            global::OrcaCore.Engine.Durable.Internal.DurableContractAdapter.RuntimeDefinition(definition),
            global::OrcaCore.Engine.Durable.Internal.DurableContractAdapter.RuntimeStateType(definition),
            () => global::OrcaCore.Engine.Durable.Internal.DurableContractAdapter.DurableDefinitionHandle<TInput, TOutput>(
                definition.DefinitionId,
                definition.DefinitionVersion,
                definition.DefinitionFingerprint,
                (input, key, token) => StartAsync<TInput, TOutput>(definition, input, key, token),
                (instanceId, token) => GetInstanceAsync<TOutput>(
                    definition.DefinitionId,
                    definition.DefinitionVersion,
                    instanceId,
                    token)));
    }

    public EphemeralDefinitionHandle<TInput> GetRequiredHandle<TInput>(
        EphemeralWorkflowRef<TInput> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return GetRequiredHandleCore<EphemeralDefinitionHandle<TInput>>(
            reference.DefinitionId,
            reference.DefinitionVersion,
            reference.DefinitionFingerprint);
    }

    public EphemeralDefinitionHandle<TInput, TOutput> GetRequiredHandle<TInput, TOutput>(
        EphemeralWorkflowRef<TInput, TOutput> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return GetRequiredHandleCore<EphemeralDefinitionHandle<TInput, TOutput>>(
            reference.DefinitionId,
            reference.DefinitionVersion,
            reference.DefinitionFingerprint);
    }

    public DurableDefinitionHandle<TInput> GetRequiredHandle<TInput>(
        DurableWorkflowRef<TInput> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return GetRequiredHandleCore<DurableDefinitionHandle<TInput>>(
            reference.DefinitionId,
            reference.DefinitionVersion,
            reference.DefinitionFingerprint);
    }

    public DurableDefinitionHandle<TInput, TOutput> GetRequiredHandle<TInput, TOutput>(
        DurableWorkflowRef<TInput, TOutput> reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return GetRequiredHandleCore<DurableDefinitionHandle<TInput, TOutput>>(
            reference.DefinitionId,
            reference.DefinitionVersion,
            reference.DefinitionFingerprint);
    }

    private WorkflowRegistrationResult<THandle> RegisterCore<THandle>(
        object publicDefinition,
        object runtimeDefinition,
        Type stateType,
        Func<THandle> createHandle)
    {
        var definitionType = publicDefinition.GetType();
        var definitionId = (DefinitionId)definitionType
            .GetProperty(nameof(DurableWorkflowDefinition<object>.DefinitionId))!
            .GetValue(publicDefinition)!;
        var definitionVersion = (DefinitionVersion)definitionType
            .GetProperty(nameof(DurableWorkflowDefinition<object>.DefinitionVersion))!
            .GetValue(publicDefinition)!;
        var fingerprint = (DefinitionFingerprint)definitionType
            .GetProperty(nameof(DurableWorkflowDefinition<object>.DefinitionFingerprint))!
            .GetValue(publicDefinition)!;
        var key = new DefinitionKey(definitionId, definitionVersion);
        if (ContainsPublish(runtimeDefinition) && !hasWorkflowEventDispatcher)
        {
            return new WorkflowRegistrationResult<THandle>.HostIncompatible(
                new DefinitionHostCompatibilityFailure.MissingWorkflowEventDispatcher());
        }

        var missing = MissingDurablePools(runtimeDefinition);
        if (missing.Count > 0)
        {
            return new WorkflowRegistrationResult<THandle>.HostIncompatible(
                new DefinitionHostCompatibilityFailure.MissingDurableResourcePools(missing));
        }

        lock (gate)
        {
            if (registrations.TryGetValue(key, out var existing))
            {
                if (existing.Fingerprint.Equals(fingerprint))
                {
                    return new WorkflowRegistrationResult<THandle>.Registered((THandle)existing.Handle);
                }

                OrcaCoreDurableDiagnostics.RecordRegistrationConflict();
                return new WorkflowRegistrationResult<THandle>.Conflict(
                    global::OrcaCore.Engine.Durable.Internal.DurableContractAdapter.DefinitionRegistrationConflict(
                        definitionId,
                        definitionVersion,
                        existing.Fingerprint,
                        fingerprint));
            }

            runtime.RegisterDefinition(publicDefinition);
            var handle = createHandle();
            registrations.Add(key, new Registration(handle!, fingerprint, stateType));
            return new WorkflowRegistrationResult<THandle>.Registered(handle);
        }
    }

    private THandle GetRequiredHandleCore<THandle>(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint definitionFingerprint)
    {
        lock (gate)
        {
            if (registrations.TryGetValue(
                    new DefinitionKey(definitionId, definitionVersion),
                    out var registration) &&
                registration.Fingerprint.Equals(definitionFingerprint) &&
                registration.Handle is THandle handle)
            {
                return handle;
            }
        }

        throw global::OrcaCore.Engine.Durable.Internal.DurableContractAdapter.DefinitionNotRegistered(
            definitionId,
            definitionVersion,
            definitionFingerprint);
    }

    private async ValueTask<WorkflowStartResult<WorkflowInstanceHandle>> StartAsync<TInput>(
        DurableWorkflowDefinition<TInput> definition,
        TInput input,
        StartIdempotencyKey idempotencyKey,
        CancellationToken cancellationToken)
    {
        var started = await StartCoreAsync(
            definition.DefinitionId,
            definition.DefinitionVersion,
            definition.DefinitionFingerprint,
            global::OrcaCore.Engine.Durable.Internal.DurableContractAdapter.RuntimeStateType(definition),
            input,
            idempotencyKey,
            cancellationToken).ConfigureAwait(false);
        return started.Conflict is not null
            ? new WorkflowStartResult<WorkflowInstanceHandle>.Conflict(started.Conflict)
            : new WorkflowStartResult<WorkflowInstanceHandle>.Accepted(
                CreateInstanceHandle(started.Binding!),
                started.WasExisting);
    }

    private async ValueTask<WorkflowStartResult<WorkflowInstanceHandle<TOutput>>> StartAsync<TInput, TOutput>(
        DurableWorkflowDefinition<TInput, TOutput> definition,
        TInput input,
        StartIdempotencyKey idempotencyKey,
        CancellationToken cancellationToken)
    {
        var started = await StartCoreAsync(
            definition.DefinitionId,
            definition.DefinitionVersion,
            definition.DefinitionFingerprint,
            global::OrcaCore.Engine.Durable.Internal.DurableContractAdapter.RuntimeStateType(definition),
            input,
            idempotencyKey,
            cancellationToken).ConfigureAwait(false);
        return started.Conflict is not null
            ? new WorkflowStartResult<WorkflowInstanceHandle<TOutput>>.Conflict(started.Conflict)
            : new WorkflowStartResult<WorkflowInstanceHandle<TOutput>>.Accepted(
                CreateInstanceHandle<TOutput>(started.Binding!),
                started.WasExisting);
    }

    private async ValueTask<StartCoreResult> StartCoreAsync<TInput>(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint definitionFingerprint,
        Type stateType,
        TInput input,
        StartIdempotencyKey idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(idempotencyKey);
        var inputFingerprint = global::OrcaCore.Engine.Durable.Internal.DurableContractAdapter.PayloadFingerprint(input);

        await startGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            lock (gate)
            {
                if (starts.TryGetValue(idempotencyKey.Value, out var existing))
                {
                    if (existing.DefinitionId.Equals(definitionId) &&
                        existing.DefinitionVersion.Equals(definitionVersion) &&
                        existing.DefinitionFingerprint.Equals(definitionFingerprint) &&
                        existing.InputFingerprint.Equals(inputFingerprint))
                    {
                        return new StartCoreResult(
                            new InstanceBinding(
                                existing.InstanceId,
                                existing.DefinitionId,
                                existing.DefinitionVersion,
                                existing.DefinitionFingerprint,
                                stateType),
                            true,
                            null);
                    }

                    return new StartCoreResult(
                        null,
                        false,
                        global::OrcaCore.Engine.Durable.Internal.DurableContractAdapter.StartIdempotencyConflict(
                            idempotencyKey,
                            existing.DefinitionId,
                            existing.DefinitionVersion,
                            existing.DefinitionFingerprint,
                            existing.InputFingerprint,
                            definitionId,
                            definitionVersion,
                            definitionFingerprint,
                            inputFingerprint));
                }
            }

            var task = (Task)StartRuntimeMethod
                .MakeGenericMethod(typeof(TInput), stateType)
                .Invoke(
                    runtime,
                    [
                        idempotencyKey.Value,
                        definitionId,
                        definitionVersion,
                        definitionFingerprint,
                        input,
                        cancellationToken
                    ])!;
            await task.ConfigureAwait(false);
            var result = (DurableFacadeStartResult)task.GetType().GetProperty("Result")!.GetValue(task)!;
            if (result.ConflictingBinding is { } conflict)
            {
                return new StartCoreResult(
                    null,
                    false,
                    global::OrcaCore.Engine.Durable.Internal.DurableContractAdapter.StartIdempotencyConflict(
                        idempotencyKey,
                        conflict.DefinitionId,
                        conflict.DefinitionVersion,
                        conflict.DefinitionFingerprint,
                        conflict.InputFingerprint,
                        definitionId,
                        definitionVersion,
                        definitionFingerprint,
                        inputFingerprint));
            }

            if (result.ConflictingPendingIntent is { } pendingConflict)
            {
                return new StartCoreResult(
                    null,
                    false,
                    global::OrcaCore.Engine.Durable.Internal.DurableContractAdapter
                        .PendingStartIdempotencyConflict(
                            idempotencyKey,
                            pendingConflict,
                            definitionId,
                            definitionVersion,
                            definitionFingerprint,
                            inputFingerprint));
            }

            var binding = new InstanceBinding(
                result.InstanceId ?? throw new InvalidOperationException(
                    "A successful durable start did not return an instance identity."),
                definitionId,
                definitionVersion,
                definitionFingerprint,
                stateType);
            lock (gate)
            {
                starts[idempotencyKey.Value] = new StartBinding(
                    result.InstanceId,
                    definitionId,
                    definitionVersion,
                    definitionFingerprint,
                    inputFingerprint);
            }

            return new StartCoreResult(binding, !result.Created, null);
        }
        finally
        {
            startGate.Release();
        }
    }

    private async ValueTask<WorkflowInstanceHandle> GetInstanceAsync(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        var binding = await RequireInstanceAsync(
            definitionId,
            definitionVersion,
            instanceId,
            cancellationToken).ConfigureAwait(false);
        return CreateInstanceHandle(binding);
    }

    private async ValueTask<WorkflowInstanceHandle<TOutput>> GetInstanceAsync<TOutput>(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        var binding = await RequireInstanceAsync(
            definitionId,
            definitionVersion,
            instanceId,
            cancellationToken).ConfigureAwait(false);
        return CreateInstanceHandle<TOutput>(binding);
    }

    private async ValueTask<InstanceBinding> RequireInstanceAsync(
        DefinitionId expectedId,
        DefinitionVersion expectedVersion,
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        var projected = await projectionStore.GetAsync(instanceId, cancellationToken).ConfigureAwait(false);
        var snapshot = projected.HasValue
            ? projected.Value
            :
            throw global::OrcaCore.Engine.Durable.Internal.DurableContractAdapter.InstanceNotFound(instanceId);
        if (!snapshot.DefinitionId.Equals(expectedId) ||
            !snapshot.DefinitionVersion.Equals(expectedVersion))
        {
            throw global::OrcaCore.Engine.Durable.Internal.DurableContractAdapter.InstanceDefinitionMismatch(
                instanceId,
                expectedId,
                snapshot.DefinitionId);
        }

        Registration registration;
        lock (gate)
        {
            registration = registrations[new DefinitionKey(expectedId, expectedVersion)];
        }

        return new InstanceBinding(
            instanceId,
            expectedId,
            expectedVersion,
            registration.Fingerprint,
            registration.StateType);
    }

    private WorkflowInstanceHandle CreateInstanceHandle(InstanceBinding binding) =>
        global::OrcaCore.Engine.Durable.Internal.DurableContractAdapter.InstanceHandle(
            binding.InstanceId,
            token => GetSnapshotAsync(binding, token),
            (requestedType, token) => GetStateAsync(binding, requestedType, token),
            token => RequestCancellationAsync(binding, token),
            token => TerminateAsync(binding, token));

    private WorkflowInstanceHandle<TOutput> CreateInstanceHandle<TOutput>(InstanceBinding binding) =>
        global::OrcaCore.Engine.Durable.Internal.DurableContractAdapter.InstanceHandle(
            binding.InstanceId,
            token => GetSnapshotAsync(binding, token),
            (requestedType, token) => GetStateAsync(binding, requestedType, token),
            token => RequestCancellationAsync(binding, token),
            token => TerminateAsync(binding, token),
            token => GetOutputAsync<TOutput>(binding, token),
            token => WaitForOutputAsync<TOutput>(binding, token));

    private async ValueTask<global::OrcaCore.WorkflowInstanceSnapshot> GetSnapshotAsync(
        InstanceBinding binding,
        CancellationToken cancellationToken)
    {
        var snapshot = await GetProjectionAsync(binding.InstanceId, cancellationToken).ConfigureAwait(false);
        var terminal = IsTerminal(snapshot.Status);
        CheckpointWrite? checkpoint = null;
        DurableExecutionEnvelopeV2? envelope = null;
        if (snapshot.ActiveWaits.Count > 0)
        {
            checkpoint = await GetCheckpointAsync(binding.InstanceId, cancellationToken).ConfigureAwait(false);
            envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint.Payload);
        }

        return new global::OrcaCore.WorkflowInstanceSnapshot(
            snapshot.InstanceId,
            WorkflowMode.Durable,
            snapshot.DefinitionId,
            snapshot.DefinitionVersion,
            binding.DefinitionFingerprint,
            snapshot.Status,
            snapshot.CreatedAt,
            terminal ? snapshot.UpdatedAt : null,
            string.IsNullOrWhiteSpace(snapshot.EndOutcomeName)
                ? null
                : WorkflowOutcomeName.Create(snapshot.EndOutcomeName),
            MapFailure(snapshot),
            snapshot.ActiveWaits
                .Select(wait => MapWait(wait, checkpoint!, envelope!))
                .OfType<global::OrcaCore.ActiveWaitSnapshot>()
                .ToArray());
    }

    private async ValueTask<object?> GetStateAsync(
        InstanceBinding binding,
        Type requestedType,
        CancellationToken cancellationToken)
    {
        if (requestedType != binding.StateType)
        {
            throw global::OrcaCore.Engine.Durable.Internal.DurableContractAdapter.StateTypeMismatch(
                binding.InstanceId,
                binding.StateType,
                requestedType);
        }

        var envelope = await GetEnvelopeAsync(binding.InstanceId, cancellationToken).ConfigureAwait(false);
        return CoreWorkflowValueCodec.Deserialize(envelope.StatePayload, requestedType);
    }

    private async ValueTask<WorkflowCancellationRequestStatus> RequestCancellationAsync(
        InstanceBinding binding,
        CancellationToken cancellationToken)
    {
        var result = await commandProcessor.ProcessAsync(
            new CancelWorkflowCommand
            {
                CommandId = CommandId.New(),
                InstanceId = binding.InstanceId,
                RequestedAt = timeProvider.GetUtcNow()
            },
            cancellationToken).ConfigureAwait(false);
        if (result.Outcome == DurableCommandOutcome.Conflict)
        {
            throw new WorkflowConcurrencyException(
                result.Message ?? "Cancellation request conflicted with another durable mutation.");
        }

        var status = result.LifecycleDisposition switch
        {
            DurableLifecycleDisposition.CancellationRequested =>
                WorkflowCancellationRequestStatus.Requested,
            DurableLifecycleDisposition.CancellationAlreadyRequested =>
                WorkflowCancellationRequestStatus.AlreadyRequested,
            DurableLifecycleDisposition.AlreadyTerminal =>
                WorkflowCancellationRequestStatus.AlreadyTerminal,
            _ => throw new WorkflowLifecycleException(
                "The durable cancellation command did not produce a lifecycle disposition.")
        };

        if (status != WorkflowCancellationRequestStatus.AlreadyTerminal)
        {
            _ = await runtime.DriveAsync(binding.InstanceId, cancellationToken).ConfigureAwait(false);
        }

        return status;
    }

    private async ValueTask<WorkflowTerminationStatus> TerminateAsync(
        InstanceBinding binding,
        CancellationToken cancellationToken)
    {
        var result = await commandProcessor.ProcessAsync(
            new TerminateWorkflowCommand
            {
                CommandId = CommandId.New(),
                InstanceId = binding.InstanceId,
                RequestedAt = timeProvider.GetUtcNow()
            },
            cancellationToken).ConfigureAwait(false);
        if (result.Outcome == DurableCommandOutcome.Conflict)
        {
            throw new WorkflowConcurrencyException(
                result.Message ?? "Termination conflicted with another durable mutation.");
        }

        return result.LifecycleDisposition switch
        {
            DurableLifecycleDisposition.Terminated => WorkflowTerminationStatus.Terminated,
            DurableLifecycleDisposition.AlreadyTerminal => WorkflowTerminationStatus.AlreadyTerminal,
            _ => throw new WorkflowLifecycleException(
                "The durable termination command did not produce a lifecycle disposition.")
        };
    }

    private async ValueTask<WorkflowOutputResult<TOutput>> GetOutputAsync<TOutput>(
        InstanceBinding binding,
        CancellationToken cancellationToken)
    {
        var snapshot = await GetProjectionAsync(binding.InstanceId, cancellationToken).ConfigureAwait(false);
        if (!IsTerminal(snapshot.Status))
        {
            return new WorkflowOutputResult<TOutput>.Pending();
        }

        var envelope = await GetEnvelopeAsync(binding.InstanceId, cancellationToken).ConfigureAwait(false);
        if (snapshot.Status == WorkflowInstanceStatus.Completed && envelope.Output is { } output)
        {
            var detached = (TOutput)CoreWorkflowValueCodec.Deserialize(output.Payload, typeof(TOutput))!;
            return new WorkflowOutputResult<TOutput>.Available(detached);
        }

        return new WorkflowOutputResult<TOutput>.Unavailable(
            snapshot.Status,
            MapFailure(snapshot));
    }

    private async ValueTask<TOutput> WaitForOutputAsync<TOutput>(
        InstanceBinding binding,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnCommitted(InstanceId instanceId)
            {
                if (instanceId.Equals(binding.InstanceId))
                {
                    signal.TrySetResult();
                }
            }

            notifications.InstanceCommitted += OnCommitted;
            try
            {
                var output = await GetOutputAsync<TOutput>(binding, cancellationToken).ConfigureAwait(false);
                switch (output)
                {
                    case WorkflowOutputResult<TOutput>.Available available:
                        return available.Output;
                    case WorkflowOutputResult<TOutput>.Unavailable unavailable:
                        throw global::OrcaCore.Engine.Durable.Internal.DurableContractAdapter.OutputUnavailable(unavailable.Status, unavailable.Failure);
                }

                await signal.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                notifications.InstanceCommitted -= OnCommitted;
            }
        }
    }

    private async ValueTask<ProjectionSnapshot> GetProjectionAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        var projected = await projectionStore.GetAsync(instanceId, cancellationToken).ConfigureAwait(false);
        return projected.HasValue
            ? projected.Value
            :
            throw global::OrcaCore.Engine.Durable.Internal.DurableContractAdapter.InstanceNotFound(instanceId);
    }

    private async ValueTask<DurableExecutionEnvelopeV2> GetEnvelopeAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        var checkpoint = await GetCheckpointAsync(instanceId, cancellationToken).ConfigureAwait(false);
        return DurableExecutionEnvelopeV2.Deserialize(checkpoint.Payload);
    }

    private async ValueTask<CheckpointWrite> GetCheckpointAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        var checkpoint = await eventStore.LoadCheckpointAsync(instanceId, cancellationToken).ConfigureAwait(false);
        if (!checkpoint.HasValue ||
            !string.Equals(
                checkpoint.Value.ContentType,
                DurableExecutionEnvelopeV2.ContentType,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Workflow instance '{instanceId}' has no readable committed state checkpoint.");
        }

        return checkpoint.Value;
    }

    private static IReadOnlyList<ResourcePoolName> RequiredDurablePools(object runtimeDefinition)
    {
        var metadata = runtimeDefinition as WorkflowDefinitionRuntimeMetadata ??
            throw new InvalidOperationException(
                $"Runtime definition '{runtimeDefinition.GetType().FullName}' does not expose runtime metadata.");
        return metadata.RequiredDurablePools;
    }

    private static bool ContainsPublish(object runtimeDefinition)
    {
        var metadata = runtimeDefinition as WorkflowDefinitionRuntimeMetadata ??
            throw new InvalidOperationException(
                $"Runtime definition '{runtimeDefinition.GetType().FullName}' does not expose runtime metadata.");
        return metadata.ContainsPublish;
    }

    private static global::OrcaCore.ActiveWaitSnapshot? MapWait(
        ProjectionActiveWaitSnapshot wait,
        CheckpointWrite checkpoint,
        DurableExecutionEnvelopeV2 envelope)
    {
        var obligation = envelope.OwnedObligations.SingleOrDefault(candidate =>
            candidate.Kind == DurableOwnedObligationKind.Wait &&
            string.Equals(candidate.ObligationId, wait.WaitId.ToString(), StringComparison.Ordinal));
        if (string.IsNullOrWhiteSpace(obligation?.AuthoredPath))
        {
            // Provider projections also contain internal resource-admission waits. Only
            // waits authored through the application contract belong in the public view.
            return null;
        }

        var checkpointWait = checkpoint.RuntimeState.ActiveWaits.SingleOrDefault(candidate =>
            candidate.WaitId.Equals(wait.WaitId));
        var deadline = checkpointWait?.TimeoutTimerId is { } timeoutTimerId
            ? checkpoint.RuntimeState.ActiveTimers
                .SingleOrDefault(timer => timer.TimerId.Equals(timeoutTimerId))
                ?.FireAt
            : null;

        return new global::OrcaCore.ActiveWaitSnapshot(
            wait.WaitId,
            FailureProvenance.LocationFromCompilerPath(obligation.AuthoredPath),
            WorkflowEventContract.Create(
                EventName.Create(wait.EventName),
                new EventContractVersion(checkpointWait?.EventContractVersion ?? 1)),
            wait.CorrelationId,
            wait.RegisteredAt,
            deadline);
    }

    private static WorkflowFailure? MapFailure(ProjectionSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot.ErrorSummary))
        {
            return null;
        }

        var separator = snapshot.ErrorSummary.IndexOf(':', StringComparison.Ordinal);
        var code = separator > 0 ? snapshot.ErrorSummary[..separator] : "WF-RUNTIME-FAILED";
        var message = separator > 0
            ? snapshot.ErrorSummary[(separator + 1)..].Trim()
            : snapshot.ErrorSummary;
        return global::OrcaCore.Engine.Durable.Internal.DurableContractAdapter.WorkflowFailure(
            code,
            message,
            global::OrcaCore.Engine.Durable.Internal.DurableContractAdapter.AuthoredLocation("workflow:$"),
            global::OrcaCore.Engine.Durable.Internal.DurableContractAdapter.RootFailureOccurrence(),
            []);
    }

    private static bool IsTerminal(WorkflowInstanceStatus status) =>
        status is WorkflowInstanceStatus.Completed or
            WorkflowInstanceStatus.Failed or
            WorkflowInstanceStatus.TimedOut or
            WorkflowInstanceStatus.Cancelled or
            WorkflowInstanceStatus.Terminated;

    private sealed record DefinitionKey(DefinitionId Id, DefinitionVersion Version);
    private sealed record Registration(
        object Handle,
        DefinitionFingerprint Fingerprint,
        Type StateType);
    private sealed record StartCoreResult(
        InstanceBinding? Binding,
        bool WasExisting,
        StartIdempotencyConflict? Conflict);
    private sealed record InstanceBinding(
        InstanceId InstanceId,
        DefinitionId DefinitionId,
        DefinitionVersion DefinitionVersion,
        DefinitionFingerprint DefinitionFingerprint,
        Type StateType);
    private sealed record StartBinding(
        InstanceId InstanceId,
        DefinitionId DefinitionId,
        DefinitionVersion DefinitionVersion,
        DefinitionFingerprint DefinitionFingerprint,
        PayloadFingerprint InputFingerprint);
}
