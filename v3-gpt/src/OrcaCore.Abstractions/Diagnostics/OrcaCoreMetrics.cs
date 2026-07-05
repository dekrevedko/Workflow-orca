namespace OrcaCore.Abstractions.Diagnostics;

/// <summary>
/// Defines stable OrcaCore metric instrument names.
/// </summary>
public static class OrcaCoreMetrics
{
    public const string CommandsProcessedName = "orca.commands.processed";
    public const string CommandsDurationName = "orca.commands.duration";
    public const string OutboxDispatchedName = "orca.outbox.dispatched";
    public const string OutboxDispatchDurationName = "orca.outbox.dispatch.duration";
    public const string InstancesActiveName = "orca.instances.active";
}
