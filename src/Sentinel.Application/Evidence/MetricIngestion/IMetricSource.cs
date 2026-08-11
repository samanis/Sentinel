namespace Sentinel.Application.Evidence.MetricIngestion;

public interface IMetricSource
{
    Task<IReadOnlyList<MetricObservation>> QueryAsync(
        MetricQuery query,
        CancellationToken cancellationToken);
}

public sealed record MetricQuery(
    string ServiceName,
    DateTimeOffset From,
    DateTimeOffset To);

public sealed record MetricObservation(
    string Name,
    double Value,
    string Unit,
    DateTimeOffset ObservedAt,
    string ServiceName,
    string? Scenario,
    string Query);
