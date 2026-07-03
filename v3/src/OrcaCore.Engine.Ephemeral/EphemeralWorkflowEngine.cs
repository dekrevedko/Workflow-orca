using System.Collections.Concurrent;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Core.Definitions;
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
    /// result is an immutable snapshot or copy, never a live runtime object).
    /// </summary>
    public ManagementQueryRoot Query() => new(instanceRegistry);

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

        var interpreter = new Interpreter<TState>();
        await executionLane.RunAsync(
            instance.InstanceId,
            () => interpreter.RunAsync(instance, definition, timeProvider, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        SyncCorrelationIndex(instance);

        return ToSnapshot(instance);
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
        var outcome = RaiseEventOutcome.InstanceNotFound;

        await executionLane.RunAsync(
            instanceId,
            async () =>
            {
                var instance = instanceRegistry.TryGet<TState>(instanceId);
                if (instance is null)
                {
                    return;
                }

                if (!definitions.TryGetValue(instance.DefinitionId, out var untypedDefinition) ||
                    untypedDefinition is not WorkflowDefinition<TState> definition)
                {
                    throw new WorkflowDefinitionException(
                        $"No definition registered for '{instance.DefinitionId}'. The instance cannot be resumed.");
                }

                var interpreter = new Interpreter<TState>();
                outcome = await interpreter.TryResumeAsync(instance, definition, envelope, timeProvider, cancellationToken)
                    .ConfigureAwait(false);

                SyncCorrelationIndex(instance);
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
    /// Reconciles the EV-011 correlation index for one instance against its current runtime
    /// state after a lane-guarded mutation (start, resume). Removes any stale entry first, then
    /// re-registers if the instance is <c>Waiting</c> with a still-<c>Active</c> wait — covers
    /// registration, match/resume, and terminal cleanup in one place, since all three collapse
    /// to "what does this instance's active wait look like right now".
    /// </summary>
    private void SyncCorrelationIndex<TState>(WorkflowInstance<TState> instance)
    {
        lock (indexLock)
        {
            correlationIndex.Remove(instance.InstanceId);

            if (instance.Status == WorkflowStatus.Waiting && instance.ActiveWait is { Status: WaitStatus.Active } wait)
            {
                correlationIndex.Register(instance.InstanceId, wait.EventName, wait.CorrelationId);
            }
        }
    }

    private static WorkflowInstanceSnapshot ToSnapshot<TState>(WorkflowInstance<TState> instance) =>
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
