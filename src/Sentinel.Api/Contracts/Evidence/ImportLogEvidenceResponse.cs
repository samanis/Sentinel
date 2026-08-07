using Sentinel.Application.Evidence.LogIngestion;

namespace Sentinel.Api.Contracts.Evidence;

public sealed record ImportLogEvidenceResponse(
    int LogCount,
    int EligibleLogCount,
    int CreatedEvidenceCount,
    int ExistingEvidenceCount,
    IReadOnlyList<EvidenceResponse> Evidence)
{
    public static ImportLogEvidenceResponse From(ImportLogEvidenceResult result) => new(
        result.LogCount,
        result.EligibleLogCount,
        result.Evidence.Count(item => item.WasCreated),
        result.Evidence.Count(item => !item.WasCreated),
        result.Evidence.Select(item => EvidenceResponse.From(item.Evidence)).ToArray());
}
