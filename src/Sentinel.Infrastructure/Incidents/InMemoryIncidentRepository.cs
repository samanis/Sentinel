using System.Collections.Concurrent;
using Sentinel.Application.Incidents;
using Sentinel.Domain.Incidents;

namespace Sentinel.Infrastructure.Incidents;

public sealed class InMemoryIncidentRepository : IIncidentRepository
{
    private readonly ConcurrentDictionary<IncidentId, Incident> _incidents = new();

    public Task AddAsync(Incident incident, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(incident);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_incidents.TryAdd(incident.Id, incident))
        {
            throw new InvalidOperationException($"Incident {incident.Id} already exists.");
        }

        return Task.CompletedTask;
    }

    public Task<Incident?> GetByIdAsync(IncidentId id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _incidents.TryGetValue(id, out var incident);

        return Task.FromResult(incident);
    }
}
