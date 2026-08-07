using System.Net;
using System.Text;
using Sentinel.Application.Abstractions;
using Sentinel.Application.Evidence.AddEvidence;
using Sentinel.Application.Evidence.TraceIngestion;
using Sentinel.Domain.Incidents;
using Sentinel.Infrastructure.Evidence;
using Sentinel.Infrastructure.Incidents;
using Sentinel.Infrastructure.Observability;

namespace Sentinel.UnitTests.Evidence;

public sealed class TraceIngestionTests
{
    private const string TraceId = "ad2eaedb322650f08b0a3caf608ed4fa";
    private static readonly DateTimeOffset Now = new(2026, 8, 3, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task TempoSourceParsesCanonicalTraceObservation()
    {
        using var client = CreateClient(HttpStatusCode.OK, TempoPayload);
        var source = new TempoTraceSource(client);

        var trace = await source.GetTraceAsync(TraceId, CancellationToken.None);

        Assert.NotNull(trace);
        Assert.Equal(TraceId, trace.TraceId);
        var span = Assert.Single(trace.Spans);
        Assert.Equal("incidentlab-order-api", span.ServiceName);
        Assert.Equal("orders.get", span.Name);
        Assert.True(span.IsError);
        Assert.Equal("DependencyTimeout", span.Attributes["incidentlab.scenario"]);
        Assert.Equal("dependency.failure", Assert.Single(span.Events));
    }

    [Fact]
    public async Task TempoSourceReturnsNullForUnknownTrace()
    {
        using var client = CreateClient(HttpStatusCode.NotFound, string.Empty);
        var source = new TempoTraceSource(client);

        var trace = await source.GetTraceAsync(TraceId, CancellationToken.None);

        Assert.Null(trace);
    }

    [Fact]
    public async Task TempoSourceRejectsMalformedPayload()
    {
        using var client = CreateClient(HttpStatusCode.OK, "{\"unexpected\":true}");
        var source = new TempoTraceSource(client);

        var exception = await Assert.ThrowsAsync<TraceSourceException>(() =>
            source.GetTraceAsync(TraceId, CancellationToken.None));

        Assert.Contains("invalid trace payload", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("MissingRequiredField", exception.FailureCategory);
        Assert.Equal("batches", exception.InvalidField);
        Assert.Equal(64, exception.PayloadHash?.Length);
    }

    [Fact]
    public async Task ImportPersistsOnlyErrorSpansAndIsIdempotent()
    {
        var incident = Incident.Create(
            "Order API timeout",
            "order-api",
            Now.AddMinutes(-10),
            Sentinel.Domain.Incidents.IncidentSeverity.High,
            Now.AddMinutes(-5));
        var incidents = new InMemoryIncidentRepository();
        await incidents.AddAsync(incident, CancellationToken.None);
        var evidence = new InMemoryEvidenceRepository();
        var trace = new TraceObservation(
            TraceId,
            [
                Span("error-span", true),
                Span("successful-span", false)
            ]);
        var useCase = new ImportTraceEvidenceUseCase(
            new StubTraceSource(trace),
            new DeterministicTraceEvidenceNormalizer(),
            incidents,
            evidence,
            new StubClock(Now));

        var first = await useCase.ExecuteAsync(incident.Id, TraceId, CancellationToken.None);
        var second = await useCase.ExecuteAsync(incident.Id, TraceId, CancellationToken.None);
        var stored = await evidence.ListByIncidentIdAsync(incident.Id, CancellationToken.None);

        Assert.Equal(ImportTraceEvidenceStatus.Imported, first.Status);
        Assert.True(Assert.Single(first.Evidence).WasCreated);
        Assert.False(Assert.Single(second.Evidence).WasCreated);
        var item = Assert.Single(stored);
        Assert.Contains("error-span", item.Summary, StringComparison.Ordinal);
        Assert.Contains("HTTP status: 504", item.Summary, StringComparison.Ordinal);
        Assert.Equal(TraceId, item.SourceTraceId);
        Assert.NotNull(item.SourceSpanId);
        Assert.Equal("incidentlab-order-api", item.SourceService);
        Assert.Equal(2, first.SpanCount);
        Assert.Equal(1, first.ErrorSpanCount);
    }

    [Fact]
    public async Task ImportPersistsNothingWhenAnyNormalizedItemIsInvalid()
    {
        var incident = Incident.Create(
            "Order API timeout",
            "order-api",
            Now.AddMinutes(-10),
            IncidentSeverity.High,
            Now.AddMinutes(-5));
        var incidents = new InMemoryIncidentRepository();
        await incidents.AddAsync(incident, CancellationToken.None);
        var evidence = new InMemoryEvidenceRepository();
        var trace = new TraceObservation(
            TraceId,
            [
                Span("valid-error", true),
                Span("future-error", true) with { StartedAt = Now.AddMinutes(1) }
            ]);
        var useCase = new ImportTraceEvidenceUseCase(
            new StubTraceSource(trace),
            new DeterministicTraceEvidenceNormalizer(),
            incidents,
            evidence,
            new StubClock(Now));

        var exception = await Assert.ThrowsAsync<TraceSourceException>(() =>
            useCase.ExecuteAsync(incident.Id, TraceId, CancellationToken.None));
        var stored = await evidence.ListByIncidentIdAsync(incident.Id, CancellationToken.None);

        Assert.Equal("InvalidTimestamp", exception.FailureCategory);
        Assert.Empty(stored);
    }

    [Fact]
    public async Task ImportDoesNotCallTempoForUnknownIncident()
    {
        var source = new CountingTraceSource();
        var incidents = new InMemoryIncidentRepository();
        var evidence = new InMemoryEvidenceRepository();
        var useCase = new ImportTraceEvidenceUseCase(
            source,
            new DeterministicTraceEvidenceNormalizer(),
            incidents,
            evidence,
            new StubClock(Now));

        var result = await useCase.ExecuteAsync(
            IncidentId.New(),
            TraceId,
            CancellationToken.None);

        Assert.Equal(ImportTraceEvidenceStatus.IncidentNotFound, result.Status);
        Assert.Equal(0, source.CallCount);
    }

    private static TraceSpanObservation Span(string name, bool isError) => new(
        Convert.ToBase64String(Encoding.UTF8.GetBytes(name)),
        "incidentlab-order-api",
        name,
        Now.AddMinutes(-2),
        isError,
        isError ? "Order database timed out" : null,
        new Dictionary<string, string>
        {
            ["http.response.status_code"] = isError ? "504" : "200",
            ["incidentlab.scenario"] = "DependencyTimeout"
        },
        isError ? ["dependency.failure"] : []);

    private static HttpClient CreateClient(HttpStatusCode statusCode, string content) => new(
        new StubHttpMessageHandler(statusCode, content))
    {
        BaseAddress = new Uri("http://tempo:3200/")
    };

    private sealed class StubHttpMessageHandler(HttpStatusCode statusCode, string content)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });
    }

    private sealed class StubTraceSource(TraceObservation trace) : ITraceSource
    {
        public Task<TraceObservation?> GetTraceAsync(
            string traceId,
            CancellationToken cancellationToken) => Task.FromResult<TraceObservation?>(trace);
    }

    private sealed class CountingTraceSource : ITraceSource
    {
        public int CallCount { get; private set; }

        public Task<TraceObservation?> GetTraceAsync(
            string traceId,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult<TraceObservation?>(null);
        }
    }

    private sealed class StubClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private const string TempoPayload = """
        {
          "batches": [{
            "resource": {"attributes": [
              {"key":"service.name","value":{"stringValue":"incidentlab-order-api"}}
            ]},
            "scopeSpans": [{"spans": [{
              "spanId":"5fHyeXxo5xs=",
              "name":"orders.get",
              "startTimeUnixNano":"1785714207567861800",
              "attributes":[
                {"key":"incidentlab.scenario","value":{"stringValue":"DependencyTimeout"}},
                {"key":"http.response.status_code","value":{"intValue":"504"}}
              ],
              "events":[{"name":"dependency.failure"}],
              "status":{"message":"Order database timed out","code":"STATUS_CODE_ERROR"}
            }]}]
          }]
        }
        """;
}
