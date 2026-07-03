using System.Collections.Concurrent;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Core.Definitions;
using OrcaCore.Core.Lifecycle;
using OrcaCore.Engine.Ephemeral.Execution;
using OrcaCore.Engine.Ephemeral.Management;

namespace OrcaCore.Engine.Ephemeral;

/// <summary>
/// The minimal public ephemeral engine facade (CR-016, CR-021): registers definitions and
/// starts instances, running each straight-line body inline to its first suspension or
/// terminal and returning a metadata-only snapshot. No live instance type is ever public.
/// </summary>
public sealed class EphemeralWorkflowEngine
{
    private readonly ConcurrentDictionary<DefinitionId, object> definitions = new();
    private readonly IInstanceRegistry instanceRegistry;
    private readonly InstanceExecutionLane executionLane;
    private readonly TimeProvider timeProvider;

    /// <summary>
    /// EV-011 correlation index over active waits, engine-owned (spans instances, so it
    /// cannot live on <see cref="WorkflowInstance{TState}"/>). Guarded by <see cref="indexLock"/>
    /// since routing calls for different instances run concurrently on independent lanes.
    /// </summary>
    private readonly CorrelationIndex correlationIndex = new();

    /// <summary>Definition-targeted fanout scoping (EV-010): which instances belong to which definition.</summary>
    private readonly ConcurrentDictionary<DefinitionId, ConcurrentDictionary<InstanceId, byte>> instancesByDefinition = new();

    /// <summary>
    /// Per-instance cooperative-cancellation source (CR-031 Cancel): linked into the token
    /// business steps observe (<see cref="Interpreter{TState}"/>'s <c>ExecuteAsync</c> calls) so a
    /// concurrent <see cref="CancelAsync{TState}"/> call signals an in-flight step immediately,
    /// without waiting for the instance's execution lane turn. Created when an instance starts,
    /// disposed once the instance reaches a terminal status.
    /// </summary>
    private readonly ConcurrentDictionary<InstanceId, CancellationTokenSource> instanceCancellations = new();

    /// <summary>
    /// Per-instance completion bridge (CR-016): lazily created by
    /// <see cref="AwaitCompletionAsync{TState}"/> for an instance that is not yet terminal, and
    /// completed by <see cref="CompleteAwaitersIfTerminal{TState}"/> the moment any engine-driven
    /// mutation lands the instance on a terminal <see cref="WorkflowStatus"/>.
    /// </summary>
    private readonly ConcurrentDictionary<InstanceId, TaskCompletionSource<WorkflowInstanceSnapshot>> completionWaiters = new();

    private readonly Lock indexLock = new();

    public EphemeralWorkflowEngine()
        : this(TimeProvider.System)
    {
    }

    public EphemeralWorkflowEngine(TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider;
        instanceRegistry = new InstanceRegistry();
        executionLane = new InstanceExecutionLane();
    }

    /// <summary>Registers a built, validated definition (CR-002) for later starts.</summary>
    public void RegisterDefinition<TState>(WorkflowDefinition<TState> definition) =>
        definitions[definition.DefinitionId] = definition;

    /// <summary>
    /// The management query entry point (MG-001/MG-010, T1-13): scope selection
    /// (<c>All()</c>/<c>ForDefinition(id)</c>/<c>Instance(id)</c>), constrained filtering
    /// (<c>Where(...)</c>, MG-002), and terminal snapshot/statistics queries (MG-005 - every
    /// result is an immutable snapshot or copy, never a live runtime object). T1-14 adds bulk
    /// <c>Terminate(...)</c> (MG-004) over the selected scope, routed back through
    /// <see cref="TerminateAsync{TState}"/> per instance so every instance still commits through
    /// its own execution lane (CR-040).
    /// </summary>
    public ManagementQueryRoot Query() => new(instanceRegistry, TerminateUntypedAsync);

