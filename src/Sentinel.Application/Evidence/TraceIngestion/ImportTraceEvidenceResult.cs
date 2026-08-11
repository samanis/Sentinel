namespace Sentinel.Application.Evidence.TraceIngestion;

public enum ImportTraceEvidenceStatus
{
    Imported = 1,
    IncidentNotFound = 2,
    TraceNotFound = 3
}

public sealed record ImportTraceEvidenceResult(
    ImportTraceEvidenceStatus Status,
    IReadOnlyList<AddEvidence.AddEvidenceResult> Evidence,
    int SpanCount,
    int ErrorSpanCount);
