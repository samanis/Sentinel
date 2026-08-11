namespace Sentinel.Api.Contracts.Evidence;

public sealed record ImportPrometheusMetricsRequest(DateTimeOffset From, DateTimeOffset To);
