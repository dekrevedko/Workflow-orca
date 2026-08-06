using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using OrcaCore.Core.Definitions;
using OrcaCore.Core.Execution;
using OrcaCore.Engine.Ephemeral.Execution;
using OrcaCore.Core.Internal;
using OrcaCore.Internal;
using LegacySnapshot = OrcaCore.Engine.Ephemeral.Execution.EphemeralWorkflowInstanceSnapshot;
using LegacyStatus = OrcaCore.WorkflowInstanceStatus;

namespace OrcaCore.Engine.Ephemeral;

internal sealed class EphemeralWorkflowDefinitionRegistry : IWorkflowDefinitionRegistry
{
    private static readonly MethodInfo RegisterRuntimeMethod = typeof(EphemeralWorkflowEngine)
        .GetMethods(BindingFlags.Instance | BindingFlags.Public)
        .Single(method => method.Name == nameof(EphemeralWorkflowEngine.RegisterDefinition));
    private static readonly MethodInfo StartRuntimeMethod = typeof(EphemeralWorkflowEngine)
        .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
        .Single(method => method.Name == nameof(EphemeralWorkflowEngine.StartCoreAsync));

    private readonly object gate = new();
    private readonly SemaphoreSlim startGate = new(1, 1);
    private readonly Dictionary<DefinitionKey, Registration> registrations = [];
    private readonly Dictionary<string, StartBinding> starts = new(StringComparer.Ordinal);
    private readonly Dictionary<InstanceId, InstanceBinding> instances = [];
    private readonly EphemeralWorkflowEngine engine;

