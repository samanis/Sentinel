using Sentinel.Domain.Incidents;

namespace Sentinel.Api.Contracts.Incidents;

public sealed record CreateIncidentHttpRequest(
    string Title,
    string Service,
    DateTimeOffset StartedAt,
    IncidentSeverity Severity);
