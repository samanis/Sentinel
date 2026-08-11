namespace Sentinel.Application.Evidence.LogIngestion;

public interface ILogEvidenceNormalizer
{
    IReadOnlyList<NormalizedLogEvidence> Normalize(
        IReadOnlyList<LogObservation> logs);
}

public sealed record NormalizedLogEvidence(
    string SourceReference,
    DateTimeOffset ObservedAt,
    string Summary,
    string? SourceTraceId,
    string? SourceSpanId,
    string SourceService);

public sealed class DeterministicLogEvidenceNormalizer : ILogEvidenceNormalizer
{
    private static readonly HashSet<string> EligibleSeverities = new(
        ["WARN", "WARNING", "ERROR", "FATAL", "CRITICAL"],
        StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<NormalizedLogEvidence> Normalize(
        IReadOnlyList<LogObservation> logs)
    {
        ArgumentNullException.ThrowIfNull(logs);

        return logs
            .Where(log => EligibleSeverities.Contains(log.Severity))
            .Select(log => new NormalizedLogEvidence(
                $"loki://logs/{Uri.EscapeDataString(log.ServiceName)}/{log.TimestampNanoseconds}",
                log.ObservedAt,
                $"Service '{log.ServiceName}' emitted a {log.Severity.ToUpperInvariant()} log: {log.Body}",
                log.TraceId,
                log.SpanId,
                log.ServiceName))
            .ToArray();
    }
}
