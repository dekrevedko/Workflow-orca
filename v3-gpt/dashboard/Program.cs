using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OrcaCore.Abstractions.Diagnostics;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Dashboard.Components;
using OrcaCore.Dashboard.Telemetry;
using OrcaCore.Dashboard.Workflows;
using OrcaCore.Hosting;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);
var timeProvider = TimeProvider.System;
var telemetryStore = new DashboardTelemetryStore(timeProvider);
telemetryStore.Start();

builder.Logging.ClearProviders();
builder.Logging.SetMinimumLevel(LogLevel.Information);
builder.Logging.AddConsole();
builder.Logging.AddProvider(telemetryStore);

builder.Services.AddSingleton(timeProvider);
builder.Services.AddSingleton(telemetryStore);
builder.Services.AddSingleton<DashboardReadModel>();
builder.Services.AddSingleton<DashboardDemoSeeder>();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddHealthChecks();

builder.Services
    .AddOrcaCore()
    .AddOrcaCoreHostedServices(options =>
    {
        options.OutboxPumpInterval = TimeSpan.FromSeconds(2);
        options.TimerSweepInterval = TimeSpan.FromSeconds(2);
        options.OperationalSweepInterval = TimeSpan.FromSeconds(15);
    });
builder.Services.Replace(ServiceDescriptor.Singleton<IMessageDispatcher, DashboardDemoDispatcher>());

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics.AddMeter(OrcaCoreDiagnostics.SourceName))
    .WithTracing(tracing => tracing.AddSource(OrcaCoreDiagnostics.SourceName));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpMetrics();
app.UseAntiforgery();

app.MapHealthChecks("/health/ready");
app.MapGet(
    "/health/live",
    () => Results.Ok(new { status = "Healthy", checkedAt = timeProvider.GetUtcNow() }));
app.MapMetrics("/metrics");
app.MapGet(
    "/api/dashboard/snapshot",
    async (DashboardReadModel readModel, CancellationToken cancellationToken) =>
        await readModel.GetSnapshotAsync(cancellationToken).ConfigureAwait(false));
app.MapPost(
    "/api/dashboard/demo",
    async (DashboardDemoSeeder seeder, CancellationToken cancellationToken) =>
        Results.Accepted(value: await seeder.SeedAsync(cancellationToken).ConfigureAwait(false)));
app.MapPost(
    "/api/dashboard/outbox/pump",
    async (DashboardDemoSeeder seeder, CancellationToken cancellationToken) =>
        Results.Ok(new { dispatched = await seeder.PumpOutboxAsync(cancellationToken).ConfigureAwait(false) }));

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
