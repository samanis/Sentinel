using Microsoft.EntityFrameworkCore;
using Sentinel.Application.Evidence;
using Sentinel.Domain.Evidence;
using Sentinel.Domain.Incidents;

namespace Sentinel.Infrastructure.Persistence;

public sealed class PostgresEvidenceRepository(SentinelDbContext dbContext)
    : IEvidenceRepository
{
    public async Task<IReadOnlyList<EvidencePersistenceResult>> AddMissingAsync(
        IReadOnlyCollection<EvidenceItem> evidence,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (evidence.Count == 0)
        {
            return [];
        }

        var incidentIds = evidence.Select(item => item.IncidentId).Distinct().ToArray();
        if (incidentIds.Length != 1)
        {
            throw new ArgumentException(
                "An atomic Evidence batch must belong to one incident.",
                nameof(evidence));
        }

        var hashes = evidence.Select(item => item.ContentHash).Distinct().ToArray();
        var tempoTraceIds = evidence
            .Where(IsTempoTrace)
            .Select(item => item.SourceTraceId)
            .Where(traceId => traceId is not null)
            .Distinct()
            .ToArray();
        var existing = await dbContext.Evidence
            .AsNoTracking()
            .Where(item => item.IncidentId == incidentIds[0] &&
                (hashes.Contains(item.ContentHash) ||
                    (item.Type == EvidenceType.Trace && item.SourceSystem == "Tempo" &&
                        tempoTraceIds.Contains(item.SourceTraceId))))
            .ToArrayAsync(cancellationToken);
        var existingByHash = existing.ToDictionary(item => item.ContentHash);
        var existingByProvenance = existing
            .Where(item => IsTempoTrace(item) &&
                item.SourceTraceId is not null && item.SourceSpanId is not null)
            .ToDictionary(item => (item.SourceTraceId!, item.SourceSpanId!));
        var results = new List<EvidencePersistenceResult>(evidence.Count);

        foreach (var item in evidence)
        {
            if (existingByHash.TryGetValue(item.ContentHash, out var stored) ||
                (IsTempoTrace(item) && item.SourceTraceId is not null && item.SourceSpanId is not null &&
                    existingByProvenance.TryGetValue(
                        (item.SourceTraceId, item.SourceSpanId),
                        out stored)))
            {
                results.Add(new EvidencePersistenceResult(stored, false));
                continue;
            }

            dbContext.Evidence.Add(item);
            existingByHash[item.ContentHash] = item;
            if (IsTempoTrace(item) && item.SourceTraceId is not null && item.SourceSpanId is not null)
            {
                existingByProvenance[(item.SourceTraceId, item.SourceSpanId)] = item;
            }
            results.Add(new EvidencePersistenceResult(item, true));
        }

        // A single SaveChanges call is transactional: either the full normalized
        // trace batch is committed or none of it is.
        await dbContext.SaveChangesAsync(cancellationToken);
        return results;
    }

    private static bool IsTempoTrace(EvidenceItem item) =>
        item.Type == EvidenceType.Trace && item.SourceSystem == "Tempo";

    public Task<EvidenceItem?> GetByIdAsync(
        EvidenceId id,
        CancellationToken cancellationToken) =>
        dbContext.Evidence
            .AsNoTracking()
            .SingleOrDefaultAsync(evidence => evidence.Id == id, cancellationToken);

    public async Task<IReadOnlyList<EvidenceItem>> ListByIncidentIdAsync(
        IncidentId incidentId,
        CancellationToken cancellationToken) =>
        await dbContext.Evidence
            .AsNoTracking()
            .Where(evidence => evidence.IncidentId == incidentId)
            .OrderBy(evidence => evidence.ObservedAt)
            .ThenBy(evidence => evidence.Id)
            .ToArrayAsync(cancellationToken);
}