    /// <summary>
    /// Starts a new instance of the definition identified by <paramref name="definitionId"/>,
    /// converting <paramref name="input"/> to initial business state via the definition's
    /// <c>Init</c> step (CR-005), then runs the straight-line body inline to its first
    /// suspension or terminal (CR-016) and returns a metadata-only snapshot (CR-021).
    /// </summary>
    public async Task<WorkflowInstanceSnapshot> StartAsync<TInput, TState>(
        DefinitionId definitionId,
        TInput input,
        CancellationToken cancellationToken)
    {
        if (!definitions.TryGetValue(definitionId, out var untypedDefinition) ||
            untypedDefinition is not WorkflowDefinition<TState> definition)
        {
            throw new WorkflowDefinitionException(
                $"No definition registered for '{definitionId}'. Call {nameof(RegisterDefinition)} before {nameof(StartAsync)}.");
        }

        if (definition.Root.Steps.Count == 0 || definition.Root.Steps[0] is not InitNode<TState, TInput> initNode)
        {
            throw new WorkflowDefinitionException(
                $"Definition '{definitionId}' does not start with an Init step accepting '{typeof(TInput).Name}'.");
        }

        var createdAt = timeProvider.GetUtcNow();
        var state = initNode.CreateState(input);
        var instance = new WorkflowInstance<TState>(InstanceId.New(), definitionId, definition.DefinitionVersion, state, createdAt);

        instanceRegistry.Add(instance);
        instancesByDefinition.GetOrAdd(definitionId, static _ => new ConcurrentDictionary<InstanceId, byte>())[instance.InstanceId] = 0;
        var instanceCancellation = instanceCancellations.GetOrAdd(instance.InstanceId, static _ => new CancellationTokenSource());

        var interpreter = new Interpreter<TState>();
        await RunUntilNotYieldingAsync(
            instance,
            async ct =>
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, instanceCancellation.Token);
                await interpreter.RunAsync(instance, definition, timeProvider, linkedCts.Token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        SyncCorrelationIndex(instance);
        CompleteAwaitersIfTerminal(instance);

        return ToSnapshot(instance);
    }

    /// <summary>
    /// CR-017: repeatedly issues SEPARATE <see cref="InstanceExecutionLane.RunAsync"/> calls for
    /// <paramref name="instance"/> — each cycle of "interpret until yield-or-suspend" is its own
    /// lane acquisition/release — as long as the interpreter keeps stopping because of a
    /// cooperative <see cref="StepResult.Yield"/> (<see cref="WorkflowInstance{TState}.YieldPending"/>).
    /// This is what "release the instance's execution lane" (CR-017) means for a synchronous,
    /// single-caller ephemeral engine with no background scheduler: the lane genuinely becomes
    /// available to any other concurrent caller for this instance between continuations, then this
    /// same logical operation reacquires it to continue. Stops once the interpreter halts for a
    /// real reason — terminal, a genuine <c>Wait</c> suspension, or (mid-loop only) a failure.
    /// </summary>
    private async ValueTask RunUntilNotYieldingAsync<TState>(
        WorkflowInstance<TState> instance,
        Func<CancellationToken, ValueTask> runOnce,
        CancellationToken cancellationToken)
    {
        do
        {
            instance.YieldPending = false;

            await executionLane.RunAsync(
                instance.InstanceId,
                () => runOnce(cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }
        while (instance.YieldPending);
    }

    /// <summary>
    /// Delivers <paramref name="envelope"/> to the instance identified by <paramref name="instanceId"/>
    /// (instance-targeted only — correlation-index fanout is T1-10). Routes through the T1-06
    /// execution lane so concurrent deliveries for the same instance serialize: only one caller
    /// observes <see cref="RaiseEventOutcome.Resumed"/> for a given wait (EV-023 exactly-once).
    /// Returns <see cref="RaiseEventOutcome.NoMatch"/> rather than throwing when the event does
    /// not match the instance's active wait (EV matching rule) — matching is a routine outcome.
    /// </summary>
    public async Task<RaiseEventOutcome> RaiseEventAsync<TState>(
        InstanceId instanceId,
        EventEnvelope envelope,
        CancellationToken cancellationToken)
    {
        var instance = instanceRegistry.TryGet<TState>(instanceId);
        if (instance is null)
        {
            return RaiseEventOutcome.InstanceNotFound;
        }

        if (!definitions.TryGetValue(instance.DefinitionId, out var untypedDefinition) ||
            untypedDefinition is not WorkflowDefinition<TState> definition)
        {
            throw new WorkflowDefinitionException(
                $"No definition registered for '{instance.DefinitionId}'. The instance cannot be resumed.");
        }

        var outcome = RaiseEventOutcome.InstanceNotFound;
        var isFirstAttempt = true;

        await RunUntilNotYieldingAsync(
            instance,
            async ct =>
            {
                var instanceCancellation = instanceCancellations.GetOrAdd(instanceId, static _ => new CancellationTokenSource());
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, instanceCancellation.Token);

                var interpreter = new Interpreter<TState>();
                if (isFirstAttempt)
                {
                    // Only the first cycle matches/consumes the delivered event (EV-023
                    // exactly-once). CR-017 continuation cycles after this one are draining
                    // further yields of the same step the resume landed on - the event has
                    // already done its job (TryResumeAsync -> ResumeWaitAsync -> RunLoopAsync).
                    isFirstAttempt = false;
                    outcome = await interpreter.TryResumeAsync(instance, definition, envelope, timeProvider, linkedCts.Token)
                        .ConfigureAwait(false);
                }
                else
                {
                    await interpreter.RunAsync(instance, definition, timeProvider, linkedCts.Token).ConfigureAwait(false);
                }

                SyncCorrelationIndex(instance);
                CompleteAwaitersIfTerminal(instance);
            },
            cancellationToken).ConfigureAwait(false);

        return outcome;
    }

    /// <summary>
    /// Correlation-targeted delivery (EV-010 mode 2): resolves the target instance via the
    /// EV-011 correlation index by <paramref name="envelope"/>'s <c>EventName</c>/
    /// <c>CorrelationId</c> and delivers to it through the same instance-targeted path as
    /// <see cref="RaiseEventAsync{TState}"/>, so it still routes through that instance's
    /// execution lane. EV-012: throws <see cref="WorkflowRoutingException"/> when zero or more
    /// than one instance currently has a matching active wait — uniqueness is checked here, at
    /// routing time, never at wait-registration time.
    /// </summary>
    public async Task<RaiseEventOutcome> RaiseByCorrelationAsync<TState>(
        EventEnvelope envelope,
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<InstanceId> matches;
        lock (indexLock)
        {
            matches = correlationIndex.Resolve(envelope.EventName, envelope.CorrelationId);
        }

        switch (matches.Count)
        {
            case 0:
                throw new WorkflowRoutingException(
                    $"No active wait matches event '{envelope.EventName}' with correlation '{envelope.CorrelationId}'. " +
                    "Deliver later once a matching Wait is registered, or use instance-targeted routing if the target is known.");

            case > 1:
                throw new WorkflowRoutingException(
                    $"{matches.Count} instances have an active wait matching event '{envelope.EventName}' with " +
                    $"correlation '{envelope.CorrelationId}'. Correlation-targeted delivery requires exactly one match " +
                    "(EV-012) - use instance-targeted or definition-targeted fanout routing instead.");
        }

        var targetInstanceId = matches.Single();
        return await RaiseEventAsync<TState>(targetInstanceId, envelope, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Definition-targeted fanout delivery (EV-010 mode 3): delivers <paramref name="envelope"/>
    /// to every currently-registered instance of <paramref name="definitionId"/> and only that
    /// definition - never other definitions, never engine-wide. Each targeted instance gets its
    /// own execution-lane call (never one lane call spanning several instances), run
    /// independently so one instance's outcome cannot block another's.
    /// </summary>
    public async Task<IReadOnlyDictionary<InstanceId, RaiseEventOutcome>> RaiseByDefinitionAsync<TState>(
        DefinitionId definitionId,
        EventEnvelope envelope,
        CancellationToken cancellationToken)
    {
        if (!instancesByDefinition.TryGetValue(definitionId, out var targetInstanceIds) || targetInstanceIds.IsEmpty)
        {
            return new Dictionary<InstanceId, RaiseEventOutcome>();
        }

        var targets = targetInstanceIds.Keys.ToArray();
        var outcomes = await Task.WhenAll(
            targets.Select(async instanceId =>
            {
                var outcome = await RaiseEventAsync<TState>(instanceId, envelope, cancellationToken).ConfigureAwait(false);
                return (InstanceId: instanceId, Outcome: outcome);
            })).ConfigureAwait(false);

        return outcomes.ToDictionary(pair => pair.InstanceId, pair => pair.Outcome);
    }

    /// <summary>
    /// The completion bridge (CR-016): awaits <paramref name="instanceId"/> reaching a terminal
    /// <see cref="WorkflowStatus"/> and returns its terminal snapshot, without exposing any live
    /// internal state. If the instance is already terminal, returns immediately. Otherwise waits
    /// on a per-instance completion signal that any later engine-driven mutation for this instance
    /// (a resume, <see cref="CancelAsync{TState}"/>, or <see cref="TerminateAsync{TState}"/>,
    /// possibly from a different caller) completes once that mutation lands the instance on a
    /// terminal status. Cancelling <paramref name="cancellationToken"/> cancels only this caller's
    /// wait (<see cref="Task.WaitAsync(CancellationToken)"/> never affects other awaiters of the
    /// same underlying instance completion).
    /// </summary>
    public async Task<WorkflowInstanceSnapshot> AwaitCompletionAsync<TState>(
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        var current = instanceRegistry.TryGetUntyped(instanceId);
        if (current is not null && LifecycleMachine.TerminalStatuses.Contains(current.Status))
        {
            return ToSnapshot(current);
        }

        var tcs = completionWaiters.GetOrAdd(
            instanceId, static _ => new TaskCompletionSource<WorkflowInstanceSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously));

        // Re-check after registering the waiter: the instance may have reached terminal status
        // between the lookup above and GetOrAdd (a concurrent mutation's own completion signal
        // could have already fired and removed the entry) - if so, fall back to the direct read
        // instead of waiting on a TCS nothing will ever complete again.
        var recheck = instanceRegistry.TryGetUntyped(instanceId);
        if (recheck is not null && LifecycleMachine.TerminalStatuses.Contains(recheck.Status))
        {
            return ToSnapshot(recheck);
        }

        return await tcs.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Graceful cancel (CR-031): signals the instance's cooperative <see cref="CancellationTokenSource"/>
    /// immediately (thread-safe, does not wait for the execution lane), so a genuinely in-flight
    /// step's <c>ExecuteAsync</c> observes cancellation as soon as it next checks its token. Then,
    /// through the execution lane, fires <see cref="LifecycleTrigger.Cancel"/> (CR-030) and marks
    /// every active wait <see cref="WaitStatus.Cancelled"/> before the instance commits
    /// <see cref="WorkflowStatus.Cancelled"/>. Throws <see cref="WorkflowLifecycleException"/> if
    /// the instance is already terminal (CR-030/AC-005) or not found. <typeparamref name="TState"/>
    /// is not otherwise needed (a terminal command never touches business state) but is kept on
    /// this overload to match <see cref="StartAsync{TInput,TState}"/>/<see cref="RaiseEventAsync{TState}"/>'s
    /// generic facade shape.
    /// </summary>
    public Task<WorkflowInstanceSnapshot> CancelAsync<TState>(InstanceId instanceId, CancellationToken cancellationToken) =>
        ApplyTerminalTriggerAsync(instanceId, LifecycleTrigger.Cancel, cancellationToken);

    /// <summary>
    /// Forced terminate (CR-031): no cooperative wait for in-flight work - through the execution
    /// lane, fires <see cref="LifecycleTrigger.Terminate"/> immediately, forcibly marks every
    /// active wait <see cref="WaitStatus.Cancelled"/> (the natural terminal wait status), and
    /// commits <see cref="WorkflowStatus.Terminated"/>. No policies or compensation run. Throws
    /// <see cref="WorkflowLifecycleException"/> if the instance is already terminal (CR-030/AC-005)
    /// or not found.
    /// </summary>
    public Task<WorkflowInstanceSnapshot> TerminateAsync<TState>(InstanceId instanceId, CancellationToken cancellationToken) =>
        ApplyTerminalTriggerAsync(instanceId, LifecycleTrigger.Terminate, cancellationToken);

    /// <summary>
    /// Non-generic terminate entry point for the management surface's bulk
    /// <c>ManagementQueryScope.Terminate(...)</c> (MG-004, T1-14): reuses
    /// <see cref="ApplyTerminalTriggerAsync"/> directly, since a terminal command never touches
    /// business state and so needs no <c>TState</c> at any call site — <see cref="ManagementQueryScope"/>
    /// only ever knows instances as <see cref="IWorkflowInstance"/>.
    /// </summary>
    private async Task<TerminalCommandOutcome> TerminateUntypedAsync(InstanceId instanceId, CancellationToken cancellationToken)
    {
        var snapshot = await ApplyTerminalTriggerAsync(instanceId, LifecycleTrigger.Terminate, cancellationToken).ConfigureAwait(false);
        return new TerminalCommandOutcome(snapshot.InstanceId, snapshot.Status);
    }

    /// <summary>
    /// Shared, state-agnostic Cancel/Terminate implementation (CR-031), reached by both the
    /// generic <see cref="CancelAsync{TState}"/>/<see cref="TerminateAsync{TState}"/> facade
    /// overloads and the untyped management bulk-terminate path — a terminal command mutates only
    /// engine-owned lifecycle/wait state, never business state, so it needs no <c>TState</c> at
    /// all. Cancel additionally signals the instance-scoped cooperative
    /// <see cref="CancellationTokenSource"/> immediately, before entering the lane (CR-031's
    /// cooperative-stop requirement, so a genuinely in-flight step observes it as soon as it next
    /// checks its token); Terminate does not (forced, no cooperative wait). Throws
    /// <see cref="WorkflowLifecycleException"/> if the instance is already terminal (CR-030/AC-005)
    /// or not found.
    /// </summary>
    private async Task<WorkflowInstanceSnapshot> ApplyTerminalTriggerAsync(
        InstanceId instanceId,
        LifecycleTrigger trigger,
        CancellationToken cancellationToken)
    {
        if (trigger == LifecycleTrigger.Cancel && instanceCancellations.TryGetValue(instanceId, out var cooperativeSource))
        {
            cooperativeSource.Cancel();
        }

        WorkflowInstanceSnapshot? snapshot = null;
        WorkflowLifecycleException? failure = null;

        await executionLane.RunAsync(
            instanceId,
            () =>
            {
                var instance = instanceRegistry.TryGetUntyped(instanceId);
                if (instance is null)
                {
                    failure = new WorkflowLifecycleException(
                        $"No instance registered for '{instanceId}'. It may already have been removed or never started.");
                    return ValueTask.CompletedTask;
                }

                try
                {
                    instance.ApplyTerminalTrigger(trigger, timeProvider);
                }
                catch (WorkflowLifecycleException exception)
                {
                    failure = exception;
                    return ValueTask.CompletedTask;
                }

                SyncCorrelationIndex(instance);
                CompleteAwaitersIfTerminal(instance);
                snapshot = ToSnapshot(instance);
                return ValueTask.CompletedTask;
            },
            cancellationToken).ConfigureAwait(false);

        return snapshot ?? throw failure!;
    }

    /// <summary>
    /// Reconciles the EV-011 correlation index for one instance against its current runtime
    /// state after a lane-guarded mutation (start, resume, cancel/terminate). Removes any stale
    /// entry first, then re-registers if the instance is <c>Waiting</c> with a still-<c>Active</c>
    /// wait — covers registration, match/resume, and terminal cleanup in one place, since all
    /// three collapse to "what does this instance's active wait look like right now". The
    /// state-agnostic <see cref="IWorkflowInstance"/> surface is enough here — active-wait lookup
    /// never needs <c>TState</c>.
    /// </summary>
    private void SyncCorrelationIndex(IWorkflowInstance instance)
    {
        lock (indexLock)
        {
            correlationIndex.Remove(instance.InstanceId);

            if (instance.Status == WorkflowStatus.Waiting && instance.ActiveWaitSources() is [{ BranchId: null } wait])
            {
                correlationIndex.Register(instance.InstanceId, wait.EventName, wait.CorrelationId);
            }
        }
    }

    /// <summary>
    /// Completes this instance's <see cref="AwaitCompletionAsync{TState}"/> waiter (if one is
    /// registered) with the final snapshot, the moment any lane-guarded mutation lands the
    /// instance on a terminal <see cref="WorkflowStatus"/> (CR-016). Also disposes and removes the
    /// instance's cooperative-cancellation source (CR-031) — nothing observes it once terminal.
    /// </summary>
    private void CompleteAwaitersIfTerminal(IWorkflowInstance instance)
    {
        if (!LifecycleMachine.TerminalStatuses.Contains(instance.Status))
        {
            return;
        }

        if (completionWaiters.TryRemove(instance.InstanceId, out var tcs))
        {
            tcs.TrySetResult(ToSnapshot(instance));
        }

        if (instanceCancellations.TryRemove(instance.InstanceId, out var cts))
        {
            cts.Dispose();
        }
    }

    private static WorkflowInstanceSnapshot ToSnapshot(IWorkflowInstance instance) =>
        new(
            instance.InstanceId,
            instance.DefinitionId,
            instance.DefinitionVersion,
            instance.Status,
            instance.CreatedAt,
            instance.UpdatedAt,
            instance.ErrorSummary,
            instance.EndOutcomeName);
}
