using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using OrcaCore.Abstractions.Diagnostics;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Definitions;
using OrcaCore.Core.Execution;
using OrcaCore.Core.Lifecycle;
using OrcaCore.Engine.Ephemeral.Diagnostics;
using OrcaCore.Engine.Ephemeral.Execution;
using OrcaCore.Engine.Ephemeral.Governance;
using OrcaCore.Engine.Ephemeral.Timers;

namespace OrcaCore.Engine.Ephemeral;

/// <summary>
/// Executes registered workflow definitions in the current process without durable recovery.
/// </summary>
internal sealed class EphemeralWorkflowEngine : IDisposable
{
    private readonly ConcurrentDictionary<DefinitionKey, RegisteredDefinition> definitions = [];
    private readonly InstanceExecutionLane executionLane;
    private readonly ResourceGovernanceCoordinator governance;
    private readonly IInstanceRegistry instanceRegistry;
    private readonly EphemeralWorkflowEngineOptions options;
    private readonly EphemeralRoutingIndex routingIndex = new();
    private readonly EphemeralTimerService timerService;
    private readonly TimeProvider timeProvider;
    private readonly YieldContinuationScheduler yieldContinuationScheduler;
    private readonly InterpreterFactory interpreterFactory;
    private readonly IServiceProvider? serviceProvider;

    internal event Action<InstanceId>? InstanceCommitted;

    /// <summary>
    /// Initializes an engine using system time and an in-memory instance registry.
    /// </summary>
    public EphemeralWorkflowEngine()
        : this(TimeProvider.System)
    {
    }

    /// <summary>
    /// Initializes an engine using the supplied time provider and an in-memory instance registry.
    /// </summary>
    public EphemeralWorkflowEngine(TimeProvider timeProvider)
        : this(timeProvider, new EphemeralWorkflowEngineOptions())
    {
    }

    /// <summary>
    /// Initializes an engine using the supplied time provider, options, and an in-memory instance registry.
    /// </summary>
    public EphemeralWorkflowEngine(
        TimeProvider timeProvider,
        EphemeralWorkflowEngineOptions options,
        IServiceProvider? serviceProvider = null)
        : this(timeProvider, options, new InMemoryInstanceRegistry(), CreateExecutionLane(options), serviceProvider)
    {
    }

    /// <summary>
    /// Initializes an engine from the validated public host-governance contract.
    /// </summary>
    public EphemeralWorkflowEngine(
        TimeProvider timeProvider,
        global::OrcaCore.Hosting.EphemeralEngineHostOptions options,
        IServiceProvider? serviceProvider = null)
        : this(
            timeProvider,
            EphemeralWorkflowEngineOptions.FromHostOptions(options),
            serviceProvider)
    {
    }

    internal EphemeralWorkflowEngine(
        TimeProvider timeProvider,
        IInstanceRegistry instanceRegistry,
        InstanceExecutionLane executionLane)
        : this(timeProvider, new EphemeralWorkflowEngineOptions(), instanceRegistry, executionLane, null)
    {
    }

