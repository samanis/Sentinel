using Sentinel.Application.Abstractions;
using Sentinel.Application.Evidence.AddEvidence;
using Sentinel.Application.Incidents;
using Sentinel.Domain.Evidence;
using Sentinel.Domain.Incidents;

namespace Sentinel.Application.Evidence.LogIngestion;

public enum ImportLogEvidenceStatus { Imported = 1, IncidentNotFound = 2 }

public sealed record ImportLogEvidenceResult(
    ImportLogEvidenceStatus Status,
    IReadOnlyList<AddEvidenceResult> Evidence,
    int LogCount,
    int EligibleLogCount);

public sealed class ImportLogEvidenceUseCase(
    ILogSource logSource,
    ILogEvidenceNormalizer normalizer,
    IIncidentRepository incidentRepository,
    IEvidenceRepository evidenceRepository,
    IClock clock)
{
    public async Task<ImportLogEvidenceResult> ExecuteAsync(
        IncidentId incidentId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        ValidateRange(from, to);
        var incident = await incidentRepository.GetByIdAsync(incidentId, cancellationToken);
        if (incident is null)
        {
            return new(ImportLogEvidenceStatus.IncidentNotFound, [], 0, 0);
        }

        var logs = await logSource.QueryAsync(
            new LogQuery(incident.Service, from, to), cancellationToken);
        var normalized = normalizer.Normalize(incidentId, logs);
        var now = clock.UtcNow;
        var items = normalized.Select(item => EvidenceItem.Create(
            incidentId,
            EvidenceType.Log,
            "Loki",
            item.SourceReference,
            item.ObservedAt,
            item.Summary,
            now,
            item.SourceTraceId,
            item.SourceSpanId,
            item.SourceService)).ToArray();
        var persisted = await evidenceRepository.AddMissingAsync(items, cancellationToken);

        return new(
            ImportLogEvidenceStatus.Imported,
            persisted.Select(item => new AddEvidenceResult(
                EvidenceDetails.From(item.Evidence), item.WasCreated)).ToArray(),
            logs.Count,
            normalized.Count);
    }

    private static void ValidateRange(DateTimeOffset from, DateTimeOffset to)
    {
        if (from == default || to == default || from > to)
        {
            throw new ArgumentException("A valid log query time range is required.");
        }

        if (to - from > TimeSpan.FromHours(24))
        {
            throw new ArgumentException("A log query range cannot exceed 24 hours.");
        }
    }
}
