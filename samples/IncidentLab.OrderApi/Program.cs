using System.Text.Json.Serialization;
using IncidentLab.OrderApi.Endpoints;
using IncidentLab.OrderApi.Scenarios;
using IncidentLab.OrderApi.Telemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Logging.AddOpenTelemetry(options =>
{
    options.IncludeFormattedMessage = true;
    options.IncludeScopes = true;
    options.AddOtlpExporter();
});

var serviceVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown";

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton<ScenarioEngine>();
builder.Services.AddSingleton<IncidentLabTelemetry>();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(
            serviceName: "incidentlab-order-api",
            serviceVersion: serviceVersion)
        .AddAttributes([
            new KeyValuePair<string, object>(
                "deployment.environment.name",
                builder.Environment.EnvironmentName)
        ]))
    .WithTracing(tracing => tracing
        .AddSource(IncidentLabTelemetry.ActivitySourceName)
        .AddAspNetCoreInstrumentation(options =>
            options.Filter = context => !context.Request.Path.StartsWithSegments("/health"))
        .AddHttpClientInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddMeter(IncidentLabTelemetry.MeterName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddOtlpExporter());

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    name = "Incident Lab Order API",
    purpose = "Produces controlled telemetry for Sentinel investigations"
}));
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapOrderEndpoints();
app.MapScenarioEndpoints();

app.Run();
