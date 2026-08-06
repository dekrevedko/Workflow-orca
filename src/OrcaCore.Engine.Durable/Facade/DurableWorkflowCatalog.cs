namespace OrcaCore.Engine.Durable;

internal sealed partial class DurableWorkflowDefinitionRegistry
{
    internal void InstallStagedBatch(IEnumerable<IDurableStagedWorkflowDefinition> stagedDefinitions)
    {
        ArgumentNullException.ThrowIfNull(stagedDefinitions);
        var staged = stagedDefinitions.ToArray();
        if (staged.Any(definition => definition is null))
        {
            throw new ArgumentException(
                "The staged workflow batch contains a null definition.",
                nameof(stagedDefinitions));
        }

        lock (gate)
        {
            var candidates = new Dictionary<DefinitionKey, IDurableStagedWorkflowDefinition>();
            foreach (var definition in staged)
            {
                var missingPools = MissingDurablePools(definition.RuntimeDefinition);
                if (missingPools.Count > 0)
                {
                    throw global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.HostCompatibility(
                        new DefinitionHostCompatibilityFailure.MissingDurableResourcePools(missingPools));
                }

                var key = new DefinitionKey(definition.DefinitionId, definition.DefinitionVersion);
                if (registrations.TryGetValue(key, out var existing))
                {
                    if (existing.Fingerprint.Equals(definition.DefinitionFingerprint) &&
                        definition.HandleType.IsInstanceOfType(existing.Handle))
                    {
                        continue;
                    }

                    throw StagedRegistrationConflict(definition, existing.Fingerprint);
                }

                if (candidates.TryGetValue(key, out var earlier))
                {
                    if (earlier.DefinitionFingerprint.Equals(definition.DefinitionFingerprint) &&
                        earlier.HandleType == definition.HandleType)
                    {
                        continue;
                    }

                    throw StagedRegistrationConflict(definition, earlier.DefinitionFingerprint);
                }

                candidates.Add(key, definition);
            }

            foreach (var definition in candidates.Values)
            {
                definition.Install(this);
            }
        }
    }

    private IReadOnlyList<ResourcePoolName> MissingDurablePools(object runtimeDefinition) =>
        RequiredDurablePools(runtimeDefinition)
            .Where(pool => !configuredResourcePools.Contains(pool.Value))
            .Distinct()
            .OrderBy(pool => pool.Value, StringComparer.Ordinal)
            .ToArray();

    private static WorkflowDefinitionRegistrationConflictException StagedRegistrationConflict(
        IDurableStagedWorkflowDefinition definition,
        DefinitionFingerprint existingFingerprint)
    {
        var conflict = global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.DefinitionRegistrationConflict(
            definition.DefinitionId,
            definition.DefinitionVersion,
            existingFingerprint,
            definition.DefinitionFingerprint);
        return global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.RegistrationConflict(
            conflict);
    }
}

internal interface IDurableStagedWorkflowDefinition
{
    DefinitionId DefinitionId { get; }
    DefinitionVersion DefinitionVersion { get; }
    DefinitionFingerprint DefinitionFingerprint { get; }
    Type HandleType { get; }
    object RuntimeDefinition { get; }
    void Install(DurableWorkflowDefinitionRegistry registry);
}

internal sealed class DurableStagedWorkflowDefinition<TInput> : IDurableStagedWorkflowDefinition
{
    private readonly DurableWorkflowDefinition<TInput> definition;

    internal DurableStagedWorkflowDefinition(DurableWorkflowDefinition<TInput> definition)
    {
        this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
        RuntimeDefinition = global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.RuntimeDefinition(definition);
    }

    public DefinitionId DefinitionId => definition.DefinitionId;
    public DefinitionVersion DefinitionVersion => definition.DefinitionVersion;
    public DefinitionFingerprint DefinitionFingerprint => definition.DefinitionFingerprint;
    public Type HandleType => typeof(DurableDefinitionHandle<TInput>);
    public object RuntimeDefinition { get; }

    public void Install(DurableWorkflowDefinitionRegistry registry) =>
        _ = registry.Register(definition).GetHandleOrThrow();
}

internal sealed class DurableStagedWorkflowDefinition<TInput, TOutput> : IDurableStagedWorkflowDefinition
{
    private readonly DurableWorkflowDefinition<TInput, TOutput> definition;

    internal DurableStagedWorkflowDefinition(DurableWorkflowDefinition<TInput, TOutput> definition)
    {
        this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
        RuntimeDefinition = global::OrcaCore.Engine.Durable.Internal.DurableApplicationContractFactory.RuntimeDefinition(definition);
    }

    public DefinitionId DefinitionId => definition.DefinitionId;
    public DefinitionVersion DefinitionVersion => definition.DefinitionVersion;
    public DefinitionFingerprint DefinitionFingerprint => definition.DefinitionFingerprint;
    public Type HandleType => typeof(DurableDefinitionHandle<TInput, TOutput>);
    public object RuntimeDefinition { get; }

    public void Install(DurableWorkflowDefinitionRegistry registry) =>
        _ = registry.Register(definition).GetHandleOrThrow();
}
