using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Microsoft.EntityFrameworkCore;
using Sentinel.Api.Endpoints;
using Sentinel.Application.Abstractions;
using Sentinel.Application.AI;
using Sentinel.Application.Evidence;
using Sentinel.Application.Evidence.AddEvidence;
using Sentinel.Application.Evidence.GetEvidence;
using Sentinel.Application.Evidence.ListIncidentEvidence;
using Sentinel.Application.Evidence.TraceIngestion;
using Sentinel.Application.Evidence.LogIngestion;
using Sentinel.Application.Evidence.MetricIngestion;
using Sentinel.Application.Incidents;
using Sentinel.Application.Incidents.CreateIncident;
using Sentinel.Application.Incidents.GetIncident;
using Sentinel.Application.Investigations.Analysis;
using Sentinel.Application.Investigations;
using Sentinel.Domain.Incidents;
using Sentinel.Infrastructure.Persistence;
using Sentinel.Infrastructure.Observability;
using Sentinel.Infrastructure.Time;
using Sentinel.Infrastructure.AI;

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

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(
            serviceName: "sentinel-api",
            serviceVersion: serviceVersion)
        .AddAttributes([
            new KeyValuePair<string, object>(
                "deployment.environment.name",
                builder.Environment.EnvironmentName)
        ]))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(options =>
            options.Filter = context => !context.Request.Path.StartsWithSegments("/health"))
        .AddHttpClientInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddOtlpExporter());

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHealthChecks();

var sentinelConnectionString = builder.Configuration.GetConnectionString("Sentinel")
    ?? throw new InvalidOperationException("Connection string 'Sentinel' is not configured.");

builder.Services.AddDbContext<SentinelDbContext>(options =>
    options.UseNpgsql(sentinelConnectionString));
builder.Services.AddScoped<IIncidentRepository, PostgresIncidentRepository>();
builder.Services.AddScoped<IEvidenceRepository, PostgresEvidenceRepository>();
builder.Services.AddScoped<IInvestigationRepository, PostgresInvestigationRepository>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<CreateIncidentUseCase>();
builder.Services.AddScoped<GetIncidentUseCase>();
builder.Services.AddScoped<AddEvidenceUseCase>();
builder.Services.AddScoped<GetEvidenceUseCase>();
builder.Services.AddScoped<ListIncidentEvidenceUseCase>();
builder.Services.AddSingleton<ITraceEvidenceNormalizer, DeterministicTraceEvidenceNormalizer>();
builder.Services.AddScoped<ImportTraceEvidenceUseCase>();
builder.Services.AddSingleton<ILogEvidenceNormalizer, DeterministicLogEvidenceNormalizer>();
builder.Services.AddScoped<ImportLogEvidenceUseCase>();
builder.Services.AddSingleton<IMetricEvidenceNormalizer, DeterministicMetricEvidenceNormalizer>();
builder.Services.AddScoped<ImportMetricEvidenceUseCase>();
builder.Services.AddScoped<AnalyzeIncidentUseCase>();
builder.Services.AddScoped<GetInvestigationUseCase>();

builder.Services.Configure<OpenAiModelOptions>(
    builder.Configuration.GetSection(OpenAiModelOptions.SectionName));
builder.Services.Configure<OllamaModelOptions>(
    builder.Configuration.GetSection(OllamaModelOptions.SectionName));
builder.Services.AddScoped<IInvestigationAnalyzer, RootCauseAgent>();
var aiProvider = builder.Configuration["AI:Provider"] ?? "OpenAI";
if (string.Equals(aiProvider, "Ollama", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHttpClient<IStructuredModelClient, OllamaStructuredModelClient>((services, client) =>
    {
        var settings = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<OllamaModelOptions>>().Value;
        client.BaseAddress = new Uri(settings.BaseUrl, UriKind.Absolute);
        client.Timeout = TimeSpan.FromMinutes(10);
    });
}
else if (string.Equals(aiProvider, "OpenAI", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHttpClient<IStructuredModelClient, OpenAiStructuredModelClient>((services, client) =>
    {
        var settings = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<OpenAiModelOptions>>().Value;
        client.BaseAddress = new Uri(settings.BaseUrl, UriKind.Absolute);
        client.Timeout = TimeSpan.FromSeconds(60);
    });
}
else
{
    throw new InvalidOperationException($"Unsupported AI provider '{aiProvider}'.");
}

var tempoBaseUrl = builder.Configuration["Tempo:BaseUrl"]
    ?? throw new InvalidOperationException("Tempo base URL is not configured.");
builder.Services.AddHttpClient<ITraceSource, TempoTraceSource>(client =>
{
    client.BaseAddress = new Uri(tempoBaseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(10);
});

var lokiBaseUrl = builder.Configuration["Loki:BaseUrl"]
    ?? throw new InvalidOperationException("Loki base URL is not configured.");
builder.Services.AddHttpClient<ILogSource, LokiLogSource>(client =>
{
    client.BaseAddress = new Uri(lokiBaseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(10);
});

var prometheusBaseUrl = builder.Configuration["Prometheus:BaseUrl"]
    ?? throw new InvalidOperationException("Prometheus base URL is not configured.");
builder.Services.AddHttpClient<IMetricSource, PrometheusMetricSource>(client =>
{
    client.BaseAddress = new Uri(prometheusBaseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(10);
});

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<SentinelDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Sentinel API v1");
        options.RoutePrefix = "swagger";
        options.DocumentTitle = "Sentinel API";
    });
}

app.MapGet("/", () => Results.Ok(new
{
    name = "Sentinel API",
    status = "available"
}))
    .ExcludeFromDescription();
app.MapHealthChecks("/health");
app.MapIncidentEndpoints();
app.MapEvidenceEndpoints();
app.MapInvestigationEndpoints();

app.Run();

internal sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title) = exception switch
        {
            ArgumentException => (StatusCodes.Status400BadRequest, "Invalid request"),
            IncidentDomainException => (StatusCodes.Status409Conflict, "Incident operation rejected"),
            TraceSourceException => (StatusCodes.Status502BadGateway, "Trace source unavailable"),
            LogSourceException => (StatusCodes.Status502BadGateway, "Log source unavailable"),
            MetricSourceException => (StatusCodes.Status502BadGateway, "Metric source unavailable"),
            StructuredModelException => (StatusCodes.Status502BadGateway, "AI model unavailable"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            LogUnhandledException(logger, exception);
        }
        else
        {
            LogRejectedRequest(logger, exception, statusCode);
        }

        httpContext.Response.StatusCode = statusCode;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = exception.Message
            }
        });
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "An unhandled exception occurred while processing the request")]
    private static partial void LogUnhandledException(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "A request was rejected with status code {StatusCode}")]
    private static partial void LogRejectedRequest(
        ILogger logger,
        Exception exception,
        int statusCode);
}