    public EphemeralWorkflowDefinitionRegistry(EphemeralWorkflowEngine engine)
    {
        this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    public WorkflowRegistrationResult<EphemeralDefinitionHandle<TInput>> Register<TInput>(
        EphemeralWorkflowDefinition<TInput> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return RegisterCore(
            definition,
            global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.RuntimeDefinition(definition),
            global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.RuntimeStateType(definition),
            () => global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.EphemeralDefinitionHandle<TInput>(
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

    public WorkflowRegistrationResult<EphemeralDefinitionHandle<TInput, TOutput>> Register<TInput, TOutput>(
        EphemeralWorkflowDefinition<TInput, TOutput> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return RegisterCore(
            definition,
            global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.RuntimeDefinition(definition),
            global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.RuntimeStateType(definition),
            () => global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.EphemeralDefinitionHandle<TInput, TOutput>(
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

    public WorkflowRegistrationResult<DurableDefinitionHandle<TInput>> Register<TInput>(
        DurableWorkflowDefinition<TInput> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new WorkflowRegistrationResult<DurableDefinitionHandle<TInput>>.HostIncompatible(
            new DefinitionHostCompatibilityFailure.EngineModeMismatch(
                WorkflowMode.Ephemeral,
                WorkflowMode.Durable));
    }

    public WorkflowRegistrationResult<DurableDefinitionHandle<TInput, TOutput>> Register<TInput, TOutput>(
        DurableWorkflowDefinition<TInput, TOutput> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new WorkflowRegistrationResult<DurableDefinitionHandle<TInput, TOutput>>.HostIncompatible(
            new DefinitionHostCompatibilityFailure.EngineModeMismatch(
                WorkflowMode.Ephemeral,
                WorkflowMode.Durable));
    }

    internal IReadOnlyList<InstanceBinding> ListInstances() =>
        GetInstancesSnapshot();

    internal bool TryGetInstance(InstanceId instanceId, out InstanceBinding? binding)
    {
        lock (gate)
        {
            return instances.TryGetValue(instanceId, out binding);
        }
    }

    private WorkflowRegistrationResult<THandle> RegisterCore<THandle>(
        object publicDefinition,
        object runtimeDefinition,
        Type stateType,
        Func<THandle> createHandle)
    {
        var definitionId = (DefinitionId)publicDefinition.GetType()
            .GetProperty(nameof(EphemeralWorkflowDefinition<object>.DefinitionId))!
            .GetValue(publicDefinition)!;
        var definitionVersion = (DefinitionVersion)publicDefinition.GetType()
            .GetProperty(nameof(EphemeralWorkflowDefinition<object>.DefinitionVersion))!
            .GetValue(publicDefinition)!;
        var fingerprint = (DefinitionFingerprint)publicDefinition.GetType()
            .GetProperty(nameof(EphemeralWorkflowDefinition<object>.DefinitionFingerprint))!
            .GetValue(publicDefinition)!;
        var key = new DefinitionKey(definitionId, definitionVersion);

        lock (gate)
        {
            if (registrations.TryGetValue(key, out var existing))
            {
                return existing.Fingerprint.Equals(fingerprint)
                    ? new WorkflowRegistrationResult<THandle>.Registered((THandle)existing.Handle)
                    : new WorkflowRegistrationResult<THandle>.Conflict(
                        FacadeValueFactory.RegistrationConflict(
                            definitionId,
                            definitionVersion,
                            existing.Fingerprint,
                            fingerprint));
            }

            try
            {
                RegisterRuntimeMethod.MakeGenericMethod(stateType).Invoke(engine, [runtimeDefinition]);
            }
            catch (TargetInvocationException exception)
                when (exception.InnerException is WorkflowDefinitionException definitionException &&
                      definitionException.Message.Contains(
                          "HostIncompatible.MissingTransientPools:",
                          StringComparison.Ordinal))
            {
                const string marker = "HostIncompatible.MissingTransientPools:";
                var names = definitionException.Message[
                        (definitionException.Message.IndexOf(marker, StringComparison.Ordinal) + marker.Length)..]
                    .Split('(', 2)[0]
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(TransientPoolName.Create)
                    .ToArray();
                return new WorkflowRegistrationResult<THandle>.HostIncompatible(
                    new DefinitionHostCompatibilityFailure.MissingTransientPools(names));
            }

            var handle = createHandle();
            registrations.Add(
                key,
                new Registration(handle!, fingerprint, stateType, runtimeDefinition));
            return new WorkflowRegistrationResult<THandle>.Registered(handle);
        }
    }

    private async ValueTask<WorkflowStartResult<WorkflowInstanceHandle>> StartAsync<TInput>(
        EphemeralWorkflowDefinition<TInput> definition,
        TInput input,
        StartIdempotencyKey idempotencyKey,
        CancellationToken cancellationToken)
    {
        var started = await StartCoreAsync(
            definition.DefinitionId,
            definition.DefinitionVersion,
            definition.DefinitionFingerprint,
            global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.RuntimeStateType(definition),
            input,
            idempotencyKey,
            cancellationToken).ConfigureAwait(false);
        return started.Conflict is not null
            ? new WorkflowStartResult<WorkflowInstanceHandle>.Conflict(started.Conflict)
            : new WorkflowStartResult<WorkflowInstanceHandle>.Accepted(
                CreateInstanceHandle(started.Instance!),
                started.WasExisting);
    }

    private async ValueTask<WorkflowStartResult<WorkflowInstanceHandle<TOutput>>> StartAsync<TInput, TOutput>(
        EphemeralWorkflowDefinition<TInput, TOutput> definition,
        TInput input,
        StartIdempotencyKey idempotencyKey,
        CancellationToken cancellationToken)
    {
        var started = await StartCoreAsync(
            definition.DefinitionId,
            definition.DefinitionVersion,
            definition.DefinitionFingerprint,
            global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.RuntimeStateType(definition),
            input,
            idempotencyKey,
            cancellationToken).ConfigureAwait(false);
        return started.Conflict is not null
            ? new WorkflowStartResult<WorkflowInstanceHandle<TOutput>>.Conflict(started.Conflict)
            : new WorkflowStartResult<WorkflowInstanceHandle<TOutput>>.Accepted(
                CreateInstanceHandle<TOutput>(started.Instance!),
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
        cancellationToken.ThrowIfCancellationRequested();
        var inputFingerprint = FacadeValueFactory.PayloadFingerprint(input);

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
                        return new StartCoreResult(instances[existing.InstanceId], true, null);
                    }

                    return new StartCoreResult(
                        null,
                        false,
                        FacadeValueFactory.StartConflict(
                            idempotencyKey,
                            existing,
                            definitionId,
                            definitionVersion,
                            definitionFingerprint,
                            inputFingerprint));
                }
            }

            var task = (Task)StartRuntimeMethod
                .MakeGenericMethod(typeof(TInput), stateType)
                .Invoke(engine, [definitionId, input, cancellationToken])!;
            await task.ConfigureAwait(false);
            var snapshot = (LegacySnapshot)task.GetType().GetProperty("Result")!.GetValue(task)!;
            var ambiguous = FindAmbiguousWait(definitionId, snapshot);
            if (ambiguous is not null)
            {
                await engine.TerminateInstanceAsync(
                    snapshot.InstanceId,
                    CancellationToken.None).ConfigureAwait(false);
                throw FacadeValueFactory.AmbiguousWait(
                    definitionId,
                    WorkflowEventContract.Create(
                        EventName.Create(ambiguous.Value.EventName),
                        new EventContractVersion(ambiguous.Value.EventContractVersion)),
                    CorrelationId.Create(ambiguous.Value.CorrelationId));
            }

            var instance = new InstanceBinding(
                snapshot.InstanceId,
                definitionId,
                definitionVersion,
                definitionFingerprint,
                stateType);

            lock (gate)
            {
                instances.Add(snapshot.InstanceId, instance);
                starts.Add(
                    idempotencyKey.Value,
                    new StartBinding(
                        snapshot.InstanceId,
                        definitionId,
                        definitionVersion,
                        definitionFingerprint,
                        inputFingerprint));
            }

            return new StartCoreResult(instance, false, null);
        }
        finally
        {
            startGate.Release();
        }
    }

    private (string EventName, int EventContractVersion, string CorrelationId)? FindAmbiguousWait(
        DefinitionId definitionId,
        LegacySnapshot candidate)
    {
        var candidatePairs = candidate.ActiveWaits
            .Select(wait => (
                wait.EventContract.EventName.Value,
                wait.EventContract.Version.Value,
                wait.CorrelationId.Value))
            .ToArray();
        var duplicateInsideCandidate = candidatePairs
            .GroupBy(pair => pair)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateInsideCandidate is not null)
        {
            return duplicateInsideCandidate.Key;
        }

        var activePairs = ListInstances()
            .Where(binding => binding.DefinitionId.Equals(definitionId))
            .Where(binding => engine.TryGetFacadeInstance(binding.InstanceId, out _))
            .SelectMany(binding => RequireRuntimeInstance(binding.InstanceId)
                .GetPublishedSnapshot()
                .ActiveWaits)
            .Select(wait => (
                wait.EventContract.EventName.Value,
                wait.EventContract.Version.Value,
                wait.CorrelationId.Value))
            .ToHashSet();
        foreach (var pair in candidatePairs)
        {
            if (activePairs.Contains(pair))
            {
                return pair;
            }
        }

        return null;
    }

    private ValueTask<WorkflowInstanceHandle> GetInstanceAsync(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var binding = RequireInstance(definitionId, definitionVersion, instanceId);
        return ValueTask.FromResult(CreateInstanceHandle(binding));
    }

    private ValueTask<WorkflowInstanceHandle<TOutput>> GetInstanceAsync<TOutput>(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var binding = RequireInstance(definitionId, definitionVersion, instanceId);
        return ValueTask.FromResult(CreateInstanceHandle<TOutput>(binding));
    }

    private InstanceBinding RequireInstance(
        DefinitionId expectedId,
        DefinitionVersion expectedVersion,
        InstanceId instanceId)
    {
        lock (gate)
        {
            if (!instances.TryGetValue(instanceId, out var binding))
            {
                throw FacadeValueFactory.InstanceNotFound(instanceId);
            }

            if (!binding.DefinitionId.Equals(expectedId) ||
                !binding.DefinitionVersion.Equals(expectedVersion))
            {
                throw FacadeValueFactory.InstanceDefinitionMismatch(
                    instanceId,
                    expectedId,
                    binding.DefinitionId);
            }

            return binding;
        }
    }

    private WorkflowInstanceHandle CreateInstanceHandle(InstanceBinding binding) =>
        FacadeHandleFactory.Create(
            binding.InstanceId,
            token => GetSnapshotAsync(binding, token),
            (requestedType, token) => GetStateAsync(binding, requestedType, token),
            token => RequestCancellationAsync(binding, token),
            token => TerminateAsync(binding, token));

    private WorkflowInstanceHandle<TOutput> CreateInstanceHandle<TOutput>(InstanceBinding binding) =>
        FacadeHandleFactory.Create<TOutput>(
            binding.InstanceId,
            token => GetSnapshotAsync(binding, token),
            (requestedType, token) => GetStateAsync(binding, requestedType, token),
            token => RequestCancellationAsync(binding, token),
            token => TerminateAsync(binding, token),
            token => GetOutputAsync<TOutput>(binding, token),
            token => WaitForOutputAsync<TOutput>(binding, token));

    private ValueTask<global::OrcaCore.WorkflowInstanceSnapshot> GetSnapshotAsync(
        InstanceBinding binding,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var instance = RequireRuntimeInstance(binding.InstanceId);
        return ValueTask.FromResult(MapSnapshot(
            binding,
            instance.GetPublishedSnapshot(),
            instance.IsCancellationRequested));
    }

    private ValueTask<object?> GetStateAsync(
        InstanceBinding binding,
        Type requestedType,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var instance = RequireRuntimeInstance(binding.InstanceId);
        if (instance.StateType != requestedType)
        {
            throw FacadeValueFactory.StateTypeMismatch(
                binding.InstanceId,
                instance.StateType,
                requestedType);
        }

        return ValueTask.FromResult<object?>(instance.CopyState());
    }

    private async ValueTask<WorkflowCancellationRequestStatus> RequestCancellationAsync(
        InstanceBinding binding,
        CancellationToken cancellationToken)
    {
        return await engine.RequestCancellationAsync(binding.InstanceId, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<WorkflowTerminationStatus> TerminateAsync(
        InstanceBinding binding,
        CancellationToken cancellationToken)
    {
        return await engine.RequestTerminationAsync(binding.InstanceId, cancellationToken).ConfigureAwait(false);
    }

    private ValueTask<WorkflowOutputResult<TOutput>> GetOutputAsync<TOutput>(
        InstanceBinding binding,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var instance = RequireRuntimeInstance(binding.InstanceId);
        var snapshot = instance.GetPublishedSnapshot();
        if (!IsTerminal(snapshot.Status))
        {
            return ValueTask.FromResult<WorkflowOutputResult<TOutput>>(new WorkflowOutputResult<TOutput>.Pending());
        }

        var payload = instance.CopyOutputPayload();
        if (snapshot.Status == LegacyStatus.Completed &&
            payload is not null &&
            instance.OutputType == typeof(TOutput))
        {
        var output = (TOutput)CoreWorkflowValueCodec.Deserialize(payload, typeof(TOutput))!;
            return ValueTask.FromResult<WorkflowOutputResult<TOutput>>(
                new WorkflowOutputResult<TOutput>.Available(output));
        }

        return ValueTask.FromResult<WorkflowOutputResult<TOutput>>(
            new WorkflowOutputResult<TOutput>.Unavailable(MapStatus(snapshot.Status), null));
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

            engine.InstanceCommitted += OnCommitted;
            try
            {
                var result = await GetOutputAsync<TOutput>(binding, cancellationToken).ConfigureAwait(false);
                switch (result)
                {
                    case WorkflowOutputResult<TOutput>.Available available:
                        return available.Output;
                    case WorkflowOutputResult<TOutput>.Unavailable unavailable:
                        throw FacadeValueFactory.OutputUnavailable(unavailable.Status, unavailable.Failure);
                }

                await signal.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                engine.InstanceCommitted -= OnCommitted;
            }
        }
    }

    private IWorkflowInstance RequireRuntimeInstance(InstanceId instanceId) =>
        engine.TryGetFacadeInstance(instanceId, out var instance)
            ? instance!
            : throw FacadeValueFactory.InstanceNotFound(instanceId);

    private global::OrcaCore.WorkflowInstanceSnapshot MapSnapshot(
        InstanceBinding binding,
        LegacySnapshot snapshot,
        bool cancellationRequested)
    {
        var terminal = IsTerminal(snapshot.Status);
        return new global::OrcaCore.WorkflowInstanceSnapshot(
            snapshot.InstanceId,
            WorkflowMode.Ephemeral,
            snapshot.DefinitionId,
            snapshot.DefinitionVersion,
            binding.DefinitionFingerprint,
            cancellationRequested && !terminal
                ? WorkflowInstanceStatus.CancellationRequested
                : MapStatus(snapshot.Status),
            snapshot.CreatedAt,
            terminal ? snapshot.UpdatedAt : null,
            string.IsNullOrWhiteSpace(snapshot.EndOutcomeName)
                ? null
                : WorkflowOutcomeName.Create(snapshot.EndOutcomeName),
            MapFailure(snapshot),
            snapshot.ActiveWaits.Select(MapWait).ToArray());
    }

    private static global::OrcaCore.ActiveWaitSnapshot MapWait(
        OrcaCore.Engine.Ephemeral.Execution.EphemeralActiveWaitSnapshot wait) =>
        new(
            wait.WaitId,
            FailureProvenance.LocationFromCompilerPath(wait.AuthoredPath),
            wait.EventContract,
            wait.CorrelationId,
            wait.RegisteredAt,
            wait.Deadline);

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
        return global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.WorkflowFailure(
            code,
            message,
            global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.AuthoredLocation("workflow:$"),
            global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.RootFailureOccurrence(),
            []);
    }

    private static WorkflowInstanceStatus MapStatus(LegacyStatus status) => status switch
    {
        LegacyStatus.Running => WorkflowInstanceStatus.Running,
        LegacyStatus.Waiting => WorkflowInstanceStatus.Waiting,
        LegacyStatus.Completed => WorkflowInstanceStatus.Completed,
        LegacyStatus.Failed => WorkflowInstanceStatus.Failed,
        LegacyStatus.TimedOut => WorkflowInstanceStatus.TimedOut,
        LegacyStatus.Cancelled => WorkflowInstanceStatus.Cancelled,
        LegacyStatus.Terminated => WorkflowInstanceStatus.Terminated,
        _ => WorkflowInstanceStatus.Pending
    };

    private static bool IsTerminal(LegacyStatus status) =>
        status is LegacyStatus.Completed or
            LegacyStatus.Failed or
            LegacyStatus.TimedOut or
            LegacyStatus.Cancelled or
            LegacyStatus.Terminated;

    private IReadOnlyList<InstanceBinding> GetInstancesSnapshot()
    {
        lock (gate)
        {
            return instances.Values.ToArray();
        }
    }

    private sealed record DefinitionKey(DefinitionId Id, DefinitionVersion Version);
    private sealed record Registration(
        object Handle,
        DefinitionFingerprint Fingerprint,
        Type StateType,
        object RuntimeDefinition);
    private sealed record StartCoreResult(
        InstanceBinding? Instance,
        bool WasExisting,
        StartIdempotencyConflict? Conflict);

    internal sealed record InstanceBinding(
        InstanceId InstanceId,
        DefinitionId DefinitionId,
        DefinitionVersion DefinitionVersion,
        DefinitionFingerprint DefinitionFingerprint,
        Type StateType);

    internal sealed record StartBinding(
        InstanceId InstanceId,
        DefinitionId DefinitionId,
        DefinitionVersion DefinitionVersion,
        DefinitionFingerprint DefinitionFingerprint,
        PayloadFingerprint InputFingerprint);
}

internal sealed class EphemeralWorkflowEventClient(
    EphemeralWorkflowEngine engine,
    EphemeralWorkflowDefinitionRegistry registry) : IWorkflowEventClient
{
    private readonly object gate = new();
    private readonly Dictionary<(InstanceId InstanceId, string EventId), string> accepted = [];

    public ValueTask<EventDeliveryResult> DeliverToInstanceAsync(
        InstanceId instanceId,
        global::OrcaCore.WorkflowEvent @event,
        CancellationToken cancellationToken = default) =>
        DeliverToInstanceCoreAsync(
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
            CoreWorkflowValueCodec.Serialize(@event.Payload, typeof(TPayload)),
            cancellationToken);

    public ValueTask<EventDeliveryResult> DeliverByCorrelationAsync(
        DefinitionId definitionId,
        global::OrcaCore.WorkflowEvent @event,
        CancellationToken cancellationToken = default) =>
        DeliverByCorrelationCoreAsync(
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
            CoreWorkflowValueCodec.Serialize(@event.Payload, typeof(TPayload)),
            cancellationToken);

    private async ValueTask<EventDeliveryResult> DeliverByCorrelationCoreAsync(
        DefinitionId definitionId,
        EventId eventId,
        EventName eventName,
        CorrelationId correlationId,
        DateTimeOffset occurredAt,
        byte[]? payloadBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definitionId);
        var matches = registry.ListInstances()
            .Where(binding => binding.DefinitionId.Equals(definitionId))
            .Where(binding => engine.TryGetFacadeInstance(binding.InstanceId, out var instance) &&
                instance!.GetPublishedSnapshot().ActiveWaits.Any(wait =>
                    wait.EventContract.Version.Equals(EventContractVersion.Initial) &&
                    wait.EventContract.EventName.Equals(eventName) &&
                    wait.CorrelationId.Equals(correlationId)))
            .ToArray();
        if (matches.Length == 0)
        {
            return new EventDeliveryResult(EventDeliveryStatus.NoActiveWait, null);
        }

        if (matches.Length > 1)
        {
            throw FacadeValueFactory.AmbiguousWait(
                definitionId,
                WorkflowEventContract.Create(eventName, EventContractVersion.Initial),
                correlationId);
        }

        return await DeliverToInstanceCoreAsync(
            matches[0].InstanceId,
            eventId,
            eventName,
            correlationId,
            occurredAt,
            payloadBytes,
            cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<EventDeliveryResult> DeliverToInstanceCoreAsync(
        InstanceId instanceId,
        EventId eventId,
        EventName eventName,
        CorrelationId correlationId,
        DateTimeOffset occurredAt,
        byte[]? payloadBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instanceId);
        cancellationToken.ThrowIfCancellationRequested();
        if (!registry.TryGetInstance(instanceId, out var binding) ||
            !engine.TryGetFacadeInstance(instanceId, out var instance))
        {
            throw FacadeValueFactory.InstanceNotFound(instanceId);
        }

        var fingerprint = EventFingerprint(eventName, correlationId, occurredAt, payloadBytes);
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

        var snapshot = instance!.GetPublishedSnapshot();
        if (snapshot.Status is LegacyStatus.Completed or
            LegacyStatus.Failed or
            LegacyStatus.TimedOut or
            LegacyStatus.Cancelled or
            LegacyStatus.Terminated)
        {
            return new EventDeliveryResult(EventDeliveryStatus.InstanceTerminal, instanceId);
        }

        var hasWait = snapshot.ActiveWaits.Any(wait =>
            wait.EventContract.Version.Equals(EventContractVersion.Initial) &&
            wait.EventContract.EventName.Equals(eventName) &&
            wait.CorrelationId.Equals(correlationId));
        if (!hasWait)
        {
            return new EventDeliveryResult(EventDeliveryStatus.NoActiveWait, instanceId);
        }

        var envelope = global::OrcaCore.Engine.Ephemeral.Internal.EphemeralApplicationContractFactory.EventEnvelope(
            eventId,
            WorkflowEventContract.Create(eventName, EventContractVersion.Initial),
            correlationId,
            occurredAt,
            payloadBytes ?? []);
        await RaiseEventAsync(binding!.StateType, instanceId, envelope, cancellationToken).ConfigureAwait(false);
        lock (gate)
        {
            accepted[(instanceId, eventId.Value)] = fingerprint;
        }

        return new EventDeliveryResult(EventDeliveryStatus.Accepted, instanceId);
    }

    private async Task RaiseEventAsync(
        Type stateType,
        InstanceId instanceId,
        EventEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var method = typeof(EphemeralWorkflowEngine)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(candidate => candidate.Name == nameof(EphemeralWorkflowEngine.RaiseEventCoreAsync) &&
                candidate.IsGenericMethodDefinition);
        var task = (Task)method.MakeGenericMethod(stateType)
            .Invoke(engine, [instanceId, envelope, cancellationToken])!;
        await task.ConfigureAwait(false);
    }

    private static string EventFingerprint(
        EventName eventName,
        CorrelationId correlationId,
        DateTimeOffset occurredAt,
        byte[]? payload)
    {
        var prefix = Encoding.UTF8.GetBytes(
            $"{eventName.Value}\n{correlationId.Value}\n{occurredAt.ToUniversalTime():O}\n");
        var buffer = new byte[prefix.Length + (payload?.Length ?? 0)];
        prefix.CopyTo(buffer, 0);
        payload?.CopyTo(buffer, prefix.Length);
        return Convert.ToHexString(SHA256.HashData(buffer));
    }
}

file static class FacadeHandleFactory
{
    internal static WorkflowInstanceHandle Create(
        InstanceId instanceId,
        Func<CancellationToken, ValueTask<global::OrcaCore.WorkflowInstanceSnapshot>> getSnapshot,
        Func<Type, CancellationToken, ValueTask<object?>> getState,
        Func<CancellationToken, ValueTask<WorkflowCancellationRequestStatus>> requestCancellation,
        Func<CancellationToken, ValueTask<WorkflowTerminationStatus>> terminate) =>
        (WorkflowInstanceHandle)typeof(WorkflowInstanceHandle)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single()
            .Invoke([instanceId, getSnapshot, getState, requestCancellation, terminate]);

    internal static WorkflowInstanceHandle<TOutput> Create<TOutput>(
        InstanceId instanceId,
        Func<CancellationToken, ValueTask<global::OrcaCore.WorkflowInstanceSnapshot>> getSnapshot,
        Func<Type, CancellationToken, ValueTask<object?>> getState,
        Func<CancellationToken, ValueTask<WorkflowCancellationRequestStatus>> requestCancellation,
        Func<CancellationToken, ValueTask<WorkflowTerminationStatus>> terminate,
        Func<CancellationToken, ValueTask<WorkflowOutputResult<TOutput>>> getOutput,
        Func<CancellationToken, ValueTask<TOutput>> waitForOutput) =>
        (WorkflowInstanceHandle<TOutput>)typeof(WorkflowInstanceHandle<TOutput>)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single()
            .Invoke([
                instanceId,
                getSnapshot,
                getState,
                requestCancellation,
                terminate,
                getOutput,
                waitForOutput
            ]);
}

file static class FacadeValueFactory
{
    internal static PayloadFingerprint PayloadFingerprint<T>(T value)
    {
        var bytes = CoreWorkflowValueCodec.Serialize(value, typeof(T));
        return Construct<PayloadFingerprint>(Convert.ToHexString(SHA256.HashData(bytes)));
    }

    internal static DefinitionRegistrationConflict RegistrationConflict(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint existing,
        DefinitionFingerprint attempted) =>
        Construct<DefinitionRegistrationConflict>(
            definitionId,
            definitionVersion,
            existing,
            attempted);

    internal static StartIdempotencyConflict StartConflict(
        StartIdempotencyKey key,
        EphemeralWorkflowDefinitionRegistry.StartBinding existing,
        DefinitionId attemptedId,
        DefinitionVersion attemptedVersion,
        DefinitionFingerprint attemptedFingerprint,
        PayloadFingerprint attemptedInput) =>
        Construct<StartIdempotencyConflict>(
            key,
            existing.DefinitionId,
            existing.DefinitionVersion,
            existing.DefinitionFingerprint,
            existing.InputFingerprint,
            attemptedId,
            attemptedVersion,
            attemptedFingerprint,
            attemptedInput);

    internal static WorkflowInstanceNotFoundException InstanceNotFound(InstanceId instanceId) =>
        Construct<WorkflowInstanceNotFoundException>(instanceId);

    internal static WorkflowInstanceDefinitionMismatchException InstanceDefinitionMismatch(
        InstanceId instanceId,
        DefinitionId expected,
        DefinitionId actual) =>
        Construct<WorkflowInstanceDefinitionMismatchException>(instanceId, expected, actual);

    internal static WorkflowStateTypeMismatchException StateTypeMismatch(
        InstanceId instanceId,
        Type actual,
        Type requested) =>
        Construct<WorkflowStateTypeMismatchException>(instanceId, actual, requested);

    internal static WorkflowOutputUnavailableException OutputUnavailable(
        WorkflowInstanceStatus status,
        WorkflowFailure? failure) =>
        Construct<WorkflowOutputUnavailableException>(status, failure);

    internal static AmbiguousWaitRegistrationException AmbiguousWait(
        DefinitionId definitionId,
        WorkflowEventContract eventContract,
        CorrelationId correlationId) =>
        Construct<AmbiguousWaitRegistrationException>(definitionId, eventContract, correlationId);

    internal static AuthoredLocation RootLocation() =>
        Construct<AuthoredLocation>("workflow:$");

    private static T Construct<T>(params object?[] arguments) =>
        (T)typeof(T)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(constructor => constructor.GetParameters().Length == arguments.Length)
            .Invoke(arguments);
}
