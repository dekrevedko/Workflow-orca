using System.Collections.Concurrent;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Durable.Driver;
using OrcaCore.Hosting;

namespace OrcaCore.Engine.Durable.Definitions;

/// <summary>
/// Keeps the workflow definition versions a durable host can resume.
/// </summary>
internal sealed class DurableDefinitionRegistry
{
    private readonly ConcurrentDictionary<DurableDefinitionKey, RegisteredDefinition> definitions = [];
    private readonly IServiceProvider? serviceProvider;
    private readonly int maxConcurrentExecutionPathsPerInstance = int.MaxValue;
    private readonly DurableStepThrottleCoordinator stepThrottles = new();

    public DurableDefinitionRegistry()
    {
    }

    public DurableDefinitionRegistry(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        this.serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Creates a registry using the common structured-execution host profile.
    /// </summary>
    public DurableDefinitionRegistry(
        StructuredExecutionHostOptions options,
        IServiceProvider? serviceProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.MaxConcurrentExecutionPathsPerInstance <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                options.MaxConcurrentExecutionPathsPerInstance,
                "The execution-path limit must be positive.");
        }

        ArgumentNullException.ThrowIfNull(options.StepThrottles);
        var copied = new Dictionary<Type, int>();
        foreach (var throttle in options.StepThrottles)
        {
            ArgumentNullException.ThrowIfNull(throttle);
            if (!copied.TryAdd(throttle.StepType, throttle.MaxConcurrency))
            {
                throw new ArgumentException(
                    $"StepThrottles contains duplicate exact step type '{throttle.StepType.FullName}'.",
                    nameof(options));
            }
        }

        maxConcurrentExecutionPathsPerInstance = options.MaxConcurrentExecutionPathsPerInstance;
        stepThrottles = new DurableStepThrottleCoordinator(copied);
        this.serviceProvider = serviceProvider;
    }

    internal DurableDefinitionRegistry(
        int maxConcurrentExecutionPathsPerInstance,
        IServiceProvider? serviceProvider = null,
        IReadOnlyDictionary<Type, int>? exactStepThrottles = null)
    {
        if (maxConcurrentExecutionPathsPerInstance <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxConcurrentExecutionPathsPerInstance),
                maxConcurrentExecutionPathsPerInstance,
                "MaxConcurrentExecutionPathsPerInstance must be positive.");
        }

        this.maxConcurrentExecutionPathsPerInstance = maxConcurrentExecutionPathsPerInstance;
        stepThrottles = new DurableStepThrottleCoordinator(exactStepThrottles);
        this.serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Registers one immutable workflow definition version.
    /// </summary>
    public void Register<TState>(WorkflowDefinition<TState> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var key = new DurableDefinitionKey(definition.DefinitionId, definition.DefinitionVersion);
        var registered = new RegisteredDefinition(
            definition,
            typeof(TState),
            ((CompiledWorkflowPlan)WorkflowDefinitionRuntime.GetPlan(definition)).Fingerprint,
            DurableDriverCatalog.CreateExecutor(
                definition,
                serviceProvider,
                maxConcurrentExecutionPathsPerInstance,
                stepThrottles));
        definitions.AddOrUpdate(
            key,
            registered,
            (_, existing) => SameRegistration(existing, registered)
                ? existing
                : throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException(
                    $"Workflow definition '{key.DefinitionId}' version '{key.DefinitionVersion}' " +
                    $"is already registered with state type '{existing.StateType.FullName}' and " +
                    $"fingerprint '{existing.Fingerprint}'. Candidate fingerprint: '{registered.Fingerprint}'."));
    }

    /// <summary>
    /// Resolves the definition version recorded on a durable instance snapshot.
    /// </summary>
    public WorkflowDefinition<TState> ResolveBound<TState>(WorkflowInstanceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return Resolve<TState>(snapshot.DefinitionId, snapshot.DefinitionVersion);
    }

    /// <summary>
    /// Resolves a registered workflow definition version.
    /// </summary>
    public WorkflowDefinition<TState> Resolve<TState>(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion)
    {
        ArgumentNullException.ThrowIfNull(definitionId);
        ArgumentNullException.ThrowIfNull(definitionVersion);

        var key = new DurableDefinitionKey(definitionId, definitionVersion);
        if (!definitions.TryGetValue(key, out var registered))
        {
            throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException(
                $"Workflow definition '{definitionId}' version '{definitionVersion}' is not registered.");
        }

        if (registered.Definition is WorkflowDefinition<TState> typed)
        {
            return typed;
        }

        throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionException(
            $"Workflow definition '{definitionId}' version '{definitionVersion}' is registered for " +
            $"state type '{registered.StateType.FullName}', not '{typeof(TState).FullName}'.");
    }

    /// <summary>
    /// Lists the registered definition versions.
    /// </summary>
    public IReadOnlyList<DurableRegisteredDefinition> List()
    {
        return definitions
            .Select(entry => new DurableRegisteredDefinition(
                entry.Key.DefinitionId,
                entry.Key.DefinitionVersion,
                entry.Value.StateType))
            .OrderBy(definition => definition.DefinitionId.Value)
            .ThenBy(definition => definition.DefinitionVersion.Value)
            .ToArray();
    }

    internal IDurableDriverExecutor? ResolveExecutor(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion)
    {
        return definitions.TryGetValue(
            new DurableDefinitionKey(definitionId, definitionVersion),
            out var registered)
            ? registered.Executor
            : null;
    }

    internal IReadOnlyList<StepThrottleDebugSnapshot> StepThrottleSnapshots =>
        stepThrottles.Snapshot();

    private static bool SameRegistration(
        RegisteredDefinition existing,
        RegisteredDefinition candidate)
    {
        return existing.StateType == candidate.StateType &&
            string.Equals(existing.Fingerprint, candidate.Fingerprint, StringComparison.Ordinal);
    }

    private sealed record RegisteredDefinition(
        object Definition,
        Type StateType,
        string Fingerprint,
        IDurableDriverExecutor Executor);
}

/// <summary>
/// Identifies a durable workflow definition version.
/// </summary>
internal readonly record struct DurableDefinitionKey(
    DefinitionId DefinitionId,
    DefinitionVersion DefinitionVersion);

/// <summary>
/// Describes one registered durable workflow definition version.
/// </summary>
internal sealed record DurableRegisteredDefinition(
    DefinitionId DefinitionId,
    DefinitionVersion DefinitionVersion,
    Type StateType);
