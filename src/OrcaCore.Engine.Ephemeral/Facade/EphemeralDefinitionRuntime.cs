using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Ephemeral.Execution;

namespace OrcaCore.Engine.Ephemeral;

// The Core definition carries TState across this existing friend boundary.
// No Type-to-generic reconstruction or user service resolution occurs here.
internal interface IEphemeralDefinitionRuntime
{
    Type StateType { get; }
    IReadOnlyList<TransientPoolName> MissingTransientPools();
    void Register();
    Task<EphemeralWorkflowInstanceSnapshot> StartAsync<TInput>(
        DefinitionId definitionId, DefinitionVersion version, TInput input, CancellationToken cancellationToken);
    Task RaiseEventAsync(InstanceId instanceId, EventEnvelope envelope, CancellationToken cancellationToken);
}

internal static class EphemeralDefinitionRuntime
{
    internal static IEphemeralDefinitionRuntime Bind(EphemeralWorkflowEngine engine, object definition) =>
        ((WorkflowDefinitionRuntimeMetadata)definition).Visit(new Binder(engine));

    private sealed class Binder(EphemeralWorkflowEngine engine) : IWorkflowDefinitionVisitor<IEphemeralDefinitionRuntime>
    {
        public IEphemeralDefinitionRuntime Visit<TState>(WorkflowDefinition<TState> definition) =>
            new Binding<TState>(engine, definition);
    }

    private sealed class Binding<TState>(EphemeralWorkflowEngine engine, WorkflowDefinition<TState> definition)
        : IEphemeralDefinitionRuntime
    {
        public Type StateType => typeof(TState);
        public IReadOnlyList<TransientPoolName> MissingTransientPools() => engine.PreflightDefinition(definition);
        public void Register() => engine.RegisterDefinition(definition);
        public Task<EphemeralWorkflowInstanceSnapshot> StartAsync<TInput>(
            DefinitionId definitionId, DefinitionVersion version, TInput input, CancellationToken cancellationToken) =>
            engine.StartCoreAsync<TInput, TState>(definitionId, version, input, cancellationToken);
        public async Task RaiseEventAsync(InstanceId instanceId, EventEnvelope envelope, CancellationToken cancellationToken) =>
            _ = await engine.RaiseEventCoreAsync<TState>(instanceId, envelope, cancellationToken).ConfigureAwait(false);
    }
}
