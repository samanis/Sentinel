using System.Security.Cryptography;
using System.Text;

namespace Sentinel.Domain.Ingestion;

public sealed class IngestionObservation
{
    public const int MaxSourceSystemLength = 20;
    public const int MaxSourceReferenceLength = 500;
    public const int MaxSummaryLength = 2_000;

    private IngestionObservation(
        Guid id,
        IngestionRunId ingestionRunId,
        string sourceSystem,
        string sourceReference,
        DateTimeOffset observedAt,
        string summary,
        string? traceId,
        string? spanId,
        string service,
        string contentHash,
        DateTimeOffset createdAt)
    {
        Id = id;
        IngestionRunId = ingestionRunId;
        SourceSystem = sourceSystem;
        SourceReference = sourceReference;
        ObservedAt = observedAt;
        Summary = summary;
        TraceId = traceId;
        SpanId = spanId;
        Service = service;
        ContentHash = contentHash;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }
    public IngestionRunId IngestionRunId { get; }
    public string SourceSystem { get; }
    public string SourceReference { get; }
    public DateTimeOffset ObservedAt { get; }
    public string Summary { get; }
    public string? TraceId { get; }
    public string? SpanId { get; }
    public string Service { get; }
    public string ContentHash { get; }
    public DateTimeOffset CreatedAt { get; }

    public static IngestionObservation Create(
        IngestionRunId ingestionRunId,
        string sourceSystem,
        string sourceReference,
        DateTimeOffset observedAt,
        string summary,
        string? traceId,
        string? spanId,
        string service,
        DateTimeOffset createdAt)
    {
        var normalizedSource = Required(sourceSystem, MaxSourceSystemLength, nameof(sourceSystem));
        var normalizedReference = Required(sourceReference, MaxSourceReferenceLength, nameof(sourceReference));
        var normalizedSummary = Required(summary, MaxSummaryLength, nameof(summary));
        var normalizedService = Required(service, AlertOccurrence.MaxServiceLength, nameof(service));
        var hashInput = $"{normalizedSource}\n{normalizedReference}";
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(hashInput)));
        return new IngestionObservation(
            Guid.NewGuid(), ingestionRunId, normalizedSource, normalizedReference,
            observedAt.ToUniversalTime(), normalizedSummary, Optional(traceId, 64),
            Optional(spanId, 64), normalizedService, hash, createdAt.ToUniversalTime());
    }

    private static string Required(string value, int maximum, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A value is required.", parameter);
        var result = value.Trim();
        if (result.Length > maximum) throw new ArgumentException($"The value cannot exceed {maximum} characters.", parameter);
        return result;
    }

    private static string? Optional(string? value, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var result = value.Trim();
        if (result.Length > maximum) throw new ArgumentException($"The value cannot exceed {maximum} characters.");
        return result;
    }
}
