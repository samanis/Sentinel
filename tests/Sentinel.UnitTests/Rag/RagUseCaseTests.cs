using System.Text.Json;
using Sentinel.Application.AI;
using Sentinel.Application.Rag;

namespace Sentinel.UnitTests.Rag;

public sealed class RagUseCaseTests
{
    [Fact]
    public async Task SearchEmbedsQuestionAndReturnsSemanticMatches()
    {
        var match = CreateMatch();
        var repository = new StubRepository([match]);
        var useCase = new SearchIncidentKnowledgeUseCase(repository, new StubEmbeddingClient());

        var result = await useCase.ExecuteAsync(
            "dependency timeout", " incidentlab-order-api ", " local ", 5);

        Assert.Same(match, Assert.Single(result));
        Assert.Equal("incidentlab-order-api", repository.Service);
        Assert.Equal("local", repository.Environment);
        Assert.Equal("embeddinggemma", repository.Model);
    }

    [Fact]
    public async Task SearchReranksExplicitSqlEvidenceAboveGenericTimeout()
    {
        var sql = CreateMatch() with { Similarity = 0.60 };
        var generic = CreateMatch() with
        {
            BundleId = Guid.NewGuid(),
            SearchDocument = "Order database timed out in span GET /orders. HTTP 504.",
            Similarity = 0.70
        };
        var useCase = new SearchIncidentKnowledgeUseCase(
            new StubRepository([generic, sql]), new StubEmbeddingClient());

        var result = await useCase.ExecuteAsync(
            "Which database query caused the Order API delay and timeout?", null, null, 2);

        Assert.Equal(sql.BundleId, result[0].BundleId);
        Assert.Equal(generic.BundleId, result[1].BundleId);
    }

    [Fact]
    public async Task SearchUsesRecencyForMostRecentStatusQuery()
    {
        var older502 = CreateMatch() with
        {
            SearchDocument = "HTTP=502 older incident",
            CreatedAt = new DateTimeOffset(2026, 8, 10, 18, 0, 0, TimeSpan.Zero)
        };
        var newest502 = CreateMatch() with
        {
            BundleId = Guid.NewGuid(),
            SearchDocument = "HTTP status: 502 newest incident",
            Similarity = 0.40,
            CreatedAt = new DateTimeOffset(2026, 8, 10, 20, 0, 0, TimeSpan.Zero)
        };
        var newerBut500 = CreateMatch() with
        {
            BundleId = Guid.NewGuid(),
            SearchDocument = "HTTP=500 unrelated status",
            CreatedAt = new DateTimeOffset(2026, 8, 10, 21, 0, 0, TimeSpan.Zero)
        };
        var useCase = new SearchIncidentKnowledgeUseCase(
            new StubRepository([older502, newerBut500, newest502]), new StubEmbeddingClient());

        var result = await useCase.ExecuteAsync("What is the issue with mos recent 502 error?", null, null, 1);

        Assert.Equal(newest502.BundleId, Assert.Single(result).BundleId);
    }

    [Fact]
    public async Task QueryReturnsGroundedAnswerWithRetrievedSource()
    {
        var match = CreateMatch();
        var search = new SearchIncidentKnowledgeUseCase(
            new StubRepository([match]), new StubEmbeddingClient());
        var model = new StubModelClient(JsonSerializer.Serialize(new
        {
            answer = "A simulated dependency timeout produced HTTP 504 responses.",
            sourceIds = new[] { match.BundleId }
        }));
        var useCase = new QueryIncidentsUseCase(search, model);

        var result = await useCase.ExecuteAsync("What caused the failures?", null, null, 5);

        Assert.Equal("A simulated dependency timeout produced HTTP 504 responses.", result.Answer);
        Assert.Equal("test-model", result.Model);
        var source = Assert.Single(result.Sources);
        Assert.Equal(match.BundleId, source.BundleId);
        Assert.Equal(1, source.OccurrenceCount);
        Assert.Equal(1, source.OccurrencesLastHour);
        Assert.True(source.IsSimulated);
        Assert.Equal("SlowDatabase", source.Scenario);
        Assert.Contains("20 related log entries", source.LogSummary);
        Assert.Contains("1 related error spans", source.TraceSummary);
        Assert.Contains("HTTP 504", source.LogContents[0]);
        Assert.Contains("Order database timed out", Assert.Single(source.TraceContents));
    }

    [Fact]
    public async Task QueryFiltersMixedLegacySourceToItsMostRelevantScenario()
    {
        var match = CreateMatch() with
        {
            SearchDocument = """
                Alert: IncidentLabOrderFailure
                Evidence:
                - [Loki] Scenario SlowDatabase DiagnosticStatement=SELECT * FROM orders TraceId=slow1
                - [Loki] Scenario ExternalApiTimeout Target=payments.example.test TraceId=api1
                - [Tempo] Scenario: SlowDatabase. Database query: SELECT * FROM orders TraceId=slow1
                - [Tempo] Scenario: ExternalApiTimeout. Target: payments.example.test TraceId=api1
                """
        };
        var search = new SearchIncidentKnowledgeUseCase(
            new StubRepository([match]), new StubEmbeddingClient());
        var model = new StubModelClient(JsonSerializer.Serialize(new
        {
            answer = "Incident Lab simulated a slow database query.",
            sourceIds = new[] { match.BundleId }
        }));

        var result = await new QueryIncidentsUseCase(search, model)
            .ExecuteAsync("What query was slow?", null, null, 5);

        var source = Assert.Single(result.Sources);
        Assert.Equal("SlowDatabase", source.Scenario);
        Assert.All(source.LogContents.Concat(source.TraceContents),
            item => Assert.DoesNotContain("ExternalApiTimeout", item));
    }

