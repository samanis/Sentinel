using Sentinel.Domain.Incidents;

namespace Sentinel.Application.Incidents.CreateIncident;

public sealed record CreateIncidentRequest(
    string Title,
    string Service,
    DateTimeOffset StartedAt,
    IncidentSeverity Severity);
