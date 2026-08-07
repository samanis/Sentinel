using Sentinel.Domain.Evidence;

namespace Sentinel.Application.Evidence;

public sealed record EvidenceDetails(
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
    public static EvidenceDetails From(EvidenceItem evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        return new EvidenceDetails(
            evidence.Id.Value,
            evidence.IncidentId.Value,
            evidence.Type,
            evidence.SourceSystem,
            evidence.SourceReference,
            evidence.ObservedAt,
            evidence.Summary,
            evidence.SourceTraceId,
            evidence.SourceSpanId,
            evidence.SourceService,
            evidence.ContentHash,
            evidence.VerificationStatus,
            evidence.CreatedAt);
    }
}
