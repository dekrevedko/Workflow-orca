using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OrcaCore.Abstractions.Diagnostics;
using OrcaCore.Hosting.Telemetry;

namespace OrcaCore.Hosting;

/// <summary>
/// Registers the OpenTelemetry SDK bridge for OrcaCore BCL diagnostics.
/// </summary>
public static class OrcaCoreOpenTelemetryServiceCollectionExtensions
{
    private const string ConfigurationSectionName = "OrcaCore:OpenTelemetry";

    public static IServiceCollection AddOrcaCoreOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        return services.AddOrcaCoreOpenTelemetry(ReadOptions(configuration));
    }

    public static IServiceCollection AddOrcaCoreOpenTelemetry(
        this IServiceCollection services,
        Action<OrcaCoreOpenTelemetryOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new OrcaCoreOpenTelemetryOptions();
        configure?.Invoke(options);
        return services.AddOrcaCoreOpenTelemetry(options);
    }

    public static IServiceCollection AddOrcaCoreOpenTelemetry(
        this IServiceCollection services,
        OrcaCoreOpenTelemetryOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(
                serviceName: options.ServiceName,
                serviceInstanceId: options.ServiceInstanceId)
            .AddAttributes(
            [
                new KeyValuePair<string, object>("orca.hosting.profile", options.HostingProfile)
            ]);

        services.TryAddSingleton<OrcaCoreTelemetryInstruments>();
        services.TryAddSingleton(options);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, OrcaCoreTelemetryGaugeCollector>());

        services.AddLogging(logging => logging.AddOpenTelemetry(otel =>
        {
            otel.IncludeScopes = true;
            otel.IncludeFormattedMessage = true;
            otel.SetResourceBuilder(resourceBuilder);
            if (options.EnableConsoleExporter)
            {
                otel.AddConsoleExporter();
            }

            if (options.EnableOtlpExporter)
            {
                otel.AddOtlpExporter(otlp =>
                {
                    if (!string.IsNullOrWhiteSpace(options.OtlpEndpoint))
                    {
                        otlp.Endpoint = new Uri(options.OtlpEndpoint);
                    }
                });
            }
        }));

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: options.ServiceName,
                    serviceInstanceId: options.ServiceInstanceId)
                .AddAttributes(
                [
                    new KeyValuePair<string, object>("orca.hosting.profile", options.HostingProfile)
                ]))
            .WithMetrics(metrics =>
            {
                metrics.AddMeter(OrcaCoreDiagnostics.MeterNames);
                if (options.EnableConsoleExporter)
                {
                    metrics.AddConsoleExporter();
                }

                if (options.EnableOtlpExporter)
                {
                    metrics.AddOtlpExporter(otlp =>
                    {
                        if (!string.IsNullOrWhiteSpace(options.OtlpEndpoint))
                        {
                            otlp.Endpoint = new Uri(options.OtlpEndpoint);
                        }
                    });
                }

                if (options.EnablePrometheusExporter)
                {
                    metrics.AddPrometheusExporter();
                }
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(OrcaCoreDiagnostics.ActivitySourceNames);
                if (options.TraceSamplingRatio is { } ratio)
                {
                    tracing.SetSampler(new ParentBasedSampler(
                        new TraceIdRatioBasedSampler(Math.Clamp(ratio, 0, 1))));
                }

                if (options.EnableConsoleExporter)
                {
                    tracing.AddConsoleExporter();
                }

                if (options.EnableOtlpExporter)
                {
                    tracing.AddOtlpExporter(otlp =>
                    {
                        if (!string.IsNullOrWhiteSpace(options.OtlpEndpoint))
                        {
                            otlp.Endpoint = new Uri(options.OtlpEndpoint);
                        }
                    });
                }
            });

        return services;
    }

    private static OrcaCoreOpenTelemetryOptions ReadOptions(IConfiguration configuration)
    {
        var section = configuration.GetSection(ConfigurationSectionName);
        return new OrcaCoreOpenTelemetryOptions
        {
            OtlpEndpoint = Value(section, nameof(OrcaCoreOpenTelemetryOptions.OtlpEndpoint)),
            EnableConsoleExporter = Bool(section, nameof(OrcaCoreOpenTelemetryOptions.EnableConsoleExporter)),
            EnableOtlpExporter = Bool(section, nameof(OrcaCoreOpenTelemetryOptions.EnableOtlpExporter)) ||
                !string.IsNullOrWhiteSpace(Value(section, nameof(OrcaCoreOpenTelemetryOptions.OtlpEndpoint))),
            EnablePrometheusExporter = Bool(
                section,
                nameof(OrcaCoreOpenTelemetryOptions.EnablePrometheusExporter)),
            TraceSamplingRatio = Double(section, nameof(OrcaCoreOpenTelemetryOptions.TraceSamplingRatio)),
            GaugeCollectionInterval = TimeSpanValue(
                section,
                nameof(OrcaCoreOpenTelemetryOptions.GaugeCollectionInterval)) ?? TimeSpan.FromSeconds(15),
            ServiceName = Value(section, nameof(OrcaCoreOpenTelemetryOptions.ServiceName)) ?? "orca-core-host",
            ServiceInstanceId = Value(section, nameof(OrcaCoreOpenTelemetryOptions.ServiceInstanceId)) ??
                $"{Environment.MachineName}-{Environment.ProcessId}",
            HostingProfile = Value(section, nameof(OrcaCoreOpenTelemetryOptions.HostingProfile)) ?? "default"
        };
    }

    private static string? Value(IConfiguration section, string name)
    {
        return section[name];
    }

    private static bool Bool(IConfiguration section, string name)
    {
        return bool.TryParse(section[name], out var value) && value;
    }

    private static double? Double(IConfiguration section, string name)
    {
        return double.TryParse(section[name], out var value) ? value : null;
    }

    private static TimeSpan? TimeSpanValue(IConfiguration section, string name)
    {
        return TimeSpan.TryParse(section[name], out var value) ? value : null;
    }
}
