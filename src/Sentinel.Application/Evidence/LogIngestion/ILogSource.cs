namespace Sentinel.Application.Evidence.LogIngestion;

public interface ILogSource
{
    Task<IReadOnlyList<LogObservation>> QueryAsync(
        LogQuery query,
        CancellationToken cancellationToken);
}

public sealed record LogQuery(
    string ServiceName,
    DateTimeOffset From,
    DateTimeOffset To,
    int Limit = 500);

public sealed record LogObservation(
    string TimestampNanoseconds,
    DateTimeOffset ObservedAt,
    string ServiceName,
    string Severity,
    string Body,
    string? TraceId,
    string? SpanId,
    IReadOnlyDictionary<string, string> Attributes);
