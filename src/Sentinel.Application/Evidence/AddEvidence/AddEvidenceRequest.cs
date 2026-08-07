using Sentinel.Domain.Evidence;
using Sentinel.Domain.Incidents;

namespace Sentinel.Application.Evidence.AddEvidence;

public sealed record AddEvidenceRequest(
    IncidentId IncidentId,
    EvidenceType Type,
    string SourceSystem,
    string SourceReference,
    DateTimeOffset ObservedAt,
    string Summary,
    string? SourceTraceId = null,
    string? SourceSpanId = null,
    string? SourceService = null);
