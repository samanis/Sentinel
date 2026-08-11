using Microsoft.EntityFrameworkCore;
using Sentinel.Application.Abstractions;
using Sentinel.Application.Evidence.LogIngestion;
using Sentinel.Application.Evidence.TraceIngestion;
using Sentinel.Application.Ingestion;
using Sentinel.Infrastructure.Observability;
using Sentinel.Infrastructure.Persistence;
using Sentinel.Infrastructure.Time;
using Sentinel.Worker;
using Sentinel.Application.AI;
using Sentinel.Infrastructure.AI;
using Pgvector.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();

var connectionString = builder.Configuration.GetConnectionString("Sentinel")
    ?? throw new InvalidOperationException("Connection string 'Sentinel' is not configured.");
builder.Services.AddDbContext<SentinelDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql => npgsql.UseVector()));
builder.Services.AddScoped<IAlertIngestionRepository, PostgresAlertIngestionRepository>();
builder.Services.AddScoped<IIngestionWorkRepository, PostgresAlertIngestionRepository>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<ILogEvidenceNormalizer, DeterministicLogEvidenceNormalizer>();
builder.Services.AddSingleton<ITraceEvidenceNormalizer, DeterministicTraceEvidenceNormalizer>();
builder.Services.AddScoped<ProcessNextIngestionUseCase>();
builder.Services.AddScoped<IEvidenceBundleRepository, PostgresEvidenceBundleRepository>();
builder.Services.Configure<EmbeddingOptions>(builder.Configuration.GetSection(EmbeddingOptions.SectionName));
var embeddingBaseUrl = builder.Configuration["Embedding:BaseUrl"]
    ?? throw new InvalidOperationException("Embedding base URL is not configured.");
builder.Services.AddHttpClient<IEmbeddingClient, OllamaEmbeddingClient>(client =>
{
    client.BaseAddress = new Uri(embeddingBaseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddScoped<ProcessNextEvidenceBundleUseCase>();

var lokiBaseUrl = builder.Configuration["Loki:BaseUrl"]
    ?? throw new InvalidOperationException("Loki base URL is not configured.");
builder.Services.AddHttpClient<ILogSource, LokiLogSource>(client =>
{
    client.BaseAddress = new Uri(lokiBaseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(10);
});
var tempoBaseUrl = builder.Configuration["Tempo:BaseUrl"]
    ?? throw new InvalidOperationException("Tempo base URL is not configured.");
builder.Services.AddHttpClient<ITraceSource, TempoTraceSource>(client =>
{
    client.BaseAddress = new Uri(tempoBaseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.Configure<IngestionWorkerOptions>(builder.Configuration.GetSection("IngestionWorker"));
builder.Services.AddHostedService<IngestionWorker>();

var host = builder.Build();
await using (var scope = host.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<SentinelDbContext>().Database.MigrateAsync();
}
await host.RunAsync();
