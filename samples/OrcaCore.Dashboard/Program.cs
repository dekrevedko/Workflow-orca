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
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddHealthChecks();
builder.Services.AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions
{
    StructuredExecution = new StructuredExecutionHostOptions
    {
        MaxConcurrentExecutionPathsPerInstance = 2,
        StepThrottles = []
    },
    TransientPools = []
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();
app.MapHealthChecks("/health/ready");
app.MapGet("/health/live", () => Results.Ok(new
{
    status = "Healthy",
    checkedAt = timeProvider.GetUtcNow()
}));
app.MapGet(
    "/api/dashboard/snapshot",
    (DashboardReadModel readModel) => readModel.GetSnapshot());
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
