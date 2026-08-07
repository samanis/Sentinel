using Sentinel.Application.Evidence.TraceIngestion;

namespace Sentinel.Api.Contracts.Evidence;

public sealed record ImportTraceEvidenceResponse(
    string TraceId,
    int CreatedCount,
    int ExistingCount,
    IReadOnlyList<EvidenceResponse> Evidence)
{
    public static ImportTraceEvidenceResponse From(
        string traceId,
        ImportTraceEvidenceResult result) => new(
        traceId,
        result.Evidence.Count(item => item.WasCreated),
        result.Evidence.Count(item => !item.WasCreated),
        result.Evidence.Select(item => EvidenceResponse.From(item.Evidence)).ToArray());
}
