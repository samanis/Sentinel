using Sentinel.Application.Abstractions;
using Sentinel.Application.AI;
using Sentinel.Application.Ingestion;
using Sentinel.Domain.Ingestion;
using Sentinel.Infrastructure.AI;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text;

namespace Sentinel.UnitTests.Ingestion;

public sealed class EvidenceBundleTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 10, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task OllamaClientAcceptsEmbeddingGemmaVector()
    {
        var values = string.Join(',', Enumerable.Repeat("0.03608439", EmbeddingOptions.RequiredDimensions));
        using var httpClient = new HttpClient(new StubHttpHandler(
            $"{{\"model\":\"embeddinggemma\",\"embeddings\":[[{values}]]}}"))
        {
            BaseAddress = new Uri("http://ollama:11434/")
        };
        var client = new OllamaEmbeddingClient(httpClient, Options.Create(new EmbeddingOptions()));

        var result = await client.EmbedAsync("database timeout", default);

        Assert.Equal("embeddinggemma", result.Model);
        Assert.Equal(EmbeddingOptions.RequiredDimensions, result.Vector.Length);
    }

    [Fact]
    public async Task WorkerBuildsAndPersistsCanonicalBundle()
    {
        var repository = new StubBundleRepository(Candidate());
        var useCase = new ProcessNextEvidenceBundleUseCase(
            repository, new StubEmbeddingClient(), new StubClock());

        var result = await useCase.ExecuteAsync();

        Assert.True(result.WorkClaimed);
        Assert.Contains("Alert: DependencyTimeout", repository.Document);
        Assert.Contains("Database timed out", repository.Document);
        Assert.Equal("test-embedding", repository.Model);
        Assert.Equal(EmbeddingOptions.RequiredDimensions, repository.Vector?.Length);
    }

    [Fact]
    public async Task BundlePrioritizesEvidenceClosestToAlertStart()
    {
        var oldObservations = Enumerable.Range(0, 250)
            .Select(index => new BundleObservation(
                "Loki",
                $"loki://logs/order-api/old-{index}",
                Now.AddMinutes(-10).AddMilliseconds(index),
                $"Old unrelated failure {index}",
                null,
                "order-api"));
        var currentSql = new BundleObservation(
            "Loki",
            "loki://logs/order-api/current",
            Now.AddSeconds(1),
            "DiagnosticStatement=SELECT id, status, total FROM orders WHERE id = @orderId",
            null,
            "order-api");
        var candidate = new EvidenceBundleCandidate(
            Guid.NewGuid(), IngestionRunId.New(), "SlowDatabase", "order-api", "local",
            "SlowDatabase", true, Now,
            oldObservations.Append(currentSql).ToArray());

        var repository = new StubBundleRepository(candidate);
        var useCase = new ProcessNextEvidenceBundleUseCase(
            repository, new StubEmbeddingClient(), new StubClock());

        await useCase.ExecuteAsync();

        Assert.Contains("SELECT id, status, total FROM orders", repository.Document);
    }

    [Fact]
    public async Task BundleExcludesTelemetryFromAnotherScenario()
    {
        var candidate = new EvidenceBundleCandidate(
            Guid.NewGuid(), IngestionRunId.New(), "IncidentLabOrderFailure", "order-api", "local",
            "SlowDatabase", true, Now,
            [
                new BundleObservation("Loki", "loki://slow", Now,
                    "Scenario SlowDatabase injected a simulated database delay", null, "order-api"),
                new BundleObservation("Loki", "loki://external", Now,
                    "Scenario ExternalApiTimeout injected a simulated payment timeout", null, "order-api")
            ]);
        var repository = new StubBundleRepository(candidate);

        await new ProcessNextEvidenceBundleUseCase(
            repository, new StubEmbeddingClient(), new StubClock()).ExecuteAsync();

        Assert.Contains("Scenario SlowDatabase", repository.Document);
        Assert.DoesNotContain("ExternalApiTimeout", repository.Document);
    }

    [Fact]
    public async Task BundleDoesNotLetLogsCrowdOutTempoEvidence()
    {
        var logs = Enumerable.Range(0, 250).Select(index => new BundleObservation(
            "Loki", $"loki://{index}", Now.AddMilliseconds(index),
            $"Scenario FtpTransferFailure log {index}", $"trace-{index}", "order-api"));
        var trace = new BundleObservation(
            "Tempo", "tempo://ftp", Now.AddSeconds(1),
            "Scenario: FtpTransferFailure. Error type: connection_reset.", "trace-1", "order-api");
        var candidate = new EvidenceBundleCandidate(
            Guid.NewGuid(), IngestionRunId.New(), "IncidentLabOrderFailure", "order-api", "local",
            "FtpTransferFailure", true, Now, logs.Append(trace).ToArray());
        var repository = new StubBundleRepository(candidate);

        await new ProcessNextEvidenceBundleUseCase(
            repository, new StubEmbeddingClient(), new StubClock()).ExecuteAsync();

        Assert.Contains("[Loki]", repository.Document);
        Assert.Contains("[Tempo]", repository.Document);
        Assert.Contains("connection_reset", repository.Document);
    }

    private static EvidenceBundleCandidate Candidate() => new(
        Guid.NewGuid(), IngestionRunId.New(), "DependencyTimeout", "order-api", "local",
        "DependencyTimeout", true, Now,
        [new BundleObservation(
            "Loki", "loki://logs/order-api/1", Now,
            "Scenario DependencyTimeout: Database timed out while loading an order.", null, "order-api")]);

    private sealed class StubBundleRepository(EvidenceBundleCandidate candidate) : IEvidenceBundleRepository
    {
        private bool claimed;
        public string? Document { get; private set; }
        public string? Model { get; private set; }
        public float[]? Vector { get; private set; }

        public Task<EvidenceBundleCandidate?> ClaimNextAsync(DateTimeOffset claimedAt, CancellationToken cancellationToken)
        {
            if (claimed) return Task.FromResult<EvidenceBundleCandidate?>(null);
            claimed = true;
            return Task.FromResult<EvidenceBundleCandidate?>(candidate);
        }

        public Task CompleteAsync(
            Guid bundleId, string searchDocument, string embeddingModel,
            float[] embedding, DateTimeOffset completedAt, CancellationToken cancellationToken)
        {
            Document = searchDocument;
            Model = embeddingModel;
            Vector = embedding;
            return Task.CompletedTask;
        }

        public Task FailAsync(Guid bundleId, string failureCode, DateTimeOffset failedAt, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<SimilarEvidenceBundle>> SearchAsync(
            float[] embedding, string embeddingModel, string? service,
            string? environment, int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SimilarEvidenceBundle>>([]);
    }

    private sealed class StubClock : IClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class StubEmbeddingClient : IEmbeddingClient
    {
        public Task<EmbeddingResult> EmbedAsync(string text, CancellationToken cancellationToken) =>
            Task.FromResult(new EmbeddingResult(
                "test-embedding", new float[EmbeddingOptions.RequiredDimensions]));
    }

    private sealed class StubHttpHandler(string response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json")
            });
    }
}
