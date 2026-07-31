using Sentinel.Domain.Incidents;

namespace Sentinel.Application.Incidents.GetIncident;

public sealed record GetIncidentRequest(IncidentId IncidentId);
