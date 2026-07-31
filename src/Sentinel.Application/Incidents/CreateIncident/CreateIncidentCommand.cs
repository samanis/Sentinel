using Sentinel.Domain.Incidents;

namespace Sentinel.Application.Incidents.CreateIncident;

public sealed record CreateIncidentCommand(
    string Title,
    string Service,
    DateTimeOffset StartedAt,
    IncidentSeverity Severity);
