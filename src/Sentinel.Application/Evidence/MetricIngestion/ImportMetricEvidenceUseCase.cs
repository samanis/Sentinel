using Sentinel.Application.Abstractions;
using Sentinel.Application.Evidence.AddEvidence;
using Sentinel.Application.Incidents;
using Sentinel.Domain.Evidence;
using Sentinel.Domain.Incidents;

namespace Sentinel.Application.Evidence.MetricIngestion;

public enum ImportMetricEvidenceStatus { Imported = 1, IncidentNotFound = 2 }

public sealed record ImportMetricEvidenceResult(
    ImportMetricEvidenceStatus Status,
    IReadOnlyList<AddEvidenceResult> Evidence,
    int MetricCount,
    int EligibleMetricCount);

public sealed class ImportMetricEvidenceUseCase(
    IMetricSource metricSource,
    IMetricEvidenceNormalizer normalizer,
    IIncidentRepository incidentRepository,
    IEvidenceRepository evidenceRepository,
    IClock clock)
{
    public async Task<ImportMetricEvidenceResult> ExecuteAsync(
        IncidentId incidentId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        ValidateRange(from, to);
        var incident = await incidentRepository.GetByIdAsync(incidentId, cancellationToken);
        if (incident is null) return new(ImportMetricEvidenceStatus.IncidentNotFound, [], 0, 0);

        var metrics = await metricSource.QueryAsync(
            new MetricQuery(incident.Service, from, to), cancellationToken);
        var normalized = normalizer.Normalize(incidentId, metrics, from, to);
        var now = clock.UtcNow;
        var items = normalized.Select(item => EvidenceItem.Create(
            incidentId, EvidenceType.Metric, "Prometheus", item.SourceReference,
            item.ObservedAt, item.Summary, now, sourceService: item.SourceService)).ToArray();
        var persisted = await evidenceRepository.AddMissingAsync(items, cancellationToken);
        return new(
            ImportMetricEvidenceStatus.Imported,
            persisted.Select(item => new AddEvidenceResult(
                EvidenceDetails.From(item.Evidence), item.WasCreated)).ToArray(),
            metrics.Count,
            normalized.Count);
    }

    private static void ValidateRange(DateTimeOffset from, DateTimeOffset to)
    {
        if (from == default || to == default || from > to)
            throw new ArgumentException("A valid metric query time range is required.");
        if (to - from > TimeSpan.FromHours(24))
            throw new ArgumentException("A metric query range cannot exceed 24 hours.");
        if (to - from < TimeSpan.FromMinutes(1))
            throw new ArgumentException("A metric query range must be at least one minute.");
    }
}
