using System.Reflection;
using System.Runtime.ExceptionServices;

namespace OrcaCore.Engine.Ephemeral;

internal sealed partial class EphemeralWorkflowDefinitionRegistry
{
    internal void InstallStagedBatch(IEnumerable<IEphemeralStagedWorkflowDefinition> stagedDefinitions)
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
            var candidates = new Dictionary<DefinitionKey, IEphemeralStagedWorkflowDefinition>();
            foreach (var definition in staged)
            {
                var missingPools = MissingTransientPools(
                    definition.RuntimeDefinition,
                    definition.RuntimeStateType);
                if (missingPools.Count > 0)
                {
                    throw global::OrcaCore.Engine.Ephemeral.Internal.EphemeralContractAdapter.HostCompatibility(
                        new DefinitionHostCompatibilityFailure.MissingTransientPools(missingPools));
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

    private IReadOnlyList<TransientPoolName> MissingTransientPools(
        object runtimeDefinition,
        Type stateType)
    {
        try
        {
            return (IReadOnlyList<TransientPoolName>)PreflightRuntimeMethod
                .MakeGenericMethod(stateType)
                .Invoke(engine, [runtimeDefinition])!;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }
}

internal interface IEphemeralStagedWorkflowDefinition
{
    DefinitionId DefinitionId { get; }
    DefinitionVersion DefinitionVersion { get; }
    DefinitionFingerprint DefinitionFingerprint { get; }
    Type HandleType { get; }
    object RuntimeDefinition { get; }
    Type RuntimeStateType { get; }
    void Install(EphemeralWorkflowDefinitionRegistry registry);
}

internal sealed class EphemeralStagedWorkflowDefinition<TInput> : IEphemeralStagedWorkflowDefinition
{
    private readonly EphemeralWorkflowDefinition<TInput> definition;

    internal EphemeralStagedWorkflowDefinition(EphemeralWorkflowDefinition<TInput> definition)
    {
        this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
        RuntimeDefinition = global::OrcaCore.Engine.Ephemeral.Internal.EphemeralContractAdapter.RuntimeDefinition(definition);
        RuntimeStateType = global::OrcaCore.Engine.Ephemeral.Internal.EphemeralContractAdapter.RuntimeStateType(definition);
    }

    public DefinitionId DefinitionId => definition.DefinitionId;
    public DefinitionVersion DefinitionVersion => definition.DefinitionVersion;
    public DefinitionFingerprint DefinitionFingerprint => definition.DefinitionFingerprint;
    public Type HandleType => typeof(EphemeralDefinitionHandle<TInput>);
    public object RuntimeDefinition { get; }
    public Type RuntimeStateType { get; }

    public void Install(EphemeralWorkflowDefinitionRegistry registry) =>
        _ = registry.Register(definition).GetHandleOrThrow();
}

internal sealed class EphemeralStagedWorkflowDefinition<TInput, TOutput> : IEphemeralStagedWorkflowDefinition
{
    private readonly EphemeralWorkflowDefinition<TInput, TOutput> definition;

    internal EphemeralStagedWorkflowDefinition(EphemeralWorkflowDefinition<TInput, TOutput> definition)
    {
        this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
        RuntimeDefinition = global::OrcaCore.Engine.Ephemeral.Internal.EphemeralContractAdapter.RuntimeDefinition(definition);
        RuntimeStateType = global::OrcaCore.Engine.Ephemeral.Internal.EphemeralContractAdapter.RuntimeStateType(definition);
    }

    public DefinitionId DefinitionId => definition.DefinitionId;
    public DefinitionVersion DefinitionVersion => definition.DefinitionVersion;
    public DefinitionFingerprint DefinitionFingerprint => definition.DefinitionFingerprint;
    public Type HandleType => typeof(EphemeralDefinitionHandle<TInput, TOutput>);
    public object RuntimeDefinition { get; }
    public Type RuntimeStateType { get; }

    public void Install(EphemeralWorkflowDefinitionRegistry registry) =>
        _ = registry.Register(definition).GetHandleOrThrow();
}
