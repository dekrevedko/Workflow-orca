using System.Collections.Concurrent;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Ephemeral.Execution;

namespace OrcaCore.Engine.Ephemeral;

/// <summary>
/// Executes registered workflow definitions in the current process without durable recovery.
/// </summary>
public sealed class EphemeralWorkflowEngine
{
    private readonly ConcurrentDictionary<DefinitionId, object> definitions = [];
    private readonly IInstanceRegistry instanceRegistry;
    private readonly TimeProvider timeProvider;

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
        : this(timeProvider, new InMemoryInstanceRegistry())
    {
    }

    internal EphemeralWorkflowEngine(TimeProvider timeProvider, IInstanceRegistry instanceRegistry)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(instanceRegistry);

        this.timeProvider = timeProvider;
        this.instanceRegistry = instanceRegistry;
    }

    /// <summary>
    /// Registers a workflow definition version for later starts.
    /// </summary>
    public void RegisterDefinition<TState>(WorkflowDefinition<TState> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        definitions[definition.DefinitionId] = definition;
    }

    /// <summary>
    /// Starts a registered workflow and runs it inline to suspension or terminal status.
    /// </summary>
    public async Task<WorkflowInstanceSnapshot> StartAsync<TInput, TState>(
        DefinitionId definitionId,
        TInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!definitions.TryGetValue(definitionId, out var registeredDefinition))
        {
            throw new WorkflowDefinitionException(
                $"No workflow definition is registered for definition id '{definitionId}'.");
        }

        if (registeredDefinition is not WorkflowDefinition<TState> definition)
        {
            throw new WorkflowDefinitionException(
                $"Workflow definition '{definitionId}' was not registered for state type '{typeof(TState).Name}'.");
        }

        var interpreter = new Interpreter<TState>(timeProvider);
        var instance = await interpreter.RunAsync(definition, input, cancellationToken).ConfigureAwait(false);
        instanceRegistry.Save(instance);

        return instance.ToSnapshot();
    }
}
