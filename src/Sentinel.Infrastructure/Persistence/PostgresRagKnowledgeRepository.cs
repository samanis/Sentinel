using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using Sentinel.Application.Rag;

namespace Sentinel.Infrastructure.Persistence;

public sealed class PostgresRagKnowledgeRepository(SentinelDbContext dbContext)
    : IRagKnowledgeRepository
{
    public async Task<IReadOnlyList<RagEvidenceMatch>> SearchRecentAsync(
        string? service,
        string? environment,
        int limit,
        CancellationToken cancellationToken)
    {
        var bundles = dbContext.Set<EvidenceBundleRecord>().AsNoTracking()
            .Where(item => item.Status == "Completed" && item.Embedding != null);
        if (service is not null) bundles = bundles.Where(item => item.Service == service);
        if (environment is not null) bundles = bundles.Where(item => item.Environment == environment);

        var matches = await (
            from bundle in bundles
            join run in dbContext.IngestionRuns.AsNoTracking()
                on bundle.IngestionRunId equals run.Id
            join alert in dbContext.AlertOccurrences.AsNoTracking()
                on run.AlertOccurrenceId equals alert.Id
            orderby alert.StartedAt descending
            select new RagEvidenceMatch(
                bundle.Id, bundle.IngestionRunId.Value, bundle.AlertName,
                bundle.Service, bundle.Environment, bundle.SearchDocument,
                bundle.EmbeddingModel!, 0d, alert.StartedAt))
            .Take(limit)
            .ToListAsync(cancellationToken);
        return await EnrichRecurrenceAsync(matches, cancellationToken);
    }

    public async Task<IReadOnlyList<RagEvidenceMatch>> SearchAsync(
        float[] embedding,
        string embeddingModel,
        string? service,
        string? environment,
        int limit,
        CancellationToken cancellationToken)
    {
        var queryVector = new Vector(embedding);
        var query = dbContext.Set<EvidenceBundleRecord>().AsNoTracking()
            .Where(item => item.Status == "Completed" &&
                           item.Embedding != null &&
                           item.EmbeddingModel == embeddingModel);

        if (service is not null)
            query = query.Where(item => item.Service == service);
        if (environment is not null)
            query = query.Where(item => item.Environment == environment);

        var matches = await (
            from item in query
            join run in dbContext.IngestionRuns.AsNoTracking()
                on item.IngestionRunId equals run.Id
            join alert in dbContext.AlertOccurrences.AsNoTracking()
                on run.AlertOccurrenceId equals alert.Id
            select new
            {
                item.Id,
                item.IngestionRunId,
                item.AlertName,
                item.Service,
                item.Environment,
                item.SearchDocument,
                item.EmbeddingModel,
                CreatedAt = alert.StartedAt,
                Distance = item.Embedding!.CosineDistance(queryVector)
            })
            .OrderBy(item => item.Distance)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var result = matches.Select(item => new RagEvidenceMatch(
            item.Id,
            item.IngestionRunId.Value,
            item.AlertName,
            item.Service,
            item.Environment,
            item.SearchDocument,
            item.EmbeddingModel!,
            1d - item.Distance,
            item.CreatedAt)).ToArray();
        return await EnrichRecurrenceAsync(result, cancellationToken);
    }

    private async Task<IReadOnlyList<RagEvidenceMatch>> EnrichRecurrenceAsync(
        IReadOnlyList<RagEvidenceMatch> matches,
        CancellationToken cancellationToken)
    {
        if (matches.Count == 0) return matches;
        var bundleIds = matches.Select(item => item.BundleId).ToArray();
        var memberships = await dbContext.IncidentClusterOccurrences.AsNoTracking()
            .Where(item => bundleIds.Contains(item.EvidenceBundleId))
            .ToDictionaryAsync(item => item.EvidenceBundleId, cancellationToken);
        if (memberships.Count == 0) return matches;
        var clusterIds = memberships.Values.Select(item => item.ClusterId).Distinct().ToArray();
        var clusters = await dbContext.IncidentClusters.AsNoTracking()
            .Where(item => clusterIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var oneHourAgo = DateTimeOffset.UtcNow.AddHours(-1);
        var lastHourCounts = await dbContext.IncidentClusterOccurrences.AsNoTracking()
            .Where(item => clusterIds.Contains(item.ClusterId) && item.OccurredAt >= oneHourAgo)
            .GroupBy(item => item.ClusterId)
            .Select(group => new { ClusterId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.ClusterId, item => item.Count, cancellationToken);

        return matches.Select(item =>
        {
            if (!memberships.TryGetValue(item.BundleId, out var membership) ||
                !clusters.TryGetValue(membership.ClusterId, out var cluster)) return item;
            return item with
            {
                ClusterId = cluster.Id,
                OccurrenceCount = cluster.OccurrenceCount,
                OccurrencesLastHour = lastHourCounts.GetValueOrDefault(cluster.Id),
                FirstSeenAt = cluster.FirstSeenAt,
                LastSeenAt = cluster.LastSeenAt
            };
        }).ToArray();
    }
}
