using System.Net;
using System.Text;
using Sentinel.Application.Evidence.LogIngestion;
using Sentinel.Domain.Incidents;
using Sentinel.Infrastructure.Observability;

namespace Sentinel.UnitTests.Evidence;

public sealed class LogIngestionTests
{
    [Fact]
    public async Task LokiSourceMapsLabelsAndStructuredMetadata()
    {
        using var client = new HttpClient(new StubHandler("""
            {"status":"success","data":{"resultType":"streams","result":[{
              "stream":{"service_name":"incidentlab-order-api"},
              "values":[["1785714207567861800","Database timed out",{
                "severity_text":"Error","trace_id":"0123456789abcdef0123456789abcdef","span_id":"0123456789abcdef"
              }]]}]}}
            """)) { BaseAddress = new Uri("http://loki:3100/") };

        var logs = await new LokiLogSource(client).QueryAsync(new LogQuery(
            "incidentlab-order-api",
            DateTimeOffset.FromUnixTimeSeconds(1785714000),
            DateTimeOffset.FromUnixTimeSeconds(1785714300)), default);

        var log = Assert.Single(logs);
        Assert.Equal("ERROR", log.Severity.ToUpperInvariant());
        Assert.Equal("Database timed out", log.Body);
        Assert.Equal("0123456789abcdef0123456789abcdef", log.TraceId);
    }

    [Fact]
    public void NormalizerKeepsWarningsAndErrorsOnly()
    {
        var now = DateTimeOffset.UtcNow;
        var logs = new[]
        {
            Observation("INFO", "request started", now, "1"),
            Observation("ERROR", "database timed out", now, "2")
        };

        var result = new DeterministicLogEvidenceNormalizer().Normalize(
            IncidentId.New(), logs);

        var evidence = Assert.Single(result);
        Assert.Contains("database timed out", evidence.Summary);
        Assert.Equal("loki://logs/incidentlab-order-api/2", evidence.SourceReference);
    }

    [Fact]
    public async Task LokiSourceRejectsMalformedPayload()
    {
        using var client = new HttpClient(new StubHandler("not-json"))
            { BaseAddress = new Uri("http://loki:3100/") };

        var exception = await Assert.ThrowsAsync<LogSourceException>(() =>
            new LokiLogSource(client).QueryAsync(new LogQuery(
                "orders", DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow), default));

        Assert.Equal("InvalidPayload", exception.FailureCategory);
        Assert.NotNull(exception.PayloadHash);
    }

    private static LogObservation Observation(
        string severity, string body, DateTimeOffset observedAt, string timestamp) => new(
            timestamp, observedAt, "incidentlab-order-api", severity, body,
            null, null, new Dictionary<string, string>());

    private sealed class StubHandler(string content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });
    }
}