    [Fact]
    public async Task QueryReturnsTheModelsAnswerWithoutReplacingItsWording()
    {
        var match = CreateMatch();
        var search = new SearchIncidentKnowledgeUseCase(
            new StubRepository([match]), new StubEmbeddingClient());
        var model = new StubModelClient(JsonSerializer.Serialize(new
        {
            answer = "The database query was executed and caused the timeout.",
            sourceIds = new[] { match.BundleId }
        }));

        var result = await new QueryIncidentsUseCase(search, model)
            .ExecuteAsync("What query caused the timeout?", null, null, 5);

        Assert.Equal("The database query was executed and caused the timeout.", result.Answer);
    }

    [Fact]
    public async Task QueryRejectsModelCitationThatWasNotRetrieved()
    {
        var search = new SearchIncidentKnowledgeUseCase(
            new StubRepository([CreateMatch()]), new StubEmbeddingClient());
        var model = new StubModelClient(JsonSerializer.Serialize(new
        {
            answer = "Invented answer",
            sourceIds = new[] { Guid.NewGuid() }
        }));
        var useCase = new QueryIncidentsUseCase(search, model);

        var exception = await Assert.ThrowsAsync<StructuredModelException>(() =>
            useCase.ExecuteAsync("What happened?", null, null, 5));

        Assert.Contains("not retrieved", exception.Message);
    }

    [Fact]
    public async Task QueryDoesNotCallModelWhenNoEvidenceExists()
    {
        var search = new SearchIncidentKnowledgeUseCase(
            new StubRepository([]), new StubEmbeddingClient());
        var model = new StubModelClient("{}");
        var useCase = new QueryIncidentsUseCase(search, model);

        var result = await useCase.ExecuteAsync("Unknown incident", null, null, 5);

        Assert.Null(result.Model);
        Assert.Empty(result.Sources);
        Assert.Equal(0, model.CallCount);
    }

    private static RagEvidenceMatch CreateMatch()
    {
        var ordinaryLogs = string.Join('\n', Enumerable.Range(1, 21)
            .Select(index => $"- [Loki] Ordinary older error {index}"));
        var content = $"""
            Alert: IncidentLabOrderFailure
            Scenario: SlowDatabase
            Simulation: true
            Evidence:
            {ordinaryLogs}
            - [Loki] Service 'incidentlab-order-api' emitted an ERROR log: HTTP 504 DiagnosticStatement=SELECT id, status, total FROM orders WHERE id = @orderId
            - [Tempo] Span 'orders.get' failed: Order database timed out. TraceId=abc123
            """;
        return new RagEvidenceMatch(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "IncidentLabOrderFailure",
            "incidentlab-order-api",
            "local",
            content,
            "embeddinggemma",
            0.92,
            new DateTimeOffset(2026, 8, 10, 18, 44, 51, TimeSpan.Zero));
    }

    private sealed class StubEmbeddingClient : IEmbeddingClient
    {
        public Task<EmbeddingResult> EmbedAsync(string text, CancellationToken cancellationToken) =>
            Task.FromResult(new EmbeddingResult("embeddinggemma", [1f, 0f]));
    }

    private sealed class StubRepository(IReadOnlyList<RagEvidenceMatch> matches)
        : IRagKnowledgeRepository
    {
        public string? Model { get; private set; }
        public string? Service { get; private set; }
        public string? Environment { get; private set; }

        public Task<IReadOnlyList<RagEvidenceMatch>> SearchRecentAsync(
            string? service, string? environment, int limit, CancellationToken cancellationToken)
        {
            Service = service;
            Environment = environment;
            return Task.FromResult<IReadOnlyList<RagEvidenceMatch>>(
                matches.OrderByDescending(item => item.CreatedAt).Take(limit).ToArray());
        }

        public Task<IReadOnlyList<RagEvidenceMatch>> SearchAsync(
            float[] embedding,
            string embeddingModel,
            string? service,
            string? environment,
            int limit,
            CancellationToken cancellationToken)
        {
            Model = embeddingModel;
            Service = service;
            Environment = environment;
            return Task.FromResult(matches);
        }
    }

    private sealed class StubModelClient(string output) : IStructuredModelClient
    {
        public int CallCount { get; private set; }

        public Task<StructuredModelResponse> GenerateAsync(
            StructuredModelRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new StructuredModelResponse("test-model", output));
        }
    }
}
