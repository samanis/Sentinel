using Sentinel.Domain.Incidents;
using Sentinel.Application.Incidents;

namespace Sentinel.Application.Evidence.ListIncidentEvidence;

public sealed class ListIncidentEvidenceUseCase(
    IEvidenceRepository evidenceRepository,
    IIncidentRepository incidentRepository)
{
    public async Task<IReadOnlyList<EvidenceDetails>?> ExecuteAsync(
        IncidentId incidentId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var incident = await incidentRepository.GetByIdAsync(incidentId, cancellationToken);
        if (incident is null)
        {
            return null;
        }

        var evidence = await evidenceRepository.ListByIncidentIdAsync(
            incidentId,
            cancellationToken);
        return evidence.Select(EvidenceDetails.From).ToArray();
    }
}
