using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;

namespace OrcaCore.Engine.Ephemeral.Management;

/// <summary>
/// One grouped count from <c>Statistics()</c> (MG-030): the number of instances sharing the same
/// definition, version, and status within the queried scope.
/// </summary>
public sealed record InstanceStatisticsGroup(
    DefinitionId DefinitionId,
    DefinitionVersion DefinitionVersion,
    WorkflowStatus Status,
    int Count);
