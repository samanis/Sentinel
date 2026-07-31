using Sentinel.Domain.Incidents;

namespace Sentinel.Application.Incidents;

public interface IIncidentRepository
{
    Task AddAsync(Incident incident, CancellationToken cancellationToken);

    Task<Incident?> GetByIdAsync(IncidentId id, CancellationToken cancellationToken);
}
