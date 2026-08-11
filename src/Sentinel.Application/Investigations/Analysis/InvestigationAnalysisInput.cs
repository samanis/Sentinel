using Sentinel.Domain.Evidence;

namespace Sentinel.Application.Investigations.Analysis;

public enum EvidenceAnalysisScope
{
    Event = 1,
    ServiceWindow = 2,
    Incident = 3
}

public sealed record InvestigationAnalysisInput(
    Guid IncidentId,
    string Title,
    string Service,
    DateTimeOffset StartedAt,
    IReadOnlyList<EvidenceAnalysisInput> Evidence);

public sealed record EvidenceAnalysisInput(
    Guid Id,
    EvidenceType Type,
    EvidenceAnalysisScope Scope,
    DateTimeOffset ObservedAt,
    string Summary,
    string? TraceId,
    string? SpanId,
    string? Service);
