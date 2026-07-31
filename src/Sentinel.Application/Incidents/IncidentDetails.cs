using Sentinel.Domain.Incidents;

namespace Sentinel.Application.Incidents;

public sealed record IncidentDetails(
    Guid Id,
    string Title,
    string Service,
    DateTimeOffset StartedAt,
    IncidentSeverity Severity,
    IncidentStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset? ClosedAt)
{
    public static IncidentDetails From(Incident incident)
    {
        ArgumentNullException.ThrowIfNull(incident);

        return new IncidentDetails(
            incident.Id.Value,
            incident.Title,
            incident.Service,
            incident.StartedAt,
            incident.Severity,
            incident.Status,
            incident.CreatedAt,
            incident.UpdatedAt,
            incident.ResolvedAt,
            incident.ClosedAt);
    }
}
