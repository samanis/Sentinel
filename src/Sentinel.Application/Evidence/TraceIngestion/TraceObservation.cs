namespace Sentinel.Application.Evidence.TraceIngestion;

public sealed record TraceObservation(
    string TraceId,
    IReadOnlyList<TraceSpanObservation> Spans);

public sealed record TraceSpanObservation(
    string SpanId,
    string ServiceName,
    string Name,
    DateTimeOffset StartedAt,
    bool IsError,
    string? StatusMessage,
    IReadOnlyDictionary<string, string> Attributes,
    IReadOnlyList<string> Events);
