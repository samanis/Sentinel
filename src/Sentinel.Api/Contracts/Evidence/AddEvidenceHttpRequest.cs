using Sentinel.Domain.Evidence;

namespace Sentinel.Api.Contracts.Evidence;

public sealed record AddEvidenceHttpRequest(
    EvidenceType Type,
    string SourceSystem,
    string SourceReference,
    DateTimeOffset ObservedAt,
    string Summary,
    string? SourceTraceId = null,
    string? SourceSpanId = null,
    string? SourceService = null);