    private EphemeralWorkflowEngine(
        TimeProvider timeProvider,
        EphemeralWorkflowEngineOptions options,
        IInstanceRegistry instanceRegistry,
        InstanceExecutionLane executionLane,
        IServiceProvider? serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(instanceRegistry);
        ArgumentNullException.ThrowIfNull(executionLane);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxPendingEventsPerInstance);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxConsumedEventIdsPerInstance);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxLifecycleEventsPerInstance);

        this.timeProvider = timeProvider;
        this.options = options;
        this.instanceRegistry = instanceRegistry;
        this.executionLane = executionLane;
        this.serviceProvider = serviceProvider;
        governance = new ResourceGovernanceCoordinator(options);
        timerService = new EphemeralTimerService(timeProvider);
        yieldContinuationScheduler = new YieldContinuationScheduler(executionLane);
        interpreterFactory = new InterpreterFactory(
            timeProvider,
            timerService,
            governance,
            yieldContinuationScheduler,
            options,
            serviceProvider);
        timerService.SetDueDispatcher(FireDueTimersCoreAsync);
    }

    private static InstanceExecutionLane CreateExecutionLane(EphemeralWorkflowEngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new InstanceExecutionLane(options.LaneWorkItemEnqueued);
    }

    public void Dispose() => timerService.Dispose();

    internal bool IsTimerDispatching => timerService.IsDispatching;

    /// <summary>
    /// Registers a workflow definition version for later starts.
    /// </summary>
    public void RegisterDefinition<TState>(WorkflowDefinition<TState> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ValidateDefinition(definition);

        var plan = (global::OrcaCore.Core.Compilation.CompiledWorkflowPlan)
            WorkflowDefinitionRuntime.GetPlan(definition);
        var registered = new RegisteredDefinition(
            definition,
            definition.DefinitionVersion,
            typeof(TState),
            plan.Fingerprint);
        definitions.AddOrUpdate(
            new DefinitionKey(definition.DefinitionId, definition.DefinitionVersion),
            registered,
            (_, existing) => SameRegistration(existing, registered)
                ? existing
                : throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralContractAdapter.DefinitionException(
                    $"Workflow definition '{definition.DefinitionId}' is already registered with " +
                    $"version '{existing.DefinitionVersion}', state type '{existing.StateType.FullName}', " +
                    $"and fingerprint '{existing.Fingerprint}'. Candidate version: " +
                    $"'{registered.DefinitionVersion}', candidate fingerprint: '{registered.Fingerprint}'."));
    }

    internal void ValidateDefinition<TState>(WorkflowDefinition<TState> definition)
    {
        var missingTransientPools = PreflightDefinition(definition);
        if (missingTransientPools.Count > 0)
        {
            OrcaCoreEphemeralDiagnostics.RecordHostCompatibilityFailure(
                OrcaCoreDiagnostics.MissingTransientPoolsError);
            throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralContractAdapter.DefinitionException(
                "HostIncompatible.MissingTransientPools: " +
                string.Join(", ", missingTransientPools.Select(pool => pool.Value)));
        }
    }

    internal IReadOnlyList<TransientPoolName> PreflightDefinition<TState>(
        WorkflowDefinition<TState> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var plan = (global::OrcaCore.Core.Compilation.CompiledWorkflowPlan)
            WorkflowDefinitionRuntime.GetPlan(definition);
        if (plan.Mode == global::OrcaCore.Core.Compilation.WorkflowExecutionMode.Durable)
        {
            throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralContractAdapter.DefinitionException(
                $"Workflow definition '{definition.DefinitionId}' contains durable-only nodes " +
                "(RunChild/RunChildren) and cannot be registered on the ephemeral engine.");
        }

        if (definition.Policies.Retry is not null)
        {
            throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralContractAdapter.DefinitionException(
                "Definition-level retry is not supported by the ephemeral engine because replaying the whole " +
                "definition could duplicate completed side effects. Apply retry to individual steps instead.");
        }

        var missingTransientPools = plan.Instructions
            .Select(instruction => instruction.Policy.TransientPoolKey)
            .Where(pool => pool is not null && !options.TransientPools.ContainsKey(pool))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .OrderBy(pool => pool, StringComparer.Ordinal)
            .Select(TransientPoolName.Create)
            .ToArray();
        return missingTransientPools;
    }

    /// <summary>
    /// Starts a registered workflow and runs it inline to suspension or terminal status.
    /// </summary>
    internal async Task<EphemeralWorkflowInstanceSnapshot> StartCoreAsync<TInput, TState>(
        DefinitionId definitionId,
        TInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definitionId);
        var matches = definitions
            .Where(pair => pair.Key.DefinitionId.Equals(definitionId))
            .Select(pair => pair.Key.DefinitionVersion)
            .ToArray();
        if (matches.Length == 0)
        {
            throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralContractAdapter.DefinitionException(
                $"No workflow definition is registered for definition id '{definitionId}'.");
        }

        if (matches.Length > 1)
        {
            throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralContractAdapter.DefinitionException(
                $"Multiple workflow definition versions are registered for definition id '{definitionId}'. " +
                "Start through an exact definition handle.");
        }

        return await StartCoreAsync<TInput, TState>(
            definitionId,
            matches[0],
            input,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Starts an exact registered workflow version for a catalog-resolved definition handle.
    /// </summary>
    internal async Task<EphemeralWorkflowInstanceSnapshot> StartCoreAsync<TInput, TState>(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        TInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definitionId);
        var commandStopwatch = Stopwatch.StartNew();
        using var activity = OrcaCoreEphemeralDiagnostics.StartOperation(OrcaCoreDiagnostics.StartOperation);
        cancellationToken.ThrowIfCancellationRequested();

        if (!definitions.TryGetValue(
                new DefinitionKey(definitionId, definitionVersion),
                out var registeredDefinition))
        {
            throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralContractAdapter.DefinitionException(
                $"No workflow definition is registered for definition id '{definitionId}' " +
                $"and version '{definitionVersion}'.");
        }

        if (registeredDefinition.Definition is not WorkflowDefinition<TState> definition)
        {
            throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralContractAdapter.DefinitionException(
                $"Workflow definition '{definitionId}' was not registered for state type '{typeof(TState).Name}'.");
        }

        var instanceId = InstanceId.Parse(Guid.CreateVersion7().ToString());
        WorkflowInstance<TState>? instance = null;
        var snapshot = await executionLane.RunAsync(
            instanceId,
            async laneCancellationToken =>
            {
                var interpreter = interpreterFactory.Create<TState>();
                instance = await interpreter.RunAsync(
                    definition,
                    input,
                    instanceId,
                    initializedInstance =>
                    {
                        instanceRegistry.Save(initializedInstance);
                        CommitSnapshot(initializedInstance.ToSnapshot());
                    },
                    committed => CommitSnapshot(committed),
                    laneCancellationToken).ConfigureAwait(false);

                return CommitSnapshot(instance.ToSnapshot());
            },
            cancellationToken).ConfigureAwait(false);

        snapshot = instance is null
            ? snapshot
            : await yieldContinuationScheduler
                .DrainAsync(instance, instanceId, committed => CommitSnapshot(committed), cancellationToken)
                .ConfigureAwait(false);
        commandStopwatch.Stop();
        activity?.SetTag(OrcaCoreDiagnostics.InstanceIdKey, snapshot.InstanceId.ToString());
        activity?.SetTag(OrcaCoreDiagnostics.DefinitionIdKey, snapshot.DefinitionId.ToString());
        activity?.SetTag(OrcaCoreDiagnostics.StatusKey, snapshot.Status.ToString());
        OrcaCoreEphemeralDiagnostics.RecordCommand(
            snapshot.DefinitionId,
            OrcaCoreDiagnostics.StartOperation,
            snapshot.Status,
            commandStopwatch.Elapsed);
        OrcaCoreEphemeralDiagnostics.RecordWorkflowStarted(snapshot.DefinitionId);
        return snapshot;
    }

    /// <summary>
    /// Starts a registered workflow and returns the approved detached application snapshot.
    /// </summary>
    public async Task<global::OrcaCore.WorkflowInstanceSnapshot> StartAsync<TInput, TState>(
        DefinitionId definitionId,
        TInput input,
        CancellationToken cancellationToken) =>
        ToApplicationSnapshot(await StartCoreAsync<TInput, TState>(
            definitionId,
            input,
            cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Starts a short-running workflow and returns its approved terminal application snapshot.
    /// </summary>
    public async Task<global::OrcaCore.WorkflowInstanceSnapshot> AwaitCompletionAsync<TInput, TState>(
        DefinitionId definitionId,
        TInput input,
        CancellationToken cancellationToken) =>
        ToApplicationSnapshot(await AwaitCompletionCoreAsync<TInput, TState>(
            definitionId,
            input,
            cancellationToken).ConfigureAwait(false));

    /// <summary>Fires due transient timers and returns approved application snapshots.</summary>
    public async Task<IReadOnlyList<global::OrcaCore.WorkflowInstanceSnapshot>> FireDueTimersAsync(
        CancellationToken cancellationToken) =>
        (await FireDueTimersCoreAsync(cancellationToken).ConfigureAwait(false))
            .Select(ToApplicationSnapshot)
            .ToArray();

    /// <summary>Delivers an event to one instance and returns its approved application snapshot.</summary>
    public async Task<global::OrcaCore.WorkflowInstanceSnapshot> RaiseEventAsync<TState>(
        InstanceId instanceId,
        EventEnvelope envelope,
        CancellationToken cancellationToken) =>
        ToApplicationSnapshot(await RaiseEventCoreAsync<TState>(
            instanceId,
            envelope,
            cancellationToken).ConfigureAwait(false));

    /// <summary>Delivers an event by unique correlation and returns its approved application snapshot.</summary>
    public async Task<global::OrcaCore.WorkflowInstanceSnapshot> RaiseEventByCorrelationAsync<TState>(
        EventEnvelope envelope,
        CancellationToken cancellationToken) =>
        ToApplicationSnapshot(await RaiseEventByCorrelationCoreAsync<TState>(
            envelope,
            cancellationToken).ConfigureAwait(false));

    private global::OrcaCore.WorkflowInstanceSnapshot ToApplicationSnapshot(
        EphemeralWorkflowInstanceSnapshot snapshot)
    {
        if (!definitions.TryGetValue(
                new DefinitionKey(snapshot.DefinitionId, snapshot.DefinitionVersion),
                out var registration))
        {
            throw new InvalidOperationException(
                $"Definition '{snapshot.DefinitionId}' is not registered for snapshot projection.");
        }

        var fingerprint = ConstructNonPublic<global::OrcaCore.DefinitionFingerprint>(registration.Fingerprint);
        var terminal = LifecycleMachine.TerminalStatuses.Contains(snapshot.Status);
        return new global::OrcaCore.WorkflowInstanceSnapshot(
            snapshot.InstanceId,
            global::OrcaCore.WorkflowMode.Ephemeral,
            snapshot.DefinitionId,
            snapshot.DefinitionVersion,
            fingerprint,
            snapshot.Status,
            snapshot.CreatedAt,
            terminal ? snapshot.UpdatedAt : null,
            string.IsNullOrWhiteSpace(snapshot.EndOutcomeName)
                ? null
                : global::OrcaCore.WorkflowOutcomeName.Create(snapshot.EndOutcomeName),
            ToApplicationFailure(snapshot.ErrorSummary),
            snapshot.ActiveWaits.Select(wait => new global::OrcaCore.ActiveWaitSnapshot(
                wait.WaitId,
                FailureProvenance.LocationFromCompilerPath(wait.AuthoredPath),
                wait.EventContract,
                wait.CorrelationId,
                wait.RegisteredAt,
                wait.Deadline)).ToArray());
    }

    private static global::OrcaCore.WorkflowFailure? ToApplicationFailure(string? errorSummary)
    {
        if (string.IsNullOrWhiteSpace(errorSummary))
        {
            return null;
        }

        var separator = errorSummary.IndexOf(':', StringComparison.Ordinal);
        var code = separator > 0 ? errorSummary[..separator] : "WF-RUNTIME-FAILED";
        var message = separator > 0 ? errorSummary[(separator + 1)..].Trim() : errorSummary;
        return global::OrcaCore.Engine.Ephemeral.Internal.EphemeralContractAdapter.WorkflowFailure(
            code,
            message,
            global::OrcaCore.Engine.Ephemeral.Internal.EphemeralContractAdapter.AuthoredLocation("workflow:$"),
            global::OrcaCore.Engine.Ephemeral.Internal.EphemeralContractAdapter.RootFailureOccurrence(),
            []);
    }

    private static T ConstructNonPublic<T>(params object?[] arguments) =>
        (T)typeof(T)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(constructor => constructor.GetParameters().Length == arguments.Length)
            .Invoke(arguments);

    private static bool SameRegistration(
        RegisteredDefinition existing,
        RegisteredDefinition candidate)
    {
        return existing.DefinitionVersion == candidate.DefinitionVersion &&
            existing.StateType == candidate.StateType &&
            string.Equals(existing.Fingerprint, candidate.Fingerprint, StringComparison.Ordinal);
    }

    private sealed record RegisteredDefinition(
        object Definition,
        DefinitionVersion DefinitionVersion,
        Type StateType,
        string Fingerprint);

    private readonly record struct DefinitionKey(
        DefinitionId DefinitionId,
        DefinitionVersion DefinitionVersion);

    internal DateTimeOffset GetUtcNow()
    {
        return timeProvider.GetUtcNow();
    }

    /// <summary>
    /// Starts a short-running workflow and returns its terminal snapshot.
    /// </summary>
    internal async Task<EphemeralWorkflowInstanceSnapshot> AwaitCompletionCoreAsync<TInput, TState>(
        DefinitionId definitionId,
        TInput input,
        CancellationToken cancellationToken)
    {
        var snapshot = await StartCoreAsync<TInput, TState>(
            definitionId,
            input,
            cancellationToken).ConfigureAwait(false);

        if (!LifecycleMachine.TerminalStatuses.Contains(snapshot.Status))
        {
            throw new WorkflowLifecycleException(
                $"Workflow instance '{snapshot.InstanceId}' did not reach a terminal status synchronously.");
        }

        return snapshot;
    }

    /// <summary>
    /// Fires all transient timers whose due time has passed in this process.
    /// </summary>
    internal async Task<IReadOnlyList<EphemeralWorkflowInstanceSnapshot>> FireDueTimersCoreAsync(
        CancellationToken cancellationToken)
    {
        using var activity = OrcaCoreEphemeralDiagnostics.StartOperation(OrcaCoreDiagnostics.FireDueTimersOperation);
        cancellationToken.ThrowIfCancellationRequested();

        var dueTimers = timerService.ClaimDueTimers();
        if (dueTimers.Count == 0)
        {
            return [];
        }

        var snapshots = new List<EphemeralWorkflowInstanceSnapshot>(dueTimers.Count);
        for (var index = 0; index < dueTimers.Count; index++)
        {
            var timer = dueTimers[index];
            var commandStopwatch = Stopwatch.StartNew();
            try
            {
                instanceRegistry.TryGet(timer.InstanceId, out var registeredInstance);
                var snapshot = await executionLane.RunAsync(
                        timer.InstanceId,
                        async laneCancellationToken =>
                        {
                            if (registeredInstance is IWorkflowInstance instanceForToken)
                            {
                                using var linkedCancellation = instanceForToken.CreateLinkedExecutionToken(laneCancellationToken);
                                return CommitSnapshot(await timer.FireAsync(linkedCancellation.Token).ConfigureAwait(false));
                            }

                            return CommitSnapshot(await timer.FireAsync(laneCancellationToken).ConfigureAwait(false));
                        },
                        cancellationToken).ConfigureAwait(false);

                if (registeredInstance is IWorkflowInstance instance)
                {
                    snapshot = await yieldContinuationScheduler
                        .DrainAsync(instance, timer.InstanceId, committed => CommitSnapshot(committed), cancellationToken)
                        .ConfigureAwait(false);
                }

                snapshots.Add(snapshot);
                commandStopwatch.Stop();
                OrcaCoreEphemeralDiagnostics.RecordCommand(
                    snapshot.DefinitionId,
                    OrcaCoreDiagnostics.FireDueTimersOperation,
                    snapshot.Status,
                    commandStopwatch.Elapsed);
                timerService.Complete(timer);
            }
            catch
            {
                for (var releaseIndex = index; releaseIndex < dueTimers.Count; releaseIndex++)
                {
                    timerService.Release(dueTimers[releaseIndex]);
                }

                throw;
            }
        }

        return snapshots;
    }

    /// <summary>
    /// Delivers an event directly to one known instance and resumes it when an active wait matches.
    /// </summary>
    internal async Task<EphemeralWorkflowInstanceSnapshot> RaiseEventCoreAsync<TState>(
        InstanceId instanceId,
        EventEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var commandStopwatch = Stopwatch.StartNew();
        using var activity = OrcaCoreEphemeralDiagnostics.StartOperation(OrcaCoreDiagnostics.RaiseEventOperation);
        ArgumentNullException.ThrowIfNull(envelope);
        ValidateEventEnvelope(envelope);
        cancellationToken.ThrowIfCancellationRequested();

        if (!instanceRegistry.TryGet(instanceId, out var registeredInstance))
        {
            throw new WorkflowRoutingException(
                $"No workflow instance exists for instance id '{instanceId}'.");
        }

        if (registeredInstance is not WorkflowInstance<TState> instance)
        {
            throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralContractAdapter.DefinitionException(
                $"Workflow instance '{instanceId}' is not using state type '{typeof(TState).Name}'.");
        }

        var matchedWait = instance.ToSnapshot().ActiveWaits.FirstOrDefault(wait =>
            wait.EventContract.Equals(envelope.EventContract) &&
            wait.CorrelationId.Equals(envelope.CorrelationId));

        var snapshot = await executionLane.RunAsync(
                instanceId,
                async laneCancellationToken =>
                {
                    using var linkedCancellation = instance.CreateLinkedExecutionToken(laneCancellationToken);
                    return CommitSnapshot(await instance
                        .RaiseEventAsync(envelope, timeProvider.GetUtcNow(), linkedCancellation.Token)
                        .ConfigureAwait(false));
                },
                cancellationToken).ConfigureAwait(false);

        snapshot = await yieldContinuationScheduler
            .DrainAsync(instance, instanceId, committed => CommitSnapshot(committed), cancellationToken)
            .ConfigureAwait(false);
        commandStopwatch.Stop();
        activity?.SetTag(OrcaCoreDiagnostics.InstanceIdKey, snapshot.InstanceId.ToString());
        activity?.SetTag(OrcaCoreDiagnostics.DefinitionIdKey, snapshot.DefinitionId.ToString());
        activity?.SetTag(OrcaCoreDiagnostics.StatusKey, snapshot.Status.ToString());
        activity?.SetTag(OrcaCoreDiagnostics.EventTypeKey, envelope.EventContract.EventName.Value);
        OrcaCoreEphemeralDiagnostics.RecordCommand(
            snapshot.DefinitionId,
            OrcaCoreDiagnostics.RaiseEventOperation,
            snapshot.Status,
            commandStopwatch.Elapsed);
        OrcaCoreEphemeralDiagnostics.RecordEventDelivered(
            snapshot.DefinitionId,
            envelope.EventContract.EventName,
            snapshot.Status);
        if (matchedWait is not null && snapshot.ActiveWaits.All(wait => !wait.WaitId.Equals(matchedWait.WaitId)))
        {
            OrcaCoreEphemeralDiagnostics.RecordWaitMatched(
                snapshot.DefinitionId,
                matchedWait.EventContract.EventName,
                timeProvider.GetUtcNow() - matchedWait.RegisteredAt);
        }

        return snapshot;
    }

    internal async Task<global::OrcaCore.WorkflowCancellationRequestStatus> RequestCancellationAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        var commandStopwatch = Stopwatch.StartNew();
        cancellationToken.ThrowIfCancellationRequested();

        if (!instanceRegistry.TryGet(instanceId, out var registeredInstance) ||
            registeredInstance is not IWorkflowInstance instance)
        {
            throw new WorkflowRoutingException(
                $"No workflow instance exists for instance id '{instanceId}'.");
        }

        var disposition = instance.TryRequestCancellation(timeProvider.GetUtcNow());
        if (disposition != global::OrcaCore.WorkflowCancellationRequestStatus.Requested)
        {
            return disposition;
        }

        // The cancellation request is the command's commit point. Once user code has
        // observed it, finish the terminal transition even if the caller disconnects.
        var snapshot = await executionLane.RunAsync(
                instanceId,
                _ => Task.FromResult(CommitSnapshot(instance.Cancel(timeProvider.GetUtcNow()))),
                CancellationToken.None).ConfigureAwait(false);
        commandStopwatch.Stop();
        OrcaCoreEphemeralDiagnostics.RecordCommand(
            snapshot.DefinitionId,
            OrcaCoreDiagnostics.RequestCancellationOperation,
            snapshot.Status,
            commandStopwatch.Elapsed);
        OrcaCoreEphemeralDiagnostics.RecordTerminalLifecycle(snapshot.DefinitionId, snapshot.Status);
        return disposition;
    }

    internal async Task<EphemeralWorkflowInstanceSnapshot> CancelInstanceAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        _ = await RequestCancellationAsync(instanceId, cancellationToken).ConfigureAwait(false);
        return instanceRegistry.TryGet(instanceId, out var registeredInstance) &&
               registeredInstance is IWorkflowInstance instance
            ? instance.GetPublishedSnapshot()
            : throw new WorkflowRoutingException(
                $"No workflow instance exists for instance id '{instanceId}'.");
    }

    internal async Task<global::OrcaCore.WorkflowTerminationStatus> RequestTerminationAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        var commandStopwatch = Stopwatch.StartNew();
        cancellationToken.ThrowIfCancellationRequested();

        if (!instanceRegistry.TryGet(instanceId, out var registeredInstance) ||
            registeredInstance is not IWorkflowInstance instance)
        {
            throw new WorkflowRoutingException(
                $"No workflow instance exists for instance id '{instanceId}'.");
        }

        instance.SignalCancellation();
        var result = await executionLane.RunAsync(
                instanceId,
                _ =>
                {
                    var current = instance.ToSnapshot();
                    if (LifecycleMachine.TerminalStatuses.Contains(current.Status))
                    {
                        return Task.FromResult((
                            Status: global::OrcaCore.WorkflowTerminationStatus.AlreadyTerminal,
                            Snapshot: current));
                    }

                    return Task.FromResult((
                        Status: global::OrcaCore.WorkflowTerminationStatus.Terminated,
                        Snapshot: CommitSnapshot(instance.Terminate(timeProvider.GetUtcNow()))));
                },
                CancellationToken.None).ConfigureAwait(false);
        commandStopwatch.Stop();
        OrcaCoreEphemeralDiagnostics.RecordCommand(
            result.Snapshot.DefinitionId,
            OrcaCoreDiagnostics.RequestTerminationOperation,
            result.Snapshot.Status,
            commandStopwatch.Elapsed);
        OrcaCoreEphemeralDiagnostics.RecordTerminalLifecycle(
            result.Snapshot.DefinitionId,
            result.Snapshot.Status);
        return result.Status;
    }

    internal async Task<EphemeralWorkflowInstanceSnapshot> TerminateInstanceAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        _ = await RequestTerminationAsync(instanceId, cancellationToken).ConfigureAwait(false);
        return instanceRegistry.TryGet(instanceId, out var registeredInstance) &&
               registeredInstance is IWorkflowInstance instance
            ? instance.GetPublishedSnapshot()
            : throw new WorkflowRoutingException(
                $"No workflow instance exists for instance id '{instanceId}'.");
    }

    /// <summary>
    /// Removes one terminal instance and its saga runtime state from process memory.
    /// Returns false when the instance is unknown; throws when it is still active.
    /// </summary>
    internal bool EvictInstance(InstanceId instanceId)
    {
        if (!instanceRegistry.TryGet(instanceId, out var registeredInstance) ||
            registeredInstance is not IWorkflowInstance instance)
        {
            return false;
        }

        var snapshot = instance.ToSnapshot();
        if (!LifecycleMachine.TerminalStatuses.Contains(snapshot.Status))
        {
            throw new WorkflowLifecycleException(
                $"Workflow instance '{instanceId}' is '{snapshot.Status}' and cannot be evicted before " +
                "reaching a terminal status.");
        }

        var removed = instanceRegistry.Remove(instanceId);
        IndexSnapshot(snapshot);
        OrcaCoreEphemeralDiagnostics.RefreshStatistics(CurrentSnapshots());
        return removed;
    }

    /// <summary>
    /// Removes every terminal instance from process memory.
    /// </summary>
    internal int EvictTerminalInstances()
    {
        var purged = 0;
        foreach (var registered in instanceRegistry.List())
        {
            if (registered is not IWorkflowInstance instance)
            {
                continue;
            }

            var snapshot = instance.ToSnapshot();
            if (!LifecycleMachine.TerminalStatuses.Contains(snapshot.Status))
            {
                continue;
            }

            if (instanceRegistry.Remove(instance.InstanceId))
            {
                purged++;
            }

            IndexSnapshot(snapshot);
        }

        OrcaCoreEphemeralDiagnostics.RefreshStatistics(CurrentSnapshots());
        return purged;
    }

    internal EphemeralOperatorStatistics GetOperatorStatistics() =>
        OrcaCoreEphemeralDiagnostics.CaptureStatistics(CurrentSnapshots());

    private static WorkflowErrorDetails ToWorkflowError(Exception exception, string stepPath, DateTimeOffset occurredAt)
    {
        return new WorkflowErrorDetails(
            exception.GetType().Name,
            exception.Message,
            stepPath,
            occurredAt);
    }

    /// <summary>
    /// Resolves one active wait by event name and correlation, then delivers the event to it.
    /// </summary>
    internal async Task<EphemeralWorkflowInstanceSnapshot> RaiseEventByCorrelationCoreAsync<TState>(
        EventEnvelope envelope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ValidateEventEnvelope(envelope);
        cancellationToken.ThrowIfCancellationRequested();

        var candidateIds = routingIndex.FindCandidates(envelope);
        if (candidateIds.Count == 0)
        {
            throw new WorkflowRoutingException(
                $"No active wait exists for event '{envelope.EventContract.EventName}' version " +
                $"'{envelope.EventContract.Version}' and correlation '{envelope.CorrelationId}'.");
        }

        var matches = instanceRegistry.GetMany(candidateIds)
            .OfType<WorkflowInstance<TState>>()
            .Where(instance => instance.HasActiveWait(envelope))
            .ToArray();

        if (matches.Length == 0)
        {
            throw new WorkflowRoutingException(
                $"No active wait exists for event '{envelope.EventContract.EventName}' version " +
                $"'{envelope.EventContract.Version}' and correlation '{envelope.CorrelationId}'.");
        }

        if (matches.Length > 1)
        {
            throw new WorkflowRoutingException(
                $"Correlation-targeted delivery for event '{envelope.EventContract.EventName}' version " +
                $"'{envelope.EventContract.Version}' and correlation " +
                $"'{envelope.CorrelationId}' is ambiguous; the correlation route requires exactly one active wait.");
        }

        return await RaiseEventCoreAsync<TState>(
            matches[0].InstanceId,
            envelope,
            cancellationToken).ConfigureAwait(false);
    }

    private void IndexSnapshot(EphemeralWorkflowInstanceSnapshot snapshot)
    {
        routingIndex.IndexSnapshot(snapshot);
    }

    private EphemeralWorkflowInstanceSnapshot CommitSnapshot(EphemeralWorkflowInstanceSnapshot snapshot)
    {
        if (instanceRegistry.TryGet(snapshot.InstanceId, out var registered) &&
            registered is IWorkflowInstance instance)
        {
            instance.PublishState();
        }

        IndexSnapshot(snapshot);
        OrcaCoreEphemeralDiagnostics.RefreshStatistics(CurrentSnapshots());
        InstanceCommitted?.Invoke(snapshot.InstanceId);
        return snapshot;
    }

    private EphemeralWorkflowInstanceSnapshot[] CurrentSnapshots() =>
        instanceRegistry.List()
            .OfType<IWorkflowInstance>()
            .Select(instance => instance.GetPublishedSnapshot())
            .ToArray();

    internal bool TryGetFacadeInstance(
        InstanceId instanceId,
        out IWorkflowInstance? instance)
    {
        if (instanceRegistry.TryGet(instanceId, out var registered) &&
            registered is IWorkflowInstance workflowInstance)
        {
            instance = workflowInstance;
            return true;
        }

        instance = null;
        return false;
    }

    private static void ValidateEventEnvelope(EventEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope.EventId);

        if (string.IsNullOrWhiteSpace(envelope.EventContract.EventName.Value))
        {
            throw new ArgumentException("EventName must not be empty.", nameof(envelope));
        }

        if (string.IsNullOrWhiteSpace(envelope.CorrelationId.Value))
        {
            throw new ArgumentException("CorrelationId must not be empty.", nameof(envelope));
        }

        if (envelope.OccurredAt == default)
        {
            throw new ArgumentException("OccurredAt must not be the default value.", nameof(envelope));
        }

    }

}
