using Sentinel.Application.Incidents;
using Sentinel.Domain.Incidents;

namespace Sentinel.Api.Contracts.Incidents;

public sealed record IncidentResponse(
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
    public static IncidentResponse From(IncidentDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);

        return new IncidentResponse(
            details.Id,
            details.Title,
            details.Service,
            details.StartedAt,
            details.Severity,
            details.Status,
            details.CreatedAt,
            details.UpdatedAt,
            details.ResolvedAt,
            details.ClosedAt);
    }
}
