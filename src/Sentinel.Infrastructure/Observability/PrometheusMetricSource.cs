using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Sentinel.Application.Evidence.MetricIngestion;

namespace Sentinel.Infrastructure.Observability;

public sealed class PrometheusMetricSource(HttpClient httpClient) : IMetricSource
{
    public async Task<IReadOnlyList<MetricObservation>> QueryAsync(
        MetricQuery query,
        CancellationToken cancellationToken)
    {
        var selector = $"service_name=\"{EscapePromQl(query.ServiceName)}\"";
        var definitions = new[]
        {
            new Definition("cumulative_request_failures", "count",
                $"sum by (incidentlab_scenario) (incidentlab_order_failures_total{{{selector}}})"),
            new Definition("cumulative_requests", "count",
                $"sum by (incidentlab_scenario) (incidentlab_order_requests_total{{{selector}}})"),
            new Definition("cumulative_request_duration_p95", "ms",
                $"histogram_quantile(0.95, sum by (le, incidentlab_scenario) (incidentlab_order_duration_milliseconds_bucket{{{selector}}}))")
        };
        var observations = new List<MetricObservation>();
        foreach (var definition in definitions)
        {
            observations.AddRange(await QueryOneAsync(definition, query, cancellationToken));
        }
        return observations;
    }

    private async Task<List<MetricObservation>> QueryOneAsync(
        Definition definition, MetricQuery query, CancellationToken cancellationToken)
    {
        string? payloadHash = null;
        try
        {
            var uri = $"api/v1/query?query={Uri.EscapeDataString(definition.Query)}" +
                $"&time={Uri.EscapeDataString(query.To.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))}";
            using var response = await httpClient.GetAsync(uri, cancellationToken);
            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            payloadHash = Convert.ToHexString(SHA256.HashData(payload));
            using var document = JsonDocument.Parse(payload);
            return Parse(definition, query, document.RootElement);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new MetricSourceException("Timeout", "Prometheus did not respond before the request timeout.");
        }
        catch (HttpRequestException exception)
        {
            throw new MetricSourceException("SourceUnavailable", "Prometheus metric query failed.", innerException: exception);
        }
        catch (Exception exception) when (exception is JsonException or FormatException or OverflowException)
        {
            throw new MetricSourceException("InvalidPayload", "Prometheus returned an invalid metric payload.", payloadHash, exception);
        }
    }

    private static List<MetricObservation> Parse(
        Definition definition, MetricQuery query, JsonElement root)
    {
        if (root.GetProperty("status").GetString() != "success")
            throw new JsonException("Prometheus query was not successful.");
        var result = new List<MetricObservation>();
        foreach (var item in root.GetProperty("data").GetProperty("result").EnumerateArray())
        {
            var metric = item.GetProperty("metric");
            var value = item.GetProperty("value").EnumerateArray().ToArray();
            if (value.Length != 2) throw new JsonException("Invalid Prometheus sample.");
            result.Add(new MetricObservation(
                definition.Name,
                double.Parse(value[1].GetString()!, CultureInfo.InvariantCulture),
                definition.Unit,
                DateTimeOffset.FromUnixTimeMilliseconds((long)(value[0].GetDouble() * 1000)),
                query.ServiceName,
                metric.TryGetProperty("incidentlab_scenario", out var scenario) ? scenario.GetString() : null,
                definition.Query));
        }
        return result;
    }

    private static string EscapePromQl(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    private sealed record Definition(string Name, string Unit, string Query);
}
