namespace Sentinel.Application.Incidents.GetIncident;

public sealed class GetIncidentUseCase
{
    private readonly IIncidentRepository _repository;

    public GetIncidentUseCase(IIncidentRepository repository)
    {
        _repository = repository;
    }

    public async Task<IncidentDetails?> ExecuteAsync(
        GetIncidentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.IncidentId.Value == Guid.Empty)
        {
            throw new ArgumentException("The incident ID is required.", nameof(request));
        }

        var incident = await _repository.GetByIdAsync(request.IncidentId, cancellationToken);

        return incident is null ? null : IncidentDetails.From(incident);
    }
}
