using System.Text.Json.Serialization;
using OrcaCore.Dashboard.Components;
using OrcaCore.Dashboard.Telemetry;
using OrcaCore.Dashboard.Workflows;
using OrcaCore.Hosting;

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
builder.Services.AddHttpClient();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddHealthChecks();

builder.Services
    .AddOrcaCore()
    .AddOrcaCoreOpenTelemetry(builder.Configuration)
    .AddOrcaCoreHostedServices(options =>
    {
        options.OutboxPumpInterval = TimeSpan.FromSeconds(2);
        options.TimerSweepInterval = TimeSpan.FromSeconds(2);
        options.OperationalSweepInterval = TimeSpan.FromSeconds(15);
    });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapHealthChecks("/health/ready");
app.MapGet(
    "/health/live",
    () => Results.Ok(new { status = "Healthy", checkedAt = timeProvider.GetUtcNow() }));
app.MapPrometheusScrapingEndpoint("/metrics");
app.MapGet(
    "/api/dashboard/snapshot",
    async (DashboardReadModel readModel, CancellationToken cancellationToken) =>
        await readModel.GetSnapshotAsync(cancellationToken).ConfigureAwait(false));

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
