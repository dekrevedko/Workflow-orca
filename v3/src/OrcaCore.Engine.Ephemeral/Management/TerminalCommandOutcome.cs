using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;

namespace OrcaCore.Engine.Ephemeral.Management;

/// <summary>
/// One instance's result from a bulk terminal command over a <see cref="ManagementQueryScope"/>
/// selection (MG-004/MG-005, T1-14): the affected <see cref="InstanceId"/> and the
/// <see cref="WorkflowStatus"/> it reached. Never a live instance reference.
/// </summary>
public sealed record TerminalCommandOutcome(InstanceId InstanceId, WorkflowStatus Status);

/// <summary>
/// Engine-supplied per-instance terminate operation (T1-14), reached from
/// <see cref="ManagementQueryScope.Terminate"/> without the management layer needing to know each
/// instance's business-state type — a terminal command never touches business state.
/// </summary>
internal delegate Task<TerminalCommandOutcome> TerminateInstanceAsync(InstanceId instanceId, CancellationToken cancellationToken);
