using OrcaCore.Abstractions.Ids;
using OrcaCore.Engine.Ephemeral.Execution;

namespace OrcaCore.Engine.Ephemeral.Management;

/// <summary>
/// Fluent management query entry point (MG-001: scope → filter → terminal), reached via
/// <see cref="EphemeralWorkflowEngine.Query"/>. Scoping methods have no side effects; the
/// resulting <see cref="ManagementQueryScope"/> adds constrained filtering (MG-002) and terminal
/// snapshot/statistics/command operations (MG-005: every result is an immutable snapshot or copy,
/// never a live runtime object).
/// </summary>
public sealed class ManagementQueryRoot
{
    private readonly IInstanceRegistry instanceRegistry;
    private readonly TerminateInstanceAsync terminateInstance;

    internal ManagementQueryRoot(IInstanceRegistry instanceRegistry, TerminateInstanceAsync terminateInstance)
    {
        this.instanceRegistry = instanceRegistry;
        this.terminateInstance = terminateInstance;
    }

    /// <summary>Engine-wide scope: every currently-registered instance, across all definitions. Unconstrained until narrowed (MG-004 broad-selection gate).</summary>
    public ManagementQueryScope All() => new(instanceRegistry, terminateInstance, definitionId: null, instanceId: null, isUnconstrainedAll: true);

    /// <summary>Definition-scoped selection (MG-001): only instances of <paramref name="definitionId"/>.</summary>
    public ManagementQueryScope ForDefinition(DefinitionId definitionId) =>
        new(instanceRegistry, terminateInstance, definitionId, instanceId: null, isUnconstrainedAll: false);

    /// <summary>Instance-scoped selection (MG-001): at most the single instance identified by <paramref name="instanceId"/>.</summary>
    public ManagementQueryScope Instance(InstanceId instanceId) =>
        new(instanceRegistry, terminateInstance, definitionId: null, instanceId, isUnconstrainedAll: false);
}
