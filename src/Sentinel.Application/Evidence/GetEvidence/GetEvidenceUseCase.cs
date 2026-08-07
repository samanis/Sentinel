using Sentinel.Domain.Evidence;

namespace Sentinel.Application.Evidence.GetEvidence;

public sealed class GetEvidenceUseCase(IEvidenceRepository repository)
{
    public async Task<EvidenceDetails?> ExecuteAsync(
        EvidenceId evidenceId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var evidence = await repository.GetByIdAsync(evidenceId, cancellationToken);
        return evidence is null ? null : EvidenceDetails.From(evidence);
    }
}
