using Sentinel.Domain.Incidents;

namespace Sentinel.Application.Evidence.TraceIngestion;

public interface ITraceEvidenceNormalizer
{
    IReadOnlyList<NormalizedTraceEvidence> Normalize(
        IncidentId incidentId,
        TraceObservation trace);
}

public sealed record NormalizedTraceEvidence(
    string SourceReference,
    DateTimeOffset ObservedAt,
    string Summary,
    string SourceTraceId,
    string SourceSpanId,
    string SourceService);
