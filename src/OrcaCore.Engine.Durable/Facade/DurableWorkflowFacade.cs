using System.Reflection;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Management;
using OrcaCore.Core.Internal;
using OrcaCore.Internal;
using LegacySnapshot = OrcaCore.Abstractions.Instances.WorkflowInstanceSnapshot;
using LegacyStatus = OrcaCore.Abstractions.Instances.WorkflowStatus;
using LegacyActiveWaitSnapshot = OrcaCore.Abstractions.Instances.ActiveWaitSnapshot;

namespace OrcaCore.Engine.Durable;

public sealed class DurableFacadeNotificationHub : IWorkflowRuntimeObserver
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

public sealed class DurableWorkflowDefinitionRegistry : IWorkflowDefinitionRegistry
{
    private static readonly MethodInfo RegisterRuntimeMethod = typeof(DurableWorkflowRuntime)
        .GetMethods(BindingFlags.Instance | BindingFlags.Public)
        .Single(method => method.Name == nameof(DurableWorkflowRuntime.RegisterDefinition));
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
    private readonly DurableManagement management;
    private readonly DurableFacadeNotificationHub notifications;
    private readonly TimeProvider timeProvider;
    private readonly IReadOnlySet<string> configuredResourcePools;

    public DurableWorkflowDefinitionRegistry(
        DurableWorkflowRuntime runtime,
        IWorkflowProjectionStore projectionStore,
        IWorkflowEventStore eventStore,
        DurableManagement management,
        DurableFacadeNotificationHub notifications,
        TimeProvider timeProvider,
        IEnumerable<ResourcePoolName>? configuredResourcePools = null)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        this.projectionStore = projectionStore ?? throw new ArgumentNullException(nameof(projectionStore));
        this.eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
        this.management = management ?? throw new ArgumentNullException(nameof(management));
        this.notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.configuredResourcePools = (configuredResourcePools ?? [])
            .Select(pool => pool.Value)
            .ToHashSet(StringComparer.Ordinal);
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
            WorkflowRuntimeBridge.RuntimeDefinition(definition),
            WorkflowRuntimeBridge.RuntimeStateType(definition),
            () => WorkflowRuntimeBridge.DurableDefinitionHandle<TInput>(
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
            WorkflowRuntimeBridge.RuntimeDefinition(definition),
            WorkflowRuntimeBridge.RuntimeStateType(definition),
            () => WorkflowRuntimeBridge.DurableDefinitionHandle<TInput, TOutput>(
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

        lock (gate)
        {
            if (registrations.TryGetValue(key, out var existing))
            {
                return existing.Fingerprint.Equals(fingerprint)
                    ? new WorkflowRegistrationResult<THandle>.Registered((THandle)existing.Handle)
                    : new WorkflowRegistrationResult<THandle>.Conflict(
                        WorkflowRuntimeBridge.DefinitionRegistrationConflict(
                            definitionId,
                            definitionVersion,
                            existing.Fingerprint,
                            fingerprint));
            }

            var missing = RequiredDurablePools(runtimeDefinition)
                .Where(pool => !configuredResourcePools.Contains(pool.Value))
                .Distinct()
                .OrderBy(pool => pool.Value, StringComparer.Ordinal)
                .ToArray();
            if (missing.Length > 0)
            {
                return new WorkflowRegistrationResult<THandle>.HostIncompatible(
                    new DefinitionHostCompatibilityFailure.MissingDurableResourcePools(missing));
            }

            RegisterRuntimeMethod.MakeGenericMethod(stateType).Invoke(runtime, [runtimeDefinition]);
            var handle = createHandle();
            registrations.Add(key, new Registration(handle!, fingerprint, stateType));
            return new WorkflowRegistrationResult<THandle>.Registered(handle);
        }
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
            WorkflowRuntimeBridge.RuntimeStateType(definition),
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
            WorkflowRuntimeBridge.RuntimeStateType(definition),
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
        var inputFingerprint = WorkflowRuntimeBridge.PayloadFingerprint(input);

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
                        WorkflowRuntimeBridge.StartIdempotencyConflict(
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
                    WorkflowRuntimeBridge.StartIdempotencyConflict(
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

            var binding = new InstanceBinding(
                result.InstanceId,
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
        var snapshots = await projectionStore.ListAsync(
            new WorkflowProjectionQuery { InstanceId = instanceId },
            cancellationToken).ConfigureAwait(false);
        var snapshot = snapshots.SingleOrDefault() ??
            throw WorkflowRuntimeBridge.InstanceNotFound(instanceId);
        if (!snapshot.DefinitionId.Equals(expectedId) ||
            !snapshot.DefinitionVersion.Equals(expectedVersion))
        {
            throw WorkflowRuntimeBridge.InstanceDefinitionMismatch(
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
        WorkflowRuntimeBridge.InstanceHandle(
            binding.InstanceId,
            token => GetSnapshotAsync(binding, token),
            (requestedType, token) => GetStateAsync(binding, requestedType, token),
            token => RequestCancellationAsync(binding, token),
            token => TerminateAsync(binding, token));

    private WorkflowInstanceHandle<TOutput> CreateInstanceHandle<TOutput>(InstanceBinding binding) =>
        WorkflowRuntimeBridge.InstanceHandle(
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
        return new global::OrcaCore.WorkflowInstanceSnapshot(
            snapshot.InstanceId,
            WorkflowMode.Durable,
            snapshot.DefinitionId,
            snapshot.DefinitionVersion,
            binding.DefinitionFingerprint,
            MapStatus(snapshot.Status),
            snapshot.CreatedAt,
            terminal ? snapshot.UpdatedAt : null,
            string.IsNullOrWhiteSpace(snapshot.EndOutcomeName)
                ? null
                : WorkflowOutcomeName.Create(snapshot.EndOutcomeName),
            MapFailure(snapshot),
            snapshot.ActiveWaits.Select(MapWait).ToArray());
    }

    private async ValueTask<object?> GetStateAsync(
        InstanceBinding binding,
        Type requestedType,
        CancellationToken cancellationToken)
    {
        if (requestedType != binding.StateType)
        {
            throw WorkflowRuntimeBridge.StateTypeMismatch(
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
        var result = await management.CancelAsync(
            binding.InstanceId,
            timeProvider.GetUtcNow(),
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
        var result = await management.TerminateAsync(
            binding.InstanceId,
            timeProvider.GetUtcNow(),
            DestructiveCommandSafety.Confirmed,
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
        if (snapshot.Status == LegacyStatus.Completed && envelope.Output is { } output)
        {
        var detached = (TOutput)CoreWorkflowValueCodec.Deserialize(output.Payload, typeof(TOutput))!;
            return new WorkflowOutputResult<TOutput>.Available(detached);
        }

        return new WorkflowOutputResult<TOutput>.Unavailable(
            MapStatus(snapshot.Status),
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
                        throw WorkflowRuntimeBridge.OutputUnavailable(unavailable.Status, unavailable.Failure);
                }

                await signal.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                notifications.InstanceCommitted -= OnCommitted;
            }
        }
    }

    private async ValueTask<LegacySnapshot> GetProjectionAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        var snapshots = await projectionStore.ListAsync(
            new WorkflowProjectionQuery { InstanceId = instanceId },
            cancellationToken).ConfigureAwait(false);
        return snapshots.SingleOrDefault() ??
            throw WorkflowRuntimeBridge.InstanceNotFound(instanceId);
    }

    private async ValueTask<DurableExecutionEnvelopeV2> GetEnvelopeAsync(
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

        return DurableExecutionEnvelopeV2.Deserialize(checkpoint.Value.Payload);
    }

    private static IReadOnlyList<ResourcePoolName> RequiredDurablePools(object runtimeDefinition)
    {
        var compiledPlan = runtimeDefinition.GetType()
            .GetProperty(
                "CompiledPlan",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetValue(runtimeDefinition)!;
        var instructions = (System.Collections.IEnumerable)compiledPlan.GetType()
            .GetProperty("Instructions")!
            .GetValue(compiledPlan)!;
        var names = new List<ResourcePoolName>();
        foreach (var instruction in instructions)
        {
            var request = instruction!.GetType()
                .GetProperty(
                    "StaticLeaseRequest",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
                .GetValue(instruction) as ResourceLeaseRequest;
            if (request is not null)
            {
                names.AddRange(request.Requirements.Select(requirement => requirement.Pool));
            }
        }

        return names;
    }

    private static global::OrcaCore.ActiveWaitSnapshot MapWait(LegacyActiveWaitSnapshot wait) =>
        new(
            wait.WaitId,
            WorkflowRuntimeBridge.AuthoredLocation("workflow:$"),
            EventName.Create(wait.EventName),
            wait.CorrelationId,
            wait.RegisteredAt,
            null);

    private static WorkflowFailure? MapFailure(LegacySnapshot snapshot)
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
        return WorkflowRuntimeBridge.WorkflowFailure(
            code,
            message,
            WorkflowRuntimeBridge.AuthoredLocation("workflow:$"),
            WorkflowRuntimeBridge.RootFailureOccurrence(),
            []);
    }

    private static WorkflowInstanceStatus MapStatus(LegacyStatus status) => status switch
    {
        LegacyStatus.Running => WorkflowInstanceStatus.Running,
        LegacyStatus.Waiting => WorkflowInstanceStatus.Waiting,
        LegacyStatus.CancellationRequested => WorkflowInstanceStatus.CancellationRequested,
        LegacyStatus.Completed => WorkflowInstanceStatus.Completed,
        LegacyStatus.Failed or LegacyStatus.CompensationFailed => WorkflowInstanceStatus.Failed,
        LegacyStatus.TimedOut => WorkflowInstanceStatus.TimedOut,
        LegacyStatus.Cancelled or LegacyStatus.Compensated => WorkflowInstanceStatus.Cancelled,
        LegacyStatus.Terminated => WorkflowInstanceStatus.Terminated,
        LegacyStatus.Paused or LegacyStatus.Parked => WorkflowInstanceStatus.Waiting,
        _ => WorkflowInstanceStatus.Pending
    };

    private static bool IsTerminal(LegacyStatus status) =>
        status is LegacyStatus.Completed or
            LegacyStatus.Failed or
            LegacyStatus.TimedOut or
            LegacyStatus.Cancelled or
            LegacyStatus.Terminated or
            LegacyStatus.Compensated or
            LegacyStatus.CompensationFailed;

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

public sealed class DurableWorkflowEventClient(
    DurableWorkflowRuntime runtime,
    IWorkflowProjectionStore projectionStore,
    IWorkflowInboxStore inboxStore,
    bool driveAfterDelivery = true) : IWorkflowEventClient
{
    private readonly object gate = new();
    private readonly Dictionary<(InstanceId InstanceId, string EventId), string> accepted = [];

    public ValueTask<EventDeliveryResult> DeliverToInstanceAsync(
        InstanceId instanceId,
        global::OrcaCore.WorkflowEvent @event,
        CancellationToken cancellationToken = default) =>
        DeliverToInstanceCoreAsync<object?>(
            instanceId,
            @event.EventId,
            @event.EventName,
            @event.CorrelationId,
            @event.OccurredAt,
            null,
            cancellationToken);

    public ValueTask<EventDeliveryResult> DeliverToInstanceAsync<TPayload>(
        InstanceId instanceId,
        global::OrcaCore.WorkflowEvent<TPayload> @event,
        CancellationToken cancellationToken = default) =>
        DeliverToInstanceCoreAsync(
            instanceId,
            @event.EventId,
            @event.EventName,
            @event.CorrelationId,
            @event.OccurredAt,
            @event.Payload,
            cancellationToken);

    public ValueTask<EventDeliveryResult> DeliverByCorrelationAsync(
        DefinitionId definitionId,
        global::OrcaCore.WorkflowEvent @event,
        CancellationToken cancellationToken = default) =>
        DeliverByCorrelationCoreAsync<object?>(
            definitionId,
            @event.EventId,
            @event.EventName,
            @event.CorrelationId,
            @event.OccurredAt,
            null,
            cancellationToken);

    public ValueTask<EventDeliveryResult> DeliverByCorrelationAsync<TPayload>(
        DefinitionId definitionId,
        global::OrcaCore.WorkflowEvent<TPayload> @event,
        CancellationToken cancellationToken = default) =>
        DeliverByCorrelationCoreAsync(
            definitionId,
            @event.EventId,
            @event.EventName,
            @event.CorrelationId,
            @event.OccurredAt,
            @event.Payload,
            cancellationToken);

    private async ValueTask<EventDeliveryResult> DeliverByCorrelationCoreAsync<TPayload>(
        DefinitionId definitionId,
        EventId eventId,
        EventName eventName,
        CorrelationId correlationId,
        DateTimeOffset occurredAt,
        TPayload payload,
        CancellationToken cancellationToken)
    {
        var matches = await projectionStore.ListAsync(
            new WorkflowProjectionQuery
            {
                DefinitionId = definitionId,
                ActiveWaitEventName = eventName.Value,
                ActiveWaitCorrelationId = correlationId
            },
            cancellationToken).ConfigureAwait(false);
        if (matches.Count == 0)
        {
            return new EventDeliveryResult(EventDeliveryStatus.NoActiveWait, null);
        }

        if (matches.Count > 1)
        {
            throw WorkflowRuntimeBridge.AmbiguousWait(definitionId, eventName, correlationId);
        }

        return await DeliverToInstanceCoreAsync(
            matches[0].InstanceId,
            eventId,
            eventName,
            correlationId,
            occurredAt,
            payload,
            cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<EventDeliveryResult> DeliverToInstanceCoreAsync<TPayload>(
        InstanceId instanceId,
        EventId eventId,
        EventName eventName,
        CorrelationId correlationId,
        DateTimeOffset occurredAt,
        TPayload payload,
        CancellationToken cancellationToken)
    {
        var matches = await projectionStore.ListAsync(
            new WorkflowProjectionQuery { InstanceId = instanceId },
            cancellationToken).ConfigureAwait(false);
        var snapshot = matches.SingleOrDefault() ??
            throw WorkflowRuntimeBridge.InstanceNotFound(instanceId);
        var fingerprint = DurableEventEnvelopeFingerprint.Create(
            eventName,
            correlationId,
            occurredAt,
            payload);
        lock (gate)
        {
            if (accepted.TryGetValue((instanceId, eventId.Value), out var existing))
            {
                return new EventDeliveryResult(
                    string.Equals(existing, fingerprint, StringComparison.Ordinal)
                        ? EventDeliveryStatus.Duplicate
                        : EventDeliveryStatus.EventConflict,
                instanceId);
            }
        }

        var prior = await inboxStore
            .GetAsync(instanceId, eventId, cancellationToken)
            .ConfigureAwait(false);
        if (prior.HasValue)
        {
            return new EventDeliveryResult(
                string.Equals(
                    prior.Value.EnvelopeFingerprint,
                    fingerprint,
                    StringComparison.Ordinal)
                    ? EventDeliveryStatus.Duplicate
                    : EventDeliveryStatus.EventConflict,
                instanceId);
        }

        if (IsTerminal(snapshot.Status))
        {
            return new EventDeliveryResult(EventDeliveryStatus.InstanceTerminal, instanceId);
        }

        if (!snapshot.ActiveWaits.Any(wait =>
                string.Equals(wait.EventName, eventName.Value, StringComparison.Ordinal) &&
                wait.CorrelationId.Equals(correlationId)))
        {
            return new EventDeliveryResult(EventDeliveryStatus.NoActiveWait, instanceId);
        }

        var result = await runtime.RaiseFacadeEventAsync(
            instanceId,
            eventId,
            eventName,
            correlationId,
            occurredAt,
            payload,
            cancellationToken,
            driveAfterDelivery,
            fingerprint).ConfigureAwait(false);
        if (result.Outcome == DurableCommandOutcome.Committed)
        {
            lock (gate)
            {
                accepted[(instanceId, eventId.Value)] = fingerprint;
            }

            return new EventDeliveryResult(EventDeliveryStatus.Accepted, instanceId);
        }

        var committed = await inboxStore
            .GetAsync(instanceId, eventId, cancellationToken)
            .ConfigureAwait(false);
        return new EventDeliveryResult(
            committed.HasValue &&
            !string.Equals(
                committed.Value.EnvelopeFingerprint,
                fingerprint,
                StringComparison.Ordinal)
                ? EventDeliveryStatus.EventConflict
                : EventDeliveryStatus.Duplicate,
            instanceId);
    }

    private static bool IsTerminal(LegacyStatus status) =>
        status is LegacyStatus.Completed or
            LegacyStatus.Failed or
            LegacyStatus.TimedOut or
            LegacyStatus.Cancelled or
            LegacyStatus.Terminated or
            LegacyStatus.Compensated or
            LegacyStatus.CompensationFailed;

}
