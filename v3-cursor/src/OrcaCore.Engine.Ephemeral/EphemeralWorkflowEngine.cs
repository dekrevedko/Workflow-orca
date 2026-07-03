using System.Collections.Concurrent;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Ephemeral.Execution;

namespace OrcaCore.Engine.Ephemeral;

/// <summary>
/// Public entry point for the ephemeral (single-host, in-memory) workflow engine. Exposes
/// metadata-only snapshots; no live instance type is ever public (CR-021).
/// </summary>
public sealed class EphemeralWorkflowEngine
{
    private readonly ConcurrentDictionary<DefinitionId, object> definitions = new();
    private readonly IInstanceRegistry registry;
    private readonly InstanceExecutionLane executionLane = new();
    private readonly TimeProvider timeProvider;

    public EphemeralWorkflowEngine()
        : this(TimeProvider.System)
    {
    }

    public EphemeralWorkflowEngine(TimeProvider timeProvider)
        : this(new InMemoryInstanceRegistry(), timeProvider)
    {
    }

    internal EphemeralWorkflowEngine(IInstanceRegistry registry, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(timeProvider);
        this.registry = registry;
        this.timeProvider = timeProvider;
    }

    /// <summary>Registers a built, immutable definition (CR-002, CR-004) so instances can start against it.</summary>
    public void RegisterDefinition<TState>(WorkflowDefinition<TState> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definitions[definition.DefinitionId] = definition;
    }

    /// <summary>
    /// Starts a new instance of the definition registered under <paramref name="definitionId"/>,
    /// running it inline to its first suspension or terminal outcome (CR-016), and returns a
    /// metadata-only snapshot (CR-021).
    /// </summary>
    public async Task<WorkflowInstanceSnapshot> StartAsync<TInput, TState>(
        DefinitionId definitionId,
        TInput input,
        CancellationToken cancellationToken = default)
    {
        if (!definitions.TryGetValue(definitionId, out var registered) ||
            registered is not WorkflowDefinition<TState> definition)
        {
            throw new WorkflowDefinitionException(
                $"No definition registered for '{definitionId}' with business state type '{typeof(TState).Name}'.");
        }

        var instanceId = InstanceId.New();
        return await executionLane.RunAsync(
            instanceId,
            async ct =>
            {
                var interpreter = new Interpreter<TState>(timeProvider);
                var instance = await interpreter.RunAsync(definition, input, instanceId, ct).ConfigureAwait(false);
                registry.Save(instanceId, instance);
                return instance.ToSnapshot();
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Instance-targeted event delivery (EV-010 item 1): delivers <paramref name="event"/>
    /// directly to <paramref name="instanceId"/>. Routes through the per-instance execution
    /// lane (CR-040) so a matching event resumes its instance exactly once even under
    /// concurrent delivery (EV-023). A non-matching event returns
    /// <see cref="RaiseEventResult.NoMatch"/> rather than throwing (EV-020); correlation-targeted
    /// and definition-targeted fanout routing arrive in T1-10.
    /// </summary>
    public async Task<RaiseEventResult> RaiseEventAsync<TState>(
        InstanceId instanceId,
        EventEnvelope @event,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);

        return await executionLane.RunAsync(
            instanceId,
            async ct =>
            {
                if (!registry.TryGet(instanceId, out var stored) ||
                    stored is not WorkflowInstance<TState> instance)
                {
                    throw new WorkflowRoutingException(
                        $"No active instance '{instanceId}' found for business state type '{typeof(TState).Name}'.");
                }

                if (instance.Status != WorkflowStatus.Waiting ||
                    instance.ActiveWait is not { Status: WaitStatus.Active } wait ||
                    !string.Equals(wait.EventName, @event.EventName, StringComparison.Ordinal) ||
                    !wait.CorrelationId.Equals(@event.CorrelationId))
                {
                    return (RaiseEventResult)new RaiseEventResult.NoMatch();
                }

                if (!definitions.TryGetValue(instance.DefinitionId, out var registered) ||
                    registered is not WorkflowDefinition<TState> definition)
                {
                    throw new WorkflowDefinitionException(
                        $"No definition registered for '{instance.DefinitionId}' with business state type '{typeof(TState).Name}'.");
                }

                var interpreter = new Interpreter<TState>(timeProvider);
                var resumed = await interpreter.ResumeAsync(definition, instance, @event, ct).ConfigureAwait(false);
                registry.Save(instanceId, resumed);
                return new RaiseEventResult.Resumed(resumed.ToSnapshot());
            },
            cancellationToken).ConfigureAwait(false);
    }
}
