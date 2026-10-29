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

internal sealed partial class EphemeralWorkflowDefinitionRegistry : IWorkflowDefinitionRegistry
{
    private readonly object gate;
    private readonly SemaphoreSlim startGate = new(1, 1);
    private readonly Dictionary<DefinitionKey, Registration> registrations = [];
    private readonly Dictionary<string, StartBinding> starts = new(StringComparer.Ordinal);
    private readonly Dictionary<InstanceId, InstanceBinding> instances = [];
    private readonly EphemeralWorkflowEngine engine;

    public EphemeralWorkflowDefinitionRegistry(EphemeralWorkflowEngine engine)
    {
        this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
        gate = engine.VisibilityGate;
    }

    public WorkflowRegistrationResult<EphemeralDefinitionHandle<TInput>> Register<TInput>(
        EphemeralWorkflowDefinition<TInput> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return RegisterCore(
            definition.DefinitionId, definition.DefinitionVersion, definition.DefinitionFingerprint,
            global::OrcaCore.Engine.Ephemeral.Internal.EphemeralContractAdapter.RuntimeDefinition(definition),
            global::OrcaCore.Engine.Ephemeral.Internal.EphemeralContractAdapter.RuntimeStateType(definition),
            () => global::OrcaCore.Engine.Ephemeral.Internal.EphemeralContractAdapter.EphemeralDefinitionHandle<TInput>(
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
            definition.DefinitionId, definition.DefinitionVersion, definition.DefinitionFingerprint,
            global::OrcaCore.Engine.Ephemeral.Internal.EphemeralContractAdapter.RuntimeDefinition(definition),
            global::OrcaCore.Engine.Ephemeral.Internal.EphemeralContractAdapter.RuntimeStateType(definition),
            () => global::OrcaCore.Engine.Ephemeral.Internal.EphemeralContractAdapter.EphemeralDefinitionHandle<TInput, TOutput>(
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
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint fingerprint,
        object runtimeDefinition,
        Type stateType,
        Func<THandle> createHandle)
    {
        var typedRuntime = EphemeralDefinitionRuntime.Bind(engine, runtimeDefinition);
        var key = new DefinitionKey(definitionId, definitionVersion);
        var missingPools = typedRuntime.MissingTransientPools();
        if (missingPools.Count > 0)
        {
            return new WorkflowRegistrationResult<THandle>.HostIncompatible(
                new DefinitionHostCompatibilityFailure.MissingTransientPools(missingPools));
        }

        lock (gate)
        {
            using var registrationOwnership = engine.HostLifetime.AdmitStart();
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

            typedRuntime.Register();

            var handle = createHandle();
            registrations.Add(
                key,
                new Registration(handle!, fingerprint, stateType, runtimeDefinition, typedRuntime));
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

        throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralContractAdapter.DefinitionNotRegistered(
            definitionId,
            definitionVersion,
            definitionFingerprint);
    }

    private static WorkflowDefinitionRegistrationConflictException StagedRegistrationConflict(
        IEphemeralStagedWorkflowDefinition definition,
        DefinitionFingerprint existingFingerprint)
    {
        var conflict = FacadeValueFactory.RegistrationConflict(
            definition.DefinitionId,
            definition.DefinitionVersion,
            existingFingerprint,
            definition.DefinitionFingerprint);
        return global::OrcaCore.Engine.Ephemeral.Internal.EphemeralContractAdapter.RegistrationConflict(
            conflict);
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
            global::OrcaCore.Engine.Ephemeral.Internal.EphemeralContractAdapter.RuntimeStateType(definition),
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
            global::OrcaCore.Engine.Ephemeral.Internal.EphemeralContractAdapter.RuntimeStateType(definition),
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
        using var startOwnership = engine.HostLifetime.AdmitStart();
        var inputFingerprint = FacadeValueFactory.PayloadFingerprint(input);

        await startGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        LegacySnapshot? provisional = null;
        try
        {
            StartCoreResult? replay = null;
            EphemeralInstanceLifetime.WorkLease? replayPin = null;
            lock (gate)
            {
                engine.HostLifetime.RequireOpen();
                if (starts.TryGetValue(idempotencyKey.Value, out var existing))
                {
                    if (!engine.TryGetFacadeInstance(existing.InstanceId, out var retained) ||
                        !retained!.Lifetime.TryPin(out replayPin))
                    {
                        starts.Remove(idempotencyKey.Value);
                        instances.Remove(existing.InstanceId);
                    }
                    else if (existing.DefinitionId.Equals(definitionId) &&
                        existing.DefinitionVersion.Equals(definitionVersion) &&
                        existing.DefinitionFingerprint.Equals(definitionFingerprint) &&
                        existing.InputFingerprint.Equals(inputFingerprint))
                        replay = new StartCoreResult(instances[existing.InstanceId], true, null);
                    else
                        replay = new StartCoreResult(null, false, FacadeValueFactory.StartConflict(
                            idempotencyKey, existing, definitionId, definitionVersion, definitionFingerprint, inputFingerprint));
                }
            }
            if (replay is not null)
            {
                using (replayPin) return replay;
            }

            IEphemeralDefinitionRuntime typedRuntime;
            lock (gate) typedRuntime = registrations[new DefinitionKey(definitionId, definitionVersion)].Runtime;
            var snapshot = await typedRuntime.StartAsync(definitionId, definitionVersion, input, cancellationToken).ConfigureAwait(false);
            provisional = snapshot;
            var ambiguous = FindAmbiguousWait(definitionId, snapshot);
            if (ambiguous is not null)
            {
                throw FacadeValueFactory.AmbiguousWait(
                    definitionId,
                    WorkflowEventContract.Create(
                        EventName.Create(ambiguous.Value.EventName),
                        new EventContractVersion(ambiguous.Value.EventContractVersion)),
                    CorrelationId.Create(ambiguous.Value.CorrelationId));
            }

            if (!engine.TryGetFacadeInstance(snapshot.InstanceId, out var runtime))
                throw FacadeValueFactory.InstanceNotFound(snapshot.InstanceId);
            using var publicationOwnership = runtime!.Lifetime.Own();
            var publishedId = snapshot.InstanceId;
            runtime.Lifetime.RegisterCleanup(this, () => ForgetPublishedInstance(publishedId));

            var instance = new InstanceBinding(
                snapshot.InstanceId,
                definitionId,
                definitionVersion,
                definitionFingerprint,
                stateType, typedRuntime);

            lock (gate)
            {
                if (!engine.TryAcceptStart(snapshot.InstanceId, cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (engine.HostLifetime.IsStopping) throw new global::OrcaCore.Hosting.EphemeralHostStoppingException();
                    throw FacadeValueFactory.InstanceNotFound(snapshot.InstanceId);
                }
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
        catch (Exception original)
        {
            if (provisional is not null)
            {
                try { await engine.AbortProvisionalStartAsync(provisional.InstanceId).ConfigureAwait(false); }
                catch (Exception cleanupError) { original.Data["OrcaCore.AbortCleanupError"] = cleanupError; }
            }
            throw;
        }
        finally
        {
            startGate.Release();
        }
    }

    private void ForgetPublishedInstance(InstanceId instanceId)
    {
        lock (gate)
        {
            instances.Remove(instanceId);
            foreach (var key in starts.Where(pair => pair.Value.InstanceId.Equals(instanceId))
                         .Select(pair => pair.Key).ToArray()) starts.Remove(key);
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
            .Select(binding => engine.TryGetFacadeInstance(binding.InstanceId, out var runtime)
                ? runtime!.GetPublishedSnapshot() : null)
            .Where(snapshot => snapshot is not null)
            .SelectMany(snapshot => snapshot!.ActiveWaits)
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
        if (snapshot.StructuredFailure is { } failure) return failure;
        if (string.IsNullOrWhiteSpace(snapshot.ErrorSummary))
        {
            return null;
        }

        var separator = snapshot.ErrorSummary.IndexOf(':', StringComparison.Ordinal);
        var code = separator > 0 ? snapshot.ErrorSummary[..separator] : "WF-RUNTIME-FAILED";
        var message = separator > 0
            ? snapshot.ErrorSummary[(separator + 1)..].Trim()
            : snapshot.ErrorSummary;
        return global::OrcaCore.Engine.Ephemeral.Internal.EphemeralContractAdapter.WorkflowFailure(
            code,
            message,
            global::OrcaCore.Engine.Ephemeral.Internal.EphemeralContractAdapter.AuthoredLocation("workflow:$"),
            global::OrcaCore.Engine.Ephemeral.Internal.EphemeralContractAdapter.RootFailureOccurrence(),
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
        object RuntimeDefinition,
        IEphemeralDefinitionRuntime Runtime);
    private sealed record StartCoreResult(
        InstanceBinding? Instance,
        bool WasExisting,
        StartIdempotencyConflict? Conflict);

    internal sealed record InstanceBinding(
        InstanceId InstanceId,
        DefinitionId DefinitionId,
        DefinitionVersion DefinitionVersion,
        DefinitionFingerprint DefinitionFingerprint,
        Type StateType,
        IEphemeralDefinitionRuntime Runtime);

    internal sealed record StartBinding(
        InstanceId InstanceId,
        DefinitionId DefinitionId,
        DefinitionVersion DefinitionVersion,
        DefinitionFingerprint DefinitionFingerprint,
        PayloadFingerprint InputFingerprint);
}

internal enum EphemeralEventRouteStatus
{
    Accepted,
    Duplicate,
    NoActiveWait,
    InstanceTerminal,
    EventConflict
}

internal sealed record EphemeralEventRouteResult(
    EphemeralEventRouteStatus Status,
    InstanceId? InstanceId);

file static class FacadeHandleFactory
{
    internal static WorkflowInstanceHandle Create(
        InstanceId instanceId,
        Func<CancellationToken, ValueTask<global::OrcaCore.WorkflowInstanceSnapshot>> getSnapshot,
        Func<Type, CancellationToken, ValueTask<object?>> getState,
        Func<CancellationToken, ValueTask<WorkflowCancellationRequestStatus>> requestCancellation,
        Func<CancellationToken, ValueTask<WorkflowTerminationStatus>> terminate) =>
        new(instanceId, getSnapshot, getState, requestCancellation, terminate);

    internal static WorkflowInstanceHandle<TOutput> Create<TOutput>(
        InstanceId instanceId,
        Func<CancellationToken, ValueTask<global::OrcaCore.WorkflowInstanceSnapshot>> getSnapshot,
        Func<Type, CancellationToken, ValueTask<object?>> getState,
        Func<CancellationToken, ValueTask<WorkflowCancellationRequestStatus>> requestCancellation,
        Func<CancellationToken, ValueTask<WorkflowTerminationStatus>> terminate,
        Func<CancellationToken, ValueTask<WorkflowOutputResult<TOutput>>> getOutput,
        Func<CancellationToken, ValueTask<TOutput>> waitForOutput) =>
        new(instanceId, getSnapshot, getState, requestCancellation, terminate, getOutput, waitForOutput);
}

internal static class FacadeValueFactory
{
    internal static PayloadFingerprint PayloadFingerprint<T>(T value)
    {
        var bytes = CoreWorkflowValueCodec.Serialize(value, typeof(T));
        return new PayloadFingerprint(Convert.ToHexString(SHA256.HashData(bytes)));
    }

    internal static DefinitionRegistrationConflict RegistrationConflict(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        DefinitionFingerprint existing,
        DefinitionFingerprint attempted) =>
        new DefinitionRegistrationConflict(
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
        new StartIdempotencyConflict(
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
        new WorkflowInstanceNotFoundException(instanceId);

    internal static WorkflowInstanceDefinitionMismatchException InstanceDefinitionMismatch(
        InstanceId instanceId,
        DefinitionId expected,
        DefinitionId actual) =>
        new WorkflowInstanceDefinitionMismatchException(instanceId, expected, actual);

    internal static WorkflowStateTypeMismatchException StateTypeMismatch(
        InstanceId instanceId,
        Type actual,
        Type requested) =>
        new WorkflowStateTypeMismatchException(instanceId, actual, requested);

    internal static WorkflowOutputUnavailableException OutputUnavailable(
        WorkflowInstanceStatus status,
        WorkflowFailure? failure) =>
        new WorkflowOutputUnavailableException(status, failure);

    internal static AmbiguousWaitRegistrationException AmbiguousWait(
        DefinitionId definitionId,
        WorkflowEventContract eventContract,
        CorrelationId correlationId) =>
        new AmbiguousWaitRegistrationException(definitionId, eventContract, correlationId);

    internal static AuthoredLocation RootLocation() =>
        new AuthoredLocation("workflow:$");

}
