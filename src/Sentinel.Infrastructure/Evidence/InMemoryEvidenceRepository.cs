using System.Collections.Concurrent;
using Sentinel.Application.Evidence;
using Sentinel.Domain.Evidence;
using Sentinel.Domain.Incidents;

namespace Sentinel.Infrastructure.Evidence;

public sealed class InMemoryEvidenceRepository : IEvidenceRepository
{
    private readonly ConcurrentDictionary<EvidenceId, EvidenceItem> _items = new();
    private readonly object _writeLock = new();

    public Task<IReadOnlyList<EvidencePersistenceResult>> AddMissingAsync(
        IReadOnlyCollection<EvidenceItem> evidence,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(evidence);
        var results = new List<EvidencePersistenceResult>(evidence.Count);
        lock (_writeLock)
        {
            foreach (var item in evidence)
            {
                var existing = _items.Values.SingleOrDefault(candidate =>
                    candidate.IncidentId == item.IncidentId &&
                    candidate.ContentHash == item.ContentHash);
                if (existing is not null)
                {
                    results.Add(new EvidencePersistenceResult(existing, false));
                    continue;
                }

                _items.TryAdd(item.Id, item);
                results.Add(new EvidencePersistenceResult(item, true));
            }
        }

        return Task.FromResult<IReadOnlyList<EvidencePersistenceResult>>(results);
    }

    public Task<EvidenceItem?> GetByIdAsync(
        EvidenceId id,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _items.TryGetValue(id, out var evidence);
        return Task.FromResult(evidence);
    }

    public Task<IReadOnlyList<EvidenceItem>> ListByIncidentIdAsync(
        IncidentId incidentId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<EvidenceItem> evidence = _items.Values
            .Where(item => item.IncidentId == incidentId)
            .OrderBy(item => item.ObservedAt)
            .ThenBy(item => item.Id.Value)
            .ToArray();
        return Task.FromResult(evidence);
    }
}
