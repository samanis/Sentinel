using Sentinel.Domain.Evidence;
using Sentinel.Domain.Incidents;

namespace Sentinel.Application.Evidence;

public interface IEvidenceRepository
{
    Task<IReadOnlyList<EvidencePersistenceResult>> AddMissingAsync(
        IReadOnlyCollection<EvidenceItem> evidence,
        CancellationToken cancellationToken);

    Task<EvidenceItem?> GetByIdAsync(EvidenceId id, CancellationToken cancellationToken);

    Task<IReadOnlyList<EvidenceItem>> ListByIncidentIdAsync(
        IncidentId incidentId,
        CancellationToken cancellationToken);
}

public sealed record EvidencePersistenceResult(EvidenceItem Evidence, bool WasCreated);
