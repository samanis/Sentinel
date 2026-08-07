using System.Net;
using System.Text;
using Sentinel.Application.Evidence.MetricIngestion;
using Sentinel.Domain.Incidents;
using Sentinel.Infrastructure.Observability;

namespace Sentinel.UnitTests.Evidence;

public sealed class MetricIngestionTests
{
    [Fact]
    public async Task PrometheusSourceMapsAggregateVectors()
    {
        using var client = new HttpClient(new StubHandler("""
            {"status":"success","data":{"resultType":"vector","result":[{
              "metric":{"incidentlab_scenario":"DependencyTimeout"},
              "value":[1785855600.5,"5"]
            }]}}
            """)) { BaseAddress = new Uri("http://prometheus:9090/") };

        var metrics = await new PrometheusMetricSource(client).QueryAsync(new MetricQuery(
            "incidentlab-order-api",
            DateTimeOffset.FromUnixTimeSeconds(1785855000),
            DateTimeOffset.FromUnixTimeSeconds(1785855600)), default);

        Assert.Equal(3, metrics.Count);
        Assert.All(metrics, metric => Assert.Equal(5, metric.Value));
        Assert.All(metrics, metric => Assert.Equal("DependencyTimeout", metric.Scenario));
    }

    [Fact]
    public void NormalizerSkipsZeroAndNonFiniteValues()
    {
        var now = DateTimeOffset.UtcNow;
        var metrics = new[]
        {
            Observation("cumulative_request_failures", 5, "count", now),
            Observation("cumulative_requests", 0, "count", now),
            Observation("cumulative_request_duration_p95", double.NaN, "ms", now)
        };

        var evidence = new DeterministicMetricEvidenceNormalizer().Normalize(
            IncidentId.New(), metrics, now.AddMinutes(-5), now);

        var item = Assert.Single(evidence);
        Assert.Contains("5 failed requests", item.Summary);
        Assert.StartsWith("prometheus://metrics/cumulative_request_failures", item.SourceReference);
    }

    [Fact]
    public async Task PrometheusSourceRejectsMalformedPayload()
    {
        using var client = new HttpClient(new StubHandler("not-json"))
            { BaseAddress = new Uri("http://prometheus:9090/") };

        var exception = await Assert.ThrowsAsync<MetricSourceException>(() =>
            new PrometheusMetricSource(client).QueryAsync(new MetricQuery(
                "orders", DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow), default));

        Assert.Equal("InvalidPayload", exception.FailureCategory);
        Assert.NotNull(exception.PayloadHash);
    }

    private static MetricObservation Observation(
        string name, double value, string unit, DateTimeOffset observedAt) => new(
            name, value, unit, observedAt, "incidentlab-order-api",
            "DependencyTimeout", "query");

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
