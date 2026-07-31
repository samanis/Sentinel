namespace Sentinel.Application.Incidents.GetIncident;

public sealed class GetIncidentHandler
{
    private readonly IIncidentRepository _repository;

    public GetIncidentHandler(IIncidentRepository repository)
    {
        _repository = repository;
    }

    public async Task<IncidentDetails?> HandleAsync(
        GetIncidentQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        if (query.IncidentId.Value == Guid.Empty)
        {
            throw new ArgumentException("The incident ID is required.", nameof(query));
        }

        var incident = await _repository.GetByIdAsync(query.IncidentId, cancellationToken);

        return incident is null ? null : IncidentDetails.From(incident);
    }
}
