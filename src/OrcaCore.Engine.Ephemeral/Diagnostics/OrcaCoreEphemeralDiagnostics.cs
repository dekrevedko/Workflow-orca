using System.Diagnostics;
using System.Diagnostics.Metrics;
using OrcaCore.Abstractions.Diagnostics;

namespace OrcaCore.Engine.Ephemeral.Diagnostics;

/// <summary>
/// Owns the ephemeral engine telemetry source and meter.
/// </summary>
internal static class OrcaCoreEphemeralDiagnostics
{
    public const string SourceName = OrcaCoreDiagnostics.EphemeralSourceName;

    public static ActivitySource ActivitySource { get; } = new(SourceName);

    public static Meter Meter { get; } = new(SourceName);

    private static readonly Counter<long> WorkflowsStarted =
        Meter.CreateCounter<long>("orcacore.ephemeral.workflows.started");
    private static readonly Counter<long> EventsDelivered =
        Meter.CreateCounter<long>("orcacore.ephemeral.events.delivered");
    private static readonly Counter<long> TimersFired =
        Meter.CreateCounter<long>("orcacore.ephemeral.timers.fired");
    private static readonly Counter<long> TerminalCommands =
        Meter.CreateCounter<long>("orcacore.ephemeral.terminal_commands");
    private static readonly Counter<long> HostCompatibilityFailures =
        Meter.CreateCounter<long>("orcacore.governance.host_compatibility.failures");

    internal static Activity? StartOperation(string operationName)
    {
        return ActivitySource.StartActivity($"orcacore.ephemeral.{operationName}", ActivityKind.Internal);
    }

    internal static void RecordWorkflowStarted(global::OrcaCore.WorkflowInstanceStatus status) =>
        WorkflowsStarted.Add(1, new KeyValuePair<string, object?>("workflow.status", status.ToString()));

    internal static void RecordEventDelivered(global::OrcaCore.WorkflowInstanceStatus status) =>
        EventsDelivered.Add(1, new KeyValuePair<string, object?>("workflow.status", status.ToString()));

    internal static void RecordTimerFired(global::OrcaCore.WorkflowInstanceStatus status) =>
        TimersFired.Add(1, new KeyValuePair<string, object?>("workflow.status", status.ToString()));

    internal static void RecordTerminalCommand(global::OrcaCore.WorkflowInstanceStatus status) =>
        TerminalCommands.Add(1, new KeyValuePair<string, object?>("workflow.status", status.ToString()));

    internal static void RecordHostCompatibilityFailure(string reason) =>
        HostCompatibilityFailures.Add(
            1,
            new KeyValuePair<string, object?>("governance.reason", reason),
            new KeyValuePair<string, object?>("workflow.mode", "ephemeral"));
}
