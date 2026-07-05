namespace OrcaCore.Hosting.Telemetry;

/// <summary>
/// Configures the opt-in OpenTelemetry bridge for OrcaCore hosts.
/// </summary>
public sealed class OrcaCoreOpenTelemetryOptions
{
    public string? OtlpEndpoint { get; set; }

    public bool EnableConsoleExporter { get; set; }

    public bool EnableOtlpExporter { get; set; }

    public bool EnablePrometheusExporter { get; set; }

    public double? TraceSamplingRatio { get; set; }

    public TimeSpan GaugeCollectionInterval { get; set; } = TimeSpan.FromSeconds(15);

    public string ServiceName { get; set; } = "orca-core-host";

    public string ServiceInstanceId { get; set; } =
        $"{Environment.MachineName}-{Environment.ProcessId}";

    public string HostingProfile { get; set; } = "default";
}
