using System.Collections.Concurrent;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Ephemeral.Execution;

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

        var interpreter = new Interpreter<TState>();
        await executionLane.RunAsync(
            instance.InstanceId,
            () => interpreter.RunAsync(instance, definition, timeProvider, cancellationToken),
            cancellationToken).ConfigureAwait(false);

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
            },
            cancellationToken).ConfigureAwait(false);

        return outcome;
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
