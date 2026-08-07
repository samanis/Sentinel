using Sentinel.Application.Evidence;
using Sentinel.Domain.Evidence;

namespace Sentinel.Api.Contracts.Evidence;

public sealed record EvidenceResponse(
    Guid Id,
    Guid IncidentId,
    EvidenceType Type,
    string SourceSystem,
    string SourceReference,
    DateTimeOffset ObservedAt,
    string Summary,
    string? SourceTraceId,
    string? SourceSpanId,
    string? SourceService,
    string ContentHash,
    EvidenceVerificationStatus VerificationStatus,
    DateTimeOffset CreatedAt)
{
    public static EvidenceResponse From(EvidenceDetails details) => new(
        details.Id,
        details.IncidentId,
        details.Type,
        details.SourceSystem,
        details.SourceReference,
        details.ObservedAt,
        details.Summary,
        details.SourceTraceId,
        details.SourceSpanId,
        details.SourceService,
        details.ContentHash,
        details.VerificationStatus,
        details.CreatedAt);
}
