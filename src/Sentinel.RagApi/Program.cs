using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Pgvector.EntityFrameworkCore;
using Sentinel.Application.AI;
using Sentinel.Application.Rag;
using Sentinel.Infrastructure.AI;
using Sentinel.Infrastructure.Persistence;
using Sentinel.RagApi;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
});
builder.Services.AddExceptionHandler<RagExceptionHandler>();
builder.Services.AddHealthChecks();

var serviceVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown";
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("sentinel-rag-api", serviceVersion: serviceVersion))
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

var connectionString = builder.Configuration.GetConnectionString("Sentinel")
    ?? throw new InvalidOperationException("Connection string 'Sentinel' is not configured.");
builder.Services.AddDbContext<SentinelDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql => npgsql.UseVector()));
builder.Services.AddScoped<IRagKnowledgeRepository, PostgresRagKnowledgeRepository>();
builder.Services.AddScoped<SearchIncidentKnowledgeUseCase>();
builder.Services.AddScoped<QueryIncidentsUseCase>();

builder.Services.Configure<EmbeddingOptions>(builder.Configuration.GetSection(EmbeddingOptions.SectionName));
var embeddingBaseUrl = builder.Configuration["Embedding:BaseUrl"]
    ?? throw new InvalidOperationException("Embedding base URL is not configured.");
builder.Services.AddHttpClient<IEmbeddingClient, OllamaEmbeddingClient>(client =>
{
    client.BaseAddress = new Uri(embeddingBaseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(60);
});

builder.Services.Configure<OpenAiModelOptions>(builder.Configuration.GetSection(OpenAiModelOptions.SectionName));
builder.Services.Configure<OllamaModelOptions>(builder.Configuration.GetSection(OllamaModelOptions.SectionName));
var aiProvider = builder.Configuration["AI:Provider"] ?? "Ollama";
if (string.Equals(aiProvider, "Ollama", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHttpClient<IStructuredModelClient, OllamaStructuredModelClient>((services, client) =>
    {
        var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<OllamaModelOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
        client.Timeout = TimeSpan.FromMinutes(10);
    });
}
else if (string.Equals(aiProvider, "OpenAI", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHttpClient<IStructuredModelClient, OpenAiStructuredModelClient>((services, client) =>
    {
        var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<OpenAiModelOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
        client.Timeout = TimeSpan.FromSeconds(60);
    });
}
else
{
    throw new InvalidOperationException($"Unsupported AI provider '{aiProvider}'.");
}

var app = builder.Build();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapGet("/", () => Results.Ok(new { name = "Sentinel RAG API", status = "available" }))
    .ExcludeFromDescription();
app.MapHealthChecks("/health");

app.MapPost("/api/rag/search", async (
    RagSearchRequest request,
    SearchIncidentKnowledgeUseCase useCase,
    CancellationToken cancellationToken) =>
{
    var matches = await useCase.ExecuteAsync(
        request.Query, request.Service, request.Environment, request.Limit, cancellationToken);
    return Results.Ok(new RagSearchResponse(matches.Select(RagMatchResponse.From).ToArray()));
})
    .WithTags("RAG")
    .WithName("SearchIncidentKnowledge")
    .WithSummary("Semantically search completed incident evidence")
    .Produces<RagSearchResponse>()
    .ProducesProblem(StatusCodes.Status400BadRequest);

app.MapPost("/api/rag/query", async (
    RagQueryRequest request,
    QueryIncidentsUseCase useCase,
    CancellationToken cancellationToken) =>
{
    var answer = await useCase.ExecuteAsync(
        request.Question, request.Service, request.Environment, request.Limit, cancellationToken);
    return Results.Ok(RagQueryResponse.From(answer));
})
    .WithTags("RAG")
    .WithName("QueryIncidents")
    .WithSummary("Answer an incident question using retrieved evidence")
    .Produces<RagQueryResponse>()
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status502BadGateway);

app.Run();
