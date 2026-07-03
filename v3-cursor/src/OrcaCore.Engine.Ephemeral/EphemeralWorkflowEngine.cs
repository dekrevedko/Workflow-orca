using System.Collections.Concurrent;
using OrcaCore.Abstractions.Errors;
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
        var interpreter = new Interpreter<TState>(timeProvider);
        var instance = await interpreter.RunAsync(definition, input, instanceId, cancellationToken).ConfigureAwait(false);
        registry.Save(instanceId, instance);
        return instance.ToSnapshot();
    }
}
