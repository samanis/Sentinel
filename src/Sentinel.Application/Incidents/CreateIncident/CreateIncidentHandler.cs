using Sentinel.Application.Abstractions;
using Sentinel.Domain.Incidents;

namespace Sentinel.Application.Incidents.CreateIncident;

public sealed class CreateIncidentHandler
{
    private readonly IClock _clock;
    private readonly IIncidentRepository _repository;

    public CreateIncidentHandler(IIncidentRepository repository, IClock clock)
    {
        _repository = repository;
        _clock = clock;
    }

    public async Task<IncidentDetails> HandleAsync(
        CreateIncidentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        var now = _clock.UtcNow;

        if (command.StartedAt > now)
        {
            throw new ArgumentException(
                "The incident start time cannot be in the future.",
                nameof(command));
        }

        var incident = Incident.Create(
            command.Title,
            command.Service,
            command.StartedAt,
            command.Severity,
            now);

        await _repository.AddAsync(incident, cancellationToken);

        return IncidentDetails.From(incident);
    }
}
