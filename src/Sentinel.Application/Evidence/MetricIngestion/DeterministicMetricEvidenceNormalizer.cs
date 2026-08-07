using System.Globalization;
using Sentinel.Domain.Incidents;

namespace Sentinel.Application.Evidence.MetricIngestion;

public interface IMetricEvidenceNormalizer
{
    IReadOnlyList<NormalizedMetricEvidence> Normalize(
        IncidentId incidentId,
        IReadOnlyList<MetricObservation> metrics,
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEnd);
}

public sealed record NormalizedMetricEvidence(
    string SourceReference,
    DateTimeOffset ObservedAt,
    string Summary,
    string SourceService);

public sealed class DeterministicMetricEvidenceNormalizer : IMetricEvidenceNormalizer
{
    public IReadOnlyList<NormalizedMetricEvidence> Normalize(
        IncidentId incidentId,
        IReadOnlyList<MetricObservation> metrics,
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEnd)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        return metrics
            .Where(metric => double.IsFinite(metric.Value) && metric.Value > 0)
            .Select(metric => new NormalizedMetricEvidence(
                $"prometheus://metrics/{Uri.EscapeDataString(metric.Name)}" +
                    $"?from={Uri.EscapeDataString(rangeStart.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))}" +
                    $"&to={Uri.EscapeDataString(rangeEnd.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))}" +
                    (metric.Scenario is null ? string.Empty : $"&scenario={Uri.EscapeDataString(metric.Scenario)}"),
                metric.ObservedAt,
                BuildSummary(metric),
                metric.ServiceName))
            .ToArray();
    }

    private static string BuildSummary(MetricObservation metric)
    {
        var scenario = metric.Scenario is null ? string.Empty : $" for scenario '{metric.Scenario}'";
        return metric.Name switch
        {
            "cumulative_request_failures" => $"At the end of the query window, service '{metric.ServiceName}' had a cumulative total of {metric.Value.ToString("0.###", CultureInfo.InvariantCulture)} failed requests{scenario}.",
            "cumulative_requests" => $"At the end of the query window, service '{metric.ServiceName}' had a cumulative total of {metric.Value.ToString("0.###", CultureInfo.InvariantCulture)} requests{scenario}.",
            "cumulative_request_duration_p95" => $"At the end of the query window, service '{metric.ServiceName}' had cumulative-histogram p95 order latency of {metric.Value.ToString("0.###", CultureInfo.InvariantCulture)} ms{scenario}.",
            _ => $"Service '{metric.ServiceName}' reported {metric.Name}={metric.Value.ToString("0.###", CultureInfo.InvariantCulture)} {metric.Unit}{scenario}."
        };
    }
}
