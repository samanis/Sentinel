using Sentinel.Domain.Evidence;

namespace Sentinel.Domain.Investigations;

public sealed record HypothesisEvidenceReference(
    EvidenceId EvidenceId,
    HypothesisEvidenceRole Role);
