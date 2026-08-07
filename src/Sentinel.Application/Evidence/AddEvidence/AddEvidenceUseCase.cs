using Sentinel.Application.Abstractions;
using Sentinel.Application.Incidents;
using Sentinel.Domain.Evidence;

namespace Sentinel.Application.Evidence.AddEvidence;

public sealed class AddEvidenceUseCase(
    IEvidenceRepository evidenceRepository,
    IIncidentRepository incidentRepository,
    IClock clock)
{
    public async Task<AddEvidenceResult?> ExecuteAsync(
        AddEvidenceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var incident = await incidentRepository.GetByIdAsync(
            request.IncidentId,
            cancellationToken);
        if (incident is null)
        {
            return null;
        }

        var now = clock.UtcNow;
        if (request.ObservedAt > now)
        {
            throw new ArgumentException(
                "The evidence observation time cannot be in the future.",
                nameof(request));
        }

        var evidence = EvidenceItem.Create(
            request.IncidentId,
            request.Type,
            request.SourceSystem,
            request.SourceReference,
            request.ObservedAt,
            request.Summary,
            now,
            request.SourceTraceId,
            request.SourceSpanId,
            request.SourceService);
        var persisted = await evidenceRepository.AddMissingAsync([evidence], cancellationToken);
        var result = persisted.Single();
        return new AddEvidenceResult(EvidenceDetails.From(result.Evidence), result.WasCreated);
    }
}
