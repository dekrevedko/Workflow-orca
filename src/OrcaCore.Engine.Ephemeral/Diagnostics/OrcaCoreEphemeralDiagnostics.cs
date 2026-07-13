using System.Diagnostics;
using System.Diagnostics.Metrics;
using OrcaCore.Abstractions.Diagnostics;
using OrcaCore.Abstractions.Instances;

namespace OrcaCore.Engine.Ephemeral.Diagnostics;

/// <summary>
/// Owns the ephemeral engine telemetry source and meter.
/// </summary>
public static class OrcaCoreEphemeralDiagnostics
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

    internal static Activity? StartOperation(string operationName)
    {
        return ActivitySource.StartActivity($"orcacore.ephemeral.{operationName}", ActivityKind.Internal);
    }

    internal static void RecordWorkflowStarted(WorkflowStatus status) =>
        WorkflowsStarted.Add(1, new KeyValuePair<string, object?>("workflow.status", status.ToString()));

    internal static void RecordEventDelivered(WorkflowStatus status) =>
        EventsDelivered.Add(1, new KeyValuePair<string, object?>("workflow.status", status.ToString()));

    internal static void RecordTimerFired(WorkflowStatus status) =>
        TimersFired.Add(1, new KeyValuePair<string, object?>("workflow.status", status.ToString()));

    internal static void RecordTerminalCommand(WorkflowStatus status) =>
        TerminalCommands.Add(1, new KeyValuePair<string, object?>("workflow.status", status.ToString()));
}
