using Sentinel.Application.Abstractions;
using Sentinel.Domain.Incidents;

namespace Sentinel.Application.Incidents.CreateIncident;

public sealed class CreateIncidentUseCase
{
    private readonly IClock _clock;
    private readonly IIncidentRepository _repository;

    public CreateIncidentUseCase(IIncidentRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<IncidentDetails> ExecuteAsync(
        CreateIncidentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var now = _clock.UtcNow;

        if (request.StartedAt > now)
        {
            throw new ArgumentException(
                "The incident start time cannot be in the future.",
                nameof(request));
        }

        var incident = Incident.Create(
            request.Title,
            request.Service,
            request.StartedAt,
            request.Severity,
            now);

        await _repository.AddAsync(incident, cancellationToken);

        return IncidentDetails.From(incident);
    }
}
