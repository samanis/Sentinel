using System.Text.Json.Serialization;
using IncidentLab.TelemetryGenerator;
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

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOptions<TelemetryGeneratorOptions>()
    .Bind(builder.Configuration.GetSection(TelemetryGeneratorOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(
        options => Uri.TryCreate(options.TargetBaseUrl, UriKind.Absolute, out _),
        "TelemetryGenerator:TargetBaseUrl must be an absolute URL.")
    .Validate(
        options => options.MinimumOrderId <= options.MaximumOrderId,
        "TelemetryGenerator:MinimumOrderId must not exceed MaximumOrderId.")
    .ValidateOnStart();

builder.Services.AddSingleton<GeneratorState>();
builder.Services.AddSingleton<OrderIdSequence>();
builder.Services.AddSingleton<TelemetryGeneratorTelemetry>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHostedService<OrderTrafficWorker>();
builder.Services.AddHostedService<FailureScenarioWorker>();

builder.Services.AddHttpClient(OrderTrafficWorker.HttpClientName, (services, client) =>
{
    var options = services
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<TelemetryGeneratorOptions>>()
        .Value;

    client.BaseAddress = new Uri(options.TargetBaseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
});

var serviceVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(
            serviceName: "incidentlab-telemetry-generator",
            serviceVersion: serviceVersion)
        .AddAttributes([
            new KeyValuePair<string, object>(
                "deployment.environment.name",
                builder.Environment.EnvironmentName)
        ]))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(options =>
            options.Filter = context =>
                !context.Request.Path.StartsWithSegments("/health") &&
                !context.Request.Path.StartsWithSegments("/status"))
        .AddHttpClientInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddMeter(TelemetryGeneratorTelemetry.MeterName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddOtlpExporter());

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    name = "Incident Lab Telemetry Generator",
    purpose = "Produces deterministic HTTP traffic for the Incident Lab Order API"
}));

app.MapGet("/health", (GeneratorState state) =>
{
    var snapshot = state.GetSnapshot();
    return snapshot.IsRunning
        ? Results.Ok(new { status = "healthy" })
        : Results.Json(new { status = "starting" }, statusCode: StatusCodes.Status503ServiceUnavailable);
});

app.MapGet("/status", (GeneratorState state) => Results.Ok(state.GetSnapshot()));

app.Run();
