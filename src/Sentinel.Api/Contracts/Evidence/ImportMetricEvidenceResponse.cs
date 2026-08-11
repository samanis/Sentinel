using Sentinel.Application.Evidence.MetricIngestion;

namespace Sentinel.Api.Contracts.Evidence;

public sealed record ImportMetricEvidenceResponse(
    int MetricCount,
    int EligibleMetricCount,
    int CreatedEvidenceCount,
    int ExistingEvidenceCount,
    IReadOnlyList<EvidenceResponse> Evidence)
{
    public static ImportMetricEvidenceResponse From(ImportMetricEvidenceResult result) => new(
        result.MetricCount,
        result.EligibleMetricCount,
        result.Evidence.Count(item => item.WasCreated),
        result.Evidence.Count(item => !item.WasCreated),
        result.Evidence.Select(item => EvidenceResponse.From(item.Evidence)).ToArray());
}
