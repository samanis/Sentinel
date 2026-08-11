using Microsoft.EntityFrameworkCore;
using Sentinel.Application.Incidents;
using Sentinel.Domain.Incidents;

namespace Sentinel.Infrastructure.Persistence;

public sealed class PostgresIncidentRepository(SentinelDbContext dbContext)
    : IIncidentRepository
{
    public async Task AddAsync(Incident incident, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(incident);

        dbContext.Incidents.Add(incident);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<Incident?> GetByIdAsync(
        IncidentId id,
        CancellationToken cancellationToken) =>
        dbContext.Incidents
            .AsNoTracking()
            .SingleOrDefaultAsync(incident => incident.Id == id, cancellationToken);
}
